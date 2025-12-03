using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Grex.Services;
using Grex.ViewModels;
using Xunit;

namespace Grex.UITests
{
    [Collection("UI SettingsOverride collection")]
    public class SearchUITests
    {
        [Fact]
        public void MainWindow_InitializesCorrectly()
        {
            // Arrange & Act
            // Note: This would typically be implemented with a UI testing framework like WinAppDriver
            // For now, we'll test the ViewModel interactions that drive the UI

            var mainViewModel = new MainViewModel();

            // Assert
            mainViewModel.Should().NotBeNull();
            mainViewModel.Tabs.Should().NotBeNull();
            mainViewModel.Tabs.Should().HaveCount(1);
            mainViewModel.SelectedTab.Should().NotBeNull();
        }

        [UITestMethod]
        public void TabViewModel_PropertyChanges_UpdateUIBindings()
        {
            // Arrange
            var searchService = new Grex.Services.SearchService();
            using var tabViewModel = new TabViewModel(searchService);

            // Act & Assert - Test that property changes work correctly for UI binding
            tabViewModel.SearchPath = "C:\\Test\\Path";
            tabViewModel.SearchPath.Should().Be("C:\\Test\\Path");

            tabViewModel.SearchTerm = "test search";
            tabViewModel.SearchTerm.Should().Be("test search");

            tabViewModel.IsRegexSearch = true;
            tabViewModel.IsRegexSearch.Should().BeTrue();

            tabViewModel.RespectGitignore = true;
            tabViewModel.RespectGitignore.Should().BeTrue();

            tabViewModel.SearchCaseSensitive = true;
            tabViewModel.SearchCaseSensitive.Should().BeTrue();

            tabViewModel.IncludeSubfolders = false;
            tabViewModel.IncludeSubfolders.Should().BeFalse();

            tabViewModel.IsRegexSearch = false;
            tabViewModel.UseWindowsSearchIndex.Should().BeFalse();
            tabViewModel.UseWindowsSearchIndex = true;
            tabViewModel.UseWindowsSearchIndex.Should().BeTrue();

            tabViewModel.StatusText = "Test Status";
            tabViewModel.StatusText.Should().Be("Test Status");
        }

        [UITestMethod]
        public void TabViewModel_WindowsSearchOption_DisablesWhenNotSupported()
        {
            var searchService = new Grex.Services.SearchService();
            using var tabViewModel = new TabViewModel(searchService);

            tabViewModel.SearchPath = "C:\\Test";
            tabViewModel.IsRegexSearch = false;
            tabViewModel.UseWindowsSearchIndex = true;

            tabViewModel.IsWindowsSearchOptionEnabled.Should().BeTrue();
            tabViewModel.UseWindowsSearchIndex.Should().BeTrue();

            tabViewModel.IsRegexSearch = true;
            tabViewModel.IsWindowsSearchOptionEnabled.Should().BeFalse();
            tabViewModel.UseWindowsSearchIndex.Should().BeFalse();

            tabViewModel.IsRegexSearch = false;
            tabViewModel.SearchPath = "\\\\wsl$\\Ubuntu\\home\\user";
            tabViewModel.IsWindowsSearchOptionEnabled.Should().BeFalse();
        }

        [UITestMethod]
        public void TabViewModel_CanSearchProperty_ControlsSearchButtonState()
        {
            // Arrange
            var searchService = new Grex.Services.SearchService();
            using var tabViewModel = new TabViewModel(searchService);

            // Act & Assert - Initially should not be able to search
            tabViewModel.CanSearch.Should().BeFalse();

            // Act - Add path but no term
            tabViewModel.SearchPath = "C:\\Test";
            tabViewModel.CanSearch.Should().BeFalse();

            // Act - Add term but no path
            tabViewModel.SearchPath = "";
            tabViewModel.SearchTerm = "test";
            tabViewModel.CanSearch.Should().BeFalse();

            // Act - Add both path and term
            tabViewModel.SearchPath = "C:\\Test";
            tabViewModel.CanSearch.Should().BeTrue();

            // Act - Simulate searching state by using reflection to set IsSearching
            var isSearchingProperty = typeof(TabViewModel).GetProperty("IsSearching");
            var setMethod = isSearchingProperty?.GetSetMethod(true); // Get private setter
            setMethod?.Invoke(tabViewModel, new object[] { true });
            tabViewModel.CanSearch.Should().BeFalse();
            
            // Cleanup - reset to false
            setMethod?.Invoke(tabViewModel, new object[] { false });
        }

