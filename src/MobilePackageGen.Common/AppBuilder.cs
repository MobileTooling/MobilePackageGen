using DiscUtils;
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
                Logging.Log($"Extracting {Path.GetFileNameWithoutExtension(LicenseFile)} license file...");

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
                    continue;

                // --------------------------

                string componentStatus = $"Creating package {i + 1} of {packagesCount} - {Path.GetFileName(AppFolder)}";
                if (componentStatus.Length > Console.BufferWidth - 24 - 1)
                {
                    componentStatus = $"{componentStatus[..(Console.BufferWidth - 24 - 4)]}...";
                }

                Logging.Log(componentStatus);
                string progressBarString = Logging.GetDISMLikeProgressBar((uint)Math.Round(i * 100d / packagesCount));
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
