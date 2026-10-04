namespace Bingo.Domain.Integrations.WiseOldMan;

public enum EventCompetitionEndUpdateStatus
{
    NotRequired = 0, Pending = 1, Succeeded = 2, Rejected = 3, CouldNotUpdate = 4
}

public sealed class EventCompetitionSynchronization
{
    private static readonly TimeSpan NormalSlotInterval = TimeSpan.FromHours(1);

    private EventCompetitionSynchronization() { }

    public EventCompetitionSynchronization(
        Guid id, Guid eventId, int generation, long? competitionId, string? title,
        DateTimeOffset? competitionStartsAt, DateTimeOffset? competitionEndsAt,
        string assignmentFingerprint, DateTimeOffset now,
        EventCompetitionProvenance provenance = EventCompetitionProvenance.Unknown)
    {
        Id = id;
        EventId = eventId;
        Generation = generation;
        CompetitionId = competitionId;
        CompetitionTitle = title?.Trim();
        CompetitionStartsAt = competitionStartsAt?.ToUniversalTime();
        CompetitionEndsAt = competitionEndsAt?.ToUniversalTime();
        AssignmentFingerprint = assignmentFingerprint;
        Provenance = provenance;
        CycleStartedAt = now.ToUniversalTime();
        NormalDueAt = competitionId is null ? null : now.ToUniversalTime();
    }

    public EventCompetitionEndUpdateStatus EndUpdateStatus { get; private set; }
    public DateTimeOffset? EndUpdateTargetAt { get; private set; }
    public DateTimeOffset? EndUpdateRequestedAt { get; private set; }
    public string? EndUpdateErrorCode { get; private set; }

    public void RequestEndUpdate(DateTimeOffset target, DateTimeOffset now)
    {
        if (CompetitionId is null) return;
        EndUpdateTargetAt = target.ToUniversalTime();
        EndUpdateRequestedAt = now.ToUniversalTime();
        EndUpdateStatus = EventCompetitionEndUpdateStatus.Pending;
        EndUpdateErrorCode = null;
    }

    public bool HasUnmatchedEnd(DateTimeOffset? configuredEnd) => CompetitionId is not null &&
        (CompetitionEndsAt != configuredEnd || EndUpdateStatus is EventCompetitionEndUpdateStatus.Pending
            or EventCompetitionEndUpdateStatus.Rejected or EventCompetitionEndUpdateStatus.CouldNotUpdate);

    public void MarkEndCouldNotBeUpdated()
    {
        EndUpdateStatus = EventCompetitionEndUpdateStatus.CouldNotUpdate;
    }

    public void RejectEndUpdate(DateTimeOffset target, string code)
    {
        if (EndUpdateStatus != EventCompetitionEndUpdateStatus.Pending || EndUpdateTargetAt != target) return;
        EndUpdateStatus = EventCompetitionEndUpdateStatus.Rejected;
        EndUpdateErrorCode = code.Length <= 100 ? code : code[..100];
    }

