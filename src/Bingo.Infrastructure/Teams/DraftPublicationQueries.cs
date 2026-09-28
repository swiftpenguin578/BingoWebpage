using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Teams;

/// <summary>
/// Shared finalized-roster boundary used by lifecycle and public/remote
/// consumers. A draft state alone is not publication evidence.
/// </summary>
public static class DraftPublicationQueries
{
    public static IQueryable<DraftPublicationCycle> ActiveRosterPublications(this ApplicationDbContext db, Guid eventId) =>
        from cycle in db.DraftPublicationCycles.AsNoTracking()
        join draft in db.DraftSessions.AsNoTracking() on cycle.DraftSessionId equals draft.Id
        where draft.EventId == eventId && draft.State == DraftState.Finalized && cycle.SupersededAt == null &&
              db.DraftPublicationRosters.Any(roster => roster.DraftPublicationCycleId == cycle.Id)
        select cycle;

    public static Task<DraftPublicationCycle?> ActiveRosterPublicationAsync(this ApplicationDbContext db, Guid eventId, CancellationToken ct = default) =>
        db.ActiveRosterPublications(eventId).SingleOrDefaultAsync(ct);
}
