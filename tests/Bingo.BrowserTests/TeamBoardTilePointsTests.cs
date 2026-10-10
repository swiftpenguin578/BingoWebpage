using Bingo.Web.Pages.Events;

namespace Bingo.BrowserTests;

public sealed class TeamBoardTilePointsTests
{
    [Theory]
    [InlineData("0", 1)]
    [InlineData("0.0001", 1)]
    [InlineData("0.4999", 1)]
    [InlineData("0.5", 1)]
    [InlineData("1.4999", 1)]
    [InlineData("1.5", 2)]
    [InlineData("2.5", 3)]
    [InlineData("20.4", 20)]
    [InlineData("48.5", 49)]
    [InlineData("52.9999", 53)]
    public void TilePointsRoundToTheNearestWholeNumberAndNeverDropBelowOne(string ehb, int expected) =>
        Assert.Equal(expected, TeamBoardModel.GetTilePoints(decimal.Parse(ehb, System.Globalization.CultureInfo.InvariantCulture)));
}
