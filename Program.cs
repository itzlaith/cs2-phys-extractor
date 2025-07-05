using Microsoft.Win32;
using SteamDatabase.ValvePak;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ValveResourceFormat;
using ValveResourceFormat.ResourceTypes;

namespace CS2PhysExtractor
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("CS2 PHYS Data Extractor");
            Console.WriteLine("========================");

            try
            {
                // Create output directory
                string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vphys");
                Directory.CreateDirectory(outputDir);

                // Find Steam installation
                string steamPath = FindSteamPath();
                if (string.IsNullOrEmpty(steamPath))
                {
                    Console.WriteLine("ERROR: Steam installation not found!");
                    return;
                }

                Console.WriteLine($"Steam found at: {steamPath}");

                // Show menu
                ShowMenu();
                int choice = GetUserChoice();

                switch (choice)
                {
                    case 1:
                        ProcessOfficialMaps(steamPath, outputDir);
                        ProcessWorkshopMaps(steamPath, outputDir);
                        break;
                    case 2:
                        ProcessOfficialMaps(steamPath, outputDir);
                        break;
                    case 3:
                        ProcessWorkshopMaps(steamPath, outputDir);
                        break;
                    default:
                        Console.WriteLine("Invalid choice. Exiting...");
                        return;
                }

                Console.WriteLine("\nExtraction completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void ShowMenu()
        {
            Console.WriteLine("\nWhat would you like to process?");
            Console.WriteLine("1. Process all maps (workshop + official)");
            Console.WriteLine("2. Process only official maps");
            Console.WriteLine("3. Process only workshop maps");
            Console.Write("Enter your choice (1-3): ");
        }

        static int GetUserChoice()
        {
            while (true)
            {
                if (int.TryParse(Console.ReadLine(), out int choice) && choice >= 1 && choice <= 3)
                {
                    return choice;
                }
                Console.Write("Please enter a valid choice (1-3): ");
            }
        }

        static string FindSteamPath()
        {
            try
            {
                // Try registry first
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key?.GetValue("SteamPath") is string steamPath)
                    {
                        return steamPath.Replace('/', '\\');
                    }
                }

                // Try common locations
                string[] commonPaths = {
                    @"C:\Program Files (x86)\Steam",
                    @"C:\Program Files\Steam",
                    @"D:\Steam",
                    @"E:\Steam"
                };

                foreach (string path in commonPaths)
                {
                    if (Directory.Exists(path))
                        return path;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error finding Steam path: {ex.Message}");
            }

            return null;
        }

        static void ProcessOfficialMaps(string steamPath, string outputDir)
        {
            Console.WriteLine("\nProcessing official maps...");

            string officialMapsPath = Path.Combine(steamPath, @"steamapps\common\Counter-Strike Global Offensive\game\csgo\maps");

            if (!Directory.Exists(officialMapsPath))
            {
                Console.WriteLine("Official maps directory not found, skipping...");
                return;
            }

            var vpkFiles = Directory.GetFiles(officialMapsPath, "*.vpk")
                .Where(f => {
                    string fileName = Path.GetFileName(f).ToLower();
                    return !fileName.Contains("vanity") &&
                    !fileName.Contains("workshop") &&
                    !fileName.Contains("graphics_settings") &&
                    !fileName.Contains("lobby_mapveto") &&
                    (fileName.StartsWith("de_") || fileName.StartsWith("cs_") || fileName.StartsWith("ar_"));
                }).ToList();

            Console.WriteLine($"Found {vpkFiles.Count} official map VPK files");

            foreach (string vpkFile in vpkFiles)
            {
                try
                {
                    ProcessVpkFile(vpkFile, outputDir);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {Path.GetFileName(vpkFile)}: {ex.Message}");
                }
            }
        }

        static void ProcessWorkshopMaps(string steamPath, string outputDir)
        {
            Console.WriteLine("\nProcessing workshop maps...");

            string workshopPath = Path.Combine(steamPath, @"steamapps\workshop\content\730");

            if (!Directory.Exists(workshopPath))
            {
                Console.WriteLine("Workshop directory not found, skipping...");
                return;
            }

            var workshopDirs = Directory.GetDirectories(workshopPath);
            Console.WriteLine($"Found {workshopDirs.Length} workshop directories");

            foreach (string workshopDir in workshopDirs)
            {
                try
                {
                    var vpkFiles = Directory.GetFiles(workshopDir, "*.vpk")
                        .Where(f => !Path.GetFileName(f).ToLower().Contains("vanity") &&
                                   !Path.GetFileName(f).ToLower().Contains("workshop"))
                        .ToList();

                    Console.WriteLine($"  Workshop dir {Path.GetFileName(workshopDir)}: Found {vpkFiles.Count} VPK files");

                    if (vpkFiles.Count > 0)
                    {
                        // For workshop maps, try to find the base VPK file (without _000, _001, etc.)
                        string baseVpk = FindBaseVpkFile(vpkFiles);

                        if (!string.IsNullOrEmpty(baseVpk))
                        {
                            long fileSize = new FileInfo(baseVpk).Length;
                            Console.WriteLine($"  Processing base VPK: {Path.GetFileName(baseVpk)} ({fileSize / 1024 / 1024:F1} MB)");

                            try
                            {
                                ProcessVpkFile(baseVpk, outputDir);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  Error processing workshop map {Path.GetFileName(baseVpk)}: {ex.Message}");

                                // If base VPK fails, try the largest numbered VPK
                                string largestVpk = vpkFiles
                                    .OrderByDescending(f => new FileInfo(f).Length)
                                    .First();

                                if (largestVpk != baseVpk)
                                {
                                    Console.WriteLine($"  Trying largest VPK instead: {Path.GetFileName(largestVpk)}");
                                    try
                                    {
                                        ProcessVpkFile(largestVpk, outputDir);
                                    }
                                    catch (Exception ex2)
                                    {
                                        Console.WriteLine($"  Also failed: {ex2.Message}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Fallback to largest file
                            string largestVpk = vpkFiles
                                .OrderByDescending(f => new FileInfo(f).Length)
                                .First();

                            long fileSize = new FileInfo(largestVpk).Length;
                            Console.WriteLine($"  Processing largest VPK: {Path.GetFileName(largestVpk)} ({fileSize / 1024 / 1024:F1} MB)");

                            try
                            {
                                ProcessVpkFile(largestVpk, outputDir);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  Error processing workshop map {Path.GetFileName(largestVpk)}: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  No VPK files found in {Path.GetFileName(workshopDir)}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing workshop directory {Path.GetFileName(workshopDir)}: {ex.Message}");
                }
            }
        }

        static string FindBaseVpkFile(List<string> vpkFiles)
        {
            // Look for a VPK file that doesn't end with _000, _001, etc.
            // This is typically the "dir" file that contains the directory structure
            foreach (string vpkFile in vpkFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(vpkFile);
                if (!fileName.EndsWith("_000") && !fileName.EndsWith("_001") &&
                    !fileName.EndsWith("_002") && !fileName.EndsWith("_003") &&
                    !fileName.EndsWith("_004") && !fileName.EndsWith("_005") &&
                    !fileName.EndsWith("_006") && !fileName.EndsWith("_007") &&
                    !fileName.EndsWith("_008") && !fileName.EndsWith("_009"))
                {
                    return vpkFile;
                }
            }
            return null;
        }

        static void ProcessVpkFile(string vpkPath, string outputDir)
        {
            Console.WriteLine($"Processing: {Path.GetFileName(vpkPath)}");

            try
            {
                using (var package = new Package())
                {
                    package.Read(vpkPath);
                    Console.WriteLine($"  VPK contains {package.Entries.Count} total entries");

                    // Look for VPK files in the maps directory
                    var mapVpkFiles = new List<SteamDatabase.ValvePak.PackageEntry>();

                    // Check the "vpk" entry type for files in maps directory
                    if (package.Entries.ContainsKey("vpk"))
                    {
                        foreach (var file in package.Entries["vpk"])
                        {
                            if (file.DirectoryName != null &&
                                file.DirectoryName.Equals("maps", StringComparison.OrdinalIgnoreCase))
                            {
                                mapVpkFiles.Add(file);
                                Console.WriteLine($"  Found map VPK: {file.FileName}");
                            }
                        }
                    }

                    if (mapVpkFiles.Any())
                    {
                        // Find the main map VPK (without suffixes like _3dsky)
                        var mainMapVpk = mapVpkFiles
                            .Where(f => !f.FileName.Contains("_3dsky", StringComparison.OrdinalIgnoreCase) &&
                                       !f.FileName.Contains("_skybox", StringComparison.OrdinalIgnoreCase) &&
                                       !f.FileName.Contains("_sky", StringComparison.OrdinalIgnoreCase))
                            .OrderBy(f => f.FileName.Length) // Prefer shorter names (main map)
                            .FirstOrDefault();

                        if (mainMapVpk != null)
                        {
                            Console.WriteLine($"  Processing main map VPK: {mainMapVpk.FileName}");
                            ProcessNestedVpkFile(package, mainMapVpk, outputDir);
                        }
                        else
                        {
                            Console.WriteLine($"  No main map VPK found, trying first available: {mapVpkFiles.First().FileName}");
                            ProcessNestedVpkFile(package, mapVpkFiles.First(), outputDir);
                        }
                    }
                    else
                    {
                        // Process as official map (existing logic)
                        Console.WriteLine("  No nested map VPK found, processing as official map");
                        ProcessOfficialMapVpk(package, outputDir);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to open VPK {Path.GetFileName(vpkPath)}: {ex.Message}");
            }
        }

        static void ProcessNestedVpkFile(Package parentPackage, SteamDatabase.ValvePak.PackageEntry nestedVpkFile, string outputDir)
        {
            try
            {
                // Extract the nested VPK to memory
                parentPackage.ReadEntry(nestedVpkFile, out byte[] vpkData);
                Console.WriteLine($"    Extracted nested VPK data: {vpkData.Length} bytes");

                // Create a temporary stream and process the nested VPK
                using (var memoryStream = new MemoryStream(vpkData))
                using (var nestedPackage = new Package())
                {
                    // Set the filename before reading from stream
                    nestedPackage.SetFileName($"{nestedVpkFile.DirectoryName}/{nestedVpkFile.FileName}.vpk");
                    nestedPackage.Read(memoryStream);
                    Console.WriteLine($"    Nested VPK contains {nestedPackage.Entries.Count} entries");

                    // Process this nested VPK like an official map
                    ProcessOfficialMapVpk(nestedPackage, outputDir);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Error processing nested VPK: {ex.Message}");
                Console.WriteLine($"    Stack trace: {ex.StackTrace}");
            }
        }

        static void ProcessOfficialMapVpk(Package package, string outputDir)
        {
            // Look for vmdl_c files
            var vmdlFiles = package.Entries
                .Where(kvp => kvp.Key.Equals("vmdl_c", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Console.WriteLine($"  Found {vmdlFiles.Count} vmdl_c entries");

            foreach (var entry in vmdlFiles)
            {
                Console.WriteLine($"  Processing vmdl_c entry with {entry.Value.Count} files");

                // Iterate through all files in this entry type
                foreach (var file in entry.Value)
                {
                    //Console.WriteLine($"    File: {file.FileName} in directory: {file.DirectoryName}");

                    // Check if this is a world_physics file
                    if (file.FileName.Contains("world_physics", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"    Found world_physics file: {file.FileName}");

                        // Extract map name from directory or filename
                        string mapName = ExtractMapNameFromFile(file);
                        if (string.IsNullOrEmpty(mapName))
                        {
                            Console.WriteLine($"    Could not extract map name");
                            continue;
                        }

                        Console.WriteLine($"    Extracting PHYS data for map: {mapName}");

                        // Process this file
                        ProcessWorldPhysicsFile(package, file, mapName, outputDir);
                    }
                }
            }
        }

        static string ExtractMapNameFromFile(SteamDatabase.ValvePak.PackageEntry file)
        {
            // Try to get map name from directory first
            if (!string.IsNullOrEmpty(file.DirectoryName))
            {
                var parts = file.DirectoryName.Split('/', '\\');
                // Look for the map directory (usually after "maps")
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].Equals("maps", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
                    {
                        return parts[i + 1];
                    }
                }
                // If no "maps" directory, use the last directory part
                return parts[parts.Length - 1];
            }

            // Fallback to filename
            return file.FileName.Replace("world_physics", "").Replace(".vmdl_c", "").Trim('_', '.');
        }

        static void ProcessWorldPhysicsFile(Package package, SteamDatabase.ValvePak.PackageEntry file, string mapName, string outputDir)
        {
            try
            {
                Console.WriteLine($"    Reading file data for {mapName}...");

                // Read the file
                package.ReadEntry(file, out byte[] fileData);
                Console.WriteLine($"    File data size: {fileData.Length} bytes");

                if (fileData == null || fileData.Length == 0)
                {
                    Console.WriteLine($"    ERROR: No data read from file");
                    return;
                }

                // Parse the resource
                using (var resource = new Resource())
                {
                    Console.WriteLine($"    Parsing resource...");
                    resource.Read(new MemoryStream(fileData));
                    resource.FileName = $"{file.DirectoryName}/{file.FileName}";

                    Console.WriteLine($"    Resource contains {resource.Blocks.Count} blocks");

                    // Debug: Show all block types
                    var blockTypes = resource.Blocks.Select(b => b.Type.ToString()).ToList();
                    Console.WriteLine($"    Block types found: {string.Join(", ", blockTypes)}");

                    // Find and extract PHYS block
                    var physBlock = resource.Blocks.FirstOrDefault(b => b.Type == BlockType.PHYS);
                    if (physBlock != null)
                    {
                        Console.WriteLine($"    Found PHYS block, converting to string...");
                        string physData = physBlock.ToString();
                        Console.WriteLine($"    PHYS data length: {physData.Length} characters");

                        if (string.IsNullOrEmpty(physData))
                        {
                            Console.WriteLine($"    WARNING: PHYS data is empty for {mapName}");
                            return;
                        }

                        string outputPath = Path.Combine(outputDir, $"{mapName}.vphys");
                        Console.WriteLine($"    Writing to: {outputPath}");

                        File.WriteAllText(outputPath, physData);
                        Console.WriteLine($"    SUCCESS: Written {mapName}.vphys ({physData.Length} characters)");

                        // Verify the file was actually written
                        if (File.Exists(outputPath))
                        {
                            var fileInfo = new FileInfo(outputPath);
                            Console.WriteLine($"    File verification: {fileInfo.Length} bytes written to disk");
                        }
                        else
                        {
                            Console.WriteLine($"    ERROR: File was not created on disk!");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"    WARNING: No PHYS block found in {mapName}");
                        Console.WriteLine($"    Available blocks: {string.Join(", ", resource.Blocks.Select(b => $"{b.Type}({b.Size} bytes)"))}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ERROR: Failed to process {mapName}: {ex.Message}");
                Console.WriteLine($"    Stack trace: {ex.StackTrace}");
            }
        }
    }
}