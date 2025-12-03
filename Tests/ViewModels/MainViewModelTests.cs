using System;
using System.Collections.ObjectModel;
using System.Linq;
using FluentAssertions;
using Grex.Services;
using Grex.ViewModels;
using Moq;
using Xunit;

namespace Grex.Tests.ViewModels
{
    [Collection("SettingsOverride collection")]
    public class MainViewModelTests : IDisposable
    {
        private readonly Mock<ISearchService> _mockSearchService;
        private readonly MainViewModel _mainViewModel;

        public MainViewModelTests()
        {
            _mockSearchService = new Mock<ISearchService>();
            _mainViewModel = new MainViewModel(_mockSearchService.Object);
        }

        public void Dispose()
        {
            _mainViewModel.Dispose();
        }

        [Fact]
        public void Constructor_InitializesWithOneTab()
        {
            // Act & Assert
            _mainViewModel.Tabs.Should().NotBeNull();
            _mainViewModel.Tabs.Should().HaveCount(1);
            _mainViewModel.SelectedTab.Should().NotBeNull();
            _mainViewModel.SelectedTab.Should().Be(_mainViewModel.Tabs.First());
        }

        [Fact]
        public void AddTab_IncreasesTabCountAndSelectsNewTab()
        {
            // Arrange
            var initialTabCount = _mainViewModel.Tabs.Count;

            // Act
            _mainViewModel.AddTab();

            // Assert
            _mainViewModel.Tabs.Should().HaveCount(initialTabCount + 1);
            _mainViewModel.SelectedTab.Should().NotBeNull();
            _mainViewModel.SelectedTab!.Should().Be(_mainViewModel.Tabs.Last());
            _mainViewModel.SelectedTab!.TabTitle.Should().Be($"Search {initialTabCount + 1}");
        }

        [Fact]
        public void RemoveTab_WithMultipleTabs_RemovesTabAndSelectsPrevious()
        {
            // Arrange
            _mainViewModel.AddTab();
            var tabToRemove = _mainViewModel.SelectedTab!;
            var previousTab = _mainViewModel.Tabs.First();

            // Act
            _mainViewModel.RemoveTab(tabToRemove);

            // Assert
            _mainViewModel.Tabs.Should().NotContain(tabToRemove);
            _mainViewModel.SelectedTab.Should().Be(previousTab);
        }

        [Fact]
        public void RemoveTab_WithSingleTab_DoesNotRemoveTab()
        {
            // Arrange
            var initialTabCount = _mainViewModel.Tabs.Count;
            var singleTab = _mainViewModel.Tabs.First();

            // Act
            _mainViewModel.RemoveTab(singleTab);

            // Assert
            _mainViewModel.Tabs.Should().HaveCount(initialTabCount);
            _mainViewModel.Tabs.Should().Contain(singleTab);
            _mainViewModel.SelectedTab.Should().Be(singleTab);
        }

        [Fact]
        public void RemoveTab_WithSelectedTab_RemovesTabAndSelectsAnother()
        {
            // Arrange
            _mainViewModel.AddTab();
            var tabToRemove = _mainViewModel.SelectedTab!;

            // Act
            _mainViewModel.RemoveTab(tabToRemove);

            // Assert
            _mainViewModel.Tabs.Should().NotContain(tabToRemove);
            _mainViewModel.SelectedTab.Should().NotBeNull();
            _mainViewModel.Tabs.Should().Contain(_mainViewModel.SelectedTab!);
        }

        [Fact]
        public void RemoveTab_WithFirstTab_RemovesTabAndSelectsNext()
        {
            // Arrange
            _mainViewModel.AddTab();
            var firstTab = _mainViewModel.Tabs.First();
            _mainViewModel.SelectedTab = _mainViewModel.Tabs.Last();

            // Act
            _mainViewModel.RemoveTab(firstTab);

            // Assert
            _mainViewModel.Tabs.Should().NotContain(firstTab);
            _mainViewModel.SelectedTab.Should().NotBeNull();
            _mainViewModel.Tabs.Should().Contain(_mainViewModel.SelectedTab);
        }

        [Fact]
        public void CanRemoveTab_WithMultipleTabs_ReturnsTrue()
        {
            // Arrange
            _mainViewModel.AddTab();

            // Act & Assert
            _mainViewModel.CanRemoveTab.Should().BeTrue();
        }

        [Fact]
        public void CanRemoveTab_WithSingleTab_ReturnsFalse()
        {
            // Act & Assert
            _mainViewModel.CanRemoveTab.Should().BeFalse();
        }

