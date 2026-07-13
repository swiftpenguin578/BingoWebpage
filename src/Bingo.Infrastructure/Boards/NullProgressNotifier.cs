using Bingo.Application.Boards;

namespace Bingo.Infrastructure.Boards;

public sealed class NullProgressNotifier : IProgressNotifier
{
    public Task NotifyProgressChangedAsync(Guid eventId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
