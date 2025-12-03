using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Grex.Models;
using Grex.Services;
using Grex.Tests;
using Grex.ViewModels;
using Xunit;

namespace Grex.IntegrationTests
{
    [Collection("Integration SettingsOverride collection")]
    public class SearchWorkflowTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly MainViewModel _mainViewModel;

        public SearchWorkflowTests()
        {
            _testDirectory = TestDataHelper.CreateTestDirectory();
            var searchService = new SearchService();
            _mainViewModel = new MainViewModel(searchService);
        }

        private TabViewModel GetSelectedTab()
        {
            var tab = _mainViewModel.SelectedTab;
            if (tab == null)
            {
                throw new InvalidOperationException("MainViewModel should always have an active tab during integration tests.");
            }
            return tab;
        }

        public void Dispose()
        {
            TestDataHelper.CleanupTestDirectory(_testDirectory);
        }

        [Fact]
        public async Task CompleteSearchWorkflow_FromViewModelToResults_ReturnsExpectedResults()
        {
            // Arrange
            var testFiles = TestDataHelper.CreateSampleFiles(_testDirectory);
            var searchTab = GetSelectedTab();

            searchTab.SearchPath = _testDirectory;
            searchTab.SearchTerm = "test";

            // Act
            await searchTab.PerformSearchAsync();

            // Assert
            searchTab.SearchResults.Should().NotBeEmpty();
            var expectedMatches = searchTab.SearchResults.Count;
            var expectedFiles = searchTab.SearchResults.GroupBy(r => r.FullPath).Count();
            searchTab.StatusText.Should().Be(L("FoundMatchesStatus", expectedMatches, expectedFiles));
        }

        [Fact]
        public async Task SearchWorkflow_WithRegexSearch_ReturnsPatternMatchingResults()
        {
            // Arrange
            var testFile = TestDataHelper.CreateTestFile(_testDirectory, "pattern.txt", "123\nabc\n456\ndef\n789");
            var searchTab = GetSelectedTab();

            searchTab.SearchPath = _testDirectory;
            searchTab.SearchTerm = @"\d+"; // Match digits
            searchTab.IsRegexSearch = true;

            // Act
            await searchTab.PerformSearchAsync();

            // Assert
            searchTab.SearchResults.Should().HaveCount(3); // 123, 456, 789
            searchTab.SearchResults.All(r => r.LineContent.Any(char.IsDigit)).Should().BeTrue();
        }

        [Fact]
        public async Task SearchWorkflow_WithCaseSensitiveSearch_ReturnsCaseMatchingResults()
        {
            // Arrange
            var testFile = TestDataHelper.CreateTestFile(_testDirectory, "case.txt", "Hello World\nhello world\nHELLO WORLD");
            var searchTab = GetSelectedTab();

            searchTab.SearchPath = _testDirectory;
            searchTab.SearchTerm = "Hello";
            searchTab.SearchCaseSensitive = true;

            // Act
            await searchTab.PerformSearchAsync();

            // Assert
            searchTab.SearchResults.Should().HaveCount(1);
            searchTab.SearchResults[0].LineContent.Should().Be("Hello World");
        }

        [Fact]
        public async Task SearchWorkflow_WithGitIgnore_RespectsIgnoreRules()
        {
            // Arrange
            var gitIgnoreFile = Path.Combine(_testDirectory, ".gitignore");
            var includedFile = TestDataHelper.CreateTestFile(_testDirectory, "included.txt", "test content");
            var ignoredFile = TestDataHelper.CreateTestFile(_testDirectory, "ignored.log", "test content");
            var searchTab = GetSelectedTab();

            File.WriteAllText(gitIgnoreFile, "*.log");

            searchTab.SearchPath = _testDirectory;
            searchTab.SearchTerm = "test";
            searchTab.RespectGitignore = true;

            // Act
            await searchTab.PerformSearchAsync();

            // Assert
            searchTab.SearchResults.Should().HaveCount(1);
            searchTab.SearchResults[0].FileName.Should().Be("included.txt");
        }

