using System.Globalization;
using Bingo.Application.Dashboard;
using Bingo.Domain.Events;
using Bingo.Web;
using Microsoft.Extensions.Localization;

namespace Bingo.BrowserTests;

public sealed class U2DashboardPresentationTests
{
    private static readonly Guid EventId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset At = new(2027, 3, 31, 23, 30, 0, TimeSpan.Zero);
    private readonly Bingo.Web.Pages.Admin.IndexModel model = new(null!, new Text());

    [Fact]
    public void EventTimezoneControlsMonthRangeAndOverdueDateAcrossUtcMonthBoundary()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
        try
        {
            Assert.Equal("Apr 2027", model.Month(At, "Europe/Copenhagen"));
            Assert.Equal("Mar 2027", model.Month(At, "UTC"));
            Assert.Equal("1–2 Apr 2027", model.Range(At, At.AddDays(1), "Europe/Copenhagen"));
            var card = new DashboardEventCard(EventId, "Cup", "cup", EventState.SignupClosed, At, null,
                true, 0, 0, null, "/Admin/Events/Manage/fixture")
            { Timezone = "Europe/Copenhagen" };
            Assert.Equal("Start was due 1 Apr 2027", model.NextDate(card));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void ChartAccessibleNamesRetainImportedFirstTrackedReturningAndProvisionalDetails()
    {
        var point = Point();
        Assert.Equal("Cup: 15 players, 5 returning, 10 first time.", model.ChartLabel(point));
        Assert.Equal("Cup: 15 players, all first tracked.", model.ChartLabel(point with { TrackingStarts = true }));
        Assert.Equal("Cup: 15 players, imported, not linked to website accounts.", model.ChartLabel(point with { IsHistoricalImport = true }));
        Assert.EndsWith("Provisional, awaiting final review.", model.ChartLabel(point with { Provisional = true }));
        var live = point with { State = EventState.Live, ActualEndedAt = null, Provisional = true };
        Assert.EndsWith("Live · provisional", model.ChartLabel(live));
        Assert.Equal("Figures are provisional and may still change.", model.ChartTipNote(live));
        Assert.Contains("awaiting final review. Figures may still change.", model.ChartTipNote(point with { Provisional = true }));
    }

    [Fact]
    public void MissingEhbDistinguishesUnlinkedFromLinkedButIncompatibleWithoutRawReasons()
    {
        var metric = DashboardMetric<long>.Measured(15);
        var row = new DashboardHistoryRow(EventId, "Cup", "cup", EventState.Archived, false, At, At.AddDays(1),
            "/Admin/Events/Manage/fixture", metric, metric, metric, null,
            new DashboardEhbSummary(null, DashboardEhbCoverage.Unavailable, 1, 0), []);
        Assert.Equal("Wise Old Man wasn’t linked to this event.", model.EhbHint(row));
        Assert.Equal("No compatible stored EHB coverage is available.", model.EhbHint(row with { HasLinkedCompetition = true }));
        Assert.Equal("Not available for imported history: contributions were reconstructed, not submitted.",
            model.SubmissionHint(row with { IsHistoricalImport = true, ApprovedSubmissions = DashboardMetric<long>.Unknown("RAW") }));
    }

    [Theory]
    [InlineData("0.325", "33")]
    [InlineData("0.315", "32")]
    [InlineData("0.33333", "33")]
    [InlineData("-0.325", "-32")]
    public void PercentMatchesMathRoundTiesTowardPositiveInfinity(string ratio, string expected)
        => Assert.Equal(expected, Bingo.Web.Pages.Admin.IndexModel.Percent(decimal.Parse(ratio, CultureInfo.InvariantCulture)));

    [Fact]
    public void ProvisionalEndIncludesYearAndCountsUseCultureSeparatorsWithoutPartialSuffix()
    {
        var previous = CultureInfo.CurrentCulture; CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
        try
        {
            Assert.StartsWith("Ended 2 Apr 2027", model.ChartTipNote(Point() with { Provisional = true }));
            Assert.Equal("1,234", Bingo.Web.Pages.Admin.IndexModel.Number(1234));
            Assert.Contains("1,234 players", model.ChartLabel(Point() with { Participants = DashboardMetric<long>.Measured(1234) }));
            var metric = DashboardMetric<long>.Measured(0);
            var row = new DashboardHistoryRow(EventId, "Cup", "cup", EventState.Archived, false, At, At.AddDays(1), "/fixture", metric, metric, metric, null, new DashboardEhbSummary(1m, DashboardEhbCoverage.Partial, 1234, 1000), []);
            Assert.Equal("Wise Old Man · 1,000 of 1,234 accounts", model.EhbHint(row));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static DashboardParticipationPoint Point() => new(EventId, "Cup", "cup", EventState.AwaitingFinalReview,
        false, At, At.AddDays(1), DashboardMetric<long>.Measured(15), DashboardMetric<long>.Measured(15),
        DashboardMetric<long>.Measured(10), DashboardMetric<long>.Measured(5), DashboardMetric<long>.Measured(0));

    private sealed class Text : IStringLocalizer<AdminCommunityResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
