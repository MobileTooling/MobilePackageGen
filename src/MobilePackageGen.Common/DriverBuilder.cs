using DiscUtils;
using System.Runtime.InteropServices;

namespace MobilePackageGen
{
    public class DriverBuilder
    {
        public static void BuildDrivers(IEnumerable<IDisk> disks, string destination_path, UpdateHistory.UpdateHistory? updateHistory)
        {
            Logging.Log();
            Logging.Log("Building Driver Files...");
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
                    if (partition.Name != "BSP")
                    {
                        continue;
                    }

                    IFileSystem? fileSystem = partition.FileSystem;

                    if (fileSystem != null)
                    {
                        try
                        {
                            if (fileSystem.DirectoryExists(@"Windows\System32\DriverStore\FileRepository"))
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

                IEnumerable<string> manifestFiles = fileSystem.GetFilesWithNtfsIssueWorkaround(@"Windows\System32\DriverStore\FileRepository", "*.inf", SearchOption.AllDirectories);

                count += manifestFiles.Count();
            }

            return count;
        }

        private static List<CabinetFileInfo> GetCabinetFileInfoForCbsPackage(string driverFolder, IPartition partition)
        {
            List<CabinetFileInfo> fileMappings = [];

            IFileSystem fileSystem = partition.FileSystem!;

            foreach (string entry in fileSystem.GetFiles(driverFolder, "*", SearchOption.AllDirectories))
            {
                if (fileSystem.DirectoryExists(entry))
                {
                    continue;
                }

                fileMappings.Add(new CabinetFileInfo()
                {
                    FileName = string.Join("\\", entry.Split("\\")[5..]),
                    FileStream = fileSystem.OpenFile(entry, FileMode.Open, FileAccess.Read),
                    Attributes = fileSystem.GetAttributes(entry) & ~FileAttributes.ReparsePoint,
                    DateTime = fileSystem.GetLastWriteTime(entry)
                });
            }

            return fileMappings;
        }

        private static void BuildCabinets(IEnumerable<IDisk> disks, string outputPath, UpdateHistory.UpdateHistory? updateHistory)
        {
            int packagesCount = GetPackageCount(disks);

            IEnumerable<IPartition> partitionsWithCbsServicing = GetPartitionsWithServicing(disks);

            int i = 0;

            foreach (IPartition partition in partitionsWithCbsServicing)
            {
                IFileSystem fileSystem = partition.FileSystem!;

                IEnumerable<string> manifestFiles = fileSystem.GetFilesWithNtfsIssueWorkaround(@"Windows\System32\DriverStore\FileRepository", "*.inf", SearchOption.AllDirectories);

                foreach (string manifestFile in manifestFiles)
                {
                    try
                    {
                        string folder = string.Join("\\", manifestFile.Split("\\")[..^1]);

                        (string cabFileName, string cabFile) = BuildMetadataHandler.GetPackageNamingForINF(manifestFile, updateHistory);

                        if (string.IsNullOrEmpty(cabFileName) && string.IsNullOrEmpty(cabFile))
                        {
                            string packageName = Path.GetFileNameWithoutExtension(manifestFile);

                            string partitionName = partition.Name.Replace("\0", "-");

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

                        // Trim fileName from output
                        string DestinationFolder = Path.GetDirectoryName(cabFile)!;

                        IEnumerable<CabinetFileInfo> fileMappings = GetCabinetFileInfoForCbsPackage(folder, partition);

                        if (fileMappings.Any())
                        {
                            if (Path.GetDirectoryName(cabFile) is string directory && !Directory.Exists(directory))
                            {
                                Directory.CreateDirectory(directory);
                            }

                            foreach (CabinetFileInfo fileMapping in fileMappings)
                            {
                                string fileDestinationPath = Path.Combine(DestinationFolder, fileMapping.FileName);
                                string? fileRootPath = Path.GetDirectoryName(fileDestinationPath)!;

                                if (!Directory.Exists(fileRootPath))
                                {
                                    Directory.CreateDirectory(fileRootPath);
                                }

                                using FileStream fileStream = new(fileDestinationPath, FileMode.Create);
                                fileMapping.FileStream.CopyTo(fileStream);
                                fileMapping.FileStream.Close();

                                File.SetAttributes(fileDestinationPath, fileMapping.Attributes);
                                File.SetLastWriteTimeUtc(fileDestinationPath, fileMapping.DateTime);
                            }
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
