namespace Bingo.Application.Auditing;

public interface IAuditWriter
{
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
