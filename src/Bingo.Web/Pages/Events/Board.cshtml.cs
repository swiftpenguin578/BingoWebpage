using Bingo.Application.Boards;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Events;

public sealed class BoardModel(IPublicBoardService boards) : PageModel
{
    public PublicEventBoard Board { get; private set; } = null!;
    public string ActiveView { get; private set; } = "mission";
    public string ActiveRanking { get; private set; } = "drops";

    public async Task<IActionResult> OnGetAsync(string slug, string? view, string? ranking, CancellationToken cancellationToken)
    {
        var board = await boards.GetEventBoardAsync(slug, cancellationToken);
        if (board is null) return NotFound();
        Board = board;
        ActiveView = view is "drops" or "leaderboards" ? view : "mission";
        ActiveRanking = ranking == "activity" ? "activity" : "drops";
        return Page();
    }
}
