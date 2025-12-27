using DiscUtils;
using DiscUtils.Streams;
using StorageSpace;
using System.Xml.Linq;

namespace MobilePackageGen
{
    public class AppBuilder
    {
        public static void BuildApps(IEnumerable<IDisk> disks, string destination_path, List<AppxPackage> appList)
        {
            Logging.Log();
            Logging.Log("Building App Files...");
            Logging.Log();

            GetAdditionalContent(disks, destination_path, appList);

            Logging.Log();
            Logging.Log("Cleaning up...");
            Logging.Log();

            TempManager.CleanupTempFiles();

            // Dump PreInstalled as a temporary measure
            DumpPreinstalledSpace(disks, destination_path);
        }

        public static void DumpPreinstalledSpace(IEnumerable<IDisk> idisks, string outputDirectory)
        {
            foreach (IDisk idisk in idisks)
            {
                foreach (IPartition partition in idisk.Partitions)
                {
                    if (partition.Type == new Guid("E75CAF8F-F680-4CEE-AFA3-B001E56EFC2D"))
                    {
                        partition.Stream.Position = 0;
                        Pool pool = new(partition.Stream);

                        Dictionary<long, string> disks = pool.GetDisks();

                        foreach (KeyValuePair<long, string> disk in disks.OrderBy(x => x.Key).Skip(1))
                        {
                            if (!disk.Value.Equals("PreInstalledDisk"))
                            {
                                continue;
                            }

                            Space space = pool.OpenDisk(disk.Key);
                            int spaceSectorSize = TryDetectSectorSize(space);
                            DumpSpace(outputDirectory, disk.Value, space, spaceSectorSize);
                            space.Dispose();
                        }
                    }
                }
            }
        }

        private static void DumpSpace(string outputDirectory, string disk, Space space, int spaceSectorSize)
        {
            string vhdFile = Path.Combine(outputDirectory, $"{disk}.vhdx");

            if (File.Exists(vhdFile))
            {
                return;
            }

            Logging.Log();

            Logging.Log($"Dumping {vhdFile}...");

            long diskCapacity = space.Length;
            using Stream fs = new FileStream(vhdFile, FileMode.CreateNew, FileAccess.ReadWrite);
            using VirtualDisk outDisk = DiscUtils.Vhdx.Disk.InitializeDynamic(fs, Ownership.None, diskCapacity, Geometry.FromCapacity(diskCapacity, spaceSectorSize));

            DateTime now = DateTime.Now;
            void progressCallback(ulong readBytes, ulong totalBytes)
            {
                ShowProgress(readBytes, totalBytes, now);
            }

            Logging.Log($"Dumping {disk}");
            space.CopyTo(outDisk.Content, progressCallback);
            Logging.Log();
        }

        private static int TryDetectSectorSize(Stream diskStream)
        {
            // Default is 4096
            int sectorSize = 4096;

            if (diskStream.Length > 4096 * 2)
            {
                BinaryReader reader = new(diskStream);

                diskStream.Seek(512, SeekOrigin.Begin);
                byte[] header1 = reader.ReadBytes(8);

                diskStream.Seek(4096, SeekOrigin.Begin);
                byte[] header2 = reader.ReadBytes(8);

                string header1str = System.Text.Encoding.ASCII.GetString(header1);
                string header2str = System.Text.Encoding.ASCII.GetString(header2);

                if (header1str == "EFI PART")
                {
                    sectorSize = 512;
                }
                else if (header2str == "EFI PART")
                {
                    sectorSize = 4096;
                }
                else if (diskStream.Length % 512 == 0 && diskStream.Length % 4096 != 0)
                {
                    sectorSize = 512;
                }

                diskStream.Seek(0, SeekOrigin.Begin);
            }
            else
            {
                if (diskStream.Length % 512 == 0 && diskStream.Length % 4096 != 0)
                {
                    sectorSize = 512;
                }
            }

            return sectorSize;
        }

