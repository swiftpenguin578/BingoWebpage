using Bingo.Application.Boards;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Hubs;

public sealed class SignalRProgressNotifier(IHubContext<ProgressHub> hub, ApplicationDbContext db) : IProgressNotifier
{
    public async Task NotifyProgressChangedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (!await db.Events.AsNoTracking().AnyAsync(item => item.Id == eventId && item.HiddenAt == null, cancellationToken)) return;
        await hub.Clients.Group(ProgressHub.GroupName(eventId)).SendAsync("progressChanged", cancellationToken: cancellationToken);
    }
}
