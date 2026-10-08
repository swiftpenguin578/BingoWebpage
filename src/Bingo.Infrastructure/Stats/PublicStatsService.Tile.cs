using System.Data;
using Bingo.Application.Stats;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Stats;

public sealed partial class PublicStatsService
{
    public async Task<StatsTileActivity?> GetTileAsync(string eventSlug, Guid teamId, Guid tileId, CancellationToken cancellationToken = default)
    {
        RequireConsistentTransaction(db);
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken) : null;
        var data = await ReadDataAsync(eventSlug, cancellationToken);
        if (data is null || !data.Board.Teams.Any(x => x.TeamId == teamId) || !data.Publication.Tiles.Any(x => x.Id == tileId)) return null;
        var luck = await ReadLuckAsync(data, cancellationToken, retainTileActivity: true);
        var tile = luck.Tiles?.SingleOrDefault(x => x.TileId == tileId);
        if (tile is null)
        {
            // A compatible legacy/partial checkpoint can retain the event snapshot while
            // lacking tile inputs. Keep the route renderable and fail closed for tile Luck.
            var unavailable = UnavailableTile(data, teamId, tileId);
            return new(unavailable.HasDropOutcomes, unavailable.Team, luck.Stale,
                luck.CalculatedAt, luck.FetchedAt, luck.EvidenceRevision);
        }
        return new(tile.HasDropOutcomes, tile.Teams.Single(x => x.TeamId == teamId), luck.Stale,
            luck.CalculatedAt, luck.FetchedAt, luck.EvidenceRevision);
    }

    private static (bool HasDropOutcomes, StatsTileLuckTeam Team) UnavailableTile(StatsData data, Guid teamId, Guid tileId)
    {
        var tile = data.Publication.Tiles.Single(x => x.Id == tileId);
        var requirementIds = data.Publication.Requirements
            .Where(x => x.BoardTileId == tileId && !x.ManualObjective)
            .Select(x => x.Id)
            .ToHashSet();
        var hasDropOutcomes = data.Publication.Drops.Any(x => requirementIds.Contains(x.RequirementId));
        var status = hasDropOutcomes ? StatsLuckStatus.WaitingForActivityData : StatsLuckStatus.NoEligibleActivity;
        var result = new StatsLuckResult(0, null, null, status, false, false);
        var roster = data.Roster.Where(x => x.TeamId == teamId).ToArray();
        var historical = data.Drops.Where(x => x.TeamId == teamId)
            .Select(x => (x.PlayerId, x.CharacterName))
            .Distinct()
            .ToArray();
        var playerIds = roster.Select(x => x.PlayerId).Concat(historical.Select(x => x.PlayerId)).Distinct();
        var players = playerIds.Select(playerId =>
        {
            var name = roster.FirstOrDefault(x => x.PlayerId == playerId)?.PlayerName
                ?? historical.First(x => x.PlayerId == playerId).CharacterName;
            return new StatsTileLuckPlayer(playerId, name, result, []);
        }).OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.PlayerId).ToArray();
        return (hasDropOutcomes, new StatsTileLuckTeam(teamId, result, [], players));
    }

    private static StatsTileLuck[] CalculateTileLuck(StatsData data, IReadOnlyList<StatsLuckTeam> teams,
        IReadOnlyList<StatsLuckSource> sources, bool usableBatch, bool retainKnownActivity = false)
    {
        return data.Publication.Tiles.Select(tile =>
        {
            var requirements = data.Publication.Requirements.Where(x => x.BoardTileId == tile.Id && !x.ManualObjective).Select(x => x.Id).ToHashSet();
            var outcomes = data.Publication.Drops.Where(x => requirements.Contains(x.RequirementId))
                .Select(x => (x.SourceDropId, x.ItemIdSnapshot)).ToHashSet();
            var tileSources = sources.Where(x => outcomes.Contains((x.SourceDropId, x.ItemId))).ToArray();
            // KC follows frozen metric bindings, never item or requirement placement counts.
            var metrics = tileSources.GroupBy(x => (x.BossId, x.Metric))
                .OrderBy(x => x.First().BossName, StringComparer.Ordinal).ThenBy(x => x.Key.Metric ?? string.Empty, StringComparer.Ordinal).ToArray();
            var tileTeams = teams.Select(team =>
            {
                var drops = data.Drops.Where(x => x.TileId == tile.Id && x.TeamId == team.TeamId && outcomes.Contains((x.SourceDropId, x.Item.ItemId))).ToArray();
                var players = team.Players.Where(player => data.Roster.Any(x => x.TeamId == team.TeamId && x.PlayerId == player.PlayerId) || drops.Any(x => x.PlayerId == player.PlayerId))
                    .Select(player =>
                    {
                        var personal = drops.Where(x => x.PlayerId == player.PlayerId).ToArray();
                        var details = player.Sources
                            .Where(x => outcomes.Contains((x.SourceDropId, x.ItemId)))
                            .Select(row => row with
                            {
                                // A retained event observation carries the event-wide numerator.
                                // Tile scoring must replace it with this tile's approved count
                                // before contributor, team, or metric scoring.
                                Received = personal.Count(x => x.CharacterId == row.CharacterId &&
                                    x.SourceDropId == row.SourceDropId && x.Item.ItemId == row.ItemId)
                            }).ToArray();
                        var result = Pool(details.Select(row => Result(row.Received,
                            usableBatch ? row.Expected : null,
                            !usableBatch && retainKnownActivity ? StatsLuckStatus.WaitingForActivityData : row.Status,
                            row.Estimated, row.ZeroRecordedApproximation)).ToArray());
                        if (outcomes.Count > 0 && details.Length == 0) result = Result(personal.Length, null, StatsLuckStatus.WaitingForActivityData);
                        var scoredResult = Score(result, details, tileSources);
                        var counts = metrics.Select(metric =>
                        {
                            var keys = metric.Select(x => (x.SourceDropId, x.ItemId)).ToHashSet();
                            var characters = details.Where(x => keys.Contains((x.SourceDropId, x.ItemId))).GroupBy(x => x.CharacterId).ToArray();
                            // Freshness controls Luck, not whether compatible known KC still exists.
                            var activity = scoredResult.Activities?.SingleOrDefault(x => x.BossId == metric.Key.BossId && x.Metric == metric.Key.Metric);
                            var complete = (usableBatch || retainKnownActivity) && characters.Length > 0 && characters.All(x => x.All(row => row.Activity is not null));
                            decimal? count = complete ? activity is null ? characters.Sum(x => x.First().Activity!.Value) : activity.Kc : null;
                            var status = activity is not null ? activity.Status : complete ? count > 0 ? StatsLuckStatus.Calculated : StatsLuckStatus.NoEligibleActivity
                                : characters.Any(x => x.Any(row => row.Activity is not null)) ? StatsLuckStatus.Incomplete : StatsLuckStatus.WaitingForActivityData;
                            return new StatsTileKc(metric.First().Metric, metric.First().BossName, count, status,
                                activity?.Estimated ?? characters.Any(x => x.Any(row => row.Estimated)),
                                activity?.ZeroRecordedApproximation ?? characters.Any(x => x.Any(row => row.ZeroRecordedApproximation)),
                                activity?.Percentage, activity?.KcDifference);
                        }).ToArray();
                        return new StatsTileLuckPlayer(player.PlayerId, player.Name, scoredResult, counts, details);
                    }).OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.PlayerId).ToArray();
                var teamDetails = players.SelectMany(x => x.Sources ?? []).ToArray();
                var teamResult = Score(Pool(players.Select(x => x.Result).ToArray()), teamDetails, tileSources);
                var totals = metrics.Select((metric, index) =>
                {
                    var rows = players.Select(x => x.Metrics[index]).ToArray();
                    var activity = teamResult.Activities?.SingleOrDefault(x => x.BossId == metric.Key.BossId && x.Metric == metric.Key.Metric);
                    var complete = rows.All(x => x.Count is not null);
                    decimal? count = complete ? activity is null ? rows.Sum(x => x.Count!.Value) : activity.Kc : null;
                    return new StatsTileKc(metric.First().Metric, metric.First().BossName, count,
                        activity?.Status ?? (complete ? count > 0 ? StatsLuckStatus.Calculated : StatsLuckStatus.NoEligibleActivity
                            : rows.All(x => x.Status == StatsLuckStatus.WaitingForActivityData) ? StatsLuckStatus.WaitingForActivityData : StatsLuckStatus.Incomplete),
                        activity?.Estimated ?? rows.Any(x => x.Estimated), activity?.ZeroRecordedApproximation ?? rows.Any(x => x.ZeroRecordedApproximation),
                        activity?.Percentage, activity?.KcDifference);
                }).ToArray();
                return new StatsTileLuckTeam(team.TeamId, teamResult, totals, players);
            }).ToArray();
            return new StatsTileLuck(tile.Id, outcomes.Count > 0, tileTeams);
        }).ToArray();
    }
}
