using Bingo.Application.Catalogue;

namespace Bingo.Application.Tests;

public sealed class DropRateParserTests
{
    [Theory]
    [InlineData("1/181,4", "0.005512679162")]
    [InlineData("1/1,024", "0.0009765625")]
    [InlineData("2 × 1/1,024", "0.001953125")]
    [InlineData("2 x 1/1024", "0.001953125")]
    public void ParsesCommonDisplayedRates(string displayedRate, string expectedText)
    {
        var actual = DropRateParser.TryParseProbability(displayedRate);
        var expected = decimal.Parse(expectedText, System.Globalization.CultureInfo.InvariantCulture);
        Assert.NotNull(actual); Assert.Equal(expected, actual.Value, 10);
    }

    [Fact]
    public void ReturnsNullForDescriptiveRate()
    {
        Assert.Null(DropRateParser.TryParseProbability("Varies by invocation"));
    }
}
