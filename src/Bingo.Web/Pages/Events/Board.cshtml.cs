using Bingo.Application.Boards;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Events;

public sealed class BoardModel(IPublicBoardService boards) : PageModel
{
    public PublicEventBoard Board { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        var board = await boards.GetEventBoardAsync(slug, cancellationToken);
        if (board is null) return NotFound();
        Board = board;
        return Page();
    }
}
