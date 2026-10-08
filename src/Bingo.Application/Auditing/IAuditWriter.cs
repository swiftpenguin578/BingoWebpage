namespace Bingo.Application.Auditing;

public interface IAuditWriter
{
    /// <summary>Stages an audit entry only. The caller owns SaveChanges and any enclosing transaction.</summary>
    void Stage(Guid? actorAccountId, string actorUsername, string action, string targetType,
        string? targetId = null, string? details = null, Guid? eventId = null,
        string? beforeState = null, string? afterState = null);

    /// <summary>Explicitly saves ALL tracked changes with this audit entry. Does not commit an enclosing transaction.</summary>
    Task WriteAndSaveAsync(Guid? actorAccountId, string actorUsername, string action, string targetType,
        string? targetId = null, string? details = null, CancellationToken cancellationToken = default);

    Task WriteAndSaveAsync(Guid? actorAccountId, string actorUsername, string action, string targetType,
        string? targetId, string? details, Guid? eventId, CancellationToken cancellationToken = default);

    /// <summary>Writes a standalone entry. Rejects any pending tracked changes before staging it.</summary>
    Task WriteAsync(
        Guid? actorAccountId,
        string actorUsername,
        string action,
        string targetType,
        string? targetId = null,
        string? details = null,
        CancellationToken cancellationToken = default);

    Task WriteAsync(
        Guid? actorAccountId,
        string actorUsername,
        string action,
        string targetType,
        string? targetId,
        string? details,
        Guid? eventId,
        CancellationToken cancellationToken = default);
}
