using System;
using FluentAssertions;
using Grex.Controls;
using Xunit;

namespace Grex.Tests.Controls
{
    public class ExcludeDirsValidationTests
    {
        // Use reflection to access the private static method
        private static bool IsValidRegexPattern(string pattern)
        {
            var method = typeof(SearchTabContent).GetMethod(
                "IsValidRegexPattern",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (method == null)
                throw new InvalidOperationException("IsValidRegexPattern method not found");
            
            return (bool)method.Invoke(null, new object[] { pattern })!;
        }

        [Theory]
        [InlineData("^(**|resources)$", false)] // Nested quantifiers
        [InlineData("**", false)] // Nested quantifiers
        [InlineData("\\\\\\", false)] // Trailing unescaped backslash (3 backslashes = odd)
        [InlineData("AABB???", false)] // Nested quantifiers (???)
        [InlineData("AA(C(B)A", false)] // Missing group closure
        [InlineData("\\\\b[M]\\\\w+\\\\\\", false)] // Trailing unescaped backslash (3 backslashes = odd)
        [InlineData("^(test|resources)$", true)] // Valid Regex
        [InlineData("test,vendor", true)] // Comma-separated (not validated as Regex)
        [InlineData("", true)] // Empty string
        [InlineData("test", true)] // Simple text (not validated as Regex)
        public void IsValidRegexPattern_ShouldValidateCorrectly(string pattern, bool expected)
        {
            // Act
            var result = IsValidRegexPattern(pattern);

            // Assert
            result.Should().Be(expected, $"Pattern '{pattern}' should be {(expected ? "valid" : "invalid")}");
        }
    }
}

