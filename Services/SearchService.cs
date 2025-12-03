using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Grex.Models;

namespace Grex.Services
{
    public class SearchService : ISearchService
    {
        // Maximum degree of parallelism for file searching
        private const int MaxParallelism = 8;
        
        // File extensions to skip (binary files)
        // Note: Common text files like .env, .gitignore, .gitattributes, etc. are NOT in this list
        // and will be treated as text files
        private static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".obj", ".bin", ".zip", ".tar", ".gz", ".7z", ".rar",
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg", ".webp",
            ".mp3", ".mp4", ".avi", ".mkv", ".wav", ".flac", ".ogg",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".pdb", ".cache", ".lock", ".pack", ".idx", ".rtf"
        };

        // Binary file extensions that CAN be searched programmatically without third-party libraries
        // These are binary files that contain searchable text content
        private static readonly HashSet<string> SearchableBinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            // ZIP-based Office formats (Office Open XML and OpenDocument)
            ".docx", ".xlsx", ".pptx",  // Office Open XML
            ".odt", ".ods", ".odp",     // OpenDocument formats
            ".zip",                     // ZIP archives (can search file names and metadata)
            // Document formats
            ".pdf",                     // PDF files (can extract text)
            ".rtf"                      // Rich Text Format (text-based)
        };

        private readonly GitIgnoreService _gitIgnoreService;
        private readonly IWindowsSearchIntegration _windowsSearchIntegration;
        private readonly IEncodingDetectionService _encodingDetectionService;
        private readonly ILocalizationService _localizationService = LocalizationService.Instance;

        public SearchService(IWindowsSearchIntegration? windowsSearchIntegration = null, GitIgnoreService? gitIgnoreService = null, IEncodingDetectionService? encodingDetectionService = null)
        {
            _gitIgnoreService = gitIgnoreService ?? new GitIgnoreService();
            _windowsSearchIntegration = windowsSearchIntegration ?? new WindowsSearchIntegration();
            _encodingDetectionService = encodingDetectionService ?? new EncodingDetectionService();
        }

        public async Task<List<SearchResult>> SearchAsync(
            string path,
            string searchTerm,
            bool isRegex,
            bool respectGitignore = false,
            bool searchCaseSensitive = false,
            bool includeSystemFiles = false,
            bool includeSubfolders = true,
            bool includeHiddenItems = false,
            bool includeBinaryFiles = false,
            bool includeSymbolicLinks = false,
            Models.SizeLimitType sizeLimitType = Models.SizeLimitType.NoLimit,
            long? sizeLimitKB = null,
            Models.SizeUnit sizeUnit = Models.SizeUnit.KB,
            string matchFileNames = "",
            string excludeDirs = "",
            bool preferWindowsSearchIndex = false,
            Models.StringComparisonMode stringComparisonMode = Models.StringComparisonMode.Ordinal,
            Models.UnicodeNormalizationMode unicodeNormalizationMode = Models.UnicodeNormalizationMode.None,
            bool diacriticSensitive = true,
            string? culture = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(searchTerm))
                return new List<SearchResult>();

            cancellationToken.ThrowIfCancellationRequested();

            if (IsWslPath(path))
            {
                return await SearchWslPathAsync(path, searchTerm, isRegex, respectGitignore, searchCaseSensitive, includeSystemFiles, includeSubfolders, includeHiddenItems, includeBinaryFiles, includeSymbolicLinks, sizeLimitType, sizeLimitKB, sizeUnit, matchFileNames, excludeDirs, stringComparisonMode, unicodeNormalizationMode, diacriticSensitive, culture, cancellationToken);
            }
            else
            {
                return await SearchWindowsPathAsync(path, searchTerm, isRegex, respectGitignore, searchCaseSensitive, includeSystemFiles, includeSubfolders, includeHiddenItems, includeBinaryFiles, includeSymbolicLinks, sizeLimitType, sizeLimitKB, sizeUnit, matchFileNames, excludeDirs, preferWindowsSearchIndex, stringComparisonMode, unicodeNormalizationMode, diacriticSensitive, culture, cancellationToken);
            }
        }

        public bool IsWslPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
                
            if (path.StartsWith("\\\\wsl$", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("\\\\wsl.localhost", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/mnt/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("\\mnt\\", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (path.Length > 0 && path[0] == '/')
            {
                return path.Length < 2 || path[1] != ':';
            }

            return false;
        }

        private async Task<List<SearchResult>> SearchWindowsPathAsync(
            string path,
            string searchTerm,
            bool isRegex,
            bool respectGitignore,
            bool searchCaseSensitive,
            bool includeSystemFiles,
            bool includeSubfolders,
            bool includeHiddenItems,
            bool includeBinaryFiles,
            bool includeSymbolicLinks,
            Models.SizeLimitType sizeLimitType,
            long? sizeLimitKB,
            Models.SizeUnit sizeUnit,
            string matchFileNames,
            string excludeDirs,
            bool preferWindowsSearchIndex,
            Models.StringComparisonMode stringComparisonMode,
            Models.UnicodeNormalizationMode unicodeNormalizationMode,
            bool diacriticSensitive,
            string? culture,
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Directory not found: {path}");
            }
            
            cancellationToken.ThrowIfCancellationRequested();

            var results = new ConcurrentBag<SearchResult>();
            
            // Pre-compile regex once if needed
            Regex? compiledRegex = null;
            if (isRegex)
            {
                try
                {
                    var regexOptions = RegexOptions.Compiled;
                    if (!searchCaseSensitive)
                    {
                        regexOptions |= RegexOptions.IgnoreCase;
                    }
                    compiledRegex = new Regex(searchTerm, regexOptions);
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"Invalid regex pattern: {ex.Message}");
                }
            }

            // Configure enumeration options
            var enumOptions = new EnumerationOptions
            {
                RecurseSubdirectories = includeSubfolders,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.None
            };

            if (!includeSystemFiles)
            {
                enumOptions.AttributesToSkip |= FileAttributes.System;
            }

            if (!includeHiddenItems)
            {
                enumOptions.AttributesToSkip |= FileAttributes.Hidden;
            }

            IEnumerable<string> files;
            var usingWindowsIndex = false;

            if (ShouldUseWindowsSearchIndex(preferWindowsSearchIndex, isRegex))
            {
                try
                {
                    var indexResult = await _windowsSearchIntegration.QueryIndexedFilesAsync(path, searchTerm, includeSubfolders);
                    if (indexResult.ScopeAvailable)
                    {
                        files = indexResult.Paths;
                        usingWindowsIndex = true;
                        Debug.WriteLine($"Using Windows Search index for '{path}' (candidates: {indexResult.Paths.Count})");
                    }
                    else
                    {
                        files = Directory.EnumerateFiles(path, "*", enumOptions);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Windows Search query failed for '{path}': {ex.Message}");
                    files = Directory.EnumerateFiles(path, "*", enumOptions);
                }
            }
            else
            {
                files = Directory.EnumerateFiles(path, "*", enumOptions);
            }

            if (usingWindowsIndex && !includeSubfolders)
            {
                try
                {
                    var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
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
                    });
                }
                catch
                {
                    // If we can't normalize the root path, keep the current file set
                }
            }

            // Filter Unix-style hidden files (starting with .) when includeHiddenItems is false
            // This handles files like .env, .gitignore that don't have Windows Hidden attribute
            // but are considered hidden on Unix/WSL systems (e.g., when accessing WSL via mounted drive)
            if (!includeHiddenItems)
            {
                files = files.Where(f =>
                {
                    // Check if filename starts with .
                    var fileName = Path.GetFileName(f);
                    if (!string.IsNullOrEmpty(fileName) && fileName.StartsWith("."))
                        return false;
                    
                    // Check if any parent directory starts with .
                    var directory = Path.GetDirectoryName(f);
                    if (directory != null)
                    {
                        var parts = directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        if (parts.Any(p => !string.IsNullOrEmpty(p) && p.StartsWith(".")))
                            return false;
                    }
                    return true;
                });
            }

            // Filter .git, vendor, node_modules, and storage/framework folders unless system files are included
            if (!includeSystemFiles)
            {
                files = files.Where(f =>
                {
                    // Check if file is inside a .git, vendor, node_modules, or storage/framework folder
                    var directory = Path.GetDirectoryName(f);
                    if (directory != null)
                    {
                        var parts = directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        if (parts.Contains(".git", StringComparer.OrdinalIgnoreCase) ||
                            parts.Contains("vendor", StringComparer.OrdinalIgnoreCase) ||
                            parts.Contains("node_modules", StringComparer.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                        // Check for storage/framework path
                        for (int i = 0; i < parts.Length - 1; i++)
                        {
                            if (parts[i].Equals("storage", StringComparison.OrdinalIgnoreCase) &&
                                parts[i + 1].Equals("framework", StringComparison.OrdinalIgnoreCase))
                            {
                                return false;
                            }
                        }
                    }
                    return true;
                });
            }

            // Filter binary files if not included
            // This filter is applied BEFORE size limit check to ensure binary files are excluded
            // regardless of their size (unless includeBinaryFiles is true)
            if (!includeBinaryFiles)
            {
                files = files.Where(f => !BinaryExtensions.Contains(Path.GetExtension(f)));
            }
            else
            {
                // When includeBinaryFiles is true, only include files that are either:
                // 1. Not in BinaryExtensions (normal text files)
                // 2. In SearchableBinaryExtensions (binary files we can search)
                files = files.Where(f =>
                {
                    var ext = Path.GetExtension(f);
                    // Include if it's not a binary file (normal text file)
                    if (!BinaryExtensions.Contains(ext))
                        return true;
                    // Include if it's a searchable binary file
                    return SearchableBinaryExtensions.Contains(ext);
                });
            }

            // Filter symbolic links if not included
            // This filter is applied BEFORE size limit check to ensure symbolic links are excluded
            // regardless of their size (unless includeSymbolicLinks is true)
            if (!includeSymbolicLinks)
            {
                files = files.Where(f =>
                {
                    try
                    {
                        var fileInfo = new FileInfo(f);
                        return (fileInfo.Attributes & FileAttributes.ReparsePoint) == 0;
                    }
                    catch
                    {
                        return true; // Include if we can't determine
                    }
                });
            }

            // Filter by .gitignore if enabled
            if (respectGitignore)
            {
                files = files.Where(f => !_gitIgnoreService.ShouldIgnoreFile(f, path));
            }

            // Filter by filename pattern if specified
            if (!string.IsNullOrWhiteSpace(matchFileNames))
            {
                files = files.Where(f =>
                {
                    var fileName = Path.GetFileName(f);
                    return MatchesFileNamePattern(fileName, matchFileNames);
                });
            }

            // Filter by excluded directories if specified
            if (!string.IsNullOrWhiteSpace(excludeDirs))
            {
                files = files.Where(f => !ShouldExcludeDirectory(f, path, excludeDirs));
            }

            // Filter by file size if size limit is specified
            // This is applied AFTER binary files and symbolic links filters to ensure
            // those filters take precedence over size limits
            if (sizeLimitType != Models.SizeLimitType.NoLimit && sizeLimitKB.HasValue)
            {
                // Convert size limit to bytes based on unit
                long sizeLimitBytes = sizeLimitKB.Value;
                switch (sizeUnit)
                {
                    case Models.SizeUnit.MB:
                        sizeLimitBytes *= 1024 * 1024;
                        break;
                    case Models.SizeUnit.GB:
                        sizeLimitBytes *= 1024 * 1024 * 1024;
                        break;
                    default: // KB
                        sizeLimitBytes *= 1024;
                        break;
                }
                
                // Calculate tolerance based on size unit
                // KB: 10KB tolerance, MB: 1MB tolerance, GB: 25MB tolerance
                long tolerance = sizeUnit switch
                {
                    Models.SizeUnit.KB => 10 * 1024,  // 10 KB
                    Models.SizeUnit.MB => 1 * 1024 * 1024,  // 1 MB
                    Models.SizeUnit.GB => 25 * 1024 * 1024,  // 25 MB
                    _ => 10 * 1024  // Default to 10 KB
                };
                
                files = files.Where(f =>
                {
                    try
                    {
                        var fileInfo = new FileInfo(f);
                        if (!fileInfo.Exists)
                            return false;

                        long fileSize = fileInfo.Length;
                        return sizeLimitType switch
                        {
                            // Apply tolerance to all operations: Less Than allows up to (limit + tolerance),
                            // Equal To allows ±tolerance, Greater Than allows down to (limit - tolerance)
                            Models.SizeLimitType.LessThan => fileSize < (sizeLimitBytes + tolerance),
                            Models.SizeLimitType.EqualTo => Math.Abs(fileSize - sizeLimitBytes) <= tolerance,
                            Models.SizeLimitType.GreaterThan => fileSize > (sizeLimitBytes - tolerance),
                            _ => true
                        };
                    }
                    catch
                    {
                        return false; // Exclude files we can't get size for
                    }
                });
            }

            // Process files in parallel
            var parallelOptions = new ParallelOptions 
            { 
                MaxDegreeOfParallelism = MaxParallelism,
                CancellationToken = cancellationToken
            };
            
            await Parallel.ForEachAsync(files, parallelOptions, async (file, ct) =>
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
                    var fileResults = await SearchFileOptimizedAsync(file, searchTerm, isRegex, compiledRegex, searchCaseSensitive, path, stringComparisonMode, unicodeNormalizationMode, diacriticSensitive, culture);
                    foreach (var result in fileResults)
                    {
                        ct.ThrowIfCancellationRequested();
                        results.Add(result);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; // Propagate cancellation
                }
                catch
                {
                    // Skip files that can't be read
                }
            });

            cancellationToken.ThrowIfCancellationRequested();
            return results.OrderBy(r => r.FileName).ThenBy(r => r.LineNumber).ToList();
        }

        private async Task<List<SearchResult>> SearchFileOptimizedAsync(string filePath, string searchTerm, bool isRegex, Regex? compiledRegex, bool searchCaseSensitive, string rootPath, Models.StringComparisonMode stringComparisonMode, Models.UnicodeNormalizationMode unicodeNormalizationMode, bool diacriticSensitive, string? culture)
        {
            var results = new List<SearchResult>();
            var fileName = Path.GetFileName(filePath);
            var extension = Path.GetExtension(filePath);
            
            // Check if this is a searchable binary file
            if (SearchableBinaryExtensions.Contains(extension))
            {
                return await SearchBinaryFileAsync(filePath, searchTerm, isRegex, compiledRegex, searchCaseSensitive, rootPath, extension, stringComparisonMode, unicodeNormalizationMode, diacriticSensitive, culture);
            }
            
            try
            {
                // Use the new encoding detection service to detect file encoding
                var encodingResult = _encodingDetectionService.DetectFileEncoding(filePath);
                using var reader = new StreamReader(filePath, encodingResult.Encoding, detectEncodingFromByteOrderMarks: false);
                int lineNumber = 0;
                
                while (await reader.ReadLineAsync() is { } line)
                {
                    lineNumber++;
                    bool matches;

                    // Use original line for searching
                    if (isRegex && compiledRegex != null)
                    {
                        try
                        {
                            matches = compiledRegex.IsMatch(line);
                        }
                        catch
                        {
                            // Skip lines that cause regex errors (e.g., invalid encoding)
                            continue;
                        }
                    }
                    else
                    {
                        matches = ContainsStringWithCultureAwareComparison(line, searchTerm, searchCaseSensitive, stringComparisonMode, unicodeNormalizationMode, diacriticSensitive, culture);
                    }

                    if (matches)
                    {
                        var columnNumber = CalculateColumnNumber(line, searchTerm, isRegex, compiledRegex, searchCaseSensitive);
                        var matchCount = CountMatchesOnLine(line, searchTerm, isRegex, compiledRegex, searchCaseSensitive);
                        
                        // Sanitize only for display to prevent crashes from invalid characters
                        var sanitizedLine = SanitizeStringForDisplay(line);
                        var displayContent = sanitizedLine.Length > 500 ? sanitizedLine[..500] + "..." : sanitizedLine;

                        results.Add(new SearchResult
                        {
                            FileName = fileName,
                            LineNumber = lineNumber,
                            ColumnNumber = columnNumber,
                            LineContent = displayContent,
                            FullPath = filePath,
                            RelativePath = GetRelativePath(rootPath, filePath),
                            MatchCount = matchCount
                        });
                    }
                }
            }
            catch (System.Text.DecoderFallbackException)
            {
                // Skip files with encoding issues (likely binary files)
                System.Diagnostics.Debug.WriteLine($"Encoding error reading file {filePath}, skipping");
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                // Skip files with encoding detection issues
                System.Diagnostics.Debug.WriteLine($"Encoding detection error reading file {filePath}: {ex.Message}, skipping");
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - skip files that can't be read
                // This might happen for binary files or files with permission issues
                System.Diagnostics.Debug.WriteLine($"Error reading file {filePath}: {ex.Message}");
            }

            return results;
        }

        private async Task<List<SearchResult>> SearchBinaryFileAsync(string filePath, string searchTerm, bool isRegex, Regex? compiledRegex, bool searchCaseSensitive, string rootPath, string extension, Models.StringComparisonMode stringComparisonMode, Models.UnicodeNormalizationMode unicodeNormalizationMode, bool diacriticSensitive, string? culture)
        {
            var results = new List<SearchResult>();
            var fileName = Path.GetFileName(filePath);
            
            try
            {
                // Handle different binary file types
                if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".odt", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".ods", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".odp", StringComparison.OrdinalIgnoreCase))
                {
                    // ZIP-based formats (Office Open XML and OpenDocument)
                    results = await SearchZipBasedFileAsync(filePath, searchTerm, isRegex, compiledRegex, searchCaseSensitive, rootPath, stringComparisonMode, unicodeNormalizationMode, diacriticSensitive, culture);
                }
                else if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    // PDF files - extract text and search
                    results = await SearchPdfFileAsync(filePath, searchTerm, isRegex, compiledRegex, searchCaseSensitive, rootPath, stringComparisonMode, unicodeNormalizationMode, diacriticSensitive, culture);
                }
                else if (extension.Equals(".rtf", StringComparison.OrdinalIgnoreCase))
                {
                    // RTF files - text-based format
                    results = await SearchRtfFileAsync(filePath, searchTerm, isRegex, compiledRegex, searchCaseSensitive, rootPath, stringComparisonMode, unicodeNormalizationMode, diacriticSensitive, culture);
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - skip files that can't be read
                System.Diagnostics.Debug.WriteLine($"Error reading binary file {filePath}: {ex.Message}");
            }

            return results;
        }

        private async Task<List<SearchResult>> SearchZipBasedFileAsync(string filePath, string searchTerm, bool isRegex, Regex? compiledRegex, bool searchCaseSensitive, string rootPath, Models.StringComparisonMode stringComparisonMode, Models.UnicodeNormalizationMode unicodeNormalizationMode, bool diacriticSensitive, string? culture)
        {
            var results = new List<SearchResult>();
            var fileName = Path.GetFileName(filePath);
            
            try
            {
                using var archive = ZipFile.OpenRead(filePath);
                int fileIndex = 0;
                
                foreach (var entry in archive.Entries)
                {
                    // Only search XML/text files within the archive
                    if (entry.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                        entry.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                        entry.Name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                    {
                        fileIndex++;
                        try
                        {
                            using var entryStream = entry.Open();
                            using var reader = new StreamReader(entryStream, Encoding.UTF8);
                            
                            int lineNumber = 0;
                            while (await reader.ReadLineAsync() is { } line)
                            {
                                lineNumber++;
                                bool matches;

                                if (isRegex && compiledRegex != null)
                                {
                                    try
                                    {
                                        matches = compiledRegex.IsMatch(line);
                                    }
                                    catch
                                    {
                                        continue;
                                    }
                                }
                                else
                                {
                                    matches = ContainsStringWithCultureAwareComparison(line, searchTerm, searchCaseSensitive, stringComparisonMode, unicodeNormalizationMode, diacriticSensitive, culture);
                                }

                                if (matches)
                                {
                                    var columnNumber = CalculateColumnNumber(line, searchTerm, isRegex, compiledRegex, searchCaseSensitive);
                                    var matchCount = CountMatchesOnLine(line, searchTerm, isRegex, compiledRegex, searchCaseSensitive);
                                    var sanitizedLine = SanitizeStringForDisplay(line);
                                    var displayContent = sanitizedLine.Length > 500 ? sanitizedLine[..500] + "..." : sanitizedLine;

                                    results.Add(new SearchResult
                                    {
                                        FileName = $"{fileName} [{entry.Name}]",
                                        LineNumber = lineNumber,
                                        ColumnNumber = columnNumber,
                                        LineContent = displayContent,
                                        FullPath = filePath,
                                        RelativePath = $"{GetRelativePath(rootPath, filePath)} [{entry.Name}]",
                                        MatchCount = matchCount
                                    });
                                }
                            }
                        }
                        catch
                        {
                            // Skip entries that can't be read
                            continue;
                        }
                    }
                }
            }
            catch
            {
                // If ZIP file can't be opened, skip it
            }

            return results;
        }

        private async Task<List<SearchResult>> SearchPdfFileAsync(string filePath, string searchTerm, bool isRegex, Regex? compiledRegex, bool searchCaseSensitive, string rootPath, Models.StringComparisonMode stringComparisonMode, Models.UnicodeNormalizationMode unicodeNormalizationMode, bool diacriticSensitive, string? culture)
        {
            var results = new List<SearchResult>();
            var fileName = Path.GetFileName(filePath);
            
            try
            {
                // Read PDF as binary and extract text streams
                // PDFs contain text in streams between "stream" and "endstream" markers
                var pdfBytes = await File.ReadAllBytesAsync(filePath);
                var pdfText = Encoding.UTF8.GetString(pdfBytes);
                
                // Extract text from PDF streams (simplified approach)
                // Look for text between stream markers or in text objects
                var textMatches = new List<string>();
                var streamPattern = new Regex(@"stream\s*(.*?)\s*endstream", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                var streamMatches = streamPattern.Matches(pdfText);
                
                foreach (Match match in streamMatches)
                {
                    var streamContent = match.Groups[1].Value;
                    // Try to extract readable text (skip binary data)
                    var textBytes = Encoding.UTF8.GetBytes(streamContent);
                    var text = Encoding.UTF8.GetString(textBytes);
                    // Filter out mostly binary content
                    if (text.Any(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c)))
                    {
                        textMatches.Add(text);
                    }
                }
                
                // Also search the raw PDF text for text objects
                var textObjectPattern = new Regex(@"/Type\s*/Text[^>]*>([^<]+)", RegexOptions.IgnoreCase);
                var textObjectMatches = textObjectPattern.Matches(pdfText);
                foreach (Match match in textObjectMatches)
                {
                    var text = match.Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        textMatches.Add(text);
                    }
                }
                
                // Search through extracted text
                int lineNumber = 0;
                foreach (var text in textMatches)
                {
                    var lines = text.Split('\n', '\r');
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;
                            
                        lineNumber++;
                        bool lineMatches;

                        if (isRegex && compiledRegex != null)
                        {
                            try
                            {
                                lineMatches = compiledRegex.IsMatch(line);
                            }
                            catch
                            {
                                continue;
                            }
                        }
                        else
                        {
                            lineMatches = ContainsStringWithCultureAwareComparison(line, searchTerm, searchCaseSensitive, stringComparisonMode, unicodeNormalizationMode, diacriticSensitive, culture);
                        }

                        if (lineMatches)
                        {
                            var columnNumber = CalculateColumnNumber(line, searchTerm, isRegex, compiledRegex, searchCaseSensitive);
                            var matchCount = CountMatchesOnLine(line, searchTerm, isRegex, compiledRegex, searchCaseSensitive);
                            var sanitizedLine = SanitizeStringForDisplay(line);
                            var displayContent = sanitizedLine.Length > 500 ? sanitizedLine[..500] + "..." : sanitizedLine;

                            results.Add(new SearchResult
                            {
                                FileName = fileName,
                                LineNumber = lineNumber,
                                ColumnNumber = columnNumber,
                                LineContent = displayContent,
                                FullPath = filePath,
                                RelativePath = GetRelativePath(rootPath, filePath),
                                MatchCount = matchCount
                            });
                        }
                    }
                }
            }
            catch
            {
                // If PDF can't be read, skip it
            }

            return results;
        }

        private async Task<List<SearchResult>> SearchRtfFileAsync(string filePath, string searchTerm, bool isRegex, Regex? compiledRegex, bool searchCaseSensitive, string rootPath, Models.StringComparisonMode stringComparisonMode, Models.UnicodeNormalizationMode unicodeNormalizationMode, bool diacriticSensitive, string? culture)
        {
            var results = new List<SearchResult>();
            var fileName = Path.GetFileName(filePath);
            
            try
            {
                // RTF is text-based, but contains control codes
                // Read as text and search, stripping RTF control codes for display
                using var reader = new StreamReader(filePath, Encoding.UTF8);
                int lineNumber = 0;
                
                while (await reader.ReadLineAsync() is { } line)
                {
                    lineNumber++;
                    
                    // Extract readable text from RTF (remove control codes)
                    var readableText = ExtractRtfText(line);
                    
                    bool matches;
                    if (isRegex && compiledRegex != null)
                    {
                        try
                        {
                            matches = compiledRegex.IsMatch(readableText);
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    else
                    {
                        matches = ContainsStringWithCultureAwareComparison(readableText, searchTerm, searchCaseSensitive, stringComparisonMode, unicodeNormalizationMode, diacriticSensitive, culture);
                    }

                    if (matches)
                    {
                        var columnNumber = CalculateColumnNumber(readableText, searchTerm, isRegex, compiledRegex, searchCaseSensitive);
                        var matchCount = CountMatchesOnLine(readableText, searchTerm, isRegex, compiledRegex, searchCaseSensitive);
                        var sanitizedLine = SanitizeStringForDisplay(readableText);
                        var displayContent = sanitizedLine.Length > 500 ? sanitizedLine[..500] + "..." : sanitizedLine;

                        results.Add(new SearchResult
                        {
                            FileName = fileName,
                            LineNumber = lineNumber,
                            ColumnNumber = columnNumber,
                            LineContent = displayContent,
                            FullPath = filePath,
                            RelativePath = GetRelativePath(rootPath, filePath),
                            MatchCount = matchCount
                        });
                    }
                }
            }
            catch
            {
                // If RTF can't be read, skip it
            }

            return results;
        }

        private static string ExtractRtfText(string rtfLine)
        {
            // Simple RTF text extraction - remove control codes
            // RTF format: {\rtf1\ansi text content}
            var result = new StringBuilder();
            bool inControl = false;
            int braceDepth = 0;
            bool skipControlWord = false;
            
            for (int i = 0; i < rtfLine.Length; i++)
            {
                char c = rtfLine[i];
                
                if (c == '{')
                {
                    braceDepth++;
                    continue;
                }
                if (c == '}')
                {
                    braceDepth--;
                    continue;
                }
                if (c == '\\')
                {
                    inControl = true;
                    skipControlWord = true;
                    continue;
                }
                if (inControl)
                {
                    // Skip control word characters (letters and digits)
                    if (char.IsLetterOrDigit(c))
                    {
                        continue;
                    }
                    // Control word ended, reset
                    inControl = false;
                    skipControlWord = false;
                    // Skip space after control word
                    if (c == ' ')
                        continue;
                }
                
                // Extract text that's not in control sequences
                // We're inside the main RTF group (braceDepth > 0) and not in a control sequence
                if (!inControl && !skipControlWord && braceDepth > 0 && (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || char.IsPunctuation(c)))
                {
                    result.Append(c);
                }
            }
            
            return result.ToString().Trim();
        }

        private async Task<List<SearchResult>> SearchWslPathAsync(
            string path,
            string searchTerm,
            bool isRegex,
            bool respectGitignore,
            bool searchCaseSensitive,
            bool includeSystemFiles,
            bool includeSubfolders,
            bool includeHiddenItems,
            bool includeBinaryFiles,
            bool includeSymbolicLinks,
            Models.SizeLimitType sizeLimitType,
            long? sizeLimitKB,
            Models.SizeUnit sizeUnit,
            string matchFileNames,
            string excludeDirs,
            Models.StringComparisonMode stringComparisonMode,
            Models.UnicodeNormalizationMode unicodeNormalizationMode,
            bool diacriticSensitive,
            string? culture,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            string wslPath = ConvertToWslPath(path);
            string? distro = ExtractWslDistribution(path);
            
            // Build the WSL command with optional distribution specifier
            var wslCommand = BuildWslGrepCommand(wslPath, searchTerm, isRegex, respectGitignore, searchCaseSensitive, includeSystemFiles, includeSubfolders, includeHiddenItems, includeBinaryFiles, includeSymbolicLinks, sizeLimitType, sizeLimitKB, sizeUnit, matchFileNames, excludeDirs, stringComparisonMode, unicodeNormalizationMode, diacriticSensitive, culture);
            var wslArgs = !string.IsNullOrEmpty(distro) ? $"-d {distro} {wslCommand}" : wslCommand;

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = wslArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            
            // Register cancellation callback to kill the process
            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Ignore errors when killing process
                }
            });
            
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);
            
            cancellationToken.ThrowIfCancellationRequested();

            if (process.ExitCode != 0 && process.ExitCode != 1)
            {
                var error = await process.StandardError.ReadToEndAsync();
                var errorMessage = $"WSL grep error: {error}";
                // Log and show notification for WSL errors
                System.Diagnostics.Debug.WriteLine(errorMessage);
                NotificationService.Instance.ShowError(
                    GetString("WslSearchErrorTitle"),
                    GetString("WslSearchErrorMessage", error.Trim()));
                throw new Exception(errorMessage);
            }

            var results = ParseGrepOutput(output, wslPath, searchTerm, isRegex, searchCaseSensitive, stringComparisonMode, unicodeNormalizationMode, diacriticSensitive, culture);
            
            // Apply filename and directory filters (post-processing for WSL)
            if (!string.IsNullOrWhiteSpace(matchFileNames))
            {
                results = results.Where(r => MatchesFileNamePattern(r.FileName, matchFileNames)).ToList();
            }
            
            if (!string.IsNullOrWhiteSpace(excludeDirs))
            {
                // For WSL paths, we need to convert back to check directories
                results = results.Where(r =>
                {
                    // Convert WSL path back to Windows path for directory checking
                    var fullPath = r.FullPath;
                    // Use the relative path to check directories
                    var relativePath = r.RelativePath;
                    if (string.IsNullOrEmpty(relativePath))
                        return true;
                    
                    // Extract directory from relative path
                    var dirPath = Path.GetDirectoryName(relativePath);
                    if (string.IsNullOrEmpty(dirPath))
                        return true;
                    
                    // Check if any directory in the path should be excluded
                    var parts = dirPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                    var dirNames = excludeDirs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    
                    // Check for regex pattern
                    bool isRegex = excludeDirs.StartsWith("^") || excludeDirs.Contains("(") || excludeDirs.Contains("[") || excludeDirs.Contains("$");
                    if (isRegex)
                    {
                        try
                        {
                            var regex = new Regex(excludeDirs, RegexOptions.IgnoreCase);
                            foreach (var part in parts)
                            {
                                if (regex.IsMatch(part))
                                    return false;
                            }
                            if (regex.IsMatch(dirPath))
                                return false;
                        }
                        catch
                        {
                            // Fall through to comma-separated check
                        }
                    }
                    
                    // Check comma-separated directory names
                    foreach (var dirName in dirNames)
                    {
                        if (parts.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                            return false;
                    }
                    
                    return true;
                }).ToList();
            }
            
            return results;
        }

        private string ConvertToWslPath(string path)
        {
            if (path.StartsWith("\\\\wsl$", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("\\\\wsl.localhost", StringComparison.OrdinalIgnoreCase))
            {
                var parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 2)
                {
                    return "/" + string.Join("/", parts.Skip(2));
                }
            }
            else if (path.StartsWith("/mnt/", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
            else if (path.Length > 2 && path[1] == ':')
            {
                var drive = path[0].ToString().ToLower();
                var rest = path.Substring(3).Replace('\\', '/');
                return $"/mnt/{drive}/{rest}";
            }

            return path;
        }

        /// <summary>
        /// Extracts the WSL distribution name from a \\wsl.localhost\{DISTRO}\... or \\wsl$\{DISTRO}\... path.
        /// Returns null if the path is not a WSL UNC path.
        /// </summary>
        private string? ExtractWslDistribution(string path)
        {
            if (path.StartsWith("\\\\wsl$", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("\\\\wsl.localhost", StringComparison.OrdinalIgnoreCase))
            {
                var parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    // parts[0] is "wsl$" or "wsl.localhost", parts[1] is the distribution name
                    return parts[1];
                }
            }
            return null;
        }

        private string BuildWslGrepCommand(
            string path,
            string searchTerm,
            bool isRegex,
            bool respectGitignore,
            bool searchCaseSensitive,
            bool includeSystemFiles,
            bool includeSubfolders,
            bool includeHiddenItems,
            bool includeBinaryFiles,
            bool includeSymbolicLinks,
            Models.SizeLimitType sizeLimitType,
            long? sizeLimitKB,
            Models.SizeUnit sizeUnit,
            string matchFileNames,
            string excludeDirs,
            Models.StringComparisonMode stringComparisonMode,
            Models.UnicodeNormalizationMode unicodeNormalizationMode,
            bool diacriticSensitive,
            string? culture)
        {
            var escapedPath = path.Replace("'", "'\"'\"'");
            var escapedTerm = searchTerm.Replace("'", "'\"'\"'");

            // Build find command predicates (each entry is a full expression)
            var findPredicates = new List<string>
            {
                "-type f"
            };
            
            // If not including subfolders, only search files directly in the root directory
            if (!includeSubfolders)
            {
                // Exclude files that are located in any nested directory
                // Example pattern: ! -path '/path/to/root/*/*'
                findPredicates.Add($"! -path '{escapedPath}/*/*'");
            }
            
            if (!includeHiddenItems)
            {
                // Exclude hidden files (names beginning with ".")
                findPredicates.Add("! -name '.*'");
            }
            
            // Filter binary files if not included
            // This filter is applied BEFORE size limit check to ensure binary files are excluded
            // regardless of their size (unless includeBinaryFiles is true)
            if (!includeBinaryFiles)
            {
                var binaryExts = new[]
                {
                    "exe", "dll", "bin", "zip", "tar", "gz", "7z", "rar",
                    "png", "jpg", "jpeg", "gif", "bmp", "ico", "svg", "webp",
                    "mp3", "mp4", "avi", "mkv", "wav", "flac", "ogg",
                    "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx",
                    "pdb", "cache", "lock", "pack", "idx"
                };
                
                foreach (var ext in binaryExts)
                {
                    findPredicates.Add($"! -name '*.{ext}'");
                }
            }
            else
            {
                // When includeBinaryFiles is true, exclude non-searchable binary files
                // Only include searchable binary files (.docx, .xlsx, .pptx, .odt, .ods, .odp, .zip, .pdf, .rtf)
                var nonSearchableBinaryExts = new[]
                {
                    "exe", "dll", "bin", "tar", "gz", "7z", "rar",
                    "png", "jpg", "jpeg", "gif", "bmp", "ico", "svg", "webp",
                    "mp3", "mp4", "avi", "mkv", "wav", "flac", "ogg",
                    "doc", "xls", "ppt",  // Old Office formats (OLE) - cannot search without libraries
                    "pdb", "cache", "lock", "pack", "idx"
                };
                
                foreach (var ext in nonSearchableBinaryExts)
                {
                    findPredicates.Add($"! -name '*.{ext}'");
                }
            }
            
            // Filter symbolic links if not included
            // This filter is applied BEFORE size limit check to ensure symbolic links are excluded
            // regardless of their size (unless includeSymbolicLinks is true)
            if (!includeSymbolicLinks)
            {
                findPredicates.Add("! -type l");
            }
            
            if (!includeSystemFiles)
            {
                // Exclude common system directories within WSL roots
                findPredicates.Add("! -path '*/sys/*'");
                findPredicates.Add("! -path '*/proc/*'");
                findPredicates.Add("! -path '*/dev/*'");
                // Exclude .git, vendor, node_modules, storage/framework, bin, and obj folders
                findPredicates.Add("! -path '*/.git/*'");
                findPredicates.Add("! -path '*/vendor/*'");
                findPredicates.Add("! -path '*/node_modules/*'");
                findPredicates.Add("! -path '*/storage/framework/*'");
                findPredicates.Add("! -path '*/bin/*'");
                findPredicates.Add("! -path '*/obj/*'");
            }

            // Add size limit filtering if specified
            // This is applied AFTER binary files and symbolic links filters to ensure
            // those filters take precedence over size limits
            if (sizeLimitType != Models.SizeLimitType.NoLimit && sizeLimitKB.HasValue)
            {
                // Convert to KB for find command
                long sizeKB = sizeLimitKB.Value;
                switch (sizeUnit)
                {
                    case Models.SizeUnit.MB:
                        sizeKB *= 1024;
                        break;
                    case Models.SizeUnit.GB:
                        sizeKB *= 1024 * 1024;
                        break;
                }
                
                // Calculate tolerance in KB
                long toleranceKB = sizeUnit switch
                {
                    Models.SizeUnit.KB => 10,  // 10 KB
                    Models.SizeUnit.MB => 1024,  // 1 MB = 1024 KB
                    Models.SizeUnit.GB => 25 * 1024,  // 25 MB = 25600 KB
                    _ => 10
                };
                
                // Apply tolerance to all operations - calculate values in C# since find doesn't support arithmetic
                if (sizeLimitType == Models.SizeLimitType.LessThan)
                {
                    long upperBound = sizeKB + toleranceKB;
                    findPredicates.Add($"-size -{upperBound}k");
                }
                else if (sizeLimitType == Models.SizeLimitType.EqualTo)
                {
                    // For Equal To with tolerance, use approximate range
                    // Note: find's -size is approximate, so we'll also filter in post-processing
                    long lowerBound = Math.Max(1, sizeKB - toleranceKB);
                    long upperBound = sizeKB + toleranceKB;
                    findPredicates.Add($"-size +{lowerBound}k");
                    findPredicates.Add($"-size -{upperBound}k");
                }
                else if (sizeLimitType == Models.SizeLimitType.GreaterThan)
                {
                    long lowerBound = Math.Max(1, sizeKB - toleranceKB);
                    findPredicates.Add($"-size +{lowerBound}k");
                }
            }

            // Build grep options
            var grepOptions = new List<string> { "-Hn", "-a" }; // -H: print filename, -n: print line number, -a: treat binary files as text
            
            // grep is case-sensitive by default, add -i for case-insensitive
            if (!searchCaseSensitive)
            {
                grepOptions.Add("-i");
            }
            
            if (isRegex)
            {
                grepOptions.Add("-E"); // extended regex
            }
            
            var grepFlags = string.Join(" ", grepOptions);
            var findPreds = string.Join(" ", findPredicates);
            
            // Build the command: find files, then grep them
            var findAndGrep = $"find '{escapedPath}' {findPreds} -exec grep {grepFlags} '{escapedTerm}' {{}} + 2>/dev/null || true";
            
            // Build grep command with optional .gitignore support
            if (respectGitignore)
            {
                // Check if we need to filter by size
                bool hasSizeLimit = sizeLimitType != Models.SizeLimitType.NoLimit && sizeLimitKB.HasValue;
                
                if (hasSizeLimit)
                {
                    // When size limit is specified, we must use find+grep approach to respect size limits
                    // Filter results through git check-ignore to respect .gitignore
                    if (isRegex)
                    {
                        return $"bash -c \"cd '{escapedPath}' && {findAndGrep} | while IFS= read -r line; do file=$(echo \\\"$line\\\" | cut -d: -f1); git check-ignore -q \\\"$file\\\" 2>/dev/null || echo \\\"$line\\\"; done\"";
                    }
                    else
                    {
                        var literalTerm = Regex.Escape(escapedTerm);
                        var findAndGrepLiteral = $"find '{escapedPath}' {findPreds} -exec grep {grepFlags} '{literalTerm}' {{}} + 2>/dev/null || true";
                        return $"bash -c \"cd '{escapedPath}' && {findAndGrepLiteral} | while IFS= read -r line; do file=$(echo \\\"$line\\\" | cut -d: -f1); git check-ignore -q \\\"$file\\\" 2>/dev/null || echo \\\"$line\\\"; done\"";
                    }
                }
                else
                {
                    // No size limit, can use git grep if in a git repository
                    // Use git grep if in a git repository, otherwise use find+grep with git check-ignore filter
                    if (isRegex)
                    {
                        return $"bash -c \"cd '{escapedPath}' && if [ -d .git ]; then git grep {grepFlags} '{escapedTerm}' 2>/dev/null || true; else {findAndGrep} | while IFS= read -r line; do file=$(echo \\\"$line\\\" | cut -d: -f1); git check-ignore -q \\\"$file\\\" 2>/dev/null || echo \\\"$line\\\"; done; fi\"";
                    }
                    else
                    {
                        var literalTerm = Regex.Escape(escapedTerm);
                        var findAndGrepLiteral = $"find '{escapedPath}' {findPreds} -exec grep {grepFlags} '{literalTerm}' {{}} + 2>/dev/null || true";
                        return $"bash -c \"cd '{escapedPath}' && if [ -d .git ]; then git grep {grepFlags} '{literalTerm}' 2>/dev/null || true; else {findAndGrepLiteral} | while IFS= read -r line; do file=$(echo \\\"$line\\\" | cut -d: -f1); git check-ignore -q \\\"$file\\\" 2>/dev/null || echo \\\"$line\\\"; done; fi\"";
                    }
                }
            }
            else
            {
                if (isRegex)
                {
                    return $"bash -c \"cd '{escapedPath}' && {findAndGrep}\"";
                }
                else
                {
                    var literalTerm = Regex.Escape(escapedTerm);
                    var findAndGrepLiteral = $"find '{escapedPath}' {findPreds} -exec grep {grepFlags} '{literalTerm}' {{}} + 2>/dev/null || true";
                    return $"bash -c \"cd '{escapedPath}' && {findAndGrepLiteral}\"";
                }
            }
        }

        private List<SearchResult> ParseGrepOutput(string output, string rootPath, string searchTerm, bool isRegex, bool searchCaseSensitive, Models.StringComparisonMode stringComparisonMode, Models.UnicodeNormalizationMode unicodeNormalizationMode, bool diacriticSensitive, string? culture)
        {
            var results = new List<SearchResult>();

            if (string.IsNullOrWhiteSpace(output))
                return results;

            Regex? compiledRegex = null;
            if (isRegex)
            {
                var options = RegexOptions.Compiled;
                if (!searchCaseSensitive)
                {
                    options |= RegexOptions.IgnoreCase;
                }
                compiledRegex = new Regex(searchTerm, options);
            }

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex < 0) continue;
                
                var secondColonIndex = line.IndexOf(':', colonIndex + 1);
                if (secondColonIndex < 0) continue;

                var fileName = line[..colonIndex];
                var lineNumStr = line[(colonIndex + 1)..secondColonIndex];
                var lineContent = line[(secondColonIndex + 1)..];

                if (int.TryParse(lineNumStr, out int lineNumber))
                {
                    var fullPath = NormalizeWslResultPath(rootPath, fileName);
                    var columnNumber = CalculateColumnNumber(lineContent, searchTerm, isRegex, compiledRegex, searchCaseSensitive);
                    var matchCount = CountMatchesOnLine(lineContent, searchTerm, isRegex, compiledRegex, searchCaseSensitive);

                    // Sanitize the line content to remove invalid characters
                    lineContent = SanitizeStringForDisplay(lineContent);
                    var displayContent = lineContent.Length > 500 ? lineContent[..500] + "..." : lineContent;

                    results.Add(new SearchResult
                    {
                        FileName = Path.GetFileName(fullPath),
                        LineNumber = lineNumber,
                        ColumnNumber = columnNumber,
                        LineContent = displayContent,
                        FullPath = fullPath,
                        RelativePath = GetRelativeWslPath(rootPath, fullPath),
                        MatchCount = matchCount
                    });
                }
            }

            return results;
        }

        private static int CalculateColumnNumber(string line, string searchTerm, bool isRegex, Regex? compiledRegex, bool searchCaseSensitive)
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
                var comparison = searchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                var index = line.IndexOf(searchTerm, comparison);
                if (index >= 0)
                {
                    return index + 1;
                }
            }

            return 1;
        }

        /// <summary>
        /// Counts the number of occurrences of the search term on a single line.
        /// </summary>
        private static int CountMatchesOnLine(string line, string searchTerm, bool isRegex, Regex? compiledRegex, bool searchCaseSensitive)
        {
            if (string.IsNullOrEmpty(line))
                return 0;

            if (isRegex && compiledRegex != null)
            {
                var matches = compiledRegex.Matches(line);
                return matches.Count;
            }
            else
            {
                var comparison = searchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                int count = 0;
                int index = 0;
                while ((index = line.IndexOf(searchTerm, index, comparison)) >= 0)
                {
                    count++;
                    index += searchTerm.Length;
                }
                return Math.Max(count, 1); // Return at least 1 since we know the line matches
            }
        }

        /// <summary>
        /// Performs culture-aware string comparison based on the specified parameters.
        /// </summary>
        private static bool ContainsStringWithCultureAwareComparison(
            string text,
            string searchTerm,
            bool searchCaseSensitive,
            Models.StringComparisonMode stringComparisonMode,
            Models.UnicodeNormalizationMode unicodeNormalizationMode,
            bool diacriticSensitive,
            string? culture)
        {
            // Apply Unicode normalization if specified
            if (unicodeNormalizationMode != Models.UnicodeNormalizationMode.None)
            {
                var normalizationForm = unicodeNormalizationMode.ToNormalizationForm();
                text = text.Normalize(normalizationForm);
                searchTerm = searchTerm.Normalize(normalizationForm);
            }

            // Handle diacritic sensitivity
            if (!diacriticSensitive)
            {
                // Remove diacritics from both text and search term
                text = RemoveDiacritics(text);
                searchTerm = RemoveDiacritics(searchTerm);
            }

            // Allow overriding the culture used for the CurrentCulture comparison mode
            if (!string.IsNullOrWhiteSpace(culture) &&
                stringComparisonMode == Models.StringComparisonMode.CurrentCulture)
            {
                try
                {
                    var comparisonCulture = CultureInfo.GetCultureInfo(culture);
                    var compareOptions = searchCaseSensitive ? CompareOptions.None : CompareOptions.IgnoreCase;
                    return comparisonCulture.CompareInfo.IndexOf(text, searchTerm, compareOptions) >= 0;
                }
                catch (CultureNotFoundException)
                {
                    // Fall back to default comparison handling below
                }
            }

            // Get the appropriate comparison based on the mode
            var comparison = GetStringComparison(stringComparisonMode, searchCaseSensitive, culture);
            
            return text.Contains(searchTerm, comparison);
        }

        /// <summary>
        /// Removes diacritics (accent marks) from a string.
        /// </summary>
        private static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();
            
            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }
            
            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>
        /// Gets the appropriate StringComparison based on the mode and case sensitivity.
        /// </summary>
        private static StringComparison GetStringComparison(
            Models.StringComparisonMode mode,
            bool searchCaseSensitive,
            string? culture)
        {
            return mode switch
            {
                Models.StringComparisonMode.Ordinal
                    => searchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase,
                Models.StringComparisonMode.CurrentCulture
                    => searchCaseSensitive ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase,
                Models.StringComparisonMode.InvariantCulture
                    => searchCaseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase,
                _ => searchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase
            };
        }

        private static string SanitizeStringForDisplay(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Filter out invalid characters that can cause display issues
            // Keep only printable characters and common whitespace
            var sanitized = new System.Text.StringBuilder(input.Length);
            
            foreach (char c in input)
            {
                // Skip Unicode replacement character (U+FFFD) used by StreamReader for invalid bytes
                if (c == '\uFFFD')
                {
                    continue;
                }
                // Replace newlines and carriage returns with spaces for display
                else if (c == '\n' || c == '\r')
                {
                    sanitized.Append(' ');
                }
                // Replace other control characters (except tab) with spaces
                else if (char.IsControl(c) && c != '\t')
                {
                    sanitized.Append(' ');
                }
                // Skip invalid surrogate characters
                else if (char.IsSurrogate(c))
                {
                    continue;
                }
                // Skip unassigned Unicode characters that can cause rendering issues
                else if (char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.OtherNotAssigned)
                {
                    continue;
                }
                else
                {
                    sanitized.Append(c);
                }
            }
            
            return sanitized.ToString();
        }

        private static string GetRelativePath(string rootPath, string filePath)
        {
            try
            {
                return Path.GetRelativePath(rootPath, filePath);
            }
            catch
            {
                return filePath;
            }
        }

        private static string NormalizeWslResultPath(string rootPath, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return rootPath;

            if (filePath.StartsWith("/"))
                return filePath;

            if (filePath.StartsWith("./"))
            {
                filePath = filePath[2..];
            }

            if (!rootPath.EndsWith("/"))
            {
                rootPath += "/";
            }

            return rootPath + filePath.TrimStart('/');
        }

        private static string GetRelativeWslPath(string rootPath, string filePath)
        {
            var normalizedRoot = rootPath.EndsWith("/") ? rootPath : rootPath + "/";

            if (filePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return filePath.Substring(normalizedRoot.Length).TrimStart('/');
            }

            return filePath;
        }

        /// <summary>
        /// Checks if a filename matches the specified pattern string.
        /// Supports wildcard patterns separated by '|' and exclusion patterns prefixed with '-'.
        /// </summary>
        private static bool MatchesFileNamePattern(string fileName, string pattern)
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

            // If there are exclude patterns, check them first
            foreach (var excludePattern in excludePatterns)
            {
                if (MatchesWildcard(fileName, excludePattern))
                {
                    return false; // Excluded
                }
            }

            // If there are include patterns, at least one must match
            if (includePatterns.Count > 0)
            {
                foreach (var includePattern in includePatterns)
                {
                    if (MatchesWildcard(fileName, includePattern))
                    {
                        return true; // Matched
                    }
                }
                return false; // No include pattern matched
            }

            // No include patterns, only exclude patterns (and we didn't match any)
            return true;
        }

        /// <summary>
        /// Checks if a filename matches a wildcard pattern (e.g., "*.json", "test.*").
        /// </summary>
        private static bool MatchesWildcard(string fileName, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return true;

            // Convert wildcard pattern to regex
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            try
            {
                return Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                // If regex fails, fall back to simple string comparison
                return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Checks if a file path should be excluded based on directory exclusion patterns.
        /// Supports comma-separated directory names or regex patterns.
        /// </summary>
        private static bool ShouldExcludeDirectory(string filePath, string rootPath, string excludeDirs)
        {
            if (string.IsNullOrWhiteSpace(excludeDirs))
                return false;

            var directory = Path.GetDirectoryName(filePath);
            if (directory == null)
                return false;

            // Get relative path from root
            string relativePath;
            try
            {
                relativePath = Path.GetRelativePath(rootPath, directory);
            }
            catch
            {
                relativePath = directory;
            }

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
                    var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        if (regex.IsMatch(part))
                        {
                            return true;
                        }
                    }
                    // Also check the full relative path
                    if (regex.IsMatch(relativePath))
                    {
                        return true;
                    }
                }
                catch
                {
                    // If regex is invalid, fall back to comma-separated matching
                    isRegex = false;
                }
            }

            if (!isRegex)
            {
                // Comma-separated directory names
                var dirNames = excludeDirs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var dirName in dirNames)
                {
                    if (parts.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ShouldUseWindowsSearchIndex(bool preferWindowsSearchIndex, bool isRegex)
        {
            if (!preferWindowsSearchIndex)
                return false;

            if (isRegex)
                return false;

            if (!OperatingSystem.IsWindows())
                return false;

            return true;
        }

        public async Task<List<FileSearchResult>> ReplaceAsync(
            string path,
            string searchTerm,
            string replaceWith,
            bool isRegex,
            bool respectGitignore = false,
            bool searchCaseSensitive = false,
            bool includeSystemFiles = false,
            bool includeSubfolders = true,
            bool includeHiddenItems = false,
            bool includeBinaryFiles = false,
            bool includeSymbolicLinks = false,
            Models.SizeLimitType sizeLimitType = Models.SizeLimitType.NoLimit,
            long? sizeLimitKB = null,
            Models.SizeUnit sizeUnit = Models.SizeUnit.KB,
            string matchFileNames = "",
            string excludeDirs = "",
            Models.StringComparisonMode stringComparisonMode = Models.StringComparisonMode.Ordinal,
            Models.UnicodeNormalizationMode unicodeNormalizationMode = Models.UnicodeNormalizationMode.None,
            bool diacriticSensitive = true,
            string? culture = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(searchTerm))
                return new List<FileSearchResult>();

            cancellationToken.ThrowIfCancellationRequested();

            if (IsWslPath(path))
            {
                return await ReplaceWslPathAsync(path, searchTerm, replaceWith, isRegex, respectGitignore, searchCaseSensitive, includeSystemFiles, includeSubfolders, includeHiddenItems, includeBinaryFiles, includeSymbolicLinks, sizeLimitType, sizeLimitKB, sizeUnit, matchFileNames, excludeDirs, stringComparisonMode, unicodeNormalizationMode, diacriticSensitive, culture, cancellationToken);
            }
            else
            {
                return await ReplaceWindowsPathAsync(path, searchTerm, replaceWith, isRegex, respectGitignore, searchCaseSensitive, includeSystemFiles, includeSubfolders, includeHiddenItems, includeBinaryFiles, includeSymbolicLinks, sizeLimitType, sizeLimitKB, sizeUnit, matchFileNames, excludeDirs, stringComparisonMode, unicodeNormalizationMode, diacriticSensitive, culture, cancellationToken);
            }
        }

        private async Task<List<FileSearchResult>> ReplaceWindowsPathAsync(
            string path,
            string searchTerm,
            string replaceWith,
            bool isRegex,
            bool respectGitignore,
            bool searchCaseSensitive,
            bool includeSystemFiles,
            bool includeSubfolders,
            bool includeHiddenItems,
            bool includeBinaryFiles,
            bool includeSymbolicLinks,
            Models.SizeLimitType sizeLimitType,
            long? sizeLimitKB,
            Models.SizeUnit sizeUnit,
            string matchFileNames,
            string excludeDirs,
            Models.StringComparisonMode stringComparisonMode,
            Models.UnicodeNormalizationMode unicodeNormalizationMode,
            bool diacriticSensitive,
            string? culture,
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Directory not found: {path}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var results = new ConcurrentBag<FileSearchResult>();
            
            // Pre-compile regex once if needed
            Regex? compiledRegex = null;
            if (isRegex)
            {
                try
                {
                    var regexOptions = RegexOptions.Compiled;
                    if (!searchCaseSensitive)
                    {
                        regexOptions |= RegexOptions.IgnoreCase;
                    }
                    compiledRegex = new Regex(searchTerm, regexOptions);
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"Invalid regex pattern: {ex.Message}");
                }
            }

            // Configure enumeration options
            var enumOptions = new EnumerationOptions
            {
                RecurseSubdirectories = includeSubfolders,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.None
            };

            if (!includeSystemFiles)
            {
                enumOptions.AttributesToSkip |= FileAttributes.System;
            }

            if (!includeHiddenItems)
            {
                enumOptions.AttributesToSkip |= FileAttributes.Hidden;
            }

            // Get all files
            var files = Directory.EnumerateFiles(path, "*", enumOptions);

            // Filter Unix-style hidden files (starting with .) when includeHiddenItems is false
            // This handles files like .env, .gitignore that don't have Windows Hidden attribute
            // but are considered hidden on Unix/WSL systems (e.g., when accessing WSL via mounted drive)
            if (!includeHiddenItems)
            {
                files = files.Where(f =>
                {
                    // Check if filename starts with .
                    var fileName = Path.GetFileName(f);
                    if (!string.IsNullOrEmpty(fileName) && fileName.StartsWith("."))
                        return false;
                    
                    // Check if any parent directory starts with .
                    var directory = Path.GetDirectoryName(f);
                    if (directory != null)
                    {
                        var parts = directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        if (parts.Any(p => !string.IsNullOrEmpty(p) && p.StartsWith(".")))
                            return false;
                    }
                    return true;
                });
            }

            // Filter .git, vendor, node_modules, and storage/framework folders unless system files are included
            if (!includeSystemFiles)
            {
                files = files.Where(f =>
                {
                    var directory = Path.GetDirectoryName(f);
                    if (directory != null)
                    {
                        var parts = directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        if (parts.Contains(".git", StringComparer.OrdinalIgnoreCase) ||
                            parts.Contains("vendor", StringComparer.OrdinalIgnoreCase) ||
                            parts.Contains("node_modules", StringComparer.OrdinalIgnoreCase) ||
                            parts.Contains("bin", StringComparer.OrdinalIgnoreCase) ||
                            parts.Contains("obj", StringComparer.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                        // Check for storage/framework path
                        for (int i = 0; i < parts.Length - 1; i++)
                        {
                            if (parts[i].Equals("storage", StringComparison.OrdinalIgnoreCase) &&
                                parts[i + 1].Equals("framework", StringComparison.OrdinalIgnoreCase))
                            {
                                return false;
                            }
                        }
                    }
                    return true;
                });
            }

            // Filter by .gitignore if needed
            if (respectGitignore)
            {
                files = files.Where(f => !_gitIgnoreService.ShouldIgnoreFile(f, path));
            }

            // Filter by filename pattern if specified
            if (!string.IsNullOrWhiteSpace(matchFileNames))
            {
                files = files.Where(f =>
                {
                    var fileName = Path.GetFileName(f);
                    return MatchesFileNamePattern(fileName, matchFileNames);
                });
            }

            // Filter by excluded directories if specified
            if (!string.IsNullOrWhiteSpace(excludeDirs))
            {
                files = files.Where(f => !ShouldExcludeDirectory(f, path, excludeDirs));
            }

            // Filter binary files if not included
            if (!includeBinaryFiles)
            {
                files = files.Where(f =>
                {
                    var ext = Path.GetExtension(f);
                    return string.IsNullOrEmpty(ext) || !BinaryExtensions.Contains(ext);
                });
            }
            else
            {
                // When includeBinaryFiles is true, only include files that are either:
                // 1. Not in BinaryExtensions (normal text files)
                // 2. In SearchableBinaryExtensions (binary files we can search)
                files = files.Where(f =>
                {
                    var ext = Path.GetExtension(f);
                    // Include if it's not a binary file (normal text file)
                    if (string.IsNullOrEmpty(ext) || !BinaryExtensions.Contains(ext))
                        return true;
                    // Include if it's a searchable binary file
                    return SearchableBinaryExtensions.Contains(ext);
                });
            }

            // Filter symbolic links if not included
            if (!includeSymbolicLinks)
            {
                files = files.Where(f =>
                {
                    try
                    {
                        var fileInfo = new FileInfo(f);
                        return (fileInfo.Attributes & FileAttributes.ReparsePoint) == 0;
                    }
                    catch
                    {
                        return false;
                    }
                });
            }

            // Filter by size limit if specified
            if (sizeLimitType != Models.SizeLimitType.NoLimit && sizeLimitKB.HasValue)
            {
                long sizeLimitBytes = sizeLimitKB.Value;
                switch (sizeUnit)
                {
                    case Models.SizeUnit.MB:
                        sizeLimitBytes *= 1024 * 1024;
                        break;
                    case Models.SizeUnit.GB:
                        sizeLimitBytes *= 1024 * 1024 * 1024;
                        break;
                    default: // KB
                        sizeLimitBytes *= 1024;
                        break;
                }

                // Calculate tolerance based on size unit
                // KB: 10KB tolerance, MB: 1MB tolerance, GB: 25MB tolerance
                long tolerance = sizeUnit switch
                {
                    Models.SizeUnit.KB => 10 * 1024,  // 10 KB
                    Models.SizeUnit.MB => 1 * 1024 * 1024,  // 1 MB
                    Models.SizeUnit.GB => 25 * 1024 * 1024,  // 25 MB
                    _ => 10 * 1024  // Default to 10 KB
                };

                files = files.Where(f =>
                {
                    try
                    {
                        var fileInfo = new FileInfo(f);
                        var fileSize = fileInfo.Length;
                        return sizeLimitType switch
                        {
                            // Apply tolerance to all operations: Less Than allows up to (limit + tolerance),
                            // Equal To allows ±tolerance, Greater Than allows down to (limit - tolerance)
                            Models.SizeLimitType.LessThan => fileSize < (sizeLimitBytes + tolerance),
                            Models.SizeLimitType.EqualTo => Math.Abs(fileSize - sizeLimitBytes) <= tolerance,
                            Models.SizeLimitType.GreaterThan => fileSize > (sizeLimitBytes - tolerance),
                            _ => true
                        };
                    }
                    catch
                    {
                        return false;
                    }
                });
            }

            // Process files in parallel
            var parallelOptions = new ParallelOptions 
            { 
                MaxDegreeOfParallelism = MaxParallelism,
                CancellationToken = cancellationToken
            };
            
            await Task.Run(() =>
            {
                Parallel.ForEach(files, parallelOptions, file =>
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var fileInfo = new FileInfo(file);
                        var fileSize = fileInfo.Length;
                        var encodingResult = _encodingDetectionService.DetectFileEncoding(file);
                        var encoding = encodingResult.Encoding;
                        var content = File.ReadAllText(file, encoding);
                        
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        int matchCount = 0;
                        string newContent;
                        
                        // Count matches before replacement
                        if (isRegex && compiledRegex != null)
                        {
                            var matches = compiledRegex.Matches(content);
                            matchCount = matches.Count;
                            if (matchCount > 0)
                            {
                                newContent = compiledRegex.Replace(content, replaceWith);
                            }
                            else
                            {
                                return; // No matches, skip this file
                            }
                        }
                        else
                        {
                            // Use culture-aware comparison for counting matches
                            var comparison = GetStringComparison(stringComparisonMode, searchCaseSensitive, culture);
                            var index = 0;
                            while ((index = content.IndexOf(searchTerm, index, comparison)) >= 0)
                            {
                                matchCount++;
                                index += searchTerm.Length;
                            }
                           
                            if (matchCount > 0)
                            {
                                newContent = content.Replace(searchTerm, replaceWith, comparison);
                            }
                            else
                            {
                                return; // No matches, skip this file
                            }
                        }
                        
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // Write the modified content back to the file
                        File.WriteAllText(file, newContent, encoding);
                        
                        // Update file size after write
                        fileInfo.Refresh();
                        fileSize = fileInfo.Length;
                        
                        var relativePath = Path.GetRelativePath(path, file);
                        results.Add(new FileSearchResult
                        {
                            FileName = Path.GetFileName(file),
                            Size = fileSize,
                            MatchCount = matchCount,
                            FullPath = file,
                            RelativePath = relativePath,
                            Extension = Path.GetExtension(file),
                            Encoding = encoding.EncodingName,
                            DateModified = fileInfo.LastWriteTime
                        });
                    }
                    catch
                    {
                        // Skip files that can't be read/written
                    }
                });
            });

            return results.ToList();
        }

        private async Task<List<FileSearchResult>> ReplaceWslPathAsync(
            string path,
            string searchTerm,
            string replaceWith,
            bool isRegex,
            bool respectGitignore,
            bool searchCaseSensitive,
            bool includeSystemFiles,
            bool includeSubfolders,
            bool includeHiddenItems,
            bool includeBinaryFiles,
            bool includeSymbolicLinks,
            Models.SizeLimitType sizeLimitType,
            long? sizeLimitKB,
            Models.SizeUnit sizeUnit,
            string matchFileNames,
            string excludeDirs,
            Models.StringComparisonMode stringComparisonMode,
            Models.UnicodeNormalizationMode unicodeNormalizationMode,
            bool diacriticSensitive,
            string? culture,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // For WSL paths, we'll use sed to perform the replacement
            // First, find all files that match, then use sed to replace
            var results = new List<FileSearchResult>();
            
            // Convert Windows path to WSL path and extract distribution
            var wslPath = ConvertToWslPath(path);
            var distro = ExtractWslDistribution(path);
            var distroArg = !string.IsNullOrEmpty(distro) ? $"-d {distro} " : "";
            var escapedPath = wslPath.Replace("'", "'\"'\"'");
            
            var escapedTerm = searchTerm.Replace("'", "'\"'\"'");
            var escapedReplace = replaceWith.Replace("'", "'\"'\"'");
            
            // Build find predicates (similar to SearchWslPathAsync)
            var findPredicates = new List<string>
            {
                "-type f"
            };
            
            if (!includeSubfolders)
            {
                findPredicates.Add($"! -path '{escapedPath}/*/*'");
            }
            
            if (!includeHiddenItems)
            {
                findPredicates.Add("! -name '.*'");
            }
            
            if (!includeBinaryFiles)
            {
                var binaryExts = new[]
                {
                    "exe", "dll", "bin", "zip", "tar", "gz", "7z", "rar",
                    "png", "jpg", "jpeg", "gif", "bmp", "ico", "svg", "webp",
                    "mp3", "mp4", "avi", "mkv", "wav", "flac", "ogg",
                    "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx",
                    "pdb", "cache", "lock", "pack", "idx"
                };
                
                foreach (var ext in binaryExts)
                {
                    findPredicates.Add($"! -name '*.{ext}'");
                }
            }
            else
            {
                // When includeBinaryFiles is true, exclude non-searchable binary files
                // Only include searchable binary files (.docx, .xlsx, .pptx, .odt, .ods, .odp, .zip, .pdf, .rtf)
                var nonSearchableBinaryExts = new[]
                {
                    "exe", "dll", "bin", "tar", "gz", "7z", "rar",
                    "png", "jpg", "jpeg", "gif", "bmp", "ico", "svg", "webp",
                    "mp3", "mp4", "avi", "mkv", "wav", "flac", "ogg",
                    "doc", "xls", "ppt",  // Old Office formats (OLE) - cannot search without libraries
                    "pdb", "cache", "lock", "pack", "idx"
                };
                
                foreach (var ext in nonSearchableBinaryExts)
                {
                    findPredicates.Add($"! -name '*.{ext}'");
                }
            }
            
            if (!includeSymbolicLinks)
            {
                findPredicates.Add("! -type l");
            }
            
            if (!includeSystemFiles)
            {
                findPredicates.Add("! -path '*/sys/*'");
                findPredicates.Add("! -path '*/proc/*'");
                findPredicates.Add("! -path '*/dev/*'");
                findPredicates.Add("! -path '*/.git/*'");
                findPredicates.Add("! -path '*/vendor/*'");
                findPredicates.Add("! -path '*/node_modules/*'");
                findPredicates.Add("! -path '*/storage/framework/*'");
                findPredicates.Add("! -path '*/bin/*'");
                findPredicates.Add("! -path '*/obj/*'");
            }

            if (sizeLimitType != Models.SizeLimitType.NoLimit && sizeLimitKB.HasValue)
            {
                long sizeKB = sizeLimitKB.Value;
                switch (sizeUnit)
                {
                    case Models.SizeUnit.MB:
                        sizeKB *= 1024;
                        break;
                    case Models.SizeUnit.GB:
                        sizeKB *= 1024 * 1024;
                        break;
                }
                
                string sizePredicate = sizeLimitType switch
                {
                    Models.SizeLimitType.LessThan => $"-size -{sizeKB}k",
                    Models.SizeLimitType.EqualTo => $"-size {sizeKB}k",
                    Models.SizeLimitType.GreaterThan => $"-size +{sizeKB}k",
                    _ => string.Empty
                };
                if (!string.IsNullOrEmpty(sizePredicate))
                {
                    findPredicates.Add(sizePredicate);
                }
            }

            var findPreds = string.Join(" ", findPredicates);
            
            int CountMatches(string targetPath)
            {
                try
                {
                    var grepOptions = new List<string> { "-o", "-a" };
                    if (!searchCaseSensitive)
                    {
                        grepOptions.Add("-i");
                    }
                    if (isRegex)
                    {
                        grepOptions.Add("-E");
                    }
                    
                    var grepFlags = string.Join(" ", grepOptions);
                    var countCommand = $"grep {grepFlags} '{escapedTerm}' '{targetPath}' | wc -l";
                    
                    var countProcessInfo = new ProcessStartInfo
                    {
                        FileName = "wsl",
                        Arguments = $"{distroArg}bash -c \"{countCommand}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    
                    using var countProcess = Process.Start(countProcessInfo);
                    if (countProcess == null)
                    {
                        return 0;
                    }
                    
                    var countOutput = countProcess.StandardOutput.ReadToEnd();
                    countProcess.WaitForExit();
                    
                    return int.TryParse(countOutput.Trim(), out int matchCount) ? matchCount : 0;
                }
                catch
                {
                    return 0;
                }
            }
            
            // First, find files that contain the search term
            var grepOptions = new List<string> { "-l", "-a" }; // -l: list files only
            if (!searchCaseSensitive)
            {
                grepOptions.Add("-i");
            }
            if (isRegex)
            {
                grepOptions.Add("-E");
            }
            
            var grepFlags = string.Join(" ", grepOptions);
            
            // Find files and check if they contain the search term
            var findAndGrep = $"find '{escapedPath}' {findPreds} -exec grep {grepFlags} '{escapedTerm}' {{}} + 2>/dev/null || true";
            
            if (respectGitignore)
            {
                // Use git grep -l when in a git repository (much faster and respects .gitignore)
                // git grep -l returns relative paths, so we prefix with the base path
                // Fall back to regular find+grep if not in a git repository
                findAndGrep = $"cd '{escapedPath}' && if [ -d .git ]; then git grep -l {grepFlags} '{escapedTerm}' 2>/dev/null | sed 's|^|{escapedPath}/|' || true; else find '{escapedPath}' {findPreds} -exec grep {grepFlags} '{escapedTerm}' {{}} + 2>/dev/null || true; fi";
            }
            
            await Task.Run(() =>
            {
                try
                {
                    var wslArgs = $"{distroArg}bash -c \"{findAndGrep}\"";
                    
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "wsl",
                        Arguments = wslArgs,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    
                    using var process = Process.Start(processInfo);
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();
                        
                        // Split by both \r\n and \n to handle different line endings
                        var filePaths = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        
                        // For each file, perform the replacement using sed
                        foreach (var filePath in filePaths)
                        {
                            try
                            {
                                var trimmedPath = filePath.Trim();
                                if (string.IsNullOrEmpty(trimmedPath))
                                    continue;
                                
                                var matchCount = CountMatches(trimmedPath);
                                if (matchCount <= 0)
                                    continue;
                                
                                // Use sed to perform the replacement
                                var sedCommand = isRegex
                                    ? $"sed -i -E 's/{escapedTerm}/{escapedReplace}/g' '{trimmedPath}'"
                                    : $"sed -i 's/{escapedTerm}/{escapedReplace}/g' '{trimmedPath}'";
                                
                                if (!searchCaseSensitive && !isRegex)
                                {
                                    sedCommand = $"sed -i 's/{escapedTerm}/{escapedReplace}/gi' '{trimmedPath}'";
                                }
                                
                                var sedProcessInfo = new ProcessStartInfo
                                {
                                    FileName = "wsl",
                                    Arguments = $"{distroArg}bash -c \"{sedCommand}\"",
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    CreateNoWindow = true
                                };
                                
                                using var sedProcess = Process.Start(sedProcessInfo);
                                if (sedProcess != null)
                                {
                                    sedProcess.WaitForExit();
                                    
                                    long fileSize = 0;
                                    DateTime dateModified = DateTime.MinValue;

                                    var statCommand = $"stat -c '%s %Y' '{trimmedPath}'";
                                    var statProcessInfo = new ProcessStartInfo
                                    {
                                        FileName = "wsl",
                                        Arguments = $"{distroArg}bash -c \"{statCommand}\"",
                                        UseShellExecute = false,
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true,
                                        CreateNoWindow = true
                                    };
                                    
                                    using var statProcess = Process.Start(statProcessInfo);
                                    if (statProcess != null)
                                    {
                                        var statOutput = statProcess.StandardOutput.ReadToEnd();
                                        statProcess.WaitForExit();
                                        
                                        var statParts = statOutput.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                        if (statParts.Length >= 2)
                                        {
                                            long.TryParse(statParts[0], out fileSize);
                                            if (long.TryParse(statParts[1], out long unixTime))
                                            {
                                                dateModified = DateTimeOffset
                                                    .FromUnixTimeSeconds(unixTime)
                                                    .DateTime;
                                            }
                                        }
                                    }

                                    var fileName = Path.GetFileName(trimmedPath);
                                    var relativePath = GetRelativeWslPath(wslPath, trimmedPath);
                                    
                                    results.Add(new FileSearchResult
                                    {
                                        FileName = fileName,
                                        Size = fileSize,
                                        MatchCount = matchCount,
                                        FullPath = trimmedPath,
                                        RelativePath = relativePath,
                                        Extension = Path.GetExtension(fileName),
                                        Encoding = "UTF-8",
                                        DateModified = dateModified
                                    });
                                }
                            }
                            catch
                            {
                                // Skip files that can't be processed
                            }
                        }
                    }
                }
                catch
                {
                    // Handle errors
                }
            });

            // Apply filename and directory filters (post-processing for WSL)
            if (!string.IsNullOrWhiteSpace(matchFileNames))
            {
                results = results.Where(r => MatchesFileNamePattern(r.FileName, matchFileNames)).ToList();
            }
            
            if (!string.IsNullOrWhiteSpace(excludeDirs))
            {
                results = results.Where(r =>
                {
                    var relativePath = r.RelativePath;
                    if (string.IsNullOrEmpty(relativePath))
                        return true;
                    
                    var dirPath = Path.GetDirectoryName(relativePath);
                    if (string.IsNullOrEmpty(dirPath))
                        return true;
                    
                    var parts = dirPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
                    var dirNames = excludeDirs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    
                    bool isRegex = excludeDirs.StartsWith("^") || excludeDirs.Contains("(") || excludeDirs.Contains("[") || excludeDirs.Contains("$");
                    if (isRegex)
                    {
                        try
                        {
                            var regex = new Regex(excludeDirs, RegexOptions.IgnoreCase);
                            foreach (var part in parts)
                            {
                                if (regex.IsMatch(part))
                                    return false;
                            }
                            if (regex.IsMatch(dirPath))
                                return false;
                        }
                        catch
                        {
                            // Fall through to comma-separated check
                        }
                    }
                    
                    foreach (var dirName in dirNames)
                    {
                        if (parts.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                            return false;
                    }
                    
                    return true;
                }).ToList();
            }

            return results;
        }

        private string GetString(string key) =>
            _localizationService.GetLocalizedString(key);

        private string GetString(string key, params object[] args) =>
            _localizationService.GetLocalizedString(key, args);

    }
}
