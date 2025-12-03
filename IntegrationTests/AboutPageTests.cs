using System;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Grex.Services;
using Xunit;

namespace Grex.IntegrationTests
{
    /// <summary>
    /// Integration tests for the About page functionality
    /// </summary>
    [Collection("Integration SettingsOverride collection")]
    public class AboutPageTests
    {
        [Fact]
        public void AboutPage_LocalizationKeys_ExistInAllLanguages()
        {
            // Arrange
            var baseDir = AppContext.BaseDirectory;
            var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
            var stringsDir = Path.Combine(projectRoot, "Strings");
            
            var expectedKeys = new[]
            {
                "AboutNavItem.Content",
                "AboutCreatedByTextBlock.Text",
                "AboutLicenseTextBlock.Text",
                "AboutGitHubLinkButton.Content",
                "AboutKeyboardShortcutTextBlock.Text",
                "AboutVersionLabel.Text"
            };
            
            if (!Directory.Exists(stringsDir))
            {
                // Skip if Strings directory doesn't exist (e.g., in CI environment)
                return;
            }

            // Act & Assert
            var languageDirs = Directory.GetDirectories(stringsDir);
            
            foreach (var langDir in languageDirs)
            {
                var reswPath = Path.Combine(langDir, "Resources.resw");
                if (!File.Exists(reswPath))
                {
                    continue;
                }

                var langCode = Path.GetFileName(langDir);
                
                foreach (var key in expectedKeys)
                {
                    var hasKey = CheckResourceKeyExists(reswPath, key);
                    hasKey.Should().BeTrue($"Key '{key}' should exist in {langCode}/Resources.resw");
                }
            }
        }

        [Fact]
        public void AboutPage_EnglishLocalization_HasCorrectContent()
        {
            // Arrange
            var baseDir = AppContext.BaseDirectory;
            var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
            var reswPath = Path.Combine(projectRoot, "Strings", "en-US", "Resources.resw");
            
            if (!File.Exists(reswPath))
            {
                // Skip if file doesn't exist (e.g., in CI environment)
                return;
            }

            // Act & Assert
            var navContent = GetResourceValue(reswPath, "AboutNavItem.Content");
            navContent.Should().Be("About");
            
            var createdBy = GetResourceValue(reswPath, "AboutCreatedByTextBlock.Text");
            createdBy.Should().Be("Created by BornSupercharged");
            
            var license = GetResourceValue(reswPath, "AboutLicenseTextBlock.Text");
            license.Should().Be("Licensed under GPL 3.0");
            
            var github = GetResourceValue(reswPath, "AboutGitHubLinkButton.Content");
            github.Should().Be("View Project on GitHub");
            
            var shortcut = GetResourceValue(reswPath, "AboutKeyboardShortcutTextBlock.Text");
            shortcut.Should().Be("Press F1 anytime to open this page");
            
            var versionLabel = GetResourceValue(reswPath, "AboutVersionLabel.Text");
            versionLabel.Should().Be("Version");
        }

        [Fact]
        public void LocalizationService_AboutNavItem_ReturnsValidString()
        {
            // Arrange
            var locService = LocalizationService.Instance;
            
            // Act
            var result = locService.GetLocalizedString("AboutNavItem.Content");
            
            // Assert - Should return either the localized value or the key as fallback
            result.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void LocalizationService_AboutCreatedBy_ReturnsValidString()
        {
            // Arrange
            var locService = LocalizationService.Instance;
            
            // Act
            var result = locService.GetLocalizedString("AboutCreatedByTextBlock.Text");
            
            // Assert
            result.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void LocalizationService_AboutLicense_ReturnsValidString()
        {
            // Arrange
            var locService = LocalizationService.Instance;
            
            // Act
            var result = locService.GetLocalizedString("AboutLicenseTextBlock.Text");
            
            // Assert
            result.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void LocalizationService_AboutGitHub_ReturnsValidString()
        {
            // Arrange
            var locService = LocalizationService.Instance;
            
            // Act
            var result = locService.GetLocalizedString("AboutGitHubLinkButton.Content");
            
            // Assert
            result.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void LocalizationService_AboutKeyboardShortcut_ReturnsValidString()
        {
            // Arrange
            var locService = LocalizationService.Instance;
            
            // Act
            var result = locService.GetLocalizedString("AboutKeyboardShortcutTextBlock.Text");
            
            // Assert
            result.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void LocalizationService_AboutVersionLabel_ReturnsValidString()
        {
            // Arrange
            var locService = LocalizationService.Instance;
            
            // Act
            var result = locService.GetLocalizedString("AboutVersionLabel.Text");
            
            // Assert
            result.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void AboutPage_XamlFile_Exists()
        {
            // Arrange
            var baseDir = AppContext.BaseDirectory;
            var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
            var xamlPath = Path.Combine(projectRoot, "Controls", "AboutView.xaml");
            
            // Act & Assert
            File.Exists(xamlPath).Should().BeTrue("AboutView.xaml should exist in Controls folder");
        }

        [Fact]
        public void AboutPage_CodeBehindFile_Exists()
        {
            // Arrange
            var baseDir = AppContext.BaseDirectory;
            var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
            var csPath = Path.Combine(projectRoot, "Controls", "AboutView.xaml.cs");
            
            // Act & Assert
            File.Exists(csPath).Should().BeTrue("AboutView.xaml.cs should exist in Controls folder");
        }

        private static bool CheckResourceKeyExists(string reswPath, string key)
        {
            try
            {
                var doc = XDocument.Load(reswPath);
                
                foreach (var data in doc.Descendants("data"))
                {
                    var nameAttr = data.Attribute("name");
                    if (nameAttr != null && nameAttr.Value == key)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static string? GetResourceValue(string reswPath, string key)
        {
            try
            {
                var doc = XDocument.Load(reswPath);
                
                foreach (var data in doc.Descendants("data"))
                {
                    var nameAttr = data.Attribute("name");
                    if (nameAttr != null && nameAttr.Value == key)
                    {
                        var valueElement = data.Element("value");
                        return valueElement?.Value;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}

