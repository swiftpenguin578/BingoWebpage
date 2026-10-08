using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bingo.Application.Boards;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Boards;

/// <summary>
/// Keeps the mutable working-copy estimate cache small and explicit. This is a
/// derived cache, not competitive history: approved/public snapshots are never
/// touched by these methods and refreshes do not create Audit entries.
/// </summary>
public static class BoardEstimateService
{
    public sealed record CatalogueChangeSet(
        IReadOnlySet<Guid> BossIds,
        IReadOnlySet<Guid> DropIds,
        IReadOnlySet<Guid> ItemIds)
    {
        public bool IsEmpty => BossIds.Count == 0 && DropIds.Count == 0 && ItemIds.Count == 0;

        public static CatalogueChangeSet Capture(ApplicationDbContext db)
        {
            var bosses = db.ChangeTracker.Entries<BossActivity>()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(entry => entry.Entity.Id)
                .ToHashSet();
            var drops = db.ChangeTracker.Entries<SourceDrop>()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(entry => entry.Entity.Id)
                .ToHashSet();
            var items = db.ChangeTracker.Entries<CatalogueItem>()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(entry => entry.Entity.Id)
                .ToHashSet();
            return new(bosses, drops, items);
        }
    }

    public sealed record RefreshSummary(int Refreshed, int Invalid);

    private sealed record TileContext(
        BoardTile Tile,
        TileTemplate Template,
        IReadOnlyList<BoardRequirementSnapshot> Requirements,
        IReadOnlyList<BoardRequirementBossSnapshot> Bosses,
        IReadOnlyList<BoardRequirementDropSnapshot> Drops,
        IReadOnlyDictionary<Guid, SourceDrop> CurrentDrops,
        IReadOnlyDictionary<Guid, BossActivity> CurrentBosses,
        IReadOnlyDictionary<Guid, CatalogueItem> CurrentItems);

    private sealed record FingerprintRow(
        Guid Id,
        long? Version,
        Guid? ParentId = null,
        Guid? ItemId = null,
        bool Exists = true,
        bool Active = true);

    /// <summary>
    /// Refreshes only stale draft/correction tiles. The first call after the
    /// freshness migration is therefore the bounded backfill for unfinished
    /// working boards.
    /// </summary>
    public static async Task<IReadOnlySet<Guid>> RefreshStaleDraftTilesAsync(
        ApplicationDbContext db,
        Guid eventId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId, ct);
        if (board is null || !IsWorkingState(board.State, board.PublishedCorrectionInProgress)) return new HashSet<Guid>();

        var states = await db.BoardTiles.AsNoTracking()
            .Where(x => x.BoardId == board.Id)
            .Select(x => new { x.Id, x.EstimateCatalogueFingerprint, x.EstimateNeedsVerification })
            .ToListAsync(ct);
        if (states.Count == 0) return new HashSet<Guid>();

        var contexts = await LoadContextsAsync(db, board.Id, states.Select(x => x.Id), ct);
        var stale = states.Where(state =>
        {
            var context = contexts.GetValueOrDefault(state.Id);
            return state.EstimateNeedsVerification || context is null ||
                   !string.Equals(state.EstimateCatalogueFingerprint, Fingerprint(context), StringComparison.Ordinal);
        }).Select(x => x.Id).ToHashSet();
        if (stale.Count == 0) return stale;

