using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using Bingo.Infrastructure.Evidence;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Bingo.IntegrationTests;

[CollectionDefinition(nameof(EvidenceImageDecodeLimitTests), DisableParallelization = true)]
public sealed class EvidenceImageDecodeLimitGroup;

// Brief 167 / F01: a small compressed file must not be decoded into a huge pixel buffer.
// Runs without parallel neighbours so the process memory samples belong to this test.
[Collection(nameof(EvidenceImageDecodeLimitTests))]
public sealed class EvidenceImageDecodeLimitTests : IAsyncDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"bingo-evidence-limit-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task RejectsACompressedDecompressionBombFromItsHeaderWithBoundedMemory()
    {
        // 20000x20000 RGBA = 400M pixels (1.6 GB decoded) in roughly 1.5 MB of PNG.
        var bomb = CraftZeroFilledRgbaPng(20_000, 20_000);
        Assert.InRange(bomb.Length, 1, LocalEvidenceStorage.MaximumBytes);
        var storage = CreateStorage();

        using var process = Process.GetCurrentProcess();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        process.Refresh();
        var baseline = process.WorkingSet64;
        var peak = baseline;
        using var done = new CancellationTokenSource();
        var sampler = Task.Run(async () =>
        {
            while (!done.IsCancellationRequested)
            {
                process.Refresh();
                peak = Math.Max(peak, process.WorkingSet64);
                await Task.Delay(2);
            }
        });

        var stopwatch = Stopwatch.StartNew();
        await using var input = new MemoryStream(bomb);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => storage.StoreAsync(Guid.NewGuid(), Guid.NewGuid(), "bomb.png", input));
        stopwatch.Stop();
        await done.CancelAsync();
        await sampler;

        Assert.Equal("The evidence image dimensions are too large.", failure.Message);
        // The decoded image alone would be 1.6 GB; header validation keeps growth to the upload buffer.
        Assert.True(peak - baseline < 128L * 1024 * 1024, $"Working set grew by {(peak - baseline) / (1024 * 1024)} MB while rejecting the image.");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"Rejection took {stopwatch.Elapsed}.");
        Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task RejectsAnUnsupportedFormatBeforeDecoding()
    {
        var storage = CreateStorage();
        await using var input = new MemoryStream();
        using (var image = new Image<Rgba32>(4, 4)) await image.SaveAsGifAsync(input);
        input.Position = 0;

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => storage.StoreAsync(Guid.NewGuid(), Guid.NewGuid(), "proof.gif", input));

        Assert.Equal("Evidence must be a PNG, JPEG, or WebP image.", failure.Message);
    }

    [Fact]
    public async Task StillAcceptsALargeLegitimateScreenshot()
    {
        var storage = CreateStorage();
        await using var input = new MemoryStream();
        using (var image = new Image<Rgba32>(7000, 7000)) await image.SaveAsPngAsync(input);
        var bytes = input.ToArray();
        input.Position = 0;

        var stored = await storage.StoreAsync(Guid.NewGuid(), Guid.NewGuid(), "panorama.png", input);

        Assert.Equal("image/png", stored.MediaType);
        Assert.Equal(7000, stored.Width);
        Assert.Equal(7000, stored.Height);
        Assert.Equal(bytes.Length, stored.ByteSize);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(), stored.Checksum);
    }

    [Fact]
    public async Task StillAcceptsJpegAndUsesTheFirstFrameDimensions()
    {
        var storage = CreateStorage();
        await using var input = new MemoryStream();
        using (var image = new Image<Rgba32>(1920, 1080)) await image.SaveAsJpegAsync(input);
        input.Position = 0;

        var stored = await storage.StoreAsync(Guid.NewGuid(), Guid.NewGuid(), "shot.jpeg", input);

        Assert.Equal("image/jpeg", stored.MediaType);
        Assert.Equal(1920, stored.Width);
        Assert.Equal(1080, stored.Height);
        Assert.EndsWith(".jpg", stored.StorageKey, StringComparison.Ordinal);
    }

    // Brief 170 / M2: the per-upload allocation cap is a refusal, not a server error.
    // ImageSharp reports the cap as InvalidImageContentException wrapping
    // InvalidMemoryOperationException; a 2000x2000 RGBA decode needs about 16 MB.
    [Fact]
    public async Task AllocationCapDuringDecodeIsRejectedAsTooLarge()
    {
        await using var input = new MemoryStream();
        using (var image = new Image<Rgba32>(2000, 2000)) await image.SaveAsPngAsync(input);
        input.Position = 0;

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => EvidenceImageValidator.ReadAsync(input, 1, CancellationToken.None));

        Assert.Equal("The evidence image dimensions are too large.", failure.Message);
    }

    [Theory]
    [InlineData("truncated-png")]
    [InlineData("corrupt-png")]
    [InlineData("text")]
    public async Task UnreadableUploadsAreRejectedAsUnsupportedImages(string kind)
    {
        var storage = CreateStorage();
        await using var input = new MemoryStream(UnreadableUpload(kind));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => storage.StoreAsync(Guid.NewGuid(), Guid.NewGuid(), "proof.png", input));

        Assert.Equal("Evidence must be a PNG, JPEG, or WebP image.", failure.Message);
        Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
    }

    internal static byte[] UnreadableUpload(string kind)
    {
        using var output = new MemoryStream();
        using (var image = new Image<Rgba32>(64, 64, new Rgba32(10, 20, 30))) image.SaveAsPng(output);
        var png = output.ToArray();
        return kind switch
        {
            // Signature, IHDR and the start of IDAT only.
            "truncated-png" => png[..(png.Length / 2)],
            // A valid header whose compressed pixel data is overwritten.
            "corrupt-png" => [.. png[..41], .. Enumerable.Repeat((byte)0xFF, png.Length - 41)],
            _ => System.Text.Encoding.UTF8.GetBytes("This is a text file, not a screenshot.")
        };
    }

    internal static byte[] CraftZeroFilledRgbaPng(int width, int height)
    {
        using var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bit depth
        header[9] = 6; // RGBA
        WriteChunk(output, "IHDR", header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var row = new byte[1 + width * 4]; // filter byte 0 + zero pixels
            for (var y = 0; y < height; y++) zlib.Write(row);
        }
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        output.Write(number);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(number, ~Crc32(Crc32(0xFFFFFFFFu, typeBytes), data));
        output.Write(number);
    }

    private static uint Crc32(uint crc, byte[] data)
    {
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }
        return crc;
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