        [Fact]
        public async Task SearchWorkflow_WithFilesMode_ReturnsAggregatedFileResults()
        {
            // Arrange
            var file1 = TestDataHelper.CreateTestFile(_testDirectory, "file1.txt", "test content\nmore test");
            var file2 = TestDataHelper.CreateTestFile(_testDirectory, "file2.txt", "test content");
            var searchTab = GetSelectedTab();

            searchTab.SearchPath = _testDirectory;
            searchTab.SearchTerm = "test";
            searchTab.IsFilesSearch = true;

            // Act
            await searchTab.PerformSearchAsync();

            // Assert
            searchTab.FileSearchResults.Should().NotBeEmpty();
            searchTab.SearchResults.Should().BeEmpty();
            
            var file1Result = searchTab.FileSearchResults.FirstOrDefault(f => f.FileName == "file1.txt");
            var file2Result = searchTab.FileSearchResults.FirstOrDefault(f => f.FileName == "file2.txt");
            
            file1Result.Should().NotBeNull();
            file2Result.Should().NotBeNull();
            file1Result!.MatchCount.Should().Be(2);
            file2Result!.MatchCount.Should().Be(1);
        }

        [Fact]
        public async Task SearchWorkflow_WithSubfoldersDisabled_SearchesOnlyRootDirectory()
        {
            // Arrange
            var subDir = Directory.CreateDirectory(Path.Combine(_testDirectory, "subdir"));
            var rootFile = TestDataHelper.CreateTestFile(_testDirectory, "root.txt", "test content");
            var subFile = TestDataHelper.CreateTestFile(subDir.FullName, "sub.txt", "test content");
            var searchTab = GetSelectedTab();

            searchTab.SearchPath = _testDirectory;
            searchTab.SearchTerm = "test";
            searchTab.IncludeSubfolders = false;

            // Act
            await searchTab.PerformSearchAsync();

            // Assert
            searchTab.SearchResults.Should().HaveCount(1);
            searchTab.SearchResults[0].FileName.Should().Be("root.txt");
        }

        [Fact]
        public async Task SearchWorkflow_WithHiddenFilesIncluded_SearchesHiddenFiles()
        {
            // Arrange
            var hiddenFile = TestDataHelper.CreateTestFile(_testDirectory, ".hidden.txt", "test content");
            var normalFile = TestDataHelper.CreateTestFile(_testDirectory, "normal.txt", "test content");
            
            // Set file as hidden
            File.SetAttributes(hiddenFile, FileAttributes.Hidden);
            
            var searchTab = GetSelectedTab();

            searchTab.SearchPath = _testDirectory;
            searchTab.SearchTerm = "test";
            searchTab.IncludeHiddenItems = true;

            // Act
            await searchTab.PerformSearchAsync();

            // Assert
            searchTab.SearchResults.Should().HaveCount(2);
            searchTab.SearchResults.Should().Contain(r => r.FileName == ".hidden.txt");
            searchTab.SearchResults.Should().Contain(r => r.FileName == "normal.txt");
        }

        [Fact]
        public async Task SearchWorkflow_WithBinaryFilesExcluded_SkipsBinaryFiles()
        {
            // Arrange
            var textFile = TestDataHelper.CreateTestFile(_testDirectory, "text.txt", "test content");
            var binaryFile = TestDataHelper.CreateTestFile(_testDirectory, "binary.exe", "test content");
            var searchTab = GetSelectedTab();

            searchTab.SearchPath = _testDirectory;
            searchTab.SearchTerm = "test";
            searchTab.IncludeBinaryFiles = false;

            // Act
            await searchTab.PerformSearchAsync();

            // Assert
            searchTab.SearchResults.Should().HaveCount(1);
            searchTab.SearchResults[0].FileName.Should().Be("text.txt");
        }

