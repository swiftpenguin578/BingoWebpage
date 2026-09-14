using System.Text.Json;
using Bingo.Domain.Boards;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Boards;

// Detached projections of one immutable approval. Retained working drop rows supply
// identity only; their cached rules and wording never override published values.
public static class BoardPublicationQueries
{
    public static async Task<PublishedBoardData?> PublishedObjectivesAsync(this ApplicationDbContext db, Guid eventId, CancellationToken ct = default)
    {
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId && x.State == BoardState.Published, ct);
        return board?.ActiveApprovalSnapshotId is { } approvalId ? await db.ApprovalObjectivesAsync(board.Id, approvalId, ct) : null;
    }

    public static async Task<PublishedBoardData?> ApprovalObjectivesAsync(this ApplicationDbContext db, Guid boardId, Guid approvalId, CancellationToken ct = default)
    {
        var approval = await db.BoardApprovalSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == approvalId && x.BoardId == boardId, ct);
        if (approval is null) return null;
        var tiles = await db.BoardApprovalTileSnapshots.AsNoTracking().Where(x => x.ApprovalSnapshotId == approval.Id).ToListAsync(ct);
        var tileIds = tiles.Select(x => x.Id).ToList();
        var requirements = await db.BoardApprovalRequirementSnapshots.AsNoTracking().Where(x => tileIds.Contains(x.ApprovalTileSnapshotId)).ToListAsync(ct);
        if (tiles.Select(x => x.BoardTileId).Distinct().Count() != tiles.Count || requirements.Select(x => x.BoardRequirementSnapshotId).Distinct().Count() != requirements.Count) return null;
        var requirementIds = requirements.Select(x => x.Id).ToList();
        var drops = await db.BoardApprovalRequirementDropSnapshots.AsNoTracking().Where(x => requirementIds.Contains(x.ApprovalRequirementSnapshotId)).ToListAsync(ct);
        var workingIds = requirements.Select(x => x.BoardRequirementSnapshotId).ToList();
        var identities = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => workingIds.Contains(x.RequirementId)).ToListAsync(ct);
        var tileById = tiles.ToDictionary(x => x.Id);
        var requirementById = requirements.ToDictionary(x => x.Id);
        var projectedDrops = new List<BoardRequirementDropSnapshot>();
        foreach (var drop in drops)
        {
            var requirementId = requirementById[drop.ApprovalRequirementSnapshotId].BoardRequirementSnapshotId;
            var matches = identities.Where(x => x.RequirementId == requirementId && x.SourceDropId == drop.SourceDropId && x.ItemIdSnapshot == drop.ItemIdSnapshot).ToList();
            if (matches.Count != 1) return null;
            projectedDrops.Add(new(matches[0].Id, requirementId, drop.SourceDropId, drop.ItemIdSnapshot, drop.BossName, drop.ItemName,
                drop.DisplayRate, drop.NumericProbability, drop.MaximumContribution, drop.EhbPerContribution, drop.CreditedWeight));
        }
        if (projectedDrops.Select(x => x.Id).Distinct().Count() != projectedDrops.Count) return null;
        return new(approval,
            tiles.Select(x => new BoardTile(x.BoardTileId, boardId, x.TileTemplateId, x.RowIndex, x.ColumnIndex, x.Name, x.Description, x.EvidenceInstructions, x.EstimatedEhb)).ToList(),
            requirements.Select(x => new BoardRequirementSnapshot(x.BoardRequirementSnapshotId, tileById[x.ApprovalTileSnapshotId].BoardTileId, x.Position,
                x.TargetContribution, x.DuplicatesAllowed, x.AllowHigherWeightings, x.Description, x.ManualObjective, x.CreditedWeight)).ToList(), projectedDrops);
    }

    public static async Task<HashSet<Guid>> EvidencedObjectiveIdsAsync(this ApplicationDbContext db, Guid eventId, CancellationToken ct = default)
    {
        // Pending metadata corrections may move the current target. The immutable
        // review snapshots retain the earlier reference, which must remain locked.
        var ids = await db.Submissions.Where(x => x.EventId == eventId).Select(x => x.RequirementId).ToHashSetAsync(ct);
        var history = await (from action in db.ReviewActions.AsNoTracking()
                             join submission in db.Submissions.AsNoTracking() on action.SubmissionId equals submission.Id
                             where submission.EventId == eventId
                             select new { action.BeforeSnapshot, action.AfterSnapshot }).ToListAsync(ct);
        foreach (var json in history.SelectMany(x => new[] { x.BeforeSnapshot, x.AfterSnapshot }).Where(x => x != null))
        {
            using var document = JsonDocument.Parse(json!);
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("RequirementId", out var value) && value.TryGetGuid(out var id)) ids.Add(id);
        }
        return ids;
    }
}

public sealed record PublishedBoardData(BoardApprovalSnapshot Approval, IReadOnlyList<BoardTile> Tiles,
    IReadOnlyList<BoardRequirementSnapshot> Requirements, IReadOnlyList<BoardRequirementDropSnapshot> Drops);
