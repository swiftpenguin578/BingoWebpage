using System.Diagnostics.CodeAnalysis;

using Bingo.Domain.Events;

namespace Bingo.Application.Dashboard;

public interface IAdminDashboardService
{
    Task<AdminDashboardResult> GetAsync(Guid actorAccountId, CancellationToken cancellationToken = default);

    // The directory reuses the retained population rules without reading all
    // Dashboard statistics. Hidden records remain restricted to SuperAdmin.
    Task<IReadOnlyDictionary<Guid, EventParticipationSummary>> GetEventParticipationAsync(
        Guid actorAccountId, IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken = default);
}

// Kept as a domain-neutral alias for later page binding. Both ports describe the
// same read-only projection and are backed by one implementation in Infrastructure.
public interface ICommunityDashboardService
{
    Task<AdminDashboardResult> GetAsync(Guid actorAccountId, CancellationToken cancellationToken = default);
}

public sealed record EventParticipationSummary(DashboardMetric<long> Participants, bool IsHistoricalImport);

public enum DashboardValueAvailability
{
    Measured,
    Unavailable
}

public enum DashboardEhbCoverage
{
    Unavailable,
    Partial,
    Complete,
    MeasuredZero
}

[SuppressMessage("Design", "CA1000", Justification = "The typed metric factory keeps later UI binding and nullable value construction consistent.")]
public sealed record DashboardMetric<T>(T? Value, DashboardValueAvailability Availability,
    string? UnavailableReason = null)
{
    public bool IsAvailable => Availability == DashboardValueAvailability.Measured;
    public static DashboardMetric<T> Measured(T value) => new(value, DashboardValueAvailability.Measured);
    public static DashboardMetric<T> Unknown(string? reason = null) => new(default, DashboardValueAvailability.Unavailable, reason);
}

public sealed record AdminDashboardResult(
    DateTimeOffset AsOf,
    DashboardStatistics Statistics,
    IReadOnlyList<DashboardParticipationPoint> ParticipationChart,
    DashboardRecap? LatestEndedRecap,
    IReadOnlyList<DashboardHistoryRow> History,
    DashboardEventCard? CurrentEvent,
    DashboardCommunitySnapshot Community)
{
    public IReadOnlyList<DashboardParticipationPoint> Chart => ParticipationChart;
}

public sealed record DashboardStatistics(
    DashboardMetric<long> EventsHeld,
    DashboardMetric<long> UniqueWebsiteParticipants,
    DashboardMetric<long> EventParticipations,
    DashboardMetric<long> ApprovedSubmissions,
    DashboardMetric<long> LatestNewParticipants,
    DashboardMetric<long> LatestNewApprovedSubmissions)
{
    public DashboardMetric<long> Events => EventsHeld;
    public DashboardMetric<long> UniqueParticipants => UniqueWebsiteParticipants;
    public DashboardMetric<long> Participations => EventParticipations;
    public bool Provisional { get; init; }
    public int ProvisionalEvents { get; init; }
    public Guid? LatestContributionEventId { get; init; }
    public string? LatestContributionEventName { get; init; }
    public bool LatestContributionProvisional { get; init; }
}

public sealed record DashboardParticipationPoint(
    Guid EventId,
    string EventName,
    string EventSlug,
    EventState State,
    bool Provisional,
    DateTimeOffset? ActualStartedAt,
    DateTimeOffset? ActualEndedAt,
    DashboardMetric<long> Participants,
    DashboardMetric<long> UniqueWebsiteParticipants,
    DashboardMetric<long> NewWebsiteParticipants,
    DashboardMetric<long> ReturningWebsiteParticipants,
    DashboardMetric<long> ApprovedSubmissions)
{
    public DashboardMetric<long> TotalParticipants => Participants;
    public bool IsHistoricalImport { get; init; }
    public bool TrackingStarts { get; init; }
    public int TeamCount { get; init; }
}

public sealed record DashboardHistoryRow(
    Guid EventId,
    string EventName,
    string EventSlug,
    EventState State,
    bool Provisional,
    DateTimeOffset? ActualStartedAt,
    DateTimeOffset? ActualEndedAt,
    string OverviewPath,
    DashboardMetric<long> Participants,
    DashboardMetric<long> UniqueWebsiteParticipants,
    DashboardMetric<long> ApprovedSubmissions,
    DashboardWinnerBoard? WinnerBoard,
    DashboardEhbSummary Ehb,
    IReadOnlyList<DashboardWinner> Winners)
{
    public DashboardMetric<long> Players => Participants;
    public DashboardMetric<long> Submissions => ApprovedSubmissions;
    public bool IsHistoricalImport { get; init; }
    public int TeamCount { get; init; }
}

public sealed record DashboardWinner(Guid TeamId, string TeamName, int Placement);

public sealed record DashboardWinnerBoard(
    int CompletedTiles,
    int TotalTiles,
    decimal? CompletionRatio,
    IReadOnlyList<DashboardWinner> Winners)
{
    public bool IsAvailable => TotalTiles > 0;
}

