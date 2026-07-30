using Bingo.Domain.Boards;
using Bingo.Infrastructure.Persistence;

namespace Bingo.IntegrationTests;

/// <summary>
/// Small fixture helper for legacy integration scenarios which need a real Slice 6
/// approval tree before publishing their board. It deliberately copies the fixture
/// rows the same way production freezes public board content.
/// </summary>
internal static class BoardApprovalFixture
{
    public static async Task PublishAsync(
        ApplicationDbContext db,
        Board board,
        DateTimeOffset approvedAt,
        IEnumerable<BoardTile>? tiles = null,
        IEnumerable<BoardRequirementSnapshot>? requirements = null,
        IEnumerable<BoardRequirementDropSnapshot>? drops = null,
        CancellationToken cancellationToken = default)
    {
        var frozenTiles = (tiles ?? []).ToList();
        var frozenRequirements = (requirements ?? []).ToList();
        var frozenDrops = (drops ?? []).ToList();
        // Persist the draft rows first. The production model intentionally has FKs in
        // both directions (board -> active approval and approval -> board), so a new
        // board cannot be created and pointed at a new approval in one EF save.
        await db.SaveChangesAsync(cancellationToken);
        var approval = new BoardApprovalSnapshot(
            Guid.NewGuid(), board.Id, 1, approvedAt, null, null, board.Name,
            board.Rows, board.Columns, board.TotalEhbEstimate, board.CalculationVersion,
            board.Version, BoardState.Validated);

        db.BoardApprovalSnapshots.Add(approval);
        var approvalTiles = new Dictionary<Guid, BoardApprovalTileSnapshot>();
        foreach (var tile in frozenTiles)
        {
            var frozenTile = new BoardApprovalTileSnapshot(
                Guid.NewGuid(), approval.Id, tile.Id, tile.TileTemplateId,
                tile.RowIndex, tile.ColumnIndex, tile.NameSnapshot, tile.DescriptionSnapshot,
                tile.EvidenceInstructionsSnapshot, tile.EstimatedEhbSnapshot, null);
            approvalTiles.Add(tile.Id, frozenTile);
            db.BoardApprovalTileSnapshots.Add(frozenTile);
        }

        var approvalRequirements = new Dictionary<Guid, BoardApprovalRequirementSnapshot>();
        foreach (var requirement in frozenRequirements)
        {
            var frozenRequirement = new BoardApprovalRequirementSnapshot(
                Guid.NewGuid(), approvalTiles[requirement.BoardTileId].Id, requirement.Id,
                requirement.Position, requirement.TargetContribution, requirement.DuplicatesAllowed,
                requirement.AllowHigherWeightings, requirement.CreditedWeight,
                requirement.Description, requirement.ManualObjective);
            approvalRequirements.Add(requirement.Id, frozenRequirement);
            db.BoardApprovalRequirementSnapshots.Add(frozenRequirement);
        }

        foreach (var drop in frozenDrops)
        {
            db.BoardApprovalRequirementDropSnapshots.Add(new BoardApprovalRequirementDropSnapshot(
                Guid.NewGuid(), approvalRequirements[drop.RequirementId].Id, drop.SourceDropId,
                drop.BossName, drop.ItemName, drop.DisplayRate, drop.NumericProbability,
                drop.MaximumContribution, drop.EhbPerContribution, drop.CreditedWeight, 1));
        }

        board.Approve(approval.Id);
        board.Publish(approvedAt);
        await db.SaveChangesAsync(cancellationToken);
    }
}
