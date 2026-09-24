namespace Bingo.Domain.Integrations.WiseOldMan;

public enum EventCompetitionUpdateAllSlotStatus
{
    Pending = 1,
    Sending = 2,
    Acknowledged = 3,
    Failed = 4,
    Skipped = 5,
    Unknown = 6
}

/// <summary>
/// Durable identity and receipt for one managed update-all request. The paired
/// fetch time is part of the identity so a restart or a second worker cannot
/// enqueue the same remote slot again.
/// </summary>
public sealed class EventCompetitionUpdateAllSlot
{
    private EventCompetitionUpdateAllSlot() { }

    public EventCompetitionUpdateAllSlot(
        Guid id,
        Guid eventId,
        Guid managementId,
        Guid synchronizationId,
        long competitionId,
        DateTimeOffset actualStartedAt,
        DateTimeOffset pairedFetchAt,
        DateTimeOffset scheduledAt,
        long managementVersion,
        int synchronizationGeneration,
        DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(competitionId);
        if (pairedFetchAt <= actualStartedAt) throw new ArgumentException("The paired fetch slot must be after the Live anchor.", nameof(pairedFetchAt));
        if (scheduledAt >= pairedFetchAt || scheduledAt <= actualStartedAt)
            throw new ArgumentException("The update-all slot must lead its paired fetch inside the Live window.", nameof(scheduledAt));

        Id = id;
        EventId = eventId;
        ManagementId = managementId;
        SynchronizationId = synchronizationId;
        CompetitionId = competitionId;
        ActualStartedAt = actualStartedAt.ToUniversalTime();
        PairedFetchAt = pairedFetchAt.ToUniversalTime();
        ScheduledAt = scheduledAt.ToUniversalTime();
        ManagementVersion = managementVersion;
        SynchronizationGeneration = synchronizationGeneration;
        Status = EventCompetitionUpdateAllSlotStatus.Pending;
        CreatedAt = UpdatedAt = now.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid ManagementId { get; private set; }
    public Guid SynchronizationId { get; private set; }
    public long CompetitionId { get; private set; }
    public DateTimeOffset ActualStartedAt { get; private set; }
    public DateTimeOffset PairedFetchAt { get; private set; }
    public DateTimeOffset ScheduledAt { get; private set; }
    public long ManagementVersion { get; private set; }
    public int SynchronizationGeneration { get; private set; }
    public EventCompetitionUpdateAllSlotStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset? ClaimedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public string? OutcomeCode { get; private set; }
    public string? Outcome { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void RebindLineage(long managementVersion, int synchronizationGeneration, DateTimeOffset now)
    {
        if (Status != EventCompetitionUpdateAllSlotStatus.Pending)
            throw new InvalidOperationException("Only a pending update-all slot can be rebound.");

        ManagementVersion = managementVersion;
        SynchronizationGeneration = synchronizationGeneration;
        UpdatedAt = now.ToUniversalTime();
    }

    public void Claim(DateTimeOffset now)
    {
        if (Status != EventCompetitionUpdateAllSlotStatus.Pending)
            throw new InvalidOperationException("This update-all slot is no longer pending.");

        Status = EventCompetitionUpdateAllSlotStatus.Sending;
        AttemptCount++;
        ClaimedAt = now.ToUniversalTime();
        NextAttemptAt = null;
        UpdatedAt = now.ToUniversalTime();
    }

    public void Acknowledge(string? code, string? outcome, DateTimeOffset now)
    {
        Complete(EventCompetitionUpdateAllSlotStatus.Acknowledged, code, outcome, now);
    }

    public void Fail(string code, string outcome, DateTimeOffset now)
    {
        Complete(EventCompetitionUpdateAllSlotStatus.Failed, code, outcome, now);
    }

    public void Skip(string code, string outcome, DateTimeOffset now)
    {
        if (Status is not (EventCompetitionUpdateAllSlotStatus.Pending or EventCompetitionUpdateAllSlotStatus.Sending))
            throw new InvalidOperationException("Only a pending or claimed update-all slot can be skipped.");
        Status = EventCompetitionUpdateAllSlotStatus.Skipped;
        OutcomeCode = code;
        Outcome = outcome;
        CompletedAt = UpdatedAt = now.ToUniversalTime();
        NextAttemptAt = null;
    }

    public void MarkUnknown(string code, string outcome, DateTimeOffset now)
    {
        Complete(EventCompetitionUpdateAllSlotStatus.Unknown, code, outcome, now);
    }

    public void Retry(DateTimeOffset retryAt, string code, string outcome, DateTimeOffset now)
    {
        if (Status != EventCompetitionUpdateAllSlotStatus.Sending)
            throw new InvalidOperationException("Only a claimed update-all slot can be retried.");
        Status = EventCompetitionUpdateAllSlotStatus.Pending;
        NextAttemptAt = retryAt.ToUniversalTime();
        OutcomeCode = code;
        Outcome = outcome;
        UpdatedAt = now.ToUniversalTime();
    }

    private void Complete(EventCompetitionUpdateAllSlotStatus status, string? code, string? outcome, DateTimeOffset now)
    {
        if (Status != EventCompetitionUpdateAllSlotStatus.Sending)
            throw new InvalidOperationException("Only a claimed update-all slot can be completed.");
        Status = status;
        OutcomeCode = code;
        Outcome = outcome;
        CompletedAt = UpdatedAt = now.ToUniversalTime();
        NextAttemptAt = null;
    }
}

public static class EventCompetitionUpdateAllSchedule
{
    public static readonly TimeSpan PairedFetchInterval = TimeSpan.FromHours(4);
    public static readonly TimeSpan LeadTime = TimeSpan.FromMinutes(15);

    public static DateTimeOffset PairedFetchAt(DateTimeOffset actualStartedAt, int sequence)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        return actualStartedAt.ToUniversalTime().AddTicks(checked(PairedFetchInterval.Ticks * sequence));
    }

    public static DateTimeOffset ScheduledAt(DateTimeOffset actualStartedAt, int sequence) =>
        PairedFetchAt(actualStartedAt, sequence).Subtract(LeadTime);

    public static int CurrentSequence(DateTimeOffset actualStartedAt, DateTimeOffset now)
    {
        var first = PairedFetchAt(actualStartedAt, 1);
        now = now.ToUniversalTime();
        if (now < first) return 1;
        var elapsed = now - first;
        return checked((int)(elapsed.Ticks / PairedFetchInterval.Ticks) + 1);
    }

    public static bool IsInterior(DateTimeOffset actualStartedAt, DateTimeOffset eventEndsAt, int sequence)
    {
        var updateAt = ScheduledAt(actualStartedAt, sequence);
        var fetchAt = PairedFetchAt(actualStartedAt, sequence);
        return updateAt > actualStartedAt.ToUniversalTime() && fetchAt < eventEndsAt.ToUniversalTime();
    }
}
