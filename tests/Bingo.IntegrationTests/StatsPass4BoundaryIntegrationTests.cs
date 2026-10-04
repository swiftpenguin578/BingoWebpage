using System.Data;
using System.Data.Common;
using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Application.Stats;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Stats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bingo.IntegrationTests;

public sealed partial class Slice10Pass102CompetitionSynchronizationTests
{
    [Fact]
    public async Task StatsPass4EvidenceCommittedMidQueryCannotMixSnapshotVersions()
    {
        var f = await FullStatsFixtureAsync(); await SyncStatsAsync(f, 100);
        var id = await PendingStatsAsync(f, 0, 0, 10);
        var interceptor = new AfterStatsEventRead(() => ApproveStatsAsync(f, id));
        var racingOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(interceptor).Options;
        await using var db = new ApplicationDbContext(racingOptions);
        var result = (await new PublicStatsService(db, f.Clock).GetAsync(f.Event.Slug))!;
        Assert.True(interceptor.Ran); Assert.Equal(0, result.Value.Drops); Assert.Equal(0, result.Luck.Result.Received);
        Assert.Equal(result.EvidenceRevision, result.Luck.EvidenceRevision);
        f.Clock.Advance(TimeSpan.FromHours(3)); await SyncStatsAsync(f, 100);
        var next = await ReadStatsAsync(f); Assert.Equal(1, next.Value.Drops); Assert.Equal(1, next.Luck.Result.Received);
        Assert.Equal(result.EvidenceRevision + 1, next.EvidenceRevision);
    }

    [Theory]
    [InlineData("board")]
    [InlineData("objective")]
    [InlineData("mapping")]
    public async Task StatsPass4BoardAndBasisChangesDuringFetchFenceTheCompleteQuery(string mutation)
    {
        var f = await FullStatsFixtureAsync();
        if (mutation == "mapping")
        {
            await using var unmapped = new ApplicationDbContext(options);
            (await unmapped.BossActivities.SingleAsync(x => x.Id == f.Boss.Id)).ConfigureApi(null); await unmapped.SaveChangesAsync();
        }
        await SyncStatsAsync(f, StatsResponse(f, new(0, 100, 100)), async () =>
        {
            if (mutation != "mapping") await ReplaceStatsApprovalAsync(f, mutation == "objective");
            else
            {
                await using var db = new ApplicationDbContext(options); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {f.Event.Id} FOR UPDATE").SingleAsync();
                var boss = await db.BossActivities.SingleAsync(x => x.Id == f.Boss.Id); boss.ConfigureApi("vorkath"); boss.RecordMapping(ApiMappingStatus.Verified, f.Clock.GetUtcNow());
                await db.SaveChangesAsync(); await db.RetainLuckOutcomeBasesAsync(f.Event.Id, f.Clock.GetUtcNow()); await db.SaveChangesAsync(); await tx.CommitAsync();
            }
        });
        var blocked = await ReadStatsAsync(f); Assert.Null(blocked.Luck.Result.Percentage); Assert.Null(blocked.Luck.CalculatedAt);
        f.Clock.Advance(TimeSpan.FromHours(3)); await SyncStatsAsync(f, 100);
        var complete = await ReadStatsAsync(f); AssertLuckScore(mutation == "objective" ? 6.63097779473767m : 18.3016170636616m, complete.Luck.Result);
        Assert.Equal(mutation == "objective" ? 2 : 1, complete.Luck.Sources.Count);
        Assert.All(complete.Luck.Sources, x => Assert.Equal(.01m, x.Probability));
        Assert.Equal(mutation == "objective" ? 2 : 1, complete.Luck.Result.Expected);
    }

