using System.ComponentModel.DataAnnotations;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
namespace Bingo.Web.Pages.Account;

[Authorize]
public sealed class SettingsModel(
    ApplicationDbContext db,
    IPasswordHasher<Bingo.Domain.Access.Account> passwords,
    AccountIdentityService identities,
    AccountAuthenticationService authentication,
    DiscordLinkStateService discordLinkState,
    IStringLocalizer<SharedResource> text) : PageModel
{
    public bool DiscordLinked { get; private set; }
    [BindProperty, Required(ErrorMessage = "A password is required."), DataType(DataType.Password), Display(Name = "Current password")] public string CurrentPassword { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct) => await Load(ct) ? Page() : Challenge();

    public async Task<IActionResult> OnPostUnlinkAsync(CancellationToken ct)
    {
        var account = await Verified(ct);
        if (account is null) return await Invalid(ct);
        await identities.RemoveDiscordAsync(account, ct);
        await HttpContext.SignInAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, authentication.CreatePrincipal(account, "password"));
        TempData["StatusMessage"] = text["Your Discord account was unlinked."].Value;
        TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostBeginLinkAsync(CancellationToken ct)
    {
        var account = await Verified(ct);
        if (account is null) return await Invalid(ct);
        var purpose = DiscordLinked ? "replace" : "link";
        return RedirectToPage("DiscordLogin", new { purpose, accountId = account.Id, state = discordLinkState.Create(account.Id, purpose) });
    }

    private async Task<Bingo.Domain.Access.Account?> Verified(CancellationToken ct)
    {
        var account = await db.Accounts.SingleAsync(x => x.Id == User.GetAccountId(), ct);
        return account.PasswordHash is not null && passwords.VerifyHashedPassword(account, account.PasswordHash, CurrentPassword) != PasswordVerificationResult.Failed ? account : null;
    }

    private async Task<IActionResult> Invalid(CancellationToken ct)
    {
        ModelState.AddModelError(string.Empty, text["The current password is incorrect."]);
        await Load(ct);
        return Page();
    }

    private async Task<bool> Load(CancellationToken ct)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == User.GetAccountId(), ct);
        if (account is null) return false;
        DiscordLinked = account.DiscordUserId is not null;
        return true;
    }
}
