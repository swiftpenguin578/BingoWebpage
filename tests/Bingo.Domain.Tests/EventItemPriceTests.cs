using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;

namespace Bingo.Domain.Tests;

public sealed class EventItemPriceTests
{
    private static readonly DateTimeOffset Hour = new(2026, 9, 15, 11, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0L)]
    [InlineData(101L)]
    public void HourlyValueWinsAndCatalogueEditsCannotReviseCapturedPrice(long value)
    {
        var item = Item(777);
        var price = EventItemPrice.Capture(Guid.NewGuid(), item, Hour, Hour.AddHours(1), value, EventPriceFallbackReason.NoHourlyPrice);
        item.SetPrice(999, CataloguePriceSource.Manual, Hour.AddDays(3));
        Assert.Equal(value, price.ValueGp);
        Assert.Equal(EventItemPriceSource.WikiHourly, price.Source);
        Assert.Equal(Hour, price.PriceObservedAt);
        Assert.Equal(Hour, price.SelectedHour);
        Assert.Equal(4151, price.ApiItemId);
        Assert.Null(price.FallbackCatalogueSource);
        Assert.Null(price.FallbackReason);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(400L)]
    public void LateCatalogueFallbackRetainsRealObservationAndCaptureTimes(long value)
    {
        var item = Item(value);
        var introduction = Hour.AddDays(3);
        var price = EventItemPrice.Capture(Guid.NewGuid(), item, Hour, introduction, null, EventPriceFallbackReason.ProviderUnavailable);
        Assert.Equal(value, price.ValueGp);
        Assert.Equal(Hour.AddDays(-1), price.PriceObservedAt);
        Assert.Equal(introduction, price.CapturedAt);
        Assert.Equal(Hour, price.SelectedHour);
        Assert.Equal(EventItemPriceSource.CatalogueFallback, price.Source);
        Assert.Equal(CataloguePriceSource.Manual, price.FallbackCatalogueSource);
        Assert.Equal(EventPriceFallbackReason.ProviderUnavailable, price.FallbackReason);
    }

    [Fact]
    public void MissingValueIsNeverInventedAsZero()
    {
        var item = new CatalogueItem(Guid.NewGuid(), "Unpriced", "UNPRICED");
        Assert.Throws<InvalidOperationException>(() => EventItemPrice.Capture(Guid.NewGuid(), item, Hour, Hour.AddHours(1), null, EventPriceFallbackReason.NoMapping));
    }

    [Theory]
    [InlineData(100L, 49L, true)]
    [InlineData(100L, 50L, false)]
    [InlineData(100L, 200L, false)]
    [InlineData(100L, 201L, true)]
    [InlineData(100L, 0L, true)]
    [InlineData(0L, 100L, true)]
    [InlineData(0L, 0L, false)]
    [InlineData(null, 100L, false)]
    [InlineData(long.MaxValue, long.MaxValue, false)]
    public void SuspiciousCandidateRuleUsesTrustedValueWithoutOverflow(long? baseline, long candidate, bool rejected)
    {
        Assert.Equal(rejected, CataloguePricing.IsSuspiciousMove(baseline, candidate));
    }

    [Fact]
    public void RejectedCandidateKeepsTrustedValueAndFlagUntilAcceptedResultOrManualCorrection()
    {
        var item = Item(100);
        item.SetPrice(100, CataloguePriceSource.Api, Hour);
        Assert.False(item.ApplyApiPrice(201, Hour.AddHours(1)));
        Assert.Equal(100, item.CatalogueValueGp); Assert.Equal(201, item.RejectedPriceGp);
        Assert.False(item.ApplyApiPrice(null, Hour.AddHours(2)));
        Assert.Equal(201, item.RejectedPriceGp);
        Assert.True(item.ApplyApiPrice(200, Hour.AddHours(3)));
        Assert.Equal(200, item.CatalogueValueGp); Assert.Null(item.RejectedPriceGp);
        Assert.False(item.ApplyApiPrice(0, Hour.AddHours(4)));
        item.SetPrice(0, CataloguePriceSource.Manual, Hour.AddHours(5));
        Assert.Null(item.RejectedPriceGp); Assert.Equal(0, item.CatalogueValueGp);
        Assert.False(item.ApplyApiPrice(100, Hour.AddHours(6)));
        Assert.Equal(CataloguePriceSource.Manual, item.PriceSource); Assert.Null(item.RejectedPriceGp);
    }

    private static CatalogueItem Item(long value)
    {
        var item = new CatalogueItem(Guid.NewGuid(), "Whip", "WHIP");
        item.ConfigureApi("4151");
        item.SetPrice(value, CataloguePriceSource.Manual, Hour.AddDays(-1));
        return item;
    }
}
