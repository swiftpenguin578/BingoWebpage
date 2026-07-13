using Bingo.Application.Boards;

namespace Bingo.Application.Tests;

public sealed class PublicProgressCalculatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CenterTileCountsOnceButCompletesItsRowAndColumn()
    {
        var tiles = Grid(3, 3);
        var center = tiles.Single(value => value.Row == 1 && value.Column == 1);
        var completeIds = tiles.Where(value => value.Row == 1 || value.Column == 1).Select(value => value.Requirements[0].Id).ToHashSet();
        var result = PublicProgressCalculator.Calculate(3, 3, tiles, Contributions(completeIds));

        Assert.Equal(5, result.CompletedTiles);
        Assert.Equal([1], result.CompletedRows);
        Assert.Equal([1], result.CompletedColumns);
        Assert.Contains(result.Tiles, value => value.Id == center.Id && value.Complete);
    }

    [Fact]
    public void OverlappingLinesCountSeparatelyAndDiagonalsDoNotCount()
    {
        var tiles = Grid(3, 3);
        var completeIds = tiles.Where(value => value.Row == 0 || value.Column == 0 || value.Row == value.Column).Select(value => value.Requirements[0].Id).ToHashSet();
        var result = PublicProgressCalculator.Calculate(3, 3, tiles, Contributions(completeIds));

        Assert.Single(result.CompletedRows);
        Assert.Single(result.CompletedColumns);
        Assert.Equal(2, result.CompletedRows.Count + result.CompletedColumns.Count);
    }

    [Fact]
    public void FullBoardCompletionUsesLatestImmutableSubmissionTime()
    {
        var tiles = Grid(2, 2);
        var contributions = tiles.Select((tile, index) => new ProgressContribution(Guid.NewGuid(), tile.Requirements[0].Id, Guid.NewGuid(), $"P{index}", 1, Start.AddMinutes(index), 1, false)).ToList();
        var result = PublicProgressCalculator.Calculate(2, 2, tiles, contributions);

        Assert.True(result.BoardComplete);
        Assert.Equal(Start.AddMinutes(3), result.BoardCompletedAt);
    }

    [Fact]
    public void FinisherOutranksNonFinisherThenLinesTilesAndEhbBreakTies()
    {
        var grid = Grid(1, 1);
        var empty = PublicProgressCalculator.Calculate(1, 1, grid, []);
        var finisher = PublicProgressCalculator.Calculate(1, 1, grid, Contributions(new HashSet<Guid> { grid[0].Requirements[0].Id }));
        var lineLeader = empty with { CompletedRows = [0], CompletedTiles = 1, EhbTiebreak = 2 };
        var tileLeader = empty with { CompletedTiles = 1, EhbTiebreak = 10 };
        var ranked = PublicProgressCalculator.Rank([
            new(Guid.NewGuid(), "EHB", empty with { EhbTiebreak = 20 }),
            new(Guid.NewGuid(), "Tiles", tileLeader),
            new(Guid.NewGuid(), "Lines", lineLeader),
            new(Guid.NewGuid(), "Finisher", finisher)]);

        Assert.Equal(["Finisher", "Lines", "Tiles", "EHB"], ranked.Select(value => value.TeamName));
    }

    [Fact]
    public void ReversalInputRemovesLineAndFullBoardWithoutCachedState()
    {
        var tiles = Grid(1, 2);
        var full = PublicProgressCalculator.Calculate(1, 2, tiles, Contributions(tiles.Select(value => value.Requirements[0].Id).ToHashSet()));
        var reversed = PublicProgressCalculator.Calculate(1, 2, tiles, Contributions(new HashSet<Guid> { tiles[0].Requirements[0].Id }));

        Assert.True(full.BoardComplete);
        Assert.Single(full.CompletedRows);
        Assert.False(reversed.BoardComplete);
        Assert.Empty(reversed.CompletedRows);
    }

    [Fact]
    public void HiddenPlayerContributionStillCountsForTeamButNotLeaderboardIdentity()
    {
        var tile = Grid(1, 1)[0];
        var contribution = new ProgressContribution(Guid.NewGuid(), tile.Requirements[0].Id, Guid.NewGuid(), "Secret", 1, Start, 4, true);
        var result = PublicProgressCalculator.Calculate(1, 1, [tile], [contribution]);

        Assert.True(result.BoardComplete);
        Assert.Equal(4, result.EhbTiebreak);
        Assert.Empty(result.Players);
    }

    private static List<ProgressTileDefinition> Grid(int rows, int columns) =>
        Enumerable.Range(0, rows * columns).Select(index =>
        {
            var tileId = Guid.NewGuid();
            return new ProgressTileDefinition(tileId, index / columns, index % columns, 1, [new ProgressRequirementDefinition(Guid.NewGuid(), 1, 1)]);
        }).ToList();

    private static List<ProgressContribution> Contributions(IReadOnlySet<Guid> requirementIds) =>
        requirementIds.Select((id, index) => new ProgressContribution(Guid.NewGuid(), id, Guid.NewGuid(), $"Player {index}", 1, Start.AddMinutes(index), 1, false)).ToList();
}