        [UITestMethod]
        public async Task TabViewModel_PerformSearchAsync_UpdatesUIState()
        {
            // Arrange
            var testDirectory = UITestDataHelper.CreateTestDirectory();
            var testFile = UITestDataHelper.CreateTestFile(testDirectory, "test.txt", "Hello World\nTest Content");
            var searchService = new Grex.Services.SearchService();
            using var tabViewModel = new TabViewModel(searchService);

            tabViewModel.SearchPath = testDirectory;
            tabViewModel.SearchTerm = "Hello";

            // Act
            var searchTask = tabViewModel.PerformSearchAsync();

            // Assert - Status should update to searching
            tabViewModel.StatusText.Should().Be(L("SearchingStatus"));
            tabViewModel.CanSearch.Should().BeFalse();

            await searchTask;

            // Assert - Status should update with results
            var expectedMatches = tabViewModel.SearchResults.Count;
            var expectedFiles = tabViewModel.SearchResults.GroupBy(r => r.FullPath).Count();
            tabViewModel.StatusText.Should().Be(L("FoundMatchesStatus", expectedMatches, expectedFiles));
            tabViewModel.CanSearch.Should().BeTrue();
            tabViewModel.SearchResults.Should().NotBeEmpty();

            // Cleanup
            UITestDataHelper.CleanupTestDirectory(testDirectory);
        }

        [UITestMethod]
        public void MainViewModel_TabManagement_UpdatesUIState()
        {
            // Arrange
            var mainViewModel = new MainViewModel();
            var initialTabCount = mainViewModel.Tabs.Count;
            var initialSelectedTab = mainViewModel.SelectedTab!;

            // Act - Add new tab
            mainViewModel.AddTab();

            // Assert
            mainViewModel.Tabs.Should().HaveCount(initialTabCount + 1);
            mainViewModel.SelectedTab.Should().NotBe(initialSelectedTab);
            mainViewModel.SelectedTab.Should().Be(mainViewModel.Tabs[initialTabCount]);

            // Act - Remove tab
            var tabToRemove = mainViewModel.SelectedTab!;
            mainViewModel.RemoveTab(tabToRemove);

            // Assert
            mainViewModel.Tabs.Should().HaveCount(initialTabCount);
            mainViewModel.SelectedTab.Should().Be(initialSelectedTab);
        }

        [UITestMethod]
        public void MainViewModel_CanRemoveTabProperty_ControlsRemoveButtonState()
        {
            // Arrange
            var mainViewModel = new MainViewModel();

            // Act & Assert - With single tab, should not be able to remove
            mainViewModel.CanRemoveTab.Should().BeFalse();

            // Act - Add tab
            mainViewModel.AddTab();

            // Assert - With multiple tabs, should be able to remove
            mainViewModel.CanRemoveTab.Should().BeTrue();

            // Act - Remove tab back to single
            mainViewModel.RemoveTab(mainViewModel.SelectedTab!);

            // Assert - Back to single tab, should not be able to remove
            mainViewModel.CanRemoveTab.Should().BeFalse();
        }

        [UITestMethod]
        public void TabViewModel_SortResults_UpdatesUIOrder()
        {
            // Arrange
            var searchService = new Grex.Services.SearchService();
            using var tabViewModel = new TabViewModel(searchService);

            // Add some test results
            var results = UITestDataHelper.CreateSampleSearchResults();
            foreach (var result in results)
            {
                tabViewModel.SearchResults.Add(result);
            }

            // Act - Sort by filename
            tabViewModel.SortResults(Grex.Models.SearchResultSortField.FileName);

            // Assert - Results should be sorted
            for (int i = 1; i < tabViewModel.SearchResults.Count; i++)
            {
                var prev = tabViewModel.SearchResults[i - 1].FileName;
                var curr = tabViewModel.SearchResults[i].FileName;
                prev.CompareTo(curr).Should().BeLessOrEqualTo(0);
            }

            // Act - Sort by line number
            tabViewModel.SortResults(Grex.Models.SearchResultSortField.LineNumber);

            // Assert - Results should be sorted by line number
            for (int i = 1; i < tabViewModel.SearchResults.Count; i++)
            {
                var prev = tabViewModel.SearchResults[i - 1].LineNumber;
                var curr = tabViewModel.SearchResults[i].LineNumber;
                prev.Should().BeLessOrEqualTo(curr);
            }
        }

