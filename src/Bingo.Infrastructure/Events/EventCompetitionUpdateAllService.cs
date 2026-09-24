using System.Data;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Infrastructure.Events;

/// <summary>
/// Dispatches the durable four-hour update-all slots from the existing
/// management worker. This processor owns only the asynchronous update-all
/// action; hourly competition reads continue through their own synchronization
/// worker and state machine.
/// </summary>
public sealed class EventCompetitionUpdateAllService(
    ApplicationDbContext db,
    IWiseOldManCompetitionManagementClient managementClient,
    ICompetitionCredentialProtector credentialProtector,
    TimeProvider time) : IEventCompetitionUpdateAllService
{
    private static readonly TimeSpan DispatchGrace = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ClaimTimeout = TimeSpan.FromMinutes(2);

    public async Task ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        await ExpireClaimsAsync(now, cancellationToken);

        var roots = await GetManagedLiveRootsAsync(cancellationToken);
        foreach (var root in roots)
        {
            var current = EventCompetitionUpdateAllSchedule.CurrentSequence(root.ActualStartedAt, now);
            foreach (var sequence in new[] { current, checked(current + 1) })
            {
                var slotId = await EnsureSlotAsync(root, sequence, cancellationToken);
                if (slotId is { } id)
                    await ProcessSlotAsync(id, cancellationToken);
            }
        }

        // This second query also closes a pending slot whose management link or
        // event state changed after it was created. It is intentionally bounded
        // and only considers the one scan interval around the dispatch boundary.
        var dueSlotIds = await db.EventCompetitionUpdateAllSlots.AsNoTracking()
            .Where(x => x.Status == EventCompetitionUpdateAllSlotStatus.Pending
                && x.ScheduledAt <= now.Add(DispatchGrace)
                && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.ScheduledAt)
            .Select(x => x.Id)
            .Take(100)
            .ToListAsync(cancellationToken);
        foreach (var slotId in dueSlotIds)
            await ProcessSlotAsync(slotId, cancellationToken);
    }

    private async Task<List<ManagedLiveRoot>> GetManagedLiveRootsAsync(CancellationToken cancellationToken)
    {
        var rows = await (from management in db.EventCompetitionManagements.AsNoTracking()
                          join synchronization in db.EventCompetitionSynchronizations.AsNoTracking()
                              on management.SynchronizationId equals synchronization.Id
                          join item in db.Events.AsNoTracking() on management.EventId equals item.Id
                          where management.Status == EventCompetitionManagementStatus.Active
                              && synchronization.EventId == management.EventId
                              && synchronization.CompetitionId == management.CompetitionId
                              && item.HiddenAt == null
                              && item.State == EventState.Live
                              && item.ActualStartedAt != null
                          select new
                          {
                              EventId = item.Id,
                              ManagementId = management.Id,
                              SynchronizationId = synchronization.Id,
                              CompetitionId = management.CompetitionId,
                              ActualStartedAt = item.ActualStartedAt!.Value,
                              ManagementVersion = management.ManagementVersion,
                              SynchronizationGeneration = synchronization.Generation
                          })
            .OrderBy(x => x.ActualStartedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        return rows.Select(x => new ManagedLiveRoot(
            x.EventId, x.ManagementId, x.SynchronizationId, x.CompetitionId,
            x.ActualStartedAt, x.ManagementVersion, x.SynchronizationGeneration)).ToList();
    }

    private async Task<Guid?> EnsureSlotAsync(ManagedLiveRoot root, int sequence, CancellationToken cancellationToken)
    {
        var pairedFetchAt = EventCompetitionUpdateAllSchedule.PairedFetchAt(root.ActualStartedAt, sequence);
        var scheduledAt = EventCompetitionUpdateAllSchedule.ScheduledAt(root.ActualStartedAt, sequence);
        await using var competitionLock = await CompetitionReferenceLock.AcquireAsync(db, root.CompetitionId, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var existing = await db.EventCompetitionUpdateAllSlots
            .SingleOrDefaultAsync(x => x.CompetitionId == root.CompetitionId && x.PairedFetchAt == pairedFetchAt, cancellationToken);
        if (existing is not null)
        {
            var id = existing.Id;
            await transaction.CommitAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return id;
        }

        var current = await (from management in db.EventCompetitionManagements
                             join synchronization in db.EventCompetitionSynchronizations
                                 on management.SynchronizationId equals synchronization.Id
                             join item in db.Events on management.EventId equals item.Id
                             where management.Id == root.ManagementId
                                 && management.EventId == root.EventId
                                 && synchronization.Id == root.SynchronizationId
                             select new { management, synchronization, item }).SingleOrDefaultAsync(cancellationToken);
        if (current is null || current.management.CompetitionId != root.CompetitionId || current.synchronization.CompetitionId != root.CompetitionId
            || current.item.ActualStartedAt is not { } actualStartedAt)
        {
            await transaction.CommitAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return null;
        }

        var slot = new EventCompetitionUpdateAllSlot(
            Guid.NewGuid(), root.EventId, current.management.Id, current.synchronization.Id,
            current.management.CompetitionId, actualStartedAt, pairedFetchAt, scheduledAt,
            current.management.ManagementVersion, current.synchronization.Generation, time.GetUtcNow());
        db.EventCompetitionUpdateAllSlots.Add(slot);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var id = slot.Id;
            db.ChangeTracker.Clear();
            return id;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return await db.EventCompetitionUpdateAllSlots.AsNoTracking()
                .Where(x => x.CompetitionId == root.CompetitionId && x.PairedFetchAt == pairedFetchAt)
                .Select(x => (Guid?)x.Id)
                .SingleOrDefaultAsync(cancellationToken);
        }
    }

    private async Task ProcessSlotAsync(Guid slotId, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var snapshot = await db.EventCompetitionUpdateAllSlots.AsNoTracking()
            .Where(x => x.Id == slotId)
            .Select(x => new SlotSnapshot(x.Id, x.CompetitionId, x.Status, x.ScheduledAt, x.NextAttemptAt))
            .SingleOrDefaultAsync(cancellationToken);
        if (snapshot is null || snapshot.Status != EventCompetitionUpdateAllSlotStatus.Pending)
            return;

        await using var competitionLock = await CompetitionReferenceLock.AcquireAsync(db, snapshot.CompetitionId, cancellationToken);
        var claim = await ClaimSlotAsync(slotId, cancellationToken);
        if (claim is null) return;

        WiseOldManUpdateAllResult result;
        try
        {
            result = await managementClient.UpdateAllAsync(
                claim.CompetitionId,
                claim.VerificationCode,
                claim.ScheduledAt.Add(DispatchGrace),
                ct => RecheckDispatchEligibilityAsync(claim, ct),
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = new(WiseOldManUpdateAllStatus.Unknown, ErrorCode: "UnknownOutcome", Message: "The Wise Old Man participant update outcome is unknown.");
        }
        catch (Exception)
        {
            // A provider exception after the claim may have happened after the
            // POST left the process. Persist an ambiguous terminal outcome so a
            // restart cannot blindly duplicate it.
            result = new(WiseOldManUpdateAllStatus.Unknown, ErrorCode: "UnknownOutcome", Message: "The Wise Old Man participant update outcome is unknown.");
        }

        await CompleteSlotAsync(claim, result, cancellationToken);
    }

    private async Task<bool> RecheckDispatchEligibilityAsync(ClaimedSlot claim, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var slot = await db.EventCompetitionUpdateAllSlots
            .FromSqlInterpolated($"SELECT * FROM event_competition_update_all_slots WHERE id = {claim.SlotId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (slot is null || slot.Status != EventCompetitionUpdateAllSlotStatus.Sending
            || slot.CompetitionId != claim.CompetitionId || slot.ScheduledAt != claim.ScheduledAt)
        {
            await transaction.CommitAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return false;
        }

        var item = await db.Events
            .FromSqlInterpolated($"SELECT * FROM events WHERE id = {slot.EventId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        var management = await db.EventCompetitionManagements.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == slot.ManagementId, cancellationToken);
        var synchronization = await db.EventCompetitionSynchronizations.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == slot.SynchronizationId, cancellationToken);
        var eligible = !ValidateEligibility(slot, item, management, synchronization, out _);
        if (eligible)
            eligible = !await db.EventCompetitionManagementOperations.AsNoTracking()
                .AnyAsync(value => value.EventId == slot.EventId
                    && value.ManagementId == slot.ManagementId
                    && value.Type == EventCompetitionManagementOperationType.Delete
                    && value.CreatedAt > slot.CreatedAt
                    && value.Phase != EventCompetitionManagementOperationPhase.Failed
                    && value.Phase != EventCompetitionManagementOperationPhase.Cancelled,
                    cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return eligible;
    }

    private async Task<ClaimedSlot?> ClaimSlotAsync(Guid slotId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var slot = await db.EventCompetitionUpdateAllSlots
            .FromSqlInterpolated($"SELECT * FROM event_competition_update_all_slots WHERE id = {slotId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (slot is null || slot.Status != EventCompetitionUpdateAllSlotStatus.Pending)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var now = time.GetUtcNow();
        if (slot.NextAttemptAt is { } nextAttempt && nextAttempt > now)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (now < slot.ScheduledAt)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (now > slot.ScheduledAt.Add(DispatchGrace))
        {
            slot.Skip("DispatchWindowMissed", "The update-all dispatch window elapsed before a worker claimed this slot.", now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var item = await db.Events
            .FromSqlInterpolated($"SELECT * FROM events WHERE id = {slot.EventId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        var management = await db.EventCompetitionManagements
            .SingleOrDefaultAsync(x => x.Id == slot.ManagementId, cancellationToken);
        var synchronization = await db.EventCompetitionSynchronizations
            .SingleOrDefaultAsync(x => x.Id == slot.SynchronizationId, cancellationToken);
        var invalid = ValidateEligibility(slot, item, management, synchronization, out var reason);
        if (invalid)
        {
            slot.Skip(reason.Code, reason.Message, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        // A pending receipt may outlive a successful same-link management edit
        // or source recovery. Rebind only those mutable lineage values while
        // the competition lock and serializable transaction still protect the
        // identity. A delete operation created after this receipt is a hard
        // boundary: even if the row is later recreated with the same provider
        // id, the old receipt must remain skipped rather than being revived.
        var hasDeletionAfterReceipt = await db.EventCompetitionManagementOperations
            .AsNoTracking()
            .AnyAsync(x => x.EventId == slot.EventId
                && x.ManagementId == slot.ManagementId
                && x.Type == EventCompetitionManagementOperationType.Delete
                && x.CreatedAt > slot.CreatedAt
                && x.Phase != EventCompetitionManagementOperationPhase.Failed
                && x.Phase != EventCompetitionManagementOperationPhase.Cancelled,
                cancellationToken);
        if (hasDeletionAfterReceipt)
        {
            slot.Skip("ManagementChanged", "The managed WOM competition link was deleted after this slot was scheduled.", now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (management!.ManagementVersion != slot.ManagementVersion
            || synchronization!.Generation != slot.SynchronizationGeneration)
            slot.RebindLineage(management.ManagementVersion, synchronization!.Generation, now);

        string verificationCode;
        try { verificationCode = credentialProtector.Unprotect(management!.ProtectedVerificationCode); }
        catch
        {
            slot.Skip("CredentialUnavailable", "The protected WOM management credential could not be opened.", now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (string.IsNullOrWhiteSpace(verificationCode))
        {
            slot.Skip("CredentialUnavailable", "The protected WOM management credential is empty.", now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        management!.ObserveActualStart(item!.ActualStartedAt, now);
        slot.Claim(now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(slot.Id, slot.CompetitionId, slot.ScheduledAt, verificationCode);
    }

    private static bool ValidateEligibility(
        EventCompetitionUpdateAllSlot slot,
        BingoEvent? item,
        EventCompetitionManagement? management,
        EventCompetitionSynchronization? synchronization,
        out (string Code, string Message) reason)
    {
        if (item is null || item.HiddenAt is not null || item.State != EventState.Live || item.ActualStartedAt is not { } actualStartedAt
            || actualStartedAt.ToUniversalTime() != slot.ActualStartedAt)
        {
            reason = ("EventNotLive", "The event is no longer Live at the scheduled update-all slot.");
            return true;
        }
        if (item.EventEndsAt is not { } eventEndsAt || !EventCompetitionUpdateAllSchedule.IsInterior(actualStartedAt, eventEndsAt, Sequence(slot)))
        {
            reason = ("OutsideLiveWindow", "The update-all slot and its paired fetch are outside the useful Live window.");
            return true;
        }
        if (management is null || management.Status != EventCompetitionManagementStatus.Active
            || management.Id != slot.ManagementId || management.CompetitionId != slot.CompetitionId
            || management.SynchronizationId != slot.SynchronizationId)
        {
            reason = ("ManagementChanged", "The managed WOM competition link changed before this slot was dispatched.");
            return true;
        }
        if (synchronization is null || synchronization.EventId != slot.EventId || synchronization.CompetitionId != slot.CompetitionId
            || synchronization.Id != slot.SynchronizationId)
        {
            reason = ("SourceMismatch", "The WOM competition source no longer matches the durable update-all slot.");
            return true;
        }

        reason = default;
        return false;
    }

    private static int Sequence(EventCompetitionUpdateAllSlot slot)
    {
        var elapsed = slot.PairedFetchAt - slot.ActualStartedAt;
        return checked((int)(elapsed.Ticks / EventCompetitionUpdateAllSchedule.PairedFetchInterval.Ticks));
    }

    private async Task CompleteSlotAsync(ClaimedSlot claim, WiseOldManUpdateAllResult result, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var slot = await db.EventCompetitionUpdateAllSlots
            .FromSqlInterpolated($"SELECT * FROM event_competition_update_all_slots WHERE id = {claim.SlotId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (slot is null || slot.Status != EventCompetitionUpdateAllSlotStatus.Sending)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var now = time.GetUtcNow();
        var code = Safe(result.ErrorCode, claim.VerificationCode) ?? result.Status.ToString();
        var message = Safe(result.Message, claim.VerificationCode) ?? "Wise Old Man participant update request completed.";
        if (result.Acknowledged)
            slot.Acknowledge("Acknowledged", message, now);
        else if (result.ErrorCode is "DispatchWindowMissed" or "DispatchEligibilityChanged" or "DispatchEligibilityCheckFailed")
            slot.Skip(code, message, now);
        else if (result.Status == WiseOldManUpdateAllStatus.RateLimited
            && result.RetryAt is { } retryAt
            && retryAt > now
            && retryAt <= claim.ScheduledAt.Add(DispatchGrace))
            slot.Retry(retryAt, code, message, now);
        else if (result.Status == WiseOldManUpdateAllStatus.RateLimited)
            slot.Skip("RateLimitedWindowExpired", message, now);
        else if (result.Status == WiseOldManUpdateAllStatus.Unknown)
            slot.MarkUnknown(code, message, now);
        else
            slot.Fail(code, message, now);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ExpireClaimsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ids = await db.EventCompetitionUpdateAllSlots.AsNoTracking()
            .Where(x => x.Status == EventCompetitionUpdateAllSlotStatus.Sending
                && x.ClaimedAt != null && x.ClaimedAt <= now.Subtract(ClaimTimeout))
            .OrderBy(x => x.ClaimedAt)
            .Select(x => x.Id)
            .Take(100)
            .ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var slot = await db.EventCompetitionUpdateAllSlots
                .FromSqlInterpolated($"SELECT * FROM event_competition_update_all_slots WHERE id = {id} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (slot is not null && slot.Status == EventCompetitionUpdateAllSlotStatus.Sending
                && slot.ClaimedAt <= now.Subtract(ClaimTimeout))
                slot.MarkUnknown("ClaimExpired", "The previous update-all attempt expired before its outcome was recorded.", now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
    }

    private static string? Safe(string? value, string secret)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var safe = value.Replace(secret, "[redacted]", StringComparison.Ordinal);
        return safe.Length <= 500 ? safe : safe[..500];
    }

    private sealed record ManagedLiveRoot(
        Guid EventId,
        Guid ManagementId,
        Guid SynchronizationId,
        long CompetitionId,
        DateTimeOffset ActualStartedAt,
        long ManagementVersion,
        int SynchronizationGeneration);

    private sealed record SlotSnapshot(
        Guid Id,
        long CompetitionId,
        EventCompetitionUpdateAllSlotStatus Status,
        DateTimeOffset ScheduledAt,
        DateTimeOffset? NextAttemptAt);

    private sealed record ClaimedSlot(Guid SlotId, long CompetitionId, DateTimeOffset ScheduledAt, string VerificationCode);
}
