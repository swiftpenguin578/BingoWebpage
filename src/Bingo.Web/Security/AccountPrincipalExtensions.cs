using System.Security.Claims;

namespace Bingo.Web.Security;

public static class AccountPrincipalExtensions
{
    public static Guid? GetAccountId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
