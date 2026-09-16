using System.Globalization;
using Bingo.Application.Catalogue;
using Bingo.Application.Events;
using Bingo.Domain.Auditing;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Events;

public sealed record PreparedEventItemPrices(DateTimeOffset RequestedHour, ApiHourlyPrices? Prices);

/// <summary>Provider preparation is separate from the caller's atomic event mutation.</summary>
public sealed class EventItemPriceService(ApplicationDbContext db, TimeProvider time, ICatalogueApiClient? api = null)
{
    public async Task<PreparedEventItemPrices> PrepareStartAsync(CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null) throw new InvalidOperationException("Prepare prices before opening the event transaction.");
        var hour = CataloguePricing.LastCompletedHour(time.GetUtcNow());
        var response = api is null ? null : (await api.GetHourlyPricesAsync(hour, ct)).Data;
        return new(hour, response);
    }

    // Caller holds the event row within its lifecycle transaction. All catalogue identities
    // are considered, including inactive items; unavailable unused items have no invented row.
    public async Task CaptureStartAsync(BingoEvent bingoEvent, PreparedEventItemPrices prepared, CancellationToken ct, LifecycleActor? actor = null)
    {
        RequireTransaction();
        if (bingoEvent.ItemPricesCapturedAt is not null) return;
        var startedAt = bingoEvent.ActualStartedAt ?? throw new InvalidOperationException("Start the event before capturing prices.");
        var hour = CataloguePricing.LastCompletedHour(startedAt);
        var existing = await db.EventItemPrices.Where(x => x.EventId == bingoEvent.Id).Select(x => x.ItemId).ToHashSetAsync(ct);
        var items = await db.CatalogueItems.FromSqlRaw("SELECT * FROM catalogue_items ORDER BY id FOR SHARE").ToListAsync(ct);
        foreach (var item in items.Where(x => !existing.Contains(x.Id)))
        {
            long? value = null;
            var reason = EventPriceFallbackReason.NoMapping;
            if (int.TryParse(item.ExternalIdentifier, NumberStyles.None, CultureInfo.InvariantCulture, out var apiId) && apiId > 0)
            {
                reason = prepared.RequestedHour != hour ? EventPriceFallbackReason.PreparedHourChanged
                    : prepared.Prices is null || prepared.Prices.Hour != hour ? EventPriceFallbackReason.ProviderUnavailable
                    : EventPriceFallbackReason.NoHourlyPrice;
                if (prepared.RequestedHour == hour && prepared.Prices?.Hour == hour)
                    value = prepared.Prices.Values.GetValueOrDefault(apiId);
            }
            if (value is { } candidate)
            {
                var before = new { item.RejectedPriceGp, item.RejectedPriceObservedAt };
                if (!item.CheckApiPriceCandidate(candidate, hour))
                {
                    value = null;
                    reason = EventPriceFallbackReason.PriceMoveRejected;
                }
                if (before.RejectedPriceGp != item.RejectedPriceGp || before.RejectedPriceObservedAt != item.RejectedPriceObservedAt)
                    db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), startedAt, actor?.Id, actor?.Username ?? "System",
                        "catalogue.event_start_price_checked", "catalogue_item", item.Id.ToString(), item.Name, bingoEvent.Id,
                        System.Text.Json.JsonSerializer.Serialize(before),
                        System.Text.Json.JsonSerializer.Serialize(new { item.RejectedPriceGp, item.RejectedPriceObservedAt, candidate, item.CatalogueValueGp, hour })));
            }
            if (value is null && item.CatalogueValueGp is null) continue;
            db.EventItemPrices.Add(EventItemPrice.Capture(bingoEvent.Id, item, hour, startedAt, value, reason));
        }
        bingoEvent.MarkItemPricesCaptured();
    }

    // Successful published approval/correction is the introduction boundary. The caller
    // holds FOR UPDATE on the event; its version changes with these insertions as well.
    public async Task IntroduceAsync(BingoEvent bingoEvent, IEnumerable<Guid> itemIds, CancellationToken ct)
    {
        RequireTransaction();
        // A pre-migration start (including reconstructed imports) has no asserted price history.
        if (bingoEvent.ItemPricesCapturedAt is null || bingoEvent.ActualStartedAt is null) return;
        var ids = itemIds.Distinct().ToArray();
        var existing = await db.EventItemPrices.Where(x => x.EventId == bingoEvent.Id && ids.Contains(x.ItemId)).Select(x => x.ItemId).ToHashSetAsync(ct);
        ids = ids.Except(existing).ToArray();
        if (ids.Length == 0) return;
        var items = await db.CatalogueItems.FromSqlInterpolated($"SELECT * FROM catalogue_items WHERE id = ANY({ids}) ORDER BY id FOR SHARE").AsNoTracking().ToListAsync(ct);
        if (items.Count != ids.Length || items.Any(x => x.CatalogueValueGp is null))
            throw new InvalidOperationException("Every introduced drop needs a catalogue GP value.");
        var introducedAt = time.GetUtcNow();
        var hour = CataloguePricing.LastCompletedHour(bingoEvent.ActualStartedAt.Value);
        foreach (var item in items) db.EventItemPrices.Add(EventItemPrice.Introduce(bingoEvent.Id, item, hour, introducedAt));
        bingoEvent.AdvanceVersion();
    }

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Event price capture requires the event mutation transaction.");
    }
}
