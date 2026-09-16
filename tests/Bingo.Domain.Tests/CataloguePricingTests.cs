using Bingo.Domain.Catalogue;

namespace Bingo.Domain.Tests;

public sealed class CataloguePricingTests
{
    [Theory]
    [InlineData(797124L, 781558L, 789341L)]
    [InlineData(10L, 11L, 11L)]
    [InlineData(0L, 0L, 0L)]
    [InlineData(null, 12L, 12L)]
    [InlineData(13L, null, 13L)]
    [InlineData(null, null, null)]
    [InlineData(long.MaxValue, long.MaxValue, long.MaxValue)]
    public void HourlyPricesUseSafeRoundedMidpointAndNullFallback(long? high, long? low, long? expected) =>
        Assert.Equal(expected, CataloguePricing.Midpoint(high, low));

    [Fact]
    public void MappingChangeClearsOnlyApiDerivedValueAndVerification()
    {
        var item = new CatalogueItem(Guid.NewGuid(), "Item", "ITEM");
        item.ConfigureApi("123"); item.RecordMapping(ApiMappingStatus.Verified, DateTimeOffset.UtcNow, "Item", "Item.png");
        item.SetPrice(12, CataloguePriceSource.Api, DateTimeOffset.UtcNow);
        item.ConfigureApi("456");
        Assert.Null(item.CatalogueValueGp); Assert.Null(item.MatchedApiName);
        Assert.Equal(ApiMappingStatus.NotConfigured, item.MappingStatus);
        item.SetPrice(0, CataloguePriceSource.Manual, DateTimeOffset.UtcNow);
        item.ConfigureApi("789"); item.ApplyApiPrice(20, DateTimeOffset.UtcNow);
        Assert.Equal(0L, item.CatalogueValueGp); Assert.Equal(CataloguePriceSource.Manual, item.PriceSource);
    }

    [Fact]
    public void MissingAndUntradeableAreDistinctAndCannotBecomeInventedZeroes()
    {
        var item = new CatalogueItem(Guid.NewGuid(), "Pet", "PET");
        Assert.Null(item.CatalogueValueGp);
        Assert.False(item.ApplyApiPrice(null, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => item.SetPrice(null, CataloguePriceSource.Api, null));
        Assert.Throws<ArgumentException>(() => item.SetPrice(-1, CataloguePriceSource.Manual, null));
        item.SetPrice(0, CataloguePriceSource.Untradeable, DateTimeOffset.UtcNow);
        Assert.False(item.ApplyApiPrice(1, DateTimeOffset.UtcNow));
        Assert.Equal(0L, item.CatalogueValueGp);
    }
}
