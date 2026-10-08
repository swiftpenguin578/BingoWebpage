using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Security;

internal static class AccountResetTokenPolicy
{
    public static bool CanIssueReset(Account issuer, Account target) =>
        issuer.AccountType == AccountType.WebsiteAccount && issuer.Active &&
        target.AccountType == AccountType.WebsiteAccount && target.Active &&
        target.GlobalRole != GlobalRole.SuperAdmin &&
        (issuer.GlobalRole == GlobalRole.SuperAdmin || issuer.GlobalRole == GlobalRole.Admin && target.GlobalRole == GlobalRole.User);

    public static async Task SupersedeResetTokensAsync(ApplicationDbContext db, Guid accountId, DateTimeOffset now, CancellationToken ct)
    {
        var tokens = await db.PasswordCredentialTokens
            .Where(token => token.AccountId == accountId && token.Purpose == PasswordCredentialTokenPurpose.Reset &&
                           token.UsedAt == null && token.SupersededAt == null)
            .ToListAsync(ct);
        foreach (var token in tokens) token.Supersede(now);
    }
}
