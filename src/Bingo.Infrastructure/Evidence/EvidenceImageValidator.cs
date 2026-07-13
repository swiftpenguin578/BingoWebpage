using System.Security.Cryptography;
using SixLabors.ImageSharp;

namespace Bingo.Infrastructure.Evidence;

internal static class EvidenceImageValidator
{
    internal const long MaximumBytes = 10 * 1024 * 1024;
    private const long MaximumPixels = 50_000_000;

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
            buffer.Position = 0;
            using var image = await Image.LoadAsync(buffer, cancellationToken);
            if ((long)image.Width * image.Height > MaximumPixels) throw new InvalidOperationException("The evidence image dimensions are too large.");
            var format = image.Metadata.DecodedImageFormat ?? throw new InvalidOperationException("The uploaded file is not a recognized image.");
            var extension = format.Name.ToUpperInvariant() switch
            {
                "PNG" => ".png",
                "JPEG" => ".jpg",
                "WEBP" => ".webp",
                _ => throw new InvalidOperationException("Evidence must be a PNG, JPEG, or WebP image.")
            };
            var mediaType = extension switch { ".png" => "image/png", ".jpg" => "image/jpeg", _ => "image/webp" };
            var checksum = Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
            buffer.Position = 0;
            return new ValidatedEvidence(buffer, total, image.Width, image.Height, extension, mediaType, checksum);
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
