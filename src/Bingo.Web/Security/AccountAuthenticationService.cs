using System.Security.Claims;
using Bingo.Application.Access;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
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
            candidate => candidate.NormalizedLoginName == normalizedUsername,
            cancellationToken);

        if (account is null || !account.Active || account.AccountType != AccountType.WebsiteAccount || account.PasswordHash is null)
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
            account.SetPassword(passwordHasher.HashPassword(account, password), account.MustChangePassword, timeProvider.GetUtcNow(), incrementVersion: false);
        }

        account.RecordLogin(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return account;
    }

    public async Task<bool> RecordDiscordLoginAsync(Account account, CancellationToken cancellationToken)
    {
        if (!account.Active || account.AccountType != AccountType.WebsiteAccount) return false;
        account.RecordLogin(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public ClaimsPrincipal CreatePrincipal(Account account, string authenticationMethod = "password")
    {
        if (account.AccountType != AccountType.WebsiteAccount)
            throw new InvalidOperationException("This account is not available.");
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.LoginName),
            new(AccountClaims.MustChangePassword, account.MustChangePassword.ToString()),
            new(AccountClaims.AuthenticationMethod, authenticationMethod),
            new(AccountClaims.AuthorizationVersion, account.AuthorizationVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(AccountClaims.AccountType, account.AccountType.ToString())
        };
        if (authenticationMethod == "password") claims.Add(new Claim(AccountClaims.PasswordVersion, account.PasswordVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (account.GlobalRole is GlobalRole role) claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();
}