        [Fact]
        public async Task SearchWorkflow_WithMultipleSearches_ClearsPreviousResults()
        {
            // Arrange
            var file1 = TestDataHelper.CreateTestFile(_testDirectory, "file1.txt", "content1");
            var file2 = TestDataHelper.CreateTestFile(_testDirectory, "file2.txt", "content2");
            var searchTab = GetSelectedTab();

            // Act - First search
            searchTab.SearchPath = _testDirectory;
            searchTab.SearchTerm = "content1";
            await searchTab.PerformSearchAsync();

            var firstSearchCount = searchTab.SearchResults.Count;
            firstSearchCount.Should().Be(1);

            // Act - Second search
            searchTab.SearchTerm = "content2";
            await searchTab.PerformSearchAsync();

            // Assert
            searchTab.SearchResults.Should().HaveCount(1);
            searchTab.SearchResults[0].LineContent.Should().Contain("content2");
            searchTab.SearchResults.Should().NotContain(r => r.LineContent.Contains("content1"));
        }

        [Fact]
        public async Task SearchWorkflow_WithSortOptions_SortsResultsCorrectly()
        {
            // Arrange
            var file1 = TestDataHelper.CreateTestFile(_testDirectory, "zfile.txt", "test content");
            var file2 = TestDataHelper.CreateTestFile(_testDirectory, "afile.txt", "test content");
            var searchTab = GetSelectedTab();

            searchTab.SearchPath = _testDirectory;
            searchTab.SearchTerm = "test";

            // Act
            await searchTab.PerformSearchAsync();
            // Results are now sorted by FileName ascending by default for Content search
            // Calling SortResults again will toggle to descending
            searchTab.SortResults(SearchResultSortField.FileName);

            // Assert - After toggling, results should be in descending order
            var sortedResults = searchTab.SearchResults.ToList();
            sortedResults[0].FileName.Should().Be("zfile.txt");
            sortedResults[1].FileName.Should().Be("afile.txt");
        }

        [Fact]
        public async Task SearchWorkflow_WithTabManagement_CreatesAndManagesTabs()
        {
            // Arrange
            var initialTabCount = _mainViewModel.Tabs.Count;
            var testFiles = TestDataHelper.CreateSampleFiles(_testDirectory);

            // Act - Add new tab
            _mainViewModel.AddTab();
            var newTab = GetSelectedTab();
            newTab.SearchPath = _testDirectory;
            newTab.SearchTerm = "test";
            await newTab.PerformSearchAsync();

            // Assert
            _mainViewModel.Tabs.Should().HaveCount(initialTabCount + 1);
            newTab.SearchResults.Should().NotBeEmpty();

            // Act - Remove tab
            _mainViewModel.RemoveTab(newTab);

            // Assert
            _mainViewModel.Tabs.Should().HaveCount(initialTabCount);
            _mainViewModel.Tabs.Should().NotContain(newTab);
        }

        [Fact]
        public async Task SearchWorkflow_WithLargeDirectory_HandlesPerformanceCorrectly()
        {
            // Arrange
            TestDataHelper.CreateMultipleTestFiles(_testDirectory, 50); // Create 50 files
            var searchTab = GetSelectedTab();

            searchTab.SearchPath = _testDirectory;
            searchTab.SearchTerm = "content";

            // Act
            var startTime = DateTime.Now;
            await searchTab.PerformSearchAsync();
            var endTime = DateTime.Now;
            var duration = endTime - startTime;

            // Assert
            duration.Should().BeLessThan(TimeSpan.FromSeconds(30)); // Should complete within 30 seconds
            searchTab.SearchResults.Should().NotBeEmpty();
            var expectedMatches = searchTab.SearchResults.Count;
            var expectedFiles = searchTab.SearchResults.GroupBy(r => r.FullPath).Count();
            searchTab.StatusText.Should().Be(L("FoundMatchesStatus", expectedMatches, expectedFiles));
        }

