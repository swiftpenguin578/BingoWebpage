using Bingo.Application.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Admin.Accounts;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class CreateModel : PageModel
{
    public IActionResult OnGet() => NotFound();
    public IActionResult OnPost() => NotFound();
}
