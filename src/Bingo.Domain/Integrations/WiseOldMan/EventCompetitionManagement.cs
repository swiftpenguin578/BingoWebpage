namespace Bingo.Domain.Integrations.WiseOldMan;

public enum EventCompetitionManagementStatus
{
    Active = 1,
    Pending = 2,
    Failed = 3,
    Unknown = 4,
    Conflict = 5,
    Deleted = 6
}

public enum EventCompetitionManagementOperationType
{
    Create = 1,
    Update = 2,
    Delete = 3
}

public enum EventCompetitionManagementOperationPhase
{
    Pending = 1,
    Claimed = 2,
    Sending = 3,
    Succeeded = 4,
    Retry = 5,
    Unknown = 6,
    Failed = 7,
    Cancelled = 8
}

public sealed class EventCompetitionManagement
{
    private EventCompetitionManagement() { }

    public EventCompetitionManagement(
        Guid id,
        Guid eventId,
        Guid synchronizationId,
        long competitionId,
        string title,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string protectedVerificationCode,
        string localFingerprint,
        DateTimeOffset now,
        EventCompetitionProvenance provenance = EventCompetitionProvenance.WebsiteCreated,
        EventCompetitionCredentialStatus credentialStatus = EventCompetitionCredentialStatus.Valid)
    {
        Id = id;
        EventId = eventId;
        SynchronizationId = synchronizationId;
        CompetitionId = competitionId;
        CompetitionTitle = title.Trim();
        CompetitionStartsAt = startsAt.ToUniversalTime();
        CompetitionEndsAt = endsAt.ToUniversalTime();
        ProtectedVerificationCode = protectedVerificationCode;
        Provenance = provenance;
        WriteCapability = string.IsNullOrWhiteSpace(protectedVerificationCode)
            ? EventCompetitionWriteCapability.ReadOnly
            : EventCompetitionWriteCapability.Writable;
        CredentialStatus = credentialStatus;
        LastAppliedLocalFingerprint = localFingerprint;
        ManagementVersion = 1;
        Status = EventCompetitionManagementStatus.Active;
        CreatedAt = UpdatedAt = now.ToUniversalTime();
        LastAppliedAt = now.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid SynchronizationId { get; private set; }
    public long CompetitionId { get; private set; }
    public string CompetitionTitle { get; private set; } = string.Empty;
    public DateTimeOffset CompetitionStartsAt { get; private set; }
    public DateTimeOffset CompetitionEndsAt { get; private set; }
    public string ProtectedVerificationCode { get; private set; } = string.Empty;
    public EventCompetitionProvenance Provenance { get; private set; }
    public EventCompetitionWriteCapability WriteCapability { get; private set; }
    public EventCompetitionCredentialStatus CredentialStatus { get; private set; }
    public DateTimeOffset? CredentialUpdatedAt { get; private set; }
    public DateTimeOffset? CredentialValidatedAt { get; private set; }
    public string ManagedFieldScope { get; private set; } = "title,schedule,teams,participants";
    public EventCompetitionManagementStatus Status { get; private set; }
    public string LastAppliedLocalFingerprint { get; private set; } = string.Empty;
    public string? LastAppliedRemoteFingerprint { get; private set; }
    public string? LastAcknowledgedRosterJson { get; private set; }
    public long ManagementVersion { get; private set; }
    public DateTimeOffset? LastAppliedAt { get; private set; }
    public DateTimeOffset? LastErrorAt { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? LastError { get; private set; }
    public Guid? LastOperationId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public DateTimeOffset? ActualStartedAt { get; private set; }

    public bool CanWrite => WriteCapability == EventCompetitionWriteCapability.Writable
        && CredentialStatus is EventCompetitionCredentialStatus.Unverified or EventCompetitionCredentialStatus.Valid;

    public bool CanDelete => Provenance == EventCompetitionProvenance.WebsiteCreated && CanWrite;

    public void ObserveActualStart(DateTimeOffset? actualStartedAt, DateTimeOffset now)
    {
        if (ActualStartedAt is not null || actualStartedAt is null) return;
        ActualStartedAt = actualStartedAt.Value.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
    }

    public void MarkPending(Guid operationId, DateTimeOffset now)
    {
        Status = EventCompetitionManagementStatus.Pending;
        LastOperationId = operationId;
        LastErrorCode = null;
        LastError = null;
        DeletedAt = null;
        UpdatedAt = now.ToUniversalTime();
    }

    public void MarkApplied(
        Guid operationId,
        string localFingerprint,
        string remoteFingerprint,
        string rosterJson,
        string title,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        DateTimeOffset now)
    {
        Status = EventCompetitionManagementStatus.Active;
        LastOperationId = operationId;
        LastAppliedLocalFingerprint = localFingerprint;
        LastAppliedRemoteFingerprint = remoteFingerprint;
        LastAcknowledgedRosterJson = rosterJson;
        CompetitionTitle = title.Trim();
        CompetitionStartsAt = startsAt.ToUniversalTime();
        CompetitionEndsAt = endsAt.ToUniversalTime();
        LastAppliedAt = now.ToUniversalTime();
        LastErrorAt = null;
        LastErrorCode = null;
        LastError = null;
        DeletedAt = null;
        UpdatedAt = now.ToUniversalTime();
        ManagementVersion++;
    }

    public void ReplaceProtectedCredential(string protectedVerificationCode, DateTimeOffset now)
    {
        ProtectedVerificationCode = protectedVerificationCode;
        WriteCapability = string.IsNullOrWhiteSpace(protectedVerificationCode)
            ? EventCompetitionWriteCapability.ReadOnly
            : EventCompetitionWriteCapability.Writable;
        CredentialStatus = string.IsNullOrWhiteSpace(protectedVerificationCode)
            ? EventCompetitionCredentialStatus.Unavailable
            : EventCompetitionCredentialStatus.Unverified;
        CredentialUpdatedAt = now.ToUniversalTime();
        CredentialValidatedAt = null;
        UpdatedAt = now.ToUniversalTime();
    }

    public void AdoptProtectedCredential(string protectedVerificationCode, DateTimeOffset now)
        => ReplaceProtectedCredential(protectedVerificationCode, now);

    public void MarkCredentialValid(DateTimeOffset now)
    {
        WriteCapability = EventCompetitionWriteCapability.Writable;
        CredentialStatus = EventCompetitionCredentialStatus.Valid;
        CredentialValidatedAt = now.ToUniversalTime();
        CredentialUpdatedAt ??= now.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
    }

    public void MarkCredentialInvalid(DateTimeOffset now)
    {
        CredentialStatus = EventCompetitionCredentialStatus.Invalid;
        CredentialValidatedAt = now.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
    }

    public void MarkCredentialRevoked(DateTimeOffset now)
    {
        CredentialStatus = EventCompetitionCredentialStatus.Revoked;
        CredentialValidatedAt = now.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
    }

    public void MarkCredentialUnavailable(DateTimeOffset now)
    {
        CredentialStatus = EventCompetitionCredentialStatus.Unavailable;
        UpdatedAt = now.ToUniversalTime();
    }

    public void MarkFailure(Guid operationId, EventCompetitionManagementStatus status, string code, string error, DateTimeOffset now)
    {
        Status = status;
        LastOperationId = operationId;
        LastErrorAt = now.ToUniversalTime();
        LastErrorCode = code;
        LastError = error;
        UpdatedAt = now.ToUniversalTime();
    }

    public void MarkDeleted(Guid operationId, DateTimeOffset now)
    {
        Status = EventCompetitionManagementStatus.Deleted;
        LastOperationId = operationId;
        DeletedAt = now.ToUniversalTime();
        LastErrorAt = null;
        LastErrorCode = null;
        LastError = null;
        UpdatedAt = now.ToUniversalTime();
        ManagementVersion++;
    }

    public void UpdateMetadata(
        long? competitionId,
        string? title,
        DateTimeOffset? competitionStartsAt,
        DateTimeOffset? competitionEndsAt,
        DateTimeOffset now)
    {
        CompetitionId = competitionId ?? CompetitionId;
        CompetitionTitle = title?.Trim() ?? CompetitionTitle;
        if (competitionStartsAt is { } startsAt) CompetitionStartsAt = startsAt.ToUniversalTime();
        if (competitionEndsAt is { } endsAt) CompetitionEndsAt = endsAt.ToUniversalTime();
        LastAppliedAt = now.ToUniversalTime();
        LastErrorAt = null;
        LastErrorCode = null;
        LastError = null;
        UpdatedAt = now.ToUniversalTime();
    }

    public void RetireExternalConnection(DateTimeOffset now)
    {
        if (Provenance != EventCompetitionProvenance.External)
            throw new InvalidOperationException("Only an external connection can be retired locally.");
        MarkDeleted(Guid.Empty, now);
        LastOperationId = null;
        ProtectedVerificationCode = string.Empty;
        WriteCapability = EventCompetitionWriteCapability.ReadOnly;
        CredentialStatus = EventCompetitionCredentialStatus.NotApplicable;
        CredentialUpdatedAt = now.ToUniversalTime();
        CredentialValidatedAt = null;
        LastAppliedAt = null;
        LastAppliedLocalFingerprint = string.Empty;
        LastAppliedRemoteFingerprint = null;
        LastAcknowledgedRosterJson = null;
    }

    public void RebindExternalConnection(Guid synchronizationId, long competitionId, string title,
        DateTimeOffset startsAt, DateTimeOffset endsAt, string protectedCode, DateTimeOffset now)
    {
        if (Status != EventCompetitionManagementStatus.Deleted)
            throw new InvalidOperationException("The previous connection must be retired first.");
        SynchronizationId = synchronizationId;
        CompetitionId = competitionId;
        CompetitionTitle = title;
        CompetitionStartsAt = startsAt.ToUniversalTime();
        CompetitionEndsAt = endsAt.ToUniversalTime();
        Provenance = EventCompetitionProvenance.External;
        Status = EventCompetitionManagementStatus.Active;
        DeletedAt = null;
        LastOperationId = null;
        LastAppliedAt = null;
        LastAppliedLocalFingerprint = string.Empty;
        LastAppliedRemoteFingerprint = null;
        LastAcknowledgedRosterJson = null;
        LastErrorAt = null;
        LastErrorCode = LastError = null;
        ReplaceProtectedCredential(protectedCode, now);
        ManagementVersion++;
    }

    public void RebindWebsiteCreatedConnection(Guid synchronizationId, long competitionId)
    {
        SynchronizationId = synchronizationId;
        CompetitionId = competitionId;
        Provenance = EventCompetitionProvenance.WebsiteCreated;
    }
}

public sealed class EventCompetitionManagementOperation
{
    private EventCompetitionManagementOperation() { }

    public EventCompetitionManagementOperation(
        Guid id,
        Guid eventId,
        Guid? managementId,
        EventCompetitionManagementOperationType type,
        string desiredPayloadJson,
        string desiredFingerprint,
        long eventVersion,
        DateTimeOffset now)
    {
        Id = id;
        EventId = eventId;
        ManagementId = managementId;
        Type = type;
        DesiredPayloadJson = desiredPayloadJson;
        DesiredFingerprint = desiredFingerprint;
        EventVersion = eventVersion;
        Phase = EventCompetitionManagementOperationPhase.Pending;
        CreatedAt = UpdatedAt = now.ToUniversalTime();
        NextAttemptAt = now.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid? ManagementId { get; private set; }
    public EventCompetitionManagementOperationType Type { get; private set; }
    public string DesiredPayloadJson { get; private set; } = string.Empty;
    public string DesiredFingerprint { get; private set; } = string.Empty;
    public EventCompetitionManagementOperationPhase Phase { get; private set; }
    public long EventVersion { get; private set; }
    public Guid? ActorAccountId { get; private set; }
    public string? ActorUsername { get; private set; }
    public long? RemoteCompetitionId { get; private set; }
    public string? RemoteReceiptReference { get; private set; }
    public string? SafeErrorCode { get; private set; }
    public string? SafeError { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ClaimedAt { get; private set; }
    public DateTimeOffset? SendingAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public long Version { get; private set; } = 1;

    public void SetActor(Guid accountId, string username)
    {
        ActorAccountId = accountId;
        ActorUsername = username;
    }

    public void ReplaceDesired(string desiredPayloadJson, string desiredFingerprint, long eventVersion, DateTimeOffset now)
    {
        if (Phase is not (EventCompetitionManagementOperationPhase.Pending or EventCompetitionManagementOperationPhase.Retry))
            throw new InvalidOperationException("Only a pending WOM operation can be coalesced.");
        DesiredPayloadJson = desiredPayloadJson;
        DesiredFingerprint = desiredFingerprint;
        EventVersion = eventVersion;
        NextAttemptAt = now.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
        Version++;
    }

    public void RequeueCurrent(string desiredPayloadJson, string desiredFingerprint, long eventVersion, DateTimeOffset now)
    {
        if (Phase != EventCompetitionManagementOperationPhase.Sending)
            throw new InvalidOperationException("Only a sending WOM operation can be refreshed.");
        DesiredPayloadJson = desiredPayloadJson;
        DesiredFingerprint = desiredFingerprint;
        EventVersion = eventVersion;
        Phase = EventCompetitionManagementOperationPhase.Pending;
        ClaimedAt = null;
        SendingAt = null;
        NextAttemptAt = now.ToUniversalTime();
        SafeErrorCode = null;
        SafeError = null;
        UpdatedAt = now.ToUniversalTime();
        Version++;
    }

    public void Claim(DateTimeOffset now)
    {
        if (Phase is not (EventCompetitionManagementOperationPhase.Pending or EventCompetitionManagementOperationPhase.Retry))
            throw new InvalidOperationException("This WOM management operation is no longer claimable.");
        Phase = EventCompetitionManagementOperationPhase.Sending;
        AttemptCount++;
        ClaimedAt = now.ToUniversalTime();
        SendingAt = now.ToUniversalTime();
        NextAttemptAt = null;
        UpdatedAt = now.ToUniversalTime();
        Version++;
    }

    public void Succeed(long? remoteCompetitionId, string? receiptReference, DateTimeOffset now)
    {
        Phase = EventCompetitionManagementOperationPhase.Succeeded;
        RemoteCompetitionId = remoteCompetitionId;
        RemoteReceiptReference = receiptReference;
        CompletedAt = UpdatedAt = now.ToUniversalTime();
        SafeErrorCode = null;
        SafeError = null;
        Version++;
    }

    public void Retry(DateTimeOffset retryAt, string code, string error, DateTimeOffset now)
    {
        Phase = EventCompetitionManagementOperationPhase.Retry;
        SafeErrorCode = code;
        SafeError = error;
        NextAttemptAt = retryAt.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
        Version++;
    }

    public void MarkUnknown(string code, string error, DateTimeOffset now, DateTimeOffset? retryAt = null)
    {
        Phase = EventCompetitionManagementOperationPhase.Unknown;
        SafeErrorCode = code;
        SafeError = error;
        NextAttemptAt = retryAt?.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
        Version++;
    }

    public void ScheduleReconciliation(DateTimeOffset retryAt, DateTimeOffset now)
    {
        if (Phase != EventCompetitionManagementOperationPhase.Unknown)
            throw new InvalidOperationException("Only an unknown WOM operation can be reconciled.");
        NextAttemptAt = retryAt.ToUniversalTime();
        AttemptCount++;
        UpdatedAt = now.ToUniversalTime();
        Version++;
    }

    public void ClearReconciliation(DateTimeOffset now)
    {
        NextAttemptAt = null;
        UpdatedAt = now.ToUniversalTime();
        Version++;
    }

    public void Fail(string code, string error, DateTimeOffset now)
    {
        Phase = EventCompetitionManagementOperationPhase.Failed;
        SafeErrorCode = code;
        SafeError = error;
        CompletedAt = UpdatedAt = now.ToUniversalTime();
        Version++;
    }

    public void Cancel(DateTimeOffset now)
    {
        Phase = EventCompetitionManagementOperationPhase.Cancelled;
        CompletedAt = UpdatedAt = now.ToUniversalTime();
        Version++;
    }
}
