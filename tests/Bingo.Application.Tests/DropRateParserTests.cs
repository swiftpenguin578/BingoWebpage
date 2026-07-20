using Bingo.Application.Catalogue;

namespace Bingo.Application.Tests;

public sealed class DropRateParserTests
{
    [Theory]
    [InlineData("1/181,4", "0.005512679162")]
    [InlineData("1/1,024", "0.0009765625")]
    [InlineData("2 × 1/1,024", "0.00195217132568359375")]
    [InlineData("2 x 1/1024", "0.00195217132568359375")]
    [InlineData("3/69", "0.0434782608695652173913043478")]
    [InlineData("2 × 3/69", "0.085066162570888468809073724")]
    [InlineData("7 × 1/1000", "0.006979034965020993001")]
    [InlineData("7/1000", "0.007")]
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

    [Theory]
    [InlineData("0/69")]
    [InlineData("70/69")]
    public void ReturnsNullForInvalidProbability(string displayedRate)
    {
        Assert.Null(DropRateParser.TryParseProbability(displayedRate));
    }
}
