using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Integrations.WiseOldMan;

namespace Bingo.Application.Events;

public sealed record EventCompetitionManagementTeamView(
    Guid TeamId,
    string Name,
    IReadOnlyList<string> Participants,
    bool Empty);

public sealed record EventCompetitionManagementPreview(
    Guid EventId,
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    IReadOnlyList<EventCompetitionManagementTeamView> Teams,
    IReadOnlyList<string> Errors,
    string Fingerprint,
    bool IsFinalizedPreLive,
    bool HasManualLink,
    bool HasManagedLink)
{
    public bool Valid => Errors.Count == 0 && IsFinalizedPreLive && !HasManualLink && !HasManagedLink;
}

public sealed record EventCompetitionManagementView(
    bool Enabled,
    string Status,
    long? CompetitionId,
    string? CompetitionTitle,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    string? LastErrorCode,
    string? LastError,
    DateTimeOffset? LastAppliedAt,
    DateTimeOffset? PendingAt,
    DateTimeOffset? LastOperationAt,
    string? LastOperationType,
    string? CredentialState,
    bool RosterLocked,
    EventCompetitionManagementPreview? Preview = null,
    EventCompetitionProvenance Provenance = EventCompetitionProvenance.Unknown,
    EventCompetitionWriteCapability WriteCapability = EventCompetitionWriteCapability.Unknown,
    bool CanWrite = false,
    bool CanDelete = false,
    Guid? OperationId = null,
    EventCompetitionManagementOperationPhase? OperationPhase = null,
    EventCompetitionManagementOperationType? OperationType = null,
    DateTimeOffset? NextAttemptAt = null,
    EventCompetitionCredentialStatus CredentialStatus = EventCompetitionCredentialStatus.NotApplicable,
    bool CanCreate = false, bool CanManageCredential = false, DateTimeOffset? EndUpdateNextAttemptAt = null);

public interface IEventCompetitionManagementService
{
    Task<EventCompetitionManagementPreview?> PreviewAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<EventCompetitionManagementView?> GetAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<EventCompetitionManagementResult> CreateAsync(Guid eventId, long expectedEventVersion, LifecycleActor actor, CancellationToken cancellationToken = default);
    Task<EventCompetitionManagementResult> AdoptCredentialAsync(Guid eventId, long expectedEventVersion, string verificationCode, LifecycleActor actor, CancellationToken cancellationToken = default);
    Task<EventCompetitionManagementResult> ReplaceCredentialAsync(Guid eventId, long expectedEventVersion, string verificationCode, LifecycleActor actor, CancellationToken cancellationToken = default);
    Task<EventCompetitionManagementResult> QueueUpdateAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<EventCompetitionManagementResult> DeleteAsync(Guid eventId, long expectedEventVersion, long targetCompetitionId, bool confirmed, LifecycleActor actor, CancellationToken cancellationToken = default);
    Task ProcessDueAsync(CancellationToken cancellationToken = default);
}

public sealed record EventCompetitionManagementResult(
    bool Succeeded,
    string? Error = null,
    Guid? OperationId = null,
    string? Status = null,
    DateTimeOffset? RetryAt = null,
    string? ErrorCode = null,
    IReadOnlyList<string>? AffectedParticipants = null)
{
    public bool Pending => Status is "Pending" or "Sending" or "Unknown" or "Retry";
}

public interface ICompetitionCredentialProtector
{
    string Protect(string verificationCode);
    string Unprotect(string protectedCode);
}
