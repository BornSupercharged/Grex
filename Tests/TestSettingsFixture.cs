using System;
using System.IO;
using Grex.Services;
using Xunit;

namespace Grex.Tests
{
    public class TestSettingsFixture : IDisposable
    {
        private readonly string _tempSettingsPath;

        public TestSettingsFixture()
        {
            _tempSettingsPath = Path.Combine(Path.GetTempPath(), $"grex_TestSettings_{Guid.NewGuid():N}.json");
            SettingsService.SetSettingsFilePathOverride(_tempSettingsPath);
        }

        public void Dispose()
        {
            SettingsService.SetSettingsFilePathOverride(null);
            try
            {
                if (File.Exists(_tempSettingsPath))
                {
                    File.Delete(_tempSettingsPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [CollectionDefinition("SettingsOverride collection")]
    public class SettingsOverrideCollection : ICollectionFixture<TestSettingsFixture>
    {
    }
}


