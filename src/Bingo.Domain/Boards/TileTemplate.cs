namespace Bingo.Domain.Boards;

public sealed class TileTemplate
{
    private TileTemplate() { }
    public TileTemplate(Guid id, string name, string description, ObjectiveType objectiveType, string evidenceInstructions, decimal? manualEhbOverride, string? imageUrl = null, bool descriptionIsAutomatic = false) { ValidateEstimate(objectiveType, manualEhbOverride); Id = id; Name = name; Description = description; DescriptionIsAutomatic = descriptionIsAutomatic; ObjectiveType = objectiveType; EvidenceInstructions = evidenceInstructions; ManualEhbOverride = manualEhbOverride; ImageUrl = imageUrl; Active = true; }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty; public string Description { get; private set; } = string.Empty; public bool DescriptionIsAutomatic { get; private set; } public ObjectiveType ObjectiveType { get; private set; }
    public string EvidenceInstructions { get; private set; } = string.Empty; public decimal? ManualEhbOverride { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool Active { get; private set; }
    public void Update(string name, string description, ObjectiveType objectiveType, string evidenceInstructions, decimal? manualEhbOverride, string? imageUrl = null, bool descriptionIsAutomatic = false) { ValidateEstimate(objectiveType, manualEhbOverride); Name = name; Description = description; DescriptionIsAutomatic = descriptionIsAutomatic; ObjectiveType = objectiveType; EvidenceInstructions = evidenceInstructions; ManualEhbOverride = manualEhbOverride; ImageUrl = imageUrl; }
    public void SetActive(bool active) => Active = active;
    private static void ValidateEstimate(ObjectiveType objectiveType, decimal? manualEhbOverride)
    {
        if (objectiveType != ObjectiveType.Manual && manualEhbOverride is not null)
            throw new ArgumentException("Only a manual tile accepts a manual EHB estimate.", nameof(manualEhbOverride));
    }
}
