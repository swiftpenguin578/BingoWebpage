using Bingo.Application.Auditing;
using Bingo.Domain.Auditing;
using Bingo.Infrastructure.Persistence;

namespace Bingo.Infrastructure.Auditing;

public sealed class AuditWriter(ApplicationDbContext dbContext, TimeProvider timeProvider) : IAuditWriter
{
    public async Task WriteAsync(
        Guid? actorAccountId,
        string actorUsername,
        string action,
        string targetType,
        string? targetId = null,
        string? details = null,
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
            details));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
