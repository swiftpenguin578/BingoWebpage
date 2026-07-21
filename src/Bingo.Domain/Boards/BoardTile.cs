namespace Bingo.Domain.Boards;

public sealed class BoardTile
{
    private BoardTile() { }
    public BoardTile(Guid id, Guid boardId, Guid tileTemplateId, int row, int column, string name, string description, string evidenceInstructions, decimal estimatedEhb, string? imageUrl = null)
    { Id = id; BoardId = boardId; TileTemplateId = tileTemplateId; RowIndex = row; ColumnIndex = column; NameSnapshot = name; DescriptionSnapshot = description; EvidenceInstructionsSnapshot = evidenceInstructions; EstimatedEhbSnapshot = estimatedEhb; ImageUrlSnapshot = imageUrl; }
    public Guid Id { get; private set; }
    public Guid BoardId { get; private set; }
    public Guid TileTemplateId { get; private set; }
    public int RowIndex { get; private set; }
    public int ColumnIndex { get; private set; }
    public string NameSnapshot { get; private set; } = string.Empty; public string DescriptionSnapshot { get; private set; } = string.Empty; public string EvidenceInstructionsSnapshot { get; private set; } = string.Empty; public decimal EstimatedEhbSnapshot { get; private set; }
    public string? ImageUrlSnapshot { get; private set; }
    public void Move(int row, int column) { RowIndex = row; ColumnIndex = column; }
    public void UpdateContent(string name, string description, string evidenceInstructions, decimal estimatedEhb, string? imageUrl = null)
    {
        NameSnapshot = name;
        DescriptionSnapshot = description;
        EvidenceInstructionsSnapshot = evidenceInstructions;
        EstimatedEhbSnapshot = estimatedEhb;
        ImageUrlSnapshot = imageUrl;
    }
}
