using Microsoft.Win32;
using SteamDatabase.ValvePak;
using System.Runtime.InteropServices;
using System.Text;
using ValveResourceFormat;

namespace PhysExtractor.src
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("CS2 PHYS Data Extractor");
            Console.WriteLine("========================");

            try
            {
                // Find Steam installation
                string? steamPath = FindSteamPath();
                if (string.IsNullOrEmpty(steamPath))
                {
                    Console.WriteLine("ERROR: Steam installation not found!");
                    return;
                }

                Console.WriteLine($"Steam found at: {steamPath}");

                // Show menu for map selection
                ShowMapMenu();
                int mapChoice = GetUserChoice(1, 3);

                // Show menu for output format selection
                ShowFormatMenu();
                int formatChoice = GetUserChoice(1, 3);

                // Create output directories based on format choice
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string? triOutputDir = null;
                string? vphysOutputDir = null;

                if (formatChoice == 1 || formatChoice == 2) // Both or TRI only
                {
                    triOutputDir = Path.Combine(baseDir, "tri");
                    Directory.CreateDirectory(triOutputDir);
                }

                if (formatChoice == 1 || formatChoice == 3) // Both or VPHYS only
                {
                    vphysOutputDir = Path.Combine(baseDir, "vphys");
                    Directory.CreateDirectory(vphysOutputDir);
                }

                switch (mapChoice)
                {
                    case 1:
                        ProcessOfficialMaps(steamPath, triOutputDir, vphysOutputDir);
                        ProcessWorkshopMaps(steamPath, triOutputDir, vphysOutputDir);
                        break;
                    case 2:
                        ProcessOfficialMaps(steamPath, triOutputDir, vphysOutputDir);
                        break;
                    case 3:
                        ProcessWorkshopMaps(steamPath, triOutputDir, vphysOutputDir);
                        break;
                }

                Console.WriteLine("\nExtraction completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void ShowMapMenu()
        {
            Console.WriteLine("\nWhat would you like to process?");
            Console.WriteLine("1. Process all maps (workshop + official)");
            Console.WriteLine("2. Process only official maps");
            Console.WriteLine("3. Process only workshop maps");
            Console.Write("Enter your choice (1-3): ");
        }

        static void ShowFormatMenu()
        {
            Console.WriteLine("\nWhich output format would you like?");
            Console.WriteLine("1. Both formats (.tri and .vphys)");
            Console.WriteLine("2. TRI format only (.tri)");
            Console.WriteLine("3. VPHYS format only (.vphys)");
            Console.Write("Enter your choice (1-3): ");
        }

        static int GetUserChoice(int min, int max)
        {
            while (true)
            {
                string? input = Console.ReadLine();
                if (int.TryParse(input, out int choice) && choice >= min && choice <= max)
                    return choice;
                Console.Write($"Please enter a valid choice ({min}-{max}): ");
            }
        }

        static string? FindSteamPath()
        {
            try
            {
                // Only try registry access on Windows
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    {
                        if (key?.GetValue("SteamPath") is string steamPath)
                            return steamPath.Replace('/', '\\');
                    }
                }

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

        static void ProcessOfficialMaps(string steamPath, string? triOutputDir, string? vphysOutputDir)
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
                    return !fileName.Contains("vanity") && !fileName.Contains("workshop") &&
                           !fileName.Contains("graphics_settings") && !fileName.Contains("lobby_mapveto") &&
                           (fileName.StartsWith("de_") || fileName.StartsWith("cs_") || fileName.StartsWith("ar_"));
                }).ToList();

            Console.WriteLine($"Found {vpkFiles.Count} official map VPK files");
            foreach (string vpkFile in vpkFiles)
            {
                try
                {
                    ProcessVpkFile(vpkFile, triOutputDir, vphysOutputDir);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {Path.GetFileName(vpkFile)}: {ex.Message}");
                }
            }
        }

        static void ProcessWorkshopMaps(string steamPath, string? triOutputDir, string? vphysOutputDir)
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

                    if (vpkFiles.Count > 0)
                    {
                        string baseVpk = FindBaseVpkFile(vpkFiles) ?? vpkFiles.OrderByDescending(f => new FileInfo(f).Length).First();
                        ProcessVpkFile(baseVpk, triOutputDir, vphysOutputDir);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing workshop directory {Path.GetFileName(workshopDir)}: {ex.Message}");
                }
            }
        }

        static string? FindBaseVpkFile(List<string> vpkFiles)
        {
            return vpkFiles.FirstOrDefault(vpkFile => {
                string fileName = Path.GetFileNameWithoutExtension(vpkFile);
                return !fileName.EndsWith("_000") && !fileName.EndsWith("_001") &&
                       !fileName.EndsWith("_002") && !fileName.EndsWith("_003") &&
                       !fileName.EndsWith("_004") && !fileName.EndsWith("_005") &&
                       !fileName.EndsWith("_006") && !fileName.EndsWith("_007") &&
                       !fileName.EndsWith("_008") && !fileName.EndsWith("_009");
            });
        }

        static void ProcessVpkFile(string vpkPath, string? triOutputDir, string? vphysOutputDir)
        {
            Console.WriteLine($"Processing: {Path.GetFileName(vpkPath)}");

            try
            {
                using (var package = new Package())
                {
                    package.Read(vpkPath);

                    // Look for nested VPK files (workshop maps)
                    if (package.Entries.ContainsKey("vpk"))
                    {
                        var mapVpkFiles = package.Entries["vpk"]
                            .Where(f => f.DirectoryName?.Equals("maps", StringComparison.OrdinalIgnoreCase) == true)
                            .ToList();

                        if (mapVpkFiles.Any())
                        {
                            var mainMapVpk = mapVpkFiles
                                .Where(f => !f.FileName.Contains("_3dsky", StringComparison.OrdinalIgnoreCase) &&
                                           !f.FileName.Contains("_skybox", StringComparison.OrdinalIgnoreCase) &&
                                           !f.FileName.Contains("_sky", StringComparison.OrdinalIgnoreCase))
                                .OrderBy(f => f.FileName.Length)
                                .FirstOrDefault() ?? mapVpkFiles.First();

                            ProcessNestedVpkFile(package, mainMapVpk, triOutputDir, vphysOutputDir);
                            return;
                        }
                    }

                    // Process as official map
                    ProcessOfficialMapVpk(package, triOutputDir, vphysOutputDir);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to open VPK {Path.GetFileName(vpkPath)}: {ex.Message}");
            }
        }

        static void ProcessNestedVpkFile(Package parentPackage, PackageEntry nestedVpkFile, string? triOutputDir, string? vphysOutputDir)
        {
            try
            {
                parentPackage.ReadEntry(nestedVpkFile, out byte[] vpkData);
                using (var memoryStream = new MemoryStream(vpkData))
                using (var nestedPackage = new Package())
                {
                    nestedPackage.SetFileName($"{nestedVpkFile.DirectoryName}/{nestedVpkFile.FileName}.vpk");
                    nestedPackage.Read(memoryStream);
                    ProcessOfficialMapVpk(nestedPackage, triOutputDir, vphysOutputDir);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error processing nested VPK: {ex.Message}");
            }
        }

        static void ProcessOfficialMapVpk(Package package, string? triOutputDir, string? vphysOutputDir)
        {
            var vmdlFiles = package.Entries
                .Where(kvp => kvp.Key.Equals("vmdl_c", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Console.WriteLine($"  Found {vmdlFiles.Count} vmdl_c entries");

            foreach (var entry in vmdlFiles)
            {
                foreach (var file in entry.Value)
                {
                    if (file.FileName.Contains("world_physics", StringComparison.OrdinalIgnoreCase))
                    {
                        string? mapName = ExtractMapNameFromFile(file);
                        if (string.IsNullOrEmpty(mapName)) continue;

                        Console.WriteLine($"  Processing collision data for: {mapName}");
                        ProcessWorldPhysicsFile(package, file, mapName, triOutputDir, vphysOutputDir);
                    }
                }
            }
        }

        static string? ExtractMapNameFromFile(PackageEntry file)
        {
            if (!string.IsNullOrEmpty(file.DirectoryName))
            {
                var parts = file.DirectoryName.Split('/', '\\');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].Equals("maps", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
                        return parts[i + 1];
                }
                return parts[parts.Length - 1];
            }
            return file.FileName.Replace("world_physics", "").Replace(".vmdl_c", "").Trim('_', '.');
        }

        static void ProcessWorldPhysicsFile(Package package, PackageEntry file, string mapName, string? triOutputDir, string? vphysOutputDir)
        {
            try
            {
                package.ReadEntry(file, out byte[] fileData);
                if (fileData == null || fileData.Length == 0)
                {
                    Console.WriteLine($"    ERROR: No data read from file");
                    return;
                }

                using (var resource = new Resource())
                {
                    resource.Read(new MemoryStream(fileData));
                    resource.FileName = $"{file.DirectoryName}/{file.FileName}";

                    var physBlock = resource.Blocks.FirstOrDefault(b => b.Type == BlockType.PHYS);
                    if (physBlock == null)
                    {
                        Console.WriteLine($"    WARNING: No PHYS block found in {mapName}");
                        return;
                    }

                    // Get raw PHYS data for .vphys output
                    if (vphysOutputDir != null)
                    {
                        string vphysPath = Path.Combine(vphysOutputDir, $"{mapName}.vphys");
                        WriteVphysFile(physBlock, vphysPath);
                        Console.WriteLine($"    Written: {mapName}.vphys");
                    }

                    // Parse and extract collision triangles for .tri output
                    if (triOutputDir != null)
                    {
                        string physData = physBlock.ToString();
                        if (string.IsNullOrEmpty(physData))
                        {
                            Console.WriteLine($"    WARNING: PHYS data is empty for {mapName}");
                            return;
                        }

                        var triangles = ParsePhysicsData(physData);

                        if (triangles.Count > 0)
                        {
                            string triPath = Path.Combine(triOutputDir, $"{mapName}.tri");
                            WriteTriangleFile(triangles, triPath);
                            Console.WriteLine($"    Written: {mapName}.tri ({triangles.Count} triangles)");
                        }
                        else
                        {
                            Console.WriteLine($"    WARNING: No collision triangles found for {mapName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ERROR: Failed to process {mapName}: {ex.Message}");
            }
        }

        static void WriteVphysFile(Block physBlock, string outputPath)
        {
            // Get the raw PHYS block data
            string physData = physBlock.ToString();
            File.WriteAllText(outputPath, physData, Encoding.UTF8);
        }

        static List<Triangle> ParsePhysicsData(string physData)
        {
            var triangles = new List<Triangle>();
            var parser = new KV3Parser(physData);

            // Get collision attribute indices for "default" collision group
            var collisionIndices = GetCollisionAttributeIndices(parser);

            // Process hulls
            ProcessHulls(parser, collisionIndices, triangles);

            // Process meshes
            ProcessMeshes(parser, collisionIndices, triangles);

            return triangles;
        }

        static List<int> GetCollisionAttributeIndices(KV3Parser parser)
        {
            var indices = new List<int>();
            int index = 0;

            while (true)
            {
                string path = $"m_collisionAttributes[{index}].m_CollisionGroupString";
                string collisionGroup = parser.GetValue(path);

                if (string.IsNullOrEmpty(collisionGroup))
                    break;

                // Remove quotes and check for default group
                string cleanGroup = collisionGroup.Trim('"');
                if (cleanGroup.Equals("default", StringComparison.OrdinalIgnoreCase))
                    indices.Add(index);

                index++;
                if (index > 10) // Safety break
                    break;
            }

            return indices;
        }

        static void ProcessHulls(KV3Parser parser, List<int> collisionIndices, List<Triangle> triangles)
        {
            int index = 0;

            while (true)
            {
                string path = $"m_parts[0].m_rnShape.m_hulls[{index}].m_nCollisionAttributeIndex";
                string collisionIndexStr = parser.GetValue(path);

                if (string.IsNullOrEmpty(collisionIndexStr))
                    break;

                if (int.TryParse(collisionIndexStr, out int collisionIndex) && collisionIndices.Contains(collisionIndex))
                {
                    // Get vertex data
                    string vertexPath = $"m_parts[0].m_rnShape.m_hulls[{index}].m_Hull.m_VertexPositions";
                    string vertexData = parser.GetValue(vertexPath);

                    if (string.IsNullOrEmpty(vertexData))
                    {
                        vertexPath = $"m_parts[0].m_rnShape.m_hulls[{index}].m_Hull.m_Vertices";
                        vertexData = parser.GetValue(vertexPath);
                    }

                    if (!string.IsNullOrEmpty(vertexData))
                    {
                        var vertices = ParseFloatArray(vertexData);
                        var faces = ParseByteArray(parser.GetValue($"m_parts[0].m_rnShape.m_hulls[{index}].m_Hull.m_Faces"));
                        var edges = ParseEdgeArray(parser.GetValue($"m_parts[0].m_rnShape.m_hulls[{index}].m_Hull.m_Edges"));

                        ConvertHullToTriangles(vertices, faces, edges, triangles);
                    }
                }

                index++;
                if (index > 100) // Safety break
                    break;
            }
        }

        static void ProcessMeshes(KV3Parser parser, List<int> collisionIndices, List<Triangle> triangles)
        {
            int index = 0;

            while (true)
            {
                string path = $"m_parts[0].m_rnShape.m_meshes[{index}].m_nCollisionAttributeIndex";
                string collisionIndexStr = parser.GetValue(path);

                if (string.IsNullOrEmpty(collisionIndexStr))
                    break;

                if (int.TryParse(collisionIndexStr, out int collisionIndex) && collisionIndices.Contains(collisionIndex))
                {
                    var triangleIndices = ParseIntArray(parser.GetValue($"m_parts[0].m_rnShape.m_meshes[{index}].m_Mesh.m_Triangles"));
                    var vertices = ParseFloatArray(parser.GetValue($"m_parts[0].m_rnShape.m_meshes[{index}].m_Mesh.m_Vertices"));

                    ConvertMeshToTriangles(vertices, triangleIndices, triangles);
                }

                index++;
                if (index > 100) // Safety break
                    break;
            }
        }

        static void ConvertHullToTriangles(List<Vector3> vertices, List<byte> faces, List<Edge> edges, List<Triangle> triangles)
        {
            foreach (byte startEdge in faces)
            {
                int edge = edges[startEdge].next;
                while (edge != startEdge)
                {
                    int nextEdge = edges[edge].next;
                    triangles.Add(new Triangle
                    {
                        p1 = vertices[edges[startEdge].origin],
                        p2 = vertices[edges[edge].origin],
                        p3 = vertices[edges[nextEdge].origin]
                    });
                    edge = nextEdge;
                }
            }
        }

        static void ConvertMeshToTriangles(List<Vector3> vertices, List<int> triangleIndices, List<Triangle> triangles)
        {
            for (int i = 0; i < triangleIndices.Count; i += 3)
            {
                triangles.Add(new Triangle
                {
                    p1 = vertices[triangleIndices[i]],
                    p2 = vertices[triangleIndices[i + 1]],
                    p3 = vertices[triangleIndices[i + 2]]
                });
            }
        }

        static List<Vector3> ParseFloatArray(string data)
        {
            var floats = ParseFloatBytes(data);
            var vertices = new List<Vector3>();

            for (int i = 0; i < floats.Count; i += 3)
            {
                vertices.Add(new Vector3 { x = floats[i], y = floats[i + 1], z = floats[i + 2] });
            }

            return vertices;
        }

        static List<float> ParseFloatBytes(string data)
        {
            var bytes = ParseBytes(data);
            var floats = new List<float>();

            // Ensure we have enough bytes for complete floats (4 bytes each)
            int completeFloats = bytes.Count / 4;

            for (int i = 0; i < completeFloats; i++)
            {
                // Create a 4-byte array for this float
                byte[] floatBytes = new byte[4];
                for (int j = 0; j < 4; j++)
                {
                    floatBytes[j] = bytes[i * 4 + j];
                }

                floats.Add(BitConverter.ToSingle(floatBytes, 0));
            }

            return floats;
        }

        static List<int> ParseIntArray(string data)
        {
            var bytes = ParseBytes(data);
            var ints = new List<int>();

            // Ensure we have enough bytes for complete ints (4 bytes each)
            int completeInts = bytes.Count / 4;

            for (int i = 0; i < completeInts; i++)
            {
                // Create a 4-byte array for this int
                byte[] intBytes = new byte[4];
                for (int j = 0; j < 4; j++)
                {
                    intBytes[j] = bytes[i * 4 + j];
                }

                ints.Add(BitConverter.ToInt32(intBytes, 0));
            }

            return ints;
        }

        static List<byte> ParseByteArray(string data)
        {
            return ParseBytes(data);
        }

        static List<Edge> ParseEdgeArray(string data)
        {
            var bytes = ParseBytes(data);
            var edges = new List<Edge>();

            for (int i = 0; i < bytes.Count; i += 4)
            {
                edges.Add(new Edge
                {
                    next = bytes[i],
                    twin = bytes[i + 1],
                    origin = bytes[i + 2],
                    face = bytes[i + 3]
                });
            }

            return edges;
        }

        static List<byte> ParseBytes(string data)
        {
            var bytes = new List<byte>();

            // Remove any whitespace and clean the data
            data = data.Trim();

            // Handle different possible formats
            if (data.StartsWith("#[") && data.EndsWith("]"))
            {
                // Remove the #[ and ] wrapper
                data = data.Substring(2, data.Length - 3);
            }

            string[] parts = data.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                string cleanPart = part.Trim();
                if (cleanPart.Length == 2 && IsHex(cleanPart))
                {
                    try
                    {
                        bytes.Add(Convert.ToByte(cleanPart, 16));
                    }
                    catch
                    {
                        // Skip invalid hex bytes
                    }
                }
            }

            return bytes;
        }

        static bool IsHex(string input)
        {
            return input.All(c => "0123456789ABCDEFabcdef".Contains(c));
        }

        static void WriteTriangleFile(List<Triangle> triangles, string outputPath)
        {
            using (var fs = new FileStream(outputPath, FileMode.Create))
            using (var writer = new BinaryWriter(fs))
            {
                foreach (var triangle in triangles)
                {
                    writer.Write(triangle.p1.x);
                    writer.Write(triangle.p1.y);
                    writer.Write(triangle.p1.z);
                    writer.Write(triangle.p2.x);
                    writer.Write(triangle.p2.y);
                    writer.Write(triangle.p2.z);
                    writer.Write(triangle.p3.x);
                    writer.Write(triangle.p3.y);
                    writer.Write(triangle.p3.z);
                }
            }
        }
    }
}