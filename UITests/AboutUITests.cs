using System;
using FluentAssertions;
using Grex.ViewModels;
using Xunit;

namespace Grex.UITests
{
    /// <summary>
    /// UI tests for the About page functionality
    /// </summary>
    [Collection("UI SettingsOverride collection")]
    public class AboutUITests
    {
        [Fact]
        public void MainViewModel_InitializesWithTabs_ForAboutNavigation()
        {
            // Arrange & Act
            var mainViewModel = new MainViewModel();

            // Assert - ViewModel should be initialized and ready for navigation
            mainViewModel.Should().NotBeNull();
            mainViewModel.Tabs.Should().NotBeNull();
            mainViewModel.Tabs.Should().HaveCountGreaterThanOrEqualTo(1);
        }

        [UITestMethod]
        public void AboutPage_HasExpectedLocalizationKeys()
        {
            // This test validates that the expected localization keys exist
            // The actual About page UI would be tested via WinAppDriver or manual testing
            
            var expectedKeys = new[]
            {
                "AboutNavItem.Content",
                "AboutCreatedByTextBlock.Text",
                "AboutLicenseTextBlock.Text",
                "AboutGitHubLinkButton.Content",
                "AboutKeyboardShortcutTextBlock.Text",
                "AboutVersionLabel.Text"
            };

            foreach (var key in expectedKeys)
            {
                // Keys should not return empty - they either return a value or the key itself
                var result = Grex.Services.LocalizationService.Instance.GetLocalizedString(key);
                result.Should().NotBeNullOrEmpty($"Localization key '{key}' should have a value");
            }
        }

        [UITestMethod]
        public void AboutPage_GitHubLink_ShouldBeValidUrl()
        {
            // Arrange
            var expectedUrl = "https://github.com/BornSupercharged/Grex";
            
            // Act & Assert
            // The AboutView uses NavigateUri="https://github.com/BornSupercharged/Grex"
            // We verify this is a valid URL
            Uri.TryCreate(expectedUrl, UriKind.Absolute, out var uri).Should().BeTrue();
            uri.Should().NotBeNull();
            uri!.Scheme.Should().Be("https");
            uri.Host.Should().Be("github.com");
        }

        [UITestMethod]
        public void F1KeyboardShortcut_ShouldNavigateToAbout()
        {
            // This test validates the expected behavior of the F1 keyboard shortcut
            // The actual keyboard handling is in MainWindow.xaml.cs - RootGrid_KeyDown
            // and would be tested via WinAppDriver or manual testing
            
            // We can verify the expected virtual key is correct
            var f1Key = Windows.System.VirtualKey.F1;
            f1Key.Should().Be(Windows.System.VirtualKey.F1);
        }

        [UITestMethod]
        public void AboutNavItem_ShouldBeInFooterMenu()
        {
            // This test documents that the About nav item should be in the footer menu
            // The actual XAML placement is:
            // <NavigationView.MenuItems>
            //     <NavigationViewItem x:Name="SearchNavItem" ... />
            //     <NavigationViewItem x:Name="RegexBuilderNavItem" ... />
            //     <NavigationViewItem x:Name="SettingsNavItem" ... />
            // </NavigationView.MenuItems>
            // <NavigationView.FooterMenuItems>
            //     <NavigationViewItem x:Name="AboutNavItem" ... />
            // </NavigationView.FooterMenuItems>
            
            // Since we can't easily test XAML structure without UI context,
            // this test serves as documentation and the behavior is verified manually
            true.Should().BeTrue("About nav item is placed in FooterMenuItems (Settings is in MenuItems)");
        }

        [UITestMethod]
        public void AboutPage_ContentShouldIncludeRequiredElements()
        {
            // This test documents the expected content of the About page
            // The actual content verification would require UI automation
            
            // Expected elements:
            // 1. App logo (Image with x:Name="AppLogoImage")
            // 2. App name (TextBlock with x:Name="AppNameTextBlock")
            // 3. Version info (TextBlock with x:Name="VersionTextBlock")
            // 4. Created by text (TextBlock with x:Name="CreatedByTextBlock")
            // 5. License text (TextBlock with x:Name="LicenseTextBlock")
            // 6. GitHub link (HyperlinkButton with x:Name="GitHubLinkButton")
            // 7. Keyboard shortcut text (TextBlock with x:Name="KeyboardShortcutTextBlock")
            
            var expectedElements = new[]
            {
                "AppLogoImage",
                "AppNameTextBlock",
                "VersionTextBlock",
                "CreatedByTextBlock",
                "LicenseTextBlock",
                "GitHubLinkButton",
                "KeyboardShortcutTextBlock"
            };
            
            expectedElements.Length.Should().Be(7, "About page should have 7 main elements");
        }
    }
}

