using System.Data;
using System.Data.Common;
using System.Text.Json;
using Bingo.Application.Boards;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Boards;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

public sealed partial class BoardModel
{
    // These endpoints execute the existing command once and return issues plus
    // current state. Neither the response nor a subsequent read identifies a request.
    public async Task<IActionResult> OnPostApproveStateAsync(Guid id, bool confirmed, CancellationToken ct)
    {
        if (!await CanReadBoardStateAsync(ct)) return Forbid();
        await OnPostApproveAsync(id, confirmed, ct);
        return new JsonResult(new BoardActionState(ValidationIssues, await ReadBoardStateAsync(id, ct)));
    }

    public async Task<IActionResult> OnPostPublishStateAsync(Guid id, bool confirmed, CancellationToken ct)
    {
        if (!await CanReadBoardStateAsync(ct)) return Forbid();
        await OnPostPublishAsync(id, confirmed, ct);
        return new JsonResult(new BoardActionState(ValidationIssues, await ReadBoardStateAsync(id, ct)));
    }

    public async Task<IActionResult> OnGetReadbackAsync(Guid id, CancellationToken ct)
    {
        if (!await CanReadBoardStateAsync(ct)) return Forbid();
        Response.Headers.CacheControl = "no-store";
        return new JsonResult(await ReadBoardStateAsync(id, ct));
    }

