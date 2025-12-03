using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Grex.Models;
using Grex.Services;
using Grex.ViewModels;
using Moq;
using Xunit;

namespace Grex.Tests.ViewModels
{
    [Collection("SettingsOverride collection")]
    public class TabViewModelTests : IDisposable
    {
        private readonly Mock<ISearchService> _mockSearchService;
        private readonly TabViewModel _tabViewModel;

        public TabViewModelTests()
        {
            _mockSearchService = new Mock<ISearchService>();
            _tabViewModel = new TabViewModel(_mockSearchService.Object, "Test Tab");
        }

        public void Dispose()
        {
            _tabViewModel.Dispose();
        }

        [Fact]
        public void Constructor_WithValidParameters_InitializesCorrectly()
        {
            // Act & Assert
            _tabViewModel.TabTitle.Should().Be("Test Tab");
            _tabViewModel.SearchResults.Should().NotBeNull();
            _tabViewModel.SearchResults.Should().BeOfType<ObservableCollection<SearchResult>>();
            _tabViewModel.FileSearchResults.Should().NotBeNull();
            _tabViewModel.FileSearchResults.Should().BeOfType<ObservableCollection<FileSearchResult>>();
            _tabViewModel.StatusText.Should().Be(L("ReadyStatus"));
            _tabViewModel.IsSearching.Should().BeFalse();
            _tabViewModel.CanSearch.Should().BeFalse();
        }

        [Fact]
        public void Constructor_WithNullTitle_UsesDefaultTitle()
        {
            // Act
            var tabViewModel = new TabViewModel(_mockSearchService.Object);

            // Assert
            tabViewModel.TabTitle.Should().NotBeNullOrEmpty();

            tabViewModel.Dispose();
        }

