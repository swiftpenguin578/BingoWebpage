using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Captain;

[Authorize]
public sealed class IndexModel : PageModel
{
    public IActionResult OnGet(Guid? eventId, Guid? teamId, string? search, Guid? player, int? ledgerPage = null)
        => RedirectToPage("/Submissions/Index", new { eventId, teamId, search, player, ledgerPage });

    public IActionResult OnGetLedger(Guid? eventId, Guid? teamId, string? search, Guid? player, int? ledgerPage = null)
        => RedirectToPage("/Submissions/Index", new { eventId, teamId, search, player, ledgerPage });
}
