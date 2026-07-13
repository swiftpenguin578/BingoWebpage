namespace Bingo.Domain.Boards;

public sealed class BoardRequirementSnapshot
{
    private BoardRequirementSnapshot() { }
    public BoardRequirementSnapshot(Guid id, Guid boardTileId, int position, int target, bool duplicates, bool higherWeights, string description, bool manual) { Id = id; BoardTileId = boardTileId; Position = position; TargetContribution = target; DuplicatesAllowed = duplicates; AllowHigherWeightings = higherWeights; Description = description; ManualObjective = manual; }
    public Guid Id { get; private set; }
    public Guid BoardTileId { get; private set; }
    public int Position { get; private set; }
    public int TargetContribution { get; private set; }
    public bool DuplicatesAllowed { get; private set; }
    public bool AllowHigherWeightings { get; private set; }
    public string Description { get; private set; } = string.Empty; public bool ManualObjective { get; private set; }
}
