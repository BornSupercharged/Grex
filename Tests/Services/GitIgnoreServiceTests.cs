using System;
using System.IO;
using FluentAssertions;
using Grex.Services;
using Xunit;

namespace Grex.Tests.Services
{
    public class GitIgnoreServiceTests
    {
        private readonly GitIgnoreService _gitIgnoreService;

        public GitIgnoreServiceTests()
        {
            _gitIgnoreService = new GitIgnoreService();
        }

        [Fact]
        public void ShouldIgnoreFile_WithNoGitIgnoreFile_ReturnsFalse()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var testFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "content");

            try
            {
                // Act
                var result = _gitIgnoreService.ShouldIgnoreFile(testFile, testDirectory);

                // Assert
                result.Should().BeFalse();
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithGitIgnoreFile_ReturnsExpectedResult()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = Path.Combine(testDirectory, ".gitignore");
            var testFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "content");
            var ignoredFile = TestDataHelper.CreateTestFile(testDirectory, "ignored.log", "content");

            try
            {
                // Create .gitignore file
                File.WriteAllText(gitIgnoreFile, "*.log\nbuild/\n.DS_Store");

                // Act
                var testFileResult = _gitIgnoreService.ShouldIgnoreFile(testFile, testDirectory);
                var ignoredFileResult = _gitIgnoreService.ShouldIgnoreFile(ignoredFile, testDirectory);

                // Assert
                testFileResult.Should().BeFalse();
                ignoredFileResult.Should().BeTrue();
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithNegationPattern_ReturnsExpectedResult()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = Path.Combine(testDirectory, ".gitignore");
            var ignoredFile = TestDataHelper.CreateTestFile(testDirectory, "test.log", "content");
            var exceptionFile = TestDataHelper.CreateTestFile(testDirectory, "important.log", "content");

            try
            {
                // Create .gitignore file with negation
                File.WriteAllText(gitIgnoreFile, "*.log\n!important.log");

                // Act
                var ignoredFileResult = _gitIgnoreService.ShouldIgnoreFile(ignoredFile, testDirectory);
                var exceptionFileResult = _gitIgnoreService.ShouldIgnoreFile(exceptionFile, testDirectory);

                // Assert
                ignoredFileResult.Should().BeTrue();
                exceptionFileResult.Should().BeFalse();
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithDirectoryPattern_ReturnsExpectedResult()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = Path.Combine(testDirectory, ".gitignore");
            var buildDir = Directory.CreateDirectory(Path.Combine(testDirectory, "build"));
            var fileInBuildDir = TestDataHelper.CreateTestFile(buildDir.FullName, "output.txt", "content");
            var normalFile = TestDataHelper.CreateTestFile(testDirectory, "normal.txt", "content");

            try
            {
                // Create .gitignore file with directory pattern
                File.WriteAllText(gitIgnoreFile, "build/");

                // Act
                var fileInBuildDirResult = _gitIgnoreService.ShouldIgnoreFile(fileInBuildDir, testDirectory);
                var normalFileResult = _gitIgnoreService.ShouldIgnoreFile(normalFile, testDirectory);

                // Assert
                fileInBuildDirResult.Should().BeTrue();
                normalFileResult.Should().BeFalse();
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithNestedGitIgnore_ReturnsExpectedResult()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = Path.Combine(testDirectory, ".gitignore");
            var subDir = Directory.CreateDirectory(Path.Combine(testDirectory, "subdir"));
            var subGitIgnoreFile = Path.Combine(subDir.FullName, ".gitignore");
            var normalFile = TestDataHelper.CreateTestFile(testDirectory, "normal.txt", "content");
            var subDirFile = TestDataHelper.CreateTestFile(subDir.FullName, "subdir.txt", "content");

            try
            {
                // Create .gitignore files
                File.WriteAllText(gitIgnoreFile, "*.txt");
                File.WriteAllText(subGitIgnoreFile, "!subdir.txt");

                // Act
                var normalFileResult = _gitIgnoreService.ShouldIgnoreFile(normalFile, testDirectory);
                var subDirFileResult = _gitIgnoreService.ShouldIgnoreFile(subDirFile, testDirectory);

                // Assert
                normalFileResult.Should().BeTrue();
                subDirFileResult.Should().BeFalse();
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithEmptyOrNullParameters_ReturnsFalse()
        {
            // Act & Assert
            _gitIgnoreService.ShouldIgnoreFile("", "C:\\test").Should().BeFalse();
            _gitIgnoreService.ShouldIgnoreFile("C:\\test\\file.txt", "").Should().BeFalse();
            _gitIgnoreService.ShouldIgnoreFile(null!, "C:\\test").Should().BeFalse();
            _gitIgnoreService.ShouldIgnoreFile("C:\\test\\file.txt", null!).Should().BeFalse();
        }

        [Fact]
        public void ShouldIgnoreFile_WithComplexWildcardPatterns_ReturnsExpectedResult()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = Path.Combine(testDirectory, ".gitignore");
            var testFile1 = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "content");
            var testFile2 = TestDataHelper.CreateTestFile(testDirectory, "test123.txt", "content");
            var testFile3 = TestDataHelper.CreateTestFile(testDirectory, "test_backup.txt", "content");

            try
            {
                // Create .gitignore file with complex patterns
                File.WriteAllText(gitIgnoreFile, "test*.txt\n!test_backup.txt\n*.tmp\n*.bak");

                // Act
                var result1 = _gitIgnoreService.ShouldIgnoreFile(testFile1, testDirectory);
                var result2 = _gitIgnoreService.ShouldIgnoreFile(testFile2, testDirectory);
                var result3 = _gitIgnoreService.ShouldIgnoreFile(testFile3, testDirectory);

                // Assert
                result1.Should().BeTrue(); // test.txt matches test*.txt
                result2.Should().BeTrue(); // test123.txt matches test*.txt
                result3.Should().BeFalse(); // test_backup.txt is negated
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithDoubleAsteriskPattern_ReturnsExpectedResult()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = Path.Combine(testDirectory, ".gitignore");
            var subDir = Directory.CreateDirectory(Path.Combine(testDirectory, "subdir"));
            var testFile1 = TestDataHelper.CreateTestFile(subDir.FullName, "test.txt", "content");
            var testFile2 = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "content");

            try
            {
                // Create .gitignore file with ** pattern
                File.WriteAllText(gitIgnoreFile, "**/test.txt");

                // Act
                var result1 = _gitIgnoreService.ShouldIgnoreFile(testFile1, testDirectory);
                var result2 = _gitIgnoreService.ShouldIgnoreFile(testFile2, testDirectory);

                // Assert
                result1.Should().BeTrue(); // subdir/test.txt matches **/test.txt
                result2.Should().BeTrue(); // test.txt matches **/test.txt
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithQuestionMarkPattern_ReturnsExpectedResult()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = Path.Combine(testDirectory, ".gitignore");
            var testFile1 = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "content");
            var testFile2 = TestDataHelper.CreateTestFile(testDirectory, "test1.txt", "content");
            var testFile3 = TestDataHelper.CreateTestFile(testDirectory, "test12.txt", "content");

            try
            {
                // Create .gitignore file with ? pattern
                File.WriteAllText(gitIgnoreFile, "test?.txt");

                // Act
                var result1 = _gitIgnoreService.ShouldIgnoreFile(testFile1, testDirectory);
                var result2 = _gitIgnoreService.ShouldIgnoreFile(testFile2, testDirectory);
                var result3 = _gitIgnoreService.ShouldIgnoreFile(testFile3, testDirectory);

                // Assert
                result1.Should().BeFalse(); // test.txt doesn't match test?.txt
                result2.Should().BeTrue(); // test1.txt matches test?.txt
                result3.Should().BeFalse(); // test12.txt doesn't match test?.txt
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithBracketPatterns_ReturnsExpectedResult()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = Path.Combine(testDirectory, ".gitignore");
            var testFile1 = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "content");
            var testFile2 = TestDataHelper.CreateTestFile(testDirectory, "test1.txt", "content");
            var testFile3 = TestDataHelper.CreateTestFile(testDirectory, "test2.txt", "content");

            try
            {
                // Create .gitignore file with bracket pattern
                File.WriteAllText(gitIgnoreFile, "test[12].txt");

                // Act
                var result1 = _gitIgnoreService.ShouldIgnoreFile(testFile1, testDirectory);
                var result2 = _gitIgnoreService.ShouldIgnoreFile(testFile2, testDirectory);
                var result3 = _gitIgnoreService.ShouldIgnoreFile(testFile3, testDirectory);

                // Assert
                result1.Should().BeFalse(); // test.txt doesn't match test[12].txt
                result2.Should().BeTrue(); // test1.txt matches test[12].txt
                result3.Should().BeTrue(); // test2.txt matches test[12].txt
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithEmptyGitIgnoreFile_ReturnsFalse()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = Path.Combine(testDirectory, ".gitignore");
            var testFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "content");

            try
            {
                // Create empty .gitignore file
                File.WriteAllText(gitIgnoreFile, "");

                // Act
                var result = _gitIgnoreService.ShouldIgnoreFile(testFile, testDirectory);

                // Assert
                result.Should().BeFalse();
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithCommentsOnlyInGitIgnoreFile_ReturnsFalse()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = Path.Combine(testDirectory, ".gitignore");
            var testFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "content");

            try
            {
                // Create .gitignore file with only comments
                File.WriteAllText(gitIgnoreFile, "# This is a comment\n# Another comment\n   # Indented comment");

                // Act
                var result = _gitIgnoreService.ShouldIgnoreFile(testFile, testDirectory);

                // Assert
                result.Should().BeFalse();
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithMalformedPatterns_HandlesGracefully()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = Path.Combine(testDirectory, ".gitignore");
            var testFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "content");

            try
            {
                // Create .gitignore file with malformed patterns
                File.WriteAllText(gitIgnoreFile, "[invalid\n*.txt\n!valid.txt");

                // Act & Assert - Should not throw even with malformed patterns
                Action act = () => _gitIgnoreService.ShouldIgnoreFile(testFile, testDirectory);
                act.Should().NotThrow();
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithCachingBehavior_ReturnsConsistentResults()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = Path.Combine(testDirectory, ".gitignore");
            var testFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "content");

            try
            {
                // Create .gitignore file
                File.WriteAllText(gitIgnoreFile, "*.txt");

                // Act - Call multiple times to test caching
                var result1 = _gitIgnoreService.ShouldIgnoreFile(testFile, testDirectory);
                var result2 = _gitIgnoreService.ShouldIgnoreFile(testFile, testDirectory);
                var result3 = _gitIgnoreService.ShouldIgnoreFile(testFile, testDirectory);

                // Assert
                result1.Should().Be(result2);
                result2.Should().Be(result3);
                result1.Should().BeTrue();
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithAbsolutePath_ReturnsFalse()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var testFile = TestDataHelper.CreateTestFile(testDirectory, "test.txt", "content");
            var externalFile = @"C:\Different\Path\test.txt";

            try
            {
                // Create .gitignore file
                TestDataHelper.CreateGitIgnoreFile(testDirectory, "*.txt");

                // Act
                var result1 = _gitIgnoreService.ShouldIgnoreFile(testFile, testDirectory);
                var result2 = _gitIgnoreService.ShouldIgnoreFile(externalFile, testDirectory);

                // Assert
                result1.Should().BeTrue(); // File in test directory should be ignored
                result2.Should().BeFalse(); // External file should not be ignored
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithRootRelativePattern_OnlyMatchesFromRoot()
        {
            // Arrange - This test verifies the fix for the bug where /storage/app was incorrectly matching /app
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = Path.Combine(testDirectory, ".gitignore");
            var storageAppDir = Directory.CreateDirectory(Path.Combine(testDirectory, "storage", "app"));
            var appDir = Directory.CreateDirectory(Path.Combine(testDirectory, "app"));
            // Note: /storage/app without trailing slash matches the directory itself, not files inside
            // So we test with a file directly at storage/app (if it were a file) or use /storage/app/ for directory
            var storageAppFile = TestDataHelper.CreateTestFile(storageAppDir.FullName, "file.txt", "content");
            var appFile = TestDataHelper.CreateTestFile(appDir.FullName, "file.txt", "content");

            try
            {
                // Create .gitignore file with root-relative directory pattern (with trailing slash)
                File.WriteAllText(gitIgnoreFile, "/storage/app/");

                // Act
                var storageAppResult = _gitIgnoreService.ShouldIgnoreFile(storageAppFile, testDirectory);
                var appResult = _gitIgnoreService.ShouldIgnoreFile(appFile, testDirectory);

                // Assert
                storageAppResult.Should().BeTrue(); // storage/app/file.txt should be ignored
                appResult.Should().BeFalse(); // app/file.txt should NOT be ignored (this was the bug)
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithRootRelativeDirectoryPattern_MatchesFilesInsideDirectory()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = Path.Combine(testDirectory, ".gitignore");
            var storageAppDir = Directory.CreateDirectory(Path.Combine(testDirectory, "storage", "app"));
            var storageAppSubDir = Directory.CreateDirectory(Path.Combine(storageAppDir.FullName, "subdir"));
            var storageAppFile = TestDataHelper.CreateTestFile(storageAppDir.FullName, "file.txt", "content");
            var storageAppSubFile = TestDataHelper.CreateTestFile(storageAppSubDir.FullName, "file.txt", "content");

            try
            {
                // Create .gitignore file with root-relative directory pattern
                File.WriteAllText(gitIgnoreFile, "/storage/app/");

                // Act
                var storageAppFileResult = _gitIgnoreService.ShouldIgnoreFile(storageAppFile, testDirectory);
                var storageAppSubFileResult = _gitIgnoreService.ShouldIgnoreFile(storageAppSubFile, testDirectory);

                // Assert
                storageAppFileResult.Should().BeTrue(); // Files in storage/app should be ignored
                storageAppSubFileResult.Should().BeTrue(); // Files in subdirectories should also be ignored
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithRootRelativePattern_DoesNotMatchSegmentInPath()
        {
            // Arrange - This test specifically verifies that root-relative patterns don't match path segments
            // This was the original bug: /storage/app was incorrectly matching /app
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = Path.Combine(testDirectory, ".gitignore");
            var appHttpDir = Directory.CreateDirectory(Path.Combine(testDirectory, "app", "Http"));
            var appHttpMiddlewareDir = Directory.CreateDirectory(Path.Combine(appHttpDir.FullName, "Middleware"));
            var middlewareFile = TestDataHelper.CreateTestFile(appHttpMiddlewareDir.FullName, "ApiKeyValidation.php", "content");

            try
            {
                // Create .gitignore file with root-relative pattern that should NOT match /app
                File.WriteAllText(gitIgnoreFile, "/storage/app");

                // Act
                var result = _gitIgnoreService.ShouldIgnoreFile(middlewareFile, testDirectory);

                // Assert
                result.Should().BeFalse(); // app/Http/Middleware/ApiKeyValidation.php should NOT be ignored
                // This verifies the fix: /storage/app should not match the "app" segment in the path
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }

        [Fact]
        public void ShouldIgnoreFile_WithRootRelativePatternAndNonRootRelativePattern_HandlesBothCorrectly()
        {
            // Arrange
            var testDirectory = TestDataHelper.CreateTestDirectory();
            var gitIgnoreFile = Path.Combine(testDirectory, ".gitignore");
            var storageAppDir = Directory.CreateDirectory(Path.Combine(testDirectory, "storage", "app"));
            var appDir = Directory.CreateDirectory(Path.Combine(testDirectory, "app"));
            var storageAppFile = TestDataHelper.CreateTestFile(storageAppDir.FullName, "file.txt", "content");
            var appFile = TestDataHelper.CreateTestFile(appDir.FullName, "file.txt", "content");
            var otherAppDir = Directory.CreateDirectory(Path.Combine(testDirectory, "other", "app"));
            var otherAppFile = TestDataHelper.CreateTestFile(otherAppDir.FullName, "file.txt", "content");

            try
            {
                // Create .gitignore file with both root-relative and non-root-relative patterns
                File.WriteAllText(gitIgnoreFile, "/storage/app\napp/");

                // Act
                var storageAppResult = _gitIgnoreService.ShouldIgnoreFile(storageAppFile, testDirectory);
                var appResult = _gitIgnoreService.ShouldIgnoreFile(appFile, testDirectory);
                var otherAppResult = _gitIgnoreService.ShouldIgnoreFile(otherAppFile, testDirectory);

                // Assert
                storageAppResult.Should().BeTrue(); // storage/app should be ignored (root-relative)
                appResult.Should().BeTrue(); // app should be ignored (non-root-relative pattern)
                otherAppResult.Should().BeTrue(); // other/app should be ignored (non-root-relative pattern)
            }
            finally
            {
                // Cleanup
                TestDataHelper.CleanupTestDirectory(testDirectory);
            }
        }
    }
}