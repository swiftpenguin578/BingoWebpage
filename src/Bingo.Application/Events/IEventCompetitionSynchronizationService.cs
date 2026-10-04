using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Integrations.WiseOldMan;

namespace Bingo.Application.Events;

public sealed record EventCompetitionConfigurationResult(bool Succeeded, string? Error = null);
public sealed record EventCompetitionRefreshResult(
    bool Succeeded,
    bool Skipped,
    string? Message = null,
    string? ErrorKind = null,
    DateTimeOffset? RetryAt = null,
    EventCompetitionRefreshSkipReason? SkipReason = null);

public enum EventCompetitionRefreshSkipReason
{
    EventUnavailable, EventNotInFinalReview, IncompleteEventWindow, NoCompetition,
    RefreshInProgress, RetryDelay, NotDue, ServiceUnavailable
}

public sealed record EventCompetitionView(
    int Generation,
    long? CompetitionId,
    string? CompetitionTitle,
    DateTimeOffset? CompetitionStartsAt,
    DateTimeOffset? CompetitionEndsAt,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? LastSuccessfulAt,
    DateTimeOffset? LastUpstreamUpdatedAt,
    bool? Complete,
    IReadOnlyList<string> MissingAccounts,
    string? LastErrorKind,
    string? LastError,
    DateTimeOffset? NormalDueAt,
    DateTimeOffset? RetryDueAt,
    int RetryCount,
    WiseOldManRequestStatus RequestStatus,
    EventCompetitionProvenance Provenance = EventCompetitionProvenance.Unknown,
    EventCompetitionWriteCapability WriteCapability = EventCompetitionWriteCapability.ReadOnly,
    EventCompetitionCredentialStatus CredentialStatus = EventCompetitionCredentialStatus.NotApplicable)
{
    public bool Configured => CompetitionId is not null;
}

public interface IEventCompetitionSynchronizationService
{
    Task<EventCompetitionView?> GetAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<EventCompetitionConfigurationResult> ConfigureAsync(Guid eventId, long expectedEventVersion, long? competitionId, bool synchronizeSchedule, LifecycleActor actor, bool confirmScheduleChanges = false, CancellationToken cancellationToken = default);
    Task<EventCompetitionConfigurationResult> ConfigureAsync(Guid eventId, long expectedEventVersion, long? competitionId, bool synchronizeSchedule, LifecycleActor actor, bool confirmScheduleChanges, bool confirmCompetitionClear, string? competitionClearReason, CancellationToken cancellationToken = default);
    Task<EventCompetitionConfigurationResult> ConfigureAsync(Guid eventId, long expectedEventVersion, long? competitionId, LifecycleActor actor, bool confirmCompetitionClear = false, string? competitionClearReason = null, CancellationToken cancellationToken = default);
    Task<EventCompetitionRefreshResult> RefreshAsync(Guid eventId, LifecycleActor actor, CancellationToken cancellationToken = default);
    Task<EventCompetitionRefreshResult> RefreshForFinalReviewAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<bool> MakeDevelopmentRefreshDueAsync(Guid eventId, LifecycleActor actor, CancellationToken cancellationToken = default);
    Task ProcessDueAsync(CancellationToken cancellationToken = default);
}