    private Task<bool> CanReadBoardStateAsync(CancellationToken ct) => User.GetAccountId() is { } actor
        ? db.Accounts.AsNoTracking().AnyAsync(x => x.Id == actor && x.Active && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin), ct)
        : Task.FromResult(false);

    private async Task<BoardReadback> ReadBoardStateAsync(Guid eventId, CancellationToken ct)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, ct);
            var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId, ct);
            if (ev is null || board is null) return new(null);
            var workingTiles = await db.BoardTiles.AsNoTracking().Where(x => x.BoardId == board.Id).OrderBy(x => x.RowIndex).ThenBy(x => x.ColumnIndex).ToListAsync(ct);
            var workingRequirements = await CurrentRequirementsAsync(workingTiles, board.Columns, ct);
            var ids = workingRequirements.Select(x => x.Id).ToList();
            var workingDrops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => ids.Contains(x.RequirementId)).ToListAsync(ct);
            var working = await ReadVersionAsync(board.Id, null, board.Version, board.Name, board.Rows, board.Columns, workingTiles, workingRequirements, workingDrops, ct);
            BoardVersionContent? approved = null;
            if (board.ActiveApprovalSnapshotId is { } approvalId)
            {
                var publication = await db.ApprovalObjectivesAsync(board.Id, approvalId, ct);
                if (publication is null) return new(null);
                approved = await ReadVersionAsync(board.Id, approvalId, publication.Approval.Version, publication.Approval.Name,
                    publication.Approval.Rows, publication.Approval.Columns, publication.Tiles, publication.Requirements, publication.Drops, ct);
            }
            var published = board.State == BoardState.Published ? approved : null;
            var differences = board.PublishedCorrectionInProgress && published is not null
                ? working.Tiles.Select(x => x.TileId).Union(published.Tiles.Select(x => x.TileId)).Where(tileId =>
                    !SameTile(working.Tiles.SingleOrDefault(x => x.TileId == tileId), published.Tiles.SingleOrDefault(x => x.TileId == tileId))).ToList()
                : [];
            var state = new BoardCurrentState(eventId, ev.Version, ev.State, board.Id, board.State, board.Version,
                board.PublishedCorrectionInProgress, board.ActiveApprovalSnapshotId, board.PublishedAt,
                board.EditorAccountId, board.EditorLeaseExpiresAt, board.EditControlVersion, ev.ExpectedTeamSize,
                working, approved, published, board.PublishedCorrectionInProgress ? working : null, differences);
            await tx.CommitAsync(ct);
            return new(state);
        }
        catch (Exception ex) when (ex is DbException or TimeoutException or BoardApprovalValidationException || ex is InvalidOperationException { InnerException: DbException or TimeoutException })
        {
            return new(null);
        }
    }

    private async Task<BoardVersionContent> ReadVersionAsync(Guid boardId, Guid? approvalId, long version, string name, int rows, int columns,
        IReadOnlyList<BoardTile> tiles, IReadOnlyList<BoardRequirementSnapshot> requirements, IReadOnlyList<BoardRequirementDropSnapshot> drops, CancellationToken ct)
    {
        var tileIds = tiles.Select(x => x.Id).ToList();
        var requirementIds = requirements.Select(x => x.Id).ToList();
        var bossIdsByRequirement = new Dictionary<Guid, List<Guid>>();
        Dictionary<Guid, string?> artwork;
        if (approvalId is { } snapshotId)
        {
            var frozenTiles = await db.BoardApprovalTileSnapshots.AsNoTracking().Where(x => x.ApprovalSnapshotId == snapshotId).ToListAsync(ct);
            artwork = frozenTiles.ToDictionary(x => x.BoardTileId, x => x.ArtworkReference);
            var frozenTileIds = frozenTiles.Select(x => x.Id).ToList();
            var frozenRequirements = await db.BoardApprovalRequirementSnapshots.AsNoTracking().Where(x => frozenTileIds.Contains(x.ApprovalTileSnapshotId)).ToListAsync(ct);
            var frozenIds = frozenRequirements.Select(x => x.Id).ToList();
            var bosses = await db.BoardApprovalRequirementBossSnapshots.AsNoTracking().Where(x => frozenIds.Contains(x.ApprovalRequirementSnapshotId)).ToListAsync(ct);
            bossIdsByRequirement = frozenRequirements.ToDictionary(x => x.BoardRequirementSnapshotId,
                x => bosses.Where(b => b.ApprovalRequirementSnapshotId == x.Id).Select(b => b.BossActivityId).Order().ToList());
        }
        else
        {
            var images = await db.BoardTileImageAssets.AsNoTracking().Where(x => tileIds.Contains(x.BoardTileId) && x.ReplacedAt == null).ToListAsync(ct);
            artwork = tiles.ToDictionary(x => x.Id, x => images.SingleOrDefault(i => i.Id == x.ActiveImageAssetId && i.BoardTileId == x.Id)?.StorageKey);
            var bosses = await db.BoardRequirementBossSnapshots.AsNoTracking().Where(x => requirementIds.Contains(x.RequirementId)).ToListAsync(ct);
            bossIdsByRequirement = requirements.ToDictionary(x => x.Id, x => bosses.Where(b => b.RequirementId == x.Id).Select(b => b.BossActivityId).Order().ToList());
        }
        var templateIds = tiles.Select(x => x.TileTemplateId).Distinct().ToList();
        var templates = approvalId is null ? await db.TileTemplates.AsNoTracking().Where(x => templateIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct) : [];
        var content = tiles.OrderBy(x => x.RowIndex).ThenBy(x => x.ColumnIndex).Select(tile =>
        {
            var objectives = requirements.Where(x => x.BoardTileId == tile.Id).OrderBy(x => x.Position).ThenBy(x => x.Id).Select(r => new BoardObjectiveContent(
                r.Id, r.Position, r.ManualObjective, r.Description, r.TargetContribution, r.DuplicatesAllowed, r.AllowHigherWeightings, r.CreditedWeight,
                bossIdsByRequirement.GetValueOrDefault(r.Id, []), drops.Where(d => d.RequirementId == r.Id).OrderBy(d => d.SourceDropId).Select(d =>
                    new BoardDropContent(d.SourceDropId, d.ItemIdSnapshot, d.BossName, d.ItemName, d.DisplayRate, d.CreditedWeight, d.MaximumContribution)).ToList())).ToList();
            var description = approvalId is null && tile.DescriptionIsAutomatic
                ? TileDescriptionFormatter.Format(objectives.Select(r => new TileDescriptionRequirement(r.Position, r.Target, r.Manual, r.Description,
                    r.Drops.Select(d => new TileDescriptionDrop(d.ItemId, d.ItemName, d.BossName)).ToList())))
                : tile.DescriptionSnapshot;
            return new BoardTileContent(tile.Id, tile.TileTemplateId, tile.RowIndex, tile.ColumnIndex, tile.NameSnapshot, description,
                tile.DescriptionIsAutomatic, artwork.GetValueOrDefault(tile.Id), tile.EstimatedEhbSnapshot,
                approvalId is null ? templates.GetValueOrDefault(tile.TileTemplateId)?.ManualEhbOverride : null,
                tile.EstimateNeedsVerification, objectives);
        }).ToList();
        var approval = approvalId is { } id ? await db.BoardApprovalSnapshots.AsNoTracking().SingleAsync(x => x.Id == id && x.BoardId == boardId, ct) : null;
        return new(boardId, approvalId, version, name, rows, columns, content, approval?.ApprovedByAccountId, approval?.ApprovedAt);
    }

    private static bool SameTile(BoardTileContent? left, BoardTileContent? right)
    {
        if (left is null || right is null) return left is null && right is null;
        // An equal-value manual override is not retained separately by approval;
        // compare effective EHB, preserving the accepted snapshot limitation.
        return JsonSerializer.Serialize(left with { ManualEhbOverride = null, EstimateNeedsVerification = false }) ==
               JsonSerializer.Serialize(right with { ManualEhbOverride = null, EstimateNeedsVerification = false });
    }

    private async Task<List<BoardRequirementSnapshot>> CurrentRequirementsAsync(List<BoardTile> tiles, int columns, CancellationToken ct)
    {
        var tileIds = tiles.Select(x => x.Id).ToList();
        var templateIds = tiles.Select(x => x.TileTemplateId).Distinct().ToList();
        // A published correction keeps the old working identity rows so that the
        // active approval remains readable while the private copy is edited.  A
        // replacement approval must nevertheless take only the current row for
        // each template position; otherwise the preserved historical row and its
        // replacement would share a position in one immutable snapshot.
        var allRequirements = await db.BoardRequirementSnapshots
            .Where(x => tileIds.Contains(x.BoardTileId))
            .OrderBy(x => x.Position)
            .ToListAsync(ct);
        var historicalRequirementIds = await db.BoardApprovalRequirementSnapshots
            .Select(x => x.BoardRequirementSnapshotId)
            .ToHashSetAsync(ct);
        var templateRequirements = await db.TileTemplateRequirements
            .Where(x => templateIds.Contains(x.TileTemplateId))
            .ToListAsync(ct);
        var requirements = new List<BoardRequirementSnapshot>();
        foreach (var tile in tiles)
        {
            var tileTemplateRequirements = templateRequirements
                .Where(x => x.TileTemplateId == tile.TileTemplateId)
                .ToDictionary(x => x.Position);
            foreach (var positionGroup in allRequirements
                         .Where(x => x.BoardTileId == tile.Id)
                         .GroupBy(x => x.Position)
                         .OrderBy(x => x.Key))
            {
                var candidates = positionGroup.ToList();
                if (candidates.Count > 1 && tileTemplateRequirements.TryGetValue(positionGroup.Key, out var templateRequirement))
                {
                    var matches = candidates.Where(x => HasTemplateRules(x, templateRequirement)).ToList();
                    if (matches.Count > 0) candidates = matches;
                }
                if (candidates.Count != 1)
                {
                    var currentCandidates = candidates.Where(x => !historicalRequirementIds.Contains(x.Id)).ToList();
                    if (currentCandidates.Count == 1) candidates = currentCandidates;
                }
                if (candidates.Count != 1)
                    throw new BoardApprovalValidationException("objective-positions", tile.Id, tile.RowIndex * columns + tile.ColumnIndex, tile.NameSnapshot, "A tile has duplicate objective positions. Edit the tile before approving it.");
                requirements.Add(candidates[0]);
            }
        }
        return requirements;
    }

    public sealed record BoardValidationIssue(string Code, Guid? TileId, int? Position, string? TileName, string ResourceKey, IReadOnlyList<object> Arguments);
    public sealed record BoardActionState(IReadOnlyList<BoardValidationIssue> Issues, BoardReadback Current);
    public sealed record BoardReadback(BoardCurrentState? State) { public bool Known => State is not null; }
    public sealed record BoardCurrentState(Guid EventId, long EventVersion, EventState EventState, Guid BoardId, BoardState State, long Version,
        bool CorrectionInProgress, Guid? ActiveApprovalId, DateTimeOffset? PublishedAt, Guid? ControllerId, DateTimeOffset? ControlExpiresAt,
        long ControlVersion, int? ExpectedTeamSize, BoardVersionContent Working, BoardVersionContent? Approved, BoardVersionContent? Published,
        BoardVersionContent? PrivateCorrection, IReadOnlyList<Guid> DifferentTileIds)
    {
        public int DifferentTileCount => DifferentTileIds.Count;
    }
    public sealed record BoardVersionContent(Guid BoardId, Guid? ApprovalId, long Version, string Name, int Rows, int Columns, IReadOnlyList<BoardTileContent> Tiles, Guid? ApprovedById, DateTimeOffset? ApprovedAt);
    public sealed record BoardTileContent(Guid TileId, Guid TemplateId, int Row, int Column, string Name, string Description, bool DescriptionIsAutomatic,
        string? ArtworkReference, decimal Ehb, decimal? ManualEhbOverride, bool EstimateNeedsVerification, IReadOnlyList<BoardObjectiveContent> Objectives);
    public sealed record BoardObjectiveContent(Guid RequirementId, int Position, bool Manual, string Description, int Target, bool DuplicatesAllowed,
        bool AllowHigherWeights, int Weight, IReadOnlyList<Guid> BossIds, IReadOnlyList<BoardDropContent> Drops);
    public sealed record BoardDropContent(Guid SourceDropId, Guid ItemId, string BossName, string ItemName, string Rate, int Weight, int? MaximumContribution);
}
