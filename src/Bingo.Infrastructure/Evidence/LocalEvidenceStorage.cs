using Bingo.Application.Evidence;
using Microsoft.Extensions.Configuration;

namespace Bingo.Infrastructure.Evidence;

public sealed class LocalEvidenceStorage : IEvidenceStorage
{
    public const long MaximumBytes = EvidenceImageValidator.MaximumBytes;
    private readonly string root;

    public LocalEvidenceStorage(IConfiguration configuration)
    {
        root = Path.GetFullPath(configuration["EvidenceStorage:LocalPath"] ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "evidence"));
        Directory.CreateDirectory(root);
    }

    public async Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default)
    {
        await using var evidence = await EvidenceImageValidator.ReadAsync(content, cancellationToken);
        var key = $"{eventId:N}/{submissionId:N}/{Guid.NewGuid():N}{evidence.Extension}";
        var fullPath = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var output = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await evidence.Content.CopyToAsync(output, cancellationToken);
        return new StoredEvidence(key, EvidenceImageValidator.SafeFilename(originalFilename, evidence.Extension), evidence.MediaType, evidence.ByteSize, evidence.Width, evidence.Height, evidence.Checksum);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new FileStream(Resolve(storageKey), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous));

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = Resolve(storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        var path = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidOperationException("Invalid evidence storage key.");
        return path;
    }
}