    [Theory]
    [InlineData("assignment")]
    [InlineData("competition")]
    [InlineData("team")]
    public async Task StatsPass4ReplacementCannotPresentPreviousCheckpoint(string change)
    {
        var f = await FullStatsFixtureAsync(); await SyncStatsAsync(f, 100);
        await using (var db = new ApplicationDbContext(options))
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {f.Event.Id} FOR UPDATE").SingleAsync();
            if (change == "assignment") (await db.EventParticipantCharacters.SingleAsync(x => x.OsrsCharacterId == f.Characters[0].Id)).Release(f.Admin.Id, f.Clock.GetUtcNow());
            else if (change == "team")
            {
                var other = new Team(Guid.NewGuid(), f.Event.Id, "Replacement team", "replacement-team", TeamFormationType.Drafted, null, true); other.Finalize(f.Clock.GetUtcNow()); db.Add(other);
                (await db.TeamMemberships.SingleAsync()).Leave(f.Clock.GetUtcNow(), "Fixture move");
                db.Add(new TeamMembership(Guid.NewGuid(), other.Id, f.Players[0].Id, TeamMembershipRole.Participant, f.Clock.GetUtcNow(), null, "Fixture move"));
            }
            else (await db.EventCompetitionSynchronizations.SingleAsync()).Reconfigure(43, "Replacement", f.Event.EventStartsAt, f.Event.EventEndsAt, "replacement", f.Clock.GetUtcNow());
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }
        if (change == "team") await PublishCurrentRosterAsync(f, retainPreviousEntries: true);
        var result = await ReadStatsAsync(f); Assert.False(result.Luck.Stale);
        if (change != "team") { Assert.Null(result.Luck.Result.Percentage); Assert.Null(result.Luck.CalculatedAt); }
        else { Assert.Equal(2, result.Luck.Teams.Count); Assert.Single(result.Luck.Teams.Single(x => x.Name == "Replacement team").Players); }
    }

    [Fact]
    public async Task StatsPass4FiveByFiveObjectivesFinalizationArchiveAndUnfinalizationRetainOfficialHistory()
    {
        var f = await FullStatsFixtureAsync(target: 1, dimensions: 5);
        for (var i = 0; i < 25; i++) await ApproveStatsAsync(f, await PendingStatsAsync(f, i, 0, i + 1));
        await SyncStatsAsync(f, 100);
        var live = await ReadStatsAsync(f); var progress = Assert.Single(live.Teams).Progress;
        EventStatsLuckCheckpoint acceptedCheckpoint;
        await using (var checkpointRead = new ApplicationDbContext(options))
            acceptedCheckpoint = await checkpointRead.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        Assert.True(progress.BoardComplete); Assert.Equal(25, progress.CompletedTiles); Assert.Equal(10, progress.CompletedRows.Count + progress.CompletedColumns.Count);
        Assert.Equal(2, live.Value.Drops); Assert.Equal(25, live.MostVersatile!.DistinctTiles);
        Assert.Equal(13, live.Milestones.Single(x => x.Id == "halfway").At!.Value.Subtract(f.Event.ActualStartedAt!.Value).TotalMinutes);
        Assert.Equal(10, live.ProgressHistory[^1].TotalLines); Assert.Equal(25, live.ProgressHistory[^1].Target);
        Assert.Equal(live.StartedAt, live.Milestones[0].At); Assert.Equal("start", live.Milestones[0].Id);
        var prices = JsonSerializer.Serialize(live.Drops.Select(x => new { x.Item, x.ValueGp, x.PriceHour }));
        var rates = JsonSerializer.Serialize(live.Luck.Sources);
        // End at the configured boundary so its normal 30-minute upload grace is still open.
        f.Clock.Advance(f.Event.EventEndsAt!.Value - f.Clock.GetUtcNow());
        await using (var end = new ApplicationDbContext(options))
        {
            var ev = await end.Events.SingleAsync();
            Assert.True((await new EventLifecycleService(end, null!, f.Clock).EndNowAsync(ev.Id, ev.Version, true, "Fixture end", new(f.Admin.Id, f.Admin.LoginName))).Succeeded);
        }
        await using (var beforeCutoff = new ApplicationDbContext(options))
        {
            var readiness = (await new EventFinalizationService(beforeCutoff, new PublicBoardService(beforeCutoff, f.Clock), f.Clock).GetReadinessAsync(f.Event.Id))!;
            Assert.Contains(readiness.Blockers, blocker => blocker.Key == "submission-window");
        }
        f.Clock.Advance(TimeSpan.FromHours(1));
        var rawCompleted = live.Teams[0].Progress.BoardCompletedAt;
        var corrected = f.Event.ActualStartedAt.Value.AddMinutes(26);
        await using (var db = new ApplicationDbContext(options))
        {
            var service = new EventFinalizationService(db, new PublicBoardService(db, f.Clock), f.Clock);
            var ready = (await service.GetReadinessAsync(f.Event.Id))!;
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CorrectCompletionAsync(
                f.Event.Id, f.Team.Id, corrected, "Retired official correction", f.Admin.Id,
                ready.EventVersion, ready.ReviewCycleId));
            Assert.True(ready.CanFinalize, string.Join("; ", ready.Blockers.Select(x => x.Description)));
            await service.FinalizeAsync(f.Event.Id, new(f.Admin.Id, f.Admin.LoginName), ready.EventVersion);
        }
        var final = await ReadStatsAsync(f); Assert.Equal(rawCompleted, final.Teams[0].OfficialCompletion!.CompletedAt);
        Assert.Equal(rawCompleted, final.Teams[0].Progress.BoardCompletedAt); Assert.Equal(rawCompleted, final.Milestones.Single(x => x.Id == "board").At);
        Assert.True(final.OfficialResult!.IsOfficial); Assert.Equal(live.Luck.EvidenceRevision, final.Luck.EvidenceRevision);
        Assert.Equal(live.Luck.CalculatedAt, final.Luck.CalculatedAt); Assert.Equal(live.Luck.FetchedAt, final.Luck.FetchedAt);
        Assert.Equal(live.Luck.Result.Percentage, final.Luck.Result.Percentage); // lifecycle transitions retain the last accepted checkpoint
        await using (var checkpointVerify = new ApplicationDbContext(options))
        {
            var retainedCheckpoint = await checkpointVerify.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
            Assert.Equal(acceptedCheckpoint.Payload, retainedCheckpoint.Payload);
            Assert.Equal(acceptedCheckpoint.CalculatedAt, retainedCheckpoint.CalculatedAt);
            Assert.Equal(acceptedCheckpoint.FetchedAt, retainedCheckpoint.FetchedAt);
            Assert.Equal(acceptedCheckpoint.UpstreamUpdatedAt, retainedCheckpoint.UpstreamUpdatedAt);
        }
        var archived = await ReadStatsAsync(f); Assert.Equal(EventState.Archived, archived.State); Assert.Equal(rawCompleted, archived.Teams[0].OfficialCompletion!.CompletedAt);
        Assert.Equal(prices, JsonSerializer.Serialize(archived.Drops.Select(x => new { x.Item, x.ValueGp, x.PriceHour }))); Assert.Equal(rates, JsonSerializer.Serialize(archived.Luck.Sources));
        await using (var db = new ApplicationDbContext(options)) await new EventFinalizationService(db, new PublicBoardService(db, f.Clock), f.Clock).UnfinalizeAsync(f.Event.Id, "Legitimate fixture reopening", true, new(f.Admin.Id, f.Admin.LoginName));
        var reopened = await ReadStatsAsync(f); Assert.Equal(EventState.AwaitingFinalReview, reopened.State); Assert.Null(reopened.OfficialResult);
        Assert.Equal(live.Teams[0].Progress.BoardCompletedAt, reopened.Teams[0].Progress.BoardCompletedAt);
        await using var retained = new ApplicationDbContext(options); Assert.Equal(rawCompleted, (await retained.OfficialPlacements.SingleAsync()).BoardCompletedAt);
    }

    [Fact]
    public async Task StatsPass4AdditiveMigrationPreservesPricesRatesAndDoesNotInventCheckpointHistory()
    {
        var f = await FullStatsFixtureAsync(); await SyncStatsAsync(f, 100);
        await using var db = new ApplicationDbContext(options);
        var prices = JsonSerializer.Serialize(await db.EventItemPrices.AsNoTracking().ToListAsync());
        var basis = JsonSerializer.Serialize(await db.EventLuckOutcomeBases.AsNoTracking().ToListAsync());
        // V2 downgrade is fail-closed while retained rows exist; this additive migration
        // rehearsal intentionally removes the controlled fixture checkpoint before rewinding.
        await db.EventStatsLuckCheckpoints.ExecuteDeleteAsync();
        await db.GetService<IMigrator>().MigrateAsync("20260915174600_CacheEventCompetitionBossActivity");
        await RetainedCatalogueMigrationTestSupport.PrepareAsync(db);
        await db.Database.MigrateAsync();
        Assert.Empty(await db.EventStatsLuckCheckpoints.AsNoTracking().ToListAsync());
        Assert.Equal(0, (await db.Events.AsNoTracking().SingleAsync()).StatsEvidenceRevision);
        Assert.Equal(prices, JsonSerializer.Serialize(await db.EventItemPrices.AsNoTracking().ToListAsync()));
        Assert.Equal(basis, JsonSerializer.Serialize(await db.EventLuckOutcomeBases.AsNoTracking().ToListAsync()));
    }

    [Fact]
    public async Task StatsPass4TwoItemsFromOneCompletionAreTwoDropsWithSeparateFrozenValues()
    {
        var f = await FullStatsFixtureAsync(secondOutcome: true);
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10, itemIndex: 1));
        await SyncStatsAsync(f, 100);
        var result = await ReadStatsAsync(f);
        Assert.Equal(2, result.Drops.Count); Assert.Equal(result.Drops[0].SubmittedAt, result.Drops[1].SubmittedAt);
        Assert.Equal(2, result.Drops.Select(x => x.Item.ItemId).Distinct().Count()); Assert.Equal(300, result.Value.ValueGp);
        Assert.Equal(2, result.Luck.Result.Received); Assert.Equal(1.5m, result.Luck.Result.Expected);
        Assert.Equal(f.Items[1].Id, result.MostValuableDrop!.Item.ItemId);
    }

    [Fact]
    public async Task StatsPass4PartialPlayerCoverageAndUnexpectedUnrankedEndKeepHonestFullResults()
    {
        var f = await FullStatsFixtureAsync(players: 2);
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 1, 1, 11));
        var partial = StatsResponse(f, new(0, 100, 100));
        partial = partial with { Competition = partial.Competition! with { Participants = partial.Competition.Participants.Select((x, i) => i == 1 ? x with { Metrics = new Dictionary<string, Bingo.Application.Integrations.WiseOldMan.WiseOldManMetricDelta>() } : x).ToArray() } };
        await SyncStatsAsync(f, partial);
        var incomplete = await ReadStatsAsync(f); Assert.Equal(StatsLuckStatus.Incomplete, incomplete.Luck.Result.Status); Assert.Null(incomplete.Luck.Result.Percentage);
        Assert.Equal(2, incomplete.Luck.Teams[0].Players.Count);
        Assert.Equal(55.0897160098096m, incomplete.Luck.Teams[0].Players.Single(x => x.PlayerId == f.Players[0].Id).Result.Percentage);
        Assert.Null(incomplete.Luck.Teams[0].Players.Single(x => x.PlayerId == f.Players[1].Id).Result.Expected);
        f.Clock.Advance(TimeSpan.FromHours(3)); await SyncStatsAsync(f, 100);
        var good = await ReadStatsAsync(f); Assert.Equal(54.0662189603843m, good.Luck.Result.Percentage);
        f.Clock.Advance(TimeSpan.FromHours(3)); await SyncStatsAsync(f, new Bingo.Application.Integrations.WiseOldMan.WiseOldManMetricDelta(10, -1, 0));
        var retained = await ReadStatsAsync(f); Assert.True(retained.Luck.Stale); Assert.Equal(good.Luck.CalculatedAt, retained.Luck.CalculatedAt);
        Assert.Equal(54.0662189603843m, retained.Luck.Result.Percentage); Assert.Equal(good.Luck.ActivityBatchId, retained.Luck.ActivityBatchId);
    }

    [Fact]
    public async Task StatsPass4RepeatedIncompleteFetchReplacesPriorIncompleteSnapshot()
    {
        var f = await FullStatsFixtureAsync(secondOutcome: true);
        await using (var setup = new ApplicationDbContext(options))
        {
            (await setup.BossActivities.SingleAsync(x => x.Id == f.Boss.Id)).ConfigureApi(null);
            await setup.SaveChangesAsync();
        }

        await SyncStatsAsync(f, 100);
        EventStatsLuckCheckpoint first;
        await using (var read = new ApplicationDbContext(options))
            first = await read.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        var firstStats = await ReadStatsAsync(f);
        Assert.Equal(StatsLuckStatus.WaitingForActivityData, firstStats.Luck.Result.Status);
        Assert.Null(firstStats.Luck.Result.Percentage);

        f.Clock.Advance(TimeSpan.FromHours(3));
        await SyncStatsAsync(f, 200);
        EventStatsLuckCheckpoint second;
        await using (var read = new ApplicationDbContext(options))
            second = await read.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        var secondStats = await ReadStatsAsync(f);
        Assert.Equal(StatsLuckStatus.WaitingForActivityData, secondStats.Luck.Result.Status);
        Assert.Null(secondStats.Luck.Result.Percentage);
        Assert.NotEqual(first.ActivityBatchId, second.ActivityBatchId);
        Assert.NotEqual(first.Payload, second.Payload);
        Assert.Equal(second.ActivityBatchId, secondStats.Luck.ActivityBatchId);
    }

    [Fact]
    public async Task StatsPass4CollectiveMilestonesAndAggregateHistoryUseAllTeamsAndEndedUnreachedState()
    {
        var f = await FullStatsFixtureAsync(players: 2, target: 1);
        await using (var db = new ApplicationDbContext(options))
        {
            var team = new Team(Guid.NewGuid(), f.Event.Id, "Other team", "other-team", TeamFormationType.Drafted, null, true); team.Finalize(f.Clock.GetUtcNow()); db.Add(team);
            (await db.TeamMemberships.SingleAsync(x => x.EventParticipantId == f.Players[1].Id)).Leave(f.Clock.GetUtcNow(), "Fixture roster");
            db.Add(new TeamMembership(Guid.NewGuid(), team.Id, f.Players[1].Id, TeamMembershipRole.Participant, f.Clock.GetUtcNow(), null, "Fixture roster")); await db.SaveChangesAsync();
        }
        await PublishCurrentRosterAsync(f);
        for (var tile = 0; tile < 4; tile++) await ApproveStatsAsync(f, await PendingStatsAsync(f, tile, 0, tile + 1));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 1, 5));
        var live = await ReadStatsAsync(f); Assert.Equal(2, live.Teams.Count);
        Assert.Equal(f.Event.ActualStartedAt!.Value.AddMinutes(5), live.Milestones.Single(x => x.Id == "all-submissions").At);
        Assert.Equal(f.Event.ActualStartedAt.Value.AddMinutes(5), live.Milestones.Single(x => x.Id == "all-tiles").At);
        Assert.Null(live.Milestones.Single(x => x.Id == "all-boards").At);
        var aggregate = live.ProgressHistory[^1]; Assert.Equal(5, aggregate.CompletedTiles); Assert.Equal(8, aggregate.TotalTiles); Assert.Equal(8, aggregate.TotalLines); Assert.Equal(1, aggregate.CompletedBoards);
        await using (var db = new ApplicationDbContext(options))
        {
            var ev = await db.Events.SingleAsync(); Assert.True((await new EventLifecycleService(db, null!, f.Clock).EndNowAsync(ev.Id, ev.Version, true, "Fixture early end", new(f.Admin.Id, f.Admin.LoginName))).Succeeded);
        }
        var ended = await ReadStatsAsync(f); Assert.Equal(StatsMilestoneState.NotReached, ended.Milestones.Single(x => x.Id == "all-boards").State);
        Assert.Equal(f.Clock.GetUtcNow(), ended.EndedAt); Assert.Equal("start", ended.Milestones[0].Id);
        Assert.All(ended.Teams, team => Assert.DoesNotContain(team.Milestones, x => x.Id.StartsWith("all-", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task StatsPass4OversizedAcceptedFetchRetainsPreviousCheckpointWithBoundedDiagnostic()
    {
        var f = await FullStatsFixtureAsync();
        await SyncStatsAsync(f, 100);
        EventStatsLuckCheckpoint original;
        await using (var read = new ApplicationDbContext(options))
            original = await read.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();

        const int outcomeCount = 30_000;
        await using (var setup = new ApplicationDbContext(options))
        {
            var approvalRequirement = await setup.BoardApprovalRequirementSnapshots
                .SingleAsync(x => x.BoardRequirementSnapshotId == f.Requirements[0].Id);
            var items = Enumerable.Range(0, outcomeCount)
                .Select(index => new CatalogueItem(Guid.NewGuid(), $"Oversized retained outcome {index:D5}", $"OVERSIZED RETAINED OUTCOME {index:D5}"))
                .ToArray();
            var sources = items.Select(item => new SourceDrop(Guid.NewGuid(), f.Boss.Id, item.Id, "1/100", .01m, 1, f.Clock.GetUtcNow())).ToArray();
            var workingDrops = items.Select((item, index) =>
            {
                var source = sources[index];
                return new BoardRequirementDropSnapshot(Guid.NewGuid(), f.Requirements[0].Id, source.Id, item.Id, f.Boss.Name,
                    item.Name, source.DisplayRate, source.NumericProbability, null, source.DefaultEhbEstimate, 1,
                    source.ProbabilityScope, source.ConditionalOnParent, source.ParentProbability, source.AssumedParticipants,
                    source.RollsPerCompletion, source.RollGroup, source.RateConditionNote);
            }).ToArray();
            var approvalDrops = items.Select((item, index) =>
            {
                var source = sources[index];
                return new BoardApprovalRequirementDropSnapshot(Guid.NewGuid(), approvalRequirement.Id, source.Id, item.Id,
                    f.Boss.Name, item.Name, source.DisplayRate, source.NumericProbability, null, source.DefaultEhbEstimate, 1,
                    f.Boss.Version, source.ProbabilityScope, source.ConditionalOnParent, source.ParentProbability,
                    source.AssumedParticipants, source.RollsPerCompletion, source.RollGroup, source.RateConditionNote);
            }).ToArray();
            setup.AddRange(items);
            setup.AddRange(sources);
            setup.AddRange(workingDrops);
            setup.AddRange(approvalDrops);
            await setup.SaveChangesAsync();
        }

        f.Clock.Advance(TimeSpan.FromHours(3));
        EventCompetitionRefreshResult refresh;
        await using (var sync = new ApplicationDbContext(options))
        {
            var delta = new WiseOldManMetricDelta(0, 100, 100);
            var response = StatsResponse(f, delta);
            var service = new EventCompetitionSynchronizationService(sync, new StatsClient(response, response, null), new FixedStatus(), f.Clock);
            refresh = await service.RefreshAsync(f.Event.Id, new LifecycleActor(f.Admin.Id, f.Admin.LoginName));
        }

        Assert.True(refresh.Succeeded);
        Assert.Contains("checkpoint-discarded", refresh.Message, StringComparison.Ordinal);
        await using var verify = new ApplicationDbContext(options);
        var retained = await verify.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        Assert.Equal(original.Payload, retained.Payload);
        Assert.Equal(original.CalculatedAt, retained.CalculatedAt);
        Assert.Equal(original.FetchedAt, retained.FetchedAt);
        Assert.Equal(original.UpstreamUpdatedAt, retained.UpstreamUpdatedAt);
    }

    [Fact]
    public async Task StatsPass4CheckpointSchemaAndPayloadBoundsAreEnforcedByPostgres()
    {
        var f = await FullStatsFixtureAsync(); await SyncStatsAsync(f, 100);
        await using var db = new ApplicationDbContext(options);
        var original = (await db.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync()).Payload;
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE event_stats_luck_checkpoints SET schema_version = 3"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE event_stats_luck_checkpoints SET payload = jsonb_build_object('oversize', repeat('x', 8388609))"));
        Assert.Equal(original, (await db.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync()).Payload);
        var result = await ReadStatsAsync(f); AssertLuckScore(18.3016170636616m, result.Luck.Result);
    }

    [Fact]
    public async Task StatsPass4AcceptedFetchCheckpointFailureRollsBackSynchronizationAtomically()
    {
        var f = await FullStatsFixtureAsync(); await SyncStatsAsync(f, 100);
        var original = await ReadStatsAsync(f);
        EventStatsLuckCheckpoint originalCheckpoint;
        await using (var read = new ApplicationDbContext(options))
            originalCheckpoint = await read.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        f.Clock.Advance(TimeSpan.FromHours(3));
        await using (var setup = new ApplicationDbContext(options))
            await setup.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION reject_stats_checkpoint_fixture() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Controlled checkpoint failure' USING ERRCODE = '23514'; END $$;
                CREATE TRIGGER reject_stats_checkpoint_fixture BEFORE UPDATE ON event_stats_luck_checkpoints
                FOR EACH ROW EXECUTE FUNCTION reject_stats_checkpoint_fixture();
                """);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => SyncStatsAsync(f, 100));
        var after = await ReadStatsAsync(f);
        Assert.Equal(original.Luck.Result.Received, after.Luck.Result.Received);
        Assert.Equal(original.Luck.Result.Percentage, after.Luck.Result.Percentage);
        Assert.Equal(original.Luck.CalculatedAt, after.Luck.CalculatedAt);
        Assert.Equal(original.Luck.FetchedAt, after.Luck.FetchedAt);
        await using var verify = new ApplicationDbContext(options);
        var retained = await verify.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        Assert.Equal(originalCheckpoint.Payload, retained.Payload);
        Assert.Equal(originalCheckpoint.CalculatedAt, retained.CalculatedAt);
        Assert.Equal(originalCheckpoint.FetchedAt, retained.FetchedAt);
        Assert.Equal(originalCheckpoint.ActivityBatchId, retained.ActivityBatchId);
        var activity = await verify.EventCompetitionCharacterMetricActivities.AsNoTracking().SingleAsync();
        Assert.Equal(100m, activity.Gained);
    }

    private async Task ReplaceStatsApprovalAsync(FullStatsFixture f, bool addObjective)
    {
        await using var db = new ApplicationDbContext(options); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var ev = await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {f.Event.Id} FOR UPDATE").SingleAsync();
        var board = await db.Boards.SingleAsync(x => x.EventId == f.Event.Id);
        var previous = await db.BoardApprovalSnapshots.SingleAsync(x => x.Id == board.ActiveApprovalSnapshotId);
        var approval = new BoardApprovalSnapshot(Guid.NewGuid(), board.Id, previous.Version + 1, f.Clock.GetUtcNow(), f.Admin.Id, previous.Id, previous.Name,
            previous.Rows, previous.Columns, previous.TotalEhbEstimate, previous.CalculationVersion, board.Version, BoardState.Published);
        db.Add(approval);
        foreach (var tile in f.Tiles)
        {
            var frozen = new BoardApprovalTileSnapshot(Guid.NewGuid(), approval.Id, tile.Id, tile.TileTemplateId, tile.RowIndex, tile.ColumnIndex, tile.NameSnapshot, "", "", tile.EstimatedEhbSnapshot, null); db.Add(frozen);
            var req = f.Requirements.Single(x => x.BoardTileId == tile.Id); var adding = addObjective && tile.Id == f.Tiles[2].Id;
            var requirement = new BoardApprovalRequirementSnapshot(Guid.NewGuid(), frozen.Id, req.Id, 1, req.TargetContribution, true, true, 1, req.Description, req.ManualObjective && !adding); db.Add(requirement);
            if (!req.ManualObjective || adding) db.Add(new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), requirement.Id, f.Boss.Id, f.Boss.Name, 10, 1));
            foreach (var drop in f.Drops.Where(x => x.RequirementId == req.Id))
                db.Add(new BoardApprovalRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, drop.SourceDropId, drop.ItemIdSnapshot, drop.BossName, drop.ItemName, "1/2", .5m, null, 1, 1, 1));
            if (adding)
            {
                var item = new CatalogueItem(Guid.NewGuid(), "New actual outcome", "NEW ACTUAL OUTCOME"); item.SetPrice(1, CataloguePriceSource.Manual, f.Clock.GetUtcNow());
                var source = new SourceDrop(Guid.NewGuid(), f.Boss.Id, item.Id, "1/100", .01m, 1, f.Clock.GetUtcNow());
                db.AddRange(item, source, new BoardRequirementDropSnapshot(Guid.NewGuid(), req.Id, source.Id, item.Id, f.Boss.Name, item.Name, "1/100", .01m, null, 1),
                    new BoardApprovalRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, source.Id, item.Id, f.Boss.Name, item.Name, "1/100", .01m, null, 1, 1, 1));
            }
        }
        board.ReplacePublishedApproval(approval.Id); ev.AdvanceStatsEvidenceRevision();
        await db.SaveChangesAsync(); await db.RetainLuckOutcomeBasesAsync(ev.Id, f.Clock.GetUtcNow()); await db.SaveChangesAsync(); await tx.CommitAsync();
    }

    private sealed class AfterStatsEventRead(Func<Task> mutation) : DbCommandInterceptor
    {
        public bool Ran { get; private set; }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!Ran && command.CommandText.Contains("FROM events AS", StringComparison.Ordinal) && command.CommandText.Contains("stats_evidence_revision", StringComparison.Ordinal))
            { Ran = true; await mutation(); }
            return result;
        }
    }
}
