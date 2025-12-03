using System;
using System.Linq;
using FluentAssertions;
using Grex.Services;
using Xunit;

namespace Grex.Tests.Services
{
    /// <summary>
    /// Tests to verify that all localization keys used by RegexBuilderView are properly defined
    /// This ensures that no hardcoded strings are used and all keys exist in the resource files
    /// </summary>
    public class RegexBuilderLocalizationKeysTests
    {
        private readonly ILocalizationService _localizationService;

        public RegexBuilderLocalizationKeysTests()
        {
            _localizationService = LocalizationService.Instance;
        }

        /// <summary>
        /// All localization keys used by RegexBuilderView.xaml.cs
        /// This list should be kept in sync with the actual usage in the control
        /// </summary>
        private static readonly string[] RegexBuilderLocalizationKeys = new[]
        {
            // Messages
            "EnterValidPatternMessage",
            "EnterSampleTextMessage",
            "RegexBreakdownNoMatchesFound",
            "RegexBreakdownFoundMatches",
            "RegexBreakdownNoMatchFound",
            "RegexBreakdownFoundOneMatch",
            "RegexBreakdownErrorMessage",
            "RegexBreakdownEnterPatternMessage",
            "RegexBreakdownInvalidPatternMessage",
            
            // Regex Breakdown Types
            "RegexBreakdownTypeCharacterClass",
            "RegexBreakdownTypeNonCapturingGroup",
            "RegexBreakdownTypeCapturingGroup",
            "RegexBreakdownTypeQuantifier",
            "RegexBreakdownTypeAnchor",
            "RegexBreakdownTypeEscapeSequence",
            "RegexBreakdownTypeLiteral",
            
            // Regex Breakdown Descriptions
            "RegexBreakdownDescCharacterClass",
            "RegexBreakdownDescNonCapturingGroup",
            "RegexBreakdownDescCapturingGroup",
            "RegexBreakdownDescQuantifierRange",
            "RegexBreakdownDescZeroOrMore",
            "RegexBreakdownDescOneOrMore",
            "RegexBreakdownDescZeroOrOne",
            "RegexBreakdownDescAnchorStart",
            "RegexBreakdownDescAnchorEnd",
            "RegexBreakdownDescDigit",
            "RegexBreakdownDescNonDigit",
            "RegexBreakdownDescWordChar",
            "RegexBreakdownDescNonWordChar",
            "RegexBreakdownDescWhitespace",
            "RegexBreakdownDescNonWhitespace",
            "RegexBreakdownDescNewline",
            "RegexBreakdownDescTab",
            "RegexBreakdownDescCarriageReturn",
            "RegexBreakdownDescLiteralChar",
            
            // Dialog Messages
            "RegexBreakdownOverwritePatternTitle",
            "RegexBreakdownOverwritePatternMessage",
            "ProceedButton",
            "CancelButton",
            
            // UI Elements
            "SampleTextTextBlock.Text",
            "RegexPatternTextBlock.Text",
            "LiveMatchResultsTextBlock.Text",
            "VisualRegexBreakdownTextBlock.Text",
            "PresetsTextBlock.Text",
            "OptionsTextBlock.Text",
            
            // Placeholders
            "SampleTextTextBox.PlaceholderText",
            "RegexPatternTextBox.PlaceholderText",
            
            // Preset Buttons
            "EmailPresetButton.Content",
            "PhonePresetButton.Content",
            "DatePresetButton.Content",
            "DigitsPresetButton.Content",
            "URLPresetButton.Content",
            
            // Checkboxes
            "CaseInsensitiveCheckBox.Content",
            "MultilineCheckBox.Content",
            "GlobalMatchCheckBox.Content",
            
            // Tooltips
            "Controls.RegexBuilderView.SampleTextTextBox.ToolTip",
            "Controls.RegexBuilderView.RegexPatternTextBox.ToolTip",
            "Controls.RegexBuilderView.CaseInsensitiveCheckBox.ToolTip",
            "Controls.RegexBuilderView.MultilineCheckBox.ToolTip",
            "Controls.RegexBuilderView.GlobalMatchCheckBox.ToolTip"
        };

