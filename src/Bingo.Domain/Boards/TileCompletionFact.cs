namespace Bingo.Domain.Boards;

public sealed class TileCompletionFact
{
    private TileCompletionFact() { }

    public TileCompletionFact(
        Guid id,
        Guid eventId,
        Guid teamId,
        Guid boardTileId,
        Guid approvalSnapshotId,
        bool isComplete,
        DateTimeOffset? completedAt,
        string? qualifyingContributionsJson,
        DateTimeOffset recordedAt)
    {
        Id = id;
        EventId = eventId;
        TeamId = teamId;
        BoardTileId = boardTileId;
        ApprovalSnapshotId = approvalSnapshotId;
        Reconcile(isComplete, completedAt, qualifyingContributionsJson, recordedAt);
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid BoardTileId { get; private set; }
    public Guid ApprovalSnapshotId { get; private set; }
    public bool IsComplete { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? QualifyingContributionsJson { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    public void Reconcile(bool isComplete, DateTimeOffset? completedAt, string? qualifyingContributionsJson, DateTimeOffset recordedAt)
    {
        if (!isComplete && (completedAt is not null || qualifyingContributionsJson != "[]"))
            throw new ArgumentException("An incomplete tile cannot retain a completion time or qualifying contributions.");
        if (isComplete && (completedAt is null || string.IsNullOrWhiteSpace(qualifyingContributionsJson)))
            throw new ArgumentException("A completed tile requires its effective completion time and qualifying contribution provenance.");

        IsComplete = isComplete;
        CompletedAt = completedAt?.ToUniversalTime();
        QualifyingContributionsJson = qualifyingContributionsJson;
        RecordedAt = recordedAt.ToUniversalTime();
    }
}
