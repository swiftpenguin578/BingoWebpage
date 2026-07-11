using System.Security.Claims;
using Bingo.Application.Access;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Security;

public sealed class AccountAuthorizationHandler(ApplicationDbContext dbContext, TimeProvider timeProvider)
    : IAuthorizationHandler
{
    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId))
        {
            return;
        }

        var account = await dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(account => account.Id == accountId);
        if (account is null)
        {
            return;
        }

        var accessMode = account.GetAccessMode(timeProvider.GetUtcNow());
        foreach (var requirement in context.PendingRequirements.ToArray())
        {
            switch (requirement)
            {
                case AccountAccessRequirement access when Satisfies(accessMode, access.MinimumMode):
                    context.Succeed(requirement);
                    break;
                case TeamScopeRequirement when MatchesTeamScope(account, context.Resource):
                    context.Succeed(requirement);
                    break;
            }
        }
    }

    private static bool Satisfies(AccountAccessMode actual, AccountAccessMode required) =>
        actual != AccountAccessMode.Disabled &&
        (required == AccountAccessMode.CorrectionOnly || actual == AccountAccessMode.Full);

    private static bool MatchesTeamScope(Account account, object? resource) =>
        account.Role == AccountRole.Captain &&
        resource is TeamScope scope &&
        account.EventId == scope.EventId &&
        account.TeamId == scope.TeamId;
}
