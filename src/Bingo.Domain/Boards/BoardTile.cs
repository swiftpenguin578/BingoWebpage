namespace Bingo.Domain.Boards;

public sealed class BoardTile
{
    private BoardTile() { }
    public BoardTile(Guid id, Guid boardId, Guid tileTemplateId, int row, int column, string name, string description, string evidenceInstructions, decimal estimatedEhb, string? imageUrl = null, bool descriptionIsAutomatic = false)
    { Id = id; BoardId = boardId; TileTemplateId = tileTemplateId; RowIndex = row; ColumnIndex = column; NameSnapshot = name; DescriptionSnapshot = description; DescriptionIsAutomatic = descriptionIsAutomatic; EvidenceInstructionsSnapshot = evidenceInstructions; EstimatedEhbSnapshot = estimatedEhb; ImageUrlSnapshot = imageUrl; }
    public Guid Id { get; private set; }
    public Guid BoardId { get; private set; }
    public Guid TileTemplateId { get; private set; }
    public int RowIndex { get; private set; }
    public int ColumnIndex { get; private set; }
    public string NameSnapshot { get; private set; } = string.Empty; public string DescriptionSnapshot { get; private set; } = string.Empty; public bool DescriptionIsAutomatic { get; private set; }
    public string EvidenceInstructionsSnapshot { get; private set; } = string.Empty; public decimal EstimatedEhbSnapshot { get; private set; }
    public string? ImageUrlSnapshot { get; private set; }
    public Guid? ActiveImageAssetId { get; private set; }
    /// <summary>
    /// The catalogue identity used for the last working-copy estimate. This is
    /// deliberately kept on the tile rather than on an approval snapshot:
    /// approval/publication remains immutable while draft freshness can move.
    /// </summary>
    public string? EstimateCatalogueFingerprint { get; private set; }
    public DateTimeOffset? EstimateCalculatedAt { get; private set; }
    public bool EstimateNeedsVerification { get; private set; }
    public void Move(int row, int column) { RowIndex = row; ColumnIndex = column; }
    public void UpdateContent(string name, string description, string evidenceInstructions, decimal estimatedEhb, string? imageUrl = null, bool descriptionIsAutomatic = false)
    {
        NameSnapshot = name;
        DescriptionSnapshot = description;
        DescriptionIsAutomatic = descriptionIsAutomatic;
        EvidenceInstructionsSnapshot = evidenceInstructions;
        EstimatedEhbSnapshot = estimatedEhb;
        ImageUrlSnapshot = imageUrl;
    }
    public void SetActiveImageAsset(Guid? assetId) => ActiveImageAssetId = assetId;
    public void SetEstimateCache(decimal estimate, string fingerprint, DateTimeOffset calculatedAt, bool needsVerification)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(estimate);
        if (string.IsNullOrWhiteSpace(fingerprint)) throw new ArgumentException("A catalogue fingerprint is required.", nameof(fingerprint));
        EstimatedEhbSnapshot = estimate;
        EstimateCatalogueFingerprint = fingerprint;
        EstimateCalculatedAt = calculatedAt.ToUniversalTime();
        EstimateNeedsVerification = needsVerification;
    }
    public void MarkEstimateNeedsVerification() => EstimateNeedsVerification = true;
}
