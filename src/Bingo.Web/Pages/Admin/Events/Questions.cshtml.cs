using Bingo.Application.Access;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace Bingo.Web.Pages.Admin.Events;
// C4: historical URL only. The global event filter preserves hidden-event 404 and D16 POST refusal.
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class QuestionsModel : PageModel
{
 public IActionResult OnGet(Guid id) => RedirectToPage("SignupSetup", new { id, tab = "form" });
 public IActionResult OnPost(Guid id) => RedirectToPage("SignupSetup", new { id, tab = "form" });
}
