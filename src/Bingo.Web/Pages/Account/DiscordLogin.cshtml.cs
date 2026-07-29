using Bingo.Web;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
namespace Bingo.Web.Pages.Account;

public sealed class DiscordLoginModel(IOptions<DiscordAuthenticationOptions> options, DiscordLinkStateService linkState, IStringLocalizer<SharedResource> text) : PageModel
{
    public IActionResult OnGet(string? returnUrl, string? purpose, Guid? accountId, string? state)
    {
        if (!options.Value.IsConfigured)
        {
            TempData["StatusMessage"] = text["Discord sign-in is not configured for this environment."].Value;
            return RedirectToPage("Login");
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Page("DiscordComplete", new { returnUrl })
        };

        if (purpose is "link" or "replace" && accountId is { } id)
        {
            if (User.GetAccountId() != id || !linkState.TryConsumeForChallenge(state, id, purpose))
            {
                TempData["StatusMessage"] = text["Your Discord linking session expired. Please try again."].Value;
                return RedirectToPage("Settings");
            }
            properties.Items["discord-purpose"] = purpose;
            properties.Items["discord-account-id"] = id.ToString();
            properties.Items["discord-link-state"] = state;
        }
        else if (purpose is not null)
        {
            TempData["StatusMessage"] = text["Your Discord linking session expired. Please try again."].Value;
            return RedirectToPage("Settings");
        }

        return Challenge(properties, "Discord");
    }
}
