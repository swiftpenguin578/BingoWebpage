using System.Data;
using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Infrastructure.Events;

public sealed class EventDestructiveLifecycleService(ApplicationDbContext db, TimeProvider time, IEventBannerCleanupService? cleanup = null) : IEventDestructiveLifecycleService
{
    public async Task<LifecycleMutationResult> DiscardAsync(Guid eventId, long version, bool confirmed, LifecycleActor actor, CancellationToken ct = default)
    {
        if (!confirmed) return new(false, "Confirm that you want to permanently discard this empty event setup.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var item = await LockedEventAsync(eventId, version, ct);
            var protectedCategory = await FirstProtectedCategoryAsync(eventId, ct);
            if (protectedCategory is not null)
                return new(false, $"This event has protected {protectedCategory} history and cannot be discarded. Cancel it instead.");

            var now = time.GetUtcNow();
            var from = item.State;
            var bannerKeys = await db.EventBannerAssets.Where(x => x.EventId == eventId).Select(x => x.StorageKey).ToListAsync(ct);
            db.EventBannerCleanups.AddRange(bannerKeys.Distinct(StringComparer.Ordinal).Select(key => new EventBannerCleanup(Guid.NewGuid(), eventId, key, now)));
            item.Discard(actor.Id, now, false);
            await db.SaveChangesAsync(ct);
            await DeleteDisposableSetupAsync(eventId, ct);
            AddHistory(item, from, actor, "event.discarded", "Empty event setup discarded", now);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            if (cleanup is not null) await cleanup.ProcessEventAsync(eventId, ct);
            return new(true);
        }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(ct); return new(false, "This event changed while it was being discarded. Review it and try again."); }
        catch (InvalidOperationException ex) { await tx.RollbackAsync(ct); return new(false, ex.Message); }
        catch (DbUpdateException) { await tx.RollbackAsync(ct); return new(false, "The event changed while discard was being checked. Nothing was discarded; review its history and try again."); }
    }

    public async Task<LifecycleMutationResult> CancelAsync(Guid eventId, long version, bool confirmed, string? reason, LifecycleActor actor, CancellationToken ct = default)
    {
        if (!confirmed) return new(false, "Confirm that you want to cancel this event.");
        if (string.IsNullOrWhiteSpace(reason)) return new(false, "Enter a reason for cancelling the event.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var item = await LockedEventAsync(eventId, version, ct);
            var protectedHistory = await FirstProtectedCategoryAsync(eventId, ct) is not null;
            var now = time.GetUtcNow();
            var from = item.State;
            item.Cancel(actor.Id, now, reason, protectedHistory);
            var recipientIds = await (
                from participant in db.EventParticipants
                join account in db.Accounts on participant.AccountId equals account.Id
                where participant.EventId == eventId
                    && (participant.SignupStatus == SignupStatus.Confirmed || participant.SignupStatus == SignupStatus.WaitingList)
                    && account.AccountType == AccountType.WebsiteAccount
                select account.Id)
                .Distinct()
                .ToListAsync(ct);
            var route = item.FirstPublicAt is null ? string.Empty : $"/Events/{Uri.EscapeDataString(item.Slug)}/Signup";
            db.PersonalNotifications.AddRange(recipientIds.Select(recipientId =>
                new PersonalNotification(Guid.NewGuid(), recipientId, "event.cancelled", $"{item.Name} has been cancelled.", route, now)));
            AddHistory(item, from, actor, "event.cancelled", reason.Trim(), now);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(true);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure) { await tx.RollbackAsync(ct); return new(false, "This event changed while it was being cancelled. Review it and try again."); }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(ct); return new(false, "This event changed while it was being cancelled. Review it and try again."); }
        catch (InvalidOperationException ex) { await tx.RollbackAsync(ct); return new(false, ex.Message); }
        catch (DbUpdateException) { await tx.RollbackAsync(ct); return new(false, "The event could not be cancelled safely. No lifecycle change was saved."); }
    }

    private async Task<BingoEvent> LockedEventAsync(Guid eventId, long version, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM events WHERE id = {eventId} FOR UPDATE", ct);
        var item = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId, ct) ?? throw new InvalidOperationException("Event not found.");
        if (item.Version != version) throw new DbUpdateConcurrencyException();
        return item;
    }

    private async Task<string?> FirstProtectedCategoryAsync(Guid eventId, CancellationToken ct)
    {
        if (await db.EventParticipants.AnyAsync(x => x.EventId == eventId, ct)) return "participant";
        if (await db.Teams.AnyAsync(x => x.EventId == eventId, ct)) return "team";
        if (await db.AccountEventAccesses.AnyAsync(x => x.EventId == eventId, ct)) return "event access";
        if (await (from asset in db.EvidenceAssets join submission in db.Submissions on asset.SubmissionId equals submission.Id where submission.EventId == eventId select asset.Id).AnyAsync(ct)) return "evidence";
        if (await db.Submissions.AnyAsync(x => x.EventId == eventId, ct)) return "submission";
        return null;
    }

    private async Task DeleteDisposableSetupAsync(Guid eventId, CancellationToken ct)
    {
        var boardIds = db.Boards.Where(x => x.EventId == eventId).Select(x => x.Id);
        var tileIds = db.BoardTiles.Where(x => boardIds.Contains(x.BoardId)).Select(x => x.Id);
        var requirementIds = db.BoardRequirementSnapshots.Where(x => tileIds.Contains(x.BoardTileId)).Select(x => x.Id);
        await db.BoardRequirementBossSnapshots.Where(x => requirementIds.Contains(x.RequirementId)).ExecuteDeleteAsync(ct);
        await db.BoardRequirementDropSnapshots.Where(x => requirementIds.Contains(x.RequirementId)).ExecuteDeleteAsync(ct);
        await db.BoardRequirementSnapshots.Where(x => tileIds.Contains(x.BoardTileId)).ExecuteDeleteAsync(ct);
        await db.BoardTileImageAssets.Where(x => tileIds.Contains(x.BoardTileId)).ExecuteDeleteAsync(ct);
        await db.BoardTiles.Where(x => boardIds.Contains(x.BoardId)).ExecuteDeleteAsync(ct);
        await db.Boards.Where(x => x.EventId == eventId).ExecuteDeleteAsync(ct);
        await db.SignupQuestions.Where(x => x.EventId == eventId).ExecuteDeleteAsync(ct);
        await db.SignupForms.Where(x => x.EventId == eventId).ExecuteDeleteAsync(ct);
        await db.DraftSessions.Where(x => x.EventId == eventId).ExecuteDeleteAsync(ct);
        await db.EvidenceCodes.Where(x => x.EventId == eventId).ExecuteDeleteAsync(ct);
        await db.ScheduledEventStartAttempts.Where(x => x.EventId == eventId).ExecuteDeleteAsync(ct);
        await db.ScheduledSignupOpeningAttempts.Where(x => x.EventId == eventId).ExecuteDeleteAsync(ct);
        await db.FinalReviewResolutions.Where(x => x.EventId == eventId).ExecuteDeleteAsync(ct);
        await db.TeamCompletionCorrections.Where(x => x.EventId == eventId).ExecuteDeleteAsync(ct);
        await db.EventBannerAssets.Where(x => x.EventId == eventId).ExecuteDeleteAsync(ct);
    }

    private void AddHistory(BingoEvent item, EventState from, LifecycleActor actor, string action, string detail, DateTimeOffset now)
    {
        db.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), item.Id, from, item.State, actor.Id, now, detail));
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actor.Id, actor.Username, action, "event", item.Id.ToString(), detail, item.Id,
            JsonSerializer.Serialize(new { state = from }), JsonSerializer.Serialize(new { state = item.State })));
    }
}
