using Microsoft.Deployment.Compression.Cab;
using Microsoft.Deployment.Compression;

namespace MobilePackageGen
{
    public class CabinetBuilder
    {
        public static void BuildCab(string cabFile, IEnumerable<CabinetFileInfo> fileMappings, ref string fileStatus)
        {
            double oldPercentage = uint.MaxValue;
            double oldFilePercentage = uint.MaxValue;
            string oldFileName = "";

            string lambdaFileStatus = fileStatus;

            CabInfo cab = new(cabFile);
            cab.PackFiles(null, fileMappings.Select(x => x.GetFileTuple()).ToArray(), fileMappings.Select(x => x.FileName).ToArray(), CompressionLevel.Min, (object? _, ArchiveProgressEventArgs archiveProgressEventArgs) =>
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
                    Logging.Log(new string(' ', lambdaFileStatus.Length));
                    Logging.Log(Logging.GetDISMLikeProgressBar(0), returnLine: false);

                    Console.SetCursorPosition(0, Console.CursorTop - 2);

                    oldFileName = fileNameParsed;

                    oldFilePercentage = uint.MaxValue;

                    lambdaFileStatus = $"Adding file {archiveProgressEventArgs.CurrentFileNumber + 1} of {archiveProgressEventArgs.TotalFiles} - {fileNameParsed}";
                    if (lambdaFileStatus.Length > Console.BufferWidth - 24 - 1)
                    {
                        lambdaFileStatus = $"{lambdaFileStatus[..(Console.BufferWidth - 24 - 4)]}...";
                    }

                    Logging.Log();
                    Logging.Log(lambdaFileStatus);
                    Logging.Log(Logging.GetDISMLikeProgressBar(0), returnLine: false);

                    Console.SetCursorPosition(0, Console.CursorTop - 2);
                }

                double filePercentage = (double)archiveProgressEventArgs.CurrentFileBytesProcessed * 100 / archiveProgressEventArgs.CurrentFileTotalBytes;

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

            fileStatus = lambdaFileStatus;
        }
    }
}