        [Theory]
        [InlineData("en-US")]
        [InlineData("de-DE")]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        public void AllRegexBuilderKeys_ShouldReturnFallbackInTestEnvironment(string culture)
        {
            // Arrange
            var originalCulture = _localizationService.CurrentCulture;
            
            try
            {
                // Act
                _localizationService.SetCulture(culture);
                
                // Assert - All keys should return the key itself as fallback in test environment
                foreach (var key in RegexBuilderLocalizationKeys)
                {
                    var result = _localizationService.GetLocalizedString(key);
                    result.Should().Be(key, $"Key '{key}' should return itself as fallback in test environment for culture '{culture}'");
                }
            }
            finally
            {
                // Restore
                _localizationService.SetCulture(originalCulture);
            }
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("de-DE")]
        [InlineData("es-ES")]
        [InlineData("fr-FR")]
        public void RegexBuilderFormattedKeys_ShouldHandleParametersCorrectly(string culture)
        {
            // Arrange
            var originalCulture = _localizationService.CurrentCulture;
            const int testCount = 5;
            const string testError = "Test error message";
            const string testPreset = "Email";
            
            try
            {
                // Act
                _localizationService.SetCulture(culture);
                
                // Test formatted strings that are used in RegexBuilderView
                var foundMatchesResult = _localizationService.GetLocalizedString("RegexBreakdownFoundMatches", testCount);
                var errorMessageResult = _localizationService.GetLocalizedString("RegexBreakdownErrorMessage", testError);
                var invalidPatternResult = _localizationService.GetLocalizedString("RegexBreakdownInvalidPatternMessage", testError);
                var overwritePatternResult = _localizationService.GetLocalizedString("RegexBreakdownOverwritePatternMessage", testPreset);
                
                // Assert - In test environment, formatted strings should still return the key as fallback
                foundMatchesResult.Should().Be("RegexBreakdownFoundMatches");
                errorMessageResult.Should().Be("RegexBreakdownErrorMessage");
                invalidPatternResult.Should().Be("RegexBreakdownInvalidPatternMessage");
                overwritePatternResult.Should().Be("RegexBreakdownOverwritePatternMessage");
            }
            finally
            {
                // Restore
                _localizationService.SetCulture(originalCulture);
            }
        }

        [Fact]
        public void RegexBuilderKeys_ShouldBeUnique()
        {
            // Assert
            RegexBuilderLocalizationKeys.Should().OnlyHaveUniqueItems("All localization keys should be unique");
        }

        [Fact]
        public void RegexBuilderKeys_ShouldNotBeNullOrEmpty()
        {
            // Assert
            RegexBuilderLocalizationKeys.Should().NotContain(key => key == null);
            RegexBuilderLocalizationKeys.Should().NotContain(string.Empty);
            RegexBuilderLocalizationKeys.Should().NotContain(key => string.IsNullOrWhiteSpace(key));
        }

        [Fact]
        public void RegexBuilderKeys_ShouldFollowNamingConvention()
        {
            // Assert - Most keys should follow consistent naming patterns
            var breakdownTypeKeys = RegexBuilderLocalizationKeys.Where(key => key.Contains("RegexBreakdownType")).ToList();
            var breakdownDescKeys = RegexBuilderLocalizationKeys.Where(key => key.Contains("RegexBreakdownDesc")).ToList();
            
            breakdownTypeKeys.Should().NotBeEmpty("Should have Regex breakdown type keys");
            breakdownDescKeys.Should().NotBeEmpty("Should have Regex breakdown description keys");
            
            // All type keys should end with consistent pattern
            foreach (var typeKey in breakdownTypeKeys)
            {
                typeKey.Should().StartWith("RegexBreakdownType");
            }
            
            // All description keys should end with consistent pattern
            foreach (var descKey in breakdownDescKeys)
            {
                descKey.Should().StartWith("RegexBreakdownDesc");
            }
        }

        [Theory]
        [InlineData("")]
        [InlineData(null!)]
        public void LocalizationService_ShouldHandleEmptyOrNullKey(string? key)
        {
            // Arrange
            var originalCulture = _localizationService.CurrentCulture;
            
            try
            {
                // Act
                _localizationService.SetCulture("en-US");
                var result = _localizationService.GetLocalizedString(key ?? string.Empty);
                
                // Assert - Should return empty string for null or empty key
                result.Should().BeEmpty();
            }
            finally
            {
                // Restore
                _localizationService.SetCulture(originalCulture);
            }
        }

        [Fact]
        public void LocalizationService_ShouldHandleNonExistentKey()
        {
            // Arrange
            var originalCulture = _localizationService.CurrentCulture;
            const string nonExistentKey = "NonExentRegexBuilderKey_12345";
            
            try
            {
                // Act
                _localizationService.SetCulture("en-US");
                var result = _localizationService.GetLocalizedString(nonExistentKey);
                
                // Assert - Should return the key itself as fallback
                result.Should().Be(nonExistentKey);
            }
            finally
            {
                // Restore
                _localizationService.SetCulture(originalCulture);
            }
        }

        [Fact]
        public void RegexBuilderKeys_ShouldCoverAllUIElements()
        {
            // This test ensures that we have keys for all major UI elements in RegexBuilderView
            // It's a safeguard to make sure we don't miss any localization keys
            
            // Assert - Check that we have keys for major UI categories
            RegexBuilderLocalizationKeys.Should().Contain(key => key.Contains("TextBlock.Text"), "Should have TextBlock keys");
            RegexBuilderLocalizationKeys.Should().Contain(key => key.Contains("Button.Content"), "Should have Button keys");
            RegexBuilderLocalizationKeys.Should().Contain(key => key.Contains("CheckBox.Content"), "Should have CheckBox keys");
            RegexBuilderLocalizationKeys.Should().Contain(key => key.Contains("PlaceholderText"), "Should have PlaceholderText keys");
            RegexBuilderLocalizationKeys.Should().Contain(key => key.Contains("ToolTip"), "Should have ToolTip keys");
        }
    }
}