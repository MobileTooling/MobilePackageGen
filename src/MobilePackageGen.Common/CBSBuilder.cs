using DiscUtils;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace MobilePackageGen
{
    public class CBSBuilder
    {
        private static bool USE_UNCOMPRESSED_MANIFEST_FILES = false;

        private static string GetCBSComponentName(XmlMum.Assembly cbs)
        {
            return $"{cbs.AssemblyIdentity.Name}~{cbs.AssemblyIdentity.PublicKeyToken}~{cbs.AssemblyIdentity.ProcessorArchitecture}~{(cbs.AssemblyIdentity.Language == "neutral" ? "" : cbs.AssemblyIdentity.Language)}~{cbs.AssemblyIdentity.Version}";
        }

        private static List<CabinetFileInfo> GetCabinetFileInfoForCbsPackage(XmlMum.Assembly cbs, IPartition partition, IEnumerable<IDisk> disks)
        {
            List<CabinetFileInfo> fileMappings = [];

            IFileSystem fileSystem = partition.FileSystem!;

            string WindowsServicingPackagesFolderPath = @"Windows\servicing\Packages";
            string WindowsSideBySideManifestsFolderPath = @"Windows\WinSxS\Manifests";

            string packageName = GetCBSComponentName(cbs);

            int i = 0;

            double oldPercentage = uint.MaxValue;

            if (cbs.Package.CustomInformation != null)
            {
                foreach (XmlMum.File packageFile in cbs.Package.CustomInformation.File)
                {
                    double percentage = (double)i++ * 50 / cbs.Package.CustomInformation.File.Count;

                    if (percentage != oldPercentage)
                    {
                        oldPercentage = percentage;
                        string progressBarString = Logging.GetDISMLikeProgressBar(percentage);

                        Logging.Log(progressBarString, returnLine: false);
                    }

                    // File names in cab files are all lower case
                    string fileName = packageFile.Name.ToLower();

                    // Prevent getting files from root of this program
                    if (fileName.StartsWith('\\'))
                    {
                        fileName = fileName[1..];
                    }

                    bool isManifest = false;

                    // If a manifest file is without any path, it must be retrieved from the manifest directory
                    if (!fileName.Contains('\\') && fileName.EndsWith(".manifest"))
                    {
                        isManifest = true;
                        fileName = Path.Combine(WindowsSideBySideManifestsFolderPath, fileName);
                    }

                    // We now replace macros with known values
                    string normalized = fileName.Replace("$(runtime.bootdrive)", "")
                                                        .Replace("$(runtime.systemroot)", "windows")
                                                        .Replace("$(runtime.fonts)", @"windows\fonts")
                                                        .Replace("$(runtime.inf)", @"windows\inf")
                                                        .Replace("$(runtime.system)", @"windows\system")
                                                        .Replace("$(runtime.system32)", @"windows\system32")
                                                        .Replace("$(runtime.wbem)", @"windows\system32\wbem")
                                                        .Replace("$(runtime.drivers)", @"windows\system32\drivers")
                                                        .Replace("$(runtime.programfiles)", "Program Files")
                                                        .Replace("$(runtime.programdata)", "ProgramData")
                                                        .Replace("$(runtime.startmenu)", @"ProgramData\Microsoft\Windows\Start Menu");

                    if (cbs.AssemblyIdentity.ProcessorArchitecture.Equals("arm64.arm"))
                    {
                        normalized = fileName.Replace("$(runtime.bootdrive)", "")
                                                        .Replace("$(runtime.systemroot)", "windows")
                                                        .Replace("$(runtime.fonts)", @"windows\fonts")
                                                        .Replace("$(runtime.inf)", @"windows\inf")
                                                        .Replace("$(runtime.system)", @"windows\system")
                                                        .Replace("$(runtime.system32)", @"windows\sysarm32")
                                                        .Replace("$(runtime.wbem)", @"windows\sysarm32\wbem")
                                                        .Replace("$(runtime.drivers)", @"windows\sysarm32\drivers")
                                                        .Replace("$(runtime.programfiles)", "Program Files (Arm)")
                                                        .Replace("$(runtime.programdata)", "ProgramData")
                                                        .Replace("$(runtime.startmenu)", @"ProgramData\Microsoft\Windows\Start Menu");
                    }

                    if (cbs.AssemblyIdentity.ProcessorArchitecture.Equals("arm64.x86"))
                    {
                        normalized = fileName.Replace("$(runtime.bootdrive)", "")
                                                        .Replace("$(runtime.systemroot)", "windows")
                                                        .Replace("$(runtime.fonts)", @"windows\fonts")
                                                        .Replace("$(runtime.inf)", @"windows\inf")
                                                        .Replace("$(runtime.system)", @"windows\system")
                                                        .Replace("$(runtime.system32)", @"windows\syswow64")
                                                        .Replace("$(runtime.wbem)", @"windows\syswow64\wbem")
                                                        .Replace("$(runtime.drivers)", @"windows\syswow64\drivers")
                                                        .Replace("$(runtime.programfiles)", "Program Files (x86)")
                                                        .Replace("$(runtime.programdata)", "ProgramData")
                                                        .Replace("$(runtime.startmenu)", @"ProgramData\Microsoft\Windows\Start Menu");
                    }

                    // Prevent getting files from root of this program
                    if (normalized.StartsWith('\\'))
                    {
                        normalized = normalized[1..];
                    }

                    // The package name is renamed to "update" in cab files, fix this
                    if (normalized.EndsWith("update.mum"))
                    {
                        normalized = normalized.Replace("update.mum", $"{packageName}.mum");

                        if (!normalized.Contains('\\'))
                        {
                            normalized = Path.Combine(WindowsServicingPackagesFolderPath, normalized);
                        }
                    }

                    // Same here for the catalog
                    if (normalized.EndsWith("update.cat"))
                    {
                        normalized = normalized.Replace("update.cat", $"{packageName}.cat");

                        if (!normalized.Contains('\\'))
                        {
                            normalized = Path.Combine(WindowsServicingPackagesFolderPath, normalized);
                        }
                    }

                    // For specific wow sub architecures, we want to fetch the files from the right place on the file system
                    string architecture = cbs.Package.Update?.Component?.AssemblyIdentity?.ProcessorArchitecture!;

                    if (normalized.StartsWith(@"windows\system32") && architecture?.Contains("arm64.arm") == true)
                    {
                        string newpath = normalized.Replace(@"windows\system32", @"windows\sysarm32");
                        if (fileSystem.FileExists(newpath))
                        {
                            normalized = newpath;
                        }
                    }

                    // Prevent getting files from root of this program
                    if (normalized.StartsWith('\\'))
                    {
                        normalized = normalized[1..];
                    }

                    if (!fileSystem.Exists(normalized) && normalized.EndsWith(".manifest"))
                    {
                        normalized = Path.Combine(WindowsSideBySideManifestsFolderPath, normalized.Split('\\')[^1]);
                    }

                    if (!fileSystem.Exists(normalized) && architecture?.Contains("arm64.arm") == true && fileSystem.Exists(normalized.Replace(@"windows\sysarm32", @"windows\system32")))
                    {
                        normalized = normalized.Replace(@"windows\sysarm32", @"windows\system32");
                    }

                    CabinetFileInfo? cabinetFileInfo = null;

                    // If we end in bin, and the package is marked binary partition, this is a partition on one of the device disks, retrieve it
                    if (normalized.EndsWith(".bin") && cbs.Package.BinaryPartition.Equals("true", StringComparison.CurrentCultureIgnoreCase))
                    {
                        foreach (IDisk disk in disks)
                        {
                            bool done = false;

                            foreach (IPartition diskPartition in disk.Partitions)
                            {
                                if (diskPartition.Name.Split("\0")[0].Equals(cbs.Package.TargetPartition, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    done = true;

                                    diskPartition.Stream.Seek(0, SeekOrigin.Begin);

                                    cabinetFileInfo = new CabinetFileInfo()
                                    {
                                        FileName = packageFile.Cabpath,
                                        FileStream = new Substream(diskPartition.Stream, long.Parse(packageFile.Size)),
                                        Attributes = FileAttributes.Normal,
                                        DateTime = DateTime.Now
                                    };
                                    break;
                                }
                            }

                            if (done)
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        if (!fileSystem.FileExists(normalized))
                        {
                            string[] partitionNamesWithLinks = ["data", "efiesp", "osdata", "dpp", "mmos"];

                            foreach (string partitionNameWithLink in partitionNamesWithLinks)
                            {
                                if (normalized.StartsWith($"{partitionNameWithLink}\\", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    foreach (IDisk disk in disks)
                                    {
                                        bool done = false;

                                        foreach (IPartition diskPartition in disk.Partitions)
                                        {
                                            if (diskPartition.Name.Split("\0")[0].Equals(partitionNameWithLink, StringComparison.InvariantCultureIgnoreCase))
                                            {
                                                done = true;

                                                IFileSystem? fileSystemData = diskPartition.FileSystem;

                                                if (fileSystemData == null)
                                                {
                                                    break;
                                                }

                                                string targetFile = normalized[(partitionNameWithLink.Length + 1)..];

                                                if (!fileSystemData.FileExists(targetFile))
                                                {
                                                    break;
                                                }

                                                cabinetFileInfo = new CabinetFileInfo()
                                                {
                                                    FileName = packageFile.Cabpath,
                                                    FileStream = fileSystemData.OpenFile(targetFile, FileMode.Open, FileAccess.Read),
                                                    Attributes = fileSystemData.GetAttributes(targetFile) & ~FileAttributes.ReparsePoint,
                                                    DateTime = fileSystemData.GetLastWriteTime(targetFile)
                                                };

                                                break;
                                            }
                                        }

                                        if (done)
                                        {
                                            break;
                                        }
                                    }

                                    break;
                                }
                            }
                        }
                        else
                        {
                            Stream fileStream = fileSystem.OpenFile(normalized, FileMode.Open, FileAccess.Read);
                            if (isManifest && USE_UNCOMPRESSED_MANIFEST_FILES)
                            {
                                // LibSxS is only supported on Windows
                                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                                {
                                    throw new NotImplementedException("The compression algorithm this data block uses is not currently implemented.");
                                }

                                fileStream = LibSxS.Delta.DeltaAPI.LoadManifest(fileStream);
                            }

                            cabinetFileInfo = new CabinetFileInfo()
                            {
                                FileName = packageFile.Cabpath,
                                FileStream = fileStream,
                                Attributes = fileSystem.GetAttributes(normalized) & ~FileAttributes.ReparsePoint,
                                DateTime = fileSystem.GetLastWriteTime(normalized)
                            };
                        }
                    }

                    if (cabinetFileInfo != null)
                    {
                        fileMappings.Add(cabinetFileInfo);
                    }
                    else
                    {
                        Logging.Log($"\rError: File not found! {normalized}\n", LoggingLevel.Error);
                    }
                }
            }

            return fileMappings;
        }

        public static void BuildCBS(IEnumerable<IDisk> disks, string destination_path, UpdateHistory.UpdateHistory? updateHistory)
        {
            Logging.Log();
            Logging.Log("Building CBS Cabinet Files...");
            Logging.Log();

            BuildCabinets(disks, destination_path, updateHistory);

            Logging.Log();
            Logging.Log("Cleaning up...");
            Logging.Log();

            TempManager.CleanupTempFiles();
        }

        private static List<IPartition> GetPartitionsWithServicing(IEnumerable<IDisk> disks)
        {
            List<IPartition> fileSystemsWithServicing = [];

            foreach (IDisk disk in disks)
            {
                foreach (IPartition partition in disk.Partitions)
                {
                    IFileSystem? fileSystem = partition.FileSystem;

                    if (fileSystem != null)
                    {
                        try
                        {
                            if (fileSystem.DirectoryExists(@"Windows\Servicing\Packages"))
                            {
                                fileSystemsWithServicing.Add(partition);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logging.Log($"Error: Looking up file system servicing failed! {ex.Message}", LoggingLevel.Error);
                        }
                    }
                }
            }

            return fileSystemsWithServicing;
        }

        private static int GetPackageCount(IEnumerable<IDisk> disks)
        {
            int count = 0;

            IEnumerable<IPartition> partitionsWithCbsServicing = GetPartitionsWithServicing(disks);

            foreach (IPartition partition in partitionsWithCbsServicing)
            {
                IFileSystem fileSystem = partition.FileSystem!;

                IEnumerable<string> manifestFiles = fileSystem.GetFilesWithNtfsIssueWorkaround(@"Windows\servicing\Packages", "*.mum", SearchOption.TopDirectoryOnly);

                count += manifestFiles.Count();
            }

            return count;
        }

        private static void BuildCabinets(IEnumerable<IDisk> disks, string outputPath, UpdateHistory.UpdateHistory? updateHistory)
        {
            int packagesCount = GetPackageCount(disks);

            IEnumerable<IPartition> partitionsWithCbsServicing = GetPartitionsWithServicing(disks);

            int i = 0;

            foreach (IPartition partition in partitionsWithCbsServicing)
            {
                IFileSystem fileSystem = partition.FileSystem!;

                IEnumerable<string> manifestFiles = fileSystem.GetFilesWithNtfsIssueWorkaround(@"Windows\servicing\Packages", "*.mum", SearchOption.TopDirectoryOnly);

                foreach (string manifestFile in manifestFiles)
                {
                    try
                    {
                        Stream stream = fileSystem.OpenFile(manifestFile, FileMode.Open, FileAccess.Read);
                        XmlSerializer serializer = new(typeof(XmlMum.Assembly));
                        XmlMum.Assembly cbs = (XmlMum.Assembly)serializer.Deserialize(stream)!;

                        (string cabFileName, string cabFile) = BuildMetadataHandler.GetPackageNamingForCBS(cbs, updateHistory);

                        if (string.IsNullOrEmpty(cabFileName) && string.IsNullOrEmpty(cabFile))
                        {
                            string packageName = $"{cbs.AssemblyIdentity.Name.Replace($"_{cbs.AssemblyIdentity.Language}", "", StringComparison.InvariantCultureIgnoreCase)}";

                            if (!packageName.Contains("InboxCompDB"))
                            {
                                packageName = $"{packageName}~{cbs.AssemblyIdentity.PublicKeyToken.Replace("628844477771337a", "31bf3856ad364e35", StringComparison.InvariantCultureIgnoreCase)}~{cbs.AssemblyIdentity.ProcessorArchitecture}~{(cbs.AssemblyIdentity.Language == "neutral" ? "" : cbs.AssemblyIdentity.Language)}~";
                            }

                            string partitionName = partition.Name.Replace("\0", "-");

                            if (!string.IsNullOrEmpty(cbs.Package.TargetPartition))
                            {
                                partitionName = cbs.Package.TargetPartition;
                            }

                            cabFileName = Path.Combine(partitionName, packageName);

                            cabFile = Path.Combine(outputPath, $"{cabFileName}.cab");
                        }
                        else
                        {
                            cabFile = Path.Combine(outputPath, cabFile);
                        }

                        string componentStatus = $"Creating package {i + 1} of {packagesCount} - {Path.GetFileName(cabFileName)}";
                        if (componentStatus.Length > Console.BufferWidth - 24 - 1)
                        {
                            componentStatus = $"{componentStatus[..(Console.BufferWidth - 24 - 4)]}...";
                        }

                        Logging.Log(componentStatus);
                        string progressBarString = Logging.GetDISMLikeProgressBar(0);
                        Logging.Log(progressBarString, returnLine: false);

                        string fileStatus = "";

                        if (!File.Exists(cabFile))
                        {
                            IEnumerable<CabinetFileInfo> fileMappings = GetCabinetFileInfoForCbsPackage(cbs, partition, disks);

                            // Cab Creation is only supported on Windows
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            {
                                if (fileMappings.Any())
                                {
                                    if (Path.GetDirectoryName(cabFile) is string directory && !Directory.Exists(directory))
                                    {
                                        Directory.CreateDirectory(directory);
                                    }

                                    if (ContainerOSWrapperBuilder.IsWrapperPackage(cbs))
                                    {
                                        // This is a Wrapper package that needs to be contained in a wim.
                                        // First index of a wim wraper package contains said cbs component
                                        // (same content as the cab we would have generated normally
                                        // Second index contains the whole content of the directory in
                                        // the windows installation at the "ApplyTo" location
                                        // Note: The ApplyTo path is relative to WINDIR (ApplyTo can be equal to
                                        // "\Containers\Vail\BaseLayer" and the directory is in "C:\Windows\Containers\Vail\BaseLayer"

                                        // Still build the cab for now
                                        CabinetBuilder.BuildCab(cabFile, fileMappings, ref fileStatus);

                                        ContainerOSWrapperBuilder.BuildWrapper(cabFile, fileMappings, ref fileStatus, partition, cbs.Package.ApplyTo);
                                    }
                                    else
                                    {
                                        CabinetBuilder.BuildCab(cabFile, fileMappings, ref fileStatus);
                                    }
                                }
                            }

                            foreach (CabinetFileInfo fileMapping in fileMappings)
                            {
                                fileMapping.FileStream.Close();
                            }
                        }
                        else
                        {
                            Logging.Log($"CAB already exists! Skipping. {cabFile}", LoggingLevel.Warning);
                        }

                        if (i != packagesCount - 1)
                        {
                            Console.SetCursorPosition(0, Console.CursorTop - 1);

                            Logging.Log(new string(' ', componentStatus.Length));
                            Logging.Log(Logging.GetDISMLikeProgressBar(100));

                            if (string.IsNullOrEmpty(fileStatus))
                            {
                                Logging.Log(new string(' ', fileStatus.Length));
                                Logging.Log(new string(' ', 60));
                            }
                            else
                            {
                                Logging.Log(new string(' ', fileStatus.Length));
                                Logging.Log(Logging.GetDISMLikeProgressBar(100));
                            }

                            Console.SetCursorPosition(0, Console.CursorTop - 4);
                        }
                        else
                        {
                            Logging.Log($"\r{Logging.GetDISMLikeProgressBar(100)}");

                            if (string.IsNullOrEmpty(fileStatus))
                            {
                                Logging.Log();
                                Logging.Log(new string(' ', 60));
                            }
                            else
                            {
                                Logging.Log();
                                Logging.Log(Logging.GetDISMLikeProgressBar(100));
                            }
                        }

                        i++;
                    }
                    catch (Exception ex)
                    {
                        Logging.Log($"Error: CAB creation failed! {ex.Message}", LoggingLevel.Error);
                    }
                }
            }
        }
    }
}
