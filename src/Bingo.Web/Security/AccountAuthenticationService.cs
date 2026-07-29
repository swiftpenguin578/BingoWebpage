using System.Security.Claims;
using Bingo.Application.Access;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
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
            candidate => candidate.NormalizedLoginName == normalizedUsername,
            cancellationToken);

        if (account is null || !account.Active || account.PasswordHash is null)
        {
            return null;
        }

        AccountEventAccess? emergencyAccess = null;
        if (account.AccountType == AccountType.EmergencyCaptain)
        {
            emergencyAccess = await dbContext.AccountEventAccesses.SingleOrDefaultAsync(access => access.AccountId == account.Id, cancellationToken);
            if (emergencyAccess?.GetAccessMode(timeProvider.GetUtcNow()) == AccountAccessMode.Disabled) return null;
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
        if (account.AccountType == AccountType.EmergencyCaptain)
            dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), timeProvider.GetUtcNow(), account.Id, account.LoginName, "account.emergency_login", "account", account.Id.ToString(), "Emergency credential login succeeded."));
        await dbContext.SaveChangesAsync(cancellationToken);
        return account;
    }

    public ClaimsPrincipal CreatePrincipal(Account account, string authenticationMethod = "password", AccountEventAccess? emergencyAccess = null)
    {
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
        if (account.AccountType == AccountType.EmergencyCaptain) claims.Add(new Claim(ClaimTypes.Role, AccountRole.Captain.ToString()));

        if (emergencyAccess?.EventId is { } eventId)
        {
            claims.Add(new Claim(AccountClaims.EventId, eventId.ToString()));
        }

        if (emergencyAccess?.TeamId is { } teamId)
        {
            claims.Add(new Claim(AccountClaims.TeamId, teamId.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public Task<AccountEventAccess?> GetEmergencyAccessAsync(Guid accountId, CancellationToken cancellationToken) =>
        dbContext.AccountEventAccesses.SingleOrDefaultAsync(access => access.AccountId == accountId, cancellationToken);

    public static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();
}
