namespace Bingo.Application.Events;

public interface IEventQuarantineService
{
    Task<EventQuarantineResult> HideAsync(Guid eventId, long expectedVersion, string? confirmation, string? reason, LifecycleActor actor, CancellationToken ct = default);
    Task<EventQuarantineResult> RestoreAsync(Guid eventId, long expectedVersion, string? confirmation, string? reason, LifecycleActor actor, CancellationToken ct = default);
}

public sealed record EventQuarantineResult(bool Succeeded, string? Error = null);
