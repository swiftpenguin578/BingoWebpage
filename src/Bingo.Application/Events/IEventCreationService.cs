namespace Bingo.Application.Events;

public interface IEventCreationService
{
    Task<EventCreationResult> CreateAsync(Guid requestId, string? name, string? timezone, LifecycleActor actor, CancellationToken ct = default);
    Task<EventCreationResult> CheckAgainAsync(Guid requestId, LifecycleActor actor, CancellationToken ct = default);
}

public enum EventCreationOutcome { Completed, ValidationFailed, Conflict, Forbidden, NotFound, SlugUnavailable }

public sealed record EventCreationResult(EventCreationOutcome Outcome, Guid? EventId = null, string? Name = null, string? Error = null, string? Field = null);
