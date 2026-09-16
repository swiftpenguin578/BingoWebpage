using Bingo.Application.Boards;
using Bingo.Application.Stats;

namespace Bingo.Infrastructure.Stats;

public sealed partial class PublicStatsService
{
    private static CalculatedBoardProgress CalculateProgress(StatsData data, IEnumerable<StatsEvidence> evidence)
    {
        var drops = data.Publication.Drops.ToDictionary(x => x.Id);
        var contributions = evidence.Select(x =>
        {
            var drop = x.Contribution.DropSnapshotId is { } id ? drops.GetValueOrDefault(id) : null;
            return new ProgressContribution(x.Contribution.Id, x.Contribution.RequirementId, x.Submission.CreditedParticipantId,
                x.Submission.CreditedCharacterName, x.Contribution.Amount, x.Submission.SubmittedAt, 0,
                drop?.ItemIdSnapshot, drop?.SourceDropId, drop?.MaximumContribution);
        }).ToArray();
        return PublicProgressCalculator.Calculate(data.Board.Rows, data.Board.Columns, data.Definitions, contributions);
    }

    private static StatsProgressPoint[] ProgressHistory(StatsData data, IReadOnlyList<StatsEvidence> evidence)
    {
        var ordered = evidence.OrderBy(x => x.Submission.SubmittedAt).ThenBy(x => x.Contribution.Id).ToArray();
        var seen = new List<StatsEvidence>();
        return ordered.Select(x =>
        {
            seen.Add(x); var progress = CalculateProgress(data, seen);
            return new StatsProgressPoint(x.Submission.SubmittedAt, x.Submission.Id, progress.Tiles.Sum(t => t.Approved),
                progress.Tiles.Sum(t => t.Target), progress.CompletedTiles, progress.CompletedRows.Count + progress.CompletedColumns.Count,
                progress.BoardComplete, progress.Tiles);
        }).ToArray();
    }

    private static StatsAggregateProgressPoint[] AggregateHistory(StatsData data, StatsTeam[] teams)
    {
        var current = new Dictionary<Guid, StatsProgressPoint>();
        var target = data.Definitions.Sum(x => x.Requirements.Sum(r => r.Target)) * teams.Length;
        return teams.SelectMany(team => team.ProgressHistory.Select((point, sequence) => new { team.TeamId, Point = point, Sequence = sequence }))
            .OrderBy(x => x.Point.At).ThenBy(x => x.TeamId).ThenBy(x => x.Sequence).Select(x =>
            {
                current[x.TeamId] = x.Point;
                return new StatsAggregateProgressPoint(x.Point.At, x.Point.SubmissionId, current.Values.Sum(p => p.Approved), target,
                    current.Values.Sum(p => p.CompletedTiles), data.Board.Rows * data.Board.Columns * teams.Length,
                    current.Values.Sum(p => p.CompletedLines), (data.Board.Rows + data.Board.Columns) * teams.Length,
                    current.Values.Count(p => p.BoardComplete));
            }).ToArray();
    }

    private static StatsMilestone[] Milestones(StatsData data, Guid[] teamIds, bool includeCollective = true)
    {
        var moments = teamIds.Select(teamId =>
        {
            var evidence = data.Evidence.Where(x => x.Submission.TeamId == teamId).ToArray();
            var progress = CalculateProgress(data, evidence);
            var first = evidence.OrderBy(x => x.Submission.SubmittedAt).ThenBy(x => x.Submission.Id).FirstOrDefault()?.Submission;
            var tiles = progress.Tiles.Where(x => x.Complete && x.CompletedAt is not null).OrderBy(x => x.CompletedAt).ThenBy(x => x.Row).ThenBy(x => x.Column).ToArray();
            var lines = Enumerable.Range(0, data.Board.Rows).Select(row => (Kind: "row", Index: row, Tiles: progress.Tiles.Where(x => x.Row == row).ToArray(), Target: data.Board.Columns))
                .Concat(Enumerable.Range(0, data.Board.Columns).Select(column => (Kind: "column", Index: column, Tiles: progress.Tiles.Where(x => x.Column == column).ToArray(), Target: data.Board.Rows)))
                .Where(x => x.Tiles.Length == x.Target && x.Tiles.All(t => t.Complete && t.CompletedAt is not null))
                .Select(x => new { x.Kind, x.Index, At = x.Tiles.Max(t => t.CompletedAt) })
                .OrderBy(x => x.At).ThenBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.Index).FirstOrDefault();
            var half = (data.Board.Rows * data.Board.Columns + 1) / 2;
            var official = data.Official.GetValueOrDefault(teamId);
            return new Dictionary<string, StatsMilestone>
            {
                ["submission"] = new("submission", 1, StatsMilestoneState.Pending, first?.SubmittedAt, teamId, first?.CreditedParticipantId, first?.BoardTileId,
                    Drop: data.Drops.FirstOrDefault(x => x.SubmissionId == first?.Id)),
                ["tile"] = new("tile", 3, StatsMilestoneState.Pending, tiles.FirstOrDefault()?.CompletedAt, teamId, TileId: tiles.FirstOrDefault()?.Id),
                ["halfway"] = new("halfway", 5, StatsMilestoneState.Pending, tiles.Length >= half && half > 0 ? tiles[half - 1].CompletedAt : null, teamId),
                ["row"] = new("row", 6, StatsMilestoneState.Pending, lines?.At, teamId, LineKind: lines?.Kind, LineIndex: lines?.Index),
                ["board"] = new("board", 8, StatsMilestoneState.Pending, official is not null ? (official.BoardComplete ? official.CompletedAt : null) : progress.BoardCompletedAt, teamId)
            };
        }).ToArray();
        var result = new List<StatsMilestone> { new("start", 0, StatsMilestoneState.Pending, data.Event.ActualStartedAt), new("end", 10, StatsMilestoneState.Pending, data.Event.ActualEndedAt) };
        foreach (var key in new[] { "submission", "tile", "halfway", "row", "board" })
        {
            var candidates = moments.Select(x => x[key]).ToArray();
            var selected = candidates.Where(x => x.At is not null).OrderBy(x => x.At).ThenBy(x => x.TeamId).FirstOrDefault() ?? candidates.FirstOrDefault();
            if (selected is not null) result.Add(selected);
        }
        if (includeCollective)
        {
            foreach (var (id, key, order) in new[] { ("all-submissions", "submission", 2), ("all-tiles", "tile", 4), ("all-rows", "row", 7), ("all-boards", "board", 9) })
            {
                var candidates = moments.Select(x => x[key]).ToArray();
                result.Add(new(id, order, StatsMilestoneState.Pending, candidates.Length > 0 && candidates.All(x => x.At is not null) ? candidates.Max(x => x.At) : null));
            }
        }
        return result.Select(x => x with { State = x.At is not null ? StatsMilestoneState.Reached : data.Event.ActualEndedAt is not null ? StatsMilestoneState.NotReached : StatsMilestoneState.Pending })
            .OrderBy(x => x.Id == "start" ? 0 : x.State == StatsMilestoneState.Reached ? 1 : 2)
            .ThenBy(x => x.State == StatsMilestoneState.Reached ? x.At : null).ThenBy(x => x.DefaultOrder).ToArray();
    }
}
