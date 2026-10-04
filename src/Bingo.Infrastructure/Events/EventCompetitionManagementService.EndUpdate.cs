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
