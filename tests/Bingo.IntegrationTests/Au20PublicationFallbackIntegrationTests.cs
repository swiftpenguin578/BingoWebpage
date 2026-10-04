using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Slice10Pass102CompetitionSynchronizationTests
{
    [Theory]
    [InlineData("pending")]
    [InlineData("id-only")]
    [InlineData("rejected")]
    public async Task Au20PublicationFallbackPreservesPreEndCacheAndLuckAge(string outcome)
    {
        var f = await FullStatsFixtureAsync(target: 1);
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await SyncStatsAsync(f, 100);
        var prior = await ReadStatsAsync(f);
        Assert.NotNull(prior.Luck.Result.Percentage);
        var generation = 0;
        string oldPayload;
        await using (var before = new ApplicationDbContext(options))
        {
            generation = (await before.EventCompetitionSynchronizations.SingleAsync()).Generation;
            oldPayload = (await before.EventStatsLuckCheckpoints.SingleAsync()).Payload;
        }
        f.Clock.Advance(TimeSpan.FromSeconds(55));
        await using (var end = new ApplicationDbContext(options))
        {
            var item = await end.Events.SingleAsync();
            Assert.True((await new EventLifecycleService(end, null!, f.Clock).EndNowAsync(item.Id, item.Version, true, "AU20 fallback", new(f.Admin.Id, f.Admin.LoginName))).Succeeded);
        }
        if (outcome == "id-only")
        {
            await using var db = new ApplicationDbContext(options);
            await new EventCompetitionManagementService(db, null!, null!, null!, f.Clock).ProcessDueAsync();
            Assert.Equal(EventCompetitionEndUpdateStatus.Rejected, (await db.EventCompetitionSynchronizations.SingleAsync()).EndUpdateStatus);
        }
        if (outcome == "rejected")
        {
            await using var db = new ApplicationDbContext(options);
            var state = await db.EventCompetitionSynchronizations.SingleAsync();
            state.RejectEndUpdate(state.EndUpdateTargetAt!.Value, "COMPETITION_START_DATE_AFTER_END_DATE");
            await db.SaveChangesAsync();
        }
        f.Clock.Advance(TimeSpan.FromHours(2));
        var response = await NamedStatsResponseAsync(f, new(0, 900, 900));
        var provider = new CountingFinalReviewClient(response, response);
        await using (var db = new ApplicationDbContext(options))
        {
            var sync = new EventCompetitionSynchronizationService(db, provider, new FixedStatus(), f.Clock);
            var manual = await sync.RefreshAsync(f.Event.Id, new(f.Admin.Id, f.Admin.LoginName));
            Assert.True(manual.Skipped);
            Assert.Equal(EventCompetitionRefreshSkipReason.EndWindowUnmatched, manual.SkipReason);
            await sync.ProcessDueAsync();
            var final = await sync.RefreshForFinalReviewAsync(f.Event.Id);
            Assert.Equal(EventCompetitionRefreshSkipReason.EndWindowUnmatched, final.SkipReason);
            Assert.Equal(0, provider.Calls);
            var view = await sync.GetAsync(f.Event.Id);
            Assert.False(view!.CanRefresh);
            Assert.Equal(EventCompetitionRefreshSkipReason.EndWindowUnmatched, view.RefreshSkipReason);
            var finalization = new EventFinalizationService(db, new PublicBoardService(db, f.Clock), f.Clock, competitionSynchronization: sync);
            var readiness = (await finalization.GetReadinessAsync(f.Event.Id))!;
            Assert.True(readiness.CanFinalize, string.Join(";", readiness.Blockers.Select(x => x.Description)));
            var published = await finalization.FinalizeAsync(f.Event.Id, new(f.Admin.Id, f.Admin.LoginName), readiness.EventVersion);
            Assert.True(published.Published);
            Assert.Equal(EventCompetitionEndUpdateStatus.CouldNotUpdate, published.WomEndUpdateStatus);
            db.ChangeTracker.Clear();
            var after = (await finalization.GetReadinessAsync(f.Event.Id))!;
            Assert.Equal(EventCompetitionEndUpdateStatus.CouldNotUpdate, after.WomEndUpdateStatus);
            var history = Assert.Single(after.History);
            Assert.Equal(FinalWomRefreshStatus.Skipped, history.FinalWomRefresh!.Status);
            Assert.Equal(EventCompetitionRefreshSkipReason.EndCouldNotBeUpdated, history.FinalWomRefresh.SkipReason);
            Assert.Equal(0, provider.Calls);
            Assert.Contains("EndCouldNotBeUpdated", (await db.EventFinalizations.SingleAsync()).CalculationInputsJson);
            var state = await db.EventCompetitionSynchronizations.SingleAsync();
            Assert.Equal(generation, state.Generation);
            Assert.Equal(prior.Luck.FetchedAt, state.LastSuccessfulAt);
            Assert.Equal(EventCompetitionEndUpdateStatus.CouldNotUpdate, state.EndUpdateStatus);
            Assert.Equal(oldPayload, (await db.EventStatsLuckCheckpoints.SingleAsync()).Payload);
            Assert.Equal(Assert.Single(prior.Teams).Progress.EhbTiebreak, Assert.Single(after.Placements).EhbTiebreak);
        }
        var official = await ReadStatsAsync(f);
        Assert.Equal(prior.Luck.FetchedAt, official.Luck.FetchedAt);
        Assert.True(official.Luck.Stale);
        Assert.Equal(JsonSerializer.Serialize(prior.Luck.Result), JsonSerializer.Serialize(official.Luck.Result));
        Assert.Equal(prior.Luck.CalculatedAt, official.Luck.CalculatedAt);
        Assert.True(f.Clock.GetUtcNow() - official.Luck.FetchedAt > TimeSpan.FromHours(2));
    }

    [Fact]
    public async Task Au20FetchAlreadyInFlightCannotOverwritePreEndCacheAfterEarlyEnd()
    {
        var f = await FullStatsFixtureAsync(target: 1);
        await SyncStatsAsync(f, 100);
        var prior = await ReadStatsAsync(f);
        f.Clock.Advance(TimeSpan.FromHours(1));
        var response = await NamedStatsResponseAsync(f, new(0, 900, 900));
        var provider = new CountingFinalReviewClient(response, response, async () =>
        {
            f.Clock.Advance(TimeSpan.FromSeconds(55));
            await using var end = new ApplicationDbContext(options);
            var item = await end.Events.SingleAsync();
            Assert.True((await new EventLifecycleService(end, null!, f.Clock).EndNowAsync(item.Id, item.Version, true, "AU20 in-flight end", new(f.Admin.Id, f.Admin.LoginName))).Succeeded);
        });
        await using var db = new ApplicationDbContext(options);
        var result = await new EventCompetitionSynchronizationService(db, provider, new FixedStatus(), f.Clock)
            .RefreshAsync(f.Event.Id, new(f.Admin.Id, f.Admin.LoginName));
        Assert.False(result.Succeeded);
        Assert.Equal("EndWindowUnmatched", result.ErrorKind);
        Assert.Equal(1, provider.Calls);
        var after = await ReadStatsAsync(f);
        Assert.Equal(prior.Luck.FetchedAt, after.Luck.FetchedAt);
        Assert.Equal(JsonSerializer.Serialize(prior.Luck.Result), JsonSerializer.Serialize(after.Luck.Result));
    }
}
