using System.Security.Claims;
using Bingo.Application.Access;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Security;

public sealed class AccountAuthenticationService(
    ApplicationDbContext dbContext,
    IPasswordHasher<Account> passwordHasher,
    TimeProvider timeProvider)
{
    public async Task<Account?> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        var normalizedUsername = NormalizeUsername(username);
        var account = await dbContext.Accounts.SingleOrDefaultAsync(
            candidate => candidate.NormalizedUsername == normalizedUsername,
            cancellationToken);

        if (account is null || account.GetAccessMode(timeProvider.GetUtcNow()) == AccountAccessMode.Disabled)
        {
            return null;
        }

        var result = passwordHasher.VerifyHashedPassword(account, account.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            account.SetPasswordHash(passwordHasher.HashPassword(account, password), account.MustChangePassword);
        }

        account.RecordLogin(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return account;
    }

    public ClaimsPrincipal CreatePrincipal(Account account)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.Username),
            new(ClaimTypes.Role, account.Role.ToString()),
            new(AccountClaims.MustChangePassword, account.MustChangePassword.ToString())
        };

        if (account.EventId is { } eventId)
        {
            claims.Add(new Claim(AccountClaims.EventId, eventId.ToString()));
        }

        if (account.TeamId is { } teamId)
        {
            claims.Add(new Claim(AccountClaims.TeamId, teamId.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();
}
