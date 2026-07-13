using Bingo.Application.Boards;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Events;

public sealed class TileModel(IPublicBoardService boards) : PageModel
{
    public PublicTileDetails Tile { get; private set; } = null!;
    public Guid EventId { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, string teamSlug, Guid tileId, CancellationToken cancellationToken)
    {
        var board = await boards.GetEventBoardAsync(slug, cancellationToken);
        if (board is null) return NotFound();
        var tile = await boards.GetTileAsync(slug, teamSlug, tileId, cancellationToken);
        if (tile is null) return NotFound();
        EventId = board.EventId;
        Tile = tile;
        return Page();
    }
}