        await RefreshTilesAsync(db, stale, now, ct);
        return stale;
    }

    /// <summary>
    /// Recalculates only the draft tiles that depend on changed catalogue
    /// identities. The caller owns the surrounding transaction and persists the
    /// derived updates with its normal SaveChanges call.
    /// </summary>
    public static async Task<RefreshSummary> RefreshDependentDraftTilesAsync(
        ApplicationDbContext db,
        CatalogueChangeSet changes,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (changes.IsEmpty) return new(0, 0);

        var boardIds = await db.Boards.AsNoTracking()
            .Where(x => x.State == BoardState.Draft || x.PublishedCorrectionInProgress)
            .Select(x => x.Id)
            .ToListAsync(ct);
        if (boardIds.Count == 0) return new(0, 0);

        var requirementIds = new HashSet<Guid>();
        if (changes.BossIds.Count > 0)
        {
            requirementIds.UnionWith(await db.BoardRequirementBossSnapshots.AsNoTracking()
                .Where(x => changes.BossIds.Contains(x.BossActivityId))
                .Select(x => x.RequirementId)
                .ToListAsync(ct));
        }
        if (changes.DropIds.Count > 0 || changes.ItemIds.Count > 0)
        {
            requirementIds.UnionWith(await db.BoardRequirementDropSnapshots.AsNoTracking()
                .Where(x => changes.DropIds.Contains(x.SourceDropId) || changes.ItemIds.Contains(x.ItemIdSnapshot))
                .Select(x => x.RequirementId)
                .ToListAsync(ct));

            if (changes.ItemIds.Count > 0)
            {
                var mappedDropIds = await db.SourceDrops.AsNoTracking()
                    .Where(x => changes.ItemIds.Contains(x.ItemId))
                    .Select(x => x.Id)
                    .ToListAsync(ct);
                requirementIds.UnionWith(await db.BoardRequirementDropSnapshots.AsNoTracking()
                    .Where(x => mappedDropIds.Contains(x.SourceDropId))
                    .Select(x => x.RequirementId)
                    .ToListAsync(ct));
            }
        }

        if (requirementIds.Count == 0) return new(0, 0);
        var tileIds = await db.BoardRequirementSnapshots.AsNoTracking()
            .Where(x => requirementIds.Contains(x.Id))
            .Join(db.BoardTiles.AsNoTracking().Where(x => boardIds.Contains(x.BoardId)), x => x.BoardTileId, x => x.Id, (requirement, tile) => tile.Id)
            .Distinct()
            .ToListAsync(ct);
        return await RefreshTilesAsync(db, tileIds, now, ct);
    }

    public static async Task<RefreshSummary> RefreshTilesAsync(
        ApplicationDbContext db,
        IEnumerable<Guid> tileIds,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var ids = tileIds.Distinct().ToArray();
        if (ids.Length == 0) return new(0, 0);

        var contexts = await LoadContextsAsync(db, ids, ct);
        var tiles = await db.BoardTiles.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var invalid = 0;
        foreach (var id in ids)
        {
            if (!contexts.TryGetValue(id, out var context) || !tiles.TryGetValue(id, out var tile)) continue;
            var estimate = Calculate(context, out var valid);
            if (!valid) invalid++;
            tile.SetEstimateCache(valid ? estimate : 0m, Fingerprint(context), now, !valid);
        }

        var boardIds = tiles.Values.Select(x => x.BoardId).Distinct().ToArray();
        var boards = await db.Boards.Where(x => boardIds.Contains(x.Id) && (x.State == BoardState.Draft || x.PublishedCorrectionInProgress)).ToDictionaryAsync(x => x.Id, ct);
        var storedTotals = await db.BoardTiles.AsNoTracking()
            .Where(x => boardIds.Contains(x.BoardId))
            .GroupBy(x => x.BoardId)
            .Select(group => new { BoardId = group.Key, Total = group.Sum(x => x.EstimatedEhbSnapshot) })
            .ToDictionaryAsync(x => x.BoardId, x => x.Total, ct);
        foreach (var board in boards.Values)
        {
            var total = storedTotals.GetValueOrDefault(board.Id);
            foreach (var tile in tiles.Values.Where(x => x.BoardId == board.Id))
            {
                var original = db.Entry(tile).Property(x => x.EstimatedEhbSnapshot).OriginalValue;
                total += tile.EstimatedEhbSnapshot - original;
            }
            board.SetTotalEhb(total);
        }
        return new(tiles.Count, invalid);
    }

    public static async Task<IReadOnlyDictionary<Guid, decimal?>> CalculatedBaselinesAsync(
        ApplicationDbContext db, IEnumerable<Guid> tileIds, CancellationToken ct)
    {
        var contexts = await LoadContextsAsync(db, tileIds, ct);
        return contexts.ToDictionary(pair => pair.Key, pair =>
        {
            if (pair.Value.Template.ObjectiveType != ObjectiveType.DropRequirements) return (decimal?)null;
            var estimate = Calculate(pair.Value, out var valid, applyOverride: false);
            return valid ? estimate : (decimal?)null;
        });
    }

    private static bool IsWorkingState(BoardState state, bool correction) => state == BoardState.Draft || correction;

    private static async Task<Dictionary<Guid, TileContext>> LoadContextsAsync(
        ApplicationDbContext db,
        Guid boardId,
        IEnumerable<Guid> tileIds,
        CancellationToken ct) => await LoadContextsAsync(db, [boardId], tileIds, boardIdFilter: true, ct: ct);

    private static async Task<Dictionary<Guid, TileContext>> LoadContextsAsync(
        ApplicationDbContext db,
        IEnumerable<Guid> tileIds,
        CancellationToken ct)
    {
        var ids = tileIds.Distinct().ToArray();
        var tiles = await db.BoardTiles.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        return await BuildContextsAsync(db, tiles, ct);
    }

    private static async Task<Dictionary<Guid, TileContext>> LoadContextsAsync(
        ApplicationDbContext db,
        IReadOnlyList<Guid> boardIds,
        IEnumerable<Guid> tileIds,
        bool boardIdFilter,
        CancellationToken ct)
    {
        var ids = tileIds.Distinct().ToArray();
        var query = db.BoardTiles.AsNoTracking().Where(x => ids.Contains(x.Id));
        if (boardIdFilter) query = query.Where(x => boardIds.Contains(x.BoardId));
        var tiles = await query.ToListAsync(ct);
        return await BuildContextsAsync(db, tiles, ct);
    }

    private static async Task<Dictionary<Guid, TileContext>> BuildContextsAsync(
        ApplicationDbContext db,
        List<BoardTile> tiles,
        CancellationToken ct)
    {
        if (tiles.Count == 0) return [];
        var tileIds = tiles.Select(x => x.Id).ToArray();
        var requirements = await db.BoardRequirementSnapshots.AsNoTracking()
            .Where(x => tileIds.Contains(x.BoardTileId))
            .OrderBy(x => x.Position)
            .ToListAsync(ct);
        var frozenRequirementIds = await db.BoardApprovalRequirementSnapshots.AsNoTracking()
            .Select(x => x.BoardRequirementSnapshotId)
            .ToHashSetAsync(ct);
        var currentRequirements = requirements
            .GroupBy(x => (x.BoardTileId, x.Position))
            .Select(group => group.Where(x => !frozenRequirementIds.Contains(x.Id)).OrderByDescending(x => x.Id).FirstOrDefault() ?? group.OrderByDescending(x => x.Id).First())
            .ToList();
        var requirementIds = currentRequirements.Select(x => x.Id).ToArray();
        var bosses = await db.BoardRequirementBossSnapshots.AsNoTracking().Where(x => requirementIds.Contains(x.RequirementId)).ToListAsync(ct);
        var drops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => requirementIds.Contains(x.RequirementId)).ToListAsync(ct);
        var sourceDropIds = drops.Select(x => x.SourceDropId).Distinct().ToArray();
        var currentDrops = await db.SourceDrops.AsNoTracking().Where(x => sourceDropIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var bossIds = bosses.Select(x => x.BossActivityId).Concat(currentDrops.Values.Select(x => x.BossActivityId)).Distinct().ToArray();
        var currentBosses = await db.BossActivities.AsNoTracking().Where(x => bossIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var itemIds = drops.Select(x => x.ItemIdSnapshot).Concat(currentDrops.Values.Select(x => x.ItemId)).Distinct().ToArray();
        var currentItems = await db.CatalogueItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var templateIds = tiles.Select(x => x.TileTemplateId).Distinct().ToArray();
        var templates = await db.TileTemplates.AsNoTracking().Where(x => templateIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);

        return tiles.Where(tile => templates.ContainsKey(tile.TileTemplateId)).ToDictionary(
            tile => tile.Id,
            tile =>
            {
                var tileRequirements = currentRequirements.Where(x => x.BoardTileId == tile.Id).OrderBy(x => x.Position).ToList();
                var tileRequirementIds = tileRequirements.Select(x => x.Id).ToHashSet();
                var tileBosses = bosses.Where(x => tileRequirementIds.Contains(x.RequirementId)).ToList();
                var tileDrops = drops.Where(x => tileRequirementIds.Contains(x.RequirementId)).ToList();
                var tileSourceDropIds = tileDrops.Select(x => x.SourceDropId).ToHashSet();
                var tileCurrentDrops = currentDrops
                    .Where(x => tileSourceDropIds.Contains(x.Key))
                    .ToDictionary(x => x.Key, x => x.Value);
                var tileBossIds = tileBosses.Select(x => x.BossActivityId)
                    .Concat(tileCurrentDrops.Values.Select(x => x.BossActivityId))
                    .ToHashSet();
                var tileCurrentBosses = currentBosses
                    .Where(x => tileBossIds.Contains(x.Key))
                    .ToDictionary(x => x.Key, x => x.Value);
                var tileItemIds = tileDrops.Select(x => x.ItemIdSnapshot)
                    .Concat(tileCurrentDrops.Values.Select(x => x.ItemId))
                    .ToHashSet();
                var tileCurrentItems = currentItems
                    .Where(x => tileItemIds.Contains(x.Key))
                    .ToDictionary(x => x.Key, x => x.Value);
                return new TileContext(
                    tile,
                    templates[tile.TileTemplateId],
                    tileRequirements,
                    tileBosses,
                    tileDrops,
                    tileCurrentDrops,
                    tileCurrentBosses,
                    tileCurrentItems);
            });
    }

    private static decimal Calculate(TileContext context, out bool valid, bool applyOverride = true)
    {
        valid = true;
        var estimates = new List<decimal?>();
        foreach (var requirement in context.Requirements)
        {
            if (requirement.ManualObjective)
            {
                estimates.Add(null);
                continue;
            }

            var selected = context.Drops.Where(x => x.RequirementId == requirement.Id).ToList();
            var bossIds = context.Bosses.Where(x => x.RequirementId == requirement.Id).Select(x => x.BossActivityId).ToHashSet();
            // Older working rows can retain a drop snapshot without the
            // corresponding eligible-boss snapshot. In that legacy shape the
            // current drop's active boss is the only available mapping; when
            // eligible-boss snapshots exist, retain the fail-closed
            // membership check for mismatched mappings.
            var enforceBossMembership = bossIds.Count > 0;
            var rates = new List<EligibleDropRate>();
            foreach (var snapshot in selected)
            {
                if (!context.CurrentDrops.TryGetValue(snapshot.SourceDropId, out var drop) ||
                    !context.CurrentBosses.TryGetValue(drop.BossActivityId, out var boss) ||
                    !context.CurrentItems.TryGetValue(drop.ItemId, out var item) ||
                    !drop.Active || !boss.Active || !item.Active ||
                    drop.ItemId != snapshot.ItemIdSnapshot || enforceBossMembership && !bossIds.Contains(drop.BossActivityId))
                {
                    valid = false;
                    break;
                }
                rates.Add(new EligibleDropRate(boss.EfficientCompletionsPerHour, drop.NumericProbability,
                    drop.ItemId, boss.Id, snapshot.CreditedWeight, drop.RollsPerCompletion, drop.RollGroup));
            }
            if (!valid || selected.Count == 0)
            {
                valid = false;
                estimates.Add(null);
                continue;
            }
            var estimate = EhbCalculator.CalculateDropRequirement(requirement.TargetContribution, rates, requirement.DuplicatesAllowed);
            if (estimate is not > 0)
            {
                valid = false;
                estimates.Add(null);
            }
            else estimates.Add(estimate);
        }

        var tileEstimate = EhbCalculator.CalculateTileEstimate(context.Template.ObjectiveType,
            context.Requirements.Zip(estimates, (requirement, estimate) => (requirement.ManualObjective, estimate)),
            applyOverride ? context.Template.ManualEhbOverride : null);
        if (tileEstimate <= 0) valid = false;
        return tileEstimate;
    }

    private static string Fingerprint(TileContext context) => Fingerprint(
        context.Bosses.Select(x => context.CurrentBosses.TryGetValue(x.BossActivityId, out var boss)
            ? new FingerprintRow(x.BossActivityId, boss.Version, Active: boss.Active)
            : new FingerprintRow(x.BossActivityId, null, Exists: false, Active: false))
            .Concat(context.CurrentDrops.Values.Select(drop => context.CurrentBosses.TryGetValue(drop.BossActivityId, out var boss)
                ? new FingerprintRow(drop.BossActivityId, boss.Version, Active: boss.Active)
                : new FingerprintRow(drop.BossActivityId, null, Exists: false, Active: false)))
            .GroupBy(x => x.Id)
            .Select(group => group.First()),
        context.Drops.Select(x =>
        {
            var drop = context.CurrentDrops.GetValueOrDefault(x.SourceDropId);
            var item = drop is not null ? context.CurrentItems.GetValueOrDefault(drop.ItemId) : null;
            return new FingerprintRow(x.SourceDropId, drop?.Version, drop?.BossActivityId, x.ItemIdSnapshot,
                drop is not null, drop?.Active == true && item?.Active == true);
        }),
        context.Drops.Select(x =>
        {
            var drop = context.CurrentDrops.GetValueOrDefault(x.SourceDropId);
            var itemId = drop?.ItemId ?? x.ItemIdSnapshot;
            return new FingerprintRow(itemId, context.CurrentItems.GetValueOrDefault(itemId)?.Version,
                ItemId: itemId, Exists: context.CurrentItems.ContainsKey(itemId), Active: context.CurrentItems.GetValueOrDefault(itemId)?.Active == true);
        }),
        context.Template.ObjectiveType.ToString(),
        context.Template.ManualEhbOverride);

    private static string Fingerprint(
        IEnumerable<FingerprintRow> bosses,
        IEnumerable<FingerprintRow> drops,
        IEnumerable<FingerprintRow> items,
        string objectiveType,
        decimal? manualEhb)
    {
        var payload = new
        {
            ObjectiveType = objectiveType,
            ManualEhb = manualEhb,
            Bosses = bosses.OrderBy(x => x.Id).ThenBy(x => x.ParentId).ToArray(),
            Drops = drops.OrderBy(x => x.Id).ToArray(),
            Items = items.OrderBy(x => x.Id).ToArray()
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
    }
}
