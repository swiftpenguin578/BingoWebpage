using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;

namespace Bingo.Domain.Events;

public enum LuckBasisStatus { Retained, ConflictingEarliestApproval, MissingIdentity, UnavailableMechanics }

public sealed class EventLuckOutcomeBasis
{
    private EventLuckOutcomeBasis() { }

    public EventLuckOutcomeBasis(Guid eventId, Guid sourceDropId, Guid itemId, Guid? bossId,
        BoardApprovalSnapshot? approval, BoardApprovalRequirementDropSnapshot? drop, LuckBasisStatus status)
    {
        EventId = eventId; SourceDropId = sourceDropId; ItemIdSnapshot = itemId; BossActivityId = bossId; Status = status;
        FirstApprovalSnapshotId = approval?.Id; FirstApprovalDropSnapshotId = drop?.Id; FirstApprovedAt = approval?.ApprovedAt;
        NumericProbability = drop?.NumericProbability; RollsPerCompletion = drop?.RollsPerCompletion;
        ProbabilityScope = drop?.ProbabilityScope; ConditionalOnParent = drop?.ConditionalOnParent;
        ParentProbability = drop?.ParentProbability; AssumedParticipants = drop?.AssumedParticipants;
        RollGroup = drop?.RollGroup; RateCondition = drop?.RateCondition;
    }

    public Guid EventId { get; private set; }
    public Guid SourceDropId { get; private set; }
    public Guid ItemIdSnapshot { get; private set; }
    public Guid? BossActivityId { get; private set; }
    public Guid? FirstApprovalSnapshotId { get; private set; }
    public Guid? FirstApprovalDropSnapshotId { get; private set; }
    public DateTimeOffset? FirstApprovedAt { get; private set; }
    public decimal? NumericProbability { get; private set; }
    public int? RollsPerCompletion { get; private set; }
    public DropProbabilityScope? ProbabilityScope { get; private set; }
    public bool? ConditionalOnParent { get; private set; }
    public decimal? ParentProbability { get; private set; }
    public int? AssumedParticipants { get; private set; }
    public string? RollGroup { get; private set; }
    public string? RateCondition { get; private set; }
    public LuckBasisStatus Status { get; private set; }
    public string? Metric { get; private set; }
    public DateTimeOffset? MetricBoundAt { get; private set; }
    public DateTimeOffset? MappingValidatedAt { get; private set; }
    public long? MappingCatalogueVersion { get; private set; }
    public int SourceRevision { get; private set; } = 1;

    public void BindMetric(string metric, DateTimeOffset boundAt, DateTimeOffset validatedAt, long catalogueVersion)
    {
        if (Metric is not null || Status != LuckBasisStatus.Retained) return;
        ArgumentException.ThrowIfNullOrWhiteSpace(metric);
        Metric = metric; MetricBoundAt = boundAt.ToUniversalTime(); MappingValidatedAt = validatedAt.ToUniversalTime();
        MappingCatalogueVersion = catalogueVersion; SourceRevision++;
    }
}
