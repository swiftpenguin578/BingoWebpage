using Bingo.Application.Teams;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Bingo.Web.Hubs;

[Authorize]
public sealed class TeamFocusHub(ITeamFocusService focus) : Hub
{
    public async Task WatchTeam(string eventId, string teamId, bool inspectEnabled = false)
    {
        if (!Guid.TryParse(eventId, out var parsedEvent) || !Guid.TryParse(teamId, out var parsedTeam))
            throw new HubException("A valid event and team identifier are required.");
        var accountId = Context.User?.GetAccountId() ?? throw new HubException("Account identity is required.");
        var context = await focus.GetContextAsync(parsedEvent, parsedTeam, accountId, inspectEnabled, Context.ConnectionAborted);
        if (context is not { IsVisible: true }) throw new HubException("Team focus is not available for this view.");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(parsedEvent, parsedTeam), Context.ConnectionAborted);
    }

    public static string GroupName(Guid eventId, Guid teamId) => $"team-focus-{eventId:N}-{teamId:N}";
}