        [Fact]
        public void SelectedTab_WhenChanged_RaisesPropertyChangedEvent()
        {
            // Arrange
            var propertyChangedRaised = false;
            _mainViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.SelectedTab))
                    propertyChangedRaised = true;
            };

            _mainViewModel.AddTab();
            var newTab = _mainViewModel.Tabs.Last();

            // Act
            _mainViewModel.SelectedTab = newTab;

            // Assert
            propertyChangedRaised.Should().BeTrue();
        }

        [Fact]
        public void Tabs_CollectionIsObservableCollection()
        {
            // Act & Assert
            _mainViewModel.Tabs.Should().BeOfType<ObservableCollection<TabViewModel>>();
        }

        [Fact]
        public void Constructor_WithInjectedSearchService_UsesProvidedInstance()
        {
            var customSearchService = new Mock<ISearchService>();
            var customViewModel = new MainViewModel(customSearchService.Object);

            customViewModel.Tabs.Should().HaveCount(1);
            customViewModel.SelectedTab.Should().NotBeNull();
        }

        [Fact]
        public void AddTab_MultipleTimes_CreatesTabsWithCorrectTitles()
        {
            // Arrange
            var initialTabCount = _mainViewModel.Tabs.Count;

            // Act
            _mainViewModel.AddTab();
            _mainViewModel.AddTab();
            _mainViewModel.AddTab();

            // Assert
            _mainViewModel.Tabs.Should().HaveCount(initialTabCount + 3);
            _mainViewModel.Tabs[initialTabCount].TabTitle.Should().Be($"Search {initialTabCount + 1}");
            _mainViewModel.Tabs[initialTabCount + 1].TabTitle.Should().Be($"Search {initialTabCount + 2}");
            _mainViewModel.Tabs[initialTabCount + 2].TabTitle.Should().Be($"Search {initialTabCount + 3}");
        }

        [Fact]
        public void RemoveTab_WithNullTab_DoesNotThrow()
        {
            // Act & Assert
            Action act = () => _mainViewModel.RemoveTab(null!);
            act.Should().NotThrow();
        }

        [Fact]
        public void RemoveTab_WithNonExistentTab_DoesNotThrow()
        {
            // Arrange
            using var nonExistentTab = new TabViewModel(_mockSearchService.Object);

            // Act & Assert
            Action act = () => _mainViewModel.RemoveTab(nonExistentTab);
            act.Should().NotThrow();
        }

        [Fact]
        public void PropertyChanged_WhenTabsCollectionChanged_RaisesPropertyChangedEvent()
        {
            // Arrange
            var propertyChangedRaised = false;
            
            _mainViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == "Tabs")
                {
                    propertyChangedRaised = true;
                }
            };

            // Act
            _mainViewModel.AddTab();

            // Assert
            propertyChangedRaised.Should().BeTrue();
        }

        [Fact]
        public void PropertyChanged_WhenCanRemoveTabChanged_RaisesPropertyChangedEvent()
        {
            // Arrange
            var propertyChangedRaised = false;
            
            _mainViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == "CanRemoveTab")
                {
                    propertyChangedRaised = true;
                }
            };

            // Act
            _mainViewModel.AddTab(); // This should change CanRemoveTab from false to true

            // Assert
            propertyChangedRaised.Should().BeTrue();
        }

        [Fact]
        public void AddTab_WithMultipleTabs_MaintainsCorrectTabTitles()
        {
            // Arrange
            var initialCount = _mainViewModel.Tabs.Count;

            // Act
            _mainViewModel.AddTab();
            _mainViewModel.AddTab();
            _mainViewModel.AddTab();

            // Assert
            _mainViewModel.Tabs.Should().HaveCount(initialCount + 3);
            _mainViewModel.Tabs[initialCount].TabTitle.Should().Be($"Search {initialCount + 1}");
            _mainViewModel.Tabs[initialCount + 1].TabTitle.Should().Be($"Search {initialCount + 2}");
            _mainViewModel.Tabs[initialCount + 2].TabTitle.Should().Be($"Search {initialCount + 3}");
        }

        [Fact]
        public void RemoveTab_WithNullReference_DoesNotThrow()
        {
            // Arrange
            var initialTabCount = _mainViewModel.Tabs.Count;
            var initialSelectedTab = _mainViewModel.SelectedTab;

            // Act & Assert
            Action act = () => _mainViewModel.RemoveTab(null!);
            act.Should().NotThrow();
            
            // Verify state hasn't changed
            _mainViewModel.Tabs.Should().HaveCount(initialTabCount);
            _mainViewModel.SelectedTab.Should().Be(initialSelectedTab);
        }

        [Fact]
        public void SelectedTab_WithSameValue_DoesNotRaisePropertyChangedEvent()
        {
            // Arrange
            var propertyChangedRaised = false;
            var initialTab = _mainViewModel.SelectedTab;
            
            _mainViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == "SelectedTab")
                {
                    propertyChangedRaised = true;
                }
            };

            // Act
            _mainViewModel.SelectedTab = initialTab; // Set to same value

            // Assert
            propertyChangedRaised.Should().BeFalse();
        }

        [Fact]
        public void Tabs_WhenModifiedDirectly_DoesNotRaisePropertyChangedEvent()
        {
            // Arrange
            var propertyChangedRaised = false;
            
            _mainViewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == "Tabs")
                {
                    propertyChangedRaised = true;
                }
            };

            // Act
            // Note: ObservableCollection doesn't raise PropertyChanged when collection is modified directly
            // This test verifies the behavior
            _mainViewModel.Tabs.Add(new TabViewModel(_mockSearchService.Object));

            // Assert
            // The event should not be raised when modifying the collection directly
            // (Only when the Tabs property itself is reassigned)
            propertyChangedRaised.Should().BeFalse();
        }
    }
}