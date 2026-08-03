namespace Bingo.Domain.Integrations.WiseOldMan;

public sealed class EventCompetitionSynchronization
{
    private EventCompetitionSynchronization() { }

    public EventCompetitionSynchronization(
        Guid id, Guid eventId, int generation, long? competitionId, string? title,
        DateTimeOffset? competitionStartsAt, DateTimeOffset? competitionEndsAt,
        string assignmentFingerprint, DateTimeOffset now)
    {
        Id = id;
        EventId = eventId;
        Generation = generation;
        CompetitionId = competitionId;
        CompetitionTitle = title?.Trim();
        CompetitionStartsAt = competitionStartsAt?.ToUniversalTime();
        CompetitionEndsAt = competitionEndsAt?.ToUniversalTime();
        AssignmentFingerprint = assignmentFingerprint;
        CycleStartedAt = now.ToUniversalTime();
        NormalDueAt = competitionId is null ? null : now.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public int Generation { get; private set; }
    public long? CompetitionId { get; private set; }
    public string? CompetitionTitle { get; private set; }
    public DateTimeOffset? CompetitionStartsAt { get; private set; }
    public DateTimeOffset? CompetitionEndsAt { get; private set; }
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
        DateTimeOffset? competitionEndsAt, string assignmentFingerprint, DateTimeOffset now)
    {
        Generation++;
        CompetitionId = competitionId;
        CompetitionTitle = title?.Trim();
        CompetitionStartsAt = competitionStartsAt?.ToUniversalTime();
        CompetitionEndsAt = competitionEndsAt?.ToUniversalTime();
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

    public void BeginReplacementGeneration(string assignmentFingerprint, DateTimeOffset now)
    {
        Generation++;
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
        NormalDueAt = now.AddHours(2);
        RetryDueAt = null;
        RetryCount = 0;
    }

    public void MakeNormalRefreshDue(DateTimeOffset now) => NormalDueAt = now.ToUniversalTime();

    public void PrepareDevelopmentRefreshDue(DateTimeOffset now)
    {
        now = now.ToUniversalTime();
        LastSuccessfulAt = now.AddHours(-2);
        NormalDueAt = now;
        RetryDueAt = null;
    }

    public void MarkAttempt(DateTimeOffset now)
    {
        LastAttemptAt = now.ToUniversalTime();
        if (CycleStartedAt is null) CycleStartedAt = now.ToUniversalTime();
        if (NormalDueAt is null) NormalDueAt = now.ToUniversalTime().AddHours(2);
    }

    public void MarkSuccess(DateTimeOffset now, DateTimeOffset? upstreamUpdatedAt, bool complete, string? missingAccounts, string? error)
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
        NormalDueAt = now.AddHours(2);
        RetryDueAt = null;
        RetryCount = 0;
    }

    public void MarkFailure(DateTimeOffset now, string kind, string error, DateTimeOffset? retryAt)
    {
        now = now.ToUniversalTime();
        LastAttemptAt = now;
        LatestComplete ??= false;
        LastErrorKind = kind;
        LastError = error;
        if (CycleStartedAt is null) CycleStartedAt = now;
        if (NormalDueAt is null || NormalDueAt <= now) NormalDueAt = now.AddHours(2);
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
        decimal gainedEhb, DateTimeOffset fetchedAt, DateTimeOffset? upstreamUpdatedAt, string assignmentFingerprint)
    {
        Id = id;
        EventId = eventId;
        Generation = generation;
        CompetitionId = competitionId;
        OsrsCharacterId = osrsCharacterId;
        GainedEhb = gainedEhb;
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
    public DateTimeOffset FetchedAt { get; private set; }
    public DateTimeOffset? UpstreamUpdatedAt { get; private set; }
    public string AssignmentFingerprint { get; private set; } = string.Empty;
}