        [Fact]
        public async Task SearchWorkflow_WithWindowsSearchToggle_UsesIndexedCandidates()
        {
            // Arrange
            var recordingIntegration = new RecordingWindowsSearchIntegration();
            var searchService = new SearchService(recordingIntegration);
            var mainViewModel = new MainViewModel(searchService);
            var searchTab = mainViewModel.SelectedTab ?? throw new InvalidOperationException("Expected initial tab");

            var indexedFile = TestDataHelper.CreateTestFile(_testDirectory, "indexed.txt", "Indexed content here");
            recordingIntegration.PathsToReturn = new[] { indexedFile };
            recordingIntegration.ScopeAvailable = true;

            searchTab.SearchPath = _testDirectory;
            searchTab.SearchTerm = "Indexed";
            searchTab.UseWindowsSearchIndex = true;

            // Act
            await searchTab.PerformSearchAsync();

            // Assert
            recordingIntegration.QueryCount.Should().BeGreaterThan(0);
            searchTab.SearchResults.Should().NotBeEmpty();
            searchTab.SearchResults.Should().Contain(r => r.FullPath == indexedFile);
        }

        [Fact]
        public void LocalizationService_SetCulture_UpdatesCulture()
        {
            // Arrange
            var locService = LocalizationService.Instance;
            var originalCulture = locService.CurrentCulture;
            
            try
            {
                // Act - Change to Spanish
                locService.SetCulture("es-ES");
                
                // Assert - Verify culture changed
                locService.CurrentCulture.Should().Be("es-ES");
                
                // Verify that localized strings can be retrieved (may return key if resources unavailable)
                var spanishTitle = locService.GetLocalizedString("SettingsTitleTextBlock.Text");
                spanishTitle.Should().NotBeNullOrEmpty();
                
                // Act - Change to French
                locService.SetCulture("fr-FR");
                locService.CurrentCulture.Should().Be("fr-FR");
                
                var frenchTitle = locService.GetLocalizedString("SettingsTitleTextBlock.Text");
                frenchTitle.Should().NotBeNullOrEmpty();
                
                // Act - Change to German
                locService.SetCulture("de-DE");
                locService.CurrentCulture.Should().Be("de-DE");
                
                var germanTitle = locService.GetLocalizedString("SettingsTitleTextBlock.Text");
                germanTitle.Should().NotBeNullOrEmpty();
                
                // Act - Change back to English
                locService.SetCulture("en-US");
                locService.CurrentCulture.Should().Be("en-US");
            }
            finally
            {
                // Restore original culture
                locService.SetCulture(originalCulture);
            }
        }

        [Fact]
        public void SettingsService_UILanguage_PersistsAndLoads()
        {
            // Arrange
            var testLanguage = "es-ES";
            var originalLanguage = SettingsService.GetUILanguage();
            
            try
            {
                // Act - Set UI language
                SettingsService.SetUILanguage(testLanguage);
                
                // Assert - Verify it was saved
                var loadedLanguage = SettingsService.GetUILanguage();
                loadedLanguage.Should().Be(testLanguage);
                
                // Act - Clear and verify default
                SettingsService.SetUILanguage("");
                SettingsService.GetUILanguage().Should().BeEmpty();
            }
            finally
            {
                // Restore original language
                SettingsService.SetUILanguage(originalLanguage);
            }
        }

        private static string L(string key, params object[] args) =>
            LocalizationService.Instance.GetLocalizedString(key, args);

        private sealed class RecordingWindowsSearchIntegration : IWindowsSearchIntegration
        {
            public int QueryCount { get; private set; }
            public bool ScopeAvailable { get; set; } = true;
            public IReadOnlyList<string> PathsToReturn { get; set; } = Array.Empty<string>();

            public Task<WindowsSearchQueryResult> QueryIndexedFilesAsync(string rootPath, string searchTerm, bool includeSubfolders)
            {
                QueryCount++;
                if (!ScopeAvailable)
                {
                    return Task.FromResult(WindowsSearchQueryResult.NotAvailable());
                }

                return Task.FromResult(WindowsSearchQueryResult.FromPaths(PathsToReturn));
            }
        }
    }
}