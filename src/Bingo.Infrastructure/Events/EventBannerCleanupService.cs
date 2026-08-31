using Bingo.Application.Events;
using Bingo.Application.Evidence;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bingo.Infrastructure.Events;

public sealed partial class EventBannerCleanupService(
    ApplicationDbContext db,
    IEvidenceStorage storage,
    TimeProvider time,
    ILogger<EventBannerCleanupService> logger) : IEventBannerCleanupService
{
    public async Task ProcessPendingAsync(CancellationToken ct = default)
    {
        var eventIds = await db.EventBannerCleanups.AsNoTracking().Select(item => item.EventId).Distinct().ToListAsync(ct);
        foreach (var eventId in eventIds) await ProcessEventAsync(eventId, ct);
    }

    public async Task ProcessEventAsync(Guid eventId, CancellationToken ct = default)
    {
        var pending = await db.EventBannerCleanups.AsNoTracking().Where(item => item.EventId == eventId).ToListAsync(ct);
        foreach (var item in pending)
        {
            if (!await IsOwnedDiscardedBannerAsync(item, ct)) continue;
            try
            {
                await storage.DeleteAsync(item.StorageKey, ct);
                await db.EventBannerCleanups.Where(value => value.Id == item.Id && value.StorageKey == item.StorageKey).ExecuteDeleteAsync(ct);
            }
            catch (FileNotFoundException)
            {
                await db.EventBannerCleanups.Where(value => value.Id == item.Id && value.StorageKey == item.StorageKey).ExecuteDeleteAsync(ct);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                var tracked = await db.EventBannerCleanups.SingleOrDefaultAsync(value => value.Id == item.Id && value.StorageKey == item.StorageKey, ct);
                if (tracked is not null)
                {
                    tracked.RecordFailure(time.GetUtcNow(), "Managed banner deletion failed; retry is pending.");
                    await db.SaveChangesAsync(ct);
                }
                LogPendingCleanup(logger, exception, eventId);
            }
        }
    }

    private Task<bool> IsOwnedDiscardedBannerAsync(EventBannerCleanup item, CancellationToken ct)
    {
        // Managed banners are event-scoped.  This deliberately refuses keys that could
        // target another event, catalogue storage, or a generic/shared storage location.
        if (!item.StorageKey.StartsWith($"{item.EventId:N}/", StringComparison.Ordinal))
            return Task.FromResult(false);
        return db.Events.AsNoTracking().AnyAsync(value =>
            value.Id == item.EventId && value.HiddenAt == null && value.State == EventState.Discarded && value.BannerAssetId == null, ct);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Managed banner cleanup for discarded event {EventId} remains pending.")]
    private static partial void LogPendingCleanup(ILogger logger, Exception exception, Guid eventId);
}
