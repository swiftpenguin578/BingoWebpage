using Microsoft.AspNetCore.SignalR;

namespace Bingo.Web.Hubs;

public sealed class ProgressHub : Hub
{
    public Task WatchEvent(string eventId)
    {
        if (!Guid.TryParse(eventId, out var parsed))
            throw new HubException("A valid event identifier is required.");

        return Groups.AddToGroupAsync(Context.ConnectionId, GroupName(parsed));
    }

    public static string GroupName(Guid eventId) => $"event-progress-{eventId:N}";
}
