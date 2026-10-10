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

    private static readonly DecoderOptions DecoderLimits = new() { MaxFrames = 1 };

    // A fresh allocator per decode so the accumulative limit applies to one upload
    // and no pooled memory is retained after validation.
    private static DecoderOptions BoundedDecoderOptions()
    {
        var configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = MemoryAllocator.Create(new MemoryAllocatorOptions
        {
            AccumulativeAllocationLimitMegabytes = DecodeAllocationLimitMegabytes,
            MaximumPoolSizeMegabytes = 0
        });
        return new DecoderOptions { Configuration = configuration, MaxFrames = 1 };
    }

    internal static async Task<ValidatedEvidence> ReadAsync(Stream content, CancellationToken cancellationToken)
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
            // Validate from the header before any pixel buffer is allocated: a small
            // compressed file can declare dimensions that need gigabytes to decode.
            buffer.Position = 0;
            var format = await Image.DetectFormatAsync(DecoderLimits, buffer, cancellationToken);
            var extension = format.Name.ToUpperInvariant() switch
            {
                "PNG" => ".png",
                "JPEG" => ".jpg",
                "WEBP" => ".webp",
                _ => throw new InvalidOperationException("Evidence must be a PNG, JPEG, or WebP image.")
            };
            buffer.Position = 0;
            var info = await Image.IdentifyAsync(DecoderLimits, buffer, cancellationToken);
            if ((long)info.Width * info.Height > MaximumPixels) throw new InvalidOperationException("The evidence image dimensions are too large.");

            // The full decode still proves the pixel data is readable, but only the
            // first frame is decoded and allocations are capped per upload.
            buffer.Position = 0;
            int width, height;
            try
            {
                using var image = await Image.LoadAsync(BoundedDecoderOptions(), buffer, cancellationToken);
                width = image.Width;
                height = image.Height;
            }
            catch (InvalidMemoryOperationException)
            {
                throw new InvalidOperationException("The evidence image dimensions are too large.");
            }

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
