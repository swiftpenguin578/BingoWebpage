namespace Bingo.Domain.Events;

/// <summary>One durable evaluation of a configured signup-opening instant.</summary>
public sealed class ScheduledSignupOpeningAttempt
{
    private ScheduledSignupOpeningAttempt() { }

    public ScheduledSignupOpeningAttempt(Guid id, Guid eventId, DateTimeOffset scheduledFor, DateTimeOffset attemptedAt, bool opened, IEnumerable<string> blockerCodes, IEnumerable<string>? blockerDetails = null)
    {
        var normalized = blockerCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
        if (opened && normalized.Length != 0)
            throw new ArgumentException("A successful scheduled opening cannot retain blockers.", nameof(blockerCodes));
        Id = id;
        EventId = eventId;
        ScheduledFor = scheduledFor.ToUniversalTime();
        AttemptedAt = attemptedAt.ToUniversalTime();
        Opened = opened;
        BlockerCodes = string.Join(',', normalized);
        BlockerDetails = string.Join('\n', (blockerDetails ?? []).Where(detail => !string.IsNullOrWhiteSpace(detail)).Select(detail => detail.Trim()).Distinct(StringComparer.Ordinal));
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public DateTimeOffset ScheduledFor { get; private set; }
    public DateTimeOffset AttemptedAt { get; private set; }
    public bool Opened { get; private set; }
    public string BlockerCodes { get; private set; } = string.Empty;
    public string BlockerDetails { get; private set; } = string.Empty;
    public DateTimeOffset? ResolvedAt { get; private set; }
    public IReadOnlyList<string> Blockers => string.IsNullOrEmpty(BlockerCodes)
        ? []
        : BlockerCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    public IReadOnlyList<string> Details => string.IsNullOrEmpty(BlockerDetails)
        ? []
        : BlockerDetails.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void Resolve(DateTimeOffset resolvedAt) => ResolvedAt ??= resolvedAt.ToUniversalTime();
}
