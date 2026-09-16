namespace Bingo.Domain.Integrations.WiseOldMan;

public enum MetricActivityCoverage { Missing, Ranked, EstimatedBaseline, ZeroRecorded, UnexpectedUnrankedEnd, Malformed, ProviderFailure }
public enum MetricActivityAvailability { Available, Estimated, NoRecordedActivity, WaitingForActivityData, WaitingForActivityUpdate, RetainedWithIssue }

/// <summary>One character's raw competition metric. Failed observations never relabel retained values as fresh.</summary>
public sealed class EventCompetitionCharacterMetricActivity
{
    private EventCompetitionCharacterMetricActivity() { }

    public EventCompetitionCharacterMetricActivity(Guid eventId, int generation, long competitionId, Guid characterId, string metric, string assignmentFingerprint)
    {
        EventId = eventId; Generation = generation; CompetitionId = competitionId; OsrsCharacterId = characterId;
        Metric = metric; AssignmentFingerprint = assignmentFingerprint;
    }

    public Guid EventId { get; private set; }
    public int Generation { get; private set; }
    public long CompetitionId { get; private set; }
    public Guid OsrsCharacterId { get; private set; }
    public string Metric { get; private set; } = string.Empty;
    public decimal? Start { get; private set; }
    public decimal? End { get; private set; }
    public decimal? Gained { get; private set; }
    public MetricActivityCoverage Coverage { get; private set; }
    public MetricActivityCoverage? LastIssue { get; private set; }
    public DateTimeOffset? FetchedAt { get; private set; }
    public DateTimeOffset? UpstreamUpdatedAt { get; private set; }
    public DateTimeOffset LastAttemptAt { get; private set; }
    public Guid? ActivityBatchId { get; private set; }
    public string? SourceRequestFingerprint { get; private set; }
    public string AssignmentFingerprint { get; private set; } = string.Empty;

    public void Observe(decimal? start, decimal? end, decimal? gained, DateTimeOffset fetchedAt, DateTimeOffset? upstreamUpdatedAt, Guid batchId, string sourceFingerprint)
    {
        var coverage = Classify(start, end, gained);
        LastAttemptAt = fetchedAt.ToUniversalTime();
        if (coverage is MetricActivityCoverage.Missing or MetricActivityCoverage.Malformed or MetricActivityCoverage.UnexpectedUnrankedEnd)
        {
            LastIssue = coverage;
            // With no usable observation yet, retain the request identity so the read boundary
            // can expose this exact issue. Null values/fetch time still mean no activity data.
            if (FetchedAt is null) { ActivityBatchId = batchId; SourceRequestFingerprint = sourceFingerprint; }
            return;
        }
        Start = start; End = end; Gained = gained; Coverage = coverage; LastIssue = null;
        FetchedAt = fetchedAt.ToUniversalTime(); UpstreamUpdatedAt = upstreamUpdatedAt?.ToUniversalTime();
        ActivityBatchId = batchId; SourceRequestFingerprint = sourceFingerprint;
    }

    public void RecordFailure(DateTimeOffset attemptedAt)
    {
        LastAttemptAt = attemptedAt.ToUniversalTime(); LastIssue = MetricActivityCoverage.ProviderFailure;
    }

    public static MetricActivityCoverage Classify(decimal? start, decimal? end, decimal? gained)
    {
        if (start is null || end is null || gained is null) return MetricActivityCoverage.Missing;
        if (start < -1 || end < -1 || gained < 0 || start >= 100000000000000000000m || end >= 100000000000000000000m || gained >= 100000000000000000000m || start != decimal.Truncate(start.Value) || end != decimal.Truncate(end.Value) || gained != decimal.Truncate(gained.Value))
            return MetricActivityCoverage.Malformed;
        if (start >= 0 && end == -1) return MetricActivityCoverage.UnexpectedUnrankedEnd;
        if (start == -1 && end == -1) return gained == 0 ? MetricActivityCoverage.ZeroRecorded : MetricActivityCoverage.Malformed;
        if (start == -1) return MetricActivityCoverage.EstimatedBaseline;
        return MetricActivityCoverage.Ranked;
    }

    public decimal? RecordedActivity() => Coverage switch
    {
        MetricActivityCoverage.Ranked => Gained,
        MetricActivityCoverage.EstimatedBaseline => End,
        MetricActivityCoverage.ZeroRecorded => 0,
        _ => null
    };

    // Evidence is deliberately an input: approving/reversing a drop must not require a new provider fetch.
    public MetricActivityAvailability Availability(bool hasApprovedSourceDrop)
    {
        if (RecordedActivity() is null) return MetricActivityAvailability.WaitingForActivityData;
        if (LastIssue is not null) return MetricActivityAvailability.RetainedWithIssue;
        if (Coverage == MetricActivityCoverage.ZeroRecorded)
            return hasApprovedSourceDrop ? MetricActivityAvailability.WaitingForActivityData : MetricActivityAvailability.NoRecordedActivity;
        if (RecordedActivity() == 0)
            return hasApprovedSourceDrop ? MetricActivityAvailability.WaitingForActivityUpdate : MetricActivityAvailability.NoRecordedActivity;
        return Coverage == MetricActivityCoverage.EstimatedBaseline ? MetricActivityAvailability.Estimated : MetricActivityAvailability.Available;
    }
}
