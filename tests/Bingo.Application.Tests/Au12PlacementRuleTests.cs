using Bingo.Application.Boards;
using Bingo.Domain.Events;

namespace Bingo.Application.Tests;

public sealed class Au12PlacementRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
    private static UnrankedTeamProgress Team(string name, decimal ehb, DateTimeOffset? score) =>
        new(Guid.NewGuid(), name, new([], 1, [], [], false, null, ehb, []) { CurrentScoreReachedAt = score });

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

}
