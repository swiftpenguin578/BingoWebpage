using Bingo.Application.Teams;

namespace Bingo.Infrastructure.Teams;

public sealed class NullAdminCollaborationNotifier : IAdminCollaborationNotifier
{
    public Task NotifyDraftChangedAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyBoardChangedAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyEventsControlChangedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
