using System.ComponentModel.DataAnnotations;
using Bingo.Application.Auditing;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Account;

[Authorize]
public sealed class ChangePasswordModel(
    ApplicationDbContext dbContext,
    IPasswordHasher<Bingo.Domain.Access.Account> passwordHasher,
    AccountIdentityService identities,
    AccountAuthenticationService authentication,
    TimeProvider time,
    IStringLocalizer<SharedResource> text,
    ILogger<ChangePasswordModel> logger) : PageModel
{
    [BindProperty]
    public PasswordInput Input { get; set; } = new();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var accountId = User.GetAccountId();
        var account = await dbContext.Accounts.SingleOrDefaultAsync(
            candidate => candidate.Id == accountId,
            cancellationToken);
        if (account is null)
        {
            return Challenge();
        }

        if (account.PasswordHash is null || passwordHasher.VerifyHashedPassword(account, account.PasswordHash, Input.CurrentPassword) ==
            PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, text["The current password is incorrect."]);
            return Page();
        }

        try
        {
            await identities.ChangePasswordAsync(account, Input.NewPassword, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("Input.NewPassword", SafeUserFailure.Message(text, logger, exception));
            return Page();
        }
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, authentication.CreatePrincipal(account, "password"), CreatePasswordSessionProperties(false, time.GetUtcNow()));
        TempData["StatusMessage"] = text["Your password was changed."].Value;
        TempData[Bingo.Web.UI.UiMessage.TypeKey] = Bingo.Web.UI.UiMessageType.Success.ToString();
        return RedirectToPage("Settings");
    }

    public static AuthenticationProperties CreatePasswordSessionProperties(bool rememberMe, DateTimeOffset issuedAt) => new()
    {
        IsPersistent = rememberMe,
        IssuedUtc = issuedAt,
        ExpiresUtc = issuedAt.Add(rememberMe ? TimeSpan.FromDays(30) : TimeSpan.FromHours(12)),
        AllowRefresh = false
    };

    public sealed class PasswordInput
    {
        [Required(ErrorMessage = "A password is required."), DataType(DataType.Password), Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "A password is required."), StringLength(200, MinimumLength = 10, ErrorMessage = "Passwords must be between 10 and 200 characters."), DataType(DataType.Password), Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm your password."), Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match."), DataType(DataType.Password), Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
