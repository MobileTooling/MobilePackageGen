using CSharpFunctionalExtensions;
using DotnetPackaging.Msix;
using Serilog;
using System.IO.Abstractions;
using Zafiro.DivineBytes;
using Zafiro.DivineBytes.System.IO;
using File = System.IO.File;

namespace RemakeAppx
{
    public class Program
    {
        public static async Task MakeAppx(string inputFolder, string outputFile, bool bundleMode, bool unsignedMode)
        {
            if (File.Exists(outputFile))
            {
                return;
            }

            FileSystem fs = new();
            var directoryInfo = fs.DirectoryInfo.New(inputFolder);
            var directoryContainer = new DirectoryContainer(directoryInfo);

            await Msix.FromDirectory(directoryContainer, Maybe<ILogger>.None, bundleMode, unsignedMode, inputFolder)
                .Map(async source =>
                {
                    await using var fileStream = File.Open(outputFile, FileMode.Create);
                    return await source.WriteTo(fileStream);
                });
        }
    }
}
