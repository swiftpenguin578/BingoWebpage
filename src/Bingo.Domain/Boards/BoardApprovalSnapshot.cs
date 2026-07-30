using Bingo.Domain.Catalogue;

namespace Bingo.Domain.Boards;

// The approval tree deliberately copies public board data rather than retaining a
// mutable reference to catalogue or draft rows. Supersession is represented by the
// next snapshot's parent identifier, so a captured snapshot never needs mutation.
public sealed class BoardApprovalSnapshot
{
    private BoardApprovalSnapshot() { }

    public BoardApprovalSnapshot(
        Guid id,
        Guid boardId,
        int version,
        DateTimeOffset approvedAt,
        Guid? approvedByAccountId,
        Guid? supersedesApprovalSnapshotId,
        string name,
        int rows,
        int columns,
        decimal totalEhbEstimate,
        int calculationVersion,
        long boardVersion,
        BoardState lifecycleState = BoardState.Draft)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(calculationVersion, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(boardVersion, 1);
        Id = id;
        BoardId = boardId;
        Version = version;
        ApprovedAt = approvedAt.ToUniversalTime();
        ApprovedByAccountId = approvedByAccountId;
        SupersedesApprovalSnapshotId = supersedesApprovalSnapshotId;
        Name = name;
        Rows = rows;
        Columns = columns;
        TotalEhbEstimate = totalEhbEstimate;
        CalculationVersion = calculationVersion;
        BoardVersion = boardVersion;
        LifecycleState = lifecycleState;
    }

    public Guid Id { get; private set; }
    public Guid BoardId { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset ApprovedAt { get; private set; }
    public Guid? ApprovedByAccountId { get; private set; }
    public Guid? SupersedesApprovalSnapshotId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Rows { get; private set; }
    public int Columns { get; private set; }
    public decimal TotalEhbEstimate { get; private set; }
    public int CalculationVersion { get; private set; }
    public long BoardVersion { get; private set; }
    public BoardState LifecycleState { get; private set; }
}

public sealed class BoardApprovalTileSnapshot
{
    private BoardApprovalTileSnapshot() { }

    public BoardApprovalTileSnapshot(Guid id, Guid approvalSnapshotId, Guid boardTileId, Guid tileTemplateId, int row, int column, string name, string description, string evidenceInstructions, decimal estimatedEhb, string? artworkReference)
    {
        Id = id; ApprovalSnapshotId = approvalSnapshotId; BoardTileId = boardTileId; TileTemplateId = tileTemplateId;
        RowIndex = row; ColumnIndex = column; Name = name; Description = description; EvidenceInstructions = evidenceInstructions;
        EstimatedEhb = estimatedEhb; ArtworkReference = artworkReference;
    }

    public Guid Id { get; private set; }
    public Guid ApprovalSnapshotId { get; private set; }
    public Guid BoardTileId { get; private set; }
    public Guid TileTemplateId { get; private set; }
    public int RowIndex { get; private set; }
    public int ColumnIndex { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string EvidenceInstructions { get; private set; } = string.Empty;
    public decimal EstimatedEhb { get; private set; }
    public string? ArtworkReference { get; private set; }
}

public sealed class BoardApprovalRequirementSnapshot
{
    private BoardApprovalRequirementSnapshot() { }

    public BoardApprovalRequirementSnapshot(Guid id, Guid approvalTileSnapshotId, Guid boardRequirementSnapshotId, int position, int target, bool duplicates, bool higherWeights, int creditedWeight, string description, bool manualObjective)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(creditedWeight, 1);
        Id = id; ApprovalTileSnapshotId = approvalTileSnapshotId; BoardRequirementSnapshotId = boardRequirementSnapshotId;
        Position = position; TargetContribution = target; DuplicatesAllowed = duplicates; AllowHigherWeightings = higherWeights;
        CreditedWeight = higherWeights ? creditedWeight : 1; Description = description; ManualObjective = manualObjective;
    }

    public Guid Id { get; private set; }
    public Guid ApprovalTileSnapshotId { get; private set; }
    public Guid BoardRequirementSnapshotId { get; private set; }
    public int Position { get; private set; }
    public int TargetContribution { get; private set; }
    public bool DuplicatesAllowed { get; private set; }
    public bool AllowHigherWeightings { get; private set; }
    public int CreditedWeight { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public bool ManualObjective { get; private set; }
}

public sealed class BoardApprovalRequirementBossSnapshot
{
    private BoardApprovalRequirementBossSnapshot() { }
    public BoardApprovalRequirementBossSnapshot(Guid id, Guid approvalRequirementSnapshotId, Guid bossActivityId, string name, decimal? efficientRate, long catalogueVersion)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(catalogueVersion, 1);
        Id = id; ApprovalRequirementSnapshotId = approvalRequirementSnapshotId; BossActivityId = bossActivityId; Name = name; EfficientRate = efficientRate; CatalogueVersion = catalogueVersion;
    }
    public Guid Id { get; private set; }
    public Guid ApprovalRequirementSnapshotId { get; private set; }
    public Guid BossActivityId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal? EfficientRate { get; private set; }
    public long CatalogueVersion { get; private set; }
}

public sealed class BoardApprovalRequirementDropSnapshot
{
    private BoardApprovalRequirementDropSnapshot() { }
    public BoardApprovalRequirementDropSnapshot(Guid id, Guid approvalRequirementSnapshotId, Guid sourceDropId, string bossName, string itemName, string displayRate, decimal? numericProbability, int? maximumContribution, decimal? ehbPerContribution, int creditedWeight, long catalogueVersion, DropProbabilityScope probabilityScope = DropProbabilityScope.Participant, bool conditionalOnParent = false, decimal? parentProbability = null, int assumedParticipants = 1, int rollsPerCompletion = 1, string? rollGroup = null, string? rateCondition = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(creditedWeight, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(catalogueVersion, 1);
        Id = id; ApprovalRequirementSnapshotId = approvalRequirementSnapshotId; SourceDropId = sourceDropId; BossName = bossName; ItemName = itemName;
        DisplayRate = displayRate; NumericProbability = numericProbability; MaximumContribution = maximumContribution; EhbPerContribution = ehbPerContribution;
        CreditedWeight = creditedWeight; CatalogueVersion = catalogueVersion; ProbabilityScope = probabilityScope;
        ConditionalOnParent = conditionalOnParent; ParentProbability = conditionalOnParent ? parentProbability : null;
        AssumedParticipants = probabilityScope == DropProbabilityScope.Team ? assumedParticipants : 1;
        RollsPerCompletion = rollsPerCompletion; RollGroup = string.IsNullOrWhiteSpace(rollGroup) ? "default" : rollGroup.Trim(); RateCondition = rateCondition;
    }
    public Guid Id { get; private set; }
    public Guid ApprovalRequirementSnapshotId { get; private set; }
    public Guid SourceDropId { get; private set; }
    public string BossName { get; private set; } = string.Empty;
    public string ItemName { get; private set; } = string.Empty;
    public string DisplayRate { get; private set; } = string.Empty;
    public decimal? NumericProbability { get; private set; }
    public int? MaximumContribution { get; private set; }
    public decimal? EhbPerContribution { get; private set; }
    public int CreditedWeight { get; private set; }
    public long CatalogueVersion { get; private set; }
    public DropProbabilityScope ProbabilityScope { get; private set; }
    public bool ConditionalOnParent { get; private set; }
    public decimal? ParentProbability { get; private set; }
    public int AssumedParticipants { get; private set; }
    public int RollsPerCompletion { get; private set; }
    public string RollGroup { get; private set; } = "default";
    public string? RateCondition { get; private set; }
}
