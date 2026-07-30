namespace Bingo.Domain.Boards;

public sealed class BoardTileImageAsset
{
    private BoardTileImageAsset() { }
    public BoardTileImageAsset(Guid id, Guid eventId, Guid boardTileId, string storageKey, string filename, string mediaType, long byteSize, int width, int height, string checksum, Guid uploadedByAccountId, DateTimeOffset uploadedAt)
    { Id = id; EventId = eventId; BoardTileId = boardTileId; StorageKey = storageKey; OriginalFilename = filename; MediaType = mediaType; ByteSize = byteSize; Width = width; Height = height; Checksum = checksum; UploadedByAccountId = uploadedByAccountId; UploadedAt = uploadedAt.ToUniversalTime(); }
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid BoardTileId { get; private set; }
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
    public void Replace(DateTimeOffset now) => ReplacedAt = now.ToUniversalTime();
}
