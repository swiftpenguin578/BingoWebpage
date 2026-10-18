using System.Data;
using System.Text.Json;
using Bingo.Application.Stats;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Stats;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

// Brief 180: drops in one roll group with different roll counts (production Zulrah and Grotesque Guardians:
// tertiaries at 1 roll, uniques at 2 rolls, all in roll group "default") are split by roll count and each
// sub-group is calculated as its own roll group. They must not make every player's Luck Incomplete.
public sealed partial class Slice10Pass102CompetitionSynchronizationTests
{
    [Fact]
    public async Task LuckSplitsARollGroupWithMixedRollCountsInsteadOfMarkingEveryPlayerIncomplete()
    {
        // Three bosses with six outcomes each: boss 0 is shaped like Zulrah (two 1-roll tertiaries and four
        // 2-roll uniques), boss 1 like Grotesque Guardians (two 1-roll and four 2-roll drops) and boss 2 is a
        // normal boss whose drops all share one roll count.
        var f = await LuckSizeFixtureAsync(teams: 2, playersPerTeam: 2, bosses: 3, outcomes: 18, dimension: 3);
        var twoRollItems = Enumerable.Range(0, 12).Where(i => i % 6 >= 2).Select(i => f.Stats.Items[i].Id).ToArray();
        await using (var db = new ApplicationDbContext(options))
            Assert.True(await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE board_approval_requirement_drop_snapshots SET rolls_per_completion = 2 WHERE item_id_snapshot = ANY({twoRollItems})") >= twoRollItems.Length);
        await ApproveLuckSizeDropsAsync(f, 6);
        await SyncLuckSizeAsync(f);

        StatsLuck full;
        await using (var db = new ApplicationDbContext(options))
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
            full = Assert.IsType<StatsLuck>(await PublicStatsService.CalculateAcceptedFetchLuckAsync(db, f.Clock, f.Event.Id));
            await tx.CommitAsync();
        }
        // The frozen mechanics really are mixed inside roll group "default".
        foreach (var boss in f.Bosses.Take(2))
        {
            var mechanics = full.Sources.Where(x => x.BossId == boss.Id).ToArray();
            Assert.Equal(6, mechanics.Length);
            Assert.All(mechanics, x => Assert.Equal("default", x.RollGroup));
            Assert.Equal(2, mechanics.Count(x => x.Rolls == 1));
            Assert.Equal(4, mechanics.Count(x => x.Rolls == 2));
        }
        Assert.All(full.Sources.Where(x => x.BossId == f.Bosses[2].Id), x => Assert.Equal(1, x.Rolls));

        Assert.Equal(StatsLuckStatus.Calculated, full.Result.Status);
        Assert.NotNull(full.Result.Percentage);
        Assert.Equal(2, full.Teams.Count);
        Assert.All(full.Teams, team =>
        {
            Assert.Equal(StatsLuckStatus.Calculated, team.Result.Status);
            Assert.NotNull(team.Result.Percentage);
            Assert.Equal(2, team.Players.Count);
            Assert.All(team.Players, player =>
            {
                Assert.Equal(StatsLuckStatus.Calculated, player.Result.Status);
                Assert.NotNull(player.Result.Percentage);
                Assert.NotNull(player.Result.KcDifference);
                Assert.Equal(3, player.Result.Activities!.Count);
                Assert.All(player.Result.Activities, activity =>
                {
                    Assert.Equal(StatsLuckStatus.Calculated, activity.Status);
                    Assert.NotNull(activity.Percentage);
                });
            });
        });
        // Tile Luck uses the same rule.
        Assert.All(full.Tiles!.Where(t => t.HasDropOutcomes).SelectMany(t => t.Teams), team => Assert.Equal(StatsLuckStatus.Calculated, team.Result.Status));

        // The published checkpoint shows the same result.
        var stats = await ReadStatsAsync(f.Stats);
        Assert.Equal(StatsLuckStatus.Calculated, stats.Luck.Result.Status);
        Assert.Equal(full.Result.Percentage, stats.Luck.Result.Percentage);
    }
}

// Brief 180: the scoring rule itself, on retained observation rows.
public sealed class LuckMixedRollCountScoreTests
{
    private static readonly Guid Character = Guid.NewGuid();
    private static readonly Guid Zulrah = Guid.NewGuid();
    private static readonly Guid Guardians = Guid.NewGuid();
    private static readonly Guid Normal = Guid.NewGuid();

    private sealed record Drop(Guid Boss, string Metric, decimal Probability, int Rolls, int Received, int Kc, Guid Id);

    private static Drop D(Guid boss, string metric, decimal probability, int rolls, int received, int kc) =>
        new(boss, metric, probability, rolls, received, kc, Guid.NewGuid());

