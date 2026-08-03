namespace Bingo.Domain.Events;

public sealed class EventFinalizationSnapshot
{
    private EventFinalizationSnapshot() { }
    public EventFinalizationSnapshot(Guid id, Guid eventId, int version, DateTimeOffset finalizedAt, Guid finalizedByAccountId, Guid reviewCycleId, string? consumedResolutionIdsJson = null, string? calculationInputsJson = null, string? calculationResultsJson = null)
    { ArgumentOutOfRangeException.ThrowIfLessThan(version, 1); Id = id; EventId = eventId; Version = version; FinalizedAt = finalizedAt.ToUniversalTime(); FinalizedByAccountId = finalizedByAccountId; ReviewCycleId = reviewCycleId; ConsumedResolutionIdsJson = consumedResolutionIdsJson; CalculationInputsJson = calculationInputsJson; CalculationResultsJson = calculationResultsJson; }
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset FinalizedAt { get; private set; }
    public Guid FinalizedByAccountId { get; private set; }
    public Guid ReviewCycleId { get; private set; }
    public string? ConsumedResolutionIdsJson { get; private set; }
    public string? CalculationInputsJson { get; private set; }
    public string? CalculationResultsJson { get; private set; }
    public DateTimeOffset? UnfinalizedAt { get; private set; }
    public Guid? UnfinalizedByAccountId { get; private set; }
    public string? UnfinalizeReason { get; private set; }
    public bool Active => UnfinalizedAt is null;
    public void Unfinalize(DateTimeOffset at, Guid accountId, string reason)
    { if (UnfinalizedAt is not null) throw new InvalidOperationException("This finalization is already historical."); if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason)); UnfinalizedAt = at.ToUniversalTime(); UnfinalizedByAccountId = accountId; UnfinalizeReason = reason.Trim(); }
}
