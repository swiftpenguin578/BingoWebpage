using System.Data;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.WiseOldMan;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Bingo.IntegrationTests;

public sealed partial class Slice10Pass102CompetitionSynchronizationTests
{
    [Fact]
    public async Task StatsPass3InitialUnexpectedUnrankedEndIsVisibleWithoutInventingActivity()
    {
        var f = await MetricFixtureAsync(); var client = new MetricClient(f) { Override = MetricResult(f, new(10, -1, 0)) };
        await using var db = new ApplicationDbContext(options); var service = MetricService(db, client, f);
        await ConfigureMetricAsync(service, f); await service.RefreshAsync(f.Event.Id, f.Actor);
        var cache = (await service.ReadMetricCacheAsync(f.Event.Id))!; Assert.False(cache.Complete);
        var rows = cache.Rows.Where(x => x.Metric == "vorkath").ToArray(); Assert.Equal(2, rows.Length);
        Assert.All(rows, x => { Assert.Equal(MetricActivityCoverage.UnexpectedUnrankedEnd, x.LastIssue); Assert.Null(x.RecordedActivity()); Assert.Null(x.FetchedAt); });
    }

    [Fact]
    public async Task StatsPass3AdditiveMigrationRetainsExistingEhbAndDefersBasisToProvenHistory()
    {
        // Create current-model fixture rows before downgrading; later additive event columns
        // must not be inserted into the intentionally older schema under test.
        var fixture = await MetricFixtureAsync();
        await using var migration = new ApplicationDbContext(options);
        await migration.GetService<IMigrator>().MigrateAsync("20260915170124_GuardSuspiciousPriceCandidates");
        await using (var old = new ApplicationDbContext(options))
        {
            old.EventCompetitionCharacterActivities.Add(new(Guid.NewGuid(), fixture.Event.Id, 1, 42, fixture.Characters[0].Id, 15,
                fixture.Clock.GetUtcNow(), fixture.Clock.GetUtcNow().AddMinutes(-5), "original", 10, 25));
            await old.SaveChangesAsync();
        }
        await migration.Database.MigrateAsync();
        await using var verify = new ApplicationDbContext(options);
        Assert.Empty(await verify.EventLuckOutcomeBases.ToListAsync()); Assert.Empty(await verify.EventCompetitionCharacterMetricActivities.ToListAsync());
        var existing = Assert.Single(await verify.EventCompetitionCharacterActivities.ToListAsync());
        Assert.Equal(15, existing.GainedEhb); Assert.Equal(10, existing.StartEhb); Assert.Equal(25, existing.EndEhb);
        Assert.Equal("original", existing.AssignmentFingerprint);
        await using var transaction = await verify.Database.BeginTransactionAsync();
        await verify.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {fixture.Event.Id} FOR UPDATE").SingleAsync();
        await verify.RetainLuckOutcomeBasesAsync(fixture.Event.Id, fixture.Clock.GetUtcNow()); await verify.SaveChangesAsync(); await transaction.CommitAsync();
        Assert.Equal(2, await verify.EventLuckOutcomeBases.CountAsync());
        Assert.All(await verify.EventLuckOutcomeBases.ToListAsync(), x => Assert.Equal(LuckBasisStatus.Retained, x.Status));
    }

