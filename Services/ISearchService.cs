using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Grex.Models;

namespace Grex.Services
{
    public interface ISearchService
    {
        Task<List<SearchResult>> SearchAsync(
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
            CancellationToken cancellationToken = default);
        
        Task<List<FileSearchResult>> ReplaceAsync(
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
            CancellationToken cancellationToken = default);
    }
}

