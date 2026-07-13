namespace Bingo.Application.Boards;

public static class PublicProgressCalculator
{
    public static CalculatedBoardProgress Calculate(
        int rows,
        int columns,
        IReadOnlyList<ProgressTileDefinition> tiles,
        IReadOnlyList<ProgressContribution> contributions)
    {
        var contributionByRequirement = contributions
            .GroupBy(value => value.RequirementId)
            .ToDictionary(group => group.Key, group => group.OrderBy(value => value.SubmittedAt).ThenBy(value => value.Id).ToList());
        var tileResults = new List<CalculatedTileProgress>(tiles.Count);

        foreach (var tile in tiles.OrderBy(value => value.Row).ThenBy(value => value.Column))
        {
            var requirements = new List<CalculatedRequirementProgress>(tile.Requirements.Count);
            foreach (var requirement in tile.Requirements.OrderBy(value => value.Position))
            {
                var approved = contributionByRequirement.GetValueOrDefault(requirement.Id) ?? [];
                var total = Math.Min(requirement.Target, approved.Sum(value => value.Amount));
                DateTimeOffset? completedAt = null;
                var running = 0;
                foreach (var contribution in approved)
                {
                    running += contribution.Amount;
                    if (running >= requirement.Target)
                    {
                        completedAt = contribution.SubmittedAt;
                        break;
                    }
                }
                requirements.Add(new CalculatedRequirementProgress(requirement.Id, requirement.Target, total, total >= requirement.Target, completedAt));
            }

            var complete = requirements.Count > 0 && requirements.All(value => value.Complete);
            tileResults.Add(new CalculatedTileProgress(
                tile.Id, tile.Row, tile.Column, requirements.Sum(value => value.Approved),
                requirements.Sum(value => value.Target), complete,
                complete ? requirements.Max(value => value.CompletedAt) : null,
                tile.EstimatedEhb));
        }

        var completedRows = Enumerable.Range(0, rows)
            .Where(row => tileResults.Where(tile => tile.Row == row).Count() == columns && tileResults.Where(tile => tile.Row == row).All(tile => tile.Complete))
            .ToList();
        var completedColumns = Enumerable.Range(0, columns)
            .Where(column => tileResults.Where(tile => tile.Column == column).Count() == rows && tileResults.Where(tile => tile.Column == column).All(tile => tile.Complete))
            .ToList();
        var boardComplete = tileResults.Count == rows * columns && tileResults.All(value => value.Complete);
        var playerContributions = contributions
            .Where(value => !value.PublicPlayerHidden)
            .GroupBy(value => new { value.PlayerId, value.PlayerName })
            .Select(group => new CalculatedPlayerContribution(
                group.Key.PlayerId, group.Key.PlayerName,
                group.Sum(value => value.EstimatedEhb), group.Sum(value => value.Amount), group.Count()))
            .OrderByDescending(value => value.EstimatedEhb)
            .ThenByDescending(value => value.ApprovedContribution)
            .ThenBy(value => value.PlayerName)
            .ToList();

        return new CalculatedBoardProgress(
            tileResults,
            tileResults.Count(value => value.Complete),
            completedRows,
            completedColumns,
            boardComplete,
            boardComplete ? tileResults.Max(value => value.CompletedAt) : null,
            contributions.Sum(value => value.EstimatedEhb),
            playerContributions);
    }

    public static IReadOnlyList<RankedTeamProgress> Rank(IReadOnlyList<UnrankedTeamProgress> teams)
    {
        var ordered = teams
            .OrderByDescending(value => value.Progress.BoardComplete)
            .ThenBy(value => value.Progress.BoardComplete ? value.Progress.BoardCompletedAt : DateTimeOffset.MaxValue)
            .ThenByDescending(value => value.Progress.CompletedRows.Count + value.Progress.CompletedColumns.Count)
            .ThenByDescending(value => value.Progress.CompletedTiles)
            .ThenByDescending(value => value.Progress.EhbTiebreak)
            .ThenBy(value => value.TeamName)
            .ToList();
        var result = new List<RankedTeamProgress>(ordered.Count);
        for (var index = 0; index < ordered.Count; index++)
        {
            var current = ordered[index];
            var rank = index == 0 || !SameRank(ordered[index - 1], current) ? index + 1 : result[^1].Rank;
            result.Add(new RankedTeamProgress(current.TeamId, current.TeamName, current.Progress, rank));
        }
        return result;
    }

    private static bool SameRank(UnrankedTeamProgress left, UnrankedTeamProgress right) =>
        left.Progress.BoardComplete == right.Progress.BoardComplete &&
        left.Progress.BoardCompletedAt == right.Progress.BoardCompletedAt &&
        left.Progress.CompletedRows.Count + left.Progress.CompletedColumns.Count == right.Progress.CompletedRows.Count + right.Progress.CompletedColumns.Count &&
        left.Progress.CompletedTiles == right.Progress.CompletedTiles &&
        left.Progress.EhbTiebreak == right.Progress.EhbTiebreak;
}

public sealed record ProgressTileDefinition(Guid Id, int Row, int Column, decimal EstimatedEhb, IReadOnlyList<ProgressRequirementDefinition> Requirements);
public sealed record ProgressRequirementDefinition(Guid Id, int Position, int Target);
public sealed record ProgressContribution(Guid Id, Guid RequirementId, Guid PlayerId, string PlayerName, int Amount, DateTimeOffset SubmittedAt, decimal EstimatedEhb, bool PublicPlayerHidden);
public sealed record CalculatedRequirementProgress(Guid Id, int Target, int Approved, bool Complete, DateTimeOffset? CompletedAt);
public sealed record CalculatedTileProgress(Guid Id, int Row, int Column, int Approved, int Target, bool Complete, DateTimeOffset? CompletedAt, decimal EstimatedEhb);
public sealed record CalculatedPlayerContribution(Guid PlayerId, string PlayerName, decimal EstimatedEhb, int ApprovedContribution, int ApprovedSubmissions);
public sealed record CalculatedBoardProgress(IReadOnlyList<CalculatedTileProgress> Tiles, int CompletedTiles, IReadOnlyList<int> CompletedRows, IReadOnlyList<int> CompletedColumns, bool BoardComplete, DateTimeOffset? BoardCompletedAt, decimal EhbTiebreak, IReadOnlyList<CalculatedPlayerContribution> Players);
public sealed record UnrankedTeamProgress(Guid TeamId, string TeamName, CalculatedBoardProgress Progress);
public sealed record RankedTeamProgress(Guid TeamId, string TeamName, CalculatedBoardProgress Progress, int Rank);
