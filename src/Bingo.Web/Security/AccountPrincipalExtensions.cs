using System.Security.Claims;
using Bingo.Application.Access;

namespace Bingo.Web.Security;

public static class AccountPrincipalExtensions
{
    public static Guid? GetAccountId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public static Guid? GetEventId(this ClaimsPrincipal principal) => Guid.TryParse(principal.FindFirstValue(AccountClaims.EventId), out var id) ? id : null;
    public static Guid? GetTeamId(this ClaimsPrincipal principal) => Guid.TryParse(principal.FindFirstValue(AccountClaims.TeamId), out var id) ? id : null;
}