public sealed record DashboardRecap(
    Guid EventId,
    string EventName,
    string EventSlug,
    DateTimeOffset ActualStartedAt,
    DateTimeOffset ActualEndedAt,
    DashboardMetric<long> Participants,
    DashboardMetric<long> ApprovedSubmissions,
    DashboardWinnerBoard? WinnerBoard,
    IReadOnlyList<DashboardWinner> Winners,
    string OverviewPath)
{
    public EventState State { get; init; }
    public bool Provisional { get; init; }
    public bool IsHistoricalImport { get; init; }
    public int TeamCount { get; init; }
}

public sealed record DashboardEhbSummary(
    decimal? Gain,
    DashboardEhbCoverage Coverage,
    int ExpectedAccounts,
    int MatchedAccounts)
{
    public bool IsAvailable => Coverage is DashboardEhbCoverage.Complete or DashboardEhbCoverage.MeasuredZero or DashboardEhbCoverage.Partial;
}

public sealed record DashboardEventCard(
    Guid EventId,
    string EventName,
    string EventSlug,
    EventState State,
    DateTimeOffset? ScheduledStartAt,
    DateTimeOffset? ActualStartedAt,
    bool IsOverdue,
    long ConfirmedParticipants,
    long WaitingParticipants,
    int? Capacity,
    string OverviewPath)
{
    public DashboardNextDateKind NextDateKind { get; init; }
    public DateTimeOffset? NextDate { get; init; }
    public string Timezone { get; init; } = "UTC";
}

public enum DashboardNextDateKind
{
    EventStarts,
    SignupsOpen,
    SignupsClose,
    EventEnds
}

public sealed record DashboardCommunitySnapshot(
    DashboardMetric<long> WebsiteAccounts,
    DashboardMetric<long> NewWebsiteAccounts,
    DashboardMetric<long> LoggedInWebsiteAccounts,
    DateTimeOffset? Since,
    bool UsedThirtyDayFallback);

public enum DashboardHistorySortField
{
    EventDate,
    Players,
    ApprovedSubmissions,
    WinnerBoard,
    Ehb,
    Winner
}

/// <summary>
/// Applies the Dashboard history's six supported null-last stable sorts for a
/// later UI binding without changing the authoritative read projection.
/// </summary>
public static class DashboardHistoryOrdering
{
    public static IReadOnlyList<DashboardHistoryRow> Sort(
        IEnumerable<DashboardHistoryRow> rows,
        DashboardHistorySortField field,
        bool descending)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var source = rows.ToArray();
        return field switch
        {
            DashboardHistorySortField.EventDate => SortBy<DateTimeOffset>(source, row => row.ActualStartedAt, descending),
            DashboardHistorySortField.Players => SortBy<long>(source, row => row.Participants.IsAvailable ? row.Participants.Value : null, descending),
            DashboardHistorySortField.ApprovedSubmissions => SortBy<long>(source, row => row.ApprovedSubmissions.IsAvailable ? row.ApprovedSubmissions.Value : null, descending),
            DashboardHistorySortField.WinnerBoard => SortBy<decimal>(source, row => row.WinnerBoard?.CompletionRatio, descending),
            DashboardHistorySortField.Ehb => SortBy<decimal>(source, row => row.Ehb.IsAvailable ? row.Ehb.Gain : null, descending),
            DashboardHistorySortField.Winner => SortByString(source, row => row.Winners.Count == 0 ? null : row.Winners[0].TeamName, descending),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unsupported Dashboard history sort field.")
        };
    }

    private static List<DashboardHistoryRow> SortBy<T>(
        DashboardHistoryRow[] source,
        Func<DashboardHistoryRow, T?> selector,
        bool descending)
        where T : struct, IComparable<T>
    {
        var populated = source.Where(row => selector(row) is not null).ToList();
        populated.Sort((left, right) =>
        {
            var comparison = selector(left)!.Value.CompareTo(selector(right)!.Value);
            if (comparison != 0 && descending) comparison = -comparison;
            return comparison != 0 ? comparison : left.EventId.CompareTo(right.EventId);
        });
        var missing = source.Where(row => selector(row) is null)
            .OrderBy(row => row.EventId)
            .ToList();
        populated.AddRange(missing);
        return populated;
    }

    private static List<DashboardHistoryRow> SortByString(
        DashboardHistoryRow[] source,
        Func<DashboardHistoryRow, string?> selector,
        bool descending)
    {
        var populated = source.Where(row => selector(row) is not null).ToList();
        populated.Sort((left, right) =>
        {
            var comparison = StringComparer.Ordinal.Compare(selector(left), selector(right));
            if (comparison != 0 && descending) comparison = -comparison;
            return comparison != 0 ? comparison : left.EventId.CompareTo(right.EventId);
        });
        populated.AddRange(source.Where(row => selector(row) is null).OrderBy(row => row.EventId));
        return populated;
    }
}
