using DiscUtils;
using MobilePackageGen.GZip;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace MobilePackageGen
{
    public class SPKGBuilder
    {
        private static string GetSPKGComponentName(XmlDsm.Package dsm)
        {
            return $"{dsm.Identity.Owner}" +
                $"{(string.IsNullOrEmpty(dsm.Identity.Component) ? "" : $".{dsm.Identity.Component}")}" +
                $"{(string.IsNullOrEmpty(dsm.Identity.SubComponent) ? "" : $".{dsm.Identity.SubComponent}")}" +
                $"{(string.IsNullOrEmpty(dsm.Culture) == true ? "" : $"_Lang_{dsm.Culture}")}" +
                $"{(string.IsNullOrEmpty(dsm.Resolution) == true ? "" : $"_Res_{dsm.Resolution}")}";
        }

        private static List<CabinetFileInfo> GetCabinetFileInfoForDsmPackage(XmlDsm.Package dsm, IPartition partition, IEnumerable<IDisk> disks)
        {
            List<CabinetFileInfo> fileMappings = [];

            IFileSystem fileSystem = partition.FileSystem!;

            int i = 0;

            double oldPercentage = uint.MaxValue;

            bool hasSeenManifest = false;
            bool hasSeenCatalog = false;

            foreach (XmlDsm.FileEntry packageFile in dsm.Files.FileEntry)
            {
                double percentage = (double)i++ * 50 / dsm.Files.FileEntry.Count;

                if (percentage != oldPercentage)
                {
                    oldPercentage = percentage;
                    string progressBarString = Logging.GetDISMLikeProgressBar(percentage);

                    Logging.Log(progressBarString, returnLine: false);
                }

                string fileName = packageFile.DevicePath;

                string normalized = fileName;

                // Prevent getting files from root of this program
                if (normalized.StartsWith('\\'))
                {
                    normalized = normalized[1..];
                }

                List<string> normalizedParts = [];

                foreach (string part in normalized.Split('\\'))
                {
                    if (part == ".")
                    {
                        continue;
                    }

                    if (part == "..")
                    {
                        normalizedParts.RemoveAt(normalizedParts.Count - 1);
                        continue;
                    }

                    normalizedParts.Add(part);
                }

                normalized = string.Join("\\", normalizedParts);

                CabinetFileInfo? cabinetFileInfo = null;

                string fileType = packageFile.FileType ?? packageFile.Type ?? "";

                if (!hasSeenManifest && fileType.Equals("Manifest", StringComparison.InvariantCultureIgnoreCase))
                {
                    hasSeenManifest = true;
                }

                if (!hasSeenCatalog && fileType.Equals("Catalog", StringComparison.InvariantCultureIgnoreCase))
                {
                    hasSeenCatalog = true;
                }

                // If we end in bin, and the package is marked binary partition, this is a partition on one of the device disks, retrieve it
                if (normalized.EndsWith(".bin") && fileType.Contains("BinaryPartition", StringComparison.CurrentCultureIgnoreCase))
                {
                    foreach (IDisk disk in disks)
                    {
                        bool done = false;

                        foreach (IPartition diskPartition in disk.Partitions)
                        {
                            if (diskPartition.Name.Split("\0")[0].Equals(dsm.Partition, StringComparison.InvariantCultureIgnoreCase))
                            {
                                done = true;

                                // Some older SPKGs from 2012 / 2013 may lack the packageFile.FileSize property entirely
                                // in their DSM file. In this case, we will use the size of the partition itself.
                                // It may also be possible to infer the correct size from the catalog
                                // if the size is not meant to be the full size of the partition.

                                Stream partitionStream = packageFile.FileSize != null ? new Substream(diskPartition.Stream, long.Parse(packageFile.FileSize)) : diskPartition.Stream;

                                diskPartition.Stream.Seek(0, SeekOrigin.Begin);

                                cabinetFileInfo = new CabinetFileInfo()
                                {
                                    FileName = packageFile.CabPath,
                                    FileStream = partitionStream,
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

                                            bool needsDecompression = fileType.Contains("registry", StringComparison.CurrentCultureIgnoreCase) || fileType.Contains("policy", StringComparison.CurrentCultureIgnoreCase) || fileType.Contains("manifest", StringComparison.CurrentCultureIgnoreCase);
                                            bool doesNotNeedDecompression = fileType.Contains("catalog", StringComparison.CurrentCultureIgnoreCase) || fileType.Contains("regular", StringComparison.CurrentCultureIgnoreCase);

                                            if (needsDecompression)
                                            {
                                                Stream? cabFileStream = null;

                                                try
                                                {
                                                    cabFileStream = fileSystemData.OpenFileAndDecompressAsGZip(targetFile);
                                                }
                                                catch (InvalidDataException)
                                                {
                                                    cabFileStream = fileSystemData.OpenFile(targetFile, FileMode.Open, FileAccess.Read);
                                                }

                                                cabinetFileInfo = new CabinetFileInfo()
                                                {
                                                    FileName = packageFile.CabPath,
                                                    FileStream = cabFileStream,
                                                    Attributes = fileSystemData.GetAttributes(targetFile) & ~FileAttributes.ReparsePoint,
                                                    DateTime = fileSystemData.GetLastWriteTime(targetFile)
                                                };
                                            }
                                            else if (doesNotNeedDecompression)
                                            {
                                                cabinetFileInfo = new CabinetFileInfo()
                                                {
                                                    FileName = packageFile.CabPath,
                                                    FileStream = fileSystemData.OpenFile(targetFile, FileMode.Open, FileAccess.Read),
                                                    Attributes = fileSystemData.GetAttributes(targetFile) & ~FileAttributes.ReparsePoint,
                                                    DateTime = fileSystemData.GetLastWriteTime(targetFile)
                                                };
                                            }
                                            else
                                            {
                                                cabinetFileInfo = new CabinetFileInfo()
                                                {
                                                    FileName = packageFile.CabPath,
                                                    FileStream = fileSystemData.OpenFile(targetFile, FileMode.Open, FileAccess.Read),
                                                    Attributes = fileSystemData.GetAttributes(targetFile) & ~FileAttributes.ReparsePoint,
                                                    DateTime = fileSystemData.GetLastWriteTime(targetFile)
                                                };
                                            }

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
                        bool needsDecompression = fileType.Contains("registry", StringComparison.CurrentCultureIgnoreCase) || fileType.Contains("policy", StringComparison.CurrentCultureIgnoreCase) || fileType.Contains("manifest", StringComparison.CurrentCultureIgnoreCase);
                        bool doesNotNeedDecompression = fileType.Contains("catalog", StringComparison.CurrentCultureIgnoreCase) || fileType.Contains("regular", StringComparison.CurrentCultureIgnoreCase);

                        if (needsDecompression)
                        {
                            Stream? cabFileStream = null;

                            try
                            {
                                cabFileStream = fileSystem.OpenFileAndDecompressAsGZip(normalized);
                            }
                            catch (InvalidDataException)
                            {
                                cabFileStream = fileSystem.OpenFile(normalized, FileMode.Open, FileAccess.Read);
                            }

                            cabinetFileInfo = new CabinetFileInfo()
                            {
                                FileName = packageFile.CabPath,
                                FileStream = cabFileStream,
                                Attributes = fileSystem.GetAttributes(normalized) & ~FileAttributes.ReparsePoint,
                                DateTime = fileSystem.GetLastWriteTime(normalized)
                            };
                        }
                        else if (doesNotNeedDecompression)
                        {
                            cabinetFileInfo = new CabinetFileInfo()
                            {
                                FileName = packageFile.CabPath,
                                FileStream = fileSystem.OpenFile(normalized, FileMode.Open, FileAccess.Read),
                                Attributes = fileSystem.GetAttributes(normalized) & ~FileAttributes.ReparsePoint,
                                DateTime = fileSystem.GetLastWriteTime(normalized)
                            };
                        }
                        else
                        {
                            cabinetFileInfo = new CabinetFileInfo()
                            {
                                FileName = packageFile.CabPath,
                                FileStream = fileSystem.OpenFile(normalized, FileMode.Open, FileAccess.Read),
                                Attributes = fileSystem.GetAttributes(normalized) & ~FileAttributes.ReparsePoint,
                                DateTime = fileSystem.GetLastWriteTime(normalized)
                            };
                        }
                    }
                }

                if (cabinetFileInfo != null)
                {
                    fileMappings.Add(cabinetFileInfo);
                }
                else
                {
                    Logging.Log($"\rError: File not found! {normalized}\n", LoggingLevel.Error);
                    //throw new FileNotFoundException(normalized);
                }
            }

            if (!hasSeenManifest && !hasSeenCatalog)
            {
                string packageName = GetSPKGComponentName(dsm);

                string normalized = @$"Windows\Packages\DsmFiles\{packageName}.dsm.xml";

                if (fileSystem.FileExists(normalized))
                {
                    fileMappings.Add(new CabinetFileInfo()
                    {
                        FileName = "man.dsm.xml",
                        FileStream = fileSystem.OpenFile(normalized, FileMode.Open, FileAccess.Read),
                        Attributes = FileAttributes.Normal,
                        DateTime = fileSystem.GetLastWriteTime(normalized)
                    });
                }
                else
                {
                    Logging.Log($"\rError: File not found! {normalized}\n", LoggingLevel.Error);
                }

                normalized = @$"Windows\System32\catroot\{{F750E6C3-38EE-11D1-85E5-00C04FC295EE}}\{packageName}.cat";

                if (fileSystem.FileExists(normalized))
                {
                    fileMappings.Add(new CabinetFileInfo()
                    {
                        FileName = "content.cat",
                        FileStream = fileSystem.OpenFile(normalized, FileMode.Open, FileAccess.Read),
                        Attributes = FileAttributes.Normal,
                        DateTime = fileSystem.GetLastWriteTime(normalized)
                    });
                }
                else
                {
                    Logging.Log($"\rError: File not found! {normalized}\n", LoggingLevel.Error);
                }
            }

            return fileMappings;
        }

        public static void BuildSPKG(IEnumerable<IDisk> disks, string destination_path, UpdateHistory.UpdateHistory? updateHistory)
        {
            Logging.Log();
            Logging.Log("Building SPKG Cabinet Files...");
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
                            if (fileSystem.DirectoryExists(@"Windows\Packages\DsmFiles"))
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

                IEnumerable<string> manifestFiles = fileSystem.GetFilesWithNtfsIssueWorkaround(@"Windows\Packages\DsmFiles", "*.xml", SearchOption.TopDirectoryOnly);

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

                IEnumerable<string> manifestFiles = fileSystem.GetFilesWithNtfsIssueWorkaround(@"Windows\Packages\DsmFiles", "*.xml", SearchOption.TopDirectoryOnly);

                foreach (string manifestFile in manifestFiles)
                {
                    try
                    {
                        XmlDsm.Package? dsm = null;

                        try
                        {
                            using Stream stream = fileSystem.OpenFileAndDecompressAsGZip(manifestFile);
                            XmlSerializer serializer = new(typeof(XmlDsm.Package));
                            dsm = (XmlDsm.Package)serializer.Deserialize(stream)!;
                        }
                        catch (InvalidDataException)
                        {
                            using Stream stream = fileSystem.OpenFile(manifestFile, FileMode.Open, FileAccess.Read);
                            XmlSerializer serializer = new(typeof(XmlDsm.Package));
                            dsm = (XmlDsm.Package)serializer.Deserialize(stream)!;
                        }

                        (string cabFileName, string cabFile) = BuildMetadataHandler.GetPackageNamingForSPKG(dsm, updateHistory);

                        if (string.IsNullOrEmpty(cabFileName) && string.IsNullOrEmpty(cabFile))
                        {
                            string partitionName = partition.Name.Replace("\0", "-");

                            if (!string.IsNullOrEmpty(dsm.Partition))
                            {
                                partitionName = dsm.Partition.Replace("\0", "-");
                            }

                            string packageName = GetSPKGComponentName(dsm);

                            cabFileName = Path.Combine(partitionName, packageName);

                            cabFile = Path.Combine(outputPath, $"{cabFileName}.spkg");
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
                            IEnumerable<CabinetFileInfo> fileMappings = GetCabinetFileInfoForDsmPackage(dsm, partition, disks);

                            // Cab Creation is only supported on Windows
                            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            {
                                if (fileMappings.Any())
                                {
                                    if (Path.GetDirectoryName(cabFile) is string directory && !Directory.Exists(directory))
                                    {
                                        Directory.CreateDirectory(directory);
                                    }

                                    CabinetBuilder.BuildCab(cabFile, fileMappings, ref fileStatus);
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