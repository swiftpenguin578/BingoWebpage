namespace Bingo.Application.Events;

public interface IEventDestructiveLifecycleService
{
    Task<LifecycleMutationResult> DiscardAsync(Guid eventId, long version, bool confirmed, LifecycleActor actor, CancellationToken ct = default);
    Task<LifecycleMutationResult> CancelAsync(Guid eventId, long version, bool confirmed, string? reason, LifecycleActor actor, CancellationToken ct = default);
}

public sealed record LifecycleMutationResult(bool Succeeded, string? Error = null);
