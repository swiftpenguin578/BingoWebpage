using Bingo.Application.Access;
using Bingo.Application.Dashboard;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Admin;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class IndexModel(IAdminDashboardService dashboard) : PageModel
{
    public AdminDashboardResult? Dashboard { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (User.GetAccountId() is not { } actorId) return Forbid();
        try
        {
            Dashboard = await dashboard.GetAsync(actorId, cancellationToken);
            return Page();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