    // Production shapes: tertiaries at 1 roll and uniques at 2 rolls, all in roll group "default".
    private static readonly Drop[] ProductionShape =
    [
        D(Zulrah, "zulrah", .0003m, 1, 0, 600), D(Zulrah, "zulrah", .00025m, 1, 0, 600),
        D(Zulrah, "zulrah", .002m, 2, 1, 600), D(Zulrah, "zulrah", .002m, 2, 0, 600),
        D(Zulrah, "zulrah", .002m, 2, 2, 600), D(Zulrah, "zulrah", .002m, 2, 0, 600),
        D(Guardians, "grotesque_guardians", .001m, 1, 0, 400), D(Guardians, "grotesque_guardians", .0003m, 1, 0, 400),
        D(Guardians, "grotesque_guardians", .002m, 2, 1, 400), D(Guardians, "grotesque_guardians", .001m, 2, 0, 400),
        D(Guardians, "grotesque_guardians", .001m, 2, 0, 400), D(Guardians, "grotesque_guardians", .0005m, 2, 0, 400),
        D(Normal, "vorkath", .0002m, 2, 0, 300), D(Normal, "vorkath", .0001m, 2, 1, 300), D(Normal, "vorkath", .001m, 2, 0, 300)
    ];

    private static (StatsLuckResult Result, StatsLuckCharacterSource[] Rows, StatsLuckSource[] Sources) Build(IEnumerable<Drop> drops, Func<Drop, string> rollGroup)
    {
        var list = drops.ToArray();
        var sources = list.Select(x => new StatsLuckSource(x.Id, x.Id, x.Boss, x.Metric, x.Probability, x.Rolls, null, null,
            new StatsItemIdentity(x.Id, "Item", null), x.Metric, DropProbabilityScope.Participant, 1, rollGroup(x), null)).ToArray();
        var rows = list.Select(x => new StatsLuckCharacterSource(Character, x.Id, x.Id, x.Received, x.Kc, x.Kc * x.Probability * x.Rolls,
            StatsLuckStatus.Calculated, false, false, null, null)).ToArray();
        var result = new StatsLuckResult(rows.Sum(x => x.Received), rows.Sum(x => x.Expected!.Value), null, StatsLuckStatus.Calculated, false, false);
        return (result, rows, sources);
    }

    private static StatsLuckResult Score(IEnumerable<Drop> drops, Func<Drop, string> rollGroup)
    {
        var (result, rows, sources) = Build(drops, rollGroup);
        return PublicStatsService.Score(result, rows, sources);
    }

    private static decimal? Reference(IEnumerable<Drop> drops) => LuckScoreCalculator.Calculate(
        drops.GroupBy(x => (x.Boss, x.Rolls)).Select(g => new LuckBinomialComponent(g.First().Kc * g.Key.Rolls, g.Sum(x => x.Probability))).ToArray(),
        drops.Sum(x => x.Received));

    [Fact]
    public void MixedRollCountsInOneRollGroupScoreExactlyLikeSeparateRollGroupsPerRollCount()
    {
        var mixed = Score(ProductionShape, _ => "default");
        var separated = Score(ProductionShape, x => "default-" + x.Rolls + "-rolls");

        Assert.Equal(StatsLuckStatus.Calculated, mixed.Status);
        Assert.NotNull(mixed.Percentage);
        Assert.NotNull(mixed.KcDifference);
        Assert.Equal(Reference(ProductionShape), mixed.Percentage);
        Assert.Equal(3, mixed.Activities!.Count);
        Assert.All(mixed.Activities, x => { Assert.Equal(StatsLuckStatus.Calculated, x.Status); Assert.NotNull(x.Percentage); });
        Assert.Equal(JsonSerializer.Serialize(separated), JsonSerializer.Serialize(mixed));
    }

    [Fact]
    public void AConsistentRollGroupIsStillOneComponent()
    {
        var consistent = ProductionShape.Where(x => x.Boss == Normal).ToArray();
        var scored = Score(consistent, _ => "default");

        Assert.Equal(StatsLuckStatus.Calculated, scored.Status);
        // One binomial over KC x rolls with the group's combined chance, as before brief 180.
        Assert.Equal(LuckScoreCalculator.Calculate([new LuckBinomialComponent(300 * 2, .0013m)], 1), scored.Percentage);
        Assert.Equal(scored.Percentage, Assert.Single(scored.Activities!).Percentage);
    }

    [Fact]
    public void ASubGroupWhoseCombinedChanceExceedsOneLeavesOnlyThatBossIncomplete()
    {
        Drop[] impossible = [D(Zulrah, "zulrah", .6m, 2, 0, 100), D(Zulrah, "zulrah", .6m, 2, 0, 100), D(Zulrah, "zulrah", .1m, 1, 0, 100)];
        var normal = ProductionShape.Where(x => x.Boss == Normal).ToArray();
        var scored = Score(impossible.Concat(normal), _ => "default");

        // The impossible boss keeps its own Incomplete line and is left out; the player is scored from the rest.
        var zulrah = scored.Activities!.Single(x => x.Metric == "zulrah");
        Assert.Equal(StatsLuckStatus.Incomplete, zulrah.Status);
        Assert.Null(zulrah.Percentage);
        Assert.Equal(StatsLuckStatus.Calculated, scored.Status);
        Assert.Equal(Score(normal, _ => "default").Percentage, scored.Percentage);
        Assert.Equal(normal.Sum(x => x.Kc * x.Probability * x.Rolls), scored.Expected);
        Assert.Equal(Reference(normal), scored.Percentage);
    }
}
