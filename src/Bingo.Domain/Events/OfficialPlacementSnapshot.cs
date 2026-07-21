namespace Bingo.Domain.Events;

public sealed class OfficialPlacementSnapshot
{
    private OfficialPlacementSnapshot() { }
    public OfficialPlacementSnapshot(Guid id, Guid finalizationId, Guid eventId, Guid teamId, string teamName, int placement, bool boardComplete, DateTimeOffset? boardCompletedAt, int completedLines, int completedTiles, decimal ehbTiebreak)
    { ArgumentOutOfRangeException.ThrowIfLessThan(placement, 1); Id = id; FinalizationId = finalizationId; EventId = eventId; TeamId = teamId; TeamName = teamName; Placement = placement; BoardComplete = boardComplete; BoardCompletedAt = boardCompletedAt?.ToUniversalTime(); CompletedLines = completedLines; CompletedTiles = completedTiles; EhbTiebreak = ehbTiebreak; }
    public Guid Id { get; private set; }
    public Guid FinalizationId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid TeamId { get; private set; }
    public string TeamName { get; private set; } = string.Empty; public int Placement { get; private set; }
    public bool BoardComplete { get; private set; }
    public DateTimeOffset? BoardCompletedAt { get; private set; }
    public int CompletedLines { get; private set; }
    public int CompletedTiles { get; private set; }
    public decimal EhbTiebreak { get; private set; }
}
