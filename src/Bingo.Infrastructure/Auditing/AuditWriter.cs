using Bingo.Application.Auditing;
using Bingo.Domain.Auditing;
using Bingo.Infrastructure.Persistence;

namespace Bingo.Infrastructure.Auditing;

public sealed class AuditWriter(ApplicationDbContext dbContext, TimeProvider timeProvider) : IAuditWriter
{
    public void Stage(Guid? actorAccountId, string actorUsername, string action, string targetType,
        string? targetId = null, string? details = null, Guid? eventId = null,
        string? beforeState = null, string? afterState = null) =>
        dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), timeProvider.GetUtcNow(), actorAccountId,
            actorUsername, action, targetType, targetId, details, eventId, beforeState, afterState));

    public Task WriteAndSaveAsync(Guid? actorAccountId, string actorUsername, string action, string targetType,
        string? targetId = null, string? details = null, CancellationToken cancellationToken = default) =>
        WriteAndSaveAsync(actorAccountId, actorUsername, action, targetType, targetId, details, null, cancellationToken);

    public async Task WriteAndSaveAsync(Guid? actorAccountId, string actorUsername, string action, string targetType,
        string? targetId, string? details, Guid? eventId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stage(actorAccountId, actorUsername, action, targetType, targetId, details, eventId);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

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
        cancellationToken.ThrowIfCancellationRequested();
        if (dbContext.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Standalone audit writes require a clean change tracker. Stage the audit and save in the owning workflow, or explicitly use WriteAndSaveAsync.");
        await WriteAndSaveAsync(actorAccountId, actorUsername, action, targetType, targetId, details, eventId, cancellationToken);
    }
}
