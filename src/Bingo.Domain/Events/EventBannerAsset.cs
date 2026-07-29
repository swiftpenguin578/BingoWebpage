namespace Bingo.Domain.Events;

public sealed class EventBannerAsset
{
    private EventBannerAsset() { }

    public EventBannerAsset(Guid id, Guid eventId, string storageKey, string filename, string mediaType, long byteSize, int width, int height, string checksum, Guid uploadedByAccountId, DateTimeOffset uploadedAt)
    {
        Id = id;
        EventId = eventId;
        StorageKey = storageKey;
        OriginalFilename = filename;
        MediaType = mediaType;
        ByteSize = byteSize;
        Width = width;
        Height = height;
        Checksum = checksum;
        UploadedByAccountId = uploadedByAccountId;
        UploadedAt = uploadedAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string OriginalFilename { get; private set; } = string.Empty;
    public string MediaType { get; private set; } = string.Empty;
    public long ByteSize { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public string Checksum { get; private set; } = string.Empty;
    public Guid UploadedByAccountId { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public DateTimeOffset? ReplacedAt { get; private set; }

    public void Replace(DateTimeOffset replacedAt) => ReplacedAt = replacedAt.ToUniversalTime();
}
