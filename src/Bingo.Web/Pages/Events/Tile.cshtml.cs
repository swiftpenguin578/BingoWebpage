using Bingo.Application.Access;
using Bingo.Application.Boards;
using Bingo.Application.Evidence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Events;

public sealed class TileModel(IPublicBoardService boards, IEvidenceAuthority evidenceAuthority, TimeProvider time) : PageModel
{
    public PublicTileDetails Tile { get; private set; } = null!;
    public Guid EventId { get; private set; }
    public Guid TeamId { get; private set; }
    public int SequenceNumber { get; private set; }
    public bool CanSubmit { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, string teamSlug, Guid tileId, CancellationToken cancellationToken)
        => await LoadAsync(slug, teamSlug, tileId, cancellationToken) ? Page() : NotFound();

    public async Task<IActionResult> OnGetSidebarAsync(string slug, string teamSlug, Guid tileId, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(slug, teamSlug, tileId, cancellationToken)) return NotFound();
        return Partial("_TileSidebar", new TileSidebarView(Tile, CanSubmit, EventId, TeamId, SequenceNumber));
    }

    private async Task<bool> LoadAsync(string slug, string teamSlug, Guid tileId, CancellationToken cancellationToken)
    {
        var board = await boards.GetEventBoardAsync(slug, cancellationToken);
        if (board is null) return false;
        var tile = await boards.GetTileAsync(slug, teamSlug, tileId, cancellationToken);
        if (tile is null) return false;
        EventId = board.EventId;
        Tile = tile;
        var team = board.Teams.SingleOrDefault(value => value.TeamSlug == teamSlug);
        if (team is null) return false;
        TeamId = team.TeamId;
        SequenceNumber = team.Tiles.ToList().FindIndex(value => value.TileId == tileId) + 1;
        CanSubmit = User.GetAccountId() is Guid accountId && await CanSubmitAsync(accountId, board.EventId, team.TeamId, cancellationToken);
        return true;
    }

    private async Task<bool> CanSubmitAsync(Guid accountId, Guid eventId, Guid teamId, CancellationToken cancellationToken)
    {
        try
        {
            var scope = await evidenceAuthority.ResolveActorAsync(accountId, eventId, teamId, time.GetUtcNow(), cancellationToken);
            return scope.Kind is EvidenceActorKind.Participant or EvidenceActorKind.Captain or EvidenceActorKind.EmergencyCaptain;
        }
        catch (InvalidOperationException) { return false; }
    }

}
