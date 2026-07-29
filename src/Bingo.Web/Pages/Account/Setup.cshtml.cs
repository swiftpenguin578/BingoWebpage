using System.ComponentModel.DataAnnotations;
using System.Net;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Account;

public sealed class SetupModel(
    IWebHostEnvironment environment,
    ApplicationDbContext dbContext,
    IPasswordHasher<Bingo.Domain.Access.Account> passwordHasher,
    TimeProvider timeProvider) : PageModel
{
    [BindProperty]
    public SetupInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        await IsAvailableAsync(cancellationToken) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await IsAvailableAsync(cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var account = Bingo.Domain.Access.Account.CreateWebsite(
            Guid.NewGuid(), Input.Username.Trim(), AccountAuthenticationService.NormalizeUsername(Input.Username), timeProvider.GetUtcNow());
        account.SetGlobalRole(GlobalRole.SuperAdmin);
        account.SetPasswordHash(passwordHasher.HashPassword(account, Input.Password), mustChangePassword: false);
        dbContext.Accounts.Add(account);
        dbContext.AuditEntries.Add(new AuditEntry(
            Guid.NewGuid(),
            timeProvider.GetUtcNow(),
            account.Id,
            account.LoginName,
            "account.bootstrap_created",
            "account",
            account.Id.ToString(),
            "Created through the localhost development setup page"));
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToPage("/Account/Login");
    }

    private async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        var address = HttpContext.Connection.RemoteIpAddress;
        return environment.IsDevelopment() &&
            address is not null && IPAddress.IsLoopback(address) &&
            !await dbContext.Accounts.AnyAsync(account => account.GlobalRole == GlobalRole.SuperAdmin, cancellationToken);
    }

    public sealed class SetupInput
    {
        [Required, StringLength(100, MinimumLength = 3)]
        public string Username { get; set; } = string.Empty;

        [Required, StringLength(200, MinimumLength = 12), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required, Compare(nameof(Password)), DataType(DataType.Password), Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
