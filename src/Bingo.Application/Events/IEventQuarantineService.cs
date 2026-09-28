namespace Bingo.Application.Events;

public interface IEventQuarantineService
{
    Task<EventQuarantineResult> HideAsync(Guid eventId, long expectedVersion, string? confirmation, string? reason, LifecycleActor actor, CancellationToken ct = default);
    Task<EventQuarantineResult> RestoreAsync(Guid eventId, long expectedVersion, string? confirmation, string? reason, LifecycleActor actor, CancellationToken ct = default);
}

public enum EventQuarantineOutcome { Applied, ValidationFailed, Forbidden, NotFound, Stale, InvalidState }

public sealed record EventQuarantineResult(bool Succeeded, string? Error = null)
{
    public EventQuarantineOutcome Outcome { get; init; } = Succeeded ? EventQuarantineOutcome.Applied : EventQuarantineOutcome.InvalidState;
    public IReadOnlyDictionary<string, string> FieldErrors { get; init; } = new Dictionary<string, string>();
    public bool? Hidden { get; init; }
    public long? Version { get; init; }
}
