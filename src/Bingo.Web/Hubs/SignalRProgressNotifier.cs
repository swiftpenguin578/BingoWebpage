using Bingo.Application.Boards;
using Microsoft.AspNetCore.SignalR;

namespace Bingo.Web.Hubs;

public sealed class SignalRProgressNotifier(IHubContext<ProgressHub> hub) : IProgressNotifier
{
    public Task NotifyProgressChangedAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(ProgressHub.GroupName(eventId))
            .SendAsync("progressChanged", cancellationToken: cancellationToken);
}
