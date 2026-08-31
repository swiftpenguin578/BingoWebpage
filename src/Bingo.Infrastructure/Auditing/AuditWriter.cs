using Bingo.Application.Auditing;
using Bingo.Domain.Auditing;
using Bingo.Infrastructure.Persistence;

namespace Bingo.Infrastructure.Auditing;

public sealed class AuditWriter(ApplicationDbContext dbContext, TimeProvider timeProvider) : IAuditWriter
{
    public Task WriteAsync(
        Guid? actorAccountId,
        string actorUsername,
        string action,
        string targetType,
        string? targetId = null,
        string? details = null,
        CancellationToken cancellationToken = default) =>
        WriteAsync(actorAccountId, actorUsername, action, targetType, targetId, details, null, cancellationToken);

    public async Task WriteAsync(
        Guid? actorAccountId,
        string actorUsername,
        string action,
        string targetType,
        string? targetId,
        string? details,
        Guid? eventId,
        CancellationToken cancellationToken = default)
    {
        dbContext.AuditEntries.Add(new AuditEntry(
            Guid.NewGuid(),
            timeProvider.GetUtcNow(),
            actorAccountId,
            actorUsername,
            action,
            targetType,
            targetId,
            details,
            eventId));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
