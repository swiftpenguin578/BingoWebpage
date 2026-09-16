using Bingo.Domain.Catalogue;

namespace Bingo.Domain.Tests;

public sealed class EventPriceHourTests
{
    [Theory]
    [InlineData("2026-09-15T13:00:00Z", "2026-09-15T12:00:00Z")]
    [InlineData("2026-09-15T12:59:59.9999999Z", "2026-09-15T11:00:00Z")]
    [InlineData("2026-09-15T14:30:00+02:00", "2026-09-15T11:00:00Z")]
    [InlineData("2026-01-01T00:00:00Z", "2025-12-31T23:00:00Z")]
    public void SelectsLastFullyCompletedUtcHour(string start, string expected)
    {
        Assert.Equal(DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            CataloguePricing.LastCompletedHour(DateTimeOffset.Parse(start, System.Globalization.CultureInfo.InvariantCulture)));
    }
}
