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
        string? details)
    {
        Id = id;
        OccurredAt = occurredAt;
        ActorAccountId = actorAccountId;
        ActorUsername = actorUsername;
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        Details = details;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public Guid? ActorAccountId { get; private set; }

    public string ActorUsername { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string TargetType { get; private set; } = string.Empty;

    public string? TargetId { get; private set; }

    public string? Details { get; private set; }
}
