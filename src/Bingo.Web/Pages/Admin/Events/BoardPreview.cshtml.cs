using Bingo.Application.Access;
using Bingo.Domain.Boards;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class BoardPreviewModel(ApplicationDbContext db) : PageModel
{
    public string EventName { get; private set; } = string.Empty;
    public int Rows { get; private set; }
    public int Columns { get; private set; }
    public IReadOnlyList<PreviewTile> Tiles { get; private set; } = [];
    public IReadOnlyList<PreviewTeam> Teams { get; private set; } = [];
    public PreviewTeam? SelectedTeam { get; private set; }
    public PreviewTile? SelectedTile { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, string? teamSlug, Guid? tileId, CancellationToken ct)
    {
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct);
        var bingoEvent = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (board is null || bingoEvent is null) return NotFound();
        EventName = bingoEvent.Name;
        Rows = board.Rows;
        Columns = board.Columns;

        if (board.State == BoardState.Validated && board.ActiveApprovalSnapshotId is { } approvalId)
        {
            var approval = await db.BoardApprovalSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == approvalId && x.BoardId == board.Id, ct);
            if (approval is null) return NotFound();
            Rows = approval.Rows;
            Columns = approval.Columns;
            var tiles = await db.BoardApprovalTileSnapshots.AsNoTracking().Where(x => x.ApprovalSnapshotId == approval.Id).OrderBy(x => x.RowIndex).ThenBy(x => x.ColumnIndex).ToListAsync(ct);
            var ids = tiles.Select(x => x.Id).ToList();
            var requirements = await db.BoardApprovalRequirementSnapshots.AsNoTracking().Where(x => ids.Contains(x.ApprovalTileSnapshotId)).ToListAsync(ct);
            Tiles = tiles.Select((tile, index) => new PreviewTile(tile.Id, tile.RowIndex, tile.ColumnIndex, tile.Name, Math.Max(1, requirements.Where(x => x.ApprovalTileSnapshotId == tile.Id).Sum(x => x.TargetContribution)), DemonstrationProgress(index, requirements.Where(x => x.ApprovalTileSnapshotId == tile.Id).Sum(x => x.TargetContribution)))).ToList();
        }
        else
        {
            var tiles = await db.BoardTiles.AsNoTracking().Where(x => x.BoardId == board.Id).OrderBy(x => x.RowIndex).ThenBy(x => x.ColumnIndex).ToListAsync(ct);
            var ids = tiles.Select(x => x.Id).ToList();
            var requirements = await db.BoardRequirementSnapshots.AsNoTracking().Where(x => ids.Contains(x.BoardTileId)).ToListAsync(ct);
            Tiles = tiles.Select((tile, index) => new PreviewTile(tile.Id, tile.RowIndex, tile.ColumnIndex, tile.NameSnapshot, Math.Max(1, requirements.Where(x => x.BoardTileId == tile.Id).Sum(x => x.TargetContribution)), DemonstrationProgress(index, requirements.Where(x => x.BoardTileId == tile.Id).Sum(x => x.TargetContribution)))).ToList();
        }

        Teams = [new("preview-alpha", "Preview team Alpha", 1, "3 tiles · 1 line"), new("preview-bravo", "Preview team Bravo", 2, "1 tile · in progress")];
        if (!string.IsNullOrWhiteSpace(teamSlug))
        {
            SelectedTeam = Teams.SingleOrDefault(team => team.Slug.Equals(teamSlug, StringComparison.OrdinalIgnoreCase));
            if (SelectedTeam is null) return NotFound();
            if (tileId is { } selectedTileId)
            {
                SelectedTile = Tiles.SingleOrDefault(tile => tile.Id == selectedTileId);
                if (SelectedTile is null) return NotFound();
            }
        }
        return Page();
    }

    private static int DemonstrationProgress(int index, int target) => index switch { 0 => Math.Max(1, target), 1 => Math.Max(1, target / 2), _ => 0 };
    public sealed record PreviewTile(Guid Id, int Row, int Column, string Name, int Target, int Approved);
    public sealed record PreviewTeam(string Slug, string Name, int Rank, string Summary);
}
