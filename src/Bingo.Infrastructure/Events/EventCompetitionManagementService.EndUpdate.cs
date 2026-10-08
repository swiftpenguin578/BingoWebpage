using Bingo.Application.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Events;

public sealed partial class EventCompetitionManagementService
{
    private Task<bool> EndUpdatesStoppedAsync(Guid eventId, CancellationToken ct) =>
        db.EventCompetitionSynchronizations.AsNoTracking().AnyAsync(x => x.EventId == eventId && x.EndUpdateTargetAt != null
            && db.EventFinalizations.Any(f => f.EventId == eventId), ct);

    private Task<bool> IsPendingEndOperationAsync(EventCompetitionManagementOperation operation, CancellationToken ct) =>
        operation.Type != EventCompetitionManagementOperationType.Update ? Task.FromResult(false) :
        db.EventCompetitionSynchronizations.AsNoTracking().AnyAsync(x => x.EventId == operation.EventId
            && x.EndUpdateStatus == EventCompetitionEndUpdateStatus.Pending, ct);

    private async Task<DateTimeOffset> EndRetryAtAsync(EventCompetitionManagementOperation operation, DateTimeOffset? providerRetryAt, CancellationToken ct, int attemptOffset = 0)
    {
        if (!await IsPendingEndOperationAsync(operation, ct)) return providerRetryAt ?? time.GetUtcNow().Add(RetryDelay);
        var minutes = (operation.AttemptCount + attemptOffset) switch { <= 1 => 1, 2 => 2, 3 => 4, 4 => 8, 5 => 16, _ => 30 };
        var due = time.GetUtcNow().AddMinutes(minutes);
        return providerRetryAt is { } provider && provider > due ? provider : due;
    }

    // Only an end-only update with read-back proof of the unchanged source may be
    // resent. Other ambiguous operations keep their existing reconciliation policy.
    private async Task<EventCompetitionManagementResult?> RetryUnappliedEndAsync(Guid operationId, bool reconciled, DateTimeOffset? providerDue, CancellationToken ct)
    {
        var reference = await db.EventCompetitionManagementOperations.AsNoTracking().SingleAsync(x => x.Id == operationId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {reference.EventId} FOR UPDATE").SingleAsync(ct);
        var operation = await db.EventCompetitionManagementOperations.FromSqlInterpolated($"SELECT * FROM event_competition_management_operations WHERE id = {operationId} FOR UPDATE").SingleAsync(ct);
        await db.Entry(operation).ReloadAsync(ct);
        var expectedPhase = reconciled ? EventCompetitionManagementOperationPhase.Unknown : EventCompetitionManagementOperationPhase.Sending;
        if (operation.Phase != expectedPhase) return new(true, OperationId: operation.Id, Status: operation.Phase.ToString());
        if (await EndUpdatesStoppedAsync(operation.EventId, ct)) return new(false, Status: "Stopped", ErrorCode: "ResultsPublished");
        if (DeserializePayload(operation.DesiredPayloadJson).IncludeTeams || !await IsPendingEndOperationAsync(operation, ct)) return null;
        // A scheduled reconciliation already waited for this attempt's backoff.
        var due = reconciled ? operation.NextAttemptAt ?? time.GetUtcNow() : await EndRetryAtAsync(operation, providerDue, ct);
        if (providerDue > due) due = providerDue.Value;
        operation.Retry(due, "EndUpdateNotApplied", "WOM still has the previous end; the end update will be retried.", time.GetUtcNow());
        var management = await db.EventCompetitionManagements.SingleAsync(x => x.Id == operation.ManagementId, ct);
        management.MarkPending(operation.Id, time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(false, operation.SafeError, operation.Id, "Retry", due, "EndUpdateNotApplied");
    }

    private async Task RejectPendingEndAsync(Guid eventId, DateTimeOffset? target, string code, CancellationToken ct)
    {
        if (target is null) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} FOR UPDATE").SingleAsync(ct);
        var state = await db.EventCompetitionSynchronizations.SingleOrDefaultAsync(x => x.EventId == eventId, ct);
        if (state is not null) { await db.Entry(state).ReloadAsync(ct); state.RejectEndUpdate(target.Value, code); }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
