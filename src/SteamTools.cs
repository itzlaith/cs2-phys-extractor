using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PhysExtractor.src
{
    public static class SteamTools
    {
        public static string? GetSteamInstallPath()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\\WOW6432Node\\Valve\\Steam");

                if (key?.GetValue("InstallPath") is string installPath)
                    return installPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Steam] Issue reading Steam Path: {ex.Message}");
            }

            return null;
        }

        public static List<string> GetAllSteamLibraryPaths(string steamPath, bool log = false)
        {
            var result = new List<string>();
            string vdfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");

            if (!File.Exists(vdfPath))
                return result;

            var lines = File.ReadAllLines(vdfPath);
            string? currentPath = null;
            bool inApps = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.StartsWith("\"path\""))
                {
                    var match = Regex.Match(line, "\"path\"\\s+\"([^\"]+)\"");
                    if (match.Success)
                    {
                        currentPath = match.Groups[1].Value.Replace(@"\\", @"\");

                        if (log) 
                            Console.WriteLine("[Steam] Found: " + currentPath);
                    }
                }

                if (line.StartsWith("\"apps\""))
                {
                    inApps = true;
                    continue;
                }

                if (inApps && line.StartsWith("}"))
                {
                    inApps = false;
                    if (currentPath != null)
                    {
                        var full = Path.Combine(currentPath, "steamapps");

                        if (Directory.Exists(full))
                            result.Add(full);

                        currentPath = null;
                    }
                }
            }

            return result;
        }

        public static string? FindCS2InstallPath(bool log = false)
        {
            const string appId = "730";

            string? steamPath = GetSteamInstallPath();

            if (steamPath == null)
                return null;

            var libraries = GetAllSteamLibraryPaths(steamPath, log);
            libraries.Insert(0, Path.Combine(steamPath, "steamapps"));

            foreach (var lib in libraries)
            {
                if (log) Console.WriteLine("[Steam] Checking: " + lib);

                var manifest = Path.Combine(lib, $"appmanifest_{appId}.acf");
                if (!File.Exists(manifest))
                    continue;

                var common = Path.Combine(lib, "common");

                string? installdir = null;
                foreach (var line in File.ReadLines(manifest))
                {
                    var match = Regex.Match(line, "\"installdir\"\\s+\"([^\"]+)\"");
                    if (match.Success)
                    {
                        installdir = match.Groups[1].Value;
                        break;
                    }
                }

                if (installdir != null)
                {
                    var full = Path.Combine(common, installdir);
                    if (Directory.Exists(full))
                    {
                        if (log) Console.WriteLine("[Steam] Found CS2 at: " + full);
                        return full;
                    }
                }

                var fallback = Path.Combine(common, "Counter-Strike Global Offensive");
                if (Directory.Exists(fallback))
                {
                    if (log) 
                        Console.WriteLine("[Steam] Found CS2 fallback at: " + fallback);

                    return fallback;
                }
            }

            if (log) 
                Console.WriteLine("[Steam] CS2 not found.");

            return null;
        }
    }
}
