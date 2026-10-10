using Bingo.Application.Boards;
using Bingo.Domain.Events;

namespace Bingo.Application.Tests;

public sealed class Au12PlacementRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
    private static UnrankedTeamProgress Team(string name, decimal ehb, DateTimeOffset? score) =>
        new(Guid.NewGuid(), name, new([], 1, [], [], false, null, ehb, []) { CurrentScoreReachedAt = score, CompletedTileEhb = ehb });

    // 2x2 board: tiles of 5, 5, 20 and 1 expected EHB; each tile has one requirement with target 10.
    private static readonly ProgressTileDefinition[] Tiles =
    [
        Tile(0, 0, 5m), Tile(0, 1, 5m), Tile(1, 0, 20m), Tile(1, 1, 1m)
    ];
    private static ProgressTileDefinition Tile(int row, int column, decimal ehb) =>
        new(Guid.NewGuid(), row, column, ehb, [new ProgressRequirementDefinition(Guid.NewGuid(), 0, 10)]);
    private static ProgressContribution Credit(ProgressTileDefinition tile, int amount, DateTimeOffset at) =>
        new(Guid.NewGuid(), tile.Requirements[0].Id, Guid.NewGuid(), "Player", amount, at, tile.EstimatedEhb * amount / 10m);
    private static UnrankedTeamProgress Calculated(string name, params ProgressContribution[] contributions) =>
        new(Guid.NewGuid(), name, PublicProgressCalculator.Calculate(2, 2, Tiles, contributions));

    [Theory]
    [InlineData(PlacementRule.LegacyScoreTimeThenEhb, "Early")]
    [InlineData(PlacementRule.CreditedEhbThenScoreTime, "Effort")]
    public void EventRuleControlsOnlyLastTwoInputs(PlacementRule rule, string winner)
    {
        var early = Team("Early", 1m, Now);
        var effort = Team("Effort", 2m, Now.AddSeconds(1));
        Assert.Equal(winner, PublicProgressCalculator.Rank([early, effort], rule)[0].TeamName);
        foreach (var stronger in new[]
        {
            early with { Progress = early.Progress with { BoardComplete = true, BoardCompletedAt = Now.AddDays(1) } },
            early with { Progress = early.Progress with { CompletedRows = [0] } },
            early with { Progress = early.Progress with { CompletedTiles = 2 } }
        }) Assert.Equal("Early", PublicProgressCalculator.Rank([effort, stronger], rule)[0].TeamName);
        var finished = early with { Progress = early.Progress with { BoardComplete = true, BoardCompletedAt = Now } };
        var laterFinished = effort with { Progress = effort.Progress with { BoardComplete = true, BoardCompletedAt = Now.AddSeconds(1) } };
        Assert.Equal("Early", PublicProgressCalculator.Rank([laterFinished, finished], rule)[0].TeamName);
    }

    [Theory]
    [InlineData(PlacementRule.LegacyScoreTimeThenEhb)]
    [InlineData(PlacementRule.CreditedEhbThenScoreTime)]
    public void EqualEhbUsesScoreTimeAndExactNullTiesShareCompetitionRanks(PlacementRule rule)
    {
        var teams = new[] { Team("Z", 3m, null), Team("A", 3m, null), Team("Early", 3m, Now) };
        var ranked = PublicProgressCalculator.Rank(teams, rule);
        Assert.Equal<string>(["Early", "A", "Z"], ranked.Select(x => x.TeamName));
        Assert.Equal<int>([1, 2, 2], ranked.Select(x => x.Rank));
        Assert.Equal<int>([1, 1, 3], PublicProgressCalculator.Rank([Team("Z", 3m, Now), Team("A", 3m, Now), Team("Late", 3m, Now.AddSeconds(1))], rule).Select(x => x.Rank));
    }
    [Theory]
    [InlineData(PlacementRule.CreditedEhbThenScoreTime, false)]
    [InlineData(PlacementRule.CreditedEhbThenScoreTime, true)]
    [InlineData(PlacementRule.LegacyScoreTimeThenEhb, false)]
    [InlineData(PlacementRule.LegacyScoreTimeThenEhb, true)]
    public void FractionalCreditUsesFourDecimalsOnlyForNewRule(PlacementRule rule, bool sameTime)
    {
        var a = Team("A", 10m / 3m + 10m / 3m, Now);
        var b = Team("B", 10m * 2m / 3m, sameTime ? Now : Now.AddSeconds(1));
        Assert.True(a.Progress.EhbTiebreak < b.Progress.EhbTiebreak);
        var ranked = PublicProgressCalculator.Rank([b, a], rule);
        var shared = sameTime && rule == PlacementRule.CreditedEhbThenScoreTime;
        Assert.Equal(sameTime && !shared ? "B" : "A", ranked[0].TeamName);
        Assert.Equal<int>(shared ? [1, 1] : [1, 2], ranked.Select(x => x.Rank));
    }

    [Fact]
    public void CompletedTileEhbCountsOnlyCompleteTilesAndKeepsProportionalDropEhb()
    {
        var team = Calculated("A", Credit(Tiles[0], 10, Now), Credit(Tiles[2], 9, Now));
        Assert.Equal(5m, team.Progress.CompletedTileEhb);
        Assert.Equal(23m, team.Progress.EhbTiebreak);
        Assert.Equal(5m, PublicProgressCalculator.PlacementEhb(team.Progress, PlacementRule.CreditedEhbThenScoreTime));
        Assert.Equal(23m, PublicProgressCalculator.PlacementEhb(team.Progress, PlacementRule.LegacyScoreTimeThenEhb));
        Assert.Equal(0m, Calculated("None", Credit(Tiles[2], 9, Now)).Progress.CompletedTileEhb);
    }

    [Fact]
    public void ProportionalProgressDoesNotOutrankMoreCompletedTileEhb()
    {
        // Equal lines (0) and tiles (1) and equal score time. A has more proportional
        // credit (1 + 18) but less completed-tile EHB (1) than B (5).
        var a = Calculated("A", Credit(Tiles[3], 10, Now), Credit(Tiles[2], 9, Now));
        var b = Calculated("B", Credit(Tiles[0], 10, Now));
        Assert.True(a.Progress.EhbTiebreak > b.Progress.EhbTiebreak);
        Assert.Equal<string>(["B", "A"], PublicProgressCalculator.Rank([a, b], PlacementRule.CreditedEhbThenScoreTime).Select(x => x.TeamName));
        Assert.Equal<string>(["A", "B"], PublicProgressCalculator.Rank([b, a], PlacementRule.LegacyScoreTimeThenEhb).Select(x => x.TeamName));
    }

    [Fact]
    public void EqualCompletedTileEhbFallsToScoreTimeThenSharesPlace()
    {
        var a = Calculated("A", Credit(Tiles[0], 10, Now.AddSeconds(1)), Credit(Tiles[2], 9, Now));
        var b = Calculated("B", Credit(Tiles[1], 10, Now));
        var ranked = PublicProgressCalculator.Rank([a, b], PlacementRule.CreditedEhbThenScoreTime);
        Assert.Equal<string>(["B", "A"], ranked.Select(x => x.TeamName));
        Assert.Equal<int>([1, 2], ranked.Select(x => x.Rank));

        var sameTime = Calculated("A", Credit(Tiles[0], 10, Now), Credit(Tiles[2], 9, Now));
        var shared = PublicProgressCalculator.Rank([sameTime, b], PlacementRule.CreditedEhbThenScoreTime);
        Assert.Equal<int>([1, 1], shared.Select(x => x.Rank));
        var legacy = PublicProgressCalculator.Rank([b, sameTime], PlacementRule.LegacyScoreTimeThenEhb);
        Assert.Equal<string>(["A", "B"], legacy.Select(x => x.TeamName));
        Assert.Equal<int>([1, 2], legacy.Select(x => x.Rank));
    }
}
