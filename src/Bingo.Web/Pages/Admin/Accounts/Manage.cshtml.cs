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
public sealed class ManageModel(
    ApplicationDbContext dbContext,
    IPasswordHasher<Bingo.Domain.Access.Account> passwordHasher,
    IAuditWriter auditWriter,
    TimeProvider timeProvider) : PageModel
{
    public AccountDetails? AccountView { get; private set; }

    [BindProperty, StringLength(200, MinimumLength = 12), DataType(DataType.Password), Display(Name = "Temporary password")]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty, Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    [BindProperty, DataType(DataType.DateTime), Display(Name = "New expiry (optional)")]
    public DateTimeOffset? NewExpiry { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken) =>
        await LoadAsync(id, cancellationToken) ? Page() : NotFound();

    public async Task<IActionResult> OnPostResetPasswordAsync(Guid id, CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(Reason));
        if (!ModelState.IsValid)
        {
            await LoadAsync(id, cancellationToken);
            return Page();
        }

        var account = await FindAsync(id, cancellationToken);
        if (account is null) return NotFound();
        account.SetPasswordHash(passwordHasher.HashPassword(account, NewPassword), mustChangePassword: true);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("account.password_reset", account, null, cancellationToken);
        TempData["StatusMessage"] = $"Reset the password for {account.Username}.";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostDisableAsync(Guid id, CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(NewPassword));
        if (!ModelState.IsValid)
        {
            await LoadAsync(id, cancellationToken);
            return Page();
        }

        if (User.GetAccountId() == id)
        {
            ModelState.AddModelError(string.Empty, "You cannot disable the account you are currently using.");
            await LoadAsync(id, cancellationToken);
            return Page();
        }

        var account = await FindAsync(id, cancellationToken);
        if (account is null) return NotFound();
        account.Disable(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("account.disabled", account, Reason, cancellationToken);
        TempData["StatusMessage"] = $"Disabled {account.Username}.";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostEnableAsync(Guid id, CancellationToken cancellationToken)
    {
        ModelState.Remove(nameof(Reason));
        ModelState.Remove(nameof(NewPassword));
        var account = await FindAsync(id, cancellationToken);
        if (account is null) return NotFound();
        account.Enable(NewExpiry);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditAsync("account.enabled", account, NewExpiry is null ? "No automatic expiry" : $"Expires: {NewExpiry:O}", cancellationToken);
        TempData["StatusMessage"] = $"Re-enabled {account.Username}.";
        return RedirectToPage("Index");
    }

    private Task<Bingo.Domain.Access.Account?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Accounts.SingleOrDefaultAsync(account => account.Id == id, cancellationToken);

    private async Task<bool> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(account => account.Id == id, cancellationToken);
        if (account is null) return false;
        AccountView = new AccountDetails(
            account.Id, account.Username, account.Role, account.EventId, account.TeamId,
            account.LastLoginAt, account.GetAccessMode(timeProvider.GetUtcNow()));
        return true;
    }

    private Task AuditAsync(string action, Bingo.Domain.Access.Account account, string? details, CancellationToken cancellationToken) =>
        auditWriter.WriteAsync(User.GetAccountId(), User.Identity?.Name ?? "unknown", action, "account", account.Id.ToString(), details, cancellationToken);

    public sealed record AccountDetails(
        Guid Id, string Username, AccountRole Role, Guid? EventId, Guid? TeamId,
        DateTimeOffset? LastLoginAt, AccountAccessMode AccessMode);
}
