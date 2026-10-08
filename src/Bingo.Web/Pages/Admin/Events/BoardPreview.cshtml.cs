using Bingo.Application.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Admin.Events;

// U7-Q3 (corrected, brief 88): the legacy BoardPreview page with demonstration teams
// and progress is retired. Its route, with or without team/tile segments, redirects to
// the Board, whose Preview button opens the placeholder preview. No reads, no writes.
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class BoardPreviewModel : PageModel
{
    public IActionResult OnGet(Guid id) => RedirectToPage("Board", new { id });
}
