namespace Bingo.Domain.Boards;

public sealed class TileTemplate
{
    private TileTemplate() { }
    public TileTemplate(Guid id, string name, string description, ObjectiveType objectiveType, string evidenceInstructions, decimal? manualEhbOverride, string? imageUrl = null) { Id = id; Name = name; Description = description; ObjectiveType = objectiveType; EvidenceInstructions = evidenceInstructions; ManualEhbOverride = manualEhbOverride; ImageUrl = imageUrl; Active = true; }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty; public string Description { get; private set; } = string.Empty; public ObjectiveType ObjectiveType { get; private set; }
    public string EvidenceInstructions { get; private set; } = string.Empty; public decimal? ManualEhbOverride { get; private set; } public string? ImageUrl { get; private set; }
    public bool Active { get; private set; }
    public void Update(string name, string description, ObjectiveType objectiveType, string evidenceInstructions, decimal? manualEhbOverride, string? imageUrl = null) { Name = name; Description = description; ObjectiveType = objectiveType; EvidenceInstructions = evidenceInstructions; ManualEhbOverride = manualEhbOverride; ImageUrl = imageUrl; }
    public void SetActive(bool active) => Active = active;
}
