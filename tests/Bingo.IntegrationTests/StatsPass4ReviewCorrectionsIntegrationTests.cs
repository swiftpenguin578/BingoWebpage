using System.Data;
using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Stats;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Stats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace Bingo.IntegrationTests;

public sealed partial class Slice10Pass102CompetitionSynchronizationTests
{
    [Fact]
    public async Task StatsPass4Lkp1MixedUnsupportedOutcomeIsIncompleteAndReplaceableUntilComplete()
    {
        var f = await FullStatsFixtureAsync(secondOutcome: true, secondMetric: "unsupported_fixture", players: 2,
            clockNow: new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero).AddTicks(123450));
        for (var player = 0; player < 2; player++)
            await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, player, 10 + player, itemIndex: player));

        await SyncStatsAsync(f, 100);
        var first = await ReadLkp1CheckpointAsync();
        var mixed = await ReadStatsAsync(f);
        AssertLkp1Incomplete(mixed.Luck.Result);
        var team = Assert.Single(mixed.Luck.Teams);
        AssertLkp1Incomplete(team.Result);
        Assert.Equal(2, team.Players.Count);
        Assert.All(team.Players, player =>
        {
            AssertLkp1Incomplete(player.Result);
            var supported = Assert.Single(player.Sources, source => source.ItemId == f.Items[0].Id);
            Assert.Equal(StatsLuckStatus.Calculated, supported.Status);
            Assert.Equal(100m, supported.Activity); Assert.Equal(1m, supported.Expected);
            var unsupported = Assert.Single(player.Sources, source => source.ItemId == f.Items[1].Id);
            Assert.Equal(StatsLuckStatus.WaitingForActivityData, unsupported.Status);
            Assert.Null(unsupported.Activity); Assert.Null(unsupported.Expected);
        });
        Assert.Null(mixed.Luck.Sources.Single(source => source.ItemId == f.Items[0].Id).UnavailableReason);
        Assert.Equal("Unmapped", mixed.Luck.Sources.Single(source => source.ItemId == f.Items[1].Id).UnavailableReason);
        Assert.Equal("SourceBasisUnavailable", mixed.Luck.UnavailableReason);

        f.Clock.Advance(TimeSpan.FromHours(2));
        await SyncStatsAsync(f, 200);
        var second = await ReadLkp1CheckpointAsync();
        Assert.Equal(first.SourceFingerprint, second.SourceFingerprint);
        Assert.Equal(first.AssignmentFingerprint, second.AssignmentFingerprint);
        Assert.NotEqual(first.ActivityBatchId, second.ActivityBatchId);
        Assert.NotEqual(first.Payload, second.Payload);
        Assert.Equal(f.Clock.GetUtcNow(), second.CalculatedAt);
        var replaced = await ReadStatsAsync(f);
        AssertLkp1Incomplete(replaced.Luck.Result);
        Assert.Equal(second.ActivityBatchId, replaced.Luck.ActivityBatchId);
        Assert.All(replaced.Luck.Teams.SelectMany(t => t.Players), player =>
            Assert.Equal(200m, player.Sources.Single(source => source.ItemId == f.Items[0].Id).Activity));
        await using (var verify = new ApplicationDbContext(options))
            Assert.DoesNotContain("partial-batch-write-fenced", (await verify.EventCompetitionSynchronizations.SingleAsync()).LastError ?? "", StringComparison.Ordinal);

        // A previously unsupported boss can bind its first verified supported metric.
        // This changes source identity; the preceding replacement proved the compatible fence.
        await using (var setup = new ApplicationDbContext(options))
        {
            var boss = await setup.BossActivities.SingleAsync(x => x.Id == f.SecondBoss!.Id);
            boss.ConfigureApi("zulrah"); boss.RecordMapping(Bingo.Domain.Catalogue.ApiMappingStatus.Verified, f.Clock.GetUtcNow());
            await setup.SaveChangesAsync();
        }
        f.Clock.Advance(TimeSpan.FromHours(2));
        await SyncStatsAsync(f, Lkp1Response(f, new(0, 100, 100), new(0, 100, 100)));
        var complete = await ReadLkp1CheckpointAsync();
        var calculated = await ReadStatsAsync(f);
        Assert.NotEqual(second.ActivityBatchId, complete.ActivityBatchId);
        Assert.NotEqual(second.Payload, complete.Payload);
        Assert.Equal(complete.ActivityBatchId, calculated.Luck.ActivityBatchId);
        Assert.Equal(f.Clock.GetUtcNow(), complete.CalculatedAt);
        Assert.All(calculated.Luck.Sources, source => Assert.Null(source.UnavailableReason));
        Assert.All(calculated.Luck.Teams.SelectMany(t => t.Players).Select(p => p.Result)
            .Concat(calculated.Luck.Teams.Select(t => t.Result)).Append(calculated.Luck.Result), result =>
        { Assert.Equal(StatsLuckStatus.Calculated, result.Status); Assert.NotNull(result.Percentage); });
        Assert.Equal(3m, calculated.Luck.Result.Expected);
    }

    [Fact]
    public async Task StatsPass4Lkp1MixedMissingOutcomeIsReplacedByCompatibleCompleteBatch()
    {
        var f = await FullStatsFixtureAsync(secondOutcome: true, secondMetric: "zulrah");
        await SyncStatsAsync(f, Lkp1Response(f, new(0, 100, 100), null));
        var incomplete = await ReadLkp1CheckpointAsync();
        var mixed = await ReadStatsAsync(f);
        AssertLkp1Incomplete(mixed.Luck.Result);
        AssertLkp1Incomplete(Assert.Single(mixed.Luck.Teams).Result);
        AssertLkp1Incomplete(Assert.Single(Assert.Single(mixed.Luck.Teams).Players).Result);
        Assert.All(mixed.Luck.Sources, source => Assert.Null(source.UnavailableReason));
        f.Clock.Advance(TimeSpan.FromHours(2));
        await SyncStatsAsync(f, Lkp1Response(f, new(0, 200, 200), new(0, 100, 100)));
        var complete = await ReadLkp1CheckpointAsync();
        var view = await ReadStatsAsync(f);
        Assert.Equal(incomplete.SourceFingerprint, complete.SourceFingerprint);
        Assert.Equal(incomplete.AssignmentFingerprint, complete.AssignmentFingerprint);
        Assert.NotEqual(incomplete.ActivityBatchId, complete.ActivityBatchId);
        Assert.NotEqual(incomplete.Payload, complete.Payload);
        Assert.Equal(complete.ActivityBatchId, view.Luck.ActivityBatchId);
        Assert.Equal(f.Clock.GetUtcNow(), complete.CalculatedAt);
        Assert.All(view.Luck.Teams.SelectMany(t => t.Players).Select(p => p.Result)
            .Concat(view.Luck.Teams.Select(t => t.Result)).Append(view.Luck.Result), result =>
        { Assert.Equal(StatsLuckStatus.Calculated, result.Status); Assert.NotNull(result.Percentage); });
        Assert.Equal(2.5m, view.Luck.Result.Expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatsPass4Lkp1PartialBatchRetainsWholeCompleteSnapshotThroughPublicationAndArchive(bool unranked)
    {
        var f = await FullStatsFixtureAsync(secondOutcome: true, secondMetric: "zulrah",
            clockNow: new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero).AddTicks(123450));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        var upstreamInput = f.Clock.GetUtcNow().AddMinutes(-5).AddTicks(7);
        var response = Lkp1Response(f, new(0, 100, 100), new(0, 40, 40));
        response = response with
        {
            Competition = response.Competition! with
            {
                Participants = response.Competition.Participants.Select(p => p with { UpstreamUpdatedAt = upstreamInput }).ToArray()
            }
        };
        CountingFinalReviewClient provider;
        await using (var setup = new ApplicationDbContext(options))
        {
            var names = await setup.OsrsCharacters.ToDictionaryAsync(x => x.Id, x => x.DisplayName);
            response = response with
            {
                Competition = response.Competition! with
                {
                    Participants = response.Competition.Participants.Select(p => p with { Username = names[Guid.Parse(p.Username)] }).ToArray()
                }
            };
            provider = new CountingFinalReviewClient(response, response);
            var sync = new EventCompetitionSynchronizationService(setup, provider, new FixedStatus(), f.Clock);
            Assert.True((await sync.ConfigureAsync(f.Event.Id, (await setup.Events.SingleAsync()).Version, 42, false,
                new(f.Admin.Id, f.Admin.LoginName))).Succeeded);
            Assert.True((await sync.RefreshAsync(f.Event.Id, new(f.Admin.Id, f.Admin.LoginName))).Succeeded);
        }
        Assert.Equal(1, provider.Calls); Assert.Equal(1, provider.ValidationCalls);
        var original = await ReadLkp1CheckpointAsync();
        var originalView = await ReadStatsAsync(f);
        var persistedUpstream = upstreamInput.AddTicks(-(upstreamInput.Ticks % 10));
        Assert.NotEqual(upstreamInput, persistedUpstream);
        Assert.Equal(persistedUpstream, original.UpstreamUpdatedAt);
        Assert.Equal(persistedUpstream, originalView.Luck.UpstreamUpdatedAt);
        Assert.Equal(f.Clock.GetUtcNow(), original.CalculatedAt);
        Assert.Equal(f.Clock.GetUtcNow(), original.FetchedAt);
        Assert.Equal(StatsLuckStatus.Calculated, originalView.Luck.Result.Status);
        Assert.Equal(1.2m, originalView.Luck.Result.Expected);

        f.Clock.Advance(TimeSpan.FromHours(2));
        await SyncStatsAsync(f, Lkp1Response(f, new(0, 900, 900), unranked ? new(40, -1, 0) : null));
        await using (var verify = new ApplicationDbContext(options))
        {
            var state = await verify.EventCompetitionSynchronizations.AsNoTracking().SingleAsync();
            Assert.False(state.LatestMetricsComplete);
            Assert.NotEqual(original.ActivityBatchId, state.MetricActivityBatchId);
            var rows = await verify.EventCompetitionCharacterMetricActivities.AsNoTracking().ToListAsync();
            Assert.Equal(900m, rows.Single(row => row.Metric == "vorkath").RecordedActivity());
            Assert.Equal(unranked ? Bingo.Domain.Integrations.WiseOldMan.MetricActivityCoverage.UnexpectedUnrankedEnd
                : Bingo.Domain.Integrations.WiseOldMan.MetricActivityCoverage.Missing, rows.Single(row => row.Metric == "zulrah").LastIssue);
        }
        AssertLkp1RetainedCheckpoint(original, await ReadLkp1CheckpointAsync());
        // Current approved evidence changes, so any read-time rescore would change the numerator.
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 20, itemIndex: 1));
        var savedLuck = JsonSerializer.Deserialize<StatsLuck>(original.Payload)!;
        for (var read = 0; read < 2; read++)
        {
            var view = await ReadStatsAsync(f);
            Assert.Equal(2, view.Value.Drops);
            Assert.Equal(JsonSerializer.Serialize(savedLuck with { Stale = true, Tiles = null }), JsonSerializer.Serialize(view.Luck));
            var tile = await ReadTileActivityAsync(f);
            var savedTile = Assert.Single(savedLuck.Tiles!, t => t.TileId == f.Tiles[0].Id);
            Assert.Equal(JsonSerializer.Serialize(Assert.Single(savedTile.Teams)), JsonSerializer.Serialize(tile.Team));
            Assert.Equal(original.CalculatedAt, tile.CalculatedAt); Assert.Equal(original.FetchedAt, tile.FetchedAt);
            Assert.Equal(original.EvidenceRevision, tile.EvidenceRevision); Assert.True(tile.Stale);
            AssertLkp1RetainedCheckpoint(original, await ReadLkp1CheckpointAsync());
        }
        Assert.Equal(1, provider.Calls); Assert.Equal(1, provider.ValidationCalls);

        f.Clock.Advance(TimeSpan.FromSeconds(55));
        await using (var end = new ApplicationDbContext(options))
        {
            var ev = await end.Events.SingleAsync();
            Assert.True((await new EventLifecycleService(end, null!, f.Clock).EndNowAsync(ev.Id, ev.Version, true,
                "LKP-1 retained snapshot", new(f.Admin.Id, f.Admin.LoginName))).Succeeded);
        }
        f.Clock.Advance(TimeSpan.FromHours(2));
        await using (var db = new ApplicationDbContext(options))
        {
            var sync = new EventCompetitionSynchronizationService(db, provider, new FixedStatus(), f.Clock);
            var finalization = new EventFinalizationService(db, new PublicBoardService(db, f.Clock), f.Clock, competitionSynchronization: sync);
            var readiness = (await finalization.GetReadinessAsync(f.Event.Id))!;
            Assert.True(readiness.CanFinalize, string.Join("; ", readiness.Blockers.Select(b => b.Description)));
            Assert.True((await finalization.FinalizeAsync(f.Event.Id, new(f.Admin.Id, f.Admin.LoginName), readiness.EventVersion)).Published);
        }
        for (var read = 0; read < 2; read++)
        {
            var archived = await ReadStatsAsync(f);
            Assert.Equal(EventState.Archived, archived.State); Assert.True(archived.OfficialResult!.IsOfficial);
            Assert.Equal(JsonSerializer.Serialize(savedLuck with { Stale = true, Tiles = null }), JsonSerializer.Serialize(archived.Luck));
            var tile = await ReadTileActivityAsync(f);
            var savedTile = Assert.Single(savedLuck.Tiles!, t => t.TileId == f.Tiles[0].Id);
            Assert.Equal(JsonSerializer.Serialize(Assert.Single(savedTile.Teams)), JsonSerializer.Serialize(tile.Team));
            Assert.Equal(original.CalculatedAt, tile.CalculatedAt); Assert.Equal(original.FetchedAt, tile.FetchedAt);
            Assert.Equal(original.EvidenceRevision, tile.EvidenceRevision); Assert.True(tile.Stale);
            AssertLkp1RetainedCheckpoint(original, await ReadLkp1CheckpointAsync());
        }
        Assert.Equal(1, provider.Calls); Assert.Equal(1, provider.ValidationCalls);
    }

    private async Task<EventStatsLuckCheckpoint> ReadLkp1CheckpointAsync()
    {
        await using var db = new ApplicationDbContext(options);
        return await db.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
    }

    private static void AssertLkp1Incomplete(StatsLuckResult result)
    {
        Assert.Equal(StatsLuckStatus.Incomplete, result.Status);
        Assert.Null(result.Expected); Assert.Null(result.Percentage); Assert.Null(result.KcDifference);
    }

    private static void AssertLkp1RetainedCheckpoint(EventStatsLuckCheckpoint original, EventStatsLuckCheckpoint retained)
    {
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(original.Payload), System.Text.Encoding.UTF8.GetBytes(retained.Payload));
        Assert.Equal(original.ActivityBatchId, retained.ActivityBatchId);
        Assert.Equal(original.EvidenceRevision, retained.EvidenceRevision);
        Assert.Equal(original.SourceFingerprint, retained.SourceFingerprint);
        Assert.Equal(original.AssignmentFingerprint, retained.AssignmentFingerprint);
        Assert.Equal(original.CalculatedAt, retained.CalculatedAt);
        Assert.Equal(original.FetchedAt, retained.FetchedAt);
        Assert.Equal(original.UpstreamUpdatedAt, retained.UpstreamUpdatedAt);
    }

    private static WiseOldManCompetitionResult Lkp1Response(FullStatsFixture f, WiseOldManMetricDelta supported, WiseOldManMetricDelta? second)
    {
        var response = StatsResponse(f, supported);
        return response with
        {
            Competition = response.Competition! with
            {
                Participants = response.Competition.Participants.Select(p => p with
                {
                    Metrics = second is null ? p.Metrics : new Dictionary<string, WiseOldManMetricDelta>(p.Metrics!) { ["zulrah"] = second }
                }).ToArray()
            }
        };
    }

    [Fact]
    public async Task StatsPass4ReviewF1AggregatePreservesCumulativeSequenceForOpposingIdsAtTheSameTime()
    {
        var f = await FullStatsFixtureAsync(target: 2);
        var at = f.Event.ActualStartedAt!.Value.AddMinutes(10);
        var submissionIds = Enumerable.Range(0, 4).Select(i => new Guid(4 - i, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)).ToArray();
        for (var i = 0; i < submissionIds.Length; i++)
        {
            var tile = i / 2; var drop = f.Drops.Single(x => x.RequirementId == f.Requirements[tile].Id);
            await using (var db = new ApplicationDbContext(options))
            {
                db.Add(new Submission(submissionIds[i], f.Event.Id, f.Team.Id, f.Tiles[tile].Id, f.Requirements[tile].Id, drop.Id,
                    f.Players[0].Id, f.Characters[0].Id, f.Characters[0].DisplayName, f.Admin.Id, 1, at, null, null));
                await db.SaveChangesAsync();
            }
            await ApproveStatsAsync(f, submissionIds[i]);
            await using var fixture = new ApplicationDbContext(options);
            var contributionId = new Guid(100 + i, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            await fixture.Database.ExecuteSqlInterpolatedAsync($"UPDATE submission_contributions SET id = {contributionId} WHERE submission_id = {submissionIds[i]}");
        }
        var result = await ReadStatsAsync(f);
        var team = Assert.Single(result.Teams);
        Assert.Equal(submissionIds, team.ProgressHistory.Select(x => x.SubmissionId));
        Assert.Equal(submissionIds, result.ProgressHistory.Select(x => x.SubmissionId));
        Assert.Equal([1, 2, 3, 4], result.ProgressHistory.Select(x => x.Approved));
        Assert.Equal([0, 1, 1, 2], result.ProgressHistory.Select(x => x.CompletedTiles));
        Assert.Equal([0, 0, 0, 1], result.ProgressHistory.Select(x => x.CompletedLines));
        Assert.Equal(team.Progress.Tiles.Sum(x => x.Approved), result.ProgressHistory[^1].Approved);
        Assert.Equal(team.Progress.CompletedTiles, result.ProgressHistory[^1].CompletedTiles);
        Assert.All(result.Milestones.Where(x => x.Id is "tile" or "halfway" or "row"), x =>
        { Assert.Equal(StatsMilestoneState.Reached, x.State); Assert.Equal(at, x.At); });
        Assert.Equal(["tile", "halfway", "row"], result.Milestones.Where(x => x.Id is "tile" or "halfway" or "row").Select(x => x.Id));
    }

    [Theory]
    [InlineData("fresh")]
    [InlineData("expired")]
    public async Task StatsPass4ReviewF2ScheduledEndCapturesOnlyCompatibleFreshResultsAtomically(string scenario)
    {
        var f = await FullStatsFixtureAsync();
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        var end = f.Event.EventEndsAt!.Value;
        f.Clock.Advance(end - f.Clock.GetUtcNow() - (scenario == "expired" ? TimeSpan.FromHours(3) : TimeSpan.FromMinutes(1)));
        await SyncStatsAsync(f, 100);
        var live = await ReadStatsAsync(f);
        EventStatsLuckCheckpoint original;
        await using (var db = new ApplicationDbContext(options))
            original = await db.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        f.Clock.Advance(end - f.Clock.GetUtcNow());
        await using (var db = new ApplicationDbContext(options))
        {
            var signup = new EventSignupLifecycleService(db, new EventReadinessEvaluator(db, new ConfigurationBuilder().Build()), f.Clock);
            await new EventLifecycleService(db, signup, f.Clock).ProcessDueAsync();
        }
        var ended = await ReadStatsAsync(f);
        await using var verify = new ApplicationDbContext(options);
        var saved = await verify.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        Assert.Equal(EventState.AwaitingFinalReview, ended.State); Assert.Equal(end, ended.EndedAt);
        Assert.Equal(original.EvidenceRevision, saved.EvidenceRevision);
        Assert.Equal(original.Payload, saved.Payload);
        Assert.Equal(live.Luck.Result.Received, ended.Luck.Result.Received);
        Assert.Equal(live.Luck.Result.Expected, ended.Luck.Result.Expected);
        Assert.Equal(live.Luck.Result.Percentage, ended.Luck.Result.Percentage);
        Assert.Equal(live.Luck.CalculatedAt, ended.Luck.CalculatedAt);
        Assert.Equal(live.Luck.FetchedAt, ended.Luck.FetchedAt);
        Assert.Equal(live.Luck.UpstreamUpdatedAt, ended.Luck.UpstreamUpdatedAt);
        f.Clock.Advance(TimeSpan.FromHours(3));
        var aged = await ReadStatsAsync(f);
        Assert.Equal(ended.Luck.CalculatedAt, aged.Luck.CalculatedAt);
        Assert.Equal(ended.Luck.FetchedAt, aged.Luck.FetchedAt);
        Assert.Equal(ended.Luck.Result.Percentage, aged.Luck.Result.Percentage);
        Assert.Equal(ended.Luck.Result.Received, aged.Luck.Result.Received);
        Assert.Equal(original.ActivityBatchId, (await verify.EventCompetitionSynchronizations.AsNoTracking().SingleAsync()).MetricActivityBatchId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatsPass4ReviewF3AdditiveApprovalDuringOutageRetainsTheEntireOldLuckUntilAcceptedFetch(bool reverseNewDrop)
    {
        var f = await FullStatsFixtureAsync();
        var first = await PendingStatsAsync(f, 0, 0, 10); await ApproveStatsAsync(f, first); await SyncStatsAsync(f, 100);
        var original = await ReadStatsAsync(f);
        f.Clock.Advance(TimeSpan.FromHours(3));
        await SyncStatsAsync(f, new WiseOldManCompetitionResult(WiseOldManCompetitionStatus.Unavailable, null, Message: "Controlled outage"));
        var added = await PendingStatsAsync(f, 0, 0, 20); await ApproveStatsAsync(f, added);
        var additive = await ReadStatsAsync(f);
        Assert.Equal(2, additive.Value.Drops); Assert.Equal(200, additive.Value.ValueGp); Assert.Equal(original.EvidenceRevision + 1, additive.EvidenceRevision);
        Assert.Equal(JsonSerializer.Serialize(original.Luck with { Stale = true }), JsonSerializer.Serialize(additive.Luck));
        Assert.Equal(original.Luck.Result.Received, additive.Luck.Result.Received); Assert.Equal(original.Luck.Result.Expected, additive.Luck.Result.Expected); Assert.Equal(original.Luck.Result.Percentage, additive.Luck.Result.Percentage);
        await ReverseStatsAsync(f, reverseNewDrop ? added : first);
        var reversed = await ReadStatsAsync(f); Assert.Equal(1, reversed.Value.Drops);
        Assert.Equal(JsonSerializer.Serialize(original.Luck with { Stale = true }), JsonSerializer.Serialize(reversed.Luck));
        // Reversal records a new evidence boundary, while the last successful snapshot remains the public value.
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 30));
        var later = await ReadStatsAsync(f); Assert.Equal(2, later.Value.Drops); Assert.Equal(JsonSerializer.Serialize(original.Luck with { Stale = true }), JsonSerializer.Serialize(later.Luck));
        await using var verify = new ApplicationDbContext(options);
        var ev = await verify.Events.AsNoTracking().SingleAsync();
        Assert.True(ev.StatsLuckInvalidatedAtRevision > original.EvidenceRevision);
        Assert.Equal(original.EvidenceRevision, (await verify.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync()).EvidenceRevision);
    }

    [Fact]
    public async Task StatsPass4ReviewF3FreshApprovalRetainsExactCheckpointUntilAcceptedFetch()
    {
        var f = await FullStatsFixtureAsync(); await SyncStatsAsync(f, 100);
        EventStatsLuckCheckpoint old;
        var original = await ReadStatsAsync(f);
        await using (var db = new ApplicationDbContext(options)) old = await db.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        var result = await ReadStatsAsync(f);
        Assert.Equal(original.Luck.Result.Received, result.Luck.Result.Received);
        Assert.Equal(original.Luck.Result.Expected, result.Luck.Result.Expected);
        Assert.Equal(original.Luck.Result.Percentage, result.Luck.Result.Percentage);
        Assert.Equal(original.Luck.CalculatedAt, result.Luck.CalculatedAt);
        Assert.Equal(original.Luck.FetchedAt, result.Luck.FetchedAt);
        Assert.Equal(original.Luck.UpstreamUpdatedAt, result.Luck.UpstreamUpdatedAt);
        await using var write = new ApplicationDbContext(options); await using var tx = await write.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var current = await write.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        Assert.Equal(old.Payload, current.Payload); Assert.Equal(old.ActivityBatchId, current.ActivityBatchId);
        Assert.False(await PublicStatsService.TryWriteCheckpointAsync(write, f.Clock, old)); await tx.CommitAsync();
    }

    [Fact]
    public async Task StatsPass4ReviewF3RetiredCompletionCorrectionDoesNotReplaceTheRetainedCheckpoint()
    {
        var f = await FullStatsFixtureAsync(target: 1);
        for (var tile = 0; tile < 4; tile++) await ApproveStatsAsync(f, await PendingStatsAsync(f, tile, 0, tile + 1));
        await SyncStatsAsync(f, 100);
        await using (var db = new ApplicationDbContext(options))
        {
            var ev = await db.Events.SingleAsync();
            Assert.True((await new EventLifecycleService(db, null!, f.Clock).EndNowAsync(ev.Id, ev.Version, true, "Controlled early end", new(f.Admin.Id, f.Admin.LoginName))).Succeeded);
        }
        var original = await ReadStatsAsync(f); Assert.NotNull(original.Luck.CalculatedAt);
        f.Clock.Advance(TimeSpan.FromHours(3));
        await using (var db = new ApplicationDbContext(options))
        {
            var refresh = new RecordingFinalReviewSynchronization();
            var service = new EventFinalizationService(db, new PublicBoardService(db, f.Clock), f.Clock, competitionSynchronization: refresh);
            var ready = (await service.GetReadinessAsync(f.Event.Id))!;
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CorrectCompletionAsync(
                f.Event.Id, f.Team.Id, f.Event.ActualStartedAt!.Value.AddMinutes(5), "Retired completion correction", f.Admin.Id,
                ready.EventVersion, ready.ReviewCycleId));
            Assert.True(ready.CanFinalize, string.Join("; ", ready.Blockers.Select(x => x.Description)));
            await service.FinalizeAsync(f.Event.Id, new(f.Admin.Id, f.Admin.LoginName), ready.EventVersion);
            Assert.Equal(1, refresh.Calls);
        }
        var finalized = await ReadStatsAsync(f);
        Assert.Equal(EventState.Archived, finalized.State); Assert.Equal(original.EndedAt, finalized.EndedAt);
        Assert.Equal(original.EvidenceRevision + 1, finalized.EvidenceRevision);
        Assert.Equal(original.Luck.EvidenceRevision, finalized.Luck.EvidenceRevision);
        Assert.Equal(JsonSerializer.Serialize(original.Luck with { Stale = true }), JsonSerializer.Serialize(finalized.Luck));
        Assert.Equal(original.Teams.Single().Progress.BoardCompletedAt, finalized.Teams.Single().OfficialCompletion!.CompletedAt);
        Assert.Equal(original.Teams.Single().Progress.BoardCompletedAt, finalized.Teams.Single().Progress.BoardCompletedAt);
        Assert.NotNull(finalized.OfficialResult);
    }

    [Fact]
    public async Task StatsPass4RealFinalReviewRefreshFailureDoesNotBlockPublicationAndIsRecorded()
    {
        var f = await FullStatsFixtureAsync(target: 1);
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await SyncStatsAsync(f, 100);
        // End at the configured provider window: an early unmatched end deliberately skips this fetch.
        f.Clock.Advance(f.Event.EventEndsAt!.Value - f.Clock.GetUtcNow());
        await using (var end = new ApplicationDbContext(options))
        {
            var ev = await end.Events.SingleAsync();
            Assert.True((await new EventLifecycleService(end, null!, f.Clock).EndNowAsync(
                ev.Id, ev.Version, true, "Controlled final-review end", new(f.Admin.Id, f.Admin.LoginName))).Succeeded);
        }
        f.Clock.Advance(TimeSpan.FromHours(3));

        await using (var db = new ApplicationDbContext(options))
        {
            var validation = StatsResponse(f, new WiseOldManMetricDelta(0, 100, 100));
            var failure = new WiseOldManCompetitionResult(
                WiseOldManCompetitionStatus.Unavailable, null, Message: "Controlled final-review refresh failure");
            var synchronization = new EventCompetitionSynchronizationService(
                db, new StatsClient(validation, failure, null), new FixedStatus(), f.Clock);
            var finalization = new EventFinalizationService(
                db, new PublicBoardService(db, f.Clock), f.Clock, competitionSynchronization: synchronization);
            var readiness = (await finalization.GetReadinessAsync(f.Event.Id))!;
            Assert.True(readiness.CanFinalize, string.Join("; ", readiness.Blockers.Select(x => x.Description)));
            var outcome = await finalization.FinalizeAsync(f.Event.Id, new(f.Admin.Id, f.Admin.LoginName), readiness.EventVersion);
            Assert.True(outcome.Published);
            Assert.Contains("Controlled final-review refresh failure", outcome.Feedback, StringComparison.Ordinal);
        }

        await using var verify = new ApplicationDbContext(options);
        var archived = await verify.Events.AsNoTracking().SingleAsync(x => x.Id == f.Event.Id);
        Assert.Equal(EventState.Archived, archived.State);
        var audit = Assert.Single(await verify.AuditEntries.AsNoTracking()
            .Where(x => x.EventId == f.Event.Id && x.Action == "event.results_published").ToListAsync());
        Assert.Contains("Final-review competition refresh failed", audit.Details, StringComparison.Ordinal);
        Assert.Contains("Controlled final-review refresh failure", audit.Details, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StatsPass4RealFinalReviewUsesConfiguredWindowAndNoDuplicateFetchAfterPublish(bool matchesConfiguredWindow)
    {
        var f = await FullStatsFixtureAsync(target: 1);
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await SyncStatsAsync(f, 100);
        EventStatsLuckCheckpoint prior;
        var actualEnd = f.Clock.GetUtcNow();
        await using (var end = new ApplicationDbContext(options))
        {
            var ev = await end.Events.SingleAsync();
            Assert.True(ev.EventEndsAt!.Value - actualEnd > TimeSpan.FromMinutes(5));
            // Controlled existing final-review row: actual instants differ from its configured window.
            ev.EndEvent(actualEnd);
            end.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), ev.Id, EventState.Live,
                EventState.AwaitingFinalReview, f.Admin.Id, actualEnd, "Controlled configured-window fixture", false, actualEnd));
            await end.SaveChangesAsync();
            prior = await end.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        }
        // End early, then move beyond the initial successful fetch's hourly slot.
        // AU20 validates the configured window; actual end still drives eligibility.
        f.Clock.Advance(TimeSpan.FromHours(1));
        var response = await NamedStatsResponseAsync(f, new(0, 200, 200));
        response = response with
        {
            Competition = response.Competition! with { EndsAt = matchesConfiguredWindow ? f.Event.EventEndsAt!.Value : actualEnd }
        };
        var client = new CountingFinalReviewClient(response, response);
        await using (var db = new ApplicationDbContext(options))
        {
            var synchronization = new EventCompetitionSynchronizationService(db, client, new FixedStatus(), f.Clock);
            var first = await synchronization.RefreshForFinalReviewAsync(f.Event.Id);
            Assert.False(first.Skipped);
            Assert.Equal(1, client.Calls);
            var checkpoint = await db.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
            var currentEvent = await db.Events.AsNoTracking().SingleAsync();
            Assert.Equal(EventState.AwaitingFinalReview, currentEvent.State);
            Assert.Equal(actualEnd, currentEvent.ActualEndedAt);
            Assert.NotEqual(currentEvent.EventEndsAt, currentEvent.ActualEndedAt);
            if (!matchesConfiguredWindow)
            {
                Assert.False(first.Succeeded);
                Assert.Equal("ScheduleMismatch", first.ErrorKind);
                Assert.Contains(currentEvent.EventEndsAt!.Value.ToString("O"), first.Message, StringComparison.Ordinal);
                Assert.Equal(prior.Payload, checkpoint.Payload);
                Assert.Equal(prior.ActivityBatchId, checkpoint.ActivityBatchId);
                Assert.Equal(prior.CalculatedAt, checkpoint.CalculatedAt);
                Assert.Equal(prior.FetchedAt, checkpoint.FetchedAt);
                Assert.Equal(prior.UpstreamUpdatedAt, checkpoint.UpstreamUpdatedAt);
                return;
            }
            Assert.True(first.Succeeded, first.Message);
            Assert.NotEqual(prior.ActivityBatchId, checkpoint.ActivityBatchId);
            Assert.Equal(f.Clock.GetUtcNow(), checkpoint.CalculatedAt);
            Assert.Equal(f.Clock.GetUtcNow(), checkpoint.FetchedAt);
            Assert.Equal(response.Competition.Participants.Min(participant => participant.UpstreamUpdatedAt), checkpoint.UpstreamUpdatedAt);
            Assert.NotEqual(prior.Payload, checkpoint.Payload);

            var hourlySkip = await synchronization.RefreshForFinalReviewAsync(f.Event.Id);
            Assert.False(hourlySkip.Succeeded);
            Assert.True(hourlySkip.Skipped);
            Assert.Equal(1, client.Calls);

            var finalization = new EventFinalizationService(
                db, new PublicBoardService(db, f.Clock), f.Clock, competitionSynchronization: synchronization);
            var readiness = (await finalization.GetReadinessAsync(f.Event.Id))!;
            Assert.True(readiness.CanFinalize, string.Join("; ", readiness.Blockers.Select(x => x.Description)));
            var published = await finalization.FinalizeAsync(f.Event.Id, new(f.Admin.Id, f.Admin.LoginName), readiness.EventVersion);
            Assert.True(published.Published);
            Assert.Contains("refresh skipped", published.Feedback, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, client.Calls);

            var archivedVersion = await db.Events.AsNoTracking().Where(x => x.Id == f.Event.Id).Select(x => x.Version).SingleAsync();
            var retry = await finalization.FinalizeAsync(f.Event.Id, new(f.Admin.Id, f.Admin.LoginName), archivedVersion - 1);
            Assert.True(retry.Published);
            Assert.True(retry.AlreadyPublished);
            Assert.Null(retry.Feedback);
            Assert.Equal(1, client.Calls);
        }
    }

    [Fact]
    public async Task StatsPass4RealFinalReviewHonorsLeaseAndRejectsPostFetchLifecycleAndEvidenceChange()
    {
        var f = await FullStatsFixtureAsync(target: 1);
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await SyncStatsAsync(f, 100);
        await using (var end = new ApplicationDbContext(options))
        {
            var ev = await end.Events.SingleAsync();
            f.Clock.Advance(ev.EventEndsAt!.Value - f.Clock.GetUtcNow());
            var ended = await new EventLifecycleService(end, null!, f.Clock).EndNowAsync(
                ev.Id, ev.Version, true, "Controlled lease-boundary final-review end", new(f.Admin.Id, f.Admin.LoginName));
            Assert.True(ended.Succeeded, ended.Error);
        }
        f.Clock.Advance(TimeSpan.FromHours(1));
        var response = await NamedStatsResponseAsync(f, new(0, 100, 100));
        EventStatsLuckCheckpoint prior;
        await using (var leaseDb = new ApplicationDbContext(options))
        {
            var state = await leaseDb.EventCompetitionSynchronizations.SingleAsync();
            state.AcquireLease("controlled-final-review-lease", f.Clock.GetUtcNow().AddMinutes(5));
            await leaseDb.SaveChangesAsync();
            prior = await leaseDb.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        }

        var leaseClient = new CountingFinalReviewClient(response, response);
        await using (var skippedDb = new ApplicationDbContext(options))
        {
            var skipped = await new EventCompetitionSynchronizationService(skippedDb, leaseClient, new FixedStatus(), f.Clock)
                .RefreshForFinalReviewAsync(f.Event.Id);
            Assert.False(skipped.Succeeded);
            Assert.True(skipped.Skipped);
            Assert.Equal(0, leaseClient.Calls);
        }
        await using (var clearLease = new ApplicationDbContext(options))
        {
            var state = await clearLease.EventCompetitionSynchronizations.SingleAsync();
            state.ReleaseLease("controlled-final-review-lease");
            await clearLease.SaveChangesAsync();
        }

        var racedClient = new CountingFinalReviewClient(response, response, async () =>
        {
            await using var raced = new ApplicationDbContext(options);
            await raced.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE events
                SET state = {"Archived"}, version = version + 1,
                    stats_evidence_revision = stats_evidence_revision + 1,
                    stats_luck_invalidated_at_revision = stats_luck_invalidated_at_revision + 1
                WHERE id = {f.Event.Id}
                """);
        });
        await using (var racedDb = new ApplicationDbContext(options))
        {
            var result = await new EventCompetitionSynchronizationService(racedDb, racedClient, new FixedStatus(), f.Clock)
                .RefreshForFinalReviewAsync(f.Event.Id);
            Assert.False(result.Succeeded);
            Assert.False(result.Skipped);
            Assert.Equal("ConcurrencyConflict", result.ErrorKind);
            Assert.Equal(1, racedClient.Calls);
        }
        await using var verify = new ApplicationDbContext(options);
        var retained = await verify.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        Assert.Equal(prior.Payload, retained.Payload);
        Assert.Equal(prior.FetchedAt, retained.FetchedAt);
        Assert.Equal(EventState.Archived, await verify.Events.AsNoTracking().Where(x => x.Id == f.Event.Id).Select(x => x.State).SingleAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatsPass4ReviewF3MigrationBlocksDowngradeWithoutRelabelingV2History(bool olderCheckpoint)
    {
        var f = await FullStatsFixtureAsync(); await SyncStatsAsync(f, 100);
        if (olderCheckpoint)
        {
            f.Clock.Advance(TimeSpan.FromHours(3));
            await SyncStatsAsync(f, new WiseOldManCompetitionResult(WiseOldManCompetitionStatus.Unavailable, null, Message: "Controlled outage before migration"));
        }
        await using var db = new ApplicationDbContext(options);
        var before = await db.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        var failure = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            db.GetService<IMigrator>().MigrateAsync("20260915183337_AddEventStatsLuckCheckpoint"));
        Assert.Contains("downgrade blocked", failure.Message, StringComparison.OrdinalIgnoreCase);
        var retained = await db.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        Assert.Equal(EventStatsLuckCheckpoint.CurrentSchemaVersion, retained.SchemaVersion);
        Assert.Equal(before.Payload, retained.Payload);
        Assert.Equal(before.AlgorithmVersion, retained.AlgorithmVersion);
        Assert.Equal(before.ConvertedFromSchemaVersion, retained.ConvertedFromSchemaVersion);
    }

    private async Task<WiseOldManCompetitionResult> NamedStatsResponseAsync(FullStatsFixture f, WiseOldManMetricDelta delta)
    {
        var response = StatsResponse(f, delta);
        await using var db = new ApplicationDbContext(options);
        var names = await db.OsrsCharacters.ToDictionaryAsync(x => x.Id, x => x.DisplayName);
        return response with
        {
            Competition = response.Competition! with
            {
                Participants = response.Competition.Participants
                    .Select(participant => participant with { Username = names[Guid.Parse(participant.Username)] })
                    .ToArray()
            }
        };
    }

    private sealed class CountingFinalReviewClient(
        WiseOldManCompetitionResult validation,
        WiseOldManCompetitionResult result,
        Func<Task>? before = null) : IWiseOldManCompetitionClient
    {
        public int Calls { get; private set; }
        public int ValidationCalls { get; private set; }
        public Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, CancellationToken cancellationToken = default)
        { ValidationCalls++; return Task.FromResult(validation); }
        public async Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, IReadOnlyCollection<string> metrics, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (before is not null) await before();
            return result;
        }
    }

    private sealed class RecordingFinalReviewSynchronization : IEventCompetitionSynchronizationService
    {
        public int Calls { get; private set; }
        public Task<EventCompetitionView?> GetAsync(Guid eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EventCompetitionConfigurationResult> ConfigureAsync(Guid eventId, long expectedEventVersion, long? competitionId, bool synchronizeSchedule, LifecycleActor actor, bool confirmScheduleChanges = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EventCompetitionConfigurationResult> ConfigureAsync(Guid eventId, long expectedEventVersion, long? competitionId, bool synchronizeSchedule, LifecycleActor actor, bool confirmScheduleChanges, bool confirmCompetitionClear, string? competitionClearReason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EventCompetitionConfigurationResult> ConfigureAsync(Guid eventId, long expectedEventVersion, long? competitionId, LifecycleActor actor, bool confirmCompetitionClear = false, string? competitionClearReason = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EventCompetitionRefreshResult> RefreshAsync(Guid eventId, LifecycleActor actor, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EventCompetitionRefreshResult> RefreshForFinalReviewAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new EventCompetitionRefreshResult(true, false));
        }
        public Task<bool> MakeDevelopmentRefreshDueAsync(Guid eventId, LifecycleActor actor, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ProcessDueAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
