using Bingo.Web.TestData;

namespace Bingo.BrowserTests;

public sealed class LegacyTestSignupImporterTests
{
    [Theory]
    [InlineData("Normal player", "1,249", 1249)]
    [InlineData("W olles", "2535 på main, 311 på HCIM som er tilmeldt", 311)]
    [InlineData("detoned", "527 Iron, 2823 på main", 527)]
    [InlineData("BotF", "225 iron - 887 main", 225)]
    public void ParsesPublishedSheetEhbValues(string name, string rawValue, decimal expected)
    {
        Assert.True(LegacyTestSignupImporter.TryParseEhb(name, rawValue, out var actual));
        Assert.Equal(expected, actual);
    }
}
