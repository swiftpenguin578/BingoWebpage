using System.Security.Claims;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Security;

public sealed class AccountCookieEvents(ApplicationDbContext dbContext, TimeProvider timeProvider)
    : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId))
        {
            await RejectAsync(context);
            return;
        }

        var account = await dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == accountId,
            context.HttpContext.RequestAborted);

        if (account is null || account.GetAccessMode(timeProvider.GetUtcNow()) == AccountAccessMode.Disabled)
        {
            await RejectAsync(context);
        }
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
