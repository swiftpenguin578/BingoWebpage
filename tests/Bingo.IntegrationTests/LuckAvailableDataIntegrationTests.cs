using System.Data;
using Bingo.Application.Stats;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Stats;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

// Brief 180 item 2 (user decision, 16 October 2026): Luck is calculated from the data we have. A boss a
// player has no KC for yet, or a boss with impossible mechanics, keeps its own line and is left out of the
// player, team and overall pools instead of making them Incomplete.
public sealed partial class Slice10Pass102CompetitionSynchronizationTests
{
    [Fact]
    public async Task LuckIsCalculatedFromAvailableDataWhenOnePlayerWaitsForKcAtOneBoss()
    {
        // Two teams of three; team 0 is players 0, 2 and 4. Bosses: items 0-2 boss 0, 3-5 boss 1, 6-8 boss 2.
        var f = await LuckSizeFixtureAsync(teams: 2, playersPerTeam: 3, bosses: 3, outcomes: 9, dimension: 3);
        // Player 0 has an approved boss-0 drop but no boss-0 KC yet; player 2 has a boss-0 drop with KC.
        await ApproveStatsAsync(f.Stats, await PendingStatsAsync(f.Stats, 0, 0, 10, itemIndex: 0));
        await ApproveStatsAsync(f.Stats, await PendingStatsAsync(f.Stats, 0, 2, 11, itemIndex: 1));
        await SyncLuckSizeAsync(f, kills: (player, boss) => player == 0 && boss == 0 ? 0 : 50 + player * 10 + boss);
        var full = await CalculateLuckAsync(f);
        var boss0 = f.Bosses[0].Id;
        var team0 = full.Teams.Single(x => x.TeamId == f.Teams[0].Id);
        var waitingPlayer = team0.Players.Single(x => x.PlayerId == f.Stats.Players[0].Id);
        var otherPlayer = team0.Players.Single(x => x.PlayerId == f.Stats.Players[2].Id);

        // The waiting rows keep their own status.
        var waitingRows = waitingPlayer.Sources.Where(x => full.Sources.Single(s => s.SourceDropId == x.SourceDropId && s.ItemId == x.ItemId).BossId == boss0).ToArray();
        Assert.Equal(3, waitingRows.Length);
        Assert.All(waitingRows, x => Assert.Equal(StatsLuckStatus.WaitingForActivityUpdate, x.Status));
        Assert.Equal(1, waitingRows.Sum(x => x.Received));

        // The player is calculated from the two bosses with KC; the waiting boss line says so on its own.
        Assert.Equal(StatsLuckStatus.Calculated, waitingPlayer.Result.Status);
        Assert.NotNull(waitingPlayer.Result.Percentage);
        Assert.NotNull(waitingPlayer.Result.KcDifference);
        Assert.Equal(0, waitingPlayer.Result.Received);
        var calculableRows = waitingPlayer.Sources.Except(waitingRows).ToArray();
        Assert.Equal(calculableRows.Sum(x => x.Expected!.Value), waitingPlayer.Result.Expected);
        var waitingLine = waitingPlayer.Result.Activities!.Single(x => x.BossId == boss0);
        Assert.Equal(StatsLuckStatus.WaitingForActivityUpdate, waitingLine.Status);
        Assert.Null(waitingLine.Percentage);
        Assert.Null(waitingLine.Kc);
        Assert.All(waitingPlayer.Result.Activities!.Where(x => x.BossId != boss0), x => Assert.Equal(StatsLuckStatus.Calculated, x.Status));
        Assert.Equal(waitingPlayer.Result.Activities!.Where(x => x.BossId != boss0).Sum(x => x.KcDifference!.Value), waitingPlayer.Result.KcDifference);

        // The team and the overall result are calculated; the team's boss-0 line pools the members with KC.
        Assert.Equal(StatsLuckStatus.Calculated, team0.Result.Status);
        Assert.NotNull(team0.Result.Percentage);
        var teamBoss0 = team0.Result.Activities!.Single(x => x.BossId == boss0);
        Assert.Equal(StatsLuckStatus.Calculated, teamBoss0.Status);
        Assert.Equal(1, teamBoss0.Received);
        Assert.Equal(2 * 50m + 20 + 40, teamBoss0.Kc);
        Assert.Equal(otherPlayer.Result.Activities!.Single(x => x.BossId == boss0).Expected + team0.Players.Single(x => x.PlayerId == f.Stats.Players[4].Id).Result.Activities!.Single(x => x.BossId == boss0).Expected, teamBoss0.Expected);
        Assert.NotNull(teamBoss0.Percentage);
        Assert.Equal(team0.Players.SelectMany(x => x.Sources).Except(waitingRows).Sum(x => x.Expected!.Value), team0.Result.Expected);
        Assert.Equal(StatsLuckStatus.Calculated, full.Result.Status);
        Assert.NotNull(full.Result.Percentage);

        // A team with every KC present pools every row, as before.
        var team1 = full.Teams.Single(x => x.TeamId == f.Teams[1].Id);
        Assert.Equal(StatsLuckStatus.Calculated, team1.Result.Status);
        Assert.Equal(team1.Players.SelectMany(x => x.Sources).Sum(x => x.Expected!.Value), team1.Result.Expected);
        Assert.Equal(team1.Players.SelectMany(x => x.Sources).Sum(x => x.Received), team1.Result.Received);

        // Tile 0 holds only boss-0 outcomes: the waiting player's tile line waits, the team tile is calculated
        // from the members with KC.
        var tile = full.Tiles!.Single(x => x.TileId == f.Stats.Tiles[0].Id).Teams.Single(x => x.TeamId == f.Teams[0].Id);
        Assert.Equal(StatsLuckStatus.WaitingForActivityUpdate, tile.Players.Single(x => x.PlayerId == f.Stats.Players[0].Id).Result.Status);
        Assert.Equal(StatsLuckStatus.Calculated, tile.Result.Status);
        Assert.NotNull(tile.Result.Percentage);
        var tileKc = Assert.Single(tile.Metrics);
        Assert.Equal(StatsLuckStatus.Calculated, tileKc.Status);
        Assert.Equal(2 * 50m + 20 + 40, tileKc.Count);

        // The published checkpoint carries the same values.
        var stats = await ReadStatsAsync(f.Stats);
        Assert.Equal(StatsLuckStatus.Calculated, stats.Luck.Result.Status);
        Assert.Equal(full.Result.Percentage, stats.Luck.Result.Percentage);
    }

