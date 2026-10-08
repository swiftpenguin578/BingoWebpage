using Bingo.Application.Boards;
using Bingo.Domain.Boards;
using Bingo.Infrastructure.Boards;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

public sealed partial class BoardModel
{
    private static string ApprovalDescription(bool automatic, string authored, IEnumerable<TileDescriptionRequirement> requirements)
        => automatic ? TileDescriptionFormatter.Format(requirements) : authored;

    private static decimal ApprovalTileEhb(TileTemplate template, IEnumerable<(bool Manual, decimal? Estimate)> estimates)
        => EhbCalculator.CalculateTileEstimate(template.ObjectiveType, estimates, template.ManualEhbOverride);

    // Read-only projection of the same retained/current inputs used when approval
    // freezes a replacement. Working estimate caches are not publication facts.
    private async Task<BoardVersionContent> ProjectCorrectionPublicationAsync(BoardVersionContent working, PublishedBoardData prior, CancellationToken ct)
    {
        var templateIds = working.Tiles.Select(x => x.TemplateId).ToList();
        var templates = await db.TileTemplates.AsNoTracking().Where(x => templateIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var sourceIds = working.Tiles.SelectMany(x => x.Objectives).SelectMany(x => x.Drops).Select(x => x.SourceDropId).Distinct().ToList();
        var sources = await db.SourceDrops.AsNoTracking().Where(x => sourceIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var bossIds = sources.Values.Select(x => x.BossActivityId).Distinct().ToList();
        var bosses = await db.BossActivities.AsNoTracking().Where(x => bossIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var itemIds = sources.Values.Select(x => x.ItemId).Distinct().ToList();
        var items = await db.CatalogueItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var frozenRequirements = await db.BoardApprovalRequirementSnapshots.AsNoTracking()
            .Join(db.BoardApprovalTileSnapshots.AsNoTracking().Where(x => x.ApprovalSnapshotId == prior.Approval.Id), r => r.ApprovalTileSnapshotId, t => t.Id, (r, _) => r).ToListAsync(ct);
        var frozenIds = frozenRequirements.Select(x => x.Id).ToList();
        var frozenBosses = await db.BoardApprovalRequirementBossSnapshots.AsNoTracking().Where(x => frozenIds.Contains(x.ApprovalRequirementSnapshotId)).ToListAsync(ct);
        var frozenDrops = await db.BoardApprovalRequirementDropSnapshots.AsNoTracking().Where(x => frozenIds.Contains(x.ApprovalRequirementSnapshotId)).ToListAsync(ct);
        var projected = working.Tiles.Select(tile =>
        {
            if (!templates.TryGetValue(tile.TemplateId, out var template)) return tile;
            var estimates = new List<(bool Manual, decimal? Estimate)>();
            var objectives = tile.Objectives.Select(objective =>
            {
                var retained = frozenRequirements.SingleOrDefault(x => x.BoardRequirementSnapshotId == objective.RequirementId);
                var rates = new List<EligibleDropRate>();
                IReadOnlyList<BoardDropContent> drops;
                if (retained is not null)
                {
                    var savedDrops = frozenDrops.Where(x => x.ApprovalRequirementSnapshotId == retained.Id).ToList();
                    foreach (var drop in savedDrops)
                    {
                        var boss = frozenBosses.Single(x => x.ApprovalRequirementSnapshotId == retained.Id && x.BossActivityId == sources[drop.SourceDropId].BossActivityId);
                        rates.Add(new(boss.EfficientRate, drop.NumericProbability, drop.ItemIdSnapshot, boss.BossActivityId, drop.CreditedWeight, drop.RollsPerCompletion, drop.RollGroup));
                    }
                    drops = savedDrops.OrderBy(x => x.SourceDropId).Select(x => new BoardDropContent(x.SourceDropId, x.ItemIdSnapshot, x.BossName, x.ItemName, x.DisplayRate, x.CreditedWeight, x.MaximumContribution)).ToList();
                }
                else
                {
                    drops = objective.Drops.Select(drop =>
                    {
                        if (!sources.TryGetValue(drop.SourceDropId, out var source) || !bosses.TryGetValue(source.BossActivityId, out var boss) || !items.TryGetValue(source.ItemId, out var item)) return drop;
                        rates.Add(new(boss.EfficientCompletionsPerHour, source.NumericProbability, source.ItemId, boss.Id, drop.Weight, source.RollsPerCompletion, source.RollGroup));
                        return drop with { BossName = boss.Name, ItemName = item.Name, Rate = source.DisplayRate };
                    }).ToList();
                }
                estimates.Add((objective.Manual, objective.Manual ? null : EhbCalculator.CalculateDropRequirement(objective.Target, rates, objective.DuplicatesAllowed)));
                return objective with { Drops = drops };
            }).ToList();
            var description = ApprovalDescription(tile.DescriptionIsAutomatic, tile.Description,
                objectives.Select(x => new TileDescriptionRequirement(x.Position, x.Target, x.Manual, x.Description,
                    x.Drops.Select(d => new TileDescriptionDrop(d.ItemId, d.ItemName, d.BossName)).ToList())));
            // Numeric snapshot columns persist four decimal places, half away from zero.
            var ehb = decimal.Round(ApprovalTileEhb(template, estimates), 4, MidpointRounding.AwayFromZero);
            return tile with { Description = description, Ehb = ehb, Objectives = objectives };
        }).ToList();
        return working with { Tiles = projected };
    }
}
