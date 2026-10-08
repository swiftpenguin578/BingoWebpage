namespace Bingo.Domain.Boards;

public sealed class TileTemplate
{
    private TileTemplate() { }
    public TileTemplate(Guid id, string name, string description, ObjectiveType objectiveType, string evidenceInstructions, decimal? manualEhbOverride, string? imageUrl = null, bool descriptionIsAutomatic = false) { ValidateEstimate(objectiveType, manualEhbOverride); Id = id; Name = name; Description = description; DescriptionIsAutomatic = descriptionIsAutomatic; ObjectiveType = objectiveType; EvidenceInstructions = evidenceInstructions; ManualEhbOverride = manualEhbOverride; ImageUrl = imageUrl; Active = true; }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty; public string Description { get; private set; } = string.Empty; public bool DescriptionIsAutomatic { get; private set; }
    public ObjectiveType ObjectiveType { get; private set; }
    public string EvidenceInstructions { get; private set; } = string.Empty; public decimal? ManualEhbOverride { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool Active { get; private set; }
    public void Update(string name, string description, ObjectiveType objectiveType, string evidenceInstructions, decimal? manualEhbOverride, string? imageUrl = null, bool descriptionIsAutomatic = false) { ValidateEstimate(objectiveType, manualEhbOverride); Name = name; Description = description; DescriptionIsAutomatic = descriptionIsAutomatic; ObjectiveType = objectiveType; EvidenceInstructions = evidenceInstructions; ManualEhbOverride = manualEhbOverride; ImageUrl = imageUrl; }
    public void SetActive(bool active) => Active = active;
    private static void ValidateEstimate(ObjectiveType objectiveType, decimal? manualEhbOverride)
    {
        if (manualEhbOverride is < 0.0001m or > 100000m)
            throw new ArgumentOutOfRangeException(nameof(manualEhbOverride), "A manual total EHB estimate must be between 0.0001 and 100000.");
    }
}
