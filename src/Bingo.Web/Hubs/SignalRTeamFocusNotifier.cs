using Bingo.Application.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Hubs;

public sealed class SignalRTeamFocusNotifier(IHubContext<TeamFocusHub> hub, ApplicationDbContext db) : ITeamFocusNotifier
{
    public async Task NotifyTeamFocusChangedAsync(Guid eventId, Guid teamId, CancellationToken cancellationToken = default)
    {
        if (!await db.Events.AsNoTracking().AnyAsync(item => item.Id == eventId && item.HiddenAt == null, cancellationToken)) return;
        await hub.Clients.Group(TeamFocusHub.GroupName(eventId, teamId)).SendAsync("focusChanged", cancellationToken: cancellationToken);
    }
}
