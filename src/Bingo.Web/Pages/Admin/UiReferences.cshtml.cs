using Bingo.Application.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Admin;

// U10 part 2 (user ruling, 8 October 2026): the temporary reference gallery and its image
// handler are retired. The route (any handler) redirects to the Admin dashboard. No reads.
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class UiReferencesModel : PageModel
{
    public IActionResult OnGet() => Redirect("/Admin");
}
