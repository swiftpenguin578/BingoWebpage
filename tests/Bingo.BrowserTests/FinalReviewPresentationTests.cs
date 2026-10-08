using Bingo.Application.Events;
using Bingo.Domain.Events;
using Bingo.Web.Pages.Admin.Events;

namespace Bingo.BrowserTests;

public sealed class FinalReviewPresentationTests
{
    [Theory]
    [InlineData(PlacementRule.CreditedEhbThenScoreTime, "ehb")]
    [InlineData(PlacementRule.LegacyScoreTimeThenEhb, "score")]
    public void Au12DecisiveInputFollowsTheEventsPlacementRule(PlacementRule rule, string expected)
    {
        var first = Row(1, "A", 10m, 0);
        var other = Row(2, "B", 9m, -1);
        Assert.Equal(expected, FinalizeModel.DecisiveInput(first, other, rule));
    }

    [Fact]
    public void Au12EhbDeciderRespectsTheRankingPrecision()
    {
        var first = Row(1, "A", 10.00001m, 0);
        var other = Row(2, "B", 10.00002m, 1);
        Assert.Equal("score", FinalizeModel.DecisiveInput(first, other, PlacementRule.CreditedEhbThenScoreTime));
    }

    [Fact]
    public void Rc08SharedThirdPlaceIsCalculatedBeforeTopThreeSlicing()
    {
        var model = new FinalizeModel(null!, null!);
        var rows = model.PresentRows([Row(1, "A", 10m, 0), Row(2, "B", 9m, 0), Row(3, "C", 8m, 0), Row(3, "D", 8m, 0)], PlacementRule.CreditedEhbThenScoreTime);
        Assert.Equal("=3", rows.Take(3).Last().Place);
        Assert.Null(rows[3].Decider);
        Assert.Contains("equal on every input", rows[3].Explanation, StringComparison.Ordinal);
    }

    private static ProvisionalPlacement Row(int rank, string team, decimal ehb, int minutes) => new(Guid.NewGuid(), team, rank, false, null, null, 1, 3, ehb, new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero).AddMinutes(minutes));
}