    [Theory]
    [InlineData("conflicting-copy")]
    [InlineData("tied-approval")]
    [InlineData("missing-identity")]
    public async Task StatsPass3LegacyAmbiguityNeverGuessesTheEarliestBasis(string issue)
    {
        var f = await MetricFixtureAsync();
        await using var db = new ApplicationDbContext(options);
        if (issue == "tied-approval")
        {
            // A separate retained approval with the same earliest time cannot establish ordering.
            f.Clock.Advance(TimeSpan.FromHours(-2)); await AddMetricApprovalAsync(db, f, false);
        }
        else if (issue == "missing-identity")
            await db.BoardApprovalRequirementBossSnapshots.Where(x => x.BossActivityId == f.Bosses[0].Id).ExecuteDeleteAsync();
        else
        {
            var old = await db.BoardApprovalRequirementDropSnapshots.SingleAsync(x => x.SourceDropId == f.Drops[0].Id);
            var tile = await db.BoardApprovalTileSnapshots.SingleAsync();
            var duplicate = new BoardApprovalRequirementSnapshot(Guid.NewGuid(), tile.Id, Guid.NewGuid(), 2, 1, true, false, 1, "Conflict", false);
            db.AddRange(duplicate, new BoardApprovalRequirementDropSnapshot(Guid.NewGuid(), duplicate.Id, old.SourceDropId, old.ItemIdSnapshot, old.BossName, old.ItemName, "1/5", .2m, null, 1, 1, 1),
                new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), duplicate.Id, f.Bosses[0].Id, f.Bosses[0].Name, 10, 1));
        }
        await db.SaveChangesAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {f.Event.Id} FOR UPDATE").SingleAsync();
        await db.RetainLuckOutcomeBasesAsync(f.Event.Id, f.Clock.GetUtcNow()); await db.SaveChangesAsync(); await transaction.CommitAsync();
        var basis = await db.EventLuckOutcomeBases.AsNoTracking().SingleAsync(x => x.SourceDropId == f.Drops[0].Id);
        Assert.Equal(issue == "missing-identity" ? LuckBasisStatus.MissingIdentity : LuckBasisStatus.ConflictingEarliestApproval, basis.Status);
        Assert.Null(basis.Metric);
        if (issue != "missing-identity") { Assert.Null(basis.NumericProbability); Assert.Null(basis.FirstApprovalSnapshotId); }
        Assert.False((await db.LuckSourceRequestAsync(f.Event.Id)).SourcesAvailable);
    }

    [Fact]
    public async Task StatsPass3OneBatchIncludesBothRegularAccountsAcrossSwapAndExcludesInformationalAlt()
    {
        var f = await MetricFixtureAsync();
        await using var db = new ApplicationDbContext(options);
        var client = new MetricClient(f); var service = MetricService(db, client, f);
        await ConfigureMetricAsync(service, f);
        Assert.True((await service.RefreshAsync(f.Event.Id, f.Actor)).Succeeded);
        Assert.Equal(["vorkath", "zulrah"], client.Requested);
        var cache = await service.ReadMetricCacheAsync(f.Event.Id);
        Assert.NotNull(cache); Assert.True(cache.Compatible); Assert.True(cache.Complete);
        Assert.Equal(4, cache.Rows.Count); Assert.Equal(60, cache.Rows.Sum(x => x.RecordedActivity()));
        Assert.DoesNotContain(cache.Rows, x => x.OsrsCharacterId == f.Characters[2].Id);
        Assert.All(cache.Rows, x => { Assert.Equal(f.Clock.GetUtcNow(), x.FetchedAt); Assert.Equal(f.Clock.GetUtcNow().AddMinutes(-5), x.UpstreamUpdatedAt); Assert.Equal(cache.ActivityBatchId, x.ActivityBatchId); });
        Assert.Equal(2, await db.EventCompetitionCharacterActivities.CountAsync(x => x.EventId == f.Event.Id));
        Assert.Equal(1, client.MetricCalls); Assert.Equal(1, client.ValidationCalls);
        Assert.Single(await db.EventParticipantCharacterSwaps.Where(x => x.EventId == f.Event.Id).ToListAsync());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unranked-end")]
    [InlineData("oversized")]
    [InlineData("failure")]
    public async Task StatsPass3PartialAndFailedBatchesRetainRawOriginWithoutClaimingCompleteness(string scenario)
    {
        var f = await MetricFixtureAsync(); var client = new MetricClient(f);
        await using var db = new ApplicationDbContext(options); var service = MetricService(db, client, f);
        await ConfigureMetricAsync(service, f); await service.RefreshAsync(f.Event.Id, f.Actor);
        var first = (await service.ReadMetricCacheAsync(f.Event.Id))!; var before = first.Rows.Single(x => x.OsrsCharacterId == f.Characters[0].Id && x.Metric == "vorkath");
        f.Clock.Advance(TimeSpan.FromHours(2));
        client.Override = scenario == "failure" ? new(WiseOldManCompetitionStatus.Unavailable) : MetricResult(f, scenario == "missing" ? null : scenario == "oversized" ? new(10, 100000000000000000000m, 15) : new(20, -1, 0));
        await service.RefreshAsync(f.Event.Id, f.Actor);
        var after = (await service.ReadMetricCacheAsync(f.Event.Id))!;
        Assert.True(after.Compatible); Assert.False(after.Complete);
        var retained = after.Rows.Single(x => x.OsrsCharacterId == before.OsrsCharacterId && x.Metric == before.Metric);
        Assert.Equal(before.Start, retained.Start); Assert.Equal(before.End, retained.End); Assert.Equal(before.Gained, retained.Gained);
        Assert.Equal(before.ActivityBatchId, retained.ActivityBatchId); Assert.Equal(before.FetchedAt, retained.FetchedAt); Assert.Equal(before.UpstreamUpdatedAt, retained.UpstreamUpdatedAt);
        Assert.NotNull(retained.LastIssue); Assert.Equal(f.Clock.GetUtcNow(), retained.LastAttemptAt);
        var state = await db.EventCompetitionSynchronizations.AsNoTracking().SingleAsync(x => x.EventId == f.Event.Id);
        if (scenario != "failure") { Assert.True(state.LatestComplete); Assert.Equal(f.Clock.GetUtcNow(), state.LastSuccessfulAt); }
        else Assert.Equal(before.FetchedAt, state.LastSuccessfulAt);
    }

    [Theory]
    [InlineData(-1, -1, 0, MetricActivityCoverage.ZeroRecorded, MetricActivityAvailability.WaitingForActivityData)]
    [InlineData(-1, 35, 34, MetricActivityCoverage.EstimatedBaseline, MetricActivityAvailability.Estimated)]
    [InlineData(20, 35, 12, MetricActivityCoverage.Ranked, MetricActivityAvailability.Available)]
    [InlineData(20, 20, 0, MetricActivityCoverage.Ranked, MetricActivityAvailability.WaitingForActivityUpdate)]
    public async Task StatsPass3PostgresPreservesEachAgreedCountCase(int start, int end, int gain, MetricActivityCoverage coverage, MetricActivityAvailability withDrop)
    {
        var f = await MetricFixtureAsync(); var client = new MetricClient(f) { Override = MetricResult(f, new(start, end, gain)) };
        await using var db = new ApplicationDbContext(options); var service = MetricService(db, client, f);
        await ConfigureMetricAsync(service, f); await service.RefreshAsync(f.Event.Id, f.Actor);
        var cache = (await service.ReadMetricCacheAsync(f.Event.Id))!;
        Assert.True(cache.Complete);
        var row = cache.Rows.First(x => x.Metric == "vorkath");
        Assert.Equal(coverage, row.Coverage); Assert.Equal(withDrop, row.Availability(true));
        Assert.Equal((decimal)start, row.Start); Assert.Equal((decimal)end, row.End); Assert.Equal((decimal)gain, row.Gained);
        if (start == -1 && end == -1) Assert.Equal(MetricActivityAvailability.NoRecordedActivity, row.Availability(false));
    }

    [Theory]
    [InlineData("lease")]
    [InlineData("expired")]
    [InlineData("generation")]
    [InlineData("competition")]
    [InlineData("assignment")]
    public async Task StatsPass3RejectsAnInFlightResponseAfterItsFenceChanges(string mutation)
    {
        var f = await MetricFixtureAsync(); var client = new MetricClient(f);
        client.BeforeReturn = async () =>
        {
            await using var change = new ApplicationDbContext(options);
            var state = await change.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == f.Event.Id);
            if (mutation == "lease") state.AcquireLease("other-worker", f.Clock.GetUtcNow().AddMinutes(2));
            else if (mutation == "expired") f.Clock.Advance(TimeSpan.FromMinutes(3));
            else if (mutation == "generation") state.BeginReplacementGeneration(state.AssignmentFingerprint, f.Clock.GetUtcNow());
            else if (mutation == "competition") state.Reconfigure(43, "Replacement", f.Event.EventStartsAt, f.Event.EventEndsAt, state.AssignmentFingerprint, f.Clock.GetUtcNow());
            else
            {
                var replacement = new OsrsCharacter(Guid.NewGuid(), "Replacement", "REPLACEMENT", f.Clock.GetUtcNow()); change.Add(replacement);
                (await change.EventParticipantCharacters.SingleAsync(x => x.OsrsCharacterId == f.Characters[0].Id)).ReplaceCharacter(replacement.Id);
            }
            await change.SaveChangesAsync();
        };
        await using var db = new ApplicationDbContext(options); var service = MetricService(db, client, f);
        await ConfigureMetricAsync(service, f); await service.RefreshAsync(f.Event.Id, f.Actor);
        Assert.Empty(await db.EventCompetitionCharacterMetricActivities.AsNoTracking().ToListAsync());
        Assert.Empty(await db.EventCompetitionCharacterActivities.AsNoTracking().ToListAsync());
        Assert.False((await service.ReadMetricCacheAsync(f.Event.Id))!.Complete);
    }

    [Theory]
    [InlineData("approval")]
    [InlineData("objective")]
    [InlineData("mapping")]
    [InlineData("mapping-binding")]
    public async Task StatsPass3SourceChangeDuringFetchCannotSatisfyNewBoardButKeepsEhb(string mutation)
    {
        var f = await MetricFixtureAsync(mapped: mutation is not ("mapping" or "mapping-binding")); var client = new MetricClient(f);
        client.BeforeReturn = async () =>
        {
            await using var change = new ApplicationDbContext(options);
            await using var transaction = await change.Database.BeginTransactionAsync();
            await change.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {f.Event.Id} FOR UPDATE").SingleAsync();
            if (mutation is "mapping" or "mapping-binding")
            {
                var boss = await change.BossActivities.SingleAsync(x => x.Id == f.Bosses[0].Id);
                boss.ConfigureApi("vorkath"); boss.RecordMapping(ApiMappingStatus.Verified, f.Clock.GetUtcNow()); boss.AdvanceVersion();
            }
            else await AddMetricApprovalAsync(change, f, mutation == "objective");
            await change.SaveChangesAsync();
            if (mutation == "mapping-binding")
            {
                await change.RetainLuckOutcomeBasesAsync(f.Event.Id, f.Clock.GetUtcNow());
                await change.SaveChangesAsync();
            }
            await transaction.CommitAsync();
        };
        await using var db = new ApplicationDbContext(options); var service = MetricService(db, client, f);
        await ConfigureMetricAsync(service, f); await service.RefreshAsync(f.Event.Id, f.Actor);
        var cache = (await service.ReadMetricCacheAsync(f.Event.Id))!;
        Assert.False(cache.Complete); Assert.Empty(cache.Rows);
        Assert.Equal(2, await db.EventCompetitionCharacterActivities.CountAsync());
        Assert.True((await db.EventCompetitionSynchronizations.AsNoTracking().SingleAsync()).LatestComplete);
        if (mutation == "mapping") Assert.Equal(2, (await db.EventLuckOutcomeBases.AsNoTracking().SingleAsync(x => x.SourceDropId == f.Drops[0].Id)).SourceRevision);
        client.BeforeReturn = null; f.Clock.Advance(TimeSpan.FromHours(2));
        await service.RefreshAsync(f.Event.Id, f.Actor);
        Assert.True((await service.ReadMetricCacheAsync(f.Event.Id))!.Complete);
    }

    [Theory]
    [InlineData("board")]
    [InlineData("assignment")]
    public async Task StatsPass3ReadRechecksCurrentSourceAndAssignmentFingerprint(string change)
    {
        var f = await MetricFixtureAsync(); var client = new MetricClient(f);
        await using var db = new ApplicationDbContext(options); var service = MetricService(db, client, f);
        await ConfigureMetricAsync(service, f); await service.RefreshAsync(f.Event.Id, f.Actor);
        await using (var mutate = new ApplicationDbContext(options))
        {
            if (change == "board") await AddMetricApprovalAsync(mutate, f, false);
            else (await mutate.EventParticipantCharacters.SingleAsync(x => x.OsrsCharacterId == f.Characters[0].Id)).Release(f.Admin.Id, f.Clock.GetUtcNow());
            await mutate.SaveChangesAsync();
        }
        var cache = (await service.ReadMetricCacheAsync(f.Event.Id))!;
        Assert.False(cache.Compatible); Assert.False(cache.Complete); Assert.Empty(cache.Rows);
        Assert.Equal(1, client.MetricCalls);
    }

    [Fact]
    public void StatsPass3MetricContractKeepsMissingAndUnsupportedMappingsUnavailable()
    {
        Assert.Equal("Unmapped", CompetitionMetricContract.UnavailableReason(null));
        Assert.Equal("UnverifiedMetric", CompetitionMetricContract.UnavailableReason("unsupported_fixture_metric"));
    }

    [Theory]
    [InlineData("chambers_of_xeric", "chambers_of_xeric_challenge_mode")]
    [InlineData("theatre_of_blood", "theatre_of_blood_hard_mode")]
    [InlineData("tombs_of_amascut", "tombs_of_amascut_expert")]
    [InlineData("the_gauntlet", "the_corrupted_gauntlet")]
    [InlineData("nightmare", "phosanis_nightmare")]
    public async Task StatsPass3VerifiedModePairsRetainIndependentUsableActivity(string normalMetric, string alternateMetric)
    {
        var f = await MetricFixtureAsync(firstMetric: normalMetric, secondMetric: alternateMetric);
        var client = new MetricClient(f) { Override = MetricResult(f, new(100, 113, 13), new(40, 47, 7)) };
        await using var db = new ApplicationDbContext(options); var service = MetricService(db, client, f);
        await ConfigureMetricAsync(service, f);
        Assert.True((await service.RefreshAsync(f.Event.Id, f.Actor)).Succeeded);
        var cache = (await service.ReadMetricCacheAsync(f.Event.Id))!;
        Assert.True(cache.Compatible); Assert.True(cache.Complete); Assert.True(cache.SuccessfulBatch); Assert.False(cache.Stale);
        Assert.Equal(new[] { normalMetric, alternateMetric }.Order(StringComparer.Ordinal), client.Requested);
        Assert.True(cache.Sources.SourcesAvailable); Assert.Equal(2, cache.Sources.Outcomes.Count);
        Assert.All(cache.Sources.Outcomes, x => Assert.Null(x.UnavailableReason));
        Assert.Equal(normalMetric, cache.Sources.Outcomes.Single(x => x.SourceDropId == f.Drops[0].Id).Basis!.Metric);
        Assert.Equal(alternateMetric, cache.Sources.Outcomes.Single(x => x.SourceDropId == f.Drops[1].Id).Basis!.Metric);
        Assert.Equal(4, cache.Rows.Count);
        foreach (var character in f.Characters.Take(2))
        {
            var normal = Assert.Single(cache.Rows, x => x.OsrsCharacterId == character.Id && x.Metric == normalMetric);
            Assert.Equal(100m, normal.Start); Assert.Equal(113m, normal.End); Assert.Equal(13m, normal.Gained); Assert.Equal(13m, normal.RecordedActivity());
            var alternate = Assert.Single(cache.Rows, x => x.OsrsCharacterId == character.Id && x.Metric == alternateMetric);
            Assert.Equal(40m, alternate.Start); Assert.Equal(47m, alternate.End); Assert.Equal(7m, alternate.Gained); Assert.Equal(7m, alternate.RecordedActivity());
        }
        Assert.All(cache.Rows, x =>
        {
            Assert.Equal(MetricActivityCoverage.Ranked, x.Coverage);
            Assert.Equal(MetricActivityAvailability.Available, x.Availability(true));
            Assert.Equal(cache.ActivityBatchId, x.ActivityBatchId); Assert.Null(x.LastIssue);
        });
    }

    private async Task<MetricFixture> MetricFixtureAsync(bool mapped = true, string firstMetric = "vorkath", string secondMetric = "zulrah")
    {
        var clock = new TestClock(new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero)); var now = clock.GetUtcNow();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "MetricAdmin", "METRICADMIN", now); admin.SetGlobalRole(GlobalRole.Admin);
        var ev = new BingoEvent(Guid.NewGuid(), "Synthetic metric event", "metric-event", "", "UTC", now.AddHours(-3), now.AddHours(-2), now.AddHours(-1), now.AddHours(6), now.AddHours(6), 10, admin.Id, now);
        ev.OpenSignups(now.AddHours(-3)); ev.CloseSignups(now.AddHours(-2)); ev.StartEvent(now.AddHours(-1));
        var participant = new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, 1, now.AddHours(-3), SignupSource.Website);
        var names = new[] { "Fixture One", "Fixture Two", "Fixture Alt" };
        var characters = names.Select(name => new OsrsCharacter(Guid.NewGuid(), name, name.ToUpperInvariant(), now)).ToArray();
        var assignments = characters.Select((character, index) => new EventParticipantCharacter(Guid.NewGuid(), ev.Id, participant.Id, character.Id, index, now, null, null,
            index == 2 ? EventCharacterRole.Informational : EventCharacterRole.Playing, index == 2 ? null : 0, index == 2 ? null : EhbSource.Manual, null)).ToArray();
        var bosses = new[] { firstMetric, secondMetric }.Select((metric, i) =>
        {
            var boss = new BossActivity(Guid.NewGuid(), metric, "fixture-" + metric.Replace('_', '-'), "Boss", 10, now);
            if (mapped || i != 0) { boss.ConfigureApi(metric); boss.RecordMapping(ApiMappingStatus.Verified, now); }
            return boss;
        }).ToArray();
        var items = new[] { new CatalogueItem(Guid.NewGuid(), "Fixture drop one", "FIXTURE DROP ONE"), new CatalogueItem(Guid.NewGuid(), "Fixture drop two", "FIXTURE DROP TWO") };
        var drops = items.Select((item, index) => new SourceDrop(Guid.NewGuid(), bosses[index].Id, item.Id, "1/10", .1m, 1, now)).ToArray();
        var board = new Board(Guid.NewGuid(), ev.Id, "Metric board", 1, 1);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Metric tile", "", "", 1);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 2, true, false, "Synthetic drop objective", false);
        var workingDrops = drops.Select((drop, index) => new BoardRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, drop.Id, items[index].Id, bosses[index].Name, items[index].Name, "1/10", .1m, null, 1)).ToArray();
        await using var db = new ApplicationDbContext(options);
        db.AddRange(admin, ev, participant, board, tile, requirement); db.AddRange(characters); db.AddRange(assignments); db.AddRange(bosses); db.AddRange(items); db.AddRange(drops); db.AddRange(workingDrops);
        db.Add(new EventParticipantCharacterSwap(Guid.NewGuid(), ev.Id, participant.Id, characters[0].Id, characters[1].Id, now.AddMinutes(-10), now, admin.Id, "Synthetic active account swap"));
        await BoardApprovalFixture.PublishAsync(db, board, now.AddHours(-2), [tile], [requirement], workingDrops);
        var approvedRequirement = await db.BoardApprovalRequirementSnapshots.SingleAsync();
        db.AddRange(bosses.Select(boss => new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), approvedRequirement.Id, boss.Id, boss.Name, 10, boss.Version)));
        await db.SaveChangesAsync();
        return new(ev, admin, characters, bosses, drops, board.Id, clock);
    }

    private static async Task AddMetricApprovalAsync(ApplicationDbContext db, MetricFixture f, bool newObjective)
    {
        var board = await db.Boards.SingleAsync(x => x.Id == f.BoardId);
        var previous = await db.BoardApprovalSnapshots.SingleAsync(x => x.Id == board.ActiveApprovalSnapshotId);
        var priorTile = await db.BoardApprovalTileSnapshots.SingleAsync(x => x.ApprovalSnapshotId == previous.Id);
        var oldRequirements = await db.BoardApprovalRequirementSnapshots.Where(x => x.ApprovalTileSnapshotId == priorTile.Id).ToListAsync();
        var approval = new BoardApprovalSnapshot(Guid.NewGuid(), board.Id, previous.Version + 1, f.Clock.GetUtcNow(), f.Admin.Id, previous.Id, board.Name, 1, 1, 1, 1, board.Version, BoardState.Published);
        var tile = new BoardApprovalTileSnapshot(Guid.NewGuid(), approval.Id, priorTile.BoardTileId, priorTile.TileTemplateId, 0, 0, "Correction", "", "", 1, null);
        db.AddRange(approval, tile);
        foreach (var old in oldRequirements)
        {
            var requirement = new BoardApprovalRequirementSnapshot(Guid.NewGuid(), tile.Id, old.BoardRequirementSnapshotId, old.Position, 2, true, false, 1, "Correction", false); db.Add(requirement);
            var drops = await db.BoardApprovalRequirementDropSnapshots.Where(x => x.ApprovalRequirementSnapshotId == old.Id).ToListAsync();
            foreach (var drop in drops) db.Add(new BoardApprovalRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, drop.SourceDropId, drop.ItemIdSnapshot, drop.BossName, drop.ItemName, "1/5", .2m, null, 1, 1, 2));
            foreach (var boss in f.Bosses) db.Add(new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), requirement.Id, boss.Id, boss.Name, 10, boss.Version));
        }
        if (newObjective)
        {
            var requirement = new BoardApprovalRequirementSnapshot(Guid.NewGuid(), tile.Id, Guid.NewGuid(), 2, 1, true, false, 1, "New outcome", false);
            var item = new CatalogueItem(Guid.NewGuid(), "New fixture outcome", "NEW FIXTURE OUTCOME");
            var source = new SourceDrop(Guid.NewGuid(), f.Bosses[0].Id, item.Id, "1/20", .05m, 1, f.Clock.GetUtcNow());
            db.AddRange(requirement, item, source, new BoardApprovalRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, source.Id, item.Id, f.Bosses[0].Name, item.Name, "1/20", .05m, null, 1, 1, 1),
                new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), requirement.Id, f.Bosses[0].Id, f.Bosses[0].Name, 10, 1));
        }
        board.ReplacePublishedApproval(approval.Id);
    }

    private static EventCompetitionSynchronizationService MetricService(ApplicationDbContext db, MetricClient client, MetricFixture fixture) => new(db, client, new FixedStatus(), fixture.Clock);
    private static async Task ConfigureMetricAsync(EventCompetitionSynchronizationService service, MetricFixture f) => Assert.True((await service.ConfigureAsync(f.Event.Id, f.Event.Version, 42, false, f.Actor)).Succeeded);
    private static WiseOldManCompetitionResult MetricResult(MetricFixture f, WiseOldManMetricDelta? first, WiseOldManMetricDelta? second = null) => new(WiseOldManCompetitionStatus.Success,
        new(42, "Synthetic competition", f.Event.EventStartsAt!.Value, f.Event.EventEndsAt!.Value, f.Clock.GetUtcNow(), f.Characters.Select(character => new WiseOldManCompetitionParticipant(character.DisplayName, "regular", 5, 10, 15,
            first is null ? new Dictionary<string, WiseOldManMetricDelta> { [f.Bosses[1].ExternalIdentifier!] = second ?? new(10, 25, 15) }
                : new Dictionary<string, WiseOldManMetricDelta> { [f.Bosses[0].ExternalIdentifier ?? "vorkath"] = first, [f.Bosses[1].ExternalIdentifier!] = second ?? new(10, 25, 15) }, f.Clock.GetUtcNow().AddMinutes(-5))).ToArray()));
    private sealed record MetricFixture(BingoEvent Event, Account Admin, OsrsCharacter[] Characters, BossActivity[] Bosses, SourceDrop[] Drops, Guid BoardId, TestClock Clock)
    { public LifecycleActor Actor => new(Admin.Id, Admin.LoginName); }
    private sealed class MetricClient(MetricFixture fixture) : IWiseOldManCompetitionClient
    {
        public Func<Task>? BeforeReturn { get; set; }
        public WiseOldManCompetitionResult? Override { get; set; }
        public IReadOnlyCollection<string> Requested { get; private set; } = [];
        public int MetricCalls { get; private set; }
        public int ValidationCalls { get; private set; }
        public Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, CancellationToken cancellationToken = default)
        { ValidationCalls++; return Task.FromResult(MetricResult(fixture, new(10, 25, 15))); }
        public async Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, IReadOnlyCollection<string> metrics, CancellationToken cancellationToken = default)
        {
            MetricCalls++; Requested = metrics; var response = Override ?? MetricResult(fixture, new(10, 25, 15));
            if (BeforeReturn is not null) await BeforeReturn();
            return response;
        }
    }
}
