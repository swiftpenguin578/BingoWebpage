using Bingo.Domain.Integrations.WiseOldMan;

namespace Bingo.Domain.Tests;

public sealed class MetricActivityTests
{
    [Theory]
    [InlineData(20, 35, 12, false, MetricActivityCoverage.Ranked, MetricActivityAvailability.Available, 12)]
    [InlineData(-1, 35, 34, false, MetricActivityCoverage.EstimatedBaseline, MetricActivityAvailability.Estimated, 35)]
    [InlineData(-1, -1, 0, false, MetricActivityCoverage.ZeroRecorded, MetricActivityAvailability.NoRecordedActivity, 0)]
    [InlineData(-1, -1, 0, true, MetricActivityCoverage.ZeroRecorded, MetricActivityAvailability.WaitingForActivityData, 0)]
    [InlineData(20, 20, 0, false, MetricActivityCoverage.Ranked, MetricActivityAvailability.NoRecordedActivity, 0)]
    [InlineData(20, 20, 0, true, MetricActivityCoverage.Ranked, MetricActivityAvailability.WaitingForActivityUpdate, 0)]
    public void ExplicitCountRulesPreserveRawValues(int start, int end, int gained, bool drop, MetricActivityCoverage coverage, MetricActivityAvailability availability, int activity)
    {
        var row = Row(); var now = DateTimeOffset.UtcNow;
        row.Observe(start, end, gained, now, now.AddMinutes(-5), Guid.NewGuid(), "source");
        Assert.Equal(coverage, row.Coverage); Assert.Equal(availability, row.Availability(drop));
        Assert.Equal((decimal)activity, row.RecordedActivity()); Assert.Equal((decimal)gained, row.Gained);
        Assert.Equal((decimal)start, row.Start); Assert.Equal((decimal)end, row.End);
    }

    [Theory]
    [InlineData(20, -1, 0, MetricActivityCoverage.UnexpectedUnrankedEnd)]
    [InlineData(null, 35, 10, MetricActivityCoverage.Missing)]
    [InlineData(20, null, 10, MetricActivityCoverage.Missing)]
    [InlineData(20, 35, null, MetricActivityCoverage.Missing)]
    [InlineData(-2, 35, 10, MetricActivityCoverage.Malformed)]
    [InlineData(-1, -1, 10, MetricActivityCoverage.Malformed)]
    public void BadObservationRetainsItsLastUsableOrigin(int? start, int? end, int? gained, MetricActivityCoverage issue)
    {
        var row = Row(); var first = DateTimeOffset.UtcNow; var batch = Guid.NewGuid();
        row.Observe(10, 20, 10, first, first.AddMinutes(-5), batch, "old-source");
        row.Observe(start, end, gained, first.AddHours(2), first.AddHours(1), Guid.NewGuid(), "new-source");
        Assert.Equal(issue, row.LastIssue); Assert.Equal(10m, row.RecordedActivity());
        Assert.Equal(first, row.FetchedAt); Assert.Equal(first.AddMinutes(-5), row.UpstreamUpdatedAt);
        Assert.Equal(batch, row.ActivityBatchId); Assert.Equal("old-source", row.SourceRequestFingerprint);
        Assert.Equal(first.AddHours(2), row.LastAttemptAt); Assert.Equal(MetricActivityAvailability.RetainedWithIssue, row.Availability(true));
        var missing = Row(); missing.Observe(start, end, gained, first, null, batch, "source");
        Assert.Null(missing.RecordedActivity()); Assert.Null(missing.FetchedAt);
        Assert.Equal(MetricActivityAvailability.WaitingForActivityData, missing.Availability(false));
    }

    [Fact]
    public void OutageCannotInventAnInitialZeroOrAdvanceRetainedTime()
    {
        var now = DateTimeOffset.UtcNow; var row = Row(); row.RecordFailure(now);
        Assert.Null(row.RecordedActivity()); Assert.Null(row.FetchedAt);
        row.Observe(-1, -1, 0, now, now, Guid.NewGuid(), "source"); row.RecordFailure(now.AddHours(2));
        Assert.Equal(now, row.FetchedAt); Assert.Equal(MetricActivityCoverage.ProviderFailure, row.LastIssue);
    }

    private static EventCompetitionCharacterMetricActivity Row() => new(Guid.NewGuid(), 1, 42, Guid.NewGuid(), "vorkath", "assignments");
}
