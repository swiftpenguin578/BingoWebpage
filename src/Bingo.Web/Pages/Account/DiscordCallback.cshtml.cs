using System.Security.Claims;
using Bingo.Web;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
namespace Bingo.Web.Pages.Account;

public sealed class DiscordCallbackModel(
    Bingo.Infrastructure.Persistence.ApplicationDbContext db,
    AccountAuthenticationService authentication,
    AccountIdentityService identities,
    DiscordLinkStateService linkState,
    DiscordOnboardingStateService onboardingState,
    IStringLocalizer<SharedResource> text,
    ILogger<DiscordCallbackModel> logger) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string? returnUrl, CancellationToken ct)
    {
        var result = await HttpContext.AuthenticateAsync("Discord.External");
        var discordId = result.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        string? purpose = null;
        string? accountIdText = null;
        string? linkStateText = null;
        result.Properties?.Items.TryGetValue("discord-purpose", out purpose);
        result.Properties?.Items.TryGetValue("discord-account-id", out accountIdText);
        result.Properties?.Items.TryGetValue("discord-link-state", out linkStateText);
        await HttpContext.SignOutAsync("Discord.External");

        if (!result.Succeeded || string.IsNullOrWhiteSpace(discordId))
        {
            TempData["StatusMessage"] = text["Discord sign-in was cancelled or failed. Please try again."].Value;
            return RedirectToPage(purpose is "link" or "replace" ? "Settings" : "Login");
        }

        if (purpose is "link" or "replace")
        {
            if (!Guid.TryParse(accountIdText, out var accountId) || User.GetAccountId() != accountId || !linkState.TryConsumeForCallback(linkStateText, accountId, purpose))
            {
                TempData["StatusMessage"] = text["Your Discord linking session expired. Please try again."].Value;
                return RedirectToPage("Settings");
            }

            var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == accountId, ct);
            if (account is null || !account.Active)
            {
                TempData["StatusMessage"] = text["Your account is no longer available for Discord linking."].Value;
                return RedirectToPage("Settings");
            }

            try
            {
                await identities.SetDiscordAsync(account, discordId, result.Principal?.Identity?.Name, purpose == "replace" ? "replaced" : "linked", ct);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, authentication.CreatePrincipal(account, "discord"));
                TempData["StatusMessage"] = purpose == "replace" ? text["Your linked Discord account was replaced."].Value : text["Your Discord account is linked."].Value;
            }
            catch (InvalidOperationException exception)
            {
                TempData["StatusMessage"] = SafeUserFailure.Message(text, logger, exception);
            }

            return RedirectToPage("Settings");
        }

        var existing = await db.Accounts.SingleOrDefaultAsync(x => x.DiscordUserId == discordId, ct);
        if (existing is not null)
        {
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, authentication.CreatePrincipal(existing, "discord"));
            TempData["StatusMessage"] = text["Signed in with Discord."].Value;
            TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString();
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
        }

        onboardingState.Issue(HttpContext.Response, discordId, result.Principal?.Identity?.Name, Url.IsLocalUrl(returnUrl) ? returnUrl : null);
        return RedirectToPage("Onboarding");
    }
}
