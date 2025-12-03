using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Grex.Models;
using Grex.Services;
using Moq;
using Xunit;

namespace Grex.Tests.Services
{
    public class SearchServiceTests
    {
        private readonly SearchService _searchService;

        public SearchServiceTests()
        {
            _searchService = new SearchService();
        }

        [Fact]
        public async Task SearchAsync_WithValidPathAndTerm_ReturnsResults()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var testFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "Hello World\nThis is a test");
            
            try
            {
                // Act
                var results = await _searchService.SearchAsync(testDirectory, "World", false, false, false, true, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().NotBeNull();
                results.Should().HaveCount(1);
                results[0].FileName.Should().Be("test.txt");
                results[0].LineContent.Should().Contain("Hello World");
                results[0].LineNumber.Should().Be(1);
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithEmptyPath_ReturnsEmptyList()
        {
            // Act
            var results = await _searchService.SearchAsync("", "test", false, false, false, true, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

            // Assert
            results.Should().BeEmpty();
        }

        [Fact]
        public async Task SearchAsync_WithEmptySearchTerm_ReturnsEmptyList()
        {
            // Act
            var results = await _searchService.SearchAsync("C:\\", "", false, false, false, true, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

            // Assert
            results.Should().BeEmpty();
        }

        [Fact]
        public async Task SearchAsync_WithRegexSearch_ReturnsMatchingResults()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var testFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "Hello World\n12345\nAnother line");
            
            try
            {
                // Act
                var results = await _searchService.SearchAsync(testDirectory, @"\d+", true, false, false, true, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().NotBeNull();
                results.Should().HaveCount(1);
                results[0].LineContent.Should().Contain("12345");
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithCaseSensitiveSearch_ReturnsMatchingResults()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var testFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "Hello World\nhello world");
            
            try
            {
                // Act
                var results = await _searchService.SearchAsync(testDirectory, "Hello", false, false, true, true, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().NotBeNull();
                results.Should().HaveCount(1);
                results[0].LineContent.Should().Contain("Hello World");
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithCaseSensitiveSearch_DoesNotMatchDifferentCase()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            TestDataHelper.CreateTestFile(testDirectory, "case.txt", "Banana");

            try
            {
                // Act
                var results = await _searchService.SearchAsync(
                    testDirectory,
                    "banana",
                    isRegex: false,
                    respectGitignore: false,
                    searchCaseSensitive: true,
                    includeSystemFiles: true,
                    includeSubfolders: true,
                    includeHiddenItems: false,
                    includeBinaryFiles: false,
                    includeSymbolicLinks: false,
                    sizeLimitType: Models.SizeLimitType.NoLimit,
                    sizeLimitKB: null,
                    sizeUnit: Models.SizeUnit.KB,
                    matchFileNames: "",
                    excludeDirs: "",
                    preferWindowsSearchIndex: false,
                    stringComparisonMode: Models.StringComparisonMode.Ordinal,
                    unicodeNormalizationMode: Models.UnicodeNormalizationMode.None,
                    diacriticSensitive: true,
                    culture: null);

                // Assert
                results.Should().BeEmpty();
            }
            finally
            {
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithCaseInsensitiveOrdinalSearch_FindsDifferentCase()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            TestDataHelper.CreateTestFile(testDirectory, "case.txt", "Banana");

            try
            {
                // Act
                var results = await _searchService.SearchAsync(
                    testDirectory,
                    "banana",
                    isRegex: false,
                    respectGitignore: false,
                    searchCaseSensitive: false,
                    includeSystemFiles: true,
                    includeSubfolders: true,
                    includeHiddenItems: false,
                    includeBinaryFiles: false,
                    includeSymbolicLinks: false,
                    sizeLimitType: Models.SizeLimitType.NoLimit,
                    sizeLimitKB: null,
                    sizeUnit: Models.SizeUnit.KB,
                    matchFileNames: "",
                    excludeDirs: "",
                    preferWindowsSearchIndex: false,
                    stringComparisonMode: Models.StringComparisonMode.Ordinal,
                    unicodeNormalizationMode: Models.UnicodeNormalizationMode.None,
                    diacriticSensitive: true,
                    culture: null);

                // Assert
                results.Should().HaveCount(1);
                results[0].LineContent.Should().Be("Banana");
            }
            finally
            {
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithNonExistentDirectory_ThrowsDirectoryNotFoundException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(
                () => _searchService.SearchAsync("C:\\NonExistentDirectory", "test", false, false, false, true, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null));
        }

        [Fact]
        public void IsWslPath_WithWslPath_ReturnsTrue()
        {
            // Act & Assert
            Assert.True(_searchService.IsWslPath("\\\\wsl$\\Ubuntu\\home"));
            Assert.True(_searchService.IsWslPath("\\\\wsl.localhost\\Ubuntu\\home"));
            Assert.True(_searchService.IsWslPath("/mnt/c/users"));
            Assert.True(_searchService.IsWslPath("/home/user"));
        }

        [Fact]
        public void IsWslPath_WithWindowsPath_ReturnsFalse()
        {
            // Act & Assert
            Assert.False(_searchService.IsWslPath("C:\\Users\\Test"));
            Assert.False(_searchService.IsWslPath("D:\\Projects"));
        }

        [Fact]
        public async Task SearchAsync_WithBinaryFilesFiltered_ExcludesBinaryFiles()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var textFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "Hello World");
            var binaryFile = TestDataHelper.CreateTestFile(testDirectory, "test.exe", "Hello World");
            
            try
            {
                // Act
                var results = await _searchService.SearchAsync(testDirectory, "World", false, false, false, true, false, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().HaveCount(1);
                results[0].FileName.Should().Be("test.txt");
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithBinaryFilesIncluded_IncludesBinaryFiles()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var textFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "Hello World");
            // Use RTF (Rich Text Format) which is a searchable binary file type
            var binaryFile = TestDataHelper.CreateTestFile(testDirectory, "test.rtf", "{\\rtf1\\ansi Hello World}");
            
            try
            {
                // Act
                var results = await _searchService.SearchAsync(testDirectory, "World", false, false, false, true, false, false, true, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().HaveCount(2);
                results.Should().Contain(r => r.FileName == "test.txt");
                results.Should().Contain(r => r.FileName == "test.rtf");
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithHiddenFilesFiltered_ExcludesHiddenFiles()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var normalFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "Hello World");
            var hiddenFile = TestDataHelper.CreateTestFile(testDirectory, ".hidden.txt", "Hello World");
            
            // Set file as hidden
            File.SetAttributes(hiddenFile, File.GetAttributes(hiddenFile) | FileAttributes.Hidden);
            
            try
            {
                // Act
                var results = await _searchService.SearchAsync(testDirectory, "World", false, false, false, true, false, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().HaveCount(1);
                results[0].FileName.Should().Be("test.txt");
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithHiddenFilesIncluded_IncludesHiddenFiles()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var normalFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "Hello World");
            var hiddenFile = TestDataHelper.CreateTestFile(testDirectory, ".hidden.txt", "Hello World");
            
            // Set file as hidden
            File.SetAttributes(hiddenFile, File.GetAttributes(hiddenFile) | FileAttributes.Hidden);
            
            try
            {
                // Act
                var results = await _searchService.SearchAsync(testDirectory, "World", false, false, false, true, false, true, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().HaveCount(2);
                results.Should().Contain(r => r.FileName == "test.txt");
                results.Should().Contain(r => r.FileName == ".hidden.txt");
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithSubfoldersDisabled_SearchesOnlyRootDirectory()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var rootFile = TestDataHelper.CreateTestFile(testDirectory, "root.txt", "Hello World");
            var subDir = Directory.CreateDirectory(Path.Combine(testDirectory, "subdir"));
            var subFile = TestDataHelper.CreateTestFile(subDir.FullName, "sub.txt", "Hello World");
            
            try
            {
                // Act
                var results = await _searchService.SearchAsync(testDirectory, "World", false, false, false, false, false, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().HaveCount(1);
                results[0].FileName.Should().Be("root.txt");
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithInvalidRegexPattern_ThrowsArgumentException()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            TestDataHelper.CreateTestFile(testDirectory, "test.txt", "Hello World");
            
            try
            {
                // Act & Assert
                await Assert.ThrowsAsync<ArgumentException>(
                    () => _searchService.SearchAsync(testDirectory, "[", true, false, false, true, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null));
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithGitIgnoreEnabled_RespectsGitIgnoreRules()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = TestDataHelper.CreateGitIgnoreFile(testDirectory, "*.log");
            var textFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "Hello World");
            var logFile = TestDataHelper.CreateTestFile(testDirectory, "test.log", "Hello World");
            
            try
            {
                // Act
                var results = await _searchService.SearchAsync(testDirectory, "World", false, true, false, true, false, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().HaveCount(1);
                results[0].FileName.Should().Be("test.txt");
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void IsWslPath_WithMixedCasePath_ReturnsCorrectResult()
        {
            // Act & Assert
            Assert.True(_searchService.IsWslPath("\\\\WSL$\\Ubuntu\\home"));
            Assert.True(_searchService.IsWslPath("\\\\WSL.LOCALHOST\\Ubuntu\\home"));
            Assert.True(_searchService.IsWslPath("/MNT/c/users"));
        }

        [Fact]
        public void IsWslPath_WithEdgeCases_ReturnsCorrectResult()
        {
            // Act & Assert
            Assert.False(_searchService.IsWslPath(""));
            Assert.False(_searchService.IsWslPath(null!));
            Assert.False(_searchService.IsWslPath("C"));
            Assert.True(_searchService.IsWslPath("/"));
            Assert.True(_searchService.IsWslPath("/home"));
        }

        [Fact]
        public async Task SearchAsync_WithSpecialCharactersInPath_HandlesCorrectly()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var specialDir = Directory.CreateDirectory(Path.Combine(testDirectory, "test with spaces"));
            var testFile = TestDataHelper.CreateTestFile(specialDir.FullName, "test.txt", "Hello World");
            
            try
            {
                // Act
                var results = await _searchService.SearchAsync(testDirectory, "World", false, false, false, true, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().HaveCount(1);
                results[0].FileName.Should().Be("test.txt");
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task ReplaceAsync_WithValidPathAndTerm_ReplacesText()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var testFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "Hello World\nThis is a test");
            
            try
            {
                // Act
                var results = await _searchService.ReplaceAsync(testDirectory, "World", "Universe", false, false, false, true, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().NotBeNull();
                results.Should().HaveCount(1);
                results[0].FileName.Should().Be("test.txt");
                results[0].MatchCount.Should().Be(1);
                
                // Verify the file was actually modified
                var content = File.ReadAllText(testFile);
                content.Should().Contain("Hello Universe");
                content.Should().NotContain("Hello World");
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task ReplaceAsync_WithEmptyPath_ReturnsEmptyList()
        {
            // Act
            var results = await _searchService.ReplaceAsync("", "test", "replace", false, false, false, true, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

            // Assert
            results.Should().NotBeNull();
            results.Should().BeEmpty();
        }

        [Fact]
        public async Task ReplaceAsync_WithEmptySearchTerm_ReturnsEmptyList()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            
            try
            {
                // Act
                var results = await _searchService.ReplaceAsync(testDirectory, "", "replace", false, false, false, true, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().NotBeNull();
                results.Should().BeEmpty();
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task ReplaceAsync_WithMultipleMatches_ReplacesAll()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var testFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "Hello World\nHello World\nHello World");
            
            try
            {
                // Act
                var results = await _searchService.ReplaceAsync(testDirectory, "Hello", "Hi", false, false, false, true, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().NotBeNull();
                results.Should().HaveCount(1);
                results[0].MatchCount.Should().Be(3);
                
                // Verify all occurrences were replaced
                var content = File.ReadAllText(testFile);
                content.Should().Contain("Hi World");
                content.Should().NotContain("Hello World");
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithMatchFileNames_Wildcard_ReturnsOnlyMatchingFiles()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var jsonFile = TestDataHelper.CreateTestFile(testDirectory, "test.json", "Banana");
            var txtFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "Banana");
            
            try
            {
                // Act
                var results = await _searchService.SearchAsync(testDirectory, "Banana", false, false, false, false, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "*.json", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().NotBeNull();
                results.Should().HaveCount(1);
                results[0].FileName.Should().Be("test.json");
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithMatchFileNames_MultiplePatterns_ReturnsMatchingFiles()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var jsonFile = TestDataHelper.CreateTestFile(testDirectory, "test.json", "Banana");
            var txtFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "Banana");
            var xmlFile = TestDataHelper.CreateTestFile(testDirectory, "test.xml", "Banana");
            
            try
            {
                // Act
                var results = await _searchService.SearchAsync(testDirectory, "Banana", false, false, false, false, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "*.json|*.txt", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().NotBeNull();
                results.Should().HaveCount(2);
                results.Select(r => r.FileName).Should().Contain("test.json");
                results.Select(r => r.FileName).Should().Contain("test.txt");
                results.Select(r => r.FileName).Should().NotContain("test.xml");
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithMatchFileNames_Exclusion_ExcludesMatchingFiles()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var jsonFile = TestDataHelper.CreateTestFile(testDirectory, "test.json", "Banana");
            var txtFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "Banana");
            
            try
            {
                // Act
                var results = await _searchService.SearchAsync(testDirectory, "Banana", false, false, false, false, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "*.json|-*.txt", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().NotBeNull();
                results.Should().HaveCount(1);
                results[0].FileName.Should().Be("test.json");
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithExcludeDirs_CommaSeparated_ExcludesDirectories()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var rootFile = TestDataHelper.CreateTestFile(testDirectory, "root.txt", "Banana");
            var excludedDir = Path.Combine(testDirectory, "tester");
            Directory.CreateDirectory(excludedDir);
            var excludedFile = TestDataHelper.CreateTestFile(excludedDir, "test.json", "Banana");
            
            try
            {
                // Act
                var results = await _searchService.SearchAsync(testDirectory, "Banana", false, false, false, false, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "tester", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().NotBeNull();
                results.Should().HaveCount(1);
                results[0].FileName.Should().Be("root.txt");
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithExcludeDirs_Regex_ExcludesDirectories()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var rootFile = TestDataHelper.CreateTestFile(testDirectory, "root.txt", "Banana");
            var excludedDir = Path.Combine(testDirectory, "tester");
            Directory.CreateDirectory(excludedDir);
            var excludedFile = TestDataHelper.CreateTestFile(excludedDir, "test.json", "Banana");
            var includedDir = Path.Combine(testDirectory, "include");
            Directory.CreateDirectory(includedDir);
            var includedFile = TestDataHelper.CreateTestFile(includedDir, "test.json", "Banana");
            
            try
            {
                // Act
                var results = await _searchService.SearchAsync(testDirectory, "Banana", false, false, false, false, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "^tester$", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().NotBeNull();
                results.Should().HaveCount(2);
                results.Select(r => r.FileName).Should().Contain("root.txt");
                results.Select(r => r.FileName).Should().Contain("test.json");
                // Verify the excluded directory's file is not in results
                results.Select(r => r.FullPath).Should().NotContain(excludedFile);
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithExcludeDirs_Nested_ExcludesNestedDirectories()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var rootFile = TestDataHelper.CreateTestFile(testDirectory, "root.txt", "Banana");
            var nestedDir = Path.Combine(testDirectory, "level1", "tester");
            Directory.CreateDirectory(nestedDir);
            var excludedFile = TestDataHelper.CreateTestFile(nestedDir, "test.json", "Banana");
            
            try
            {
                // Act
                var results = await _searchService.SearchAsync(testDirectory, "Banana", false, false, false, false, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "tester", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().NotBeNull();
                results.Should().HaveCount(1);
                results[0].FileName.Should().Be("root.txt");
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithSizeLimitKB_Applies10KBTolerance()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            // Create files with sizes around 100KB limit
            var file1 = TestDataHelper.CreateTestFile(testDirectory, "file1.txt", new string('A', 100 * 1024)); // Exactly 100KB
            var file2 = TestDataHelper.CreateTestFile(testDirectory, "file2.txt", new string('B', 100 * 1024 + 9 * 1024)); // 100KB + 9KB (within 10KB tolerance)
            var file3 = TestDataHelper.CreateTestFile(testDirectory, "file3.txt", new string('C', 100 * 1024 + 11 * 1024)); // 100KB + 11KB (outside tolerance)
            
            try
            {
                // Act - Search for "A", "B", or "C" with Equal To 100KB limit
                var results1 = await _searchService.SearchAsync(testDirectory, "A", false, false, false, false, true, false, false, false, Models.SizeLimitType.EqualTo, 100, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);
                var results2 = await _searchService.SearchAsync(testDirectory, "B", false, false, false, false, true, false, false, false, Models.SizeLimitType.EqualTo, 100, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);
                var results3 = await _searchService.SearchAsync(testDirectory, "C", false, false, false, false, true, false, false, false, Models.SizeLimitType.EqualTo, 100, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);
                
                // Assert - file1 and file2 should be found (within 10KB tolerance), file3 should not
                results1.Should().HaveCount(1);
                results2.Should().HaveCount(1);
                results3.Should().HaveCount(0);
            }
            finally
            {
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithSizeLimitMB_Applies1MBTolerance()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            // Create files with sizes around 10MB limit
            var file1 = TestDataHelper.CreateTestFile(testDirectory, "file1.txt", new string('A', 10 * 1024 * 1024)); // Exactly 10MB
            var file2 = TestDataHelper.CreateTestFile(testDirectory, "file2.txt", new string('B', 10 * 1024 * 1024 + 1024 * 1024 - 1)); // 10MB + 1MB - 1 byte (within 1MB tolerance)
            var file3 = TestDataHelper.CreateTestFile(testDirectory, "file3.txt", new string('C', 10 * 1024 * 1024 + 1024 * 1024 + 1)); // 10MB + 1MB + 1 byte (outside tolerance)
            
            try
            {
                // Act - Search with Equal To 10MB limit
                var results1 = await _searchService.SearchAsync(testDirectory, "A", false, false, false, false, true, false, false, false, Models.SizeLimitType.EqualTo, 10, Models.SizeUnit.MB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);
                var results2 = await _searchService.SearchAsync(testDirectory, "B", false, false, false, false, true, false, false, false, Models.SizeLimitType.EqualTo, 10, Models.SizeUnit.MB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);
                var results3 = await _searchService.SearchAsync(testDirectory, "C", false, false, false, false, true, false, false, false, Models.SizeLimitType.EqualTo, 10, Models.SizeUnit.MB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);
                
                // Assert - file1 and file2 should be found (within 1MB tolerance), file3 should not
                results1.Should().HaveCount(1);
                results2.Should().HaveCount(1);
                results3.Should().HaveCount(0);
            }
            finally
            {
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithSizeLimitLessThan_AppliesTolerance()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            // Test that "Less Than" with tolerance allows files up to (limit + tolerance)
            // For KB: 100KB limit, 10KB tolerance, so files < 110KB should match
            var file1 = TestDataHelper.CreateTestFile(testDirectory, "file1.txt", new string('A', 100 * 1024)); // Exactly 100KB
            var file2 = TestDataHelper.CreateTestFile(testDirectory, "file2.txt", new string('B', 100 * 1024 + 9 * 1024)); // 100KB + 9KB (within tolerance)
            var file3 = TestDataHelper.CreateTestFile(testDirectory, "file3.txt", new string('C', 100 * 1024 + 11 * 1024)); // 100KB + 11KB (outside tolerance)
            
            try
            {
                // Act - Search with Less Than 100KB limit
                var results1 = await _searchService.SearchAsync(testDirectory, "A", false, false, false, false, true, false, false, false, Models.SizeLimitType.LessThan, 100, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);
                var results2 = await _searchService.SearchAsync(testDirectory, "B", false, false, false, false, true, false, false, false, Models.SizeLimitType.LessThan, 100, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);
                var results3 = await _searchService.SearchAsync(testDirectory, "C", false, false, false, false, true, false, false, false, Models.SizeLimitType.LessThan, 100, Models.SizeUnit.KB, "", "", false, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);
                
                // Assert - file1 and file2 should be found (within 10KB tolerance), file3 should not
                results1.Should().HaveCount(1);
                results2.Should().HaveCount(1);
                results3.Should().HaveCount(0);
            }
            finally
            {
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithWindowsSearchCandidates_UsesIndexedFiles()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var indexedFile = TestDataHelper.CreateTestFile(testDirectory, "hit.txt", "Hello from index");
            TestDataHelper.CreateTestFile(testDirectory, "miss.txt", "Hello from index");
            var fakeIntegration = new FakeWindowsSearchIntegration
            {
                Result = WindowsSearchQueryResult.FromPaths(new[] { indexedFile })
            };
            var searchService = new SearchService(fakeIntegration);

            try
            {
                // Act
                var results = await searchService.SearchAsync(testDirectory, "Hello", false, false, false, true, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", true, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().HaveCount(1);
                results[0].FullPath.Should().Be(indexedFile);
            }
            finally
            {
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public async Task SearchAsync_WithWindowsSearchUnavailable_FallsBackToFullScan()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var indexedFile = TestDataHelper.CreateTestFile(testDirectory, "hit.txt", "Hello world");
            var fakeIntegration = new FakeWindowsSearchIntegration
            {
                Result = WindowsSearchQueryResult.NotAvailable()
            };
            var searchService = new SearchService(fakeIntegration);

            try
            {
                // Act
                var results = await searchService.SearchAsync(testDirectory, "Hello", false, false, false, true, true, false, false, false, Models.SizeLimitType.NoLimit, null, Models.SizeUnit.KB, "", "", true, Models.StringComparisonMode.Ordinal, Models.UnicodeNormalizationMode.None, true, null);

                // Assert
                results.Should().NotBeEmpty();
                results.Should().ContainSingle(r => r.FullPath == indexedFile);
            }
            finally
            {
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        private sealed class FakeWindowsSearchIntegration : IWindowsSearchIntegration
        {
            public WindowsSearchQueryResult Result { get; set; } = WindowsSearchQueryResult.NotAvailable();

            public Task<WindowsSearchQueryResult> QueryIndexedFilesAsync(string rootPath, string searchTerm, bool includeSubfolders)
            {
                return Task.FromResult(Result);
            }
        }
    }
}