        [UITestMethod]
        public void TabViewModel_ClearResults_UpdatesUIState()
        {
            // Arrange
            var searchService = new Grex.Services.SearchService();
            using var tabViewModel = new TabViewModel(searchService);

            // Add some test results
            tabViewModel.SearchResults.Add(new Grex.Models.SearchResult { FileName = "test.txt" });
            tabViewModel.FileSearchResults.Add(new Grex.Models.FileSearchResult { FileName = "test.txt" });
            tabViewModel.StatusText = "Some status";

            // Act
            tabViewModel.ClearResults();

            // Assert
            tabViewModel.SearchResults.Should().BeEmpty();
            tabViewModel.FileSearchResults.Should().BeEmpty();
            tabViewModel.StatusText.Should().Be(L("ReadyStatus"));
        }

        [UITestMethod]
        public void TabViewModel_SearchModeToggle_SwitchesResultCollections()
        {
            // Arrange
            var searchService = new Grex.Services.SearchService();
            using var tabViewModel = new TabViewModel(searchService);

            // Act - Start with line search mode
            tabViewModel.IsFilesSearch = false;
            tabViewModel.SearchResults.Add(new Grex.Models.SearchResult { FileName = "test.txt" });

            // Assert - Should have line results but no file results
            tabViewModel.SearchResults.Should().NotBeEmpty();
            tabViewModel.FileSearchResults.Should().BeEmpty();

            // Act - Switch to file search mode
            tabViewModel.IsFilesSearch = true;

            // Assert - Property should be updated (actual result switching happens during search)
            tabViewModel.IsFilesSearch.Should().BeTrue();
        }

        [UITestMethod]
        public void TabViewModel_TabTitleUpdate_ReflectsSearchPath()
        {
            // Arrange
            var searchService = new Grex.Services.SearchService();
            using var tabViewModel = new TabViewModel(searchService, "Test Tab");
            var originalTitle = tabViewModel.TabTitle;

            // Act - Set search path
            tabViewModel.SearchPath = "C:\\Users\\TestUser\\Documents\\Project";

            // Assert - Title should be updated to reflect path
            tabViewModel.TabTitle.Should().NotBe(originalTitle);
            tabViewModel.TabTitle.Should().Contain("Project");

            // Act - Clear search path
            tabViewModel.SearchPath = "";

            // Assert - Title should revert to original
            tabViewModel.TabTitle.Should().Be(originalTitle);
        }

        [UITestMethod]
        public async Task TabViewModel_ErrorHandling_UpdatesUIWithErrorState()
        {
            // Arrange
            var searchService = new Grex.Services.SearchService();
            using var tabViewModel = new TabViewModel(searchService);

            tabViewModel.SearchPath = "C:\\NonExistentDirectory";
            tabViewModel.SearchTerm = "test";

            // Act
            await tabViewModel.PerformSearchAsync();

            // Assert
            var errorPrefix = L("ErrorStatus", string.Empty);
            tabViewModel.StatusText.Should().StartWith(errorPrefix);
            tabViewModel.SearchResults.Should().BeEmpty();
            tabViewModel.CanSearch.Should().BeTrue(); // Should be able to search again after error
        }
        private static string L(string key, params object[] args) =>
            LocalizationService.Instance.GetLocalizedString(key, args);
    }

    // UI-specific test data helper for UI tests
    internal static class UITestDataHelper
    {
        internal static string CreateTestDirectory()
        {
            var testDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "grex_UI_Test_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(testDir);
            return testDir;
        }

        internal static string CreateTestFile(string directory, string fileName, string content)
        {
            var filePath = System.IO.Path.Combine(directory, fileName);
            System.IO.File.WriteAllText(filePath, content);
            return filePath;
        }

        internal static void CleanupTestDirectory(string directory)
        {
            if (System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.Delete(directory, true);
            }
        }

        internal static System.Collections.Generic.List<Grex.Models.SearchResult> CreateSampleSearchResults()
        {
            return new System.Collections.Generic.List<Grex.Models.SearchResult>
            {
                new Grex.Models.SearchResult
                {
                    FileName = "file1.txt",
                    LineNumber = 1,
                    ColumnNumber = 1,
                    LineContent = "test content",
                    FullPath = "C:\\test\\file1.txt",
                    RelativePath = "file1.txt"
                },
                new Grex.Models.SearchResult
                {
                    FileName = "file2.txt",
                    LineNumber = 2,
                    ColumnNumber = 5,
                    LineContent = "another test",
                    FullPath = "C:\\test\\file2.txt",
                    RelativePath = "file2.txt"
                }
            };
        }
    }
}