using System.Runtime.InteropServices;

namespace Playground
{
    internal class CatalogBasedVerification
    {
        public static int CatalogBasedVerifier(string cabFile)
        {
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

                foreach (string file in Directory.EnumerateFiles(cabFile, "*.cab", SearchOption.AllDirectories))
                {
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

        private static bool ValidatePackageCabinet(string packageCabinet)
        {
            bool Valid = true;

            using FileStream strm = File.OpenRead(packageCabinet);
            Cabinet.Cabinet cabFile = new(strm);

            Console.WriteLine("Enumerating files in Cabinet...");

            Cabinet.CabinetFile[] filesInCabinet = [.. Cabinet.CabinetExtractor.EnumCabinetFiles(cabFile)];

            Console.WriteLine("Extracting files in Cabinet...");

            string tempDirectory = Path.GetTempFileName();
            File.Delete(tempDirectory);

            uint oldPercentage = uint.MaxValue;

            Cabinet.CabinetExtractor.ExtractCabinet(cabFile, tempDirectory, (int progress, string file) =>
            {
                uint newPercentage = (uint)progress;

                if (newPercentage != oldPercentage)
                {
                    oldPercentage = newPercentage;
                    Console.Write($"\r{GetDISMLikeProgressBar(newPercentage)}");
                }
            });

            // For Progress Bar
            Console.WriteLine();

            Console.WriteLine("Parsing Catalog file...");

            List<string> checksums = CatalogManager.ReadCatalogFile(Path.Combine(tempDirectory, "update.cat")).Where(x => x.Length == 64).ToList();

            /*Console.WriteLine($"Checksums from catalog file: (SHA256) ({checksums.Count})");
            Console.WriteLine();

            foreach (var cs in checksums)
            {
                Console.WriteLine(cs);
            }*/

            Console.WriteLine("Hashing Package...");

            Dictionary<string, string> hashedPackage = [];
            Dictionary<string, string> hashedExpandedPackage = [];

            oldPercentage = uint.MaxValue;

            for (int i = 0; i < filesInCabinet.Length; i++)
            {
                Cabinet.CabinetFile file = filesInCabinet[i];

                uint newPercentage = (uint)Math.Floor((double)(i + 1) * 100 / filesInCabinet.Length);

                if (newPercentage != oldPercentage)
                {
                    oldPercentage = newPercentage;
                    Console.Write($"\r{GetDISMLikeProgressBar(newPercentage)}");
                }

                if (file.FileName.Equals("update.cat", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                using Stream fileStream = File.OpenRead(Path.Combine(tempDirectory, file.FileName));
                string SHA256 = BitConverter.ToString(System.Security.Cryptography.SHA256.HashData(fileStream)).Replace("-", "");

                hashedPackage.Add(file.FileName, SHA256);

                if (file.FileName.EndsWith(".manifest", StringComparison.InvariantCultureIgnoreCase))
                {
                    // LibSxS is only supported on Windows
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        try
                        {
                            fileStream.Seek(0, SeekOrigin.Begin);
                            Stream unpackedManifest = LibSxS.Delta.DeltaAPI.LoadManifest(fileStream);

                            SHA256 = BitConverter.ToString(System.Security.Cryptography.SHA256.HashData(unpackedManifest)).Replace("-", "");

                            hashedExpandedPackage.Add(file.FileName, SHA256);
                        }
                        catch { }
                    }
                }
            }

            // For Progress Bar
            Console.WriteLine();

            /*Console.WriteLine();
            Console.WriteLine($"Checksums from package: (SHA256) ({hashedPackage.Count})");
            Console.WriteLine();

            foreach (var cs in hashedPackage)
            {
                Console.WriteLine($"{cs.Key}: {cs.Value}");
            }*/

            Console.WriteLine("Validating Package Integrity...");

            ConsoleColor backupForeground = Console.ForegroundColor;

            if (checksums.Count != hashedPackage.Count)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"WARNING: Hash Count Mismatch! CAT: ({checksums.Count})  CAB: ({hashedPackage.Count})");
            }

            int validCount = 0;
            int invalidCount = 0;

            KeyValuePair<string, string>[] hashedPackageArray = hashedPackage.ToArray();

            for (int i = 0; i < hashedPackageArray.Length; i++)
            {
                KeyValuePair<string, string> cs = hashedPackageArray[i];

                string progressString = $"\r{GetDISMLikeProgressBar((uint)Math.Floor((double)(i + 1) * 100 / hashedPackageArray.Length))}";

                Console.ForegroundColor = backupForeground;
                Console.Write(progressString);

                if (!checksums.Contains(cs.Value))
                {
                    if (cs.Key.EndsWith(".manifest", StringComparison.InvariantCultureIgnoreCase))
                    {
                        try
                        {
                            string SHA256 = hashedExpandedPackage[cs.Key];

                            if (!checksums.Contains(SHA256))
                            {
                                invalidCount++;

                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\r{cs.Key} is modified or not present in the catalog file.");
                                Console.WriteLine($"Actual hash: {SHA256}");
                                Console.ForegroundColor = backupForeground;
                                Console.Write(progressString);

                                Valid = false;
                            }
                            else
                            {
                                validCount++;

                                //Console.ForegroundColor = ConsoleColor.Yellow;
                                //Console.WriteLine($"\r{cs.Key} expanded version is present in the catalog file but the one in the package is compressed.");
                                //Console.ForegroundColor = backupForeground;
                                //Console.Write(progressString);
                            }
                        }
                        catch
                        {
                            invalidCount++;

                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine($"\r{cs.Key} is modified or not present in the catalog file and expanding it did not succeed.");
                            Console.ForegroundColor = backupForeground;
                            Console.Write(progressString);

                            Valid = false;
                        }
                    }
                    else
                    {
                        if (cs.Key.EndsWith(".mui", StringComparison.InvariantCultureIgnoreCase))
                        {
                            validCount++;

                            //Console.ForegroundColor = ConsoleColor.DarkYellow;
                            //Console.WriteLine($"\r{cs.Key} is modified or not present in the catalog file but is an MUI file and these may not be applicable.");
                            //Console.ForegroundColor = backupForeground;
                            //Console.Write(progressString);
                        }
                        else
                        {
                            invalidCount++;

                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"\r{cs.Key} is modified or not present in the catalog file.");
                            Console.WriteLine($"Actual hash: {cs.Value}");
                            Console.ForegroundColor = backupForeground;
                            Console.Write(progressString);

                            Valid = false;
                        }
                    }
                }
                else
                {
                    validCount++;

                    //Console.ForegroundColor = ConsoleColor.Green;
                    //Console.WriteLine($"\r{cs.Key} is present in the catalog file.");
                    //Console.ForegroundColor = backupForeground;
                    //Console.Write(progressString);
                }
            }

            // For Progress Bar
            Console.WriteLine();

            Console.ForegroundColor = backupForeground;

            if (!Valid)
            {
                Console.WriteLine($"Valid File count: {invalidCount}");
                Console.WriteLine($"Invalid File count: {invalidCount}");
            }

            try
            {
                Directory.Delete(tempDirectory, true);
            }
            catch { }

            return Valid;
        }

