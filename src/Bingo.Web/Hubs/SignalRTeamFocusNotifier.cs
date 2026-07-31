using Bingo.Application.Teams;
using Microsoft.AspNetCore.SignalR;

namespace Bingo.Web.Hubs;

public sealed class SignalRTeamFocusNotifier(IHubContext<TeamFocusHub> hub) : ITeamFocusNotifier
{
    public Task NotifyTeamFocusChangedAsync(Guid eventId, Guid teamId, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(TeamFocusHub.GroupName(eventId, teamId))
            .SendAsync("focusChanged", cancellationToken: cancellationToken);
}
