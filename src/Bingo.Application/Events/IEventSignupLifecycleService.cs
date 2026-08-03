using Bingo.Domain.Events;

namespace Bingo.Application.Events;

public enum SignupOpeningMode { OpenNow, ScheduleOpening, ScheduledExecution, Reopen }
public sealed record ReadinessItem(string Code, string Description, string? Route = null);
public sealed record SignupReadiness(IReadOnlyList<ReadinessItem> Blockers, IReadOnlyList<ReadinessItem> Warnings, IReadOnlyList<ReadinessItem> LaterTasks)
{ public bool CanProceed => Blockers.Count == 0; }
public sealed record EventScheduleValues(DateTimeOffset? SignupOpensAt, DateTimeOffset? SignupClosesAt, DateTimeOffset? DraftAt, DateTimeOffset? EventStartsAt, DateTimeOffset? EventEndsAt, int? ParticipantCap);
public sealed record LifecycleActor(Guid Id, string Username);
public sealed record SignupLifecycleResult(bool Succeeded, string? Error = null, DateTimeOffset? ProposedClose = null, int PromotedParticipants = 0);
public sealed record EventStartReadiness(IReadOnlyList<ReadinessItem> Blockers) { public bool CanProceed => Blockers.Count == 0; }
public sealed record EventStartResult(bool Succeeded, string? Error = null, IReadOnlyList<ReadinessItem>? Blockers = null);

public interface IEventReadinessEvaluator
{
    Task<SignupReadiness?> GetSignupReadinessAsync(Guid eventId, SignupOpeningMode mode, DateTimeOffset now, CancellationToken ct = default);
}

public interface IEventSignupLifecycleService
{
    Task<SignupLifecycleResult> SaveScheduleAsync(Guid eventId, long version, EventScheduleValues values, bool acknowledgeScheduledWarnings, bool confirmPublicChange, string? reason, LifecycleActor actor, CancellationToken ct = default);
    Task<SignupLifecycleResult> OpenAsync(Guid eventId, long version, bool acknowledgeWarnings, bool acceptProposedClose, LifecycleActor actor, CancellationToken ct = default);
    Task<SignupLifecycleResult> CloseAsync(Guid eventId, long version, LifecycleActor actor, CancellationToken ct = default);
    Task<SignupLifecycleResult> ReopenAsync(Guid eventId, long version, bool acknowledgeWarnings, bool acceptProposedClose, LifecycleActor actor, CancellationToken ct = default);
    Task ProcessDueSignupAsync(CancellationToken ct = default);
}

public interface IEventLifecycleService
{
    Task ProcessDueAsync(CancellationToken ct = default);
    Task<EventStartReadiness?> GetStartReadinessAsync(Guid eventId, CancellationToken ct = default);
    Task<EventStartResult> StartNowAsync(Guid eventId, long version, bool confirmed, string? reason, LifecycleActor actor, CancellationToken ct = default);
    Task<EventStartResult> EndNowAsync(Guid eventId, long version, bool confirmed, string? reason, LifecycleActor actor, CancellationToken ct = default);
    Task<EventStartResult> ResumePrematureEndAsync(Guid eventId, long version, bool confirmed, string? reason, DateTimeOffset replacementEventEndsAt, LifecycleActor actor, CancellationToken ct = default);
}
