using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Grex.Services
{
    public interface IWindowsSearchIntegration
    {
        Task<WindowsSearchQueryResult> QueryIndexedFilesAsync(string rootPath, string searchTerm, bool includeSubfolders);
    }

    public sealed class WindowsSearchQueryResult
    {
        private WindowsSearchQueryResult(bool scopeAvailable, IReadOnlyList<string> paths)
        {
            ScopeAvailable = scopeAvailable;
            Paths = paths;
        }

        public bool ScopeAvailable { get; }

        public IReadOnlyList<string> Paths { get; }

        public static WindowsSearchQueryResult NotAvailable() => new(false, Array.Empty<string>());

        public static WindowsSearchQueryResult FromPaths(IEnumerable<string> paths)
        {
            var normalized = paths?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            return new(true, normalized);
        }
    }

    public class WindowsSearchIntegration : IWindowsSearchIntegration
    {
        private const string ConnectionString = "Provider=Search.CollatorDSO.2;Extended Properties='Application=Grex';";

        public async Task<WindowsSearchQueryResult> QueryIndexedFilesAsync(string rootPath, string searchTerm, bool includeSubfolders)
        {
            try
            {
                if (!OperatingSystem.IsWindows())
                    return WindowsSearchQueryResult.NotAvailable();

                if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(searchTerm))
                    return WindowsSearchQueryResult.NotAvailable();

                if (!Directory.Exists(rootPath))
                    return WindowsSearchQueryResult.NotAvailable();

                if (!IsSupportedPath(rootPath))
                    return WindowsSearchQueryResult.NotAvailable();

                return await Task.Run(() => QueryInternal(rootPath, searchTerm, includeSubfolders));
            }
            catch (Exception ex) when (ex is OleDbException or InvalidOperationException or DllNotFoundException)
            {
                Debug.WriteLine($"Windows Search integration unavailable: {ex.Message}");
                return WindowsSearchQueryResult.NotAvailable();
            }
        }

        private WindowsSearchQueryResult QueryInternal(string rootPath, string searchTerm, bool includeSubfolders)
        {
            using var connection = new OleDbConnection(ConnectionString);
            connection.Open();

            var scope = BuildScope(rootPath);
            if (!ScopeHasEntries(connection, scope))
                return WindowsSearchQueryResult.NotAvailable();

            var sanitizedScope = scope.Replace("'", "''");
            var sanitizedTerm = EscapeSearchTerm(searchTerm);
            var query = $"SELECT System.ItemPathDisplay FROM SYSTEMINDEX WHERE SCOPE='{sanitizedScope}' AND CONTAINS('\"{sanitizedTerm}\"')";

            using var command = new OleDbCommand(query, connection);
            using var reader = command.ExecuteReader();

            var files = new List<string>();
            if (reader != null)
            {
                while (reader.Read())
                {
                    if (!reader.IsDBNull(0))
                    {
                        var candidate = reader.GetString(0);
                        if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                        {
                            files.Add(candidate);
                        }
                    }
                }
            }

            if (!includeSubfolders && files.Count > 0)
            {
                var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
                files = files.Where(file =>
                {
                    try
                    {
                        var directory = Path.GetDirectoryName(file);
                        if (string.IsNullOrEmpty(directory))
                            return false;
                        var normalizedDirectory = Path.TrimEndingDirectorySeparator(directory);
                        return string.Equals(normalizedDirectory, normalizedRoot, StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                }).ToList();
            }

            return WindowsSearchQueryResult.FromPaths(files);
        }

        private static bool ScopeHasEntries(OleDbConnection connection, string scope)
        {
            using var command = connection.CreateCommand();
            var sanitizedScope = scope.Replace("'", "''");
            command.CommandText = $"SELECT TOP 1 System.ItemPathDisplay FROM SYSTEMINDEX WHERE SCOPE='{sanitizedScope}'";
            using var reader = command.ExecuteReader();
            return reader != null && reader.Read();
        }

        private static string BuildScope(string rootPath)
        {
            var normalized = Path.GetFullPath(rootPath);
            normalized = Path.TrimEndingDirectorySeparator(normalized) + "\\";
            var escaped = normalized.Replace("\\", "\\\\");
            return $"file:{escaped}";
        }

        private static string EscapeSearchTerm(string term)
        {
            var value = term.Trim();
            value = value.Replace("'", "''").Replace("\"", "\"\"");
            return value;
        }

        private static bool IsSupportedPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return path.Length >= 2 && path[1] == ':';
        }
    }
}

