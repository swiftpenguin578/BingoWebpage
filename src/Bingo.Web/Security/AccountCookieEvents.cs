using System.Security.Claims;
using Bingo.Application.Access;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Security;

public sealed class AccountCookieEvents(ApplicationDbContext dbContext, TimeProvider time)
    : CookieAuthenticationEvents
{
    private const string AccessChangedItemKey = "bingo:access_changed";

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

        var method = context.Principal?.FindFirstValue(AccountClaims.AuthenticationMethod);
        var authorizationVersion = context.Principal?.FindFirstValue(AccountClaims.AuthorizationVersion);
        var passwordVersion = context.Principal?.FindFirstValue(AccountClaims.PasswordVersion);
        if (account is null || !account.Active || authorizationVersion != account.AuthorizationVersion.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
            (method == "password" && passwordVersion != account.PasswordVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)))
        {
            await RejectAsync(context);
            return;
        }

        if (account.AccountType == AccountType.EmergencyCaptain)
        {
            var access = await dbContext.AccountEventAccesses.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == accountId, context.HttpContext.RequestAborted);
            if (access?.GetAccessMode(time.GetUtcNow()) == AccountAccessMode.Disabled) await RejectAsync(context);
        }
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.HttpContext.Items[AccessChangedItemKey] = true;
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        var destination = context.RedirectUri;
        if (context.HttpContext.Items.Remove(AccessChangedItemKey))
        {
            destination = QueryHelpers.AddQueryString(destination, "accessChanged", "true");
        }

        context.Response.Redirect(destination);
        return Task.CompletedTask;
    }
}
