namespace Bingo.Application.Boards;

public interface IProgressNotifier
{
    Task NotifyProgressChangedAsync(Guid eventId, CancellationToken cancellationToken = default);
}
