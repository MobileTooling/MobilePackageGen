using Microsoft.Deployment.Compression;
using Microsoft.Deployment.Compression.Cab;
using MobilePackageGen;

namespace EzCab
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: EzCab.exe <Destination cab file> <Input directory to pack>");
                return -1;
            }

            string cabFile = args[0];
            string inputDir = args[1];

            List<CabinetFileInfo> fileMappings = [];

            DirectoryInfo dirInfo = new(inputDir);
            FileInfo[] fileInfos = dirInfo.GetFiles("*", SearchOption.AllDirectories);

            foreach (FileInfo fileInfo in fileInfos)
            {
                // Workaround for Timezone handling bug
                TimeSpan difference = DateTime.UtcNow - DateTime.Now;
                DateTime dateTime = fileInfo.LastWriteTime + difference;

                CabinetFileInfo cab = new()
                {
                    FileName = fileInfo.FullName.Replace(dirInfo.FullName + Path.DirectorySeparatorChar, ""),
                    Attributes = fileInfo.Attributes,
                    DateTime = dateTime,
                    FileStream = File.OpenRead(fileInfo.FullName)
                };

                fileMappings.Add(cab);
            }

            Logging.Log("Packing...");
            BuildCab(cabFile, fileMappings);

            foreach (CabinetFileInfo cab in fileMappings)
            {
                cab.FileStream.Dispose();
            }

            return 0;
        }

        public static void BuildCab(string cabFile, IEnumerable<CabinetFileInfo> fileMappings)
        {
            double oldPercentage = uint.MaxValue;
            double oldFilePercentage = uint.MaxValue;
            string oldFileName = "";

            string fileStatus = "Packing...";

            CabInfo cab = new(cabFile);
            cab.PackFiles(null, fileMappings.Select(x => x.GetFileTuple()).ToArray(), [.. fileMappings.Select(x => x.FileName)], CompressionLevel.Min, (object? _, ArchiveProgressEventArgs archiveProgressEventArgs) =>
            {
                string fileNameParsed;
                if (string.IsNullOrEmpty(archiveProgressEventArgs.CurrentFileName))
                {
                    fileNameParsed = $"Unknown ({archiveProgressEventArgs.CurrentFileNumber})";
                }
                else
                {
                    fileNameParsed = archiveProgressEventArgs.CurrentFileName;
                }

                double percentage = ((double)archiveProgressEventArgs.CurrentFileNumber * 50 / archiveProgressEventArgs.TotalFiles) + 50;

                if (percentage != oldPercentage)
                {
                    oldPercentage = percentage;
                    string progressBarString = Logging.GetDISMLikeProgressBar(percentage);

                    Logging.Log(progressBarString, returnLine: false);
                }

                if (fileNameParsed != oldFileName)
                {
                    Logging.Log();
                    Logging.Log(new string(' ', fileStatus.Length));
                    Logging.Log(Logging.GetDISMLikeProgressBar(0), returnLine: false);

                    Console.SetCursorPosition(0, Console.CursorTop - 2);

                    oldFileName = fileNameParsed;

                    oldFilePercentage = uint.MaxValue;

                    fileStatus = $"Adding file {archiveProgressEventArgs.CurrentFileNumber + 1} of {archiveProgressEventArgs.TotalFiles} - {fileNameParsed}";
                    if (fileStatus.Length > Console.BufferWidth - 24 - 1)
                    {
                        fileStatus = $"{fileStatus[..(Console.BufferWidth - 24 - 4)]}...";
                    }

                    Logging.Log();
                    Logging.Log(fileStatus);
                    Logging.Log(Logging.GetDISMLikeProgressBar(0), returnLine: false);

                    Console.SetCursorPosition(0, Console.CursorTop - 2);
                }

                double filePercentage = archiveProgressEventArgs.CurrentFileTotalBytes == 0 ? 100 : (double)archiveProgressEventArgs.CurrentFileBytesProcessed * 100 / archiveProgressEventArgs.CurrentFileTotalBytes;

                if (filePercentage != oldFilePercentage)
                {
                    oldFilePercentage = filePercentage;
                    string progressBarString = Logging.GetDISMLikeProgressBar(filePercentage);

                    Logging.Log();
                    Logging.Log();
                    Logging.Log(progressBarString, returnLine: false);

                    Console.SetCursorPosition(0, Console.CursorTop - 2);
                }
            });

            Logging.Log();
            Logging.Log();

            Logging.Log();
        }
    }
}
