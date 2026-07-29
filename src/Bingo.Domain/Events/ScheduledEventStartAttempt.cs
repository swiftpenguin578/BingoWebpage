namespace Bingo.Domain.Events;

/// <summary>One idempotent record of the start worker evaluating a configured event-start instant.</summary>
public sealed class ScheduledEventStartAttempt
{
    private ScheduledEventStartAttempt() { }

    public ScheduledEventStartAttempt(
        Guid id,
        Guid eventId,
        DateTimeOffset scheduledFor,
        DateTimeOffset attemptedAt,
        bool started,
        IEnumerable<string> blockerCodes)
    {
        var normalized = blockerCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        if (started && normalized.Length != 0)
            throw new ArgumentException("A successful scheduled start cannot retain blockers.", nameof(blockerCodes));

        Id = id;
        EventId = eventId;
        ScheduledFor = scheduledFor.ToUniversalTime();
        AttemptedAt = attemptedAt.ToUniversalTime();
        Started = started;
        BlockerCodes = string.Join(',', normalized);
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public DateTimeOffset ScheduledFor { get; private set; }
    public DateTimeOffset AttemptedAt { get; private set; }
    public bool Started { get; private set; }
    /// <summary>Sorted, comma-separated stable codes; the event/scheduled-for unique index is the retry boundary.</summary>
    public string BlockerCodes { get; private set; } = string.Empty;
    public DateTimeOffset? ResolvedAt { get; private set; }

    public IReadOnlyList<string> Blockers => string.IsNullOrEmpty(BlockerCodes)
        ? []
        : BlockerCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void Resolve(DateTimeOffset resolvedAt)
    {
        if (ResolvedAt is not null) throw new InvalidOperationException("This scheduled start attempt is already resolved.");
        ResolvedAt = resolvedAt.ToUniversalTime();
    }
}
