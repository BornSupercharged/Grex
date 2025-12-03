using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Grex.Services;

namespace Grex.Tests;

public class StandaloneTest
{
    [Fact]
    public void SearchService_IsWslPath_ShouldReturnCorrectResult()
    {
        // Arrange
        var searchService = new SearchService();
        
        // Act
        var wslPath = searchService.IsWslPath("/mnt/c/test");
        var windowsPath = searchService.IsWslPath("C:\\test");
        
        // Assert
        wslPath.Should().BeTrue();
        windowsPath.Should().BeFalse();
    }
    
    [Fact]
    public void SimpleMathTest_ShouldWork()
    {
        // Arrange
        int a = 1;
        int b = 2;
        
        // Act
        int result = a + b;
        
        // Assert
        result.Should().Be(3);
    }
}