using Bingo.Application.Teams;
using Microsoft.AspNetCore.SignalR;

namespace Bingo.Web.Hubs;

public sealed class SignalRAdminCollaborationNotifier(IHubContext<AdminCollaborationHub> hub) : IAdminCollaborationNotifier
{
    public Task NotifyDraftChangedAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(AdminCollaborationHub.DraftGroup(eventId))
            .SendAsync("draftChanged", cancellationToken: cancellationToken);

    public Task NotifyBoardChangedAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(AdminCollaborationHub.BoardGroup(eventId))
            .SendAsync("boardChanged", cancellationToken: cancellationToken);
}
