using Bingo.Infrastructure.Evidence;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Bingo.IntegrationTests;

public sealed class LocalEvidenceStorageTests : IAsyncDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"bingo-evidence-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task StoresDecodedImageWithServerDerivedMetadata()
    {
        var storage = CreateStorage();
        await using var input = new MemoryStream();
        using (var image = new Image<Rgba32>(12, 7)) await image.SaveAsPngAsync(input);
        input.Position = 0;

        var stored = await storage.StoreAsync(Guid.NewGuid(), Guid.NewGuid(), "discord screenshot.not-real", input);

        Assert.Equal("image/png", stored.MediaType);
        Assert.Equal(12, stored.Width);
        Assert.Equal(7, stored.Height);
        Assert.EndsWith(".png", stored.StorageKey, StringComparison.Ordinal);
        Assert.Equal("discord screenshot.png", stored.OriginalFilename);
        Assert.Equal(64, stored.Checksum.Length);
        await using var read = await storage.OpenReadAsync(stored.StorageKey);
        Assert.True(read.Length > 0);
    }

    [Fact]
    public async Task RejectsAFileThatOnlyPretendsToBeAnImage()
    {
        var storage = CreateStorage();
        await using var input = new MemoryStream("not actually a png"u8.ToArray());

        await Assert.ThrowsAnyAsync<Exception>(() => storage.StoreAsync(Guid.NewGuid(), Guid.NewGuid(), "fake.png", input));
        Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task DeletesStoredEvidence()
    {
        var storage = CreateStorage();
        await using var input = new MemoryStream();
        using (var image = new Image<Rgba32>(2, 2)) await image.SaveAsWebpAsync(input);
        input.Position = 0;
        var stored = await storage.StoreAsync(Guid.NewGuid(), Guid.NewGuid(), "proof.webp", input);

        await storage.DeleteAsync(stored.StorageKey);

        await Assert.ThrowsAsync<FileNotFoundException>(() => storage.OpenReadAsync(stored.StorageKey));
    }

    private LocalEvidenceStorage CreateStorage()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EvidenceStorage:LocalPath"] = root
        }).Build();
        return new LocalEvidenceStorage(configuration);
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
        return ValueTask.CompletedTask;
    }
}
