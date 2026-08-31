using Bingo.Application.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Hubs;

public sealed class SignalRAdminCollaborationNotifier(IHubContext<AdminCollaborationHub> hub, ApplicationDbContext db) : IAdminCollaborationNotifier
{
    public async Task NotifyDraftChangedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (!await db.Events.AsNoTracking().AnyAsync(item => item.Id == eventId && item.HiddenAt == null, cancellationToken)) return;
        await hub.Clients.Group(AdminCollaborationHub.DraftGroup(eventId)).SendAsync("draftChanged", cancellationToken: cancellationToken);
    }

    public async Task NotifyBoardChangedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (!await db.Events.AsNoTracking().AnyAsync(item => item.Id == eventId && item.HiddenAt == null, cancellationToken)) return;
        await hub.Clients.Group(AdminCollaborationHub.BoardGroup(eventId)).SendAsync("boardChanged", cancellationToken: cancellationToken);
    }

    public Task NotifyEventsControlChangedAsync(CancellationToken cancellationToken = default) =>
        hub.Clients.Group(AdminCollaborationHub.EventsControlGroup()).SendAsync("eventsControlChanged", cancellationToken: cancellationToken);
}
