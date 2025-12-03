using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Grex.Services
{
    public class RecentPathsService
    {
        private static readonly string RecentPathsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Grex",
            "search_path_history.json"
        );

        private const int MaxRecentPaths = 20;
        private static readonly object _lock = new object();

        public static List<string> GetRecentPaths()
        {
            lock (_lock)
            {
                try
                {
                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(RecentPathsFile);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    if (!File.Exists(RecentPathsFile))
                    {
                        return new List<string>();
                    }

                    var json = File.ReadAllText(RecentPathsFile);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return new List<string>();
                    }

                    var paths = JsonSerializer.Deserialize<List<string>>(json);
                    return paths ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }
        }

        public static void AddRecentPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            lock (_lock)
            {
                try
                {
                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(RecentPathsFile);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var recentPaths = GetRecentPaths();
                    
                    // Remove if already exists (to move it to top)
                    recentPaths.Remove(path);
                    
                    // Add to beginning
                    recentPaths.Insert(0, path);
                    
                    // Keep only the most recent paths
                    if (recentPaths.Count > MaxRecentPaths)
                    {
                        recentPaths = recentPaths.Take(MaxRecentPaths).ToList();
                    }

                    // Save to file
                    var json = JsonSerializer.Serialize(recentPaths, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(RecentPathsFile, json);
                }
                catch
                {
                    // Ignore errors when saving
                }
            }
        }

        public static List<string> FilterPaths(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return GetRecentPaths();
            }

            var allPaths = GetRecentPaths();
            var searchLower = searchText.ToLowerInvariant();
            
            return allPaths
                .Where(path => path.ToLowerInvariant().Contains(searchLower))
                .ToList();
        }

        public static void RemoveRecentPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            lock (_lock)
            {
                try
                {
                    var recentPaths = GetRecentPaths();
                    recentPaths.Remove(path);

                    // Save to file
                    var json = JsonSerializer.Serialize(recentPaths, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(RecentPathsFile, json);
                }
                catch
                {
                    // Ignore errors when saving
                }
            }
        }
    }
}

