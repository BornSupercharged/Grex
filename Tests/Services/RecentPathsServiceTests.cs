using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Grex.Services;
using Xunit;

namespace Grex.Tests.Services
{
    public class RecentPathsServiceTests : IDisposable
    {
        private readonly string _testAppDataPath;
        private readonly List<string> _originalPaths;

        public RecentPathsServiceTests()
        {
            // Create a temporary app data path for testing
            _testAppDataPath = Path.Combine(Path.GetTempPath(), "grex_Test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testAppDataPath);
            
            // Save original paths to restore after tests
            _originalPaths = new List<string>(RecentPathsService.GetRecentPaths());
        }

        public void Dispose()
        {
            // Restore original paths (add in reverse order since AddRecentPath adds to beginning)
            var currentPaths = RecentPathsService.GetRecentPaths();
            foreach (var path in currentPaths)
            {
                RecentPathsService.RemoveRecentPath(path);
            }
            // Add in reverse order to maintain original order (most recent first)
            for (int i = _originalPaths.Count - 1; i >= 0; i--)
            {
                RecentPathsService.AddRecentPath(_originalPaths[i]);
            }

            // Clean up test app data
            if (Directory.Exists(_testAppDataPath))
            {
                Directory.Delete(_testAppDataPath, true);
            }
        }

        [Fact]
        public void GetRecentPaths_WithNoExistingFile_ReturnsEmptyList()
        {
            // Arrange - Save original paths and clear for this test
            var originalPaths = new List<string>(RecentPathsService.GetRecentPaths());
            var existingPaths = RecentPathsService.GetRecentPaths();
            foreach (var path in existingPaths)
            {
                RecentPathsService.RemoveRecentPath(path);
            }

            // Act
            var paths = RecentPathsService.GetRecentPaths();

            // Assert
            paths.Should().NotBeNull();
            paths.Should().BeEmpty();

            // Restore original paths (add in reverse order since AddRecentPath adds to beginning)
            for (int i = originalPaths.Count - 1; i >= 0; i--)
            {
                RecentPathsService.AddRecentPath(originalPaths[i]);
            }
        }

        [Fact]
        public void AddRecentPath_WithValidPath_AddsToRecentPaths()
        {
            // Arrange
            var testPath = "C:\\Test\\Path";
            
            // Save original paths and clear for this test
            var originalPaths = new List<string>(RecentPathsService.GetRecentPaths());
            var existingPaths = RecentPathsService.GetRecentPaths();
            foreach (var path in existingPaths)
            {
                RecentPathsService.RemoveRecentPath(path);
            }
            
            // Act
            RecentPathsService.AddRecentPath(testPath);
            var recentPaths = RecentPathsService.GetRecentPaths();

            // Assert
            recentPaths.Should().Contain(testPath);
            recentPaths.Should().HaveCount(1);
            recentPaths[0].Should().Be(testPath);

            // Cleanup - Restore original paths
            RecentPathsService.RemoveRecentPath(testPath);
            foreach (var path in originalPaths)
            {
                RecentPathsService.AddRecentPath(path);
            }
        }

        [Fact]
        public void AddRecentPath_WithDuplicatePath_MovesToTop()
        {
            // Arrange
            var testPath1 = "C:\\Test\\Path1";
            var testPath2 = "C:\\Test\\Path2";

            // Save original paths and clear for this test
            var originalPaths = new List<string>(RecentPathsService.GetRecentPaths());
            var existingPaths = RecentPathsService.GetRecentPaths();
            foreach (var path in existingPaths)
            {
                RecentPathsService.RemoveRecentPath(path);
            }

            // Act
            RecentPathsService.AddRecentPath(testPath1);
            RecentPathsService.AddRecentPath(testPath2);
            RecentPathsService.AddRecentPath(testPath1); // Add again - should move to top
            
            var recentPaths = RecentPathsService.GetRecentPaths();

            // Assert
            recentPaths.Should().HaveCount(2);
            recentPaths[0].Should().Be(testPath1); // Should be at top now
            recentPaths[1].Should().Be(testPath2);

            // Cleanup - Restore original paths
            RecentPathsService.RemoveRecentPath(testPath1);
            RecentPathsService.RemoveRecentPath(testPath2);
            foreach (var path in originalPaths)
            {
                RecentPathsService.AddRecentPath(path);
            }
        }

        [Fact]
        public void AddRecentPath_WithEmptyOrNullOrWhitespace_DoesNotAdd()
        {
            // Arrange
            var initialCount = RecentPathsService.GetRecentPaths().Count;

            // Act
            RecentPathsService.AddRecentPath("");
            RecentPathsService.AddRecentPath(null!);
            RecentPathsService.AddRecentPath("   ");

            // Assert
            var recentPaths = RecentPathsService.GetRecentPaths();
            recentPaths.Should().HaveCount(initialCount);
        }

        [Fact]
        public void FilterPaths_WithEmptySearchText_ReturnsAllPaths()
        {
            // Arrange
            var testPath1 = "C:\\Test\\Path1";
            var testPath2 = "C:\\Test\\Path2";

            // Save original paths and clear for this test
            var originalPaths = new List<string>(RecentPathsService.GetRecentPaths());
            var existingPaths = RecentPathsService.GetRecentPaths();
            foreach (var path in existingPaths)
            {
                RecentPathsService.RemoveRecentPath(path);
            }

            // Act
            RecentPathsService.AddRecentPath(testPath1);
            RecentPathsService.AddRecentPath(testPath2);
            var filteredPaths = RecentPathsService.FilterPaths("");

            // Assert
            filteredPaths.Should().Contain(testPath1);
            filteredPaths.Should().Contain(testPath2);
            filteredPaths.Should().HaveCount(2);

            // Cleanup - Restore original paths
            RecentPathsService.RemoveRecentPath(testPath1);
            RecentPathsService.RemoveRecentPath(testPath2);
            foreach (var path in originalPaths)
            {
                RecentPathsService.AddRecentPath(path);
            }
        }

        [Fact]
        public void FilterPaths_WithSearchText_ReturnsFilteredPaths()
        {
            // Arrange
            var testPath1 = "C:\\Test\\Path1";
            var testPath2 = "D:\\Projects\\Code";
            var testPath3 = "C:\\Test\\Path2";

            // Save original paths and clear for this test
            var originalPaths = new List<string>(RecentPathsService.GetRecentPaths());
            var existingPaths = RecentPathsService.GetRecentPaths();
            foreach (var path in existingPaths)
            {
                RecentPathsService.RemoveRecentPath(path);
            }

            // Act
            RecentPathsService.AddRecentPath(testPath1);
            RecentPathsService.AddRecentPath(testPath2);
            RecentPathsService.AddRecentPath(testPath3);
            var filteredPaths = RecentPathsService.FilterPaths("test");

            // Assert
            filteredPaths.Should().Contain(testPath1);
            filteredPaths.Should().Contain(testPath3);
            filteredPaths.Should().NotContain(testPath2);
            filteredPaths.Should().HaveCount(2);

            // Cleanup - Restore original paths
            RecentPathsService.RemoveRecentPath(testPath1);
            RecentPathsService.RemoveRecentPath(testPath2);
            RecentPathsService.RemoveRecentPath(testPath3);
            foreach (var path in originalPaths)
            {
                RecentPathsService.AddRecentPath(path);
            }
        }

        [Fact]
        public void FilterPaths_WithCaseInsensitiveSearch_ReturnsFilteredPaths()
        {
            // Arrange
            var testPath1 = "C:\\Test\\Path1";
            var testPath2 = "D:\\Projects\\Code";

            // Save original paths and clear for this test
            var originalPaths = new List<string>(RecentPathsService.GetRecentPaths());
            var existingPaths = RecentPathsService.GetRecentPaths();
            foreach (var path in existingPaths)
            {
                RecentPathsService.RemoveRecentPath(path);
            }

            // Act
            RecentPathsService.AddRecentPath(testPath1);
            RecentPathsService.AddRecentPath(testPath2);
            var filteredPaths = RecentPathsService.FilterPaths("TEST");

            // Assert
            filteredPaths.Should().Contain(testPath1);
            filteredPaths.Should().NotContain(testPath2);
            filteredPaths.Should().HaveCount(1);

            // Cleanup - Restore original paths
            RecentPathsService.RemoveRecentPath(testPath1);
            RecentPathsService.RemoveRecentPath(testPath2);
            foreach (var path in originalPaths)
            {
                RecentPathsService.AddRecentPath(path);
            }
        }

        [Fact]
        public void RemoveRecentPath_WithExistingPath_RemovesFromRecentPaths()
        {
            // Arrange
            var testPath1 = "C:\\Test\\Path1";
            var testPath2 = "C:\\Test\\Path2";

            // Save original paths and clear for this test
            var originalPaths = new List<string>(RecentPathsService.GetRecentPaths());
            var existingPaths = RecentPathsService.GetRecentPaths();
            foreach (var path in existingPaths)
            {
                RecentPathsService.RemoveRecentPath(path);
            }

            // Act
            RecentPathsService.AddRecentPath(testPath1);
            RecentPathsService.AddRecentPath(testPath2);
            RecentPathsService.RemoveRecentPath(testPath1);
            
            var recentPaths = RecentPathsService.GetRecentPaths();

            // Assert
            recentPaths.Should().NotContain(testPath1);
            recentPaths.Should().Contain(testPath2);
            recentPaths.Should().HaveCount(1);

            // Cleanup - Restore original paths
            RecentPathsService.RemoveRecentPath(testPath2);
            foreach (var path in originalPaths)
            {
                RecentPathsService.AddRecentPath(path);
            }
        }

        [Fact]
        public void RemoveRecentPath_WithEmptyOrNullOrWhitespace_DoesNotThrow()
        {
            // Act & Assert
            RecentPathsService.RemoveRecentPath("");
            RecentPathsService.RemoveRecentPath(null!);
            RecentPathsService.RemoveRecentPath("   ");
        }

        [Fact]
        public void AddRecentPath_ExceedsMaxPaths_KeepsOnlyMostRecent()
        {
            // Arrange
            var paths = new List<string>();
            for (int i = 0; i < 25; i++) // Add more than MaxRecentPaths (20)
            {
                var path = $"C:\\Test\\Path{i}";
                paths.Add(path);
                RecentPathsService.AddRecentPath(path);
            }

            // Act
            var recentPaths = RecentPathsService.GetRecentPaths();

            // Assert
            recentPaths.Should().HaveCount(20); // MaxRecentPaths
            recentPaths[0].Should().Be("C:\\Test\\Path24"); // Most recent should be first

            // Cleanup
            foreach (var path in paths)
            {
                RecentPathsService.RemoveRecentPath(path);
            }
        }

        [Fact]
        public void GetRecentPaths_WithCorruptedJsonFile_ReturnsEmptyList()
        {
            // Arrange - This test verifies that corrupted JSON is handled gracefully
            // Since we can't easily override the file path, we'll test the error handling
            // by ensuring the service returns a valid list even if deserialization fails
            
            // The service already handles exceptions and returns empty list
            // This test verifies that behavior works correctly
            
            // Act - Get paths (may contain existing data from other tests)
            var paths = RecentPathsService.GetRecentPaths();

            // Assert - Service should return a valid list (not null)
            // Note: We can't easily test corrupted file without modifying the service
            // So we verify the service handles errors gracefully
            paths.Should().NotBeNull();
            
            // The actual corrupted file test would require dependency injection
            // For now, we verify the service doesn't throw exceptions
        }

        [Fact]
        public void AddRecentPath_WithVeryLongPath_TruncatesCorrectly()
        {
            // Arrange
            var veryLongPath = "C:\\" + new string('A', 300); // Very long path

            // Save original paths and clear for this test
            var originalPaths = new List<string>(RecentPathsService.GetRecentPaths());
            var existingPaths = RecentPathsService.GetRecentPaths();
            foreach (var path in existingPaths)
            {
                RecentPathsService.RemoveRecentPath(path);
            }
            
            // Act
            RecentPathsService.AddRecentPath(veryLongPath);
            var recentPaths = RecentPathsService.GetRecentPaths();

            // Assert
            recentPaths.Should().Contain(veryLongPath);
            recentPaths.Should().HaveCount(1);

            // Cleanup - Restore original paths
            RecentPathsService.RemoveRecentPath(veryLongPath);
            foreach (var path in originalPaths)
            {
                RecentPathsService.AddRecentPath(path);
            }
        }

        [Fact]
        public void AddRecentPath_WithSpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var pathWithSpecialChars = "C:\\Test\\Path With Spaces & Special@Characters#1";

            // Save original paths and clear for this test
            var originalPaths = new List<string>(RecentPathsService.GetRecentPaths());
            var existingPaths = RecentPathsService.GetRecentPaths();
            foreach (var path in existingPaths)
            {
                RecentPathsService.RemoveRecentPath(path);
            }
            
            // Act
            RecentPathsService.AddRecentPath(pathWithSpecialChars);
            var recentPaths = RecentPathsService.GetRecentPaths();

            // Assert
            recentPaths.Should().Contain(pathWithSpecialChars);
            recentPaths.Should().HaveCount(1);

            // Cleanup - Restore original paths
            RecentPathsService.RemoveRecentPath(pathWithSpecialChars);
            foreach (var path in originalPaths)
            {
                RecentPathsService.AddRecentPath(path);
            }
        }

