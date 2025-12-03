using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Grex.Services
{
    public class WslDistribution
    {
        public string Name { get; set; } = string.Empty;
        public int Version { get; set; }
        public string Guid { get; set; } = string.Empty;
        public string? BasePath { get; set; }
        public int State { get; set; }
    }

    public static class WindowsSubsystemLinuxService
    {
        /// <summary>
        /// Retrieves all installed WSL distributions from the registry.
        /// Reads HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Lxss
        /// No admin rights needed as it's per-user.
        /// </summary>
        public static List<WslDistribution> GetWslDistributions()
        {
            var distributions = new List<WslDistribution>();

            try
            {
                using var lxssKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Lxss");
                if (lxssKey == null) return distributions;

                foreach (string subKeyName in lxssKey.GetSubKeyNames())
                {
                    using var distroKey = lxssKey.OpenSubKey(subKeyName);
                    if (distroKey == null) continue;

                    var distro = new WslDistribution
                    {
                        Guid = subKeyName,
                        Name = distroKey.GetValue("DistributionName")?.ToString() ?? "Unknown",
                        Version = (int?)distroKey.GetValue("Version") ?? 0,
                        BasePath = distroKey.GetValue("BasePath")?.ToString(),
                        State = (int?)distroKey.GetValue("State") ?? 0
                    };
                    distributions.Add(distro);
                }
            }
            catch
            {
                // If we can't read the registry, return empty list
            }

            return distributions;
        }

        /// <summary>
        /// Attempts to convert a mounted WSL drive path (like P:\home\user\...) to a native WSL path
        /// (like \\wsl$\Ubuntu-24.04\home\user\...) by checking which WSL distribution contains the path.
        /// </summary>
        /// <param name="mountedPath">The path using a mounted drive letter (e.g., P:\home\user\project)</param>
        /// <returns>The native WSL path if found, or the original path if conversion fails</returns>
        public static string TryConvertToNativeWslPath(string mountedPath)
        {
            if (string.IsNullOrEmpty(mountedPath))
                return mountedPath;

            // Extract the relative path after the drive letter (e.g., "home\user\project" from "P:\home\user\project")
            string relativePath;
            if (mountedPath.Length >= 3 && char.IsLetter(mountedPath[0]) && mountedPath[1] == ':')
            {
                // Handle both P:\home\... and P:/home/...
                relativePath = mountedPath.Substring(2).TrimStart('\\', '/');
            }
            else
            {
                return mountedPath; // Not a drive letter path
            }

            // Normalize path separators
            relativePath = relativePath.Replace('/', '\\');

            // Get all WSL distributions
            var distributions = GetWslDistributions();
            if (distributions.Count == 0)
                return mountedPath;

            // Try each distribution to find one where the path exists
            foreach (var distro in distributions)
            {
                if (string.IsNullOrEmpty(distro.Name))
                    continue;

                // Try both \\wsl$ and \\wsl.localhost formats
                var wslPaths = new[]
                {
                    $@"\\wsl.localhost\{distro.Name}\{relativePath}",
                    $@"\\wsl$\{distro.Name}\{relativePath}"
                };

                foreach (var wslPath in wslPaths)
                {
                    try
                    {
                        if (Directory.Exists(wslPath) || File.Exists(wslPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"WSL path conversion: {mountedPath} -> {wslPath}");
                            return wslPath;
                        }
                    }
                    catch
                    {
                        // Path access failed, try next
                    }
                }
            }

            // No matching WSL path found, return original
            System.Diagnostics.Debug.WriteLine($"WSL path conversion failed: No matching distribution found for {mountedPath}");
            return mountedPath;
        }

        /// <summary>
        /// Checks if a path looks like it could be a WSL path accessed through a Windows mount point.
        /// Looks for patterns like X:\home\... which typically indicate WSL file access.
        /// </summary>
        public static bool IsLikelyMountedWslPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length < 8)
                return false;

            // Check for pattern: [A-Z]:\home\ (any drive letter followed by \home\)
            if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == ':')
            {
                var remainder = path.Substring(2);
                if (remainder.StartsWith("\\home\\", StringComparison.OrdinalIgnoreCase) ||
                    remainder.StartsWith("/home/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
