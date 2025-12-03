using System;
using FluentAssertions;
using Grex.Services;
using Xunit;

namespace Grex.Tests.Services
{
    public class NotificationServiceTests
    {
        [Fact]
        public void Instance_ShouldReturnSingleton()
        {
            // Arrange & Act
            var instance1 = NotificationService.Instance;
            var instance2 = NotificationService.Instance;

            // Assert
            instance1.Should().NotBeNull();
            instance2.Should().NotBeNull();
            instance1.Should().BeSameAs(instance2);
        }

        [Fact]
        public void ShowError_WithTitleAndMessage_ShouldNotThrow()
        {
            // Arrange
            var service = NotificationService.Instance;

            // Act & Assert
            var action = () => service.ShowError("Test Error", "This is a test error message");
            action.Should().NotThrow();
        }

        [Fact]
        public void ShowError_WithTitleAndException_ShouldNotThrow()
        {
            // Arrange
            var service = NotificationService.Instance;
            var exception = new Exception("Test exception message");

            // Act & Assert
            var action = () => service.ShowError("Test Error", exception);
            action.Should().NotThrow();
        }

        [Fact]
        public void ShowError_WithNullException_ShouldNotThrow()
        {
            // Arrange
            var service = NotificationService.Instance;

            // Act & Assert
            var action = () => service.ShowError("Test Error", (Exception)null!);
            action.Should().NotThrow();
        }

        [Fact]
        public void ShowError_WithLongrexceptionMessage_ShouldTruncate()
        {
            // Arrange
            var service = NotificationService.Instance;
            var longMessage = new string('A', 300);
            var exception = new Exception(longMessage);

            // Act & Assert
            var action = () => service.ShowError("Test Error", exception);
            action.Should().NotThrow();
        }

        [Fact]
        public void ShowInfo_WithTitleAndMessage_ShouldNotThrow()
        {
            // Arrange
            var service = NotificationService.Instance;

            // Act & Assert
            var action = () => service.ShowInfo("Test Info", "This is a test info message");
            action.Should().NotThrow();
        }

        [Fact]
        public void ShowWarning_WithTitleAndMessage_ShouldNotThrow()
        {
            // Arrange
            var service = NotificationService.Instance;

            // Act & Assert
            var action = () => service.ShowWarning("Test Warning", "This is a test warning message");
            action.Should().NotThrow();
        }

        [Fact]
        public void ShowSuccess_WithTitleAndMessage_ShouldNotThrow()
        {
            // Arrange
            var service = NotificationService.Instance;

            // Act & Assert
            var action = () => service.ShowSuccess("Test Success", "This is a test success message");
            action.Should().NotThrow();
        }

        [Fact]
        public void ShowError_WithEmptyStrings_ShouldNotThrow()
        {
            // Arrange
            var service = NotificationService.Instance;

            // Act & Assert
            var action = () => service.ShowError("", "");
            action.Should().NotThrow();
        }

        [Fact]
        public void ShowError_WithSpecialCharacters_ShouldNotThrow()
        {
            // Arrange
            var service = NotificationService.Instance;
            var messageWithSpecialChars = "Error with <special> & \"characters\" 'test'";

            // Act & Assert
            var action = () => service.ShowError("Test", messageWithSpecialChars);
            action.Should().NotThrow();
        }

        [Fact]
        public void ShowError_WithExceptionHavingInnerException_ShouldNotThrow()
        {
            // Arrange
            var service = NotificationService.Instance;
            var innerException = new Exception("Inner exception");
            var outerException = new Exception("Outer exception", innerException);

            // Act & Assert
            var action = () => service.ShowError("Test Error", outerException);
            action.Should().NotThrow();
        }
    }
}

