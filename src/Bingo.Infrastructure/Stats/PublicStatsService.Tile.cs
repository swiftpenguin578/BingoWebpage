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
        var tile = luck.Tiles!.Single(x => x.TileId == tileId);
        return new(tile.HasDropOutcomes, tile.Teams.Single(x => x.TeamId == teamId), luck.Stale,
            luck.CalculatedAt, luck.FetchedAt, luck.EvidenceRevision);
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
            var metrics = tileSources.GroupBy(x => x.Metric ?? $"unavailable:{x.BossId}:{x.BossName}")
                .OrderBy(x => x.First().BossName, StringComparer.Ordinal).ThenBy(x => x.Key, StringComparer.Ordinal).ToArray();
            var tileTeams = teams.Select(team =>
            {
                var drops = data.Drops.Where(x => x.TileId == tile.Id && x.TeamId == team.TeamId && outcomes.Contains((x.SourceDropId, x.Item.ItemId))).ToArray();
                var players = team.Players.Where(player => data.Roster.Any(x => x.TeamId == team.TeamId && x.PlayerId == player.PlayerId) || drops.Any(x => x.PlayerId == player.PlayerId))
                    .Select(player =>
                    {
                        var personal = drops.Where(x => x.PlayerId == player.PlayerId).ToArray();
                        var details = player.Sources.Where(x => outcomes.Contains((x.SourceDropId, x.ItemId))).ToArray();
                        var result = Pool(details.Select(row => Result(personal.Count(x => x.CharacterId == row.CharacterId && x.SourceDropId == row.SourceDropId && x.Item.ItemId == row.ItemId),
                            usableBatch ? row.Expected : null,
                            !usableBatch && retainKnownActivity ? StatsLuckStatus.WaitingForActivityData : row.Status,
                            row.Estimated, row.ZeroRecordedApproximation)).ToArray());
                        if (outcomes.Count > 0 && details.Length == 0) result = Result(personal.Length, null, StatsLuckStatus.WaitingForActivityData);
                        var counts = metrics.Select(metric =>
                        {
                            var keys = metric.Select(x => (x.SourceDropId, x.ItemId)).ToHashSet();
                            var characters = details.Where(x => keys.Contains((x.SourceDropId, x.ItemId))).GroupBy(x => x.CharacterId).ToArray();
                            // Freshness controls Luck, not whether compatible known KC still exists.
                            var complete = (usableBatch || retainKnownActivity) && characters.Length > 0 && characters.All(x => x.All(row => row.Activity is not null));
                            decimal? count = complete ? characters.Sum(x => x.First().Activity!.Value) : null;
                            var status = complete ? count > 0 ? StatsLuckStatus.Calculated : StatsLuckStatus.NoEligibleActivity
                                : characters.Any(x => x.Any(row => row.Activity is not null)) ? StatsLuckStatus.Incomplete : StatsLuckStatus.WaitingForActivityData;
                            return new StatsTileKc(metric.First().Metric, metric.First().BossName, count, status,
                                characters.Any(x => x.Any(row => row.Estimated)), characters.Any(x => x.Any(row => row.ZeroRecordedApproximation)));
                        }).ToArray();
                        return new StatsTileLuckPlayer(player.PlayerId, player.Name, result, counts);
                    }).OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.PlayerId).ToArray();
                var totals = metrics.Select((metric, index) =>
                {
                    var rows = players.Select(x => x.Metrics[index]).ToArray();
                    var complete = rows.All(x => x.Count is not null);
                    decimal? count = complete ? rows.Sum(x => x.Count!.Value) : null;
                    return new StatsTileKc(metric.First().Metric, metric.First().BossName, count,
                        complete ? count > 0 ? StatsLuckStatus.Calculated : StatsLuckStatus.NoEligibleActivity
                            : rows.All(x => x.Status == StatsLuckStatus.WaitingForActivityData) ? StatsLuckStatus.WaitingForActivityData : StatsLuckStatus.Incomplete,
                        rows.Any(x => x.Estimated), rows.Any(x => x.ZeroRecordedApproximation));
                }).ToArray();
                return new StatsTileLuckTeam(team.TeamId, Pool(players.Select(x => x.Result).ToArray()), totals, players);
            }).ToArray();
            return new StatsTileLuck(tile.Id, outcomes.Count > 0, tileTeams);
        }).ToArray();
    }
}