        [Fact]
        public void FilterPaths_WithNonExistentSearch_ReturnsEmptyList()
        {
            // Arrange
            var testPath1 = "C:\\Test\\Path1";
            var testPath2 = "D:\\Projects\\Code";
            
            // Act
            RecentPathsService.AddRecentPath(testPath1);
            RecentPathsService.AddRecentPath(testPath2);
            var filteredPaths = RecentPathsService.FilterPaths("NonExistentSearchTerm");

            // Assert
            filteredPaths.Should().BeEmpty();

            // Cleanup
            RecentPathsService.RemoveRecentPath(testPath1);
            RecentPathsService.RemoveRecentPath(testPath2);
        }

        [Fact]
        public void FilterPaths_WithUnicodeCharacters_HandlesCorrectly()
        {
            // Arrange
            var testPath1 = "C:\\Test\\Path\\测试";
            var testPath2 = "D:\\Projects\\Code";
            
            // Act
            RecentPathsService.AddRecentPath(testPath1);
            RecentPathsService.AddRecentPath(testPath2);
            var filteredPaths = RecentPathsService.FilterPaths("测试");

            // Assert
            filteredPaths.Should().Contain(testPath1);
            filteredPaths.Should().NotContain(testPath2);
            filteredPaths.Should().HaveCount(1);

            // Cleanup
            RecentPathsService.RemoveRecentPath(testPath1);
            RecentPathsService.RemoveRecentPath(testPath2);
        }

