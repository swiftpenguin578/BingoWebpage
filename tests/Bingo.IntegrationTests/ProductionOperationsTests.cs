using Bingo.Web.Operations;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bingo.IntegrationTests;

public sealed class ProductionOperationsTests
{
    [Fact]
    public void ProductionDataProtectionPersistsAndRoundTripsKeys()
    {
        var path = Directory.CreateTempSubdirectory("bingo-production-keys-").FullName;
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [ProductionPreflight.KeyRingSetting] = path
                })
                .Build();

            using var first = BuildProvider(path);
            ProductionPreflight.ValidateDataProtection(first, configuration);
            var protectedValue = first.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("production-test")
                .Protect("persisted");

            using var second = BuildProvider(path);
            var unprotected = second.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("production-test")
                .Unprotect(protectedValue);

            Assert.Equal("persisted", unprotected);
            Assert.NotEmpty(Directory.GetFiles(path, "*.xml"));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void ProductionDataProtectionRejectsNonAbsoluteAndUnusableStorage()
    {
        var relative = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ProductionPreflight.KeyRingSetting] = "data/keys"
            })
            .Build();
        var relativeError = Assert.Throws<InvalidOperationException>(() => ProductionPreflight.RequireAbsoluteKeyRingPath(relative));
        Assert.Contains("absolute", relativeError.Message, StringComparison.OrdinalIgnoreCase);

        var filePath = Path.Combine(Path.GetTempPath(), $"bingo-production-key-file-{Guid.NewGuid():N}");
        File.WriteAllText(filePath, "not-a-directory");
        try
        {
            var unusable = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [ProductionPreflight.KeyRingSetting] = filePath
                })
                .Build();
            using var provider = BuildProvider(filePath);
            var error = Assert.Throws<InvalidOperationException>(() => ProductionPreflight.ValidateDataProtection(provider, unusable));
            Assert.Contains("data protection", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static ServiceProvider BuildProvider(string path) =>
        new ServiceCollection()
            .AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(path))
            .Services
            .BuildServiceProvider();
}
