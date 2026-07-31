using Bingo.Application.Teams;

namespace Bingo.Infrastructure.Teams;

public sealed class NullTeamFocusNotifier : ITeamFocusNotifier
{
    public Task NotifyTeamFocusChangedAsync(Guid eventId, Guid teamId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
