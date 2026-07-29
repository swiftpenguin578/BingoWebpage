using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Bingo.Web.Security;

public sealed class DevelopmentAdminBootstrapper(
    IWebHostEnvironment environment,
    IOptions<DevelopmentAdminBootstrapOptions> options,
    IPasswordHasher<Account> passwordHasher,
    TimeProvider timeProvider)
{
    public async Task BootstrapAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!environment.IsDevelopment() || !settings.Enabled)
        {
            return;
        }

        if (await dbContext.Accounts.AnyAsync(account => account.GlobalRole == GlobalRole.Admin || account.GlobalRole == GlobalRole.SuperAdmin, cancellationToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Username) || settings.Password.Length < 12)
        {
            throw new InvalidOperationException(
                "Development admin bootstrap requires a username and a password of at least 12 characters.");
        }

        var account = Account.CreateWebsite(
            Guid.NewGuid(),
            settings.Username.Trim(),
            AccountAuthenticationService.NormalizeUsername(settings.Username),
            timeProvider.GetUtcNow());
        account.SetGlobalRole(GlobalRole.Admin);
        account.SetPasswordHash(passwordHasher.HashPassword(account, settings.Password), mustChangePassword: true);
        dbContext.Accounts.Add(account);
        dbContext.AuditEntries.Add(new AuditEntry(
            Guid.NewGuid(),
            timeProvider.GetUtcNow(),
            account.Id,
            account.LoginName,
            "account.bootstrap_created",
            "account",
            account.Id.ToString(),
            "Created through development bootstrap configuration"));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
