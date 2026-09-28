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
        if (account is null || !account.Active || account.AccountType != AccountType.WebsiteAccount)
        {
            return;
        }

        var scope = context.Resource as TeamScope;
        if (scope is null && Guid.TryParse(context.User.FindFirstValue(AccountClaims.EventId), out var eventId) &&
            Guid.TryParse(context.User.FindFirstValue(AccountClaims.TeamId), out var teamId)) scope = new TeamScope(eventId, teamId);
        if (scope is null) return;
        var member = await (from participant in dbContext.EventParticipants.AsNoTracking()
                            join membership in dbContext.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
                            join team in dbContext.Teams.AsNoTracking() on membership.TeamId equals team.Id
                            join item in dbContext.Events.AsNoTracking() on participant.EventId equals item.Id
                            where participant.AccountId == accountId && participant.EventId == scope.EventId &&
                                  team.EventId == scope.EventId && team.Id == scope.TeamId && team.Active && membership.LeftAt == null &&
                                  (membership.Role == Bingo.Domain.Teams.TeamMembershipRole.Captain || membership.Role == Bingo.Domain.Teams.TeamMembershipRole.CoCaptain) && item.HiddenAt == null
                            select item).SingleOrDefaultAsync();
        if (member is null) return;
        var accessMode = member.AcceptsNewSubmissions(timeProvider.GetUtcNow()) ? AccountAccessMode.Full : AccountAccessMode.Disabled;
        foreach (var requirement in context.PendingRequirements.ToArray())
        {
            switch (requirement)
            {
                case AccountAccessRequirement accessRequirement when Satisfies(accessMode, accessRequirement.MinimumMode):
                    context.Succeed(requirement);
                    break;
                case TeamScopeRequirement when accessMode != AccountAccessMode.Disabled:
                    context.Succeed(requirement);
                    break;
            }
        }
    }

    private static bool Satisfies(AccountAccessMode actual, AccountAccessMode required) =>
        actual != AccountAccessMode.Disabled &&
        (required == AccountAccessMode.CorrectionOnly || actual == AccountAccessMode.Full);


}
