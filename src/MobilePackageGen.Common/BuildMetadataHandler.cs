using DiscUtils;

namespace MobilePackageGen
{
    public static class BuildMetadataHandler
    {
        public static UpdateHistory.UpdateHistory? GetUpdateHistory(IEnumerable<IDisk> disks)
        {
            foreach (IDisk disk in disks)
            {
                foreach (IPartition partition in disk.Partitions)
                {
                    IFileSystem? fileSystem = partition.FileSystem;

                    if (fileSystem != null)
                    {
                        if (fileSystem.DirectoryExists(@"Windows\ImageUpdate"))
                        {
                            string[] ImageUpdateFiles = [.. fileSystem.GetFiles(@"Windows\ImageUpdate", "*", SearchOption.AllDirectories)];
                            foreach (string ImageUpdateFile in ImageUpdateFiles)
                            {
                                if (Path.GetFileName(ImageUpdateFile).Equals("UpdateHistory.xml", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    try
                                    {
                                        using Stream UpdateHistoryStream = fileSystem.OpenFile(ImageUpdateFile, FileMode.Open, FileAccess.Read);
                                        UpdateHistory.UpdateHistory UpdateHistory = UpdateHistoryStream.GetObjectFromXML<UpdateHistory.UpdateHistory>();

                                        return UpdateHistory;
                                    }
                                    catch { }
                                }
                            }
                        }

                        if (fileSystem.DirectoryExists(@"SharedData\DuShared"))
                        {
                            string[] DUSharedFiles = [.. fileSystem.GetFiles(@"SharedData\DuShared", "*", SearchOption.AllDirectories)];
                            foreach (string DUSharedFile in DUSharedFiles)
                            {
                                if (Path.GetFileName(DUSharedFile).Equals("UpdateHistory.xml", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    using Stream UpdateHistoryStream = fileSystem.OpenFile(DUSharedFile, FileMode.Open, FileAccess.Read);
                                    UpdateHistory.UpdateHistory UpdateHistory = UpdateHistoryStream.GetObjectFromXML<UpdateHistory.UpdateHistory>();

                                    return UpdateHistory;
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        public static void GetFeatureManifests(IEnumerable<IDisk> disks, string destination_path)
        {
            string OEMInputPath = Path.Combine(destination_path, "OEMInput.xml");

            if (!File.Exists(OEMInputPath))
            {
                return;
            }

            string[] FMs = File.ReadAllLines(OEMInputPath);
            FMs = [.. FMs.Where(x => x.Contains("<AdditionalFM>")).Select(x => x.Split(">")[1].Split("<")[0])];

            foreach (string FM in FMs)
            {
                string DestinationPath = FM;
                DestinationPath = ReformatDestinationPath(DestinationPath);

                foreach (IDisk disk in disks)
                {
                    foreach (IPartition partition in disk.Partitions)
                    {
                        IFileSystem? fileSystem = partition.FileSystem;

                        if (fileSystem != null)
                        {
                            if (fileSystem.FileExists($@"Windows\ImageUpdate\FeatureManifest\Microsoft\{Path.GetFileName(FM)}"))
                            {
                                string destFolder = string.Join(@"\\", DestinationPath.Split(@"\")[..^1]);
                                if (!Directory.Exists(Path.Combine(destination_path, destFolder)))
                                {
                                    Directory.CreateDirectory(Path.Combine(destination_path, destFolder));
                                }

                                string destFM = Path.Combine(destination_path, DestinationPath);

                                if (!File.Exists(destFM))
                                {
                                    using Stream FMFileStream = fileSystem.OpenFile($@"Windows\ImageUpdate\FeatureManifest\Microsoft\{Path.GetFileName(FM)}", FileMode.Open, FileAccess.Read);

                                    FileAttributes Attributes = fileSystem.GetAttributes($@"Windows\ImageUpdate\FeatureManifest\Microsoft\{Path.GetFileName(FM)}") & ~FileAttributes.ReparsePoint;
                                    DateTime LastWriteTime = fileSystem.GetLastWriteTime($@"Windows\ImageUpdate\FeatureManifest\Microsoft\{Path.GetFileName(FM)}");

                                    using (Stream outputFile = File.Create(destFM))
                                    {
                                        FMFileStream.CopyTo(outputFile);
                                    }

                                    File.SetAttributes(destFM, Attributes);
                                    File.SetLastWriteTime(destFM, LastWriteTime);
                                }
                            }
                            else if (fileSystem.FileExists($@"Windows\ImageUpdate\FeatureManifest\OEM\{Path.GetFileName(FM)}"))
                            {
                                string destFolder = string.Join(@"\\", DestinationPath.Split(@"\")[..^1]);
                                if (!Directory.Exists(Path.Combine(destination_path, destFolder)))
                                {
                                    Directory.CreateDirectory(Path.Combine(destination_path, destFolder));
                                }

                                string destFM = Path.Combine(destination_path, DestinationPath);

                                if (!File.Exists(destFM))
                                {
                                    using Stream FMFileStream = fileSystem.OpenFile($@"Windows\ImageUpdate\FeatureManifest\OEM\{Path.GetFileName(FM)}", FileMode.Open, FileAccess.Read);

                                    FileAttributes Attributes = fileSystem.GetAttributes($@"Windows\ImageUpdate\FeatureManifest\OEM\{Path.GetFileName(FM)}") & ~FileAttributes.ReparsePoint;
                                    DateTime LastWriteTime = fileSystem.GetLastWriteTime($@"Windows\ImageUpdate\FeatureManifest\OEM\{Path.GetFileName(FM)}");

                                    using (Stream outputFile = File.Create(destFM))
                                    {
                                        FMFileStream.CopyTo(outputFile);
                                    }

                                    File.SetAttributes(destFM, Attributes);
                                    File.SetLastWriteTime(destFM, LastWriteTime);
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void GetOEMInput(IEnumerable<IDisk> disks, string destination_path)
        {
            foreach (IDisk disk in disks)
            {
                foreach (IPartition partition in disk.Partitions)
                {
                    IFileSystem? fileSystem = partition.FileSystem;

                    if (fileSystem != null)
                    {
                        if (fileSystem.FileExists(@"Windows\ImageUpdate\OEMInput.xml"))
                        {
                            if (!Directory.Exists(destination_path))
                            {
                                Directory.CreateDirectory(destination_path);
                            }

                            string destOemInput = Path.Combine(destination_path, "OEMInput.xml");

                            if (!File.Exists(destOemInput))
                            {
                                using Stream OEMInputFileStream = fileSystem.OpenFile(@"Windows\ImageUpdate\OEMInput.xml", FileMode.Open, FileAccess.Read);

                                FileAttributes Attributes = fileSystem.GetAttributes(@"Windows\ImageUpdate\OEMInput.xml") & ~FileAttributes.ReparsePoint;
                                DateTime LastWriteTime = fileSystem.GetLastWriteTime(@"Windows\ImageUpdate\OEMInput.xml");

                                using (Stream outputFile = File.Create(destOemInput))
                                {
                                    OEMInputFileStream.CopyTo(outputFile);
                                }

                                File.SetAttributes(destOemInput, Attributes);
                                File.SetLastWriteTime(destOemInput, LastWriteTime);
                            }
                        }
                    }
                }
            }
        }

        public static void GetAdditionalContent(IEnumerable<IDisk> disks, string destination_path)
        {
            foreach (IDisk disk in disks)
            {
                foreach (IPartition partition in disk.Partitions)
                {
                    IFileSystem? fileSystem = partition.FileSystem;

                    if (fileSystem != null)
                    {
                        if (partition.Name.Equals("PreInstalled", StringComparison.InvariantCultureIgnoreCase))
                        {
                            // PreInstalled Partition
                            // Extracted APPX, Licenses
                            if (fileSystem.DirectoryExists("AppData") && fileSystem.DirectoryExists("WindowsApps"))
                            {
                                string[] LicenseFiles = [.. fileSystem.GetFiles("AppData", "*.xml", SearchOption.TopDirectoryOnly)];

                                foreach (string LicenseFile in LicenseFiles)
                                {
                                    Logging.Log($"Extracting {Path.GetFileNameWithoutExtension(LicenseFile)} license file...");

                                    string destFolder = Path.Combine(destination_path, "PreInstalled", "Licenses");

                                    if (!Directory.Exists(destFolder))
                                    {
                                        Directory.CreateDirectory(destFolder);
                                    }

                                    string destFile = Path.Combine(destFolder, Path.GetFileName(LicenseFile));

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

                                string[] AppFolders = [.. fileSystem.GetDirectories("WindowsApps", "*", SearchOption.TopDirectoryOnly)];
                                foreach (string AppFolder in AppFolders)
                                {
                                    Logging.Log($"Extracting {Path.GetFileName(AppFolder)} app files...");

                                    string[] AppFiles = [.. fileSystem.GetFiles(AppFolder, "*", SearchOption.AllDirectories)];
                                    foreach (string AppFile in AppFiles)
                                    {
                                        string destFolder = Path.Combine(destination_path, "PreInstalled", "Apps", string.Join("\\", AppFile[12..].Split("\\")[..^1]));
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
                                }
                            }
                        }
                    }
                }
            }
        }

        public static (string cabFileName, string cabFile) GetPackageNamingForSPKG(XmlDsm.Package dsm, UpdateHistory.UpdateHistory? updateHistory)
        {
            string cabFileName = "";
            string cabFile = "";

            bool found = false;

            if (updateHistory != null)
            {
                // Go through every update in reverse chronological order
                foreach (UpdateHistory.UpdateEvent UpdateEvent in updateHistory.UpdateEvents.UpdateEvent.Reverse())
                {
                    foreach (UpdateHistory.Package Package in UpdateEvent.UpdateOSOutput.Packages.Package)
                    {
                        bool matches = (dsm.Identity.Owner ?? "").Equals(Package.Identity.Owner ?? "", StringComparison.InvariantCultureIgnoreCase) &&
                            (dsm.Identity.Component ?? "").Equals(Package.Identity.Component ?? "", StringComparison.InvariantCultureIgnoreCase) &&
                            (dsm.Identity.Version.Major ?? "").Equals(Package.Identity.Version.Major ?? "", StringComparison.InvariantCultureIgnoreCase) &&
                            (dsm.Identity.Version.Minor ?? "").Equals(Package.Identity.Version.Minor ?? "", StringComparison.InvariantCultureIgnoreCase) &&
                            (dsm.Identity.Version.QFE ?? "").Equals(Package.Identity.Version.QFE ?? "", StringComparison.InvariantCultureIgnoreCase) &&
                            (dsm.Identity.Version.Build ?? "").Equals(Package.Identity.Version.Build ?? "", StringComparison.InvariantCultureIgnoreCase) &&
                            (dsm.Identity.SubComponent ?? "").Equals(Package.Identity.SubComponent ?? "", StringComparison.InvariantCultureIgnoreCase) &&
                            (dsm.ReleaseType ?? "").Equals(Package.ReleaseType ?? "", StringComparison.InvariantCultureIgnoreCase) &&
                            (dsm.OwnerType ?? "").Equals(Package.OwnerType ?? "", StringComparison.InvariantCultureIgnoreCase) &&
                            (dsm.BuildType ?? "").Equals(Package.BuildType ?? "", StringComparison.InvariantCultureIgnoreCase) &&
                            (dsm.CpuType ?? "").Equals(Package.CpuType ?? "", StringComparison.InvariantCultureIgnoreCase) &&
                            (dsm.Partition ?? "").Equals(Package.Partition ?? "", StringComparison.InvariantCultureIgnoreCase) &&
                            //(dsm.IsRemoval ?? "").Equals(Package.IsRemoval ?? "", StringComparison.InvariantCultureIgnoreCase) &&
                            //(dsm.GroupingKey ?? "").Equals(Package.GroupingKey ?? "", StringComparison.InvariantCultureIgnoreCase) &&
                            (dsm.Culture ?? "").Equals(Package.Culture ?? "", StringComparison.InvariantCultureIgnoreCase) &&
                            //(dsm.Platform ?? "").Equals(Package.Platform ?? "", StringComparison.InvariantCultureIgnoreCase) &&
                            (dsm.Resolution ?? "").Equals(Package.Resolution ?? "", StringComparison.InvariantCultureIgnoreCase);

                        if (matches)
                        {
                            string DestinationPath = Package.PackageFile;
                            DestinationPath = ReformatDestinationPath(DestinationPath);
                            string DestinationPathExtension = Path.GetExtension(DestinationPath);

                            if (!string.IsNullOrEmpty(DestinationPathExtension))
                            {
                                cabFileName = DestinationPath[..^DestinationPathExtension.Length];
                                cabFile = DestinationPath;
                            }
                            else
                            {
                                cabFileName = DestinationPath;
                                cabFile = cabFileName;
                            }

                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        break;
                    }
                }
            }

            return (cabFileName, cabFile);
        }

        private static string ReformatDestinationPath(string DestinationPath)
        {
            if (DestinationPath[1] == ':')
            {
                string DriveLetter = DestinationPath[0].ToString().ToUpper();
                string DriveLetterLessPath = DestinationPath[3..];

                DestinationPath = Path.Combine($"Drive{DriveLetter}", DriveLetterLessPath);
            }

            if (DestinationPath.StartsWith(@"\\?\") && DestinationPath[5] == ':')
            {
                string UNCLessPath = DestinationPath[4..];
                string DriveLetter = UNCLessPath[0].ToString().ToUpper();
                string DriveLetterLessPath = UNCLessPath[3..];

                DestinationPath = Path.Combine($"Drive{DriveLetter}", DriveLetterLessPath);
            }

            if (DestinationPath.StartsWith(@"\\?\"))
            {
                string UNCLessPath = DestinationPath[4..];

                DestinationPath = Path.Combine("UNC", UNCLessPath);
            }

            if (DestinationPath.StartsWith('\\'))
            {
                string UNCLessPath = DestinationPath[2..];

                DestinationPath = Path.Combine($"UNC", UNCLessPath);
            }

            return DestinationPath;
        }

        public static (string cabFileName, string cabFile) GetPackageNamingForCBS(XmlMum.Assembly cbs, UpdateHistory.UpdateHistory? updateHistory)
        {
            string cabFileName = "";
            string cabFile = "";

            bool found = false;

            string cbsPackageIdentity = $"{cbs.AssemblyIdentity.Name}~{cbs.AssemblyIdentity.PublicKeyToken}~{cbs.AssemblyIdentity.ProcessorArchitecture}~{(cbs.AssemblyIdentity.Language == "neutral" ? "" : cbs.AssemblyIdentity.Language)}~{cbs.AssemblyIdentity.Version}";

            if (updateHistory != null)
            {
                // Go through every update in reverse chronological order
                foreach (UpdateHistory.UpdateEvent UpdateEvent in updateHistory.UpdateEvents.UpdateEvent.Reverse())
                {
                    foreach (UpdateHistory.Package Package in UpdateEvent.UpdateOSOutput.Packages.Package)
                    {
                        if (Package.PackageFile.EndsWith(".mum", StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }

                        if (Package.PackageIdentity == null)
                        {
                            continue;
                        }

                        bool matches = Package.PackageIdentity.Equals(cbsPackageIdentity, StringComparison.InvariantCultureIgnoreCase);

                        if (matches)
                        {
                            string DestinationPath = Package.PackageFile;
                            DestinationPath = ReformatDestinationPath(DestinationPath);
                            string DestinationPathExtension = Path.GetExtension(DestinationPath);

                            if (!string.IsNullOrEmpty(DestinationPathExtension))
                            {
                                cabFileName = DestinationPath[..^DestinationPathExtension.Length];
                                cabFile = DestinationPath;
                            }
                            else
                            {
                                cabFileName = DestinationPath;
                                cabFile = cabFileName;
                            }

                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        break;
                    }
                }
            }

            return (cabFileName, cabFile);
        }

        public static (string cabFileName, string cabFile) GetPackageNamingForINF(string inf, UpdateHistory.UpdateHistory? updateHistory)
        {
            string cabFileName = "";
            string cabFile = "";

            bool found = false;
            
            string infFileName = Path.GetFileNameWithoutExtension(inf);

            if (updateHistory != null)
            {
                // Go through every update in reverse chronological order
                foreach (UpdateHistory.UpdateEvent UpdateEvent in updateHistory.UpdateEvents.UpdateEvent.Reverse())
                {
                    foreach (UpdateHistory.Package Package in UpdateEvent.UpdateOSOutput.Packages.Package)
                    {
                        if (Package.PackageFile.EndsWith(".mum", StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }

                        bool matches = Path.GetFileNameWithoutExtension(Package.PackageFile) == infFileName;

                        if (matches)
                        {
                            string DestinationPath = Package.PackageFile;
                            DestinationPath = ReformatDestinationPath(DestinationPath);
                            string DestinationPathExtension = Path.GetExtension(DestinationPath);

                            if (DestinationPathExtension == ".inf")
                            {
                                DestinationPathExtension = ".cab";
                                DestinationPath = DestinationPath[..^DestinationPathExtension.Length] + ".cab";
                            }

                            if (!string.IsNullOrEmpty(DestinationPathExtension))
                            {
                                cabFileName = DestinationPath[..^DestinationPathExtension.Length];
                                cabFile = DestinationPath;
                            }
                            else
                            {
                                cabFileName = DestinationPath;
                                cabFile = cabFileName;
                            }

                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        break;
                    }
                }
            }

            return (cabFileName, cabFile);
        }
    }
}
