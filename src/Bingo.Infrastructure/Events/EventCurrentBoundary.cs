using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Events;

internal static class EventCurrentBoundary
{
    private static readonly EventState[] CurrentStates = [EventState.Live, EventState.AwaitingFinalReview, EventState.Finalized];

    internal static bool IsCurrentState(EventState state) => CurrentStates.Contains(state);

    internal static IQueryable<BingoEvent> OtherCurrentEvents(ApplicationDbContext db, Guid eventId)
    {
        var developmentMode = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
        return db.Events.AsNoTracking()
            .Where(x => x.Id != eventId && x.HiddenAt == null && CurrentStates.Contains(x.State) && !(developmentMode && x.IsDevelopmentFixture));
    }

    internal static Task LockAsync(ApplicationDbContext db, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7303004)", ct);
}
