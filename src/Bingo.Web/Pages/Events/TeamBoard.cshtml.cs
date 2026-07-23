using Bingo.Application.Boards;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Events;

public sealed class TeamBoardModel(IPublicBoardService boards) : PageModel
{
    public PublicEventBoard Board { get; private set; } = null!;
    public PublicTeamBoard Team { get; private set; } = null!;
    public PublicTeamBoard? Previous { get; private set; }
    public PublicTeamBoard? Next { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, string teamSlug, CancellationToken cancellationToken)
    {
        var board = await boards.GetEventBoardAsync(slug, cancellationToken);
        if (board is null) return NotFound();
        var index = board.Teams.ToList().FindIndex(value => value.TeamSlug == teamSlug);
        if (index < 0) return NotFound();
        Board = board;
        Team = board.Teams[index];
        Previous = index > 0 ? board.Teams[index - 1] : null;
        Next = index + 1 < board.Teams.Count ? board.Teams[index + 1] : null;
        ViewData["BodyClass"] = "public-event-shell";
        ViewData["CompactNavigation"] = true;
        ViewData["HideBreadcrumbs"] = true;
        return Page();
    }
}
