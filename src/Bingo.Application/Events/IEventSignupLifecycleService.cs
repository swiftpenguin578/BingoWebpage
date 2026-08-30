using Bingo.Domain.Events;

namespace Bingo.Application.Events;

public enum SignupOpeningMode { OpenNow, ScheduleOpening, ScheduledExecution, Reopen }
public sealed record ReadinessItem(string Code, string Description, string? Route = null);
public sealed record SignupCloseDecision(bool IsValid, DateTimeOffset? ProposedClose)
{
    public bool RequiresAcceptance => !IsValid && ProposedClose is not null;

    public static SignupCloseDecision Evaluate(DateTimeOffset? signupClosesAt, DateTimeOffset? draftAt, DateTimeOffset? eventStartsAt, DateTimeOffset now)
    {
        now = now.ToUniversalTime();
        var valid = signupClosesAt is { } close && close > now && eventStartsAt is { } start && close <= start;
        DateTimeOffset? proposed = eventStartsAt is { } futureStart && futureStart > now
            ? draftAt is { } draft && draft > now && draft <= futureStart ? draft : futureStart
            : null;
        return new(valid, proposed);
    }
}
public sealed record SignupReadiness(IReadOnlyList<ReadinessItem> Blockers, IReadOnlyList<ReadinessItem> Warnings, IReadOnlyList<ReadinessItem> LaterTasks, SignupCloseDecision CloseDecision)
{ public bool CanProceed => Blockers.Count == 0; }
public sealed record EventScheduleValues(DateTimeOffset? SignupOpensAt, DateTimeOffset? SignupClosesAt, DateTimeOffset? DraftAt, DateTimeOffset? EventStartsAt, DateTimeOffset? EventEndsAt, int? ParticipantCap, bool ScheduledSignupOpeningEnabled);
public sealed record LifecycleActor(Guid Id, string Username);
public sealed record SignupLifecycleResult(bool Succeeded, string? Error = null, DateTimeOffset? ProposedClose = null, int PromotedParticipants = 0);
public sealed record EventStartReadiness(IReadOnlyList<ReadinessItem> Blockers) { public bool CanProceed => Blockers.Count == 0; }
public sealed record EventStartResult(bool Succeeded, string? Error = null, IReadOnlyList<ReadinessItem>? Blockers = null);

public interface IEventReadinessEvaluator
{
    Task<SignupReadiness?> GetSignupReadinessAsync(Guid eventId, SignupOpeningMode mode, DateTimeOffset now, CancellationToken ct = default);
    Task<SignupReadiness?> GetSignupReadinessAsync(Guid eventId, SignupOpeningMode mode, DateTimeOffset now, EventScheduleValues proposedValues, CancellationToken ct = default);
}

public interface IEventSignupLifecycleService
{
    Task<SignupLifecycleResult> SaveScheduleAsync(Guid eventId, long version, EventScheduleValues values, bool confirmChanges, LifecycleActor actor, CancellationToken ct = default);
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
