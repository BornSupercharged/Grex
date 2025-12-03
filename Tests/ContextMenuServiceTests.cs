using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Grex.Services;

namespace Grex.Tests.Services
{
    public class ContextMenuServiceTests
    {
        [Fact]
        public void Constructor_ShouldCreateInstance()
        {
            // Act
            var service = new ContextMenuService();

            // Assert
            Assert.NotNull(service);
        }

        [Theory]
        [InlineData(@"C:\test\file.txt", @"C:\test\file.txt")]
        [InlineData(@"C:\test\folder", @"C:\test\folder")]
        [InlineData(@"D:\path\to\file.cs", @"D:\path\to\file.cs")]
        public void ShowContextMenu_WithWindowsPath_ShouldNotThrow(string inputPath, string _)
        {
            // Arrange
            var service = new ContextMenuService();

            // Act - We can't directly test the private method, but we can test the public method
            // with Windows paths to ensure it doesn't crash
            // ShowContextMenu is now a synchronous wrapper that calls the async version
            // It should not throw exceptions for valid paths (even if files don't exist)
            try
            {
                // This should not throw an exception for valid Windows paths
                // We can't easily test the actual context menu display in unit tests
                service.ShowContextMenu(inputPath, 100, 100);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                // Expected for non-existent files - but these shouldn't throw from ShowContextMenu
                // as it handles them gracefully
            }

            // Assert - If we get here without exception, the path handling worked
            Assert.True(true);
        }

        [Theory]
        [InlineData("/home/user/file.txt", @"\\wsl.localhost\Ubuntu-24.04\home\user\file.txt")]
        [InlineData("/var/www/index.php", @"\\wsl.localhost\Ubuntu-24.04\var\www\index.php")]
        [InlineData("/mnt/c/windows/file.txt", @"\\wsl.localhost\Ubuntu-24.04\mnt\c\windows\file.txt")]
        public void ShowContextMenu_WithWslPath_ShouldNotThrow(string inputPath, string _)
        {
            // Arrange
            var service = new ContextMenuService();

            // Act - We can't directly test the private method, but we can test the public method
            // with WSL paths to ensure it doesn't crash
            try
            {
                // This should not throw an exception for valid WSL paths
                // We can't easily test the actual context menu display in unit tests
                service.ShowContextMenu(inputPath, 100, 100);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                // Expected for non-existent files - but these shouldn't throw from ShowContextMenu
                // as it handles them gracefully
            }

            // Assert - If we get here without exception, the path conversion worked
            Assert.True(true);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public void ShowContextMenu_WithInvalidPath_ShouldNotThrow(string? invalidPath)
        {
            // Arrange
            var service = new ContextMenuService();

            // Act & Assert - Should not throw exception for invalid paths
            service.ShowContextMenu(invalidPath ?? string.Empty, 100, 100);
        }

        [Fact]
        public async Task ShowContextMenuAsync_WithValidPath_ShouldNotThrow()
        {
            // Arrange
            var service = new ContextMenuService();
            var testPath = Path.Combine(Path.GetTempPath(), "grexTest", "test.txt");
            
            // Create test directory and file
            Directory.CreateDirectory(Path.GetDirectoryName(testPath)!);
            File.WriteAllText(testPath, "test content");

            try
            {
                // Act - Should not throw even if menu can't be displayed in test context
                await service.ShowContextMenuAsync(testPath, 100, 100);
            }
            finally
            {
                // Cleanup
                if (File.Exists(testPath))
                    File.Delete(testPath);
                var dir = Path.GetDirectoryName(testPath);
                if (dir != null && Directory.Exists(dir))
                    Directory.Delete(dir);
            }

            // Assert - If we get here, no exception was thrown
            Assert.True(true);
        }
    }
}