namespace Bingo.Domain.Boards;

public sealed class TileTemplateRequirement
{
    private TileTemplateRequirement() { }
    public TileTemplateRequirement(Guid id, Guid tileTemplateId, int position, int targetContribution, bool duplicatesAllowed, bool allowHigherWeightings, string description, bool manualObjective) { ArgumentOutOfRangeException.ThrowIfLessThan(targetContribution, 1); Id = id; TileTemplateId = tileTemplateId; Position = position; TargetContribution = targetContribution; DuplicatesAllowed = duplicatesAllowed; AllowHigherWeightings = allowHigherWeightings; Description = description; ManualObjective = manualObjective; }
    public Guid Id { get; private set; }
    public Guid TileTemplateId { get; private set; }
    public int Position { get; private set; }
    public int TargetContribution { get; private set; }
    public bool DuplicatesAllowed { get; private set; }
    public bool AllowHigherWeightings { get; private set; }
    public string Description { get; private set; } = string.Empty; public bool ManualObjective { get; private set; }
}
