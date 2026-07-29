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
    IStringLocalizer<SharedResource> text,
    ILogger<SettingsModel> logger) : PageModel
{
    public bool DiscordLinked { get; private set; }
    public bool IsWebsiteAccount { get; private set; }
    [BindProperty, Required(ErrorMessage = "A password is required."), DataType(DataType.Password), Display(Name = "Current password")] public string CurrentPassword { get; set; } = string.Empty;
    [BindProperty] public RenameUsernameInput Rename { get; set; } = new();

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

    public async Task<IActionResult> OnPostRenameUsernameAsync(CancellationToken ct)
    {
        KeepValidationFor(nameof(Rename));
        if (!ModelState.IsValid) return await ReloadAsync(ct);

        UsernameRenameResult result;
        try
        {
            result = await identities.RenameUsernameAsync(AccountId, Rename.Username, Rename.CurrentPassword, ct);
        }
        catch (Exception exception)
        {
            ModelState.AddModelError(string.Empty, SafeUserFailure.Message(text, logger, exception));
            return await ReloadAsync(ct);
        }
        switch (result)
        {
            case UsernameRenameResult.Success:
                var account = await db.Accounts.SingleAsync(x => x.Id == AccountId, ct);
                var ticket = await HttpContext.AuthenticateAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
                var method = User.FindFirst(Bingo.Application.Access.AccountClaims.AuthenticationMethod)?.Value ?? "password";
                await HttpContext.SignInAsync(
                    Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme,
                    authentication.CreatePrincipal(account, method),
                    ticket.Properties);
                TempData["StatusMessage"] = text["Your website username was changed."].Value;
                TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString();
                return RedirectToPage();
            case UsernameRenameResult.WrongPassword:
                ModelState.AddModelError("Rename.CurrentPassword", text["The current password is incorrect."]);
                break;
            case UsernameRenameResult.UsernameTaken:
                ModelState.AddModelError("Rename.Username", text["That website username is already in use."]);
                break;
            case UsernameRenameResult.ConcurrencyConflict:
                ModelState.AddModelError(string.Empty, text["Your username change conflicted with another update. Please reload and try again."]);
                break;
            case UsernameRenameResult.InvalidUsername:
                ModelState.AddModelError("Rename.Username", text["A public username is required."]);
                break;
            default:
                ModelState.AddModelError(string.Empty, text["We could not complete that request. Please try again or contact an administrator."]);
                break;
        }
        return await ReloadAsync(ct);
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
        IsWebsiteAccount = account.AccountType == Bingo.Domain.Access.AccountType.WebsiteAccount;
        return true;
    }

    private Guid AccountId => User.GetAccountId() ?? throw new InvalidOperationException("An authenticated account is required.");

    private void KeepValidationFor(string prefix)
    {
        foreach (var key in ModelState.Keys.Where(key => !key.StartsWith($"{prefix}.", StringComparison.Ordinal)).ToList())
            ModelState.Remove(key);
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct) => await Load(ct) ? Page() : Challenge();

    public sealed class RenameUsernameInput
    {
        [Required(ErrorMessage = "A public username is required."), StringLength(100), Display(Name = "Website username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "A password is required."), DataType(DataType.Password), Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;
    }
}
