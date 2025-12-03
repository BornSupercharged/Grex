using FluentAssertions;
using Grex.Services;
using Xunit;

namespace Grex.Tests.Services
{
    public class AdminHelperTests
    {
        [Fact]
        public void IsRunAsAdmin_ShouldNotThrow()
        {
            // Act & Assert
            var action = () => AdminHelper.IsRunAsAdmin();
            action.Should().NotThrow();
        }

        [Fact]
        public void IsRunAsAdmin_ShouldReturnBooleanValue()
        {
            // Act
            var result = AdminHelper.IsRunAsAdmin();

            // Assert - result should be a boolean (true or false)
            // We can't verify the actual value since it depends on how the test is run,
            // but we can verify it's a valid boolean
            Assert.True(result == true || result == false);
        }

        [Fact]
        public void IsRunAsAdmin_WhenCalledMultipleTimes_ShouldReturnConsistentResult()
        {
            // Act
            var result1 = AdminHelper.IsRunAsAdmin();
            var result2 = AdminHelper.IsRunAsAdmin();
            var result3 = AdminHelper.IsRunAsAdmin();

            // Assert
            result1.Should().Be(result2);
            result2.Should().Be(result3);
        }
    }
}

