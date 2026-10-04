using Bingo.Domain.Events;
namespace Bingo.Application.Boards;

public static class PublicProgressCalculator
{
    public static CalculatedBoardProgress Calculate(
        int rows,
        int columns,
        IReadOnlyList<ProgressTileDefinition> tiles,
        IReadOnlyList<ProgressContribution> contributions)
    {
        var allocatedContributions = Allocate(tiles, contributions);
        var contributionByRequirement = allocatedContributions
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
        var completedTiles = tileResults.Where(value => value.Complete).ToList();
        var currentScoreReachedAt = completedTiles.Count == 0 || completedTiles.Any(value => value.CompletedAt is null)
            ? null
            : completedTiles.Max(value => value.CompletedAt);
        var playerContributions = allocatedContributions
            .GroupBy(value => value.PlayerId)
            .Select(group => new CalculatedPlayerContribution(
                group.Key, group.First().PlayerName,
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
            allocatedContributions.Sum(value => value.EstimatedEhb),
            playerContributions)
        {
            CurrentScoreReachedAt = currentScoreReachedAt
        };
    }

    public static IReadOnlyList<ProgressContribution> Allocate(
        IReadOnlyList<ProgressTileDefinition> tiles,
        IEnumerable<ProgressContribution> contributions)
    {
        var requirementById = tiles
            .SelectMany(value => value.Requirements)
            .ToDictionary(value => value.Id);
        return contributions
            .GroupBy(value => value.RequirementId)
            .SelectMany(group => requirementById.GetValueOrDefault(group.Key) is { } requirement
                ? Allocate(requirement, group)
                : (IEnumerable<ProgressContribution>)group)
            .ToList();
    }

    public static IReadOnlyList<CalculatedTileCompletionFact> CalculateTileCompletionFacts(
        IReadOnlyList<ProgressTileDefinition> tiles,
        IReadOnlyList<ProgressContribution> contributions)
    {
        var allocated = Allocate(tiles, contributions);
        var byRequirement = allocated.GroupBy(value => value.RequirementId)
            .ToDictionary(group => group.Key, group => group.OrderBy(value => value.SubmittedAt).ThenBy(value => value.Id).ToList());
        var result = new List<CalculatedTileCompletionFact>(tiles.Count);

        foreach (var tile in tiles)
        {
            var requirementTimes = new List<DateTimeOffset>(tile.Requirements.Count);
            var provenance = new List<CompletionContributionProvenance>();
            foreach (var requirement in tile.Requirements.OrderBy(value => value.Position))
            {
                var running = 0;
                foreach (var contribution in byRequirement.GetValueOrDefault(requirement.Id) ?? [])
                {
                    running += contribution.Amount;
                    provenance.Add(new CompletionContributionProvenance(requirement.Id, contribution.Id, contribution.SubmissionId,
                        contribution.SubmittedAt, contribution.Amount));
                    if (running < requirement.Target) continue;
                    requirementTimes.Add(contribution.SubmittedAt);
                    break;
                }
            }

            var complete = tile.Requirements.Count > 0 && requirementTimes.Count == tile.Requirements.Count;
            result.Add(new CalculatedTileCompletionFact(tile.Id, complete,
                complete ? requirementTimes.Max() : null,
                complete ? provenance : []));
        }

        return result;
    }

    public static CalculatedBoardProgress ApplyTileCompletionFacts(
        CalculatedBoardProgress progress,
        IReadOnlyDictionary<Guid, PersistedTileCompletionTime> facts)
    {
        if (facts.Count != progress.Tiles.Count || progress.Tiles.Any(tile => !facts.ContainsKey(tile.Id)))
            throw new InvalidOperationException("Persisted tile completion facts do not cover the active board generation.");

        var tiles = progress.Tiles.Select(tile =>
        {
            var fact = facts[tile.Id];
            if (fact.IsComplete != tile.Complete)
                throw new InvalidOperationException("Persisted tile completion facts disagree with approved evidence.");
            return tile with { CompletedAt = fact.IsComplete ? fact.CompletedAt : null };
        }).ToList();
        var completed = tiles.Where(tile => tile.Complete).ToList();
        var currentScoreReachedAt = completed.Count == 0 || completed.Any(tile => tile.CompletedAt is null)
            ? null
            : completed.Max(tile => tile.CompletedAt);
        var boardCompletedAt = progress.BoardComplete && tiles.All(tile => tile.CompletedAt is not null)
            ? tiles.Max(tile => tile.CompletedAt)
            : null;
        return progress with { Tiles = tiles, CurrentScoreReachedAt = currentScoreReachedAt, BoardCompletedAt = boardCompletedAt };
    }

    private static List<ProgressContribution> Allocate(
        ProgressRequirementDefinition requirement,
        IEnumerable<ProgressContribution> contributions)
    {
        var ordered = contributions.OrderBy(value => value.SubmittedAt).ThenBy(value => value.Id).ToList();
        if (ordered.All(value => CapKey(requirement, value) is null)) return ordered;
        var caps = ordered
            .Where(value => CapKey(requirement, value) is not null)
            .GroupBy(value => CapKey(requirement, value)!.Value)
            .ToDictionary(group => group.Key, group =>
            {
                var values = group.Select(value => EffectiveCap(requirement, value)).Distinct().ToList();
                if (values.Count != 1) throw new InvalidOperationException("Inconsistent contribution caps for an immutable progress identity.");
                return values[0];
            });
        var used = new Dictionary<Guid, int>();
        var remaining = requirement.Target;
        var allocated = new List<ProgressContribution>(ordered.Count);
        foreach (var contribution in ordered)
        {
            if (remaining <= 0) break;
            var key = CapKey(requirement, contribution);
            var cap = key is Guid capKey ? caps[capKey] : int.MaxValue;
            var available = Math.Max(0, cap - (key is Guid usedKey ? used.GetValueOrDefault(usedKey) : 0));
            var amount = Math.Min(contribution.Amount, Math.Min(remaining, available));
            if (amount <= 0) continue;
            var ehb = contribution.Amount == amount
                ? contribution.EstimatedEhb
                : contribution.EstimatedEhb * amount / contribution.Amount;
            allocated.Add(contribution with { Amount = amount, EstimatedEhb = ehb });
            remaining -= amount;
            if (key is Guid allocatedKey) used[allocatedKey] = used.GetValueOrDefault(allocatedKey) + amount;
        }
        return allocated;
    }

    private static Guid? CapKey(ProgressRequirementDefinition requirement, ProgressContribution contribution) =>
        requirement.DuplicatesAllowed ? contribution.SourceDropId : contribution.ItemIdSnapshot;

    private static int EffectiveCap(ProgressRequirementDefinition requirement, ProgressContribution contribution) =>
        requirement.DuplicatesAllowed ? contribution.MaximumContribution ?? int.MaxValue : contribution.MaximumContribution ?? 1;

    public static IReadOnlyList<RankedTeamProgress> Rank(IReadOnlyList<UnrankedTeamProgress> teams, PlacementRule rule = PlacementRule.LegacyScoreTimeThenEhb)
    {
        if (!Enum.IsDefined(rule)) throw new ArgumentOutOfRangeException(nameof(rule));
        var primary = teams
            .OrderByDescending(value => value.Progress.BoardComplete)
            .ThenBy(value => value.Progress.BoardComplete ? value.Progress.BoardCompletedAt : DateTimeOffset.MaxValue)
            .ThenByDescending(value => value.Progress.CompletedRows.Count + value.Progress.CompletedColumns.Count)
            .ThenByDescending(value => value.Progress.CompletedTiles);
        var ordered = (rule == PlacementRule.CreditedEhbThenScoreTime
            ? primary.ThenByDescending(value => RankingEhb(value.Progress, rule))
                .ThenBy(value => ScoreTime(value.Progress) ?? DateTimeOffset.MaxValue)
            : primary.ThenBy(value => ScoreTime(value.Progress) ?? DateTimeOffset.MaxValue)
                .ThenByDescending(value => value.Progress.EhbTiebreak))
            .ThenBy(value => value.TeamName).ToList();
        var result = new List<RankedTeamProgress>(ordered.Count);
        for (var index = 0; index < ordered.Count; index++)
        {
            var current = ordered[index];
            var rank = index == 0 || !SameRank(ordered[index - 1], current, rule) ? index + 1 : result[^1].Rank;
            result.Add(new RankedTeamProgress(current.TeamId, current.TeamName, current.Progress, rank));
        }
        return result;
    }

    private static bool SameRank(UnrankedTeamProgress left, UnrankedTeamProgress right, PlacementRule rule) =>
        left.Progress.BoardComplete == right.Progress.BoardComplete &&
        left.Progress.BoardCompletedAt == right.Progress.BoardCompletedAt &&
        left.Progress.CompletedRows.Count + left.Progress.CompletedColumns.Count == right.Progress.CompletedRows.Count + right.Progress.CompletedColumns.Count &&
        left.Progress.CompletedTiles == right.Progress.CompletedTiles &&
        ScoreTime(left.Progress) == ScoreTime(right.Progress) &&
        RankingEhb(left.Progress, rule) == RankingEhb(right.Progress, rule);

    private static decimal RankingEhb(CalculatedBoardProgress progress, PlacementRule rule) =>
        rule == PlacementRule.CreditedEhbThenScoreTime ? decimal.Round(progress.EhbTiebreak, 4) : progress.EhbTiebreak;

    private static DateTimeOffset? ScoreTime(CalculatedBoardProgress progress) =>
        progress.BoardComplete ? progress.BoardCompletedAt : progress.CurrentScoreReachedAt;
}

public sealed record ProgressTileDefinition(Guid Id, int Row, int Column, decimal EstimatedEhb, IReadOnlyList<ProgressRequirementDefinition> Requirements);
public sealed record ProgressRequirementDefinition(Guid Id, int Position, int Target, bool DuplicatesAllowed = true);
public sealed record ProgressContribution(Guid Id, Guid RequirementId, Guid PlayerId, string PlayerName, int Amount, DateTimeOffset SubmittedAt, decimal EstimatedEhb, Guid? ItemIdSnapshot = null, Guid? SourceDropId = null, int? MaximumContribution = null, Guid? SubmissionId = null);
public sealed record CalculatedRequirementProgress(Guid Id, int Target, int Approved, bool Complete, DateTimeOffset? CompletedAt);
public sealed record CalculatedTileProgress(Guid Id, int Row, int Column, int Approved, int Target, bool Complete, DateTimeOffset? CompletedAt, decimal EstimatedEhb);
public sealed record CalculatedPlayerContribution(Guid PlayerId, string PlayerName, decimal EstimatedEhb, int ApprovedContribution, int ApprovedSubmissions);
public sealed record CompletionContributionProvenance(Guid RequirementId, Guid ContributionId, Guid? SubmissionId, DateTimeOffset SubmittedAt, int EffectiveAmount);
public sealed record CalculatedTileCompletionFact(Guid TileId, bool Complete, DateTimeOffset? CompletedAt, IReadOnlyList<CompletionContributionProvenance> QualifyingContributions);
public sealed record PersistedTileCompletionTime(bool IsComplete, DateTimeOffset? CompletedAt);
public sealed record CalculatedBoardProgress(IReadOnlyList<CalculatedTileProgress> Tiles, int CompletedTiles, IReadOnlyList<int> CompletedRows, IReadOnlyList<int> CompletedColumns, bool BoardComplete, DateTimeOffset? BoardCompletedAt, decimal EhbTiebreak, IReadOnlyList<CalculatedPlayerContribution> Players)
{
    public DateTimeOffset? CurrentScoreReachedAt { get; init; }
}
public sealed record UnrankedTeamProgress(Guid TeamId, string TeamName, CalculatedBoardProgress Progress);
public sealed record RankedTeamProgress(Guid TeamId, string TeamName, CalculatedBoardProgress Progress, int Rank);
