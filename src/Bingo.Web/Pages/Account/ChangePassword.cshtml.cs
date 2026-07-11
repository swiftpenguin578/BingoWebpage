using System.ComponentModel.DataAnnotations;
using Bingo.Application.Auditing;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Account;

[Authorize]
public sealed class ChangePasswordModel(
    ApplicationDbContext dbContext,
    IPasswordHasher<Bingo.Domain.Access.Account> passwordHasher,
    IAuditWriter auditWriter) : PageModel
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

        if (passwordHasher.VerifyHashedPassword(account, account.PasswordHash, Input.CurrentPassword) ==
            PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "The current password is incorrect.");
            return Page();
        }

        account.SetPasswordHash(passwordHasher.HashPassword(account, Input.NewPassword), mustChangePassword: false);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            account.Id,
            account.Username,
            "account.password_changed",
            "account",
            account.Id.ToString(),
            cancellationToken: cancellationToken);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["StatusMessage"] = "Your password was changed. Sign in with the new password.";
        return RedirectToPage("/Account/Login");
    }

    public sealed class PasswordInput
    {
        [Required, DataType(DataType.Password), Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, StringLength(200, MinimumLength = 12), DataType(DataType.Password), Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required, Compare(nameof(NewPassword)), DataType(DataType.Password), Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
