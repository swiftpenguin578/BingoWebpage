using Bingo.Domain.Catalogue;

namespace Bingo.Domain.Events;

public enum EventItemPriceSource { WikiHourly, CatalogueFallback, CatalogueIntroduction }
public enum EventPriceFallbackReason { NoMapping, NoHourlyPrice, ProviderUnavailable, PreparedHourChanged, PriceMoveRejected }

/// <summary>A price is inserted once. Catalogue edits and subsequent event operations never revise it.</summary>
public sealed class EventItemPrice
{
    private EventItemPrice() { }

    public static EventItemPrice Introduce(Guid eventId, CatalogueItem item, DateTimeOffset originalHour, DateTimeOffset introducedAt)
    {
        var price = Capture(eventId, item, originalHour, introducedAt, null, EventPriceFallbackReason.NoHourlyPrice);
        price.Source = EventItemPriceSource.CatalogueIntroduction;
        price.FallbackReason = null;
        return price;
    }

    public static EventItemPrice Capture(Guid eventId, CatalogueItem item, DateTimeOffset selectedHour,
        DateTimeOffset capturedAt, long? hourlyValue, EventPriceFallbackReason fallbackReason)
    {
        selectedHour = selectedHour.ToUniversalTime();
        capturedAt = capturedAt.ToUniversalTime();
        if (selectedHour.Ticks % TimeSpan.TicksPerHour != 0 || selectedHour.AddHours(1) > capturedAt)
            throw new ArgumentOutOfRangeException(nameof(selectedHour));
        var value = hourlyValue ?? item.CatalogueValueGp ?? throw new InvalidOperationException("The item has no price to freeze.");
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(hourlyValue));
        if (!Enum.IsDefined(fallbackReason)) throw new ArgumentOutOfRangeException(nameof(fallbackReason));
        return new EventItemPrice
        {
            EventId = eventId,
            ItemId = item.Id,
            ValueGp = value,
            SelectedHour = selectedHour,
            CapturedAt = capturedAt,
            ApiItemId = int.TryParse(item.ExternalIdentifier, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var apiId) && apiId > 0 ? apiId : null,
            Source = hourlyValue is null ? EventItemPriceSource.CatalogueFallback : EventItemPriceSource.WikiHourly,
            PriceObservedAt = hourlyValue is null ? item.PriceObservedAt : selectedHour,
            FallbackCatalogueSource = hourlyValue is null ? item.PriceSource : null,
            FallbackReason = hourlyValue is null ? fallbackReason : null
        };
    }

    public Guid EventId { get; private set; }
    public Guid ItemId { get; private set; }
    public long ValueGp { get; private set; }
    public DateTimeOffset SelectedHour { get; private set; }
    public DateTimeOffset CapturedAt { get; private set; }
    public EventItemPriceSource Source { get; private set; }
    public int? ApiItemId { get; private set; }
    public DateTimeOffset? PriceObservedAt { get; private set; }
    public CataloguePriceSource? FallbackCatalogueSource { get; private set; }
    public EventPriceFallbackReason? FallbackReason { get; private set; }
}
