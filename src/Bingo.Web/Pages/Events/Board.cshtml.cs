using Bingo.Application.Boards;
using Bingo.Application.Integrations.WiseOldMan;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Events;

public sealed class BoardModel(
    IPublicBoardService boards,
    IEventCompetitionActivityProjection activity) : PageModel
{
    public PublicEventBoard Board { get; private set; } = null!;
    public EventCompetitionActivityProjection Activity { get; private set; } = null!;
    public string ActiveView { get; private set; } = "mission";
    public string ActiveRanking { get; private set; } = "drops";

    public async Task<IActionResult> OnGetAsync(string slug, string? view, string? ranking, CancellationToken cancellationToken)
    {
        var board = await boards.GetEventBoardAsync(slug, cancellationToken);
        if (board is null) return NotFound();
        Board = board;
        Activity = await activity.GetAsync(board.EventId, cancellationToken);
        ActiveView = view is "drops" or "leaderboards" ? view : "mission";
        ActiveRanking = ranking == "activity" ? "activity" : "drops";
        return Page();
    }
}