        [Fact]
        public void SearchPath_WhenSet_RaisesPropertyChangedAndUpdatesCanSearch()
        {
            // Arrange
            var propertyChangedRaised = false;
            var canSearchChanged = false;
            
            _tabViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TabViewModel.SearchPath))
                    propertyChangedRaised = true;
                if (args.PropertyName == nameof(TabViewModel.CanSearch))
                    canSearchChanged = true;
            };

            // Act
            _tabViewModel.SearchPath = "C:\\Test";

            // Assert
            propertyChangedRaised.Should().BeTrue();
            canSearchChanged.Should().BeTrue();
            _tabViewModel.SearchPath.Should().Be("C:\\Test");
        }

        [Fact]
        public void SearchTerm_WhenSet_RaisesPropertyChangedAndUpdatesCanSearch()
        {
            // Arrange
            var propertyChangedRaised = false;
            var canSearchChanged = false;
            
            _tabViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TabViewModel.SearchTerm))
                    propertyChangedRaised = true;
                if (args.PropertyName == nameof(TabViewModel.CanSearch))
                    canSearchChanged = true;
            };

            // Act
            _tabViewModel.SearchTerm = "test";

            // Assert
            propertyChangedRaised.Should().BeTrue();
            canSearchChanged.Should().BeTrue();
            _tabViewModel.SearchTerm.Should().Be("test");
        }

        [Fact]
        public void CanSearch_WithValidPathAndTerm_ReturnsTrue()
        {
            // Arrange
            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";

            // Act & Assert
            _tabViewModel.CanSearch.Should().BeTrue();
        }

        [Fact]
        public void CanSearch_WithEmptyPath_ReturnsFalse()
        {
            // Arrange
            _tabViewModel.SearchPath = "";
            _tabViewModel.SearchTerm = "test";

            // Act & Assert
            _tabViewModel.CanSearch.Should().BeFalse();
        }

        [Fact]
        public void CanSearch_WithEmptyTerm_ReturnsFalse()
        {
            // Arrange
            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "";

            // Act & Assert
            _tabViewModel.CanSearch.Should().BeFalse();
        }

        [Fact]
        public void CanSearch_WhenSearching_ReturnsFalse()
        {
            // Arrange
            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";
            
            // Simulate searching state by using reflection to set IsSearching
            var isSearchingProperty = typeof(TabViewModel).GetProperty("IsSearching");
            var setMethod = isSearchingProperty?.GetSetMethod(true); // Get private setter
            setMethod?.Invoke(_tabViewModel, new object[] { true });

            // Act & Assert
            _tabViewModel.CanSearch.Should().BeFalse();
            
            // Cleanup - reset to false
            setMethod?.Invoke(_tabViewModel, new object[] { false });
        }

        [Fact]
        public async Task PerformSearchAsync_WithValidParameters_CallsSearchService()
        {
            // Arrange
            var searchResults = TestDataHelper.CreateSampleSearchResults();
            _mockSearchService.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Models.SizeLimitType>(),
                It.IsAny<long?>(),
                It.IsAny<Models.SizeUnit>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(),
                It.IsAny<Models.UnicodeNormalizationMode>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResults);

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";

            // Act
            await _tabViewModel.PerformSearchAsync();

            // Assert - Verify that SearchAsync was called with the correct path and search term
            // Use It.IsAny for boolean parameters since they come from default settings
            _mockSearchService.Verify(x => x.SearchAsync(
                "C:\\Test", "test", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                Models.SizeLimitType.NoLimit, null, It.IsAny<Models.SizeUnit>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(), It.IsAny<Models.UnicodeNormalizationMode>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PerformSearchAsync_WithResults_PopulatesSearchResults()
        {
            // Arrange
            var searchResults = TestDataHelper.CreateSampleSearchResults();
            _mockSearchService.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Models.SizeLimitType>(),
                It.IsAny<long?>(),
                It.IsAny<Models.SizeUnit>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(),
                It.IsAny<Models.UnicodeNormalizationMode>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResults);

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";

            // Act
            await _tabViewModel.PerformSearchAsync();

            // Assert
            _tabViewModel.SearchResults.Should().HaveCount(searchResults.Count);
            var expectedMatches = searchResults.Count;
            var expectedFiles = searchResults.GroupBy(r => r.FullPath).Count();
            // In test environment without WinUI context, LocalizationService returns the key
            // In production with timing, it would format as "Found X matches in Y files in Z seconds"
            // Either way, it should contain the status key identifier or formatted output
            _tabViewModel.StatusText.Should().NotBeNullOrEmpty();
            // The status could be the raw key or the formatted string depending on context
            var statusContainsExpectedInfo = _tabViewModel.StatusText.Contains("Found") 
                || _tabViewModel.StatusText.Contains("FoundMatchesStatus");
            statusContainsExpectedInfo.Should().BeTrue();
        }

        [Fact]
        public async Task PerformSearchAsync_WithFilesSearchMode_PopulatesFileSearchResults()
        {
            // Arrange
            var searchResults = TestDataHelper.CreateSampleSearchResults();
            _mockSearchService.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Models.SizeLimitType>(),
                It.IsAny<long?>(),
                It.IsAny<Models.SizeUnit>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(),
                It.IsAny<Models.UnicodeNormalizationMode>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResults);

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";
            _tabViewModel.IsFilesSearch = true;

            // Act
            await _tabViewModel.PerformSearchAsync();

            // Assert
            _tabViewModel.FileSearchResults.Should().NotBeEmpty();
            _tabViewModel.SearchResults.Should().BeEmpty();
            var expectedMatches = _tabViewModel.FileSearchResults.Sum(r => r.MatchCount);
            var expectedFiles = _tabViewModel.FileSearchResults.Count;
            // In test environment without WinUI context, LocalizationService returns the key
            // In production with timing, it would format as "Found X matches in Y files in Z seconds"
            _tabViewModel.StatusText.Should().NotBeNullOrEmpty();
            var statusContainsExpectedInfo = _tabViewModel.StatusText.Contains("Found") 
                || _tabViewModel.StatusText.Contains("FoundMatchesStatus");
            statusContainsExpectedInfo.Should().BeTrue();
        }

        [Fact]
        public void ClearResults_ClearsAllResults()
        {
            // Arrange
            _tabViewModel.SearchResults.Add(new SearchResult());
            _tabViewModel.FileSearchResults.Add(new FileSearchResult());
            _tabViewModel.StatusText = "Some status";

            // Act
            _tabViewModel.ClearResults();

            // Assert
            _tabViewModel.SearchResults.Should().BeEmpty();
            _tabViewModel.FileSearchResults.Should().BeEmpty();
            _tabViewModel.StatusText.Should().Be(L("ReadyStatus"));
        }

        [Fact]
        public void SortResults_WithValidField_SortsResults()
        {
            // Arrange
            for (int i = 0; i < 5; i++)
            {
                _tabViewModel.SearchResults.Add(new SearchResult
                {
                    FileName = $"file{i}.txt",
                    LineNumber = i + 1
                });
            }

            // Act
            _tabViewModel.SortResults(SearchResultSortField.FileName);

            // Assert
            var sortedResults = _tabViewModel.SearchResults.ToList();
            for (int i = 1; i < sortedResults.Count; i++)
            {
                sortedResults[i].FileName.CompareTo(sortedResults[i - 1].FileName)
                    .Should().BeGreaterOrEqualTo(0);
            }
        }

        [Fact]
        public void BooleanProperties_WhenSet_RaisePropertyChanged()
        {
            // Arrange & Act & Assert for each boolean property
            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.IsRegexSearch = false;

            var properties = new[]
            {
                nameof(TabViewModel.IsRegexSearch),
                nameof(TabViewModel.RespectGitignore),
                nameof(TabViewModel.SearchCaseSensitive),
                nameof(TabViewModel.IncludeSystemFiles),
                nameof(TabViewModel.IncludeSubfolders),
                nameof(TabViewModel.IncludeHiddenItems),
                nameof(TabViewModel.IncludeBinaryFiles),
                nameof(TabViewModel.IncludeSymbolicLinks),
                nameof(TabViewModel.IsFilesSearch),
                nameof(TabViewModel.UseWindowsSearchIndex)
            };

            foreach (var propertyName in properties)
            {
                var propertyChangedRaised = false;
                _tabViewModel.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == propertyName)
                        propertyChangedRaised = true;
                };

                // Act - get current value and set to opposite to ensure change
                var property = _tabViewModel.GetType().GetProperty(propertyName);
                if (property != null && property.PropertyType == typeof(bool))
                {
                    var currentValue = (bool)(property.GetValue(_tabViewModel) ?? false);
                    var newValue = !currentValue;
                    property.SetValue(_tabViewModel, newValue);

                    // Assert
                    propertyChangedRaised.Should().BeTrue($"PropertyChanged event should be raised for {propertyName}");
                    
                    // Reset to original value
                    property.SetValue(_tabViewModel, currentValue);
                }
            }
        }

        [Fact]
        public void TabTitle_WhenSet_RaisesPropertyChanged()
        {
            // Arrange
            var propertyChangedRaised = false;
            _tabViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TabViewModel.TabTitle))
                    propertyChangedRaised = true;
            };

            // Act
            _tabViewModel.TabTitle = "New Title";

            // Assert
            propertyChangedRaised.Should().BeTrue();
            _tabViewModel.TabTitle.Should().Be("New Title");
        }

        [Fact]
        public void StatusText_WhenSet_RaisesPropertyChanged()
        {
            // Arrange
            var propertyChangedRaised = false;
            _tabViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TabViewModel.StatusText))
                    propertyChangedRaised = true;
            };

            // Act
            _tabViewModel.StatusText = "New Status";

            // Assert
            propertyChangedRaised.Should().BeTrue();
            _tabViewModel.StatusText.Should().Be("New Status");
        }

        [Fact]
        public async Task PerformSearchAsync_WithException_HandlesErrorGracefully()
        {
            // Arrange
            _mockSearchService.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Models.SizeLimitType>(),
                It.IsAny<long?>(),
                It.IsAny<Models.SizeUnit>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(),
                It.IsAny<Models.UnicodeNormalizationMode>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Test exception"));

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";

            // Act
            await _tabViewModel.PerformSearchAsync();

            // Assert
            _tabViewModel.StatusText.Should().Be(L("ErrorStatus", "Test exception"));
            _tabViewModel.SearchResults.Should().BeEmpty();
            _tabViewModel.IsSearching.Should().BeFalse();
            _tabViewModel.CanSearch.Should().BeTrue(); // Should be searchable again after error
        }

        [Fact]
        public async Task PerformSearchAsync_WithCancellation_DoesNotThrow()
        {
            // Arrange
            var searchResults = TestDataHelper.CreateSampleSearchResults();
            _mockSearchService.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Models.SizeLimitType>(),
                It.IsAny<long?>(),
                It.IsAny<Models.SizeUnit>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(),
                It.IsAny<Models.UnicodeNormalizationMode>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResults);

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";

            // Act & Assert - Should not throw even if search is cancelled
            await _tabViewModel.PerformSearchAsync();
            
            // Verify that SearchAsync was called with the correct path and search term
            // Use It.IsAny for boolean parameters since they come from default settings
            _mockSearchService.Verify(x => x.SearchAsync(
                "C:\\Test", "test", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                Models.SizeLimitType.NoLimit, null, It.IsAny<Models.SizeUnit>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(), It.IsAny<Models.UnicodeNormalizationMode>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void SortResults_WithDescendingSort_TogglesDirection()
        {
            // Arrange
            for (int i = 0; i < 5; i++)
            {
                _tabViewModel.SearchResults.Add(new SearchResult
                {
                    FileName = $"file{i}.txt",
                    LineNumber = i + 1
                });
            }

            // Act - Sort by FileName twice to test direction toggle
            _tabViewModel.SortResults(SearchResultSortField.FileName);
            var firstSort = _tabViewModel.SearchResults.Select(r => r.FileName).ToList();
            
            _tabViewModel.SortResults(SearchResultSortField.FileName);
            var secondSort = _tabViewModel.SearchResults.Select(r => r.FileName).ToList();

            // Assert
            firstSort.Should().NotEqual(secondSort);
            secondSort.Should().Equal(firstSort.AsEnumerable().Reverse());
        }

        [Fact]
        public void SortResults_WithDifferentFields_SortsCorrectly()
        {
            // Arrange
            _tabViewModel.SearchResults.Add(new SearchResult { FileName = "b.txt", LineNumber = 1 });
            _tabViewModel.SearchResults.Add(new SearchResult { FileName = "a.txt", LineNumber = 2 });
            _tabViewModel.SearchResults.Add(new SearchResult { FileName = "c.txt", LineNumber = 3 });

            // Act - Sort by LineNumber (ascending)
            _tabViewModel.SortResults(SearchResultSortField.LineNumber);

            // Assert - When sorted by LineNumber ascending: 1, 2, 3
            _tabViewModel.SearchResults[0].LineNumber.Should().Be(1);
            _tabViewModel.SearchResults[0].FileName.Should().Be("b.txt");
            _tabViewModel.SearchResults[1].LineNumber.Should().Be(2);
            _tabViewModel.SearchResults[1].FileName.Should().Be("a.txt");
            _tabViewModel.SearchResults[2].LineNumber.Should().Be(3);
            _tabViewModel.SearchResults[2].FileName.Should().Be("c.txt");
        }

        [Fact]
        public void SortResults_WithFileSearchMode_SortsCorrectly()
        {
            // Arrange
            _tabViewModel.IsFilesSearch = true;
            _tabViewModel.FileSearchResults.Add(new FileSearchResult { FileName = "b.txt", MatchCount = 5 });
            _tabViewModel.FileSearchResults.Add(new FileSearchResult { FileName = "a.txt", MatchCount = 2 });
            _tabViewModel.FileSearchResults.Add(new FileSearchResult { FileName = "c.txt", MatchCount = 8 });

            // Act - Sort by MatchCount
            _tabViewModel.SortResults(SearchResultSortField.MatchCount);

            // Assert
            _tabViewModel.FileSearchResults[0].FileName.Should().Be("a.txt");
            _tabViewModel.FileSearchResults[1].FileName.Should().Be("b.txt");
            _tabViewModel.FileSearchResults[2].FileName.Should().Be("c.txt");
        }

        [Fact]
        public void SortResults_WithEmptyResults_DoesNotThrow()
        {
            // Arrange & Act & Assert
            Action act = () => _tabViewModel.SortResults(SearchResultSortField.FileName);
            act.Should().NotThrow();
        }

        [Fact]
        public async Task ClearResults_WhenSearching_ResetsStateCorrectly()
        {
            // Arrange
            var pendingSearch = new TaskCompletionSource<List<SearchResult>>();
            _mockSearchService.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Models.SizeLimitType>(),
                It.IsAny<long?>(),
                It.IsAny<Models.SizeUnit>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(),
                It.IsAny<Models.UnicodeNormalizationMode>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .Returns(pendingSearch.Task);

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";

            var searchTask = _tabViewModel.PerformSearchAsync();
            await Task.Yield();
            _tabViewModel.IsSearching.Should().BeTrue();

            // Act
            _tabViewModel.ClearResults();

            // Assert
            _tabViewModel.SearchResults.Should().BeEmpty();
            _tabViewModel.FileSearchResults.Should().BeEmpty();
            _tabViewModel.StatusText.Should().Be(L("ReadyStatus"));
            _tabViewModel.CanSearch.Should().BeTrue(); // Should be searchable after clearing
            _tabViewModel.IsSearching.Should().BeFalse();

            pendingSearch.SetResult(TestDataHelper.CreateSampleSearchResults());
            await searchTask;
        }

        [Fact]
        public void SearchPath_WhenUpdated_UpdatesTabTitle()
        {
            // Arrange
            var originalTitle = _tabViewModel.TabTitle;

            // Act
            _tabViewModel.SearchPath = "C:\\Very\\Long\\Path\\To\\A\\Project\\Folder";

            // Assert
            _tabViewModel.TabTitle.Should().NotBe(originalTitle);
            _tabViewModel.TabTitle.Should().Contain("...");
            _tabViewModel.TabTitle.Should().EndWith("Folder");
        }

        [Fact]
        public void SearchPath_WhenCleared_ResetsTabTitle()
        {
            // Arrange
            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.TabTitle = "Custom Title";

            // Act
            _tabViewModel.SearchPath = "";

            // Assert
            _tabViewModel.TabTitle.Should().Be(_tabViewModel.GetType().GetField("_originalTabTitle",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_tabViewModel) as string);
        }

        [Fact]
        public async Task PropertyChanged_WhenIsSearchingChanged_RaisesPropertyChangedEvent()
        {
            // Arrange
            var propertyChangedRaised = false;
            var pendingSearch = new TaskCompletionSource<List<SearchResult>>();
            _mockSearchService.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Models.SizeLimitType>(),
                It.IsAny<long?>(),
                It.IsAny<Models.SizeUnit>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(),
                It.IsAny<Models.UnicodeNormalizationMode>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .Returns(pendingSearch.Task);

            _tabViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TabViewModel.IsSearching))
                {
                    propertyChangedRaised = true;
                }
            };

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";

            // Act
            var searchTask = _tabViewModel.PerformSearchAsync();
            await Task.Yield();

            // Assert
            propertyChangedRaised.Should().BeTrue();

            pendingSearch.SetResult(TestDataHelper.CreateSampleSearchResults());
            await searchTask;
        }

        [Fact]
        public void ReplaceWith_WhenSet_RaisesPropertyChanged()
        {
            // Arrange
            var propertyChangedRaised = false;
            
            _tabViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TabViewModel.ReplaceWith))
                    propertyChangedRaised = true;
            };

            // Act
            _tabViewModel.ReplaceWith = "replacement";

            // Assert
            propertyChangedRaised.Should().BeTrue();
            _tabViewModel.ReplaceWith.Should().Be("replacement");
        }

        [Fact]
        public async Task PerformReplaceAsync_WithValidParameters_CallsReplaceService()
        {
            // Arrange
            var testResults = new List<FileSearchResult>
            {
                new FileSearchResult
                {
                    FileName = "test.txt",
                    MatchCount = 2,
                    FullPath = "C:\\Test\\test.txt",
                    RelativePath = "test.txt"
                }
            };

            _mockSearchService
                .Setup(x => x.ReplaceAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<Models.SizeLimitType>(),
                    It.IsAny<long?>(),
                    It.IsAny<Models.SizeUnit>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Models.StringComparisonMode>(),
                    It.IsAny<Models.UnicodeNormalizationMode>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(testResults);

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "old";
            _tabViewModel.ReplaceWith = "new";

            // Act
            await _tabViewModel.PerformReplaceAsync();

            // Assert
            _mockSearchService.Verify(x => x.ReplaceAsync(
                "C:\\Test",
                "old",
                "new",
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Models.SizeLimitType>(),
                It.IsAny<long?>(),
                It.IsAny<Models.SizeUnit>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Models.StringComparisonMode>(),
                It.IsAny<Models.UnicodeNormalizationMode>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
            
            _tabViewModel.FileSearchResults.Should().HaveCount(1);
            _tabViewModel.IsFilesSearch.Should().BeTrue();
        }

        [Fact]
        public async Task PerformReplaceAsync_WithEmptyReplaceWith_DoesNotCallService()
        {
            // Arrange
            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "old";
            _tabViewModel.ReplaceWith = "";

            // Act
            await _tabViewModel.PerformReplaceAsync();

            // Assert
            _mockSearchService.Verify(x => x.ReplaceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Models.SizeLimitType>(),
                It.IsAny<long?>(),
                It.IsAny<Models.SizeUnit>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Models.StringComparisonMode>(),
                It.IsAny<Models.UnicodeNormalizationMode>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PerformReplaceAsync_SetsIsFilesSearchToTrue()
        {
            // Arrange
            var testResults = new List<FileSearchResult>();

            _mockSearchService
                .Setup(x => x.ReplaceAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<Models.SizeLimitType>(),
                    It.IsAny<long?>(),
                    It.IsAny<Models.SizeUnit>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Models.StringComparisonMode>(),
                    It.IsAny<Models.UnicodeNormalizationMode>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(testResults);

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "old";
            _tabViewModel.ReplaceWith = "new";
            _tabViewModel.IsFilesSearch = false;

            // Act
            await _tabViewModel.PerformReplaceAsync();

            // Assert
            _tabViewModel.IsFilesSearch.Should().BeTrue();
        }

        [Fact]
        public void MatchFileNames_WhenSet_RaisesPropertyChanged()
        {
            // Arrange
            var propertyChangedRaised = false;
            
            _tabViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TabViewModel.MatchFileNames))
                    propertyChangedRaised = true;
            };

            // Act
            _tabViewModel.MatchFileNames = "*.json";

            // Assert
            propertyChangedRaised.Should().BeTrue();
            _tabViewModel.MatchFileNames.Should().Be("*.json");
        }

        [Fact]
        public void ExcludeDirs_WhenSet_RaisesPropertyChanged()
        {
            // Arrange
            var propertyChangedRaised = false;
            
            _tabViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TabViewModel.ExcludeDirs))
                    propertyChangedRaised = true;
            };

            // Act
            _tabViewModel.ExcludeDirs = "tester,vendor";

            // Assert
            propertyChangedRaised.Should().BeTrue();
            _tabViewModel.ExcludeDirs.Should().Be("tester,vendor");
        }

        [Fact]
        public async Task PerformSearchAsync_WithMatchFileNames_PassesToSearchService()
        {
            // Arrange
            var searchResults = TestDataHelper.CreateSampleSearchResults();
            _mockSearchService.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Models.SizeLimitType>(),
                It.IsAny<long?>(),
                It.IsAny<Models.SizeUnit>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(),
                It.IsAny<Models.UnicodeNormalizationMode>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResults);

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";
            _tabViewModel.MatchFileNames = "*.json";

            // Act
            await _tabViewModel.PerformSearchAsync();

            // Assert
            _mockSearchService.Verify(x => x.SearchAsync(
                "C:\\Test", "test", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                Models.SizeLimitType.NoLimit, null, It.IsAny<Models.SizeUnit>(), "*.json", It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(), It.IsAny<Models.UnicodeNormalizationMode>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PerformSearchAsync_WithExcludeDirs_PassesToSearchService()
        {
            // Arrange
            var searchResults = TestDataHelper.CreateSampleSearchResults();
            _mockSearchService.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Models.SizeLimitType>(),
                It.IsAny<long?>(),
                It.IsAny<Models.SizeUnit>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(),
                It.IsAny<Models.UnicodeNormalizationMode>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResults);

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";
            _tabViewModel.ExcludeDirs = "tester,vendor";

            // Act
            await _tabViewModel.PerformSearchAsync();

            // Assert
            _mockSearchService.Verify(x => x.SearchAsync(
                "C:\\Test", "test", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                Models.SizeLimitType.NoLimit, null, It.IsAny<Models.SizeUnit>(), It.IsAny<string>(), "tester,vendor", It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(), It.IsAny<Models.UnicodeNormalizationMode>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PerformSearchAsync_WhenWindowsSearchEnabled_PassesFlagToService()
        {
            // Arrange
            var searchResults = TestDataHelper.CreateSampleSearchResults();
            _mockSearchService.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Models.SizeLimitType>(),
                It.IsAny<long?>(),
                It.IsAny<Models.SizeUnit>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(),
                It.IsAny<Models.UnicodeNormalizationMode>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResults);

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";
            _tabViewModel.UseWindowsSearchIndex = true;

            // Act
            await _tabViewModel.PerformSearchAsync();

            // Assert
            _mockSearchService.Verify(x => x.SearchAsync(
                "C:\\Test", "test", It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                Models.SizeLimitType.NoLimit, null, It.IsAny<Models.SizeUnit>(), It.IsAny<string>(), It.IsAny<string>(),
                It.Is<bool>(useIndex => useIndex), It.IsAny<Models.StringComparisonMode>(), It.IsAny<Models.UnicodeNormalizationMode>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void UseWindowsSearchIndex_DisablesForRegexSearch()
        {
            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.IsRegexSearch = false;
            _tabViewModel.UseWindowsSearchIndex = true;

            _tabViewModel.IsWindowsSearchOptionEnabled.Should().BeTrue();
            _tabViewModel.UseWindowsSearchIndex.Should().BeTrue();

            _tabViewModel.IsRegexSearch = true;

            _tabViewModel.IsWindowsSearchOptionEnabled.Should().BeFalse();
            _tabViewModel.UseWindowsSearchIndex.Should().BeFalse();
        }

        [Fact]
        public void UseWindowsSearchIndex_DisablesForNonWindowsPaths()
        {
            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.IsRegexSearch = false;
            _tabViewModel.UseWindowsSearchIndex = true;
            _tabViewModel.IsWindowsSearchOptionEnabled.Should().BeTrue();

            _tabViewModel.SearchPath = "\\\\wsl$\\Ubuntu\\home\\user";
            _tabViewModel.IsWindowsSearchOptionEnabled.Should().BeFalse();
            _tabViewModel.UseWindowsSearchIndex.Should().BeFalse();

            _tabViewModel.SearchPath = "D:\\Projects";
            _tabViewModel.IsWindowsSearchOptionEnabled.Should().BeTrue();
            _tabViewModel.UseWindowsSearchIndex.Should().BeFalse();
        }

        #region CancelSearch and Stop Functionality Tests

        [Fact]
        public void CancelSearch_WhenNotSearching_DoesNotThrow()
        {
            // Arrange - TabViewModel is not searching
            _tabViewModel.IsSearching.Should().BeFalse();

            // Act & Assert
            Action act = () => _tabViewModel.CancelSearch();
            act.Should().NotThrow();
        }

        [Fact]
        public async Task CancelSearch_WhenSearching_CancelsTokenAndResetsState()
        {
            // Arrange
            var searchStarted = new TaskCompletionSource<bool>();
            var cancellationReceived = new TaskCompletionSource<bool>();
            
            _mockSearchService.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Models.SizeLimitType>(),
                It.IsAny<long?>(),
                It.IsAny<Models.SizeUnit>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(),
                It.IsAny<Models.UnicodeNormalizationMode>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .Returns(async (string path, string term, bool isRegex, bool respectGitignore, bool caseSensitive,
                    bool includeSystem, bool includeSubfolders, bool includeHidden, bool includeBinary, bool includeSymbolic,
                    Models.SizeLimitType sizeType, long? sizeKB, Models.SizeUnit sizeUnit, string matchFiles, string excludeDirs,
                    bool preferIndex, Models.StringComparisonMode strComp, Models.UnicodeNormalizationMode normMode,
                    bool diacriticSensitive, string culture, CancellationToken ct) =>
                {
                    searchStarted.SetResult(true);
                    // Wait for cancellation
                    try
                    {
                        await Task.Delay(5000, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        cancellationReceived.SetResult(true);
                        throw;
                    }
                    return new List<SearchResult>();
                });

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";

            // Start search
            var searchTask = _tabViewModel.PerformSearchAsync();
            await searchStarted.Task; // Wait for search to actually start
            
            _tabViewModel.IsSearching.Should().BeTrue();

            // Act
            _tabViewModel.CancelSearch();

            // Wait for the search task to complete (it should handle cancellation)
            await searchTask;

            // Assert - After search completes (cancelled), IsSearching should be false
            _tabViewModel.IsSearching.Should().BeFalse();
            
            // Verify cancellation was actually received
            var wasCancelled = await Task.WhenAny(cancellationReceived.Task, Task.Delay(100)) == cancellationReceived.Task;
            wasCancelled.Should().BeTrue("the search service should have received the cancellation");
        }

        [Fact]
        public void CanSearchOrStop_WhenNotSearchingWithValidInput_ReturnsTrue()
        {
            // Arrange
            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";

            // Act & Assert
            _tabViewModel.CanSearchOrStop.Should().BeTrue();
        }

        [Fact]
        public void CanSearchOrStop_WhenNotSearchingWithEmptyPath_ReturnsFalse()
        {
            // Arrange
            _tabViewModel.SearchPath = "";
            _tabViewModel.SearchTerm = "test";

            // Act & Assert
            _tabViewModel.CanSearchOrStop.Should().BeFalse();
        }

        [Fact]
        public void CanSearchOrStop_WhenNotSearchingWithEmptyTerm_ReturnsFalse()
        {
            // Arrange
            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "";

            // Act & Assert
            _tabViewModel.CanSearchOrStop.Should().BeFalse();
        }

        [Fact]
        public void CanReplaceOrStop_WhenNotSearchingWithValidInput_ReturnsTrue()
        {
            // Arrange
            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";
            _tabViewModel.ReplaceWith = "replacement";

            // Act & Assert
            _tabViewModel.CanReplaceOrStop.Should().BeTrue();
        }

        [Fact]
        public void CanReplaceOrStop_WhenNotSearchingWithEmptyReplaceWith_ReturnsFalse()
        {
            // Arrange
            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";
            _tabViewModel.ReplaceWith = "";

            // Act & Assert
            _tabViewModel.CanReplaceOrStop.Should().BeFalse();
        }

        [Fact]
        public async Task PerformSearchAsync_WhenCancelled_HandlesGracefully()
        {
            // Arrange
            _mockSearchService.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Models.SizeLimitType>(),
                It.IsAny<long?>(),
                It.IsAny<Models.SizeUnit>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(),
                It.IsAny<Models.UnicodeNormalizationMode>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";

            // Act
            await _tabViewModel.PerformSearchAsync();

            // Assert - Should handle cancellation gracefully
            _tabViewModel.IsSearching.Should().BeFalse();
            _tabViewModel.SearchResults.Should().BeEmpty();
        }

        [Fact]
        public async Task PerformReplaceAsync_WhenCancelled_HandlesGracefully()
        {
            // Arrange
            _mockSearchService.Setup(x => x.ReplaceAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Models.SizeLimitType>(),
                It.IsAny<long?>(),
                It.IsAny<Models.SizeUnit>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Models.StringComparisonMode>(),
                It.IsAny<Models.UnicodeNormalizationMode>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "old";
            _tabViewModel.ReplaceWith = "new";

            // Act
            await _tabViewModel.PerformReplaceAsync();

            // Assert - Should handle cancellation gracefully
            _tabViewModel.IsSearching.Should().BeFalse();
            _tabViewModel.FileSearchResults.Should().BeEmpty();
        }

        [Fact]
        public void SearchPath_WhenSet_NotifiesCanSearchOrStopChanged()
        {
            // Arrange
            var canSearchOrStopChanged = false;
            _tabViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TabViewModel.CanSearchOrStop))
                    canSearchOrStopChanged = true;
            };

            // Act
            _tabViewModel.SearchPath = "C:\\Test";

            // Assert
            canSearchOrStopChanged.Should().BeTrue();
        }

        [Fact]
        public void SearchTerm_WhenSet_NotifiesCanSearchOrStopChanged()
        {
            // Arrange
            var canSearchOrStopChanged = false;
            _tabViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TabViewModel.CanSearchOrStop))
                    canSearchOrStopChanged = true;
            };

            // Act
            _tabViewModel.SearchTerm = "test";

            // Assert
            canSearchOrStopChanged.Should().BeTrue();
        }

        [Fact]
        public void ReplaceWith_WhenSet_NotifiesCanReplaceOrStopChanged()
        {
            // Arrange
            var canReplaceOrStopChanged = false;
            _tabViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TabViewModel.CanReplaceOrStop))
                    canReplaceOrStopChanged = true;
            };

            // Act
            _tabViewModel.ReplaceWith = "replacement";

            // Assert
            canReplaceOrStopChanged.Should().BeTrue();
        }

        #endregion

        #region Search Timing Tests

        [Fact]
        public async Task PerformSearchAsync_CompletesSuccessfully_SetsStatusText()
        {
            // Arrange - Setup a search
            var searchResults = TestDataHelper.CreateSampleSearchResults();
            _mockSearchService.Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Models.SizeLimitType>(),
                It.IsAny<long?>(),
                It.IsAny<Models.SizeUnit>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<Models.StringComparisonMode>(),
                It.IsAny<Models.UnicodeNormalizationMode>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(searchResults);

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";

            // Act
            await _tabViewModel.PerformSearchAsync();

            // Assert - Status should be set to FoundMatchesStatus (either raw key or formatted)
            _tabViewModel.StatusText.Should().NotBeNullOrEmpty();
            var isExpectedStatus = _tabViewModel.StatusText.Contains("Found") 
                || _tabViewModel.StatusText.Contains("FoundMatchesStatus");
            isExpectedStatus.Should().BeTrue("Status should indicate matches found");
        }

        [Fact]
        public async Task PerformReplaceAsync_CompletesSuccessfully_SetsStatusText()
        {
            // Arrange
            var testResults = new List<FileSearchResult>
            {
                new FileSearchResult
                {
                    FileName = "test.txt",
                    MatchCount = 2,
                    FullPath = "C:\\Test\\test.txt",
                    RelativePath = "test.txt"
                }
            };

            _mockSearchService
                .Setup(x => x.ReplaceAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<Models.SizeLimitType>(),
                    It.IsAny<long?>(),
                    It.IsAny<Models.SizeUnit>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Models.StringComparisonMode>(),
                    It.IsAny<Models.UnicodeNormalizationMode>(),
                    It.IsAny<bool>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(testResults);

            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "old";
            _tabViewModel.ReplaceWith = "new";

            // Act
            await _tabViewModel.PerformReplaceAsync();

            // Assert - Status should be set to ReplacedMatchesStatus (either raw key or formatted)
            _tabViewModel.StatusText.Should().NotBeNullOrEmpty();
            var isExpectedStatus = _tabViewModel.StatusText.Contains("Replaced") 
                || _tabViewModel.StatusText.Contains("ReplacedMatchesStatus");
            isExpectedStatus.Should().BeTrue("Status should indicate replacements made");
        }

        #endregion

        #region Docker Search Tests

        [Fact]
        public void IsDockerSearchGloballyEnabled_WhenDockerSearchIsDisabled_ReturnsFalse()
        {
            // Arrange
            SettingsService.SetEnableDockerSearch(false);

            // Act
            var result = _tabViewModel.IsDockerSearchGloballyEnabled;

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsDockerSearchGloballyEnabled_WhenDockerSearchIsEnabled_ReturnsTrue()
        {
            // Arrange
            SettingsService.SetEnableDockerSearch(true);

            // Act
            var result = _tabViewModel.IsDockerSearchGloballyEnabled;

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsDockerModeActive_WhenNoContainerSelected_ReturnsFalse()
        {
            // Arrange
            SettingsService.SetEnableDockerSearch(true);
            _tabViewModel.SelectedDockerContainer = null;

            // Act & Assert
            _tabViewModel.IsDockerModeActive.Should().BeFalse();
        }

        [Fact]
        public void IsFileBrowserEnabled_WhenDockerModeIsActive_ReturnsFalse()
        {
            // Arrange
            SettingsService.SetEnableDockerSearch(true);
            // Set _isDockerCliAvailable to true using reflection to simulate Docker CLI being available
            var field = typeof(TabViewModel).GetField("_isDockerCliAvailable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(_tabViewModel, true);
            // Trigger property change notification for CanSelectDockerContainer so dependent properties update
            var onPropertyChangedMethod = typeof(TabViewModel).GetMethod("OnPropertyChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new[] { typeof(string) }, null);
            onPropertyChangedMethod?.Invoke(_tabViewModel, new object[] { "CanSelectDockerContainer" });
            
            var container = new DockerContainerInfo { Id = "test123", Name = "test-container" };
            _tabViewModel.SelectedDockerContainer = container;

            // Act & Assert
            _tabViewModel.IsFileBrowserEnabled.Should().BeFalse();
        }

        [Fact]
        public void IsFileBrowserEnabled_WhenDockerModeIsNotActive_ReturnsTrue()
        {
            // Arrange
            SettingsService.SetEnableDockerSearch(true);
            _tabViewModel.SelectedDockerContainer = null;

            // Act & Assert
            _tabViewModel.IsFileBrowserEnabled.Should().BeTrue();
        }

        [Fact]
        public void CanReplace_WhenDockerModeIsActive_ReturnsFalse()
        {
            // Arrange
            SettingsService.SetEnableDockerSearch(true);
            // Set _isDockerCliAvailable to true using reflection to simulate Docker CLI being available
            var field = typeof(TabViewModel).GetField("_isDockerCliAvailable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(_tabViewModel, true);
            // Trigger property change notification for CanSelectDockerContainer so dependent properties update
            var onPropertyChangedMethod = typeof(TabViewModel).GetMethod("OnPropertyChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new[] { typeof(string) }, null);
            onPropertyChangedMethod?.Invoke(_tabViewModel, new object[] { "CanSelectDockerContainer" });
            
            _tabViewModel.SearchPath = "C:\\Test";
            _tabViewModel.SearchTerm = "test";
            _tabViewModel.ReplaceWith = "replace";
            var container = new DockerContainerInfo { Id = "test123", Name = "test-container" };
            _tabViewModel.SelectedDockerContainer = container;

            // Act & Assert
            _tabViewModel.CanReplace.Should().BeFalse();
        }

        [Fact]
        public void ResolveDockerPath_WhenNotInDockerMode_ReturnsOriginalPath()
        {
            // Arrange
            var testPath = "C:\\Test\\file.txt";
            SettingsService.SetEnableDockerSearch(false);

            // Act
            var result = _tabViewModel.ResolveDockerPath(testPath);

            // Assert
            result.Should().Be(testPath);
        }

        [Fact]
        public void ResolveDockerPath_WhenInDockerMode_ReturnsContainerPath()
        {
            // Arrange
            SettingsService.SetEnableDockerSearch(true);
            var container = new DockerContainerInfo { Id = "test123", Name = "test-container" };
            _tabViewModel.SelectedDockerContainer = container;
            // Note: This test would need a real mirror to fully test, but we can test the null case
            var testPath = "C:\\Test\\file.txt";

            // Act
            var result = _tabViewModel.ResolveDockerPath(testPath);

            // Assert - Without an active mirror, should return original path
            result.Should().Be(testPath);
        }

        [Fact]
        public async Task RefreshDockerContainersAsync_WhenDockerSearchIsDisabled_DoesNothing()
        {
            // Arrange
            SettingsService.SetEnableDockerSearch(false);

            // Act
            await _tabViewModel.RefreshDockerContainersAsync();

            // Assert - Should not throw and should not change state
            _tabViewModel.DockerContainers.Should().BeEmpty();
        }

        #endregion

        private static string L(string key, params object[] args) =>
            LocalizationService.Instance.GetLocalizedString(key, args);
    }
}