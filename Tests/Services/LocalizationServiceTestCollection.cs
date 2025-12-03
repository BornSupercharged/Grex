using Xunit;

namespace Grex.Tests.Services
{
    /// <summary>
    /// Collection definition to ensure LocalizationService tests run sequentially
    /// to avoid race conditions with the singleton instance
    /// </summary>
    [CollectionDefinition("LocalizationServiceTests")]
    public class LocalizationServiceTestCollection : ICollectionFixture<LocalizationServiceTestCollection>
    {
        // This class is just a marker for xUnit to run tests in this collection sequentially
    }
}

