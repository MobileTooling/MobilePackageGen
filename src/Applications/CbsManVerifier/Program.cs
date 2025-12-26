using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

namespace CbsManVerifier
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            string cabFile = args[0];

            //return CatalogBasedVerification.CatalogBasedVerifier(cabFile);

            if (File.Exists(cabFile))
            {
                bool result = ValidatePackageCabinet(cabFile);

                if (!result)
                {
                    return -1;
                }
            }
            else if (Directory.Exists(cabFile))
            {
                ConsoleColor backupForeground = Console.ForegroundColor;

                List<(string file, ulong size)> files = [];
                foreach (string file in Directory.EnumerateFiles(cabFile, "*.cab", SearchOption.AllDirectories))
                {
                    FileInfo fi = new(file);
                    files.Add((file, (ulong)fi.Length));
                }

                foreach ((string file, ulong _) in files.OrderBy(x => x.size))
                {
                    if (!IsCBSCab(file))
                    {
                        continue;
                    }

                    Console.ForegroundColor = backupForeground;

                    Console.WriteLine($"Analysing {file}");

                    bool result = ValidatePackageCabinet(file);
                    if (result)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"{file} is valid.");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{file} is not valid!");
                    }

                    Console.WriteLine();
                }

                Console.ForegroundColor = backupForeground;
            }

            return 0;
        }

        public static Mum.Assembly OpenManifest(Stream fstream)
        {
            Mum.Assembly cbs;
            XmlSerializer serializer = new(typeof(Mum.Assembly));

            //try
            //{
            //    cbs = (Mum.Assembly)serializer.Deserialize(fstream)!;
            //}
            //catch
            {
                // LibSxS is only supported on Windows
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    throw new NotImplementedException("The compression algorithm this data block uses is not currently implemented.");
                }

                fstream.Seek(0, SeekOrigin.Begin);

                using Stream dstream = LibSxS.Delta.DeltaAPI.LoadManifest(fstream);
                cbs = (Mum.Assembly)serializer.Deserialize(dstream)!;
            }

            return cbs;
        }

        private static bool IsCBSCab(string cabFile)
        {
            Cabinet.CabinetFile[] filesInCabinet = [.. Cabinet.CabinetExtractor.EnumCabinetFiles(cabFile)];
            if (filesInCabinet.Any(x => x.FileName.Equals("update.mum", StringComparison.InvariantCultureIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        private static string PadForConsole(string m)
        {
            if (m.Length > Console.WindowWidth - 20)
            {
                m = m[..(Console.WindowWidth - 20)];
            }

            if (m.Length < Console.WindowWidth - 20)
            {
                m = m.PadRight(Console.WindowWidth - 20 - m.Length);
            }

            return m;
        }

        private static List<(string fn, string cs)> GetChecksums(string packageCabinet)
        {
            object lockobj = new();
            ConcurrentBag<(string fn, string cs)> checksums = [];

            MobilePackageGen.XmlMum.Assembly cbs;

            using (FileStream strm = File.OpenRead(packageCabinet))
            {
                Cabinet.Cabinet cabFile = new(strm);
                using Stream stream = new MemoryStream(Cabinet.CabinetExtractor.ExtractCabinetFile(cabFile, "update.mum"));
                XmlSerializer serializer = new(typeof(MobilePackageGen.XmlMum.Assembly));
                cbs = (MobilePackageGen.XmlMum.Assembly)serializer.Deserialize(stream)!;
            }

            List<MobilePackageGen.XmlMum.File> manifests = [.. cbs.Package.CustomInformation.File.Where(x => x.Name.EndsWith(".manifest", StringComparison.InvariantCultureIgnoreCase))];

            int i = 0;
            Parallel.ForEach(manifests, x =>
            {
                lock (lockobj)
                {
                    Console.Write($"\rParsing {PadForConsole(x.Name)}\n{GetDISMLikeProgressBar((double)(i + 1) * 100 / manifests.Count)}");
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                }

                using FileStream strm = File.OpenRead(packageCabinet);
                Cabinet.Cabinet cabFile = new(strm);

                Mum.Assembly ass;

                using (Stream fileStream = new MemoryStream(Cabinet.CabinetExtractor.ExtractCabinetFile(cabFile, x.Cabpath)))
                {
                    ass = OpenManifest(fileStream);
                }

                string fn = x.Name[..^(".manifest".Length)];

                Parallel.ForEach(ass.File, v =>
                {
                    checksums.Add((Path.Combine(fn, v.Name), Convert.ToHexString(Convert.FromBase64String(v.Hash.DigestValue))));
                });

                lock (lockobj)
                {
                    i++;
                }
            });

            return [.. checksums];
        }


        private static bool ValidatePackageCabinet(string packageCabinet)
        {
            bool Valid = true;

            Console.WriteLine("\rParsing manifest files...");

            List<(string fn, string cs)> checksums = GetChecksums(packageCabinet);

            /*Console.WriteLine($"Checksums from catalog file: (SHA256) ({checksums.Count})");
            Console.WriteLine();

            foreach ((string fn, string cs) in checksums)
            {
                Console.WriteLine($"{fn}: {cs}");
            }*/

            Console.WriteLine(PadForConsole("\rValidating Package Integrity..."));

            ConcurrentBag<(string fn, string cs, string acs)> results = [];

            object lockobj = new();

            int j = 0;
            Parallel.ForEach(checksums, t =>
            {
                (string fn, string cs) = t;

                lock (lockobj)
                {
                    string progressString = $"\r{GetDISMLikeProgressBar((double)(j + 1) * 100 / checksums.Count)}";
                    Console.Write(progressString);
                }

                using FileStream strm = File.OpenRead(packageCabinet);
                Cabinet.Cabinet cabFile = new(strm);

                using Stream fileStream = new MemoryStream(Cabinet.CabinetExtractor.ExtractCabinetFile(cabFile, fn));
                string SHA256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fileStream));

                if (SHA256 != cs)
                {
                    results.Add((fn, cs, SHA256));
                    Valid = false;
                }

                lock (lockobj)
                {
                    j++;
                }
            });

            // For Progress Bar
            Console.WriteLine();

            ConsoleColor backupForeground = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            foreach ((string fn, string cs, string acs) in results)
            {
                Console.WriteLine($"\r{fn} is modified or not present in the catalog file.");
                Console.WriteLine($"Actual hash: {acs}");
                Console.WriteLine($"Expected hash: {cs}");
            }
            Console.ForegroundColor = backupForeground;

            return Valid;
        }

        public static string GetDISMLikeProgressBar(double percentage)
        {
            if (percentage > 100)
            {
                percentage = 100;
            }

            int eqsLength = (int)Math.Floor((double)percentage * 55u / 100u);

            string bases = $"{new string('=', eqsLength)}{new string(' ', 55 - eqsLength)}";

            bases = bases.Insert(28, $"{percentage:0.00}%");

            if (percentage == 100)
            {
                bases = bases[1..];
            }
            else if (percentage < 10)
            {
                bases = bases.Insert(28, " ");
            }

            return $"[{bases}]";
        }
    }
}
