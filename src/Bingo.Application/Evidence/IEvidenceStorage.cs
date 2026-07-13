namespace Bingo.Application.Evidence;

public interface IEvidenceStorage
{
    Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}

public sealed record StoredEvidence(string StorageKey, string OriginalFilename, string MediaType, long ByteSize, int Width, int Height, string Checksum);
