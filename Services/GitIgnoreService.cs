using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Grex.Services
{
    public class GitIgnoreService
    {
        private readonly Dictionary<string, List<GitIgnoreRule>> _gitignoreCache = new();

        public bool ShouldIgnoreFile(string filePath, string rootPath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(rootPath))
                return false;

            // Normalize paths
            var normalizedRoot = Path.GetFullPath(rootPath).Replace('\\', '/');
            var normalizedFile = Path.GetFullPath(filePath).Replace('\\', '/');

            // Get relative path from root
            if (!normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return false;

            var relativePath = normalizedFile.Substring(normalizedRoot.Length).TrimStart('/');
            if (string.IsNullOrEmpty(relativePath))
                return false;

            // Find all .gitignore files that could affect this file
            var parts = relativePath.Split('/');
            var shouldIgnore = false;

            // Check root .gitignore first
            var rootGitignore = Path.Combine(normalizedRoot, ".gitignore").Replace('\\', '/');
            if (File.Exists(rootGitignore))
            {
                var rules = GetGitIgnoreRules(rootGitignore, normalizedRoot);
                var result = CheckRules(rules, relativePath, parts[parts.Length - 1], false);
                if (result.HasValue)
                {
                    shouldIgnore = result.Value;
                }
            }

            // Check .gitignore files from root to the file's directory (later rules override earlier ones)
            var currentPath = normalizedRoot;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                currentPath = Path.Combine(currentPath, parts[i]).Replace('\\', '/');
                var gitignorePath = Path.Combine(currentPath, ".gitignore").Replace('\\', '/');

                if (File.Exists(gitignorePath))
                {
                    var rules = GetGitIgnoreRules(gitignorePath, currentPath);
                    var pathToCheck = string.Join("/", parts.Skip(i + 1));
                    var result = CheckRules(rules, pathToCheck, parts[parts.Length - 1], false);
                    if (result.HasValue)
                    {
                        shouldIgnore = result.Value;
                    }
                }
            }

            return shouldIgnore;
        }

        private List<GitIgnoreRule> GetGitIgnoreRules(string gitignorePath, string gitignoreDirectory)
        {
            if (_gitignoreCache.TryGetValue(gitignorePath, out var cached))
            {
                return cached;
            }

            var rules = new List<GitIgnoreRule>();
            if (!File.Exists(gitignorePath))
            {
                _gitignoreCache[gitignorePath] = rules;
                return rules;
            }

            try
            {
                var lines = File.ReadAllLines(gitignorePath);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                        continue;

                    var isNegation = trimmed.StartsWith("!");
                    var pattern = isNegation ? trimmed.Substring(1).Trim() : trimmed;
                    if (string.IsNullOrEmpty(pattern))
                        continue;

                    rules.Add(new GitIgnoreRule
                    {
                        Pattern = pattern,
                        IsNegation = isNegation,
                        IsDirectoryOnly = pattern.EndsWith("/"),
                        IsRootRelative = pattern.StartsWith("/"),
                        GitIgnoreDirectory = gitignoreDirectory
                    });
                }
            }
            catch
            {
                // If we can't read the file, return empty rules
            }

            _gitignoreCache[gitignorePath] = rules;
            return rules;
        }

        private bool? CheckRules(List<GitIgnoreRule> rules, string relativePath, string fileName, bool isDirectory)
        {
            bool? result = null;

            foreach (var rule in rules)
            {
                var pattern = rule.Pattern.TrimEnd('/');
                var pathToMatch = rule.IsRootRelative ? relativePath : relativePath;

                // Handle directory-only patterns - they should match files inside the directory
                // For directory patterns like "build/", we want to match "build/file.txt"
                if (rule.IsDirectoryOnly)
                {
                    // Check if the path contains the directory pattern followed by /
                    if (!relativePath.Contains(pattern + "/", StringComparison.OrdinalIgnoreCase) &&
                        !relativePath.StartsWith(pattern + "/", StringComparison.OrdinalIgnoreCase) &&
                        !(relativePath.Equals(pattern, StringComparison.OrdinalIgnoreCase) && isDirectory))
                    {
                        continue;
                    }
                }

                if (MatchesPattern(pattern, pathToMatch, fileName, rule.GitIgnoreDirectory))
                {
                    if (rule.IsNegation)
                    {
                        result = false;
                    }
                    else
                    {
                        result = true;
                    }
                }
            }

            return result;
        }

        private bool MatchesPattern(string pattern, string relativePath, string fileName, string gitignoreDirectory)
        {
            // Convert gitignore pattern to regex
            var regexPattern = ConvertGitIgnorePatternToRegex(pattern);
            
            try
            {
                // Try matching against the full relative path
                if (Regex.IsMatch(relativePath, regexPattern, RegexOptions.IgnoreCase))
                    return true;

                // Try matching against just the filename
                if (Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase))
                    return true;

                // Try matching each path segment
                var segments = relativePath.Split('/');
                foreach (var segment in segments)
                {
                    if (Regex.IsMatch(segment, regexPattern, RegexOptions.IgnoreCase))
                        return true;
                }
            }
            catch
            {
                // Invalid regex pattern, fall back to simple matching
                return SimpleMatch(pattern, relativePath, fileName);
            }

            return false;
        }

        private string ConvertGitIgnorePatternToRegex(string pattern)
        {
            // Handle bracket patterns first (before escaping)
            // Convert [12] to [12] in regex (brackets are character classes in regex)
            var regexPattern = pattern;
            
            // Track if pattern has wildcards (which affects anchoring)
            bool hasWildcard = pattern.Contains("*") || pattern.Contains("?");
            
            // Escape special regex characters, but preserve brackets for character classes
            // We need to manually escape while preserving bracket patterns
            var result = new System.Text.StringBuilder();
            bool inBrackets = false;
            
            for (int i = 0; i < regexPattern.Length; i++)
            {
                char c = regexPattern[i];
                
                if (c == '[')
                {
                    inBrackets = true;
                    result.Append(c);
                }
                else if (c == ']')
                {
                    inBrackets = false;
                    result.Append(c);
                }
                else if (inBrackets)
                {
                    // Inside brackets, escape only special characters that need escaping
                    if (c == '\\' || c == '^' || c == '-')
                        result.Append('\\');
                    result.Append(c);
                }
                else
                {
                    // Outside brackets, escape special regex characters except * and ?
                    if (c == '*' || c == '?')
                    {
                        result.Append(c);
                    }
                    else
                    {
                        result.Append(Regex.Escape(c.ToString()));
                    }
                }
            }
            
            var escaped = result.ToString();
            
            // Handle ** (match zero or more directories) before converting single *
            // Replace ** with a special placeholder first
            escaped = escaped.Replace("**", "___DOUBLE_ASTERISK___");
            
            // Convert single * to [^/]* (match any characters except path separator)
            escaped = escaped.Replace("*", "[^/]*");
            
            // Convert ? to [^/] (match single character except path separator)
            escaped = escaped.Replace("?", "[^/]");
            
            // Replace double asterisk placeholder with .* (matches any characters including /)
            escaped = escaped.Replace("___DOUBLE_ASTERISK___", ".*");
            
            // Handle patterns starting with **/ - they should match at root or any subdirectory
            if (escaped.StartsWith(".*/"))
            {
                // Pattern like **/test.txt should match both test.txt and subdir/test.txt
                escaped = "^(" + escaped.Substring(3) + "$|.*/" + escaped.Substring(3) + "$)";
            }
            // Anchor to start if pattern starts with /
            else if (pattern.StartsWith("/"))
            {
                escaped = "^" + escaped.Substring(2) + "$";
            }
            else if (!hasWildcard)
            {
                // For patterns without wildcards (like .env), match the exact filename
                // This prevents .env from matching .env.docker
                // Pattern should match: the exact filename, or as part of a path ending with that filename
                escaped = "(^" + escaped + "$|/" + escaped + "$)";
            }
            else
            {
                // For patterns with wildcards, anchor at end to prevent partial matches
                escaped = ".*" + escaped + "$";
            }

            return escaped;
        }

        private bool SimpleMatch(string pattern, string relativePath, string fileName)
        {
            // Simple wildcard matching for common cases
            if (pattern.Contains("*") || pattern.Contains("?"))
            {
                var regexPattern = "^" + Regex.Escape(pattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") + "$";
                return Regex.IsMatch(relativePath, regexPattern, RegexOptions.IgnoreCase) ||
                       Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase);
            }

            // For patterns without wildcards, match exactly (not as substring)
            // This ensures .env matches only .env, not .env.docker
            return relativePath.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                   relativePath.EndsWith("/" + pattern, StringComparison.OrdinalIgnoreCase);
        }

        private class GitIgnoreRule
        {
            public string Pattern { get; set; } = string.Empty;
            public bool IsNegation { get; set; }
            public bool IsDirectoryOnly { get; set; }
            public bool IsRootRelative { get; set; }
            public string GitIgnoreDirectory { get; set; } = string.Empty;
        }
    }
}

