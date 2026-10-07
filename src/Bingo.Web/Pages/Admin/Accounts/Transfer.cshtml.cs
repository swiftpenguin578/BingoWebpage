using Bingo.Application.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Admin.Accounts;

// Retired page: ownership transfer is the Transfer dialog on the directory (WA-5).
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
public sealed class TransferModel : PageModel
{
    public override void OnPageHandlerExecuting(Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context)
    {
        if (context.HandlerMethod is null) context.Result = NotFound();
    }

    public IActionResult OnGet() => Redirect("/Admin/Accounts");
}
