using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Hubs;

public sealed class ProgressHub(ApplicationDbContext db) : Hub
{
    public async Task WatchEvent(string eventId)
    {
        if (!Guid.TryParse(eventId, out var parsed))
            throw new HubException("A valid event identifier is required.");

        if (!await db.Events.AsNoTracking().AnyAsync(item => item.Id == parsed && item.HiddenAt == null, Context.ConnectionAborted))
            throw new HubException("Event progress is not available.");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(parsed));
    }

    public static string GroupName(Guid eventId) => $"event-progress-{eventId:N}";
}
