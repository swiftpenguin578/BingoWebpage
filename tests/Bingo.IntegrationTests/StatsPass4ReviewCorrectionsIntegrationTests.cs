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
    [InlineData("checkpoint-failure")]
    public async Task StatsPass4ReviewF2ScheduledEndCapturesOnlyCompatibleFreshResultsAtomically(string scenario)
    {
        var f = await FullStatsFixtureAsync();
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        var end = f.Event.EventEndsAt!.Value;
        f.Clock.Advance(end - f.Clock.GetUtcNow() - (scenario == "expired" ? TimeSpan.FromHours(3) : TimeSpan.FromMinutes(1)));
        await SyncStatsAsync(f, 100);
        var live = await ReadStatsAsync(f);
        EventStatsLuckCheckpoint original;
        long invalidationBefore;
        await using (var db = new ApplicationDbContext(options))
        {
            original = await db.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
            invalidationBefore = (await db.Events.AsNoTracking().SingleAsync()).StatsLuckInvalidatedAtRevision;
        }
        if (scenario == "checkpoint-failure")
        {
            await using var db = new ApplicationDbContext(options);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION reject_scheduled_stats_fixture() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Controlled scheduled checkpoint failure' USING ERRCODE = '23514'; END $$;
                CREATE TRIGGER reject_scheduled_stats_fixture BEFORE UPDATE ON event_stats_luck_checkpoints
                FOR EACH ROW EXECUTE FUNCTION reject_scheduled_stats_fixture();
                """);
        }
        f.Clock.Advance(end - f.Clock.GetUtcNow());
        await using (var db = new ApplicationDbContext(options))
        {
            var signup = new EventSignupLifecycleService(db, new EventReadinessEvaluator(db, new ConfigurationBuilder().Build()), f.Clock);
            await new EventLifecycleService(db, signup, f.Clock).ProcessDueAsync();
        }
        var ended = await ReadStatsAsync(f);
        await using var verify = new ApplicationDbContext(options);
        var saved = await verify.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        if (scenario == "checkpoint-failure")
        {
            Assert.Equal(EventState.Live, ended.State); Assert.Null(ended.EndedAt); Assert.Equal(live.EvidenceRevision, ended.EvidenceRevision);
            Assert.Equal(original.Payload, saved.Payload); Assert.Equal(original.EvidenceRevision, saved.EvidenceRevision);
            Assert.False(await verify.EventStateTransitions.AnyAsync(x => x.ToState == EventState.AwaitingFinalReview));
            Assert.Equal(invalidationBefore, (await verify.Events.AsNoTracking().SingleAsync()).StatsLuckInvalidatedAtRevision);
            return;
        }
        Assert.Equal(EventState.AwaitingFinalReview, ended.State); Assert.Equal(end, ended.EndedAt);
        Assert.Equal(scenario == "fresh" ? ended.EvidenceRevision : original.EvidenceRevision, saved.EvidenceRevision);
        if (scenario == "fresh")
        {
            Assert.Equal(live.Luck.Result, ended.Luck.Result); Assert.Equal(end, ended.Luck.CalculatedAt);
            Assert.Equal(live.Luck.FetchedAt, ended.Luck.FetchedAt); Assert.Equal(live.Luck.UpstreamUpdatedAt, ended.Luck.UpstreamUpdatedAt);
        }
        else { Assert.Null(ended.Luck.CalculatedAt); Assert.Null(ended.Luck.Result.Percentage); }
        f.Clock.Advance(TimeSpan.FromHours(3));
        var aged = await ReadStatsAsync(f);
        if (scenario == "fresh") Assert.Equal(JsonSerializer.Serialize(ended.Luck with { Stale = true }), JsonSerializer.Serialize(aged.Luck));
        else { Assert.Null(aged.Luck.CalculatedAt); Assert.Null(aged.Luck.Result.Percentage); }
        Assert.Equal(original.ActivityBatchId, (await verify.EventCompetitionSynchronizations.AsNoTracking().SingleAsync()).MetricActivityBatchId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatsPass4ReviewF3AdditiveApprovalDuringOutageRetainsTheEntireOldLuckUntilAnyReversal(bool reverseNewDrop)
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
        Assert.Equal(1, additive.Luck.Result.Received); Assert.Equal(1, additive.Luck.Result.Expected); Assert.Equal(0, additive.Luck.Result.Percentage);
        await ReverseStatsAsync(f, reverseNewDrop ? added : first);
        var reversed = await ReadStatsAsync(f); Assert.Equal(1, reversed.Value.Drops);
        Assert.Null(reversed.Luck.Result.Percentage); Assert.Null(reversed.Luck.CalculatedAt); Assert.False(reversed.Luck.Stale);
        // A subsequent additive approval cannot resurrect a calculation invalidated by the reversal.
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 30));
        var later = await ReadStatsAsync(f); Assert.Equal(2, later.Value.Drops); Assert.Null(later.Luck.CalculatedAt); Assert.Null(later.Luck.Result.Percentage);
        await using var verify = new ApplicationDbContext(options);
        var ev = await verify.Events.AsNoTracking().SingleAsync();
        Assert.Equal(reversed.EvidenceRevision, ev.StatsLuckInvalidatedAtRevision);
        Assert.Equal(original.EvidenceRevision, (await verify.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync()).EvidenceRevision);
    }

    [Fact]
    public async Task StatsPass4ReviewF3FreshApprovalStillRequiresExactEvidenceRevisionForCheckpointWrites()
    {
        var f = await FullStatsFixtureAsync(); await SyncStatsAsync(f, 100);
        EventStatsLuckCheckpoint old;
        await using (var db = new ApplicationDbContext(options)) old = await db.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        var result = await ReadStatsAsync(f); Assert.False(result.Luck.Stale); Assert.Equal(1, result.Luck.Result.Received);
        await using var write = new ApplicationDbContext(options); await using var tx = await write.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var current = await write.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        Assert.Equal(old.ActivityBatchId, current.ActivityBatchId); Assert.Equal(old.AssignmentFingerprint, current.AssignmentFingerprint);
        Assert.Equal(old.SourceFingerprint, current.SourceFingerprint); Assert.Equal(old.LifecycleFingerprint, current.LifecycleFingerprint);
        Assert.False(await PublicStatsService.TryWriteCheckpointAsync(write, f.Clock, old)); await tx.CommitAsync();
        Assert.Equal(result.EvidenceRevision, (await write.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync()).EvidenceRevision);
    }

    [Fact]
    public async Task StatsPass4ReviewF3CompletionCorrectionInvalidatesAnOtherwiseCompatibleStaleCheckpoint()
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
            var service = new EventFinalizationService(db, new PublicBoardService(db, f.Clock), f.Clock);
            var ready = (await service.GetReadinessAsync(f.Event.Id))!;
            await service.CorrectCompletionAsync(f.Event.Id, f.Team.Id, f.Event.ActualStartedAt!.Value.AddMinutes(5), "Controlled correction", f.Admin.Id, ready.EventVersion, ready.ReviewCycleId);
        }
        var corrected = await ReadStatsAsync(f); Assert.Equal(original.State, corrected.State); Assert.Equal(original.EndedAt, corrected.EndedAt);
        Assert.Equal(original.EvidenceRevision + 1, corrected.EvidenceRevision); Assert.Null(corrected.Luck.CalculatedAt); Assert.Null(corrected.Luck.Result.Percentage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatsPass4ReviewF3MigrationConservativelyInitializesTheInvalidationBoundary(bool olderCheckpoint)
    {
        var f = await FullStatsFixtureAsync(); await SyncStatsAsync(f, 100);
        if (olderCheckpoint)
        {
            f.Clock.Advance(TimeSpan.FromHours(3));
            await SyncStatsAsync(f, new WiseOldManCompetitionResult(WiseOldManCompetitionStatus.Unavailable, null, Message: "Controlled outage before migration"));
        }
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await using var db = new ApplicationDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync("20260915183337_AddEventStatsLuckCheckpoint");
        await RetainedCatalogueMigrationTestSupport.PrepareAsync(db);
        await db.Database.MigrateAsync();
        var ev = await db.Events.AsNoTracking().SingleAsync(); Assert.Equal(ev.StatsEvidenceRevision, ev.StatsLuckInvalidatedAtRevision);
        var before = await ReadStatsAsync(f);
        Assert.Equal(1, before.Value.Drops);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE events SET stats_luck_invalidated_at_revision = stats_evidence_revision + 1"));
        if (olderCheckpoint)
        {
            Assert.Null(before.Luck.Result.Percentage); Assert.Null(before.Luck.CalculatedAt);
            Assert.True((await db.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync()).EvidenceRevision < ev.StatsLuckInvalidatedAtRevision);
            return;
        }
        Assert.Equal(1, before.Luck.Result.Received);
        f.Clock.Advance(TimeSpan.FromHours(3));
        await SyncStatsAsync(f, new WiseOldManCompetitionResult(WiseOldManCompetitionStatus.Unavailable, null, Message: "Controlled outage"));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 20));
        var after = await ReadStatsAsync(f); Assert.Equal(2, after.Value.Drops);
        Assert.Equal(JsonSerializer.Serialize(before.Luck with { Stale = true }), JsonSerializer.Serialize(after.Luck));
    }
}