    public void CompleteEndUpdate(DateTimeOffset target)
    {
        if (EndUpdateStatus != EventCompetitionEndUpdateStatus.Pending || EndUpdateTargetAt != target) return;
        EndUpdateStatus = EventCompetitionEndUpdateStatus.Succeeded;
        EndUpdateErrorCode = null;
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public int Generation { get; private set; }
    public long? CompetitionId { get; private set; }
    public string? CompetitionTitle { get; private set; }
    public DateTimeOffset? CompetitionStartsAt { get; private set; }
    public DateTimeOffset? CompetitionEndsAt { get; private set; }
    public EventCompetitionProvenance Provenance { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public DateTimeOffset? LastSuccessfulAt { get; private set; }
    public DateTimeOffset? LastUpstreamUpdatedAt { get; private set; }
    public string AssignmentFingerprint { get; private set; } = string.Empty;
    public bool? LatestComplete { get; private set; }
    public string? MissingAccountsJson { get; private set; }
    public string? LastErrorKind { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? CycleStartedAt { get; private set; }
    public DateTimeOffset? NormalDueAt { get; private set; }
    public DateTimeOffset? RetryDueAt { get; private set; }
    public int RetryCount { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public string? SourceRequestFingerprint { get; private set; }
    public Guid? MetricActivityBatchId { get; private set; }
    public bool? LatestMetricsComplete { get; private set; }
    public DateTimeOffset? LastMetricAttemptAt { get; private set; }

    public void SetSourceRequest(string fingerprint)
    {
        if (SourceRequestFingerprint == fingerprint) return;
        SourceRequestFingerprint = fingerprint;
        LatestMetricsComplete = false;
    }

    public void MarkMetricBatch(Guid batchId, bool complete, DateTimeOffset attemptedAt)
    {
        MetricActivityBatchId = batchId; LatestMetricsComplete = complete; LastMetricAttemptAt = attemptedAt.ToUniversalTime();
    }

    public void MarkMetricFailure(DateTimeOffset attemptedAt)
    {
        LatestMetricsComplete = false; LastMetricAttemptAt = attemptedAt.ToUniversalTime();
    }

    public void AcquireLease(string owner, DateTimeOffset expiresAt)
    {
        LeaseOwner = owner;
        LeaseExpiresAt = expiresAt.ToUniversalTime();
    }

    public void ReleaseLease(string owner)
    {
        if (LeaseOwner == owner)
        {
            LeaseOwner = null;
            LeaseExpiresAt = null;
        }
    }

    public void Reconfigure(
        long? competitionId, string? title, DateTimeOffset? competitionStartsAt,
        DateTimeOffset? competitionEndsAt, string assignmentFingerprint, DateTimeOffset now,
        EventCompetitionProvenance? provenance = null)
    {
        Generation++;
        SourceRequestFingerprint = null; MetricActivityBatchId = null; LatestMetricsComplete = null; LastMetricAttemptAt = null;
        CompetitionId = competitionId;
        CompetitionTitle = title?.Trim();
        CompetitionStartsAt = competitionStartsAt?.ToUniversalTime();
        CompetitionEndsAt = competitionEndsAt?.ToUniversalTime();
        if (provenance is { } source) Provenance = source;
        AssignmentFingerprint = assignmentFingerprint;
        LastAttemptAt = null;
        LastSuccessfulAt = null;
        LastUpstreamUpdatedAt = null;
        LatestComplete = null;
        MissingAccountsJson = null;
        LastErrorKind = null;
        LastError = null;
        CycleStartedAt = now.ToUniversalTime();
        NormalDueAt = competitionId is null ? null : now.ToUniversalTime();
        RetryDueAt = null;
        RetryCount = 0;
        LeaseOwner = null;
        LeaseExpiresAt = null;
    }

    public void UpdateMetadata(
        long? competitionId,
        string? title,
        DateTimeOffset? competitionStartsAt,
        DateTimeOffset? competitionEndsAt,
        DateTimeOffset now,
        EventCompetitionProvenance? provenance = null)
    {
        CompetitionId = competitionId;
        CompetitionTitle = title?.Trim();
        CompetitionStartsAt = competitionStartsAt?.ToUniversalTime();
        CompetitionEndsAt = competitionEndsAt?.ToUniversalTime();
        if (provenance is { } source) Provenance = source;
        CycleStartedAt ??= now.ToUniversalTime();
    }

    public void BeginReplacementGeneration(string assignmentFingerprint, DateTimeOffset now)
    {
        Generation++;
        MetricActivityBatchId = null; LatestMetricsComplete = false;
        AssignmentFingerprint = assignmentFingerprint;
        LastAttemptAt = now.ToUniversalTime();
        LastSuccessfulAt = null;
        LastUpstreamUpdatedAt = null;
        LatestComplete = false;
        MissingAccountsJson = null;
        LastErrorKind = "AssignmentChanged";
        LastError = "The current Playing assignment set changed; a replacement Wise Old Man generation is due.";
        CycleStartedAt = now.ToUniversalTime();
        NormalDueAt = now.ToUniversalTime();
        RetryDueAt = null;
        RetryCount = 0;
    }

    public void BeginNormalCycle(DateTimeOffset now)
    {
        now = now.ToUniversalTime();
        CycleStartedAt = now;
        // This compatibility path has no actual Live-start anchor, so it must not
        // invent a rolling schedule. Anchored callers use BeginNormalAttempt below.
        NormalDueAt = null;
        RetryDueAt = null;
        RetryCount = 0;
    }

    public void MakeNormalRefreshDue(DateTimeOffset now) => NormalDueAt = now.ToUniversalTime();

    public void PrepareDevelopmentRefreshDue(DateTimeOffset now)
    {
        now = now.ToUniversalTime();
        LastSuccessfulAt = now.Subtract(NormalSlotInterval);
        NormalDueAt = now;
        RetryDueAt = null;
    }

    public static DateTimeOffset FirstNormalSlot(DateTimeOffset actualStartedAt) =>
        actualStartedAt.ToUniversalTime().Add(NormalSlotInterval);

    public static DateTimeOffset? CurrentNormalSlot(DateTimeOffset actualStartedAt, DateTimeOffset now)
    {
        var first = FirstNormalSlot(actualStartedAt);
        now = now.ToUniversalTime();
        if (now < first) return null;

        var elapsedSlots = (now - first).Ticks / NormalSlotInterval.Ticks;
        return first.AddTicks(elapsedSlots * NormalSlotInterval.Ticks);
    }

    public static DateTimeOffset NextNormalSlot(DateTimeOffset actualStartedAt, DateTimeOffset now) =>
        CurrentNormalSlot(actualStartedAt, now) is { } current
            ? current.Add(NormalSlotInterval)
            : FirstNormalSlot(actualStartedAt);

    /// <summary>
    /// Lazily repairs the persisted normal due time to the fixed schedule for an existing Live event.
    /// A due value that is already in the past is intentionally left alone so an explicit/urgent
    /// refresh remains due; the next completed attempt advances to the next fixed future slot.
    /// </summary>
    public void ReconcileNormalSlot(DateTimeOffset? actualStartedAt, DateTimeOffset now)
    {
        if (actualStartedAt is not { } anchor)
        {
            NormalDueAt = null;
            return;
        }

        now = now.ToUniversalTime();
        var current = CurrentNormalSlot(anchor, now);
        var next = NextNormalSlot(anchor, now);
        var currentConsumed = current is { } currentSlot && LastAttemptAt is { } lastAttempt && lastAttempt >= currentSlot;
        if (NormalDueAt is null)
        {
            NormalDueAt = currentConsumed ? next : current ?? next;
            return;
        }

        var due = NormalDueAt.Value.ToUniversalTime();
        if (due <= now && LastAttemptAt is null && LastSuccessfulAt is null)
        {
            NormalDueAt = current ?? next;
            return;
        }

        if (due > now && due == next && current is { } slot && !currentConsumed)
        {
            NormalDueAt = slot;
            return;
        }

        if (due > now && due != next)
            NormalDueAt = currentConsumed ? next : current ?? next;
    }

    public void AdvanceNormalSlot(DateTimeOffset? actualStartedAt, DateTimeOffset now)
    {
        now = now.ToUniversalTime();
        NormalDueAt = actualStartedAt is { } anchor
            ? NextNormalSlot(anchor, now)
            : null;
    }

    public void BeginNormalAttempt(DateTimeOffset? actualStartedAt, DateTimeOffset now)
    {
        now = now.ToUniversalTime();
        CycleStartedAt = now;
        RetryDueAt = null;
        RetryCount = 0;
        AdvanceNormalSlot(actualStartedAt, now);
    }

    public void MarkAttempt(DateTimeOffset now)
    {
        LastAttemptAt = now.ToUniversalTime();
        if (CycleStartedAt is null) CycleStartedAt = now.ToUniversalTime();
    }

    public void MarkSuccess(DateTimeOffset now, DateTimeOffset? upstreamUpdatedAt, bool complete, string? missingAccounts, string? error,
        DateTimeOffset? actualStartedAt = null)
    {
        now = now.ToUniversalTime();
        LastAttemptAt = now;
        LastSuccessfulAt = now;
        LastUpstreamUpdatedAt = upstreamUpdatedAt?.ToUniversalTime();
        LatestComplete = complete;
        MissingAccountsJson = missingAccounts;
        LastErrorKind = complete ? null : "Incomplete";
        LastError = error;
        CycleStartedAt = now;
        AdvanceNormalSlot(actualStartedAt, now);
        RetryDueAt = null;
        RetryCount = 0;
    }

    public void MarkHistoricalSuccess(DateTimeOffset fetchedAt, DateTimeOffset? upstreamUpdatedAt)
    {
        fetchedAt = fetchedAt.ToUniversalTime();
        LastAttemptAt = fetchedAt;
        LastSuccessfulAt = fetchedAt;
        LastUpstreamUpdatedAt = upstreamUpdatedAt?.ToUniversalTime();
        LatestComplete = true;
        MissingAccountsJson = null;
        LastErrorKind = null;
        LastError = null;
        CycleStartedAt = fetchedAt;
        NormalDueAt = null;
        RetryDueAt = null;
        RetryCount = 0;
        LeaseOwner = null;
        LeaseExpiresAt = null;
    }

    public void MarkFailure(DateTimeOffset now, string kind, string error, DateTimeOffset? retryAt,
        DateTimeOffset? actualStartedAt = null)
    {
        now = now.ToUniversalTime();
        LastAttemptAt = now;
        LatestComplete ??= false;
        LastErrorKind = kind;
        LastError = error;
        if (CycleStartedAt is null) CycleStartedAt = now;
        AdvanceNormalSlot(actualStartedAt, now);
        if (retryAt is { } due && RetryCount < 3)
        {
            RetryCount++;
            RetryDueAt = due.ToUniversalTime();
        }
        else
        {
            RetryCount = 4;
            RetryDueAt = null;
        }
    }
}

public sealed class EventCompetitionCharacterActivity
{
    private EventCompetitionCharacterActivity() { }

    public EventCompetitionCharacterActivity(
        Guid id, Guid eventId, int generation, long competitionId, Guid osrsCharacterId,
        decimal gainedEhb, DateTimeOffset fetchedAt, DateTimeOffset? upstreamUpdatedAt, string assignmentFingerprint,
        decimal? startEhb = null, decimal? endEhb = null)
    {
        Id = id;
        EventId = eventId;
        Generation = generation;
        CompetitionId = competitionId;
        OsrsCharacterId = osrsCharacterId;
        GainedEhb = gainedEhb;
        StartEhb = startEhb;
        EndEhb = endEhb;
        FetchedAt = fetchedAt.ToUniversalTime();
        UpstreamUpdatedAt = upstreamUpdatedAt?.ToUniversalTime();
        AssignmentFingerprint = assignmentFingerprint;
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public int Generation { get; private set; }
    public long CompetitionId { get; private set; }
    public Guid OsrsCharacterId { get; private set; }
    public decimal GainedEhb { get; private set; }
    public decimal? StartEhb { get; private set; }
    public decimal? EndEhb { get; private set; }
    public DateTimeOffset FetchedAt { get; private set; }
    public DateTimeOffset? UpstreamUpdatedAt { get; private set; }
    public string AssignmentFingerprint { get; private set; } = string.Empty;
}
