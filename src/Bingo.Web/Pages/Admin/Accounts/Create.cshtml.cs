using System.ComponentModel.DataAnnotations;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Accounts;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class CreateModel(
    ApplicationDbContext dbContext,
    IPasswordHasher<Bingo.Domain.Access.Account> passwordHasher,
    IAuditWriter auditWriter,
    TimeProvider timeProvider) : PageModel
{
    [BindProperty]
    public CreateInput Input { get; set; } = new();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        ValidateCaptainScope();
        var normalizedUsername = AccountAuthenticationService.NormalizeUsername(Input.Username);
        if (await dbContext.Accounts.AnyAsync(account => account.NormalizedUsername == normalizedUsername, cancellationToken))
        {
            ModelState.AddModelError("Input.Username", "That username is already in use.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var account = new Bingo.Domain.Access.Account(
            Guid.NewGuid(),
            Input.Username.Trim(),
            normalizedUsername,
            Input.Role,
            timeProvider.GetUtcNow());
        if (Input.Role == AccountRole.Captain)
        {
            account.ScopeCaptain(
                Input.EventId!.Value,
                Input.TeamId!.Value,
                Input.ActiveFrom,
                Input.CorrectionOnlyFrom,
                Input.ExpiresAt);
        }

        account.SetPasswordHash(passwordHasher.HashPassword(account, Input.Password), Input.MustChangePassword);
        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(
            User.GetAccountId(),
            User.Identity?.Name ?? "unknown",
            "account.created",
            "account",
            account.Id.ToString(),
            $"Username: {account.Username}; Role: {account.Role}",
            cancellationToken);
        TempData["StatusMessage"] = $"Created {account.Username}.";
        return RedirectToPage("Index");
    }

    private void ValidateCaptainScope()
    {
        if (Input.Role != AccountRole.Captain)
        {
            return;
        }

        if (Input.EventId is null)
        {
            ModelState.AddModelError("Input.EventId", "An event is required for a captain.");
        }

        if (Input.TeamId is null)
        {
            ModelState.AddModelError("Input.TeamId", "A team is required for a captain.");
        }

        if (Input.CorrectionOnlyFrom is not null && Input.ActiveFrom is not null && Input.CorrectionOnlyFrom < Input.ActiveFrom)
        {
            ModelState.AddModelError("Input.CorrectionOnlyFrom", "Correction-only access cannot begin before activation.");
        }

        if (Input.ExpiresAt is not null && Input.ActiveFrom is not null && Input.ExpiresAt <= Input.ActiveFrom)
        {
            ModelState.AddModelError("Input.ExpiresAt", "Expiry must be after activation.");
        }
    }

    public sealed class CreateInput
    {
        [Required, StringLength(100, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Required, StringLength(200, MinimumLength = 12), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public AccountRole Role { get; set; } = AccountRole.Captain;

        [Display(Name = "Event ID")]
        public Guid? EventId { get; set; }

        [Display(Name = "Team ID")]
        public Guid? TeamId { get; set; }

        [DataType(DataType.DateTime), Display(Name = "Active from")]
        public DateTimeOffset? ActiveFrom { get; set; }

        [DataType(DataType.DateTime), Display(Name = "Correction-only from")]
        public DateTimeOffset? CorrectionOnlyFrom { get; set; }

        [DataType(DataType.DateTime), Display(Name = "Expires at")]
        public DateTimeOffset? ExpiresAt { get; set; }

        [Display(Name = "Require password change")]
        public bool MustChangePassword { get; set; } = true;
    }
}
