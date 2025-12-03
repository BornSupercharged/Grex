using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;
using Grex.Models;

namespace Grex.Services
{
    public interface IDockerProcessRunner
    {
        Task<DockerProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken);
    }

    public record DockerProcessResult(int ExitCode, string StandardOutput, string StandardError);

    public class DockerSymlinkException : InvalidOperationException
    {
        public string OriginalError { get; }

        public DockerSymlinkException(string message, string originalError) : base(message)
        {
            OriginalError = originalError;
        }
    }

    internal sealed class DockerProcessRunner : IDockerProcessRunner
    {
        public async Task<DockerProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            using var process = new Process
            {
                StartInfo = startInfo
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start docker process.");
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            var stdout = await outputTask;
            var stderr = await errorTask;

            return new DockerProcessResult(process.ExitCode, stdout, stderr);
        }
    }

    /// <summary>
    /// Result of a Docker remote grep search.
    /// </summary>
    public class DockerGrepResult
    {
        /// <summary>
        /// Whether the grep search was successful (grep was available and executed).
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// The search results from grep.
        /// </summary>
        public List<SearchResult> Results { get; init; } = new();

        /// <summary>
        /// Error message if the search failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Whether grep was not available in the container (should fall back to mirror approach).
        /// </summary>
        public bool GrepNotAvailable { get; init; }
    }

    public class DockerSearchService
    {
        private const string DockerExecutable = "docker";
        private static readonly TimeSpan DefaultMirrorRetention = TimeSpan.FromHours(6);

        public static DockerSearchService Instance { get; } = new();

        private readonly IDockerProcessRunner _processRunner;
        private readonly string _mirrorRoot;
        private readonly Lazy<DockerClient?> _dockerClient;
        
        // Cache grep availability per container to avoid repeated checks
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _grepAvailabilityCache = new();

        public DockerSearchService(IDockerProcessRunner? processRunner = null, string? mirrorRoot = null)
        {
            _processRunner = processRunner ?? new DockerProcessRunner();
            _mirrorRoot = mirrorRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Grex", "docker-mirrors");
            _dockerClient = new Lazy<DockerClient?>(() =>
            {
                try
                {
                    return new DockerClientConfiguration().CreateClient();
                }
                catch
                {
                    return null;
                }
            });
        }

        public string MirrorRoot => _mirrorRoot;

        public async Task<bool> IsDockerAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await RunDockerAsync("version --format \"{{.Server.Version}}\"", cancellationToken, throwOnError: false);
                return result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if grep is available in the specified container.
        /// Results are cached per container to avoid repeated checks.
        /// </summary>
        public async Task<bool> IsGrepAvailableInContainerAsync(string containerId, CancellationToken cancellationToken = default)
        {
            // Check cache first
            if (_grepAvailabilityCache.TryGetValue(containerId, out var cached))
            {
                return cached;
            }

            try
            {
                var client = _dockerClient.Value;
                if (client == null)
                {
                    _grepAvailabilityCache[containerId] = false;
                    return false;
                }

                var execParams = new ContainerExecCreateParameters
                {
                    AttachStdout = true,
                    AttachStderr = true,
                    Cmd = new[] { "which", "grep" }
                };

                var exec = await client.Exec.ExecCreateContainerAsync(containerId, execParams, cancellationToken);
                using var stream = await client.Exec.StartAndAttachContainerExecAsync(exec.ID, false, cancellationToken);
                
                var (stdout, _) = await stream.ReadOutputToEndAsync(cancellationToken);
                var inspectResult = await client.Exec.InspectContainerExecAsync(exec.ID, cancellationToken);
                
                var isAvailable = inspectResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout);
                _grepAvailabilityCache[containerId] = isAvailable;
                return isAvailable;
            }
            catch
            {
                _grepAvailabilityCache[containerId] = false;
                return false;
            }
        }
        
        /// <summary>
        /// Clears the grep availability cache for a specific container or all containers.
        /// </summary>
        public void ClearGrepAvailabilityCache(string? containerId = null)
        {
            if (containerId != null)
            {
                _grepAvailabilityCache.TryRemove(containerId, out _);
            }
            else
            {
                _grepAvailabilityCache.Clear();
            }
        }

        /// <summary>
        /// Searches for a term within files in a Docker container using grep executed directly in the container.
        /// This is faster than mirroring files locally but requires grep to be available in the container.
        /// </summary>
        /// <param name="container">The container to search in.</param>
        /// <param name="containerPath">The path within the container to search.</param>
        /// <param name="searchTerm">The search term or regex pattern.</param>
        /// <param name="isRegex">Whether the search term is a regex pattern.</param>
        /// <param name="caseSensitive">Whether the search should be case-sensitive.</param>
        /// <param name="respectGitignore">Whether to respect .gitignore rules.</param>
        /// <param name="includeSystemFiles">Whether to include system files and directories.</param>
        /// <param name="includeSubfolders">Whether to search subdirectories recursively.</param>
        /// <param name="includeHiddenFiles">Whether to include hidden files (starting with .).</param>
        /// <param name="includeBinaryFiles">Whether to include binary files in results.</param>
        /// <param name="includeSymbolicLinks">Whether to follow symbolic links.</param>
        /// <param name="matchFileNames">Optional file name pattern to match (e.g., "*.cs").</param>
        /// <param name="excludeDirs">Optional directory exclusion pattern (e.g., ".git,node_modules" or "^(.git|vendor)$").</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A DockerGrepResult containing the search results or error information.</returns>
        public async Task<DockerGrepResult> SearchInContainerAsync(
            DockerContainerInfo container,
            string containerPath,
            string searchTerm,
            bool isRegex = false,
            bool caseSensitive = false,
            bool respectGitignore = false,
            bool includeSystemFiles = false,
            bool includeSubfolders = true,
            bool includeHiddenFiles = false,
            bool includeBinaryFiles = false,
            bool includeSymbolicLinks = false,
            string? matchFileNames = null,
            string? excludeDirs = null,
            CancellationToken cancellationToken = default)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            if (string.IsNullOrWhiteSpace(searchTerm))
                return new DockerGrepResult { Success = true, Results = new List<SearchResult>() };

            var client = _dockerClient.Value;
            if (client == null)
            {
                return new DockerGrepResult
                {
                    Success = false,
                    GrepNotAvailable = true,
                    ErrorMessage = "Docker API client is not available."
                };
            }

            // First, check if grep is available
            if (!await IsGrepAvailableInContainerAsync(container.Id, cancellationToken))
            {
                return new DockerGrepResult
                {
                    Success = false,
                    GrepNotAvailable = true,
                    ErrorMessage = "grep is not available in this container."
                };
            }

            try
            {
                var normalizedPath = NormalizeContainerPath(containerPath);
                
                // Build command with all filters at find level for maximum performance
                var grepArgs = BuildGrepCommand(
                    searchTerm, isRegex, caseSensitive, 
                    includeSubfolders, includeHiddenFiles, includeSystemFiles, includeBinaryFiles,
                    includeSymbolicLinks, matchFileNames, excludeDirs, normalizedPath);

                var execParams = new ContainerExecCreateParameters
                {
                    AttachStdout = true,
                    AttachStderr = true,
                    Cmd = grepArgs
                };

                var exec = await client.Exec.ExecCreateContainerAsync(container.Id, execParams, cancellationToken);
                using var stream = await client.Exec.StartAndAttachContainerExecAsync(exec.ID, false, cancellationToken);

                var (stdout, stderr) = await stream.ReadOutputToEndAsync(cancellationToken);
                var inspectResult = await client.Exec.InspectContainerExecAsync(exec.ID, cancellationToken);

                // Exit code 0 = matches found, 1 = no matches, 2+ = error
                if (inspectResult.ExitCode > 1)
                {
                    // Check if it's a "grep not found" type error
                    if (stderr.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                        stderr.Contains("command not found", StringComparison.OrdinalIgnoreCase))
                    {
                        return new DockerGrepResult
                        {
                            Success = false,
                            GrepNotAvailable = true,
                            ErrorMessage = $"grep command failed: {stderr}"
                        };
                    }

                    return new DockerGrepResult
                    {
                        Success = false,
                        GrepNotAvailable = false,
                        ErrorMessage = $"grep exited with code {inspectResult.ExitCode}: {stderr}"
                    };
                }

                var results = ParseGrepOutput(stdout, normalizedPath, searchTerm, isRegex, caseSensitive);
                
                // Most filters are now applied at the find command level for performance.
                // Only lightweight post-processing filters remain here.
                
                // Filter by filename pattern (post-processing, but fast since results are already filtered)
                if (!string.IsNullOrWhiteSpace(matchFileNames))
                {
                    results = results.Where(r => MatchesFileNamePattern(r.FileName, matchFileNames)).ToList();
                }
                
                // Filter by .gitignore - only if enabled (this adds an extra Docker exec, so skip if not needed)
                if (respectGitignore)
                {
                    var gitignorePatterns = await GetGitignorePatternsAsync(container.Id, normalizedPath, cancellationToken);
                    if (gitignorePatterns.Count > 0)
                    {
                        results = results.Where(r => !MatchesGitignorePatterns(r.FullPath, normalizedPath, gitignorePatterns)).ToList();
                    }
                }
                
                return new DockerGrepResult
                {
                    Success = true,
                    Results = results
                };
            }
            catch (Exception ex)
            {
                return new DockerGrepResult
                {
                    Success = false,
                    GrepNotAvailable = false,
                    ErrorMessage = $"Error executing grep in container: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Builds the grep command arguments based on search parameters.
        /// Moves as many filters as possible to the find command level for performance.
        /// </summary>
        internal static string[] BuildGrepCommand(
            string searchTerm,
            bool isRegex,
            bool caseSensitive,
            bool includeSubfolders,
            bool includeHiddenFiles,
            bool includeSystemFiles,
            bool includeBinaryFiles,
            bool includeSymbolicLinks,
            string? matchFileNames,
            string? excludeDirs,
            string searchPath)
        {
            // Build grep options string
            var grepOptions = new List<string> { "-Hn" }; // -H: print filename, -n: line numbers
            
            if (isRegex)
            {
                grepOptions.Add("-E"); // Extended regex
            }
            else
            {
                grepOptions.Add("-F"); // Fixed string (literal match)
            }

            if (!caseSensitive)
            {
                grepOptions.Add("-i");
            }

            grepOptions.Add("-s"); // Suppress error messages
            grepOptions.Add("-I"); // Skip binary files (grep level)

            var grepFlags = string.Join(" ", grepOptions);
            var escapedTerm = searchTerm.Replace("'", "'\"'\"'"); // Escape single quotes for shell
            var escapedPath = searchPath.Replace("'", "'\"'\"'");

            // Build find options - apply as many filters at find level for performance
            var findOptions = new List<string>();
            
            // When NOT including subfolders, limit depth
            if (!includeSubfolders)
            {
                findOptions.Add("-maxdepth 1");
            }
            
            // Follow symbolic links if requested (must be before other options)
            if (includeSymbolicLinks)
            {
                findOptions.Insert(0, "-L");
            }
            
            findOptions.Add("-type f");
            
            // Exclude hidden files/directories if not requested
            if (!includeHiddenFiles)
            {
                findOptions.Add("! -name '.*'");
                if (includeSubfolders)
                {
                    findOptions.Add("! -path '*/.*'");
                }
            }
            
            // Exclude system directories at find level (much faster than post-processing)
            if (!includeSystemFiles && includeSubfolders)
            {
                findOptions.Add("! -path '*/sys/*'");
                findOptions.Add("! -path '*/proc/*'");
                findOptions.Add("! -path '*/dev/*'");
                findOptions.Add("! -path '*/.git/*'");
                findOptions.Add("! -path '*/vendor/*'");
                findOptions.Add("! -path '*/node_modules/*'");
                findOptions.Add("! -path '*/storage/framework/*'");
                findOptions.Add("! -path '*/bin/*'");
                findOptions.Add("! -path '*/obj/*'");
            }
            
            // Exclude binary file extensions at find level (much faster than post-processing)
            if (!includeBinaryFiles)
            {
                var binaryExts = new[] { "exe", "dll", "bin", "zip", "tar", "gz", "7z", "rar",
                    "png", "jpg", "jpeg", "gif", "bmp", "ico", "webp",
                    "mp3", "mp4", "avi", "mkv", "wav",
                    "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx",
                    "so", "dylib", "a" };
                foreach (var ext in binaryExts)
                {
                    findOptions.Add($"! -name '*.{ext}'");
                }
            }
            
            // Exclude directories at find level using -prune (much faster than post-processing)
            if (!string.IsNullOrWhiteSpace(excludeDirs) && includeSubfolders)
            {
                var pruneCommands = BuildExcludeDirsFindOptions(excludeDirs);
                if (!string.IsNullOrEmpty(pruneCommands))
                {
                    findOptions.Add(pruneCommands);
                }
            }

            var findOpts = string.Join(" ", findOptions);
            
            // Use xargs with parallel execution (-P 4) for faster searching
            // -print0 and -0 handle filenames with spaces/special characters safely
            // -r prevents xargs from running grep when no files are found
            var shellCmd = $"find '{escapedPath}' {findOpts} -print0 2>/dev/null | xargs -0 -P 4 -r grep {grepFlags} -- '{escapedTerm}' 2>/dev/null || true";
            
            return new[] { "sh", "-c", shellCmd };
        }

        /// <summary>
        /// Converts excludeDirs pattern to find command options.
        /// Supports comma-separated names or regex patterns.
        /// </summary>
        private static string BuildExcludeDirsFindOptions(string excludeDirs)
        {
            if (string.IsNullOrWhiteSpace(excludeDirs))
                return string.Empty;

            var options = new List<string>();
            
            // Check if it's a regex pattern
            bool isRegex = excludeDirs.StartsWith("^") || 
                          excludeDirs.Contains("(") || 
                          excludeDirs.Contains("[") || 
                          excludeDirs.Contains("$");

            if (isRegex)
            {
                // For regex like ^(.git|node_modules|vendor|storage)$
                // Extract the directory names from the pattern
                var match = Regex.Match(excludeDirs, @"\(([^)]+)\)");
                if (match.Success)
                {
                    var dirs = match.Groups[1].Value.Split('|');
                    foreach (var dir in dirs)
                    {
                        var cleanDir = dir.Trim();
                        if (!string.IsNullOrEmpty(cleanDir))
                        {
                            options.Add($"! -path '*/{cleanDir}/*'");
                        }
                    }
                }
            }
            else
            {
                // Comma-separated directory names
                var dirs = excludeDirs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var dir in dirs)
                {
                    options.Add($"! -path '*/{dir}/*'");
                }
            }

            return string.Join(" ", options);
        }

        /// <summary>
        /// Parses grep output into SearchResult objects.
        /// Expected format: filename:line_number:line_content
        /// </summary>
        internal static List<SearchResult> ParseGrepOutput(string output, string basePath, string searchTerm, bool isRegex, bool caseSensitive)
        {
            var results = new List<SearchResult>();
            if (string.IsNullOrWhiteSpace(output))
                return results;

            // Compile regex if needed for match counting
            Regex? compiledRegex = null;
            if (isRegex)
            {
                var options = RegexOptions.Compiled;
                if (!caseSensitive)
                {
                    options |= RegexOptions.IgnoreCase;
                }
                try
                {
                    compiledRegex = new Regex(searchTerm, options);
                }
                catch
                {
                    // Invalid regex, fall back to literal search
                    isRegex = false;
                }
            }

            using var reader = new StringReader(output);
            string? line;
            // Regex to match grep output: filename:line_number:content
            // Handle filenames that may contain colons (Windows-style paths in containers are unlikely but possible)
            var grepLineRegex = new Regex(@"^(.+?):(\d+):(.*)$", RegexOptions.Compiled);

            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Skip binary file match messages
                if (line.StartsWith("Binary file", StringComparison.OrdinalIgnoreCase))
                    continue;

                var match = grepLineRegex.Match(line);
                if (match.Success)
                {
                    var filePath = match.Groups[1].Value;
                    var lineNumber = int.Parse(match.Groups[2].Value);
                    var lineContent = match.Groups[3].Value;

                    // Calculate relative path
                    var relativePath = filePath;
                    if (filePath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                    {
                        relativePath = filePath.Substring(basePath.Length).TrimStart('/');
                    }

                    // Calculate column number and match count
                    var columnNumber = CalculateColumnNumber(lineContent, searchTerm, isRegex, compiledRegex, caseSensitive);
                    var matchCount = CountMatchesOnLine(lineContent, searchTerm, isRegex, compiledRegex, caseSensitive);

                    // Sanitize line content for display
                    var displayContent = lineContent.Length > 500 ? lineContent[..500] + "..." : lineContent;

                    results.Add(new SearchResult
                    {
                        FullPath = filePath,
                        FileName = Path.GetFileName(filePath),
                        RelativePath = relativePath,
                        LineNumber = lineNumber,
                        ColumnNumber = columnNumber,
                        LineContent = displayContent,
                        MatchCount = matchCount
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Calculates the column number (1-based) of the first match in a line.
        /// </summary>
        private static int CalculateColumnNumber(string line, string searchTerm, bool isRegex, Regex? compiledRegex, bool caseSensitive)
        {
            if (string.IsNullOrEmpty(line))
                return 1;

            if (isRegex && compiledRegex != null)
            {
                var match = compiledRegex.Match(line);
                if (match.Success)
                {
                    return match.Index + 1;
                }
            }
            else
            {
                var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                var index = line.IndexOf(searchTerm, comparison);
                if (index >= 0)
                {
                    return index + 1;
                }
            }

            return 1;
        }

        /// <summary>
        /// Counts the number of matches on a single line.
        /// </summary>
        private static int CountMatchesOnLine(string line, string searchTerm, bool isRegex, Regex? compiledRegex, bool caseSensitive)
        {
            if (string.IsNullOrEmpty(line))
                return 0;

            if (isRegex && compiledRegex != null)
            {
                return compiledRegex.Matches(line).Count;
            }
            else
            {
                var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                int count = 0;
                int index = 0;
                while ((index = line.IndexOf(searchTerm, index, comparison)) >= 0)
                {
                    count++;
                    index += searchTerm.Length;
                }
                return Math.Max(count, 1); // At least 1 if grep found a match
            }
        }

        /// <summary>
        /// Checks if a filename matches the specified pattern string.
        /// Supports wildcard patterns separated by '|' and exclusion patterns prefixed with '-'.
        /// This is used for post-processing filtering instead of grep's slow --include option.
        /// </summary>
        internal static bool MatchesFileNamePattern(string fileName, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return true;

            var patterns = pattern.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (patterns.Length == 0)
                return true;

            var includePatterns = new List<string>();
            var excludePatterns = new List<string>();

            foreach (var p in patterns)
            {
                if (p.StartsWith("-"))
                {
                    excludePatterns.Add(p.Substring(1));
                }
                else
                {
                    includePatterns.Add(p);
                }
            }

            // Check exclusion patterns first
            foreach (var excludePattern in excludePatterns)
            {
                if (MatchesWildcard(fileName, excludePattern))
                {
                    return false;
                }
            }

            // If no include patterns specified, include all (that weren't excluded)
            if (includePatterns.Count == 0)
                return true;

            // Check if file matches any include pattern
            foreach (var includePattern in includePatterns)
            {
                if (MatchesWildcard(fileName, includePattern))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Matches a filename against a wildcard pattern (supports * and ?).
        /// </summary>
        private static bool MatchesWildcard(string fileName, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return true;

            // Convert wildcard pattern to regex
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            return Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Checks if a file path should be excluded based on directory exclusion patterns.
        /// Supports comma-separated directory names or regex patterns.
        /// This is used for post-processing filtering instead of grep's slow --exclude-dir option.
        /// </summary>
        internal static bool ShouldExcludeByDirectory(string filePath, string rootPath, string excludeDirs)
        {
            if (string.IsNullOrWhiteSpace(excludeDirs))
                return false;

            // Get the directory part of the file path
            var lastSlash = filePath.LastIndexOf('/');
            if (lastSlash < 0)
                return false;

            var directory = filePath.Substring(0, lastSlash);
            
            // Get relative path from root
            var normalizedRoot = rootPath.TrimEnd('/');
            var relativePath = directory;
            if (directory.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = directory.Substring(normalizedRoot.Length).TrimStart('/');
            }

            if (string.IsNullOrEmpty(relativePath))
                return false;

            // Check if it's a regex pattern (starts with ^ or contains regex special chars)
            bool isRegex = excludeDirs.StartsWith("^") || 
                          excludeDirs.Contains("(") || 
                          excludeDirs.Contains("[") || 
                          excludeDirs.Contains("$");

            if (isRegex)
            {
                try
                {
                    var regex = new Regex(excludeDirs, RegexOptions.IgnoreCase);
                    // Check each directory component in the path
                    var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        if (regex.IsMatch(part))
                            return true;
                    }
                }
                catch
                {
                    // If regex is invalid, fall back to literal matching
                    isRegex = false;
                }
            }

            if (!isRegex)
            {
                // Comma-separated directory names
                var dirNames = excludeDirs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var dirName in dirNames)
                {
                    if (parts.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a file is directly in the root directory (not in a subdirectory).
        /// </summary>
        internal static bool IsFileInRootDirectory(string filePath, string rootPath)
        {
            var normalizedRoot = rootPath.TrimEnd('/');
            var relativePath = filePath;
            
            if (filePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = filePath.Substring(normalizedRoot.Length).TrimStart('/');
            }
            
            // If there's no '/' in the relative path, the file is directly in the root
            return !relativePath.Contains('/');
        }

        /// <summary>
        /// Checks if a path is a system path that should be excluded.
        /// </summary>
        internal static bool IsSystemPath(string filePath)
        {
            var systemDirs = new[] { "/sys/", "/proc/", "/dev/", "/.git/", "/vendor/", "/node_modules/", "/storage/framework/", "/bin/", "/obj/" };
            return systemDirs.Any(dir => filePath.Contains(dir, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Common binary file extensions that should be excluded when not including binary files.
        /// </summary>
        private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".bin", ".zip", ".tar", ".gz", ".7z", ".rar",
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg", ".webp",
            ".mp3", ".mp4", ".avi", ".mkv", ".wav", ".flac", ".ogg",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".pdb", ".cache", ".lock", ".pack", ".idx", ".so", ".dylib", ".a"
        };

        /// <summary>
        /// Checks if a file has a binary file extension.
        /// </summary>
        internal static bool IsBinaryFileExtension(string fileName)
        {
            var ext = Path.GetExtension(fileName);
            return !string.IsNullOrEmpty(ext) && BinaryExtensions.Contains(ext);
        }

        /// <summary>
        /// Gets .gitignore patterns from a container by reading the .gitignore file.
        /// </summary>
        private async Task<List<string>> GetGitignorePatternsAsync(string containerId, string searchPath, CancellationToken cancellationToken)
        {
            var patterns = new List<string>();
            
            try
            {
                var client = _dockerClient.Value;
                if (client == null)
                    return patterns;

                // Try to read .gitignore from the search path
                var gitignorePath = searchPath.TrimEnd('/') + "/.gitignore";
                var execParams = new ContainerExecCreateParameters
                {
                    AttachStdout = true,
                    AttachStderr = true,
                    Cmd = new[] { "cat", gitignorePath }
                };

                var exec = await client.Exec.ExecCreateContainerAsync(containerId, execParams, cancellationToken);
                using var stream = await client.Exec.StartAndAttachContainerExecAsync(exec.ID, false, cancellationToken);
                var (stdout, _) = await stream.ReadOutputToEndAsync(cancellationToken);
                var inspectResult = await client.Exec.InspectContainerExecAsync(exec.ID, cancellationToken);

                if (inspectResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(stdout))
                {
                    // Parse .gitignore patterns
                    foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = line.Trim();
                        // Skip comments and empty lines
                        if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#"))
                        {
                            patterns.Add(trimmed);
                        }
                    }
                }
            }
            catch
            {
                // If we can't read .gitignore, just return empty patterns
            }

            return patterns;
        }

        /// <summary>
        /// Checks if a file path matches any .gitignore patterns.
        /// </summary>
        internal static bool MatchesGitignorePatterns(string filePath, string rootPath, List<string> patterns)
        {
            if (patterns.Count == 0)
                return false;

            var normalizedRoot = rootPath.TrimEnd('/');
            var relativePath = filePath;
            
            if (filePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                relativePath = filePath.Substring(normalizedRoot.Length).TrimStart('/');
            }

            var fileName = Path.GetFileName(filePath);

            foreach (var pattern in patterns)
            {
                // Handle negation patterns (start with !)
                if (pattern.StartsWith("!"))
                    continue; // Skip negation for simplicity

                var cleanPattern = pattern.TrimStart('/');
                
                // Check if pattern matches file name directly
                if (MatchesGitignorePattern(fileName, cleanPattern))
                    return true;
                
                // Check if pattern matches any part of the path
                if (MatchesGitignorePattern(relativePath, cleanPattern))
                    return true;

                // Check directory patterns (ending with /)
                if (pattern.EndsWith("/"))
                {
                    var dirPattern = pattern.TrimEnd('/');
                    var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Any(p => MatchesGitignorePattern(p, dirPattern)))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Matches a string against a simple gitignore-style pattern.
        /// Supports * (any characters) and ? (single character).
        /// </summary>
        private static bool MatchesGitignorePattern(string text, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return false;

            // Convert gitignore pattern to regex
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*\\*", ".*")  // ** matches any path
                .Replace("\\*", "[^/]*")  // * matches any characters except /
                .Replace("\\?", ".")       // ? matches single character
                + "$";

            try
            {
                return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public async Task<IReadOnlyList<DockerContainerInfo>> GetContainersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await RunDockerAsync("ps --format \"{{json .}}\"", cancellationToken, throwOnError: false);
                if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
                {
                    return Array.Empty<DockerContainerInfo>();
                }

                return ParseContainerList(result.StandardOutput);
            }
            catch
            {
                return Array.Empty<DockerContainerInfo>();
            }
        }

        public async Task<DockerMirrorInfo> MirrorPathAsync(DockerContainerInfo container, string containerPath, bool includeSymbolicLinks = false, CancellationToken cancellationToken = default)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            if (string.IsNullOrWhiteSpace(containerPath))
                throw new ArgumentException("A container path must be provided.", nameof(containerPath));

            var normalizedPath = NormalizeContainerPath(containerPath);
            try
            {
                Directory.CreateDirectory(_mirrorRoot);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create docker mirror root at '{_mirrorRoot}': {ex.Message}", ex);
            }

            var mirrorDirectory = Path.Combine(_mirrorRoot, $"{container.ShortId}_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(mirrorDirectory);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create docker mirror directory '{mirrorDirectory}': {ex.Message}", ex);
            }

            try
            {
                if (includeSymbolicLinks)
                {
                    // Use docker cp when symlinks should be included (may fail on Windows without privileges)
                    var containerReference = $"{container.Id}:{normalizedPath}";
                    var arguments = $"cp \"{containerReference}\" \"{mirrorDirectory}\"";
                    await RunDockerAsync(arguments, cancellationToken);
                }
                else
                {
                    // Use docker exec with tar --dereference to follow symlinks and copy actual files
                    // This avoids the Windows privilege issue by copying target files instead of creating symlinks
                    var escapedPath = normalizedPath.Replace("\"", "\\\"").Replace("$", "\\$");
                    var arguments = $"exec -i \"{container.Id}\" tar -c --dereference -C \"{escapedPath}\" . | tar -x -C \"{mirrorDirectory}\"";
                    
                    // For Windows, we need to use a different approach since tar piping doesn't work well
                    // Instead, create a tar archive in the container, copy it out, extract it, then delete it
                    var tempTarPath = $"/tmp/grex_mirror_{Guid.NewGuid():N}.tar";
                    var escapedTarPath = tempTarPath.Replace("\"", "\\\"");
                    
                    try
                    {
                        // Create tar archive in container (with --dereference to follow symlinks)
                        var createTarArgs = $"exec -i \"{container.Id}\" tar -cf \"{escapedTarPath}\" --dereference -C \"{escapedPath}\" .";
                        await RunDockerAsync(createTarArgs, cancellationToken);
                        
                        // Copy tar file from container to host
                        var copyTarArgs = $"cp \"{container.Id}:{tempTarPath}\" \"{mirrorDirectory}\"";
                        await RunDockerAsync(copyTarArgs, cancellationToken);
                        
                            // Extract tar file on host
                            var localTarPath = Path.Combine(mirrorDirectory, Path.GetFileName(tempTarPath));
                            if (File.Exists(localTarPath))
                            {
                                // Extract using tar.exe (available on Windows 10+)
                                await ExtractTarFileAsync(localTarPath, mirrorDirectory, cancellationToken);
                                
                                // Clean up tar file
                                try
                                {
                                    File.Delete(localTarPath);
                                }
                                catch
                                {
                                    // Ignore cleanup errors
                                }
                            }
                        
                        // Clean up tar file in container
                        try
                        {
                            var rmTarArgs = $"exec -i \"{container.Id}\" rm -f \"{escapedTarPath}\"";
                            await RunDockerAsync(rmTarArgs, CancellationToken.None); // Use None to avoid cancellation issues
                        }
                        catch
                        {
                            // Ignore cleanup errors in container
                        }
                    }
                    catch
                    {
                        // Clean up tar file in container on error
                        try
                        {
                            var rmTarArgs = $"exec -i \"{container.Id}\" rm -f \"{escapedTarPath}\"";
                            await RunDockerAsync(rmTarArgs, CancellationToken.None);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                        throw;
                    }
                }
            }
            catch
            {
                CleanupMirrorDirectory(mirrorDirectory);
                throw;
            }

            var localSearchPath = ResolveLocalSearchRoot(mirrorDirectory, normalizedPath);

            return new DockerMirrorInfo
            {
                ContainerId = container.Id,
                ContainerName = container.Name,
                ContainerPath = normalizedPath,
                LocalMirrorPath = mirrorDirectory,
                LocalSearchPath = localSearchPath,
                CreatedUtc = DateTime.UtcNow
            };
        }

        public Task CleanupMirrorAsync(DockerMirrorInfo? mirrorInfo, CancellationToken cancellationToken = default)
        {
            if (mirrorInfo == null)
            {
                return Task.CompletedTask;
            }

            return CleanupMirrorAsync(mirrorInfo.LocalMirrorPath, cancellationToken);
        }

        public Task CleanupMirrorAsync(string? localPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(localPath) || !Directory.Exists(localPath))
            {
                return Task.CompletedTask;
            }

            return Task.Run(() =>
            {
                try
                {
                    Directory.Delete(localPath, true);
                }
                catch
                {
                    // Ignore cleanup failures
                }
            }, cancellationToken);
        }

        public Task<int> PruneExpiredMirrorsAsync(TimeSpan? retention = null, CancellationToken cancellationToken = default)
        {
            var ttl = retention ?? DefaultMirrorRetention;
            if (!Directory.Exists(_mirrorRoot))
            {
                return Task.FromResult(0);
            }

            var cutoff = DateTime.UtcNow - ttl;
            int removed = 0;

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(_mirrorRoot).ToList();
            }
            catch (Exception)
            {
                return Task.FromResult(0);
            }

            foreach (var directory in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var info = new DirectoryInfo(directory);
                    if (info.LastWriteTimeUtc < cutoff)
                    {
                        info.Delete(true);
                        removed++;
                    }
                }
                catch
                {
                    // Ignore stale cleanup failures
                }
            }

            return Task.FromResult(removed);
        }

        internal static string NormalizeContainerPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "/";
            }

            var normalized = path.Trim();
            normalized = normalized.Trim('"', '\'');
            normalized = normalized.Replace("\\", "/");

            while (normalized.Contains("//", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
            }

            if (!normalized.StartsWith("/", StringComparison.Ordinal))
            {
                normalized = "/" + normalized;
            }

            return normalized;
        }

        internal static string ResolveLocalSearchRoot(string mirrorDirectory, string normalizedContainerPath)
        {
            if (string.IsNullOrWhiteSpace(normalizedContainerPath))
            {
                return mirrorDirectory;
            }

            var trimmed = normalizedContainerPath.TrimEnd('/', '\\');
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return mirrorDirectory;
            }

            var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return mirrorDirectory;
            }

            var lastSegment = segments[^1];
            if (string.IsNullOrWhiteSpace(lastSegment))
            {
                return mirrorDirectory;
            }

            var candidate = Path.Combine(mirrorDirectory, lastSegment);
            return Directory.Exists(candidate) ? candidate : mirrorDirectory;
        }

        private static IReadOnlyList<DockerContainerInfo> ParseContainerList(string rawOutput)
        {
            var containers = new List<DockerContainerInfo>();
            using var reader = new StringReader(rawOutput);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    var id = root.TryGetProperty("ID", out var idProp) ? idProp.GetString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    var info = new DockerContainerInfo
                    {
                        Id = id,
                        Name = root.TryGetProperty("Names", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty,
                        Image = root.TryGetProperty("Image", out var imageProp) ? imageProp.GetString() ?? string.Empty : string.Empty,
                        State = root.TryGetProperty("State", out var stateProp) ? stateProp.GetString() ?? string.Empty : string.Empty,
                        Status = root.TryGetProperty("Status", out var statusProp) ? statusProp.GetString() ?? string.Empty : string.Empty
                    };

                    containers.Add(info);
                }
                catch (JsonException)
                {
                    // Ignore malformed lines
                }
            }

            return containers;
        }

        private async Task<DockerProcessResult> RunDockerAsync(string arguments, CancellationToken cancellationToken, bool throwOnError = true)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = DockerExecutable,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            DockerProcessResult result;
            try
            {
                result = await _processRunner.RunAsync(startInfo, cancellationToken);
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException("Docker CLI is not available. Ensure Docker Desktop is installed and added to PATH.", ex);
            }

            if (throwOnError && result.ExitCode != 0)
            {
                var error = string.IsNullOrWhiteSpace(result.StandardError)
                    ? $"Docker command failed with exit code {result.ExitCode}."
                    : result.StandardError.Trim();

                // Check if the error is related to symlink creation (Windows privilege issue)
                if (error.Contains("symlink", StringComparison.OrdinalIgnoreCase) && 
                    (error.Contains("privilege", StringComparison.OrdinalIgnoreCase) || 
                     error.Contains("required privilege", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new DockerSymlinkException(
                        $"Docker copy failed due to symbolic link creation: {error}. " +
                        "Windows requires special privileges to create symbolic links. " +
                        "Either run Grex as Administrator, or enable the 'Create symbolic links' user right for your account. " +
                        "Note: Some container paths (like node_modules) contain many symlinks and may not be searchable without these privileges.",
                        error);
                }

                throw new InvalidOperationException(error);
            }

            return result;
        }

        private static void CleanupMirrorDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
                // Ignore cleanup failures
            }
        }

        private async Task ExtractTarFileAsync(string tarPath, string extractTo, CancellationToken cancellationToken)
        {
            // Use tar.exe (built into Windows 10+)
            var startInfo = new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"-xf \"{tarPath}\" -C \"{extractTo}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                var result = await _processRunner.RunAsync(startInfo, cancellationToken);
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Failed to extract tar file: {result.StandardError}");
                }
            }
            catch (Win32Exception)
            {
                // tar.exe not available, try alternative method
                throw new InvalidOperationException(
                    "tar.exe is not available. Windows 10 or later is required for Docker search without symbolic links. " +
                    "Alternatively, enable 'Include symbolic links' to use docker cp directly.");
            }
        }
    }
}


