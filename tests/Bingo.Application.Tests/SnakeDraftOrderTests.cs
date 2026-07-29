using Bingo.Application.Teams;
using Bingo.Domain.Teams;

namespace Bingo.Application.Tests;

public sealed class SnakeDraftOrderTests
{
    [Theory]
    [InlineData(2, 8)]
    [InlineData(3, 12)]
    [InlineData(6, 24)]
    public void SnakeOrderReversesOnEveryRound(int teamCount, int pickCount)
    {
        var teams = Enumerable.Range(0, teamCount).Select(_ => Guid.NewGuid()).ToList();
        for (var pick = 0; pick < pickCount; pick++)
        {
            var turn = SnakeDraftOrder.GetTurn(pick, teams);
            var round = pick / teamCount;
            var offset = pick % teamCount;
            var expected = round % 2 == 0 ? teams[offset] : teams[teamCount - offset - 1];
            Assert.Equal(expected, turn.TeamId);
            Assert.Equal(round + 1, turn.RoundNumber);
            Assert.Equal(pick + 1, turn.PickNumber);
        }
    }

    [Fact]
    public void RoundBoundaryGivesLastTeamTwoConsecutivePicks()
    {
        var teams = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        Assert.Equal(teams[2], SnakeDraftOrder.GetTurn(2, teams).TeamId);
        Assert.Equal(teams[2], SnakeDraftOrder.GetTurn(3, teams).TeamId);
    }

    [Fact]
    public void FullTeamsAreSkippedWithoutBreakingSnakeDirection()
    {
        var teams = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var sizes = new Dictionary<Guid, int> { [teams[0]] = 2, [teams[1]] = 1, [teams[2]] = 0 };
        var turn = SnakeDraftOrder.GetNextEligibleTurn([teams[0], teams[1]], teams, sizes, 2);
        Assert.Equal(teams[2], turn!.TeamId);
        Assert.Equal(3, turn.PickNumber);
        Assert.Equal(1, turn.RoundNumber);
    }

    [Fact]
    public void NoTurnRemainsWhenEveryTeamIsFull()
    {
        var teams = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var sizes = teams.ToDictionary(team => team, _ => 2);
        Assert.Null(SnakeDraftOrder.GetNextEligibleTurn([teams[0], teams[1]], teams, sizes, 2));
    }

    [Fact]
    public void UnequalCaptainSeatsCatchUpThenExhaustEveryDerivedSeat()
    {
        var teams = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var sizes = new Dictionary<Guid, int> { [teams[0]] = 2, [teams[1]] = 1, [teams[2]] = 1 };
        var distribution = DraftRosterDistribution.Derive(8, teams.Length);
        var first = SnakeDraftOrder.GetNextEligibleTurn([], teams, sizes, distribution);
        var final = SnakeDraftOrder.ProjectFinalRosterSizes([], teams, sizes, distribution);
        Assert.NotNull(first);
        Assert.NotEqual(teams[0], first!.TeamId);
        Assert.Equal(8, final.Values.Sum());
        Assert.Equal(1, final.Values.Max() - final.Values.Min());
    }
}
