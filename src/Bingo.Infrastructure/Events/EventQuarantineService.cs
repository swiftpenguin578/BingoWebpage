using System.Data;
using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Infrastructure.Events;

public sealed class EventQuarantineService(ApplicationDbContext db, TimeProvider timeProvider, IAdminCollaborationNotifier? collaboration = null) : IEventQuarantineService
{
    public Task<EventQuarantineResult> HideAsync(Guid eventId, long expectedVersion, string? confirmation, string? reason, LifecycleActor actor, CancellationToken ct = default)
        => ExecuteAsync(eventId, expectedVersion, confirmation, reason, actor, hide: true, ct);

    public Task<EventQuarantineResult> RestoreAsync(Guid eventId, long expectedVersion, string? confirmation, string? reason, LifecycleActor actor, CancellationToken ct = default)
        => ExecuteAsync(eventId, expectedVersion, confirmation, reason, actor, hide: false, ct);

    private async Task<EventQuarantineResult> ExecuteAsync(Guid eventId, long expectedVersion, string? confirmation, string? reason, LifecycleActor actor, bool hide, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var authorized = await db.Accounts.AsNoTracking().AnyAsync(account => account.Id == actor.Id && account.Active && account.AccountType == AccountType.WebsiteAccount && account.GlobalRole == GlobalRole.SuperAdmin, ct);
            if (!authorized) return new(false, "Only a SuperAdmin can hide or restore an event.") { Outcome = EventQuarantineOutcome.Forbidden };

            var item = await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} FOR UPDATE").SingleOrDefaultAsync(ct);
            if (item is null || item.State == EventState.Discarded) return new(false, "The event was not found.") { Outcome = EventQuarantineOutcome.NotFound };
            if (item.Version != expectedVersion) return Stale();
            if (hide && string.IsNullOrWhiteSpace(reason))
                return new(false, "Enter a reason for hiding the event.")
                { Outcome = EventQuarantineOutcome.ValidationFailed, FieldErrors = new Dictionary<string, string> { ["reason"] = "Enter a reason for hiding the event." } };

            var now = timeProvider.GetUtcNow();
            var before = new { item.State, item.Version, item.HiddenAt, item.HiddenByAccountId, item.HiddenReason };
            if (hide) item.Hide(actor.Id, now, confirmation, reason);
            else item.Restore(confirmation, reason);
            var action = hide ? "event.hidden" : "event.restored";
            await db.SaveChangesAsync(ct);
            var after = new { item.State, item.Version, item.HiddenAt, item.HiddenByAccountId, item.HiddenReason };
            db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actor.Id, actor.Username, action, "event", item.Id.ToString(), string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), item.Id, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after)));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            if (collaboration is not null)
            {
                try { await collaboration.NotifyEventsControlChangedAsync(ct); }
                catch (Exception) { /* The committed quarantine remains authoritative. */ }
            }
            return new(true) { Hidden = item.HiddenAt is not null, Version = item.Version };
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            return Stale();
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        {
            await transaction.RollbackAsync(ct);
            return Stale();
        }
        catch (InvalidOperationException exception)
        {
            await transaction.RollbackAsync(ct);
            return new(false, exception.Message) { Outcome = EventQuarantineOutcome.InvalidState };
        }
        catch (ArgumentException exception)
        {
            await transaction.RollbackAsync(ct);
            return new(false, exception.Message) { Outcome = EventQuarantineOutcome.ValidationFailed };
        }
    }

    private static EventQuarantineResult Stale() => new(false, "The event changed while you were working. Review the latest values and try again.")
    { Outcome = EventQuarantineOutcome.Stale };
}
