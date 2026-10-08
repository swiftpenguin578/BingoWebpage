using Bingo.Application.Access;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Accounts;

// Retired page: the account drawer on the directory replaced it (WA-5). Old links open the
// drawer; unknown or non-website accounts stay Not Found and no handler is accepted here.
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class ManageModel(ApplicationDbContext db) : PageModel
{
    public override void OnPageHandlerExecuting(Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context)
    {
        if (context.HandlerMethod is null) context.Result = NotFound();
    }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) =>
        await db.Accounts.AsNoTracking().AnyAsync(account => account.Id == id && account.AccountType == AccountType.WebsiteAccount, ct)
            ? Redirect($"/Admin/Accounts?account={id}")
            : NotFound();
}
