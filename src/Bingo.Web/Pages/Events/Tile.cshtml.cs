using Bingo.Application.Access;
using Bingo.Application.Boards;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Events;

public sealed class TileModel(IPublicBoardService boards, IAuthorizationService authorization) : PageModel
{
    public PublicTileDetails Tile { get; private set; } = null!;
    public Guid EventId { get; private set; }
    public bool CanSubmit { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, string teamSlug, Guid tileId, CancellationToken cancellationToken)
        => await LoadAsync(slug, teamSlug, tileId, cancellationToken) ? Page() : NotFound();

    public async Task<IActionResult> OnGetSidebarAsync(string slug, string teamSlug, Guid tileId, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(slug, teamSlug, tileId, cancellationToken)) return NotFound();
        return Partial("_TileSidebar", new TileSidebarView(Tile, CanSubmit));
    }

    private async Task<bool> LoadAsync(string slug, string teamSlug, Guid tileId, CancellationToken cancellationToken)
    {
        var board = await boards.GetEventBoardAsync(slug, cancellationToken);
        if (board is null) return false;
        var tile = await boards.GetTileAsync(slug, teamSlug, tileId, cancellationToken);
        if (tile is null) return false;
        EventId = board.EventId;
        Tile = tile;
        var team = board.Teams.Single(value => value.TeamSlug == teamSlug);
        CanSubmit = User.GetEventId() == board.EventId && User.GetTeamId() == team.TeamId &&
            (await authorization.AuthorizeAsync(User, AuthorizationPolicies.CaptainFullAccess)).Succeeded;
        return true;
    }
}
