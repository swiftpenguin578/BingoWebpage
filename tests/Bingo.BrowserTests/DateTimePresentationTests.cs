using System.Globalization;
using Bingo.Web.UI;

namespace Bingo.BrowserTests;

public sealed class DateTimePresentationTests
{
    [Theory]
    [InlineData(1, "13:00")]
    [InlineData(7, "14:00")]
    public void CopenhagenPresentationFollowsWinterAndSummerOffset(int month, string expectedTime)
    {
        var utc = new DateTimeOffset(2026, month, 15, 12, 0, 0, TimeSpan.Zero).AddMicroseconds(123456);

        Assert.Equal(expectedTime, DateTimePresentation.Format(utc, "HH:mm", provider: CultureInfo.InvariantCulture));
    }
}
