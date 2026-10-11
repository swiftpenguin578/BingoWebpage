using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;

namespace Bingo.Infrastructure.Evidence;

internal static class EvidenceImageValidator
{
    internal const long MaximumBytes = 10 * 1024 * 1024;
    internal const long MaximumPixels = 50_000_000;

    // Backstop for the full decode. The pixel check above it already caps work at
    // 50M pixels; the largest accepted images (16-bit PNG, lossy WebP at 50M pixels)
    // need roughly 400-700 MB of decoder buffers, so 1 GB leaves headroom while
    // staying far below ImageSharp's 4 GB default.
    internal const int DecodeAllocationLimitMegabytes = 1024;

    private const string UnsupportedImageMessage = "Evidence must be a PNG, JPEG, or WebP image.";
    private const string DimensionsTooLargeMessage = "The evidence image dimensions are too large.";

    private static readonly DecoderOptions DecoderLimits = new() { MaxFrames = 1 };

    // A fresh allocator per decode so the accumulative limit applies to one upload
    // and no pooled memory is retained after validation.
    private static DecoderOptions BoundedDecoderOptions(int allocationLimitMegabytes)
    {
        var configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = MemoryAllocator.Create(new MemoryAllocatorOptions
        {
            AccumulativeAllocationLimitMegabytes = allocationLimitMegabytes,
            MaximumPoolSizeMegabytes = 0
        });
        return new DecoderOptions { Configuration = configuration, MaxFrames = 1 };
    }

    internal static Task<ValidatedEvidence> ReadAsync(Stream content, CancellationToken cancellationToken) =>
        ReadAsync(content, DecodeAllocationLimitMegabytes, cancellationToken);

    // The allocation limit is a parameter only so tests can reach the cap with a small image.
    internal static async Task<ValidatedEvidence> ReadAsync(Stream content, int decodeAllocationLimitMegabytes, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        var total = 0L;
        while (true)
        {
            var read = await content.ReadAsync(chunk, cancellationToken);
            if (read == 0) break;
            total += read;
            if (total > MaximumBytes)
            {
                await buffer.DisposeAsync();
                throw new InvalidOperationException("Evidence images may not exceed 10 MB.");
            }
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        if (total == 0)
        {
            await buffer.DisposeAsync();
            throw new InvalidOperationException("Choose an evidence image.");
        }

        try
        {
            var (extension, width, height) = await DecodeAsync(buffer, decodeAllocationLimitMegabytes, cancellationToken);
            var mediaType = extension switch { ".png" => "image/png", ".jpg" => "image/jpeg", _ => "image/webp" };
            var checksum = Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
            buffer.Position = 0;
            return new ValidatedEvidence(buffer, total, width, height, extension, mediaType, checksum);
        }
        catch
        {
            await buffer.DisposeAsync();
            throw;
        }
    }

    // Unknown, truncated or corrupt content and the per-upload allocation cap all
    // become ordinary validation refusals instead of escaping as server errors.
    // When the cap trips, ImageSharp wraps InvalidMemoryOperationException in
    // InvalidImageContentException, so the inner exception decides the message.
    private static async Task<(string Extension, int Width, int Height)> DecodeAsync(MemoryStream buffer, int allocationLimitMegabytes, CancellationToken cancellationToken)
    {
        try
        {
            // Validate from the header before any pixel buffer is allocated: a small
            // compressed file can declare dimensions that need gigabytes to decode.
            buffer.Position = 0;
            var format = await Image.DetectFormatAsync(DecoderLimits, buffer, cancellationToken);
            var extension = format.Name.ToUpperInvariant() switch
            {
                "PNG" => ".png",
                "JPEG" => ".jpg",
                "WEBP" => ".webp",
                _ => throw new InvalidOperationException(UnsupportedImageMessage)
            };
            buffer.Position = 0;
            var info = await Image.IdentifyAsync(DecoderLimits, buffer, cancellationToken);
            if ((long)info.Width * info.Height > MaximumPixels) throw new InvalidOperationException(DimensionsTooLargeMessage);

            // The full decode still proves the pixel data is readable, but only the
            // first frame is decoded and allocations are capped per upload.
            buffer.Position = 0;
            using var image = await Image.LoadAsync(BoundedDecoderOptions(allocationLimitMegabytes), buffer, cancellationToken);
            return (extension, image.Width, image.Height);
        }
        catch (Exception exception) when (exception is ImageFormatException or InvalidMemoryOperationException)
        {
            var allocationCap = exception is InvalidMemoryOperationException || exception.InnerException is InvalidMemoryOperationException;
            throw new InvalidOperationException(allocationCap ? DimensionsTooLargeMessage : UnsupportedImageMessage, exception);
        }
    }

    internal static string SafeFilename(string name, string extension)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrWhiteSpace(stem)) stem = "evidence";
        stem = string.Concat(stem.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ')).Trim();
        if (stem.Length > 100) stem = stem[..100];
        return $"{stem}{extension}";
    }
}

internal sealed record ValidatedEvidence(MemoryStream Content, long ByteSize, int Width, int Height, string Extension, string MediaType, string Checksum) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
