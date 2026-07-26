using Bingo.Application.Auditing;
using Bingo.Web;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Account;

[Authorize]
public sealed class LogoutModel(IAuditWriter auditWriter, IStringLocalizer<SharedResource> text) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await auditWriter.WriteAsync(
            User.GetAccountId(),
            User.Identity?.Name ?? "unknown",
            "logout",
            "account",
            User.GetAccountId()?.ToString(),
            cancellationToken: cancellationToken);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["StatusMessage"] = text["You have signed out."].Value;
        TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString();
        return RedirectToPage("/Index");
    }
}
