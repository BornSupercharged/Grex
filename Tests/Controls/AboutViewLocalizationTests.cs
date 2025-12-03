using System;
using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace Grex.Tests.Controls
{
    /// <summary>
    /// Tests for About page localization keys
    /// </summary>
    public class AboutViewLocalizationTests
    {
        private readonly string _reswPath;

        public AboutViewLocalizationTests()
        {
            // Find the Resources.resw file
            var baseDir = AppContext.BaseDirectory;
            var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
            _reswPath = Path.Combine(projectRoot, "Strings", "en-US", "Resources.resw");
        }

        [Fact]
        public void Resources_ShouldContain_AboutNavItemContent()
        {
            // Arrange & Act
            var hasKey = CheckResourceKeyExists("AboutNavItem.Content");

            // Assert
            hasKey.Should().BeTrue("AboutNavItem.Content should exist in Resources.resw");
        }

        [Fact]
        public void Resources_ShouldContain_AboutCreatedByText()
        {
            // Arrange & Act
            var hasKey = CheckResourceKeyExists("AboutCreatedByTextBlock.Text");

            // Assert
            hasKey.Should().BeTrue("AboutCreatedByTextBlock.Text should exist in Resources.resw");
        }

        [Fact]
        public void Resources_ShouldContain_AboutLicenseText()
        {
            // Arrange & Act
            var hasKey = CheckResourceKeyExists("AboutLicenseTextBlock.Text");

            // Assert
            hasKey.Should().BeTrue("AboutLicenseTextBlock.Text should exist in Resources.resw");
        }

        [Fact]
        public void Resources_ShouldContain_AboutGitHubLinkContent()
        {
            // Arrange & Act
            var hasKey = CheckResourceKeyExists("AboutGitHubLinkButton.Content");

            // Assert
            hasKey.Should().BeTrue("AboutGitHubLinkButton.Content should exist in Resources.resw");
        }

        [Fact]
        public void Resources_ShouldContain_AboutKeyboardShortcutText()
        {
            // Arrange & Act
            var hasKey = CheckResourceKeyExists("AboutKeyboardShortcutTextBlock.Text");

            // Assert
            hasKey.Should().BeTrue("AboutKeyboardShortcutTextBlock.Text should exist in Resources.resw");
        }

        [Fact]
        public void AboutCreatedByText_ShouldContain_BornSupercharged()
        {
            // Arrange & Act
            var value = GetResourceValue("AboutCreatedByTextBlock.Text");

            // Assert
            value.Should().NotBeNull();
            value.Should().Contain("BornSupercharged", "Created by text should mention BornSupercharged");
        }

        [Fact]
        public void AboutLicenseText_ShouldContain_GPL()
        {
            // Arrange & Act
            var value = GetResourceValue("AboutLicenseTextBlock.Text");

            // Assert
            value.Should().NotBeNull();
            value.Should().Contain("GPL", "License text should mention GPL");
        }

        [Fact]
        public void AboutGitHubLink_ShouldContain_GitHub()
        {
            // Arrange & Act
            var value = GetResourceValue("AboutGitHubLinkButton.Content");

            // Assert
            value.Should().NotBeNull();
            value.Should().Contain("GitHub", "GitHub link should mention GitHub");
        }

        [Fact]
        public void AboutKeyboardShortcut_ShouldContain_F1()
        {
            // Arrange & Act
            var value = GetResourceValue("AboutKeyboardShortcutTextBlock.Text");

            // Assert
            value.Should().NotBeNull();
            value.Should().Contain("F1", "Keyboard shortcut text should mention F1");
        }

        [Fact]
        public void Resources_ShouldContain_AboutVersionLabel()
        {
            // Arrange & Act
            var hasKey = CheckResourceKeyExists("AboutVersionLabel.Text");

            // Assert
            hasKey.Should().BeTrue("AboutVersionLabel.Text should exist in Resources.resw");
        }

        [Fact]
        public void AboutVersionLabel_ShouldContain_Version()
        {
            // Arrange & Act
            var value = GetResourceValue("AboutVersionLabel.Text");

            // Assert
            value.Should().NotBeNull();
            value.Should().Contain("Version", "Version label should contain 'Version'");
        }

        private bool CheckResourceKeyExists(string key)
        {
            if (!File.Exists(_reswPath))
            {
                return false;
            }

            try
            {
                var doc = XDocument.Load(_reswPath);
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                
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

        private string? GetResourceValue(string key)
        {
            if (!File.Exists(_reswPath))
            {
                return null;
            }

            try
            {
                var doc = XDocument.Load(_reswPath);
                
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

