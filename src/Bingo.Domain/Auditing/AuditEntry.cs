namespace Bingo.Domain.Auditing;

public sealed class AuditEntry
{
    private AuditEntry()
    {
    }

    public AuditEntry(
        Guid id,
        DateTimeOffset occurredAt,
        Guid? actorAccountId,
        string actorUsername,
        string action,
        string targetType,
        string? targetId,
        string? details,
        Guid? eventId = null,
        string? beforeState = null,
        string? afterState = null)
    {
        Id = id;
        OccurredAt = occurredAt.ToUniversalTime();
        ActorAccountId = actorAccountId;
        ActorUsername = actorUsername;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        Details = details;
        EventId = eventId;
        BeforeState = beforeState;
        AfterState = afterState;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public Guid? ActorAccountId { get; private set; }

    public string ActorUsername { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string TargetType { get; private set; } = string.Empty;

    public string? TargetId { get; private set; }

    public string? Details { get; private set; }

    public Guid? EventId { get; private set; }

    /// <summary>Sanitized JSON snapshot of fields changed by a security-sensitive mutation.</summary>
    public string? BeforeState { get; private set; }

    /// <summary>Sanitized JSON snapshot of fields changed by a security-sensitive mutation.</summary>
    public string? AfterState { get; private set; }
}
