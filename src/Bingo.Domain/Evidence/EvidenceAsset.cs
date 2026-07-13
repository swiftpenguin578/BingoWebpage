namespace Bingo.Domain.Evidence;

public sealed class EvidenceAsset
{
    private EvidenceAsset() { }
    public EvidenceAsset(Guid id, Guid submissionId, string storageKey, string filename, string mediaType, long bytes,
        int width, int height, string checksum, DateTimeOffset uploadedAt, Guid uploadedByAccountId, EvidenceAssetRole role)
    {
        Id = id; SubmissionId = submissionId; StorageKey = storageKey; OriginalFilename = filename; MediaType = mediaType;
        ByteSize = bytes; PixelWidth = width; PixelHeight = height; Checksum = checksum; UploadedAt = uploadedAt.ToUniversalTime();
        UploadedByAccountId = uploadedByAccountId; Role = role; Active = true;
    }
    public Guid Id { get; private set; }
    public Guid SubmissionId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string OriginalFilename { get; private set; } = string.Empty;
    public string MediaType { get; private set; } = string.Empty;
    public long ByteSize { get; private set; }
    public int PixelWidth { get; private set; }
    public int PixelHeight { get; private set; }
    public string Checksum { get; private set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; private set; }
    public Guid UploadedByAccountId { get; private set; }
    public EvidenceAssetRole Role { get; private set; }
    public bool Active { get; private set; }
    public void Deactivate() => Active = false;
}
