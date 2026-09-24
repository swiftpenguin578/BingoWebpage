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
        var contributions = tiles.Select((tile, index) => new ProgressContribution(Guid.NewGuid(), tile.Requirements[0].Id, Guid.NewGuid(), $"P{index}", 1, Start.AddMinutes(index), 1)).ToList();
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
    public void CurrentScoreTimeBreaksEqualLinesAndTilesBeforeEhbAndFormsSameRank()
    {
        var tiles = Grid(1, 3);
        var earlier = PublicProgressCalculator.Calculate(1, 3, tiles,
            [new ProgressContribution(Guid.NewGuid(), tiles[0].Requirements[0].Id, Guid.NewGuid(), "Earlier", 1, Start, 1)]);
        var later = PublicProgressCalculator.Calculate(1, 3, tiles,
            [new ProgressContribution(Guid.NewGuid(), tiles[1].Requirements[0].Id, Guid.NewGuid(), "Later", 1, Start.AddMinutes(2), 1)]);
        var sameTime = later with { CurrentScoreReachedAt = earlier.CurrentScoreReachedAt };
        var earlierTeam = new UnrankedTeamProgress(Guid.NewGuid(), "Earlier", earlier with { EhbTiebreak = 1 });
        var laterTeam = new UnrankedTeamProgress(Guid.NewGuid(), "Later", later with { EhbTiebreak = 100 });
        var tiedTeam = new UnrankedTeamProgress(Guid.NewGuid(), "Same time", sameTime with { EhbTiebreak = 1 });

        var ranked = PublicProgressCalculator.Rank([laterTeam, tiedTeam, earlierTeam]);

        Assert.Equal(["Earlier", "Same time", "Later"], ranked.Select(value => value.TeamName));
        Assert.Equal(ranked[0].Rank, ranked[1].Rank);
        Assert.True(ranked[2].Rank > ranked[1].Rank);
    }

    [Fact]
    public void MoreLinesOrTilesOutrankEarlierCurrentScoreTime()
    {
        var late = Start.AddHours(3);
        var early = Start.AddHours(1);
        var baseline = new CalculatedBoardProgress([], 1, [], [], false, null, 500, []) { CurrentScoreReachedAt = late };
        var earlierTime = baseline with { CurrentScoreReachedAt = early, EhbTiebreak = 1 };
        var moreTiles = baseline with { CompletedTiles = 2, CurrentScoreReachedAt = late, EhbTiebreak = 1 };
        var moreLines = baseline with { CompletedRows = [0], CurrentScoreReachedAt = late, EhbTiebreak = 1 };

        var ranked = PublicProgressCalculator.Rank([
            new(Guid.NewGuid(), "Time", earlierTime),
            new(Guid.NewGuid(), "Tiles", moreTiles),
            new(Guid.NewGuid(), "Lines", moreLines)]);

        Assert.Equal(["Lines", "Tiles", "Time"], ranked.Select(value => value.TeamName));
    }

    [Fact]
    public void CurrentScoreTimeIsNullForNoCompletedTilesAndEqualsBoardFinishForCompleteBoard()
    {
        var tiles = Grid(1, 2);
        var empty = PublicProgressCalculator.Calculate(1, 2, tiles, []);
        var complete = PublicProgressCalculator.Calculate(1, 2, tiles,
            Contributions(tiles.Select(value => value.Requirements[0].Id).ToHashSet()));

        Assert.Null(empty.CurrentScoreReachedAt);
        Assert.Equal(complete.BoardCompletedAt, complete.CurrentScoreReachedAt);
    }

    [Fact]
    public void EarlierCompleteBoardFinishRemainsAheadOfLaterFinishRegardlessOfEhb()
    {
        var tiles = Grid(1, 1);
        var earlier = PublicProgressCalculator.Calculate(1, 1, tiles,
            [new ProgressContribution(Guid.NewGuid(), tiles[0].Requirements[0].Id, Guid.NewGuid(), "Earlier", 1, Start, 1)]);
        var later = PublicProgressCalculator.Calculate(1, 1, tiles,
            [new ProgressContribution(Guid.NewGuid(), tiles[0].Requirements[0].Id, Guid.NewGuid(), "Later", 1, Start.AddMinutes(1), 1)]);

        var ranked = PublicProgressCalculator.Rank([
            new(Guid.NewGuid(), "Later", later with { EhbTiebreak = 100 }),
            new(Guid.NewGuid(), "Earlier", earlier with { EhbTiebreak = 1 })]);

        Assert.Equal(["Earlier", "Later"], ranked.Select(value => value.TeamName));
        Assert.True(ranked[0].Progress.BoardComplete);
        Assert.Equal(ranked[0].Progress.BoardCompletedAt, ranked[0].Progress.CurrentScoreReachedAt);
    }

    [Fact]
    public void EffectiveCorrectedFinishIsAuthoritativeWhenCompletedTeamsTie()
    {
        var corrected = Start.AddMinutes(20);
        var rawEarlier = Start.AddHours(1);
        var rawLater = Start.AddHours(2);
        CalculatedBoardProgress Complete(DateTimeOffset finish, DateTimeOffset scoreTime) =>
            new([], 1, [], [], true, finish, 10, []) { CurrentScoreReachedAt = scoreTime };

        var equalA = new UnrankedTeamProgress(Guid.NewGuid(), "Equal A", Complete(corrected, rawEarlier));
        var equalB = new UnrankedTeamProgress(Guid.NewGuid(), "Equal B", Complete(corrected, rawLater));
        var later = new UnrankedTeamProgress(Guid.NewGuid(), "Later", Complete(corrected.AddMinutes(1), rawEarlier));

        var ranked = PublicProgressCalculator.Rank([equalB, later, equalA]);

        Assert.Equal(["Equal A", "Equal B", "Later"], ranked.Select(value => value.TeamName));
        Assert.Equal(ranked[0].Rank, ranked[1].Rank);
        Assert.True(ranked[2].Rank > ranked[1].Rank);
    }

    [Fact]
    public void NullCurrentScoreTimeFallsThroughToEhbAndEqualValuesRemainTied()
    {
        var empty = PublicProgressCalculator.Calculate(1, 1, Grid(1, 1), []);
        var ranked = PublicProgressCalculator.Rank([
            new(Guid.NewGuid(), "EHB tie one", empty with { EhbTiebreak = 2 }),
            new(Guid.NewGuid(), "EHB lower", empty with { EhbTiebreak = 1 }),
            new(Guid.NewGuid(), "EHB tie two", empty with { EhbTiebreak = 2 })]);

        Assert.Null(ranked[0].Progress.CurrentScoreReachedAt);
        Assert.Equal(["EHB tie one", "EHB tie two", "EHB lower"], ranked.Select(value => value.TeamName));
        Assert.Equal(ranked[0].Rank, ranked[1].Rank);
        Assert.True(ranked[2].Rank > ranked[1].Rank);
    }

    [Fact]
    public void CompletionFactsUseLatestRequiredObjectiveAndRespectDuplicateCaps()
    {
        var firstRequirement = new ProgressRequirementDefinition(Guid.NewGuid(), 0, 2);
        var secondRequirement = new ProgressRequirementDefinition(Guid.NewGuid(), 1, 1);
        var cappedRequirement = new ProgressRequirementDefinition(Guid.NewGuid(), 0, 2, DuplicatesAllowed: false);
        var completeTileId = Guid.NewGuid();
        var cappedTileId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var tiles = new[]
        {
            new ProgressTileDefinition(completeTileId, 0, 0, 1, [firstRequirement, secondRequirement]),
            new ProgressTileDefinition(cappedTileId, 0, 1, 1, [cappedRequirement])
        };
        var firstSubmission = Guid.NewGuid();
        var crossingSubmission = Guid.NewGuid();
        var contributions = new[]
        {
            new ProgressContribution(Guid.NewGuid(), firstRequirement.Id, Guid.NewGuid(), "One", 1, Start, 0, SubmissionId: firstSubmission),
            new ProgressContribution(Guid.NewGuid(), firstRequirement.Id, Guid.NewGuid(), "Two", 1, Start.AddMinutes(1), 0, SubmissionId: crossingSubmission),
            new ProgressContribution(Guid.NewGuid(), secondRequirement.Id, Guid.NewGuid(), "Three", 1, Start.AddMinutes(2), 0, SubmissionId: Guid.NewGuid()),
            new ProgressContribution(Guid.NewGuid(), cappedRequirement.Id, Guid.NewGuid(), "Alias A", 1, Start, 0, itemId, Guid.NewGuid(), 1),
            new ProgressContribution(Guid.NewGuid(), cappedRequirement.Id, Guid.NewGuid(), "Alias B", 1, Start.AddMinutes(1), 0, itemId, Guid.NewGuid(), 1)
        };

        var facts = PublicProgressCalculator.CalculateTileCompletionFacts(tiles, contributions).ToDictionary(value => value.TileId);

        Assert.True(facts[completeTileId].Complete);
        Assert.Equal(Start.AddMinutes(2), facts[completeTileId].CompletedAt);
        Assert.Equal(3, facts[completeTileId].QualifyingContributions.Count);
        Assert.Contains(facts[completeTileId].QualifyingContributions, value => value.SubmissionId == crossingSubmission);
        Assert.False(facts[cappedTileId].Complete);
        Assert.Null(facts[cappedTileId].CompletedAt);
        Assert.Empty(facts[cappedTileId].QualifyingContributions);
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
    public void ApprovedPlayerContributionCountsForTeamAndLeaderboardIdentity()
    {
        var tile = Grid(1, 1)[0];
        var contribution = new ProgressContribution(Guid.NewGuid(), tile.Requirements[0].Id, Guid.NewGuid(), "Secret", 1, Start, 4);
        var result = PublicProgressCalculator.Calculate(1, 1, [tile], [contribution]);

        Assert.True(result.BoardComplete);
        Assert.Equal(4, result.EhbTiebreak);
        Assert.Equal("Secret", Assert.Single(result.Players).PlayerName);
    }

    [Fact]
    public void PlayerContributionsWithMultipleCreditedNamesAggregateByParticipant()
    {
        var tile = Grid(1, 1)[0];
        var playerId = Guid.NewGuid();
        var contributions = new[]
        {
            new ProgressContribution(Guid.NewGuid(), tile.Requirements[0].Id, playerId, "Main", 2, Start, 3),
            new ProgressContribution(Guid.NewGuid(), tile.Requirements[0].Id, playerId, "Alt", 1, Start.AddMinutes(1), 2)
        };

        var result = PublicProgressCalculator.Calculate(1, 1, [tile], contributions);

        var player = Assert.Single(result.Players);
        Assert.Equal(playerId, player.PlayerId);
        Assert.Equal("Main", player.PlayerName);
        Assert.Equal(5, player.EstimatedEhb);
        Assert.Equal(3, player.ApprovedContribution);
        Assert.Equal(2, player.ApprovedSubmissions);
    }

    [Fact]
    public void RetainedAliasesShareAnItemCapButSiblingRequirementsRemainIndependent()
    {
        var sharedItem = Guid.NewGuid();
        var firstRequirement = new ProgressRequirementDefinition(Guid.NewGuid(), 0, 2, false);
        var siblingRequirement = new ProgressRequirementDefinition(Guid.NewGuid(), 0, 1, false);
        var tiles = new[]
        {
            new ProgressTileDefinition(Guid.NewGuid(), 0, 0, 1, [firstRequirement]),
            new ProgressTileDefinition(Guid.NewGuid(), 0, 1, 1, [siblingRequirement])
        };
        var contributions = new[]
        {
            new ProgressContribution(Guid.NewGuid(), firstRequirement.Id, Guid.NewGuid(), "Alias A", 1, Start, 1, sharedItem, Guid.NewGuid(), 1),
            new ProgressContribution(Guid.NewGuid(), firstRequirement.Id, Guid.NewGuid(), "Alias B", 1, Start.AddMinutes(1), 1, sharedItem, Guid.NewGuid(), 1),
            new ProgressContribution(Guid.NewGuid(), siblingRequirement.Id, Guid.NewGuid(), "Sibling", 1, Start.AddMinutes(2), 1, sharedItem, Guid.NewGuid(), 1)
        };

        var result = PublicProgressCalculator.Calculate(1, 2, tiles, contributions);

        Assert.Equal(1, result.Tiles[0].Approved);
        Assert.False(result.Tiles[0].Complete);
        Assert.Equal(1, result.Tiles[1].Approved);
        Assert.True(result.Tiles[1].Complete);
        Assert.Equal(2, result.EhbTiebreak);
        Assert.Equal(2, result.Players.Sum(value => value.ApprovedContribution));
    }

    private static List<ProgressTileDefinition> Grid(int rows, int columns) =>
        Enumerable.Range(0, rows * columns).Select(index =>
        {
            var tileId = Guid.NewGuid();
            return new ProgressTileDefinition(tileId, index / columns, index % columns, 1, [new ProgressRequirementDefinition(Guid.NewGuid(), 1, 1)]);
        }).ToList();

    private static List<ProgressContribution> Contributions(IReadOnlySet<Guid> requirementIds) =>
        requirementIds.Select((id, index) => new ProgressContribution(Guid.NewGuid(), id, Guid.NewGuid(), $"Player {index}", 1, Start.AddMinutes(index), 1)).ToList();
}