    [Fact]
    public async Task LuckLeavesOnlyABossWithImpossibleMechanicsIncomplete()
    {
        var f = await LuckSizeFixtureAsync(teams: 2, playersPerTeam: 3, bosses: 3, outcomes: 9, dimension: 3);
        // Boss 1's three drops share roll group "default" and one roll count, with a combined chance of 1.8.
        var boss1Items = Enumerable.Range(3, 3).Select(i => f.Stats.Items[i].Id).ToArray();
        await using (var db = new ApplicationDbContext(options))
            Assert.True(await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE board_approval_requirement_drop_snapshots SET numeric_probability = 0.6 WHERE item_id_snapshot = ANY({boss1Items})") >= boss1Items.Length);
        await ApproveStatsAsync(f.Stats, await PendingStatsAsync(f.Stats, 0, 0, 10, itemIndex: 0));
        await SyncLuckSizeAsync(f);
        var full = await CalculateLuckAsync(f);
        var boss1 = f.Bosses[1].Id;

        Assert.All(full.Sources.Where(x => x.BossId == boss1), x => Assert.Equal(.6m, x.Probability));
        Assert.All(full.Teams.SelectMany(x => x.Players).Select(x => x.Result).Concat(full.Teams.Select(x => x.Result)).Append(full.Result), result =>
        {
            Assert.Equal(StatsLuckStatus.Calculated, result.Status);
            Assert.NotNull(result.Percentage);
            var line = result.Activities!.Single(x => x.BossId == boss1);
            Assert.Equal(StatsLuckStatus.Incomplete, line.Status);
            Assert.Null(line.Percentage);
            Assert.Null(line.Expected);
            Assert.All(result.Activities!.Where(x => x.BossId != boss1), x => Assert.Equal(StatsLuckStatus.Calculated, x.Status));
            Assert.Equal(result.Activities!.Where(x => x.BossId != boss1).Sum(x => x.Expected!.Value), result.Expected);
        });
    }

    private async Task<StatsLuck> CalculateLuckAsync(LuckSizeFixture f)
    {
        await using var db = new ApplicationDbContext(options);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        var full = Assert.IsType<StatsLuck>(await PublicStatsService.CalculateAcceptedFetchLuckAsync(db, f.Clock, f.Event.Id));
        await tx.CommitAsync();
        return full;
    }
}
