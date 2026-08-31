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

        var access = await dbContext.AccountEventAccesses.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == accountId);
        var accessMode = access?.GetAccessMode(timeProvider.GetUtcNow()) ?? AccountAccessMode.Disabled;
        if (access is not null && !await dbContext.Events.AsNoTracking().AnyAsync(x => x.Id == access.EventId && x.HiddenAt == null))
            accessMode = AccountAccessMode.Disabled;
        foreach (var requirement in context.PendingRequirements.ToArray())
        {
            switch (requirement)
            {
                case AccountAccessRequirement accessRequirement when Satisfies(accessMode, accessRequirement.MinimumMode):
                    context.Succeed(requirement);
                    break;
                case TeamScopeRequirement when MatchesTeamScope(account, access, context.Resource):
                    context.Succeed(requirement);
                    break;
            }
        }
    }

    private static bool Satisfies(AccountAccessMode actual, AccountAccessMode required) =>
        actual != AccountAccessMode.Disabled &&
        (required == AccountAccessMode.CorrectionOnly || actual == AccountAccessMode.Full);

    private static bool MatchesTeamScope(Account account, AccountEventAccess? access, object? resource) =>
        account.AccountType == AccountType.EmergencyCaptain &&
        resource is TeamScope scope &&
        access?.EventId == scope.EventId &&
        access.TeamId == scope.TeamId;
}