        static void ValidatePackageFolder(string packageFolder)
        {
            string inputCat = Path.Combine(packageFolder, "update.cat");

            List<string> checksums = CatalogManager.ReadCatalogFile(inputCat).Where(x => x.Length == 64).ToList();

            Console.WriteLine($"Checksums from catalog file: (SHA256) ({checksums.Count})");
            Console.WriteLine();

            foreach (string cs in checksums)
            {
                Console.WriteLine(cs);
            }

            Dictionary<string, string> hashedPackage = [];

            foreach (string file in Directory.EnumerateFiles(packageFolder, "*", SearchOption.AllDirectories))
            {
                string friendlyFileName = file.Replace(packageFolder + Path.DirectorySeparatorChar, "", StringComparison.InvariantCultureIgnoreCase);

                if (friendlyFileName.Equals("update.cat", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                string SHA256 = BitConverter.ToString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file))).Replace("-", "");

                hashedPackage.Add(friendlyFileName, SHA256);
            }

            Console.WriteLine();
            Console.WriteLine($"Checksums from package: (SHA256) ({hashedPackage.Count})");
            Console.WriteLine();

            foreach (KeyValuePair<string, string> cs in hashedPackage)
            {
                Console.WriteLine($"{cs.Key}: {cs.Value}");
            }

            Console.WriteLine();
            Console.WriteLine("Validating Package Integrity...");
            Console.WriteLine();

            ConsoleColor backupForeground = Console.ForegroundColor;

            foreach (KeyValuePair<string, string> cs in hashedPackage)
            {
                if (!checksums.Contains(cs.Value))
                {
                    if (cs.Key.EndsWith(".manifest", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // LibSxS is only supported on Windows
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            try
                            {
                                string inputFile = Path.Combine(packageFolder, cs.Key);
                                using FileStream fileStream = File.OpenRead(inputFile);

                                Stream unpackedManifest = LibSxS.Delta.DeltaAPI.LoadManifest(fileStream);

                                string SHA256 = BitConverter.ToString(System.Security.Cryptography.SHA256.HashData(unpackedManifest)).Replace("-", "");

                                if (!checksums.Contains(SHA256))
                                {
                                    Console.ForegroundColor = ConsoleColor.Red;
                                    Console.WriteLine($"{cs.Key} is modified or not present in the catalog file.");
                                }
                                else
                                {
                                    Console.ForegroundColor = ConsoleColor.Yellow;
                                    Console.WriteLine($"{cs.Key} expanded version is present in the catalog file but the one in the package is compressed.");
                                }
                            }
                            catch
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"{cs.Key} is modified or not present in the catalog file and expanding it did not succeed.");
                            }
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"{cs.Key} is modified or not present in the catalog file and expanding it did not succeed.");
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"{cs.Key} is modified or not present in the catalog file.");
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"{cs.Key} is present in the catalog file.");
                }
            }

            Console.ForegroundColor = backupForeground;
        }

        public static string GetDISMLikeProgressBar(uint percentage)
        {
            if (percentage > 100)
            {
                percentage = 100;
            }

            int eqsLength = (int)Math.Floor((double)percentage * 55u / 100u);

            string bases = $"{new string('=', eqsLength)}{new string(' ', 55 - eqsLength)}";

            bases = bases.Insert(28, percentage + "%");

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