        protected static void ShowProgress(ulong readBytes, ulong totalBytes, DateTime startTime)
        {
            DateTime now = DateTime.Now;
            TimeSpan timeSoFar = now - startTime;

            TimeSpan remaining = readBytes != 0 ?
                TimeSpan.FromMilliseconds(timeSoFar.TotalMilliseconds / readBytes * (totalBytes - readBytes)) : TimeSpan.MaxValue;

            double speed = Math.Round(readBytes / 1024L / 1024L / timeSoFar.TotalSeconds);

            uint percentage = (uint)(readBytes * 100 / totalBytes);

            Logging.Log($"{Logging.GetDISMLikeProgressBar(percentage)} {speed}MB/s {remaining:hh\\:mm\\:ss\\.f}", returnLine: false);
        }

        private static List<IPartition> GetPartitionsWithServicing(IEnumerable<IDisk> disks)
        {
            List<IPartition> fileSystemsWithServicing = [];

            foreach (IDisk disk in disks)
            {
                foreach (IPartition partition in disk.Partitions)
                {
                    if (partition.Name != "PreInstalled")
                    {
                        continue;
                    }

                    IFileSystem? fileSystem = partition.FileSystem;

                    if (fileSystem != null)
                    {
                        try
                        {
                            // PreInstalled Partition
                            // Extracted APPX, Licenses
                            if (fileSystem.DirectoryExists("AppData") && fileSystem.DirectoryExists("WindowsApps"))
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

                IEnumerable<string> manifestFiles = fileSystem.GetDirectories("WindowsApps", "*", SearchOption.TopDirectoryOnly);

                count += manifestFiles.Count();
            }

            return count;
        }

        public static void GetAdditionalContent(IEnumerable<IDisk> disks, string destination_path, List<AppxPackage> appList)
        {
            int packagesCount = GetPackageCount(disks);

            IEnumerable<IPartition> partitionsWithCbsServicing = GetPartitionsWithServicing(disks);

            int i = 0;

            foreach (IPartition partition in partitionsWithCbsServicing)
            {
                IFileSystem fileSystem = partition.FileSystem!;

                ExtractAppLicenses(fileSystem, destination_path, appList);
                ExtractAppPackages(fileSystem, destination_path, packagesCount, ref i, appList);
            }
        }

        public static string GetPFNFromAppLicense(Stream strm)
        {
            string PFM = "";

            XDocument xdoc = XDocument.Load(strm, LoadOptions.None);
            XNamespace ns = xdoc.Root!.GetDefaultNamespace();

            IEnumerable<XElement> packages = xdoc.Descendants(ns + "PFM");

            if (packages != null)
            {
                foreach (XElement package in packages)
                {
                    PFM = package.Value;
                    break;
                }
            }

            xdoc = null;

            return PFM;
        }

        public static void ExtractAppLicenses(IFileSystem fileSystem, string destination_path, List<AppxPackage> appList)
        {
            string[] LicenseFiles = [.. fileSystem.GetFiles("AppData", "*.xml", SearchOption.TopDirectoryOnly)];

            foreach (string LicenseFile in LicenseFiles)
            {
                // Fallback
                string destFolder = Path.Combine(destination_path, "PreInstalled", "Licenses");
                string destName = Path.GetFileName(LicenseFile);

                // Actual
                using Stream PreInstalledLicenseFileStream = fileSystem.OpenFile(LicenseFile, FileMode.Open, FileAccess.Read);
                string PFM = GetPFNFromAppLicense(PreInstalledLicenseFileStream);
                AppxPackage? matchingAppListItem = appList.FirstOrDefault(x =>
                    x.ID.Equals(PFM, StringComparison.InvariantCultureIgnoreCase));
                if (matchingAppListItem != null)
                {
                    destFolder = Path.Combine(destination_path, matchingAppListItem.Path.Replace("$(mspackageroot)\\", "", StringComparison.InvariantCultureIgnoreCase));
                    destName = matchingAppListItem.License;
                }

                if (!Directory.Exists(destFolder))
                {
                    Directory.CreateDirectory(destFolder);
                }

                string destFile = Path.Combine(destFolder, destName);
                
                if (!File.Exists(destFile))
                {
                    Logging.Log($"Extracting {Path.GetFileNameWithoutExtension(LicenseFile)} license file...");

                    using Stream PreInstalledFileStream = fileSystem.OpenFile(LicenseFile, FileMode.Open, FileAccess.Read);
                    FileAttributes Attributes = fileSystem.GetAttributes(LicenseFile) & ~FileAttributes.ReparsePoint;
                    DateTime LastWriteTime = fileSystem.GetLastWriteTime(LicenseFile);

                    using (Stream outputFile = File.Create(destFile))
                    {
                        PreInstalledFileStream.CopyTo(outputFile);
                    }

                    File.SetAttributes(destFile, Attributes);
                    File.SetLastWriteTime(destFile, LastWriteTime);
                }
            }
        }

        public static void ExtractAppPackages(IFileSystem fileSystem, string destination_path, int packagesCount, ref int i, List<AppxPackage> appList)
        {
            string[] AppFolders = [.. fileSystem.GetDirectories("WindowsApps", "*", SearchOption.TopDirectoryOnly)];
            foreach (string AppFolder in AppFolders)
            {
                if (Path.GetFileName(AppFolder).Equals("deleted", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                // --------------------------

                string componentStatus = $"Creating package {i + 1} of {packagesCount} - {Path.GetFileName(AppFolder)}";
                if (componentStatus.Length > Console.BufferWidth - 24 - 1)
                {
                    componentStatus = $"{componentStatus[..(Console.BufferWidth - 24 - 4)]}...";
                }

                Logging.Log(componentStatus);
                string progressBarString = Logging.GetDISMLikeProgressBar(i * 100d / packagesCount);
                Logging.Log(progressBarString, returnLine: false);

                string fileStatus = "";

                // --------------------------

                // Ex: "Microsoft.Dynamics365.Guides_1.0.100.0_arm__8wekyb3d8bbwe"
                // PFM: <PFM>microsoft.dynamics365.guides_8wekyb3d8bbwe</PFM>
                // Arch: ARM
                string appPackageName = Path.GetFileName(AppFolder);
                string appArchitecture = appPackageName.Split("_")[^3];
                string appPFM = $"{appPackageName.Split("_")[0]}_{appPackageName.Split("_")[^1]}";

                // Fallback
                string rootDestination = Path.Combine(destination_path, "PreInstalled", "Apps");

                // Actual
                // Order by count of CPUIDs because we prefer packages that strictly bind to the same architecture instead of wow fallback
                appList = [.. appList.OrderBy(x => x.Architectures.Length)];
                AppxPackage? matchingAppListItem = appList.FirstOrDefault(x => 
                    x.ID.Equals(appPFM, StringComparison.InvariantCultureIgnoreCase) &&
                    (x.Architectures.Length == 0 || x.Architectures.Any(y => y.Equals(appArchitecture, StringComparison.InvariantCultureIgnoreCase))));
                if (matchingAppListItem != null)
                {
                    rootDestination = Path.Combine(destination_path, matchingAppListItem.Path.Replace("$(mspackageroot)\\", "", StringComparison.InvariantCultureIgnoreCase), matchingAppListItem.Name);
                }

                string destination = Path.Combine(rootDestination, appPackageName);

                string[] AppFiles = [.. fileSystem.GetFiles(AppFolder, "*", SearchOption.AllDirectories)];
                for (int currentFileNumber = 0; currentFileNumber < AppFiles.Length; currentFileNumber++)
                {
                    string AppFile = AppFiles[currentFileNumber];
                    // This strips WindowsApps
                    string destinationFileRootPath = string.Join("\\", AppFile[(AppFolder.Length + 1)..].Split("\\")[..^1]);
                    string destFolder = Path.Combine(destination, destinationFileRootPath);

                    if (!Directory.Exists(destFolder))
                    {
                        Directory.CreateDirectory(destFolder);
                    }

                    string destFile = Path.Combine(destFolder, Path.GetFileName(AppFile));

                    if (!File.Exists(destFile))
                    {
                        using Stream PreInstalledFileStream = fileSystem.OpenFile(AppFile, FileMode.Open, FileAccess.Read);
                        FileAttributes Attributes = fileSystem.GetAttributes(AppFile) & ~FileAttributes.ReparsePoint;
                        DateTime LastWriteTime = fileSystem.GetLastWriteTime(AppFile);

                        using (Stream outputFile = File.Create(destFile))
                        {
                            PreInstalledFileStream.CopyTo(outputFile);
                        }

                        File.SetAttributes(destFile, Attributes);
                        File.SetLastWriteTime(destFile, LastWriteTime);
                    }
                }

                RemakeAppx.Program.MakeAppx(destination, $"{destination}.appx", false, false).Wait();

                // --------------------------

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
        }
    }
}