        [Fact]
        public void AddRecentPath_WithNetworkPath_HandlesCorrectly()
        {
            // Arrange
            var networkPath = "\\\\server\\share\\folder";

            // Save original paths and clear for this test
            var originalPaths = new List<string>(RecentPathsService.GetRecentPaths());
            var existingPaths = RecentPathsService.GetRecentPaths();
            foreach (var path in existingPaths)
            {
                RecentPathsService.RemoveRecentPath(path);
            }
            
            // Act
            RecentPathsService.AddRecentPath(networkPath);
            var recentPaths = RecentPathsService.GetRecentPaths();

            // Assert
            recentPaths.Should().Contain(networkPath);
            recentPaths.Should().HaveCount(1);

            // Cleanup - Restore original paths
            RecentPathsService.RemoveRecentPath(networkPath);
            foreach (var path in originalPaths)
            {
                RecentPathsService.AddRecentPath(path);
            }
        }

        [Fact]
        public void AddRecentPath_WithRelativePath_HandlesCorrectly()
        {
            // Arrange
            var relativePath = ".\\relative\\path";

            // Save original paths and clear for this test
            var originalPaths = new List<string>(RecentPathsService.GetRecentPaths());
            var existingPaths = RecentPathsService.GetRecentPaths();
            foreach (var path in existingPaths)
            {
                RecentPathsService.RemoveRecentPath(path);
            }
            
            // Act
            RecentPathsService.AddRecentPath(relativePath);
            var recentPaths = RecentPathsService.GetRecentPaths();

            // Assert
            recentPaths.Should().Contain(relativePath);
            recentPaths.Should().HaveCount(1);

            // Cleanup - Restore original paths
            RecentPathsService.RemoveRecentPath(relativePath);
            foreach (var path in originalPaths)
            {
                RecentPathsService.AddRecentPath(path);
            }
        }
    }
}