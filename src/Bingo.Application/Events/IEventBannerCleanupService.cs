namespace Bingo.Application.Events;

public interface IEventBannerCleanupService
{
    Task ProcessEventAsync(Guid eventId, CancellationToken ct = default);
    Task ProcessPendingAsync(CancellationToken ct = default);
}
