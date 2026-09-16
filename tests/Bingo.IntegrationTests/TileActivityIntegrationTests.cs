using System.Net;
using System.Text.Json;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Stats;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Stats;
using Bingo.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bingo.IntegrationTests;

public sealed partial class Slice10Pass102CompetitionSynchronizationTests
{
    [Fact]
    public async Task TileActivityPoolsPlayingAccountsAndDistinctOutcomesWithOnlyThisTilesDrops()
    {
        var f = await FullStatsFixtureAsync(secondOutcome: true, players: 2, extraRegular: true, target: 1);
        await using (var setup = new ApplicationDbContext(options))
        {
            // The same outcome appears in another requirement of this tile as well as another tile.
            var req = new BoardRequirementSnapshot(Guid.NewGuid(), f.Tiles[0].Id, 2, 10, true, false, "Repeated outcome", false);
            var drop = f.Drops[0];
            var working = new BoardRequirementDropSnapshot(Guid.NewGuid(), req.Id, drop.SourceDropId, drop.ItemIdSnapshot, drop.BossName, drop.ItemName, drop.DisplayRate, drop.NumericProbability, null, 1, 1);
            var approvalTile = await setup.BoardApprovalTileSnapshots.SingleAsync(x => x.BoardTileId == f.Tiles[0].Id);
            var frozen = new BoardApprovalRequirementSnapshot(Guid.NewGuid(), approvalTile.Id, req.Id, 2, 10, true, false, 1, req.Description, false);
            setup.AddRange(req, working, frozen,
                new BoardApprovalRequirementDropSnapshot(Guid.NewGuid(), frozen.Id, drop.SourceDropId, drop.ItemIdSnapshot, drop.BossName, drop.ItemName, drop.DisplayRate, drop.NumericProbability, null, 1, 1, 1),
                new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), frozen.Id, f.Boss.Id, f.Boss.Name, 10, 1));
            await setup.SaveChangesAsync();
        }
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 1, 1, 11));
        await SyncStatsAsync(f, 100);
        var tile = await ReadTileActivityAsync(f);
        Assert.Equal(1, tile.Team.Result.Received);
        Assert.Equal(4.5m, tile.Team.Result.Expected);
        AssertLuckScore(-93.3049094961478m, tile.Team.Result);
        Assert.Equal(300m, Assert.Single(tile.Team.Metrics).Count);
        Assert.Equal(200m, Assert.Single(tile.Team.Players.Single(x => x.PlayerId == f.Players[0].Id).Metrics).Count);
        Assert.Equal(100m, Assert.Single(tile.Team.Players.Single(x => x.PlayerId == f.Players[1].Id).Metrics).Count);
        Assert.Equal(0, tile.Team.Players.Single(x => x.PlayerId == f.Players[1].Id).Result.Received);
        AssertLuckScore(-79.4163665582917m, tile.Team.Players.Single(x => x.PlayerId == f.Players[1].Id).Result);
        Assert.Equal(2, (await ReadStatsAsync(f)).Luck.Result.Received);
        Assert.Equal(1, (await ReadTileActivityAsync(f, 1)).Team.Result.Received);
        Assert.False((await ReadTileActivityAsync(f, 2)).HasDropOutcomes);
        Assert.Empty((await ReadTileActivityAsync(f, 2)).Team.Metrics);
        // Full-event KC continues after the credited requirement is complete.
        f.Clock.Advance(TimeSpan.FromHours(3)); await SyncStatsAsync(f, 200);
        Assert.Equal(600m, Assert.Single((await ReadTileActivityAsync(f)).Team.Metrics).Count);
        Assert.Equal(600m, Assert.Single((await ReadTileActivityAsync(f, 1)).Team.Metrics).Count);
    }

    [Fact]
    public async Task TileActivitySeparatesTeamEvidenceAndRejectsUnknownTargets()
    {
        var f = await FullStatsFixtureAsync(players: 2);
        var otherTeam = new Team(Guid.NewGuid(), f.Event.Id, "Other team", "other-team", TeamFormationType.Drafted, null, true);
        otherTeam.Finalize(f.Clock.GetUtcNow());
        await using (var setup = new ApplicationDbContext(options))
        {
            (await setup.TeamMemberships.SingleAsync(x => x.EventParticipantId == f.Players[1].Id)).Leave(f.Clock.GetUtcNow(), "Controlled team split");
            setup.Add(otherTeam);
            setup.Add(new TeamMembership(Guid.NewGuid(), otherTeam.Id, f.Players[1].Id, TeamMembershipRole.Participant, f.Clock.GetUtcNow(), null, "Controlled team split"));
            await setup.SaveChangesAsync();
        }
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 1, 11));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 1, 12));
        await SyncStatsAsync(f, 100);
        Assert.Equal(1, (await ReadTileActivityAsync(f)).Team.Result.Received);
        await using var db = new ApplicationDbContext(options);
        var stats = new PublicStatsService(db, f.Clock);
        var other = (await stats.GetTileAsync(f.Event.Slug, otherTeam.Id, f.Tiles[0].Id))!;
        Assert.Equal(2, other.Team.Result.Received); AssertLuckScore(61.7447060227591m, other.Team.Result);
        Assert.Equal(100m, Assert.Single(other.Team.Metrics).Count);
        Assert.Null(await stats.GetTileAsync(f.Event.Slug, Guid.NewGuid(), f.Tiles[0].Id));
        Assert.Null(await stats.GetTileAsync(f.Event.Slug, otherTeam.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task TileActivityKeepsModeCountsSeparateAndUsesFrozenPersonalRates()
    {
        var f = await FullStatsFixtureAsync(secondOutcome: true, metric: "chambers_of_xeric", rolls: 2);
        await using (var setup = new ApplicationDbContext(options))
        {
            var mode = new BossActivity(Guid.NewGuid(), "Chambers challenge fixture", "tile-challenge", "Raid", 10, f.Clock.GetUtcNow());
            mode.ConfigureApi("chambers_of_xeric_challenge_mode"); mode.RecordMapping(ApiMappingStatus.Verified, f.Clock.GetUtcNow());
            setup.Add(mode); await setup.SaveChangesAsync();
            var sourceId = f.Drops[1].SourceDropId;
            await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE source_drops SET boss_activity_id = {mode.Id} WHERE id = {sourceId}");
            await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE board_approval_requirement_drop_snapshots SET boss_name = {mode.Name} WHERE source_drop_id = {sourceId}");
            foreach (var req in await setup.BoardApprovalRequirementSnapshots.Where(x => !x.ManualObjective).ToListAsync())
                setup.Add(new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), req.Id, mode.Id, mode.Name, 10, 1));
            await setup.SaveChangesAsync();
        }
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        var response = StatsResponse(f, new(0, 13, 13));
        response = response with
        {
            Competition = response.Competition! with
            {
                Participants = response.Competition.Participants.Select(p => p with
                { Metrics = new Dictionary<string, WiseOldManMetricDelta> { ["chambers_of_xeric"] = new(0, 13, 13), ["chambers_of_xeric_challenge_mode"] = new(0, 7, 7) } }).ToArray()
            }
        };
        await SyncStatsAsync(f, response);
        var tile = await ReadTileActivityAsync(f);
        Assert.Equal(2, tile.Team.Metrics.Count);
        Assert.Equal(13m, tile.Team.Metrics.Single(x => x.Metric == "chambers_of_xeric").Count);
        Assert.Equal(7m, tile.Team.Metrics.Single(x => x.Metric == "chambers_of_xeric_challenge_mode").Count);
        Assert.Equal(.33m, tile.Team.Result.Expected); // 13 × .01 × 2 + 7 × .005 × 2; no team-size division.
        await using (var correction = new ApplicationDbContext(options))
            await correction.Database.ExecuteSqlRawAsync("UPDATE board_approval_requirement_drop_snapshots SET numeric_probability = 0.9");
        Assert.Equal(.33m, (await ReadTileActivityAsync(f)).Team.Result.Expected);
    }

    [Theory]
    [InlineData("zero", false, StatsLuckStatus.NoEligibleActivity, 0)]
    [InlineData("zero", true, StatsLuckStatus.WaitingForActivityUpdate, null)]
    [InlineData("unranked", false, StatsLuckStatus.NoEligibleActivity, 0)]
    [InlineData("unranked", true, StatsLuckStatus.WaitingForActivityData, null)]
    [InlineData("missing", false, StatsLuckStatus.WaitingForActivityData, null)]
    [InlineData("estimated", true, StatsLuckStatus.Calculated, 100)]
    public async Task TileActivityPreservesZeroMissingUnrankedAndEstimatedSemantics(string scenario, bool drop, StatsLuckStatus status, int? kc)
    {
        var f = await FullStatsFixtureAsync();
        if (drop) await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        WiseOldManMetricDelta? delta = scenario switch { "missing" => null, "unranked" => new(-1, -1, 0), "estimated" => new(-1, 100, 100), _ => new(0, 0, 0) };
        await SyncStatsAsync(f, delta);
        var tile = await ReadTileActivityAsync(f);
        Assert.Equal(status, tile.Team.Result.Status);
        Assert.Equal(kc is { } value ? (decimal?)value : null, Assert.Single(tile.Team.Metrics).Count);
        Assert.Equal(scenario == "estimated", tile.Team.Result.Estimated);
        Assert.Equal(scenario == "unranked", tile.Team.Result.ZeroRecordedApproximation);
        if (status != StatsLuckStatus.Calculated) Assert.Null(tile.Team.Result.Percentage);
    }

    [Fact]
    public async Task TileActivityIncompleteTeamDoesNotPresentPartialKcAsTheTeamTotal()
    {
        var f = await FullStatsFixtureAsync(players: 2);
        var response = StatsResponse(f, new(0, 100, 100));
        response = response with { Competition = response.Competition! with { Participants = response.Competition.Participants.Where(x => x.Username != f.Characters[1].Id.ToString()).ToArray() } };
        await SyncStatsAsync(f, response);
        var tile = await ReadTileActivityAsync(f);
        Assert.Equal(StatsLuckStatus.Incomplete, tile.Team.Result.Status);
        Assert.Null(Assert.Single(tile.Team.Metrics).Count);
        Assert.Equal(StatsLuckStatus.Incomplete, Assert.Single(tile.Team.Metrics).Status);
        Assert.Equal(2, tile.Team.Players.Count);
    }

    [Fact]
    public async Task TileActivityRetainsWholeSnapshotDuringOutageAndInvalidatesAfterReversal()
    {
        var f = await FullStatsFixtureAsync();
        var original = await PendingStatsAsync(f, 0, 0, 10); await ApproveStatsAsync(f, original); await SyncStatsAsync(f, 100);
        var first = await ReadTileActivityAsync(f);
        f.Clock.Advance(TimeSpan.FromHours(3));
        await SyncStatsAsync(f, new WiseOldManCompetitionResult(WiseOldManCompetitionStatus.Unavailable, null));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 12));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 1, 0, 13));
        var stale = await ReadTileActivityAsync(f);
        Assert.True(stale.Stale); Assert.Equal(1, stale.Team.Result.Received); Assert.Equal(0m, stale.Team.Result.Percentage);
        Assert.Equal(first.CalculatedAt, stale.CalculatedAt); Assert.Equal(first.EvidenceRevision, stale.EvidenceRevision);
        Assert.Equal(0, (await ReadTileActivityAsync(f, 1)).Team.Result.Received);
        await ReverseStatsAsync(f, original);
        var reversed = await ReadTileActivityAsync(f);
        Assert.False(reversed.Stale); Assert.Equal(1, reversed.Team.Result.Received); Assert.Null(reversed.Team.Result.Percentage);
        Assert.Null(Assert.Single(reversed.Team.Metrics).Count); Assert.Null(reversed.CalculatedAt);
        f.Clock.Advance(TimeSpan.FromHours(3)); await SyncStatsAsync(f, 200);
        var recovered = await ReadTileActivityAsync(f);
        AssertLuckScore(-50.1883643902809m, recovered.Team.Result); Assert.Equal(200m, Assert.Single(recovered.Team.Metrics).Count);
    }

    [Fact]
    public async Task TileActivityApprovalUsesCorrectedTileAndPlayingCharacter()
    {
        var f = await FullStatsFixtureAsync(players: 2); await SyncStatsAsync(f, 100);
        var pending = await PendingStatsAsync(f, 0, 0, 10);
        await using (var correction = new ApplicationDbContext(options))
            await new SubmissionService(correction, null!, f.Clock).EditMetadataAsync(new(pending, f.Admin.Id,
                f.Tiles[1].Id, f.Requirements[1].Id, f.Drops.Single(x => x.RequirementId == f.Requirements[1].Id).Id,
                f.Characters[1].Id, "Controlled tile and character correction"));
        await ApproveStatsAsync(f, pending);
        Assert.Equal(0, (await ReadTileActivityAsync(f)).Team.Result.Received);
        var corrected = await ReadTileActivityAsync(f, 1);
        Assert.Equal(1, corrected.Team.Result.Received);
        Assert.Equal(1, corrected.Team.Players.Single(x => x.PlayerId == f.Players[1].Id).Result.Received);
        Assert.Equal(0, corrected.Team.Players.Single(x => x.PlayerId == f.Players[0].Id).Result.Received);
        AssertLuckScore(-50.1883643902809m, corrected.Team.Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TileActivityLegacyCheckpointRetainsExpiredKcAndCoherentTileLuckWithoutReadWrites(bool refreshFailure)
    {
        var f = await FullStatsFixtureAsync(players: 2, extraRegular: true);
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 1, 11));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 1, 12));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 1, 0, 13));
        await SyncStatsAsync(f, 100);
        var first = await ReadTileActivityAsync(f);
        await using (var legacy = new ApplicationDbContext(options))
            await legacy.Database.ExecuteSqlRawAsync("UPDATE event_stats_luck_checkpoints SET payload = payload - 'Tiles'");
        f.Clock.Advance(TimeSpan.FromHours(3));
        if (refreshFailure) await SyncStatsAsync(f, new WiseOldManCompetitionResult(WiseOldManCompetitionStatus.Unavailable, null));
        var retained = await ReadTileActivityAsync(f);
        Assert.True(retained.Stale);
        Assert.Equal(300m, Assert.Single(retained.Team.Metrics).Count);
        Assert.Equal(3, retained.Team.Result.Received); // Event-wide received is four; only three belong here.
        Assert.Equal(0m, retained.Team.Result.Percentage);
        AssertLuckScore(-50.1883643902809m, retained.Team.Players.Single(x => x.PlayerId == f.Players[0].Id).Result);
        AssertLuckScore(61.7447060227591m, retained.Team.Players.Single(x => x.PlayerId == f.Players[1].Id).Result);
        Assert.Equal(first.CalculatedAt, retained.CalculatedAt);
        Assert.Equal(first.FetchedAt, retained.FetchedAt);
        Assert.Equal(first.EvidenceRevision, retained.EvidenceRevision);
        Assert.Equal(1, (await ReadTileActivityAsync(f, 1)).Team.Result.Received);
        await AssertRetainedTileHttpAsync(f, retained);
        await using var verify = new ApplicationDbContext(options);
        var checkpoint = await verify.EventStatsLuckCheckpoints.SingleAsync();
        Assert.False(JsonDocument.Parse(checkpoint.Payload).RootElement.TryGetProperty("Tiles", out _));
        Assert.Equal(first.CalculatedAt, checkpoint.CalculatedAt);
        Assert.Equal(first.FetchedAt, checkpoint.FetchedAt);
    }

    [Fact]
    public async Task TileActivityLegacyCheckpointRetainsKcButWithholdsLuckAfterAdditiveEvidence()
    {
        var f = await FullStatsFixtureAsync(players: 2, extraRegular: true);
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await SyncStatsAsync(f, 100);
        var first = await ReadTileActivityAsync(f);
        await using (var legacy = new ApplicationDbContext(options))
            await legacy.Database.ExecuteSqlRawAsync("UPDATE event_stats_luck_checkpoints SET payload = payload - 'Tiles'");
        f.Clock.Advance(TimeSpan.FromHours(3));
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 1, 11));
        var retained = await ReadTileActivityAsync(f);
        Assert.True(retained.Stale);
        Assert.Equal(300m, Assert.Single(retained.Team.Metrics).Count);
        Assert.Equal(2, retained.Team.Result.Received);
        Assert.Null(retained.Team.Result.Expected); Assert.Null(retained.Team.Result.Percentage);
        Assert.Equal(StatsLuckStatus.WaitingForActivityData, retained.Team.Result.Status);
        Assert.All(retained.Team.Players, player => Assert.Null(player.Result.Percentage));
        Assert.Equal(first.CalculatedAt, retained.CalculatedAt); Assert.Equal(first.FetchedAt, retained.FetchedAt);
        Assert.Equal(first.EvidenceRevision, retained.EvidenceRevision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TileActivityWithoutCheckpointRetainsCompatibleExpiredRawKcWithoutInventingLuck(bool refreshFailure)
    {
        var f = await FullStatsFixtureAsync(players: 2, extraRegular: true);
        await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10));
        await SyncStatsAsync(f, 100);
        var first = await ReadTileActivityAsync(f);
        await using (var setup = new ApplicationDbContext(options))
            await setup.EventStatsLuckCheckpoints.ExecuteDeleteAsync();
        f.Clock.Advance(TimeSpan.FromHours(3));
        if (refreshFailure) await SyncStatsAsync(f, new WiseOldManCompetitionResult(WiseOldManCompetitionStatus.Unavailable, null));
        var retained = await ReadTileActivityAsync(f);
        Assert.True(retained.Stale);
        Assert.Equal(300m, Assert.Single(retained.Team.Metrics).Count);
        Assert.Equal(200m, Assert.Single(retained.Team.Players.Single(x => x.PlayerId == f.Players[0].Id).Metrics).Count);
        Assert.Equal(100m, Assert.Single(retained.Team.Players.Single(x => x.PlayerId == f.Players[1].Id).Metrics).Count);
        Assert.Null(retained.Team.Result.Expected); Assert.Null(retained.Team.Result.Percentage);
        Assert.Null(retained.CalculatedAt); Assert.Equal(first.FetchedAt, retained.FetchedAt);
        await AssertRetainedTileHttpAsync(f, retained);
        await using var verify = new ApplicationDbContext(options);
        Assert.Empty(await verify.EventStatsLuckCheckpoints.ToListAsync());
    }

    private async Task AssertRetainedTileHttpAsync(FullStatsFixture f, StatsTileActivity retained)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.ConfigureServices(services => { services.RemoveAll<IHostedService>(); services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(f.Clock); });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var route = $"/Events/{f.Event.Slug}/Board/{f.Team.Slug}/Tiles/{f.Tiles[0].Id}";
        foreach (var path in new[] { route, route + "?handler=Sidebar" })
        {
            var html = await client.GetStringAsync(path);
            var start = html.IndexOf("data-tile-activity", StringComparison.Ordinal);
            Assert.True(start >= 0);
            var section = WebUtility.HtmlDecode(html[start..html.IndexOf("</section>", start, StringComparison.Ordinal)]);
            Assert.Contains("Last available result", section, StringComparison.Ordinal);
            Assert.Contains(retained.FetchedAt!.Value.ToString("O"), section, StringComparison.Ordinal);
            Assert.Contains(">+300</span>", section, StringComparison.Ordinal);
            Assert.Contains("Tile activity contributors", section, StringComparison.Ordinal);
            Assert.Contains(f.Characters[0].DisplayName, section, StringComparison.Ordinal);
            Assert.Contains(f.Characters[1].DisplayName, section, StringComparison.Ordinal);
            Assert.Contains(">+200</strong>", section, StringComparison.Ordinal);
            Assert.Contains(">+100</strong>", section, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task TileActivityHttpShowsOnlyPositiveContributorsWithinEachModeAndOmitsEmptyGroups(int phase)
    {
        var f = await FullStatsFixtureAsync(secondOutcome: true, players: 4, metric: "chambers_of_xeric");
        var mode = new BossActivity(Guid.NewGuid(), "Chambers challenge fixture", "tile-challenge", "Raid", 10, f.Clock.GetUtcNow());
        mode.ConfigureApi("chambers_of_xeric_challenge_mode"); mode.RecordMapping(ApiMappingStatus.Verified, f.Clock.GetUtcNow());
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.Add(mode); await setup.SaveChangesAsync();
            var sourceId = f.Drops[1].SourceDropId;
            await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE source_drops SET boss_activity_id = {mode.Id} WHERE id = {sourceId}");
            await setup.Database.ExecuteSqlInterpolatedAsync($"UPDATE board_approval_requirement_drop_snapshots SET boss_name = {mode.Name} WHERE source_drop_id = {sourceId}");
            foreach (var req in await setup.BoardApprovalRequirementSnapshots.Where(x => !x.ManualObjective).ToListAsync())
                setup.Add(new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), req.Id, mode.Id, mode.Name, 10, 1));
            await setup.SaveChangesAsync();
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.ConfigureServices(services => { services.RemoveAll<IHostedService>(); services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(f.Clock); });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var route = $"/Events/{f.Event.Slug}/Board/{f.Team.Slug}/Tiles/{f.Tiles[0].Id}";
        // Isolated cases: both groups contribute, only the regular mode, or neither.
        var response = StatsResponse(f, null);
        response = response with
        {
            Competition = response.Competition! with
            {
                Participants = response.Competition.Participants.Select(p => p with
                {
                    Metrics = p.Username == f.Characters[3].Id.ToString() ? new Dictionary<string, WiseOldManMetricDelta>() : new Dictionary<string, WiseOldManMetricDelta>
                    {
                        ["chambers_of_xeric"] = new(0, phase < 2 && p.Username == f.Characters[0].Id.ToString() ? 13 : 0, phase < 2 && p.Username == f.Characters[0].Id.ToString() ? 13 : 0),
                        ["chambers_of_xeric_challenge_mode"] = new(0, phase == 0 && p.Username == f.Characters[1].Id.ToString() ? 7 : 0, phase == 0 && p.Username == f.Characters[1].Id.ToString() ? 7 : 0)
                    }
                }).ToArray()
            }
        };
        await SyncStatsAsync(f, response);
        var activity = await ReadTileActivityAsync(f);
        Assert.Equal(4, activity.Team.Players.Count);
        Assert.All(activity.Team.Players.Single(p => p.PlayerId == f.Players[2].Id).Metrics, metric => Assert.Equal(0m, metric.Count));
        Assert.All(activity.Team.Players.Single(p => p.PlayerId == f.Players[3].Id).Metrics, metric => Assert.Null(metric.Count));
        Assert.All(activity.Team.Metrics, metric => Assert.Null(metric.Count));
        Assert.Equal(StatsLuckStatus.Incomplete, activity.Team.Result.Status);
        foreach (var path in new[] { route, route + "?handler=Sidebar" })
        {
            var html = await client.GetStringAsync(path);
            var start = html.IndexOf("data-tile-activity", StringComparison.Ordinal);
            Assert.True(start >= 0);
            var section = WebUtility.HtmlDecode(html[start..html.IndexOf("</section>", start, StringComparison.Ordinal)]);
            Assert.Contains("Incomplete activity data", section, StringComparison.Ordinal);
            Assert.Contains(f.Boss.Name, section, StringComparison.Ordinal);
            Assert.Contains(mode.Name, section, StringComparison.Ordinal);
            if (phase == 2)
            {
                Assert.DoesNotContain("<details", section, StringComparison.Ordinal);
                Assert.DoesNotContain("Tile activity contributors", section, StringComparison.Ordinal);
                continue;
            }
            var contributors = section[section.IndexOf("<details", StringComparison.Ordinal)..];
            Assert.DoesNotContain(f.Characters[2].DisplayName, contributors, StringComparison.Ordinal);
            Assert.DoesNotContain(f.Characters[3].DisplayName, contributors, StringComparison.Ordinal);
            var groups = contributors.Split("</ul>", StringSplitOptions.None).Where(part => part.Contains("<ul ", StringComparison.Ordinal)).ToArray();
            Assert.Equal(phase == 0 ? 2 : 1, groups.Length);
            var regular = Assert.Single(groups, group => group.Contains(f.Boss.Name, StringComparison.Ordinal));
            Assert.Contains(f.Characters[0].DisplayName, regular, StringComparison.Ordinal);
            Assert.Contains(">+13</strong>", regular, StringComparison.Ordinal);
            Assert.DoesNotContain(f.Characters[1].DisplayName, regular, StringComparison.Ordinal);
            if (phase == 0)
            {
                var challenge = Assert.Single(groups, group => group.Contains(mode.Name, StringComparison.Ordinal));
                Assert.Contains(f.Characters[1].DisplayName, challenge, StringComparison.Ordinal);
                Assert.Contains(">+7</strong>", challenge, StringComparison.Ordinal);
                Assert.DoesNotContain(f.Characters[0].DisplayName, challenge, StringComparison.Ordinal);
            }
            else
            {
                Assert.DoesNotContain(mode.Name, contributors, StringComparison.Ordinal);
                Assert.DoesNotContain(f.Characters[1].DisplayName, contributors, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task TileActivityHttpUsesSameSectionForNestedAndEnhancedRoutesWithVisibilityGates()
    {
        var f = await FullStatsFixtureAsync(); await ApproveStatsAsync(f, await PendingStatsAsync(f, 0, 0, 10)); await SyncStatsAsync(f, 100);
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.ConfigureServices(services => { services.RemoveAll<IHostedService>(); services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(f.Clock); });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var route = $"/Events/{f.Event.Slug}/Board/{f.Team.Slug}/Tiles/{f.Tiles[0].Id}";
        foreach (var path in new[] { route, route + "?handler=Sidebar" })
        {
            var html = await client.GetStringAsync(path);
            Assert.Contains("data-tile-activity", html, StringComparison.Ordinal);
            Assert.Contains("Tile activity contributors", html, StringComparison.Ordinal);
            Assert.Contains("Vorkath fixture", html, StringComparison.Ordinal);
            Assert.Contains("100", html, StringComparison.Ordinal);
            Assert.DoesNotContain("data-open-submission-drawer", html, StringComparison.Ordinal);
        }
        var empty = await client.GetStringAsync(route.Replace(f.Tiles[0].Id.ToString(), f.Tiles[2].Id.ToString()) + "?handler=Sidebar");
        Assert.Contains("This objective has no eligible drop activity.", empty, StringComparison.Ordinal);
        foreach (var state in new[] { "unpublished", "cancelled", "private", "import", "hidden" })
        {
            await using var setup = new ApplicationDbContext(options);
            await setup.Database.ExecuteSqlRawAsync("UPDATE events SET hidden_at = NULL, board_published = true, first_public_at = event_starts_at, state = 'Live', slug = 'stats-event'");
            if (state == "unpublished") await setup.Database.ExecuteSqlRawAsync("UPDATE events SET board_published = false");
            if (state == "hidden")
            {
                var ev = await setup.Events.SingleAsync();
                ev.EndEvent(f.Clock.GetUtcNow()); ev.Hide(f.Admin.Id, f.Clock.GetUtcNow(), f.Event.Name, "Controlled hidden fixture");
                await setup.SaveChangesAsync();
            }
            if (state == "cancelled") await setup.Database.ExecuteSqlRawAsync("UPDATE events SET state = 'Cancelled'");
            if (state == "private") await setup.Database.ExecuteSqlRawAsync("UPDATE events SET first_public_at = NULL");
            if (state == "import") await setup.Database.ExecuteSqlRawAsync("UPDATE events SET slug = 'det-store-danske-sommerbingo-2026'");
            foreach (var path in new[] { route, route + "?handler=Sidebar" })
            {
                using var response = await client.GetAsync(state == "import" ? path.Replace("stats-event", "det-store-danske-sommerbingo-2026") : path);
                var html = await response.Content.ReadAsStringAsync();
                if (state is "private" or "import" or "unpublished") Assert.DoesNotContain("Tile activity contributors", html, StringComparison.Ordinal);
                else Assert.DoesNotContain("data-tile-activity", html, StringComparison.Ordinal);
            }
        }
    }

    private async Task<StatsTileActivity> ReadTileActivityAsync(FullStatsFixture f, int tile = 0)
    {
        await using var db = new ApplicationDbContext(options);
        var detail = await new PublicBoardService(db, f.Clock).GetTileAsync(f.Event.Slug, f.Team.Slug, f.Tiles[tile].Id);
        return Assert.IsType<StatsTileActivity>(detail!.Activity);
    }
}
