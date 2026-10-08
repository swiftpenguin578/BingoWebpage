using System.Data.Common;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bingo.IntegrationTests;

public sealed partial class EventCompetitionManagementIntegrationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Au20RemediationLifecycleSerializationRetryKeepsOriginalClick(bool resume, bool exhaust)
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero));
        var f = await SeedEventAsync(clock, true, competitionId: 2088);
        if (resume)
        {
            await using var end = CreateDb();
            Assert.True((await new EventLifecycleService(end, null!, clock).EndNowAsync(f.EventId, f.EventVersion, true, "Fixture end", f.Actor)).Succeeded);
        }
        clock.Advance(TimeSpan.FromSeconds(55) + TimeSpan.FromTicks(7));
        var clicked = clock.GetUtcNow();
        long version;
        await using (var read = CreateDb()) version = (await read.Events.SingleAsync()).Version;
        var collisions = 0;
        var interceptor = new Au20SyncConflictInterceptor(async () =>
        {
            if (!exhaust && collisions != 0) return;
            collisions++;
            await using var other = CreateDb();
            var value = $"controlled-concurrent-{collisions}";
            await other.Database.ExecuteSqlInterpolatedAsync($"UPDATE event_competition_synchronizations SET end_update_error_code = {value} WHERE event_id = {f.EventId}");
            clock.Advance(TimeSpan.FromMinutes(2));
        });
        await using var db = CreateDb(interceptor);
        var service = new EventLifecycleService(db, null!, clock);
        var result = resume
            ? await service.ResumePrematureEndAsync(f.EventId, version, true, "Resume", new DateTimeOffset(2026, 10, 4, 14, 0, 0, TimeSpan.Zero), f.Actor)
            : await service.EndNowAsync(f.EventId, version, true, "End", f.Actor);
        Assert.Equal(!exhaust, result.Succeeded);
        Assert.Equal(exhaust ? 3 : 1, collisions);
        await using var verify = CreateDb();
        var ev = await verify.Events.SingleAsync();
        if (exhaust)
        {
            Assert.Contains("try again", result.Error);
            Assert.Equal(version, ev.Version);
            Assert.Equal(resume ? EventState.AwaitingFinalReview : EventState.Live, ev.State);
        }
        else
        {
            var expected = clicked.AddTicks(-(clicked.Ticks % TimeSpan.TicksPerMicrosecond));
            var state = await verify.EventCompetitionSynchronizations.SingleAsync();
            Assert.Equal(expected, state.EndUpdateRequestedAt);
            Assert.Equal(resume ? EventState.Live : EventState.AwaitingFinalReview, ev.State);
            if (!resume)
            {
                Assert.Equal(expected, ev.ActualEndedAt);
                Assert.Equal(new DateTimeOffset(2026, 10, 4, 12, 1, 0, TimeSpan.Zero), ev.EventEndsAt);
                Assert.Equal(expected, (await verify.EventStateTransitions.SingleAsync()).EffectiveAt);
            }
        }
    }

    private sealed class Au20SyncConflictInterceptor(Func<Task> collide) : DbCommandInterceptor
    {
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("SELECT * FROM event_competition_synchronizations", StringComparison.Ordinal)
                && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal)) await collide();
            return result;
        }
    }
}
