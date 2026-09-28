using System.Data;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Stats;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Stats;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Slice10Pass102CompetitionSynchronizationTests
{
    [Theory]
    [InlineData(100, 1, 1, false, 0)]
    [InlineData(200, 4, 1, true, 42.4201097028391)]
    [InlineData(100, 2, 2, false, 0)]
    public async Task StatsPass4CompleteQueryCalculatesActualDropsAndFrozenPersonalRolls(int kills, int received, int rolls, bool secondOutcome, double expectedPercent)
    {
        var f = await FullStatsFixtureAsync(rolls, secondOutcome);
        for (var i = 0; i < received; i++) await ApproveStatsAsync(f, await PendingStatsAsync(f, i % 2, 0, i + 1));
        await SyncStatsAsync(f, kills);
        var result = await ReadStatsAsync(f);
        Assert.Equal(received, result.Value.Drops); Assert.Equal(received * 100m, result.Value.ValueGp);
        Assert.Equal(received, result.Luck.Result.Received);
        Assert.InRange(result.Luck.Result.Percentage!.Value, (decimal)expectedPercent - .00000001m, (decimal)expectedPercent + .00000001m);
        Assert.Equal(secondOutcome ? 2 : 1, result.Luck.Sources.Count);
        Assert.Equal(2, result.Rows); Assert.Equal(2, result.Columns);
        Assert.All(result.Drops, x => Assert.Equal(f.Items[0].Id, x.Item.ItemId));
        Assert.All(result.Drops, x => Assert.Equal("https://oldschool.runescape.wiki/images/Dragon_warhammer.png", x.Item.ImageUrl));
        await using var verify = new ApplicationDbContext(options);
        var cp = Assert.Single(await verify.EventStatsLuckCheckpoints.ToListAsync());
        Assert.Equal(result.EvidenceRevision, cp.EvidenceRevision); Assert.Equal(result.Luck.ActivityBatchId, cp.ActivityBatchId);
        Assert.DoesNotContain("MissingAccounts", cp.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("account_id", cp.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatsPass4LaterThenEarlierApprovalRecomputesHistoryAndWeightedDropsRemainOneItem()
    {
        var f = await FullStatsFixtureAsync(target: 6, weight: 3);
        var later = await PendingStatsAsync(f, 0, 0, 20, 3); var earlier = await PendingStatsAsync(f, 0, 0, 10, 3);
        await SyncStatsAsync(f, 200);
        await ApproveStatsAsync(f, later);
        var first = await ReadStatsAsync(f); Assert.Equal(100, first.Value.ValueGp); Assert.Equal(3, first.Teams[0].ProgressHistory[^1].Approved);
        Assert.Equal(f.Clock.GetUtcNow().AddMinutes(-40), first.Milestones.Single(x => x.Id == "submission").At);
        f.Clock.Advance(TimeSpan.FromMinutes(1)); await ApproveStatsAsync(f, earlier);
        var second = await ReadStatsAsync(f);
        Assert.Equal(2, second.Value.Drops); Assert.Equal(200, second.Value.ValueGp); Assert.Equal(2, second.Luck.Result.Received);
        Assert.Equal([earlier, later], second.Drops.Select(x => x.SubmissionId));
        Assert.Equal([100m, 200m], second.Teams[0].ValueHistory.Select(x => x.Value.ValueGp!.Value));
        Assert.Equal(f.Clock.GetUtcNow().AddMinutes(-51), second.Milestones.Single(x => x.Id == "submission").At);
        Assert.Equal(f.Clock.GetUtcNow().AddMinutes(-41), second.Milestones.Single(x => x.Id == "tile").At);
        Assert.Equal(first.EvidenceRevision + 1, second.EvidenceRevision);
        Assert.Equal(second.EvidenceRevision, second.Luck.EvidenceRevision);
        await ReverseStatsAsync(f, earlier);
        var reversed = await ReadStatsAsync(f); Assert.Single(reversed.Drops); Assert.Equal(0, reversed.Teams[0].Progress.CompletedTiles);
        Assert.Equal(StatsMilestoneState.Pending, reversed.Milestones.Single(x => x.Id == "tile").State);
    }

    [Theory]
    [InlineData("zero", false, StatsLuckStatus.NoEligibleActivity)]
    [InlineData("zero", true, StatsLuckStatus.WaitingForActivityUpdate)]
    [InlineData("unranked", false, StatsLuckStatus.NoEligibleActivity)]
    [InlineData("unranked", true, StatsLuckStatus.WaitingForActivityData)]
    [InlineData("missing", true, StatsLuckStatus.WaitingForActivityData)]
    [InlineData("estimated", true, StatsLuckStatus.Calculated)]
    [InlineData("mode", true, StatsLuckStatus.Calculated)]
    public async Task StatsPass4CompleteQueryPreservesUnavailableZeroAndEstimatedStates(string scenario, bool drop, StatsLuckStatus status)
    {
        var f = await FullStatsFixtureAsync(metric: scenario == "mode" ? "nightmare" : "vorkath");
        if (drop) await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        WiseOldManMetricDelta? delta = scenario switch { "unranked" => new(-1, -1, 0), "missing" => null, "estimated" => new(-1, 100, 100), "mode" => new(0, 100, 100), _ => new(0, 0, 0) };
        await SyncStatsAsync(f, delta);
        var result = await ReadStatsAsync(f);
        Assert.Equal(status, result.Luck.Result.Status);
        if (status != StatsLuckStatus.Calculated) Assert.Null(result.Luck.Result.Percentage);
        else { Assert.Equal(0, result.Luck.Result.Percentage); Assert.Equal(scenario == "estimated", result.Luck.Result.Estimated); }
        if (scenario == "mode")
        {
            Assert.All(result.Luck.Sources, x => Assert.Null(x.UnavailableReason));
            Assert.Equal(1m, result.Luck.Result.Expected);
            Assert.Equal(100m, Assert.Single(Assert.Single(Assert.Single(result.Luck.Teams).Players).Sources).Activity);
        }
        if (scenario == "unranked") Assert.True(result.Luck.Result.ZeroRecordedApproximation);
    }

    [Fact]
    public async Task StatsPass4NormalSyncReplacesFormerModeGateCacheAndCheckpoint()
    {
        var f = await FullStatsFixtureAsync(metric: "nightmare");
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await SyncStatsAsync(f, 100);
        var original = await ReadStatsAsync(f);
        // Represent a prior source contract with its own fingerprint and gated saved payload.
        var formerFingerprint = new string('a', 64);
        var unavailable = new StatsLuckResult(1, null, null, StatsLuckStatus.WaitingForActivityData, false, false);
        var formerLuck = original.Luck with
        {
            Result = unavailable,
            Sources = original.Luck.Sources.Select(x => x with { UnavailableReason = "ModeSemanticsUnverified" }).ToArray(),
            Teams = original.Luck.Teams.Select(team => team with
            {
                Result = unavailable,
                Players = team.Players.Select(player => player with
                {
                    Result = unavailable,
                    Sources = player.Sources.Select(x => x with { Activity = null, Expected = null, Status = StatsLuckStatus.WaitingForActivityData }).ToArray()
                }).ToArray()
            }).ToArray()
        };
        await using (var previous = new ApplicationDbContext(options))
        {
            var state = await previous.EventCompetitionSynchronizations.SingleAsync();
            state.SetSourceRequest(formerFingerprint);
            await previous.SaveChangesAsync();
            await previous.EventCompetitionCharacterMetricActivities.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.SourceRequestFingerprint, formerFingerprint));
            var payload = System.Text.Json.JsonSerializer.Serialize(formerLuck);
            await previous.EventStatsLuckCheckpoints.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.SourceFingerprint, formerFingerprint).SetProperty(x => x.Payload, payload));
        }
        var awaitingSync = await ReadStatsAsync(f);
        Assert.Equal(StatsLuckStatus.WaitingForActivityData, awaitingSync.Luck.Result.Status);
        Assert.Null(awaitingSync.Luck.CalculatedAt);
        Assert.All(awaitingSync.Luck.Sources, x => Assert.Null(x.UnavailableReason));

        f.Clock.Advance(TimeSpan.FromHours(3));
        await using (var sync = new ApplicationDbContext(options))
        {
            var response = StatsResponse(f, new(0, 200, 200));
            var names = await sync.OsrsCharacters.ToDictionaryAsync(x => x.Id, x => x.DisplayName);
            response = response with { Competition = response.Competition! with { Participants = response.Competition.Participants.Select(p => p with { Username = names[Guid.Parse(p.Username)] }).ToArray() } };
            var service = new EventCompetitionSynchronizationService(sync, new StatsClient(response, response, null), new FixedStatus(), f.Clock);
            await service.ProcessDueAsync();
            var cache = (await service.ReadMetricCacheAsync(f.Event.Id))!;
            Assert.True(cache.Compatible); Assert.True(cache.Complete);
            Assert.NotEqual(formerFingerprint, cache.Sources.Fingerprint);
            Assert.All(cache.Rows, x => Assert.Equal(cache.Sources.Fingerprint, x.SourceRequestFingerprint));
            var checkpoint = await sync.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
            Assert.Equal(cache.Sources.Fingerprint, checkpoint.SourceFingerprint);
            Assert.Equal(cache.ActivityBatchId, checkpoint.ActivityBatchId);
            Assert.NotEqual(original.Luck.ActivityBatchId, checkpoint.ActivityBatchId);
            Assert.DoesNotContain("ModeSemanticsUnverified", checkpoint.Payload, StringComparison.Ordinal);
        }
        var recovered = await ReadStatsAsync(f);
        Assert.Equal(StatsLuckStatus.Calculated, recovered.Luck.Result.Status);
        Assert.Equal(2m, recovered.Luck.Result.Expected); AssertLuckScore(-50.1883643902809m, recovered.Luck.Result);
        Assert.Equal(f.Clock.GetUtcNow(), recovered.Luck.CalculatedAt);
        Assert.False(recovered.Luck.Stale);
    }

    [Fact]
    public async Task StatsPass4PoolsRegularAccountsAndPlayersWithoutInformationalAltsOrAveragePercentages()
    {
        var f = await FullStatsFixtureAsync(players: 2, extraRegular: true);
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 1, 1, 11));
        await SyncStatsAsync(f, 100);
        var result = await ReadStatsAsync(f);
        var team = Assert.Single(result.Luck.Teams);
        Assert.Equal(2, team.Players.Count); Assert.Equal(3, team.Players.Sum(x => x.Sources.Count));
        Assert.Equal(3, team.Result.Expected); Assert.Equal(2, team.Result.Received);
        AssertLuckScore(-42.0448032334877m, team.Result);
        Assert.NotEqual(team.Players.Average(x => x.Result.Percentage), team.Result.Percentage);
        Assert.DoesNotContain(team.Players.SelectMany(x => x.Sources), x => x.CharacterId == f.Alt.Id);
        Assert.Equal(100, result.Teams[0].Players.Sum(x => x.TeamValueShare));
        Assert.Equal(2, result.RepeatedItem!.Count); Assert.Equal(1, result.MostVersatile!.DistinctTiles);
    }

    [Fact]
    public async Task StatsPass4OutageRetainsWholeCheckpointAndReversalInvalidatesIt()
    {
        var f = await FullStatsFixtureAsync();
        var id = await PendingStatsAsync(f, 0, 0, 10); await ApproveStatsAsync(f, id); await SyncStatsAsync(f, 100);
        var original = await ReadStatsAsync(f);
        f.Clock.Advance(TimeSpan.FromHours(3));
        await SyncStatsAsync(f, new WiseOldManCompetitionResult(WiseOldManCompetitionStatus.Unavailable, null, Message: "Synthetic outage"));
        var stale = await ReadStatsAsync(f); Assert.True(stale.Luck.Stale); Assert.Equal(0, stale.Luck.Result.Percentage);
        Assert.Equal(original.Luck.CalculatedAt, stale.Luck.CalculatedAt); Assert.Equal(original.Luck.FetchedAt, stale.Luck.FetchedAt);
        await ReverseStatsAsync(f, id);
        var reversed = await ReadStatsAsync(f); Assert.Equal(0, reversed.Value.Drops); Assert.Null(reversed.Luck.Result.Percentage);
        Assert.Null(reversed.Luck.CalculatedAt); Assert.False(reversed.Luck.Stale);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(original.EvidenceRevision, (await verify.EventStatsLuckCheckpoints.SingleAsync()).EvidenceRevision);
        f.Clock.Advance(TimeSpan.FromHours(3)); await SyncStatsAsync(f, 100);
        var recovered = await ReadStatsAsync(f); AssertLuckScore(-66.7785234899329m, recovered.Luck.Result); Assert.Equal(recovered.EvidenceRevision, recovered.Luck.EvidenceRevision);
    }

    [Fact]
    public async Task StatsPass4EvidenceDuringProviderFetchAndStaleWriterCannotOverwriteNewerRevision()
    {
        var f = await FullStatsFixtureAsync(); await SyncStatsAsync(f, 100);
        EventStatsLuckCheckpoint old;
        await using (var read = new ApplicationDbContext(options)) old = await read.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        var id = await PendingStatsAsync(f, 0, 0, 10);
        f.Clock.Advance(TimeSpan.FromHours(3));
        await SyncStatsAsync(f, StatsResponse(f, new(0, 200, 200)), () => ApproveStatsAsync(f, id));
        var fresh = await ReadStatsAsync(f); Assert.Equal(1, fresh.Luck.Result.Received); Assert.Equal(2, fresh.Luck.Result.Expected);
        await using var stale = new ApplicationDbContext(options);
        await using var tx = await stale.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        Assert.False(await PublicStatsService.TryWriteCheckpointAsync(stale, f.Clock, old)); await tx.CommitAsync();
        var current = await ReadStatsAsync(f); Assert.Equal(System.Text.Json.JsonSerializer.Serialize(fresh.Luck), System.Text.Json.JsonSerializer.Serialize(current.Luck));
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("private")]
    [InlineData("unpublished")]
    [InlineData("cancelled")]
    [InlineData("import")]
    [InlineData("unknown")]
    public async Task StatsPass4QueryBoundaryDoesNotLeakUnavailableEventsOrChoosePreferredEvent(string scenario)
    {
        var f = await FullStatsFixtureAsync();
        await using var db = new ApplicationDbContext(options);
        if (scenario == "hidden") { var ev = await db.Events.SingleAsync(); ev.EndEvent(f.Clock.GetUtcNow()); ev.Hide(f.Admin.Id, f.Clock.GetUtcNow(), f.Event.Name, "Fixture hidden"); }
        if (scenario == "private") await db.Database.ExecuteSqlRawAsync("UPDATE events SET first_public_at = NULL");
        if (scenario == "unpublished") await db.Database.ExecuteSqlRawAsync("UPDATE events SET board_published = false");
        if (scenario == "cancelled") await db.Database.ExecuteSqlRawAsync("UPDATE events SET state = 'Cancelled'");
        if (scenario == "import") await db.Database.ExecuteSqlRawAsync("UPDATE events SET slug = 'det-store-danske-sommerbingo-2026'");
        await db.SaveChangesAsync();
        Assert.Null(await new PublicStatsService(db, f.Clock).GetAsync(scenario == "unknown" ? "missing" : scenario == "import" ? "det-store-danske-sommerbingo-2026" : f.Event.Slug));
    }

    [Fact]
    public async Task StatsPass4MissingHistoricalPriceIsExplicitAndCatalogueEditsKeepExactFrozenIdentity()
    {
        var f = await FullStatsFixtureAsync(); var id = await PendingStatsAsync(f, 0, 0, 10); await ApproveStatsAsync(f, id);
        await using (var mutate = new ApplicationDbContext(options))
        {
            var item = await mutate.CatalogueItems.SingleAsync(x => x.Id == f.Items[0].Id); item.SetPrice(99999, CataloguePriceSource.Manual, f.Clock.GetUtcNow());
            await mutate.SaveChangesAsync();
        }
        var frozen = await ReadStatsAsync(f); Assert.Equal(100, frozen.Value.ValueGp); Assert.Equal(f.Items[0].Id, frozen.Drops[0].Item.ItemId);
        await using (var legacy = new ApplicationDbContext(options)) await legacy.EventItemPrices.ExecuteDeleteAsync();
        var missing = await ReadStatsAsync(f); Assert.Equal(1, missing.Value.MissingPrices); Assert.Null(missing.Value.ValueGp);
        Assert.Null(missing.Drops[0].ValueGp); Assert.Null(missing.Drops[0].PriceHour); Assert.Null(missing.Teams[0].EventValueShare);
    }

    [Fact]
    public async Task BoundedLuckStatsAndTileMatchCenteredReferenceAcrossReceivedCounts()
    {
        var f = await FullStatsFixtureAsync();
        await using (var setup = new ApplicationDbContext(options))
            await setup.Database.ExecuteSqlRawAsync("UPDATE board_approval_requirement_drop_snapshots SET numeric_probability = 1.0 / 251");
        await SyncStatsAsync(f, 502);
        decimal[] expected = [-87.5436222349m, -50.0748379176m, 0m, 49.2471411372m, 78.7757269631m, 92.5216205155m, 97.7353404290m];
        for (var received = 0; received < expected.Length; received++)
        {
            if (received > 0) await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, received));
            var stats = await ReadStatsAsync(f);
            var tile = await ReadTileActivityAsync(f);
            AssertLuckScore(expected[received], stats.Luck.Result, .0000001m); // frozen probability has 12 decimal places
            AssertLuckScore(expected[received], tile.Team.Result, .0000001m);
            Assert.Equal(received, stats.Luck.Result.Received); Assert.Equal(received, tile.Team.Result.Received);
            Assert.Equal(502m, Assert.Single(tile.Team.Metrics).Count);
            AssertLuckScore(-87.5436222349m, (await ReadTileActivityAsync(f, 1)).Team.Result, .0000001m);
        }
    }

    [Theory]
    [InlineData(false, 62.5)]
    [InlineData(true, 54.6852730955111)]
    public async Task BoundedLuckGroupsExclusiveOutcomesAndConvolvesIndependentRepeatedPersonalRolls(bool independent, double expected)
    {
        var f = await FullStatsFixtureAsync(secondOutcome: true, rolls: 2);
        await using (var setup = new ApplicationDbContext(options))
        {
            var firstSource = f.Drops[0].SourceDropId;
            // Frozen rates are already personal; assumed team size must not divide them again.
            await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE board_approval_requirement_drop_snapshots SET numeric_probability = CASE WHEN source_drop_id = {firstSource} THEN 0.4 ELSE 0.3 END, conditional_on_parent = (source_drop_id = {firstSource}), parent_probability = CASE WHEN source_drop_id = {firstSource} THEN 0.5 ELSE NULL END, assumed_participants = 5");
            if (independent)
                await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE board_approval_requirement_drop_snapshots SET roll_group = 'independent' WHERE source_drop_id <> {firstSource}");
        }
        for (var i = 0; i < 3; i++) await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, i + 1, itemIndex: i % 2));
        await SyncStatsAsync(f, 2);
        var stats = await ReadStatsAsync(f); var tile = await ReadTileActivityAsync(f);
        Assert.Equal(2m, stats.Luck.Result.Expected); Assert.Equal(2m, tile.Team.Result.Expected);
        AssertLuckScore((decimal)expected, stats.Luck.Result); AssertLuckScore((decimal)expected, tile.Team.Result);
        Assert.Equal(2m, Assert.Single(tile.Team.Metrics).Count);
    }

    [Fact]
    public async Task BoundedLuckRescoresLegacyStoredStatsAndTileCountsDuringAdditiveStalenessWithoutWriting()
    {
        var f = await FullStatsFixtureAsync(players: 2, extraRegular: true);
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 1, 1, 11));
        await SyncStatsAsync(f, 100);
        var first = await ReadStatsAsync(f);
        var legacy = first.Luck with
        {
            Result = OldRatio(first.Luck.Result),
            Teams = first.Luck.Teams.Select(team => team with
            {
                Result = OldRatio(team.Result),
                Players = team.Players.Select(player => player with { Result = OldRatio(player.Result) }).ToArray()
            }).ToArray(),
            Tiles = first.Luck.Tiles!.Select(tile => tile with
            {
                Teams = tile.Teams.Select(team => team with
                {
                    Result = OldRatio(team.Result),
                    Players = team.Players.Select(player => player with { Result = OldRatio(player.Result) }).ToArray()
                }).ToArray()
            }).ToArray()
        };
        await using (var setup = new ApplicationDbContext(options))
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(legacy);
            await setup.EventStatsLuckCheckpoints.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Payload, payload));
        }
        f.Clock.Advance(TimeSpan.FromHours(3));
        await SyncStatsAsync(f, new WiseOldManCompetitionResult(WiseOldManCompetitionStatus.Unavailable, null));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 1, 12));
        await using var verify = new ApplicationDbContext(options);
        var stored = await verify.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync();
        var stats = await ReadStatsAsync(f); var tile = await ReadTileActivityAsync(f);
        Assert.True(stats.Luck.Stale); Assert.True(tile.Stale);
        Assert.Equal(3, stats.Value.Drops); Assert.Equal(2, stats.Luck.Result.Received); Assert.Equal(1, tile.Team.Result.Received);
        AssertLuckScore(-42.0448032334877m, stats.Luck.Result);
        AssertLuckScore(-76.9296651569406m, tile.Team.Result);
        AssertLuckScore(-50.1883643902809m, tile.Team.Players.Single(x => x.PlayerId == f.Players[0].Id).Result);
        Assert.Equal(first.Luck.CalculatedAt, stats.Luck.CalculatedAt); Assert.Equal(first.Luck.CalculatedAt, tile.CalculatedAt);
        Assert.Equal(first.Luck.FetchedAt, stats.Luck.FetchedAt); Assert.Equal(first.Luck.FetchedAt, tile.FetchedAt);
        Assert.Equal(first.Luck.EvidenceRevision, stats.Luck.EvidenceRevision); Assert.Equal(first.Luck.EvidenceRevision, tile.EvidenceRevision);
        Assert.Equal(300m, Assert.Single(tile.Team.Metrics).Count);
        Assert.Equal(stored.Payload, (await verify.EventStatsLuckCheckpoints.AsNoTracking().SingleAsync()).Payload);

        static StatsLuckResult OldRatio(StatsLuckResult result) => result with
        { Percentage = result.Expected is > 0 ? (result.Received / result.Expected.Value - 1) * 100 : null };
    }

    private static void AssertLuckScore(decimal expected, StatsLuckResult result, decimal tolerance = .00000001m)
    {
        Assert.Equal(StatsLuckStatus.Calculated, result.Status);
        Assert.NotNull(result.Percentage);
        Assert.InRange(result.Percentage.Value, -100m, 100m);
        Assert.InRange(result.Percentage.Value, expected - tolerance, expected + tolerance);
    }

    private async Task<FullStatsFixture> FullStatsFixtureAsync(int rolls = 1, bool secondOutcome = false, int target = 10, int weight = 1,
        int players = 1, bool extraRegular = false, string metric = "vorkath", int dimensions = 2, int actualStartedHoursAgo = 1, int eventDurationHours = 11, DateTimeOffset? clockNow = null)
    {
        var clock = new TestClock(clockNow ?? new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero)); var now = clock.GetUtcNow();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "StatsAdmin", "STATSADMIN", now); admin.SetGlobalRole(GlobalRole.SuperAdmin);
        var actualStart = now.AddHours(-actualStartedHoursAgo);
        var ev = new BingoEvent(Guid.NewGuid(), "Synthetic Stats event", "stats-event", "", "UTC", actualStart.AddHours(-2), actualStart.AddHours(-1), actualStart, actualStart.AddHours(eventDurationHours), actualStart.AddHours(eventDurationHours), 20, admin.Id, now);
        ev.OpenSignups(actualStart.AddHours(-2)); ev.CloseSignups(actualStart.AddHours(-1)); ev.SetDraftRosterPublication(true); ev.SetBoardPublication(true, actualStart.AddHours(-1)); ev.MarkFirstPublic(actualStart.AddHours(-2)); ev.StartEvent(actualStart);
        var team = new Team(Guid.NewGuid(), ev.Id, "Public team", "public-team", TeamFormationType.Drafted, null, true); team.Finalize(actualStart.AddHours(-1));
        var participants = Enumerable.Range(0, players).Select(i => new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, i + 1, actualStart.AddHours(-2), SignupSource.Website)).ToArray();
        var chars = participants.Select((_, i) => new OsrsCharacter(Guid.NewGuid(), "Stats Player " + i, "STATS PLAYER " + i, now)).ToArray();
        var form = new SignupForm(Guid.NewGuid(), ev.Id, now.AddHours(-3));
        var primary = new SignupQuestion(Guid.NewGuid(), form.Id, ev.Id, "primary", "Primary account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var assignments = participants.Select((p, i) => new EventParticipantCharacter(Guid.NewGuid(), ev.Id, p.Id, chars[i].Id, 0, actualStart.AddHours(-1), null, primary.Id, EventCharacterRole.Playing, 0, EhbSource.Manual, null)).ToList();
        var draft = new DraftSession(Guid.NewGuid(), ev.Id, 1); draft.FinalizeDirect(actualStart.AddHours(-1));
        var publication = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, actualStart.AddHours(-1), admin.Id, DraftPublicationMethod.DirectRoster);
        var publishedRoster = participants.Select((participant, index) => new DraftPublicationRoster(
            Guid.NewGuid(), publication.Id, team.Id, participant.Id, TeamMembershipRole.Participant, null, chars[index].DisplayName)).ToArray();
        var regular = new OsrsCharacter(Guid.NewGuid(), "Regular two", "REGULAR TWO", now);
        if (extraRegular) assignments.Add(new(Guid.NewGuid(), ev.Id, participants[0].Id, regular.Id, 1, actualStart.AddHours(-1), null, null, EventCharacterRole.Playing, 0, EhbSource.Manual, null));
        var alt = new OsrsCharacter(Guid.NewGuid(), "Informational", "INFORMATIONAL", now);
        assignments.Add(new(Guid.NewGuid(), ev.Id, participants[0].Id, alt.Id, 2, actualStart.AddHours(-1), null, null, EventCharacterRole.Informational, null, null, null));
        var boss = new BossActivity(Guid.NewGuid(), "Vorkath fixture", "stats-source", "Boss", 10, now); boss.ConfigureApi(metric); boss.RecordMapping(ApiMappingStatus.Verified, now);
        var items = Enumerable.Range(0, secondOutcome ? 2 : 1).Select(i => { var item = new CatalogueItem(Guid.NewGuid(), "Exact variant " + i, "EXACT VARIANT " + i); item.SetPrice(100 + i * 100, CataloguePriceSource.Manual, now); item.Update(item.Name, item.NormalizedName, null, null, "https://oldschool.runescape.wiki/images/Dragon_warhammer.png"); return item; }).ToArray();
        var sources = items.Select((item, i) => new SourceDrop(Guid.NewGuid(), boss.Id, item.Id, i == 0 ? "1/100" : "1/200", i == 0 ? .01m : .005m, 1, now)).ToArray();
        var board = new Board(Guid.NewGuid(), ev.Id, "Stats board", dimensions, dimensions);
        var tiles = Enumerable.Range(0, dimensions * dimensions).Select(i => new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), i / dimensions, i % dimensions, "Tile " + i, "", "", 10)).ToArray();
        var requirements = tiles.Select((tile, i) => new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, target, true, true, "Objective " + i, i >= 2, weight)).ToArray();
        var drops = requirements.Take(2).SelectMany(req => sources.Select((source, i) => new BoardRequirementDropSnapshot(Guid.NewGuid(), req.Id, source.Id, items[i].Id, boss.Name, items[i].Name, source.DisplayRate, i == 0 ? .01m : .005m, null, 1, weight,
            DropProbabilityScope.Participant, false, null, 1, rolls))).ToArray();
        await using var db = new ApplicationDbContext(options);
        db.AddRange(admin, ev, team, draft, publication, boss, board, regular, alt, form, primary); db.AddRange(participants); db.AddRange(chars); db.AddRange(assignments); db.AddRange(publishedRoster); db.AddRange(items); db.AddRange(sources); db.AddRange(tiles); db.AddRange(requirements); db.AddRange(drops);
        db.AddRange(participants.Select(p => new TeamMembership(Guid.NewGuid(), team.Id, p.Id, TeamMembershipRole.Participant, now.AddHours(-2), null, "Fixture")));
        await BoardApprovalFixture.PublishAsync(db, board, now.AddHours(-2), tiles, requirements, drops);
        // The common fixture helper predates personal rolls. Set fixture mechanics before first basis capture.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE board_approval_requirement_drop_snapshots SET rolls_per_completion = {rolls}");
        foreach (var req in await db.BoardApprovalRequirementSnapshots.Where(x => !x.ManualObjective).ToListAsync())
            db.Add(new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), req.Id, boss.Id, boss.Name, 10, 1));
        db.AddRange(items.Select(item => EventItemPrice.Introduce(ev.Id, item, now.AddHours(-2), now)));
        await db.SaveChangesAsync();
        return new(ev, admin, team, participants, chars, assignments, alt, boss, items, tiles, requirements, drops, clock);
    }

    private async Task<Guid> PendingStatsAsync(FullStatsFixture f, int tile, int player, int minutes, int weight = 1, int itemIndex = 0)
    {
        var drop = f.Drops.Where(x => x.RequirementId == f.Requirements[tile].Id).Skip(itemIndex).FirstOrDefault();
        await using var db = new ApplicationDbContext(options);
        var teamId = await db.TeamMemberships.Where(x => x.EventParticipantId == f.Players[player].Id && x.LeftAt == null).Select(x => x.TeamId).SingleAsync();
        var s = new Submission(Guid.NewGuid(), f.Event.Id, teamId, f.Tiles[tile].Id, f.Requirements[tile].Id, drop?.Id,
            f.Players[player].Id, f.Characters[player].Id, f.Characters[player].DisplayName, f.Admin.Id, weight,
            f.Event.ActualStartedAt!.Value.AddMinutes(minutes), null, null);
        db.Add(s); await db.SaveChangesAsync(); return s.Id;
    }
    private async Task ApproveStatsAsync(FullStatsFixture f, Guid submission)
    { await using var db = new ApplicationDbContext(options); await new SubmissionService(db, null!, f.Clock).ApproveAsync(submission, f.Admin.Id); }
    private async Task ReverseStatsAsync(FullStatsFixture f, Guid submission)
    { await using var db = new ApplicationDbContext(options); await new SubmissionService(db, null!, f.Clock).ReverseAsync(submission, f.Admin.Id, "Synthetic reversal"); }
    private async Task PublishCurrentRosterAsync(FullStatsFixture f, bool retainPreviousEntries = false)
    {
        await using var db = new ApplicationDbContext(options);
        var draft = await db.DraftSessions.SingleAsync(x => x.EventId == f.Event.Id);
        var current = await db.DraftPublicationCycles.SingleAsync(x => x.DraftSessionId == draft.Id && x.SupersededAt == null);
        var previousEntries = retainPreviousEntries
            ? await db.DraftPublicationRosters.Where(x => x.DraftPublicationCycleId == current.Id).ToListAsync()
            : [];
        current.Supersede(f.Clock.GetUtcNow(), f.Admin.Id, "Controlled fixture roster replacement");
        var cycleNumber = await db.DraftPublicationCycles.Where(x => x.DraftSessionId == draft.Id).MaxAsync(x => x.CycleNumber) + 1;
        var replacement = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, cycleNumber, f.Clock.GetUtcNow(), f.Admin.Id, DraftPublicationMethod.DirectRoster);
        db.Add(replacement);
        var names = f.Players.Select((player, index) => (player.Id, Name: f.Characters[index].DisplayName)).ToDictionary(x => x.Id, x => x.Name);
        var memberships = await db.TeamMemberships.Where(x => x.LeftAt == null && names.Keys.Contains(x.EventParticipantId)).ToListAsync();
        foreach (var member in memberships)
            db.Add(new DraftPublicationRoster(Guid.NewGuid(), replacement.Id, member.TeamId, member.EventParticipantId, member.Role, null, names[member.EventParticipantId]));
        foreach (var entry in previousEntries.Where(entry => !memberships.Any(member => member.TeamId == entry.TeamId && member.EventParticipantId == entry.EventParticipantId)))
            db.Add(new DraftPublicationRoster(Guid.NewGuid(), replacement.Id, entry.TeamId, entry.EventParticipantId, entry.Role, entry.EffectivePickNumber, entry.PublicCharacterName));
        await db.SaveChangesAsync();
    }
    private async Task<PublicEventStats> ReadStatsAsync(FullStatsFixture f)
    { await using var db = new ApplicationDbContext(options); return Assert.IsType<PublicEventStats>(await new PublicStatsService(db, f.Clock).GetAsync(f.Event.Slug)); }
    private Task SyncStatsAsync(FullStatsFixture f, int kills) => SyncStatsAsync(f, new WiseOldManMetricDelta(0, kills, kills));
    private Task SyncStatsAsync(FullStatsFixture f, WiseOldManMetricDelta? delta) => SyncStatsAsync(f, StatsResponse(f, delta));
    private static WiseOldManCompetitionResult StatsResponse(FullStatsFixture f, WiseOldManMetricDelta? delta) => new(WiseOldManCompetitionStatus.Success,
        new(42, "Controlled Stats competition", f.Event.EventStartsAt!.Value, f.Event.EventEndsAt!.Value, f.Clock.GetUtcNow(),
            f.Assignments.Select(a => new WiseOldManCompetitionParticipant(a.OsrsCharacterId.ToString(), "regular", 1, 0, 1,
                delta is null ? new Dictionary<string, WiseOldManMetricDelta>() : new Dictionary<string, WiseOldManMetricDelta> { [f.Boss.ExternalIdentifier!] = delta }, f.Clock.GetUtcNow().AddMinutes(-5))).ToArray()));
    private async Task SyncStatsAsync(FullStatsFixture f, WiseOldManCompetitionResult result, Func<Task>? before = null)
    {
        await using var db = new ApplicationDbContext(options);
        // Match the controlled provider payload to public character names only inside this fixture.
        var names = await db.OsrsCharacters.ToDictionaryAsync(x => x.Id, x => x.DisplayName);
        if (result.Competition is { } c) result = result with { Competition = c with { Participants = c.Participants.Select(p => p with { Username = names[Guid.Parse(p.Username)] }).ToArray() } };
        var client = new StatsClient(StatsResponse(f, new(0, 100, 100)), result, before);
        var service = new EventCompetitionSynchronizationService(db, client, new FixedStatus(), f.Clock);
        var actor = new LifecycleActor(f.Admin.Id, f.Admin.LoginName);
        if (!await db.EventCompetitionSynchronizations.AnyAsync()) Assert.True((await service.ConfigureAsync(f.Event.Id, (await db.Events.SingleAsync()).Version, 42, false, actor)).Succeeded);
        await service.RefreshAsync(f.Event.Id, actor);
    }
    private sealed record FullStatsFixture(BingoEvent Event, Account Admin, Team Team, EventParticipant[] Players, OsrsCharacter[] Characters,
        IReadOnlyList<EventParticipantCharacter> Assignments, OsrsCharacter Alt, BossActivity Boss, CatalogueItem[] Items, BoardTile[] Tiles,
        BoardRequirementSnapshot[] Requirements, BoardRequirementDropSnapshot[] Drops, TestClock Clock);
    private sealed class StatsClient(WiseOldManCompetitionResult validation, WiseOldManCompetitionResult result, Func<Task>? before) : IWiseOldManCompetitionClient
    {
        public Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, CancellationToken cancellationToken = default) => Task.FromResult(validation);
        public async Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, IReadOnlyCollection<string> metrics, CancellationToken cancellationToken = default)
        { if (before is not null) await before(); return result; }
    }
}
