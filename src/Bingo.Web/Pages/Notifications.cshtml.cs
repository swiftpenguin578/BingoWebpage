using Bingo.Web.Navigation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages;

[Authorize]
public sealed class NotificationsModel(SharedShellService shell) : PageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var inbox = await shell.GetNotificationsAsync(User, cancellationToken);
        return new JsonResult(inbox);
    }
}
