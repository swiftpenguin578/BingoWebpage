using Bingo.Application.Dashboard;
using Bingo.Domain.Events;

namespace Bingo.Application.Tests;

public sealed class DashboardHistoryOrderingTests
{
    [Fact]
    public void SixHistoryColumnsSortNullsLastInBothDirectionsWithStableIds()
    {
        var first = Row("00000000-0000-0000-0000-000000000001", "Alpha", 2, 3, .50m, 4m);
        var second = Row("00000000-0000-0000-0000-000000000002", "Beta", 1, null, .75m, 0m,
            DashboardEhbCoverage.MeasuredZero);
        var missing = Row("00000000-0000-0000-0000-000000000003", null, null, 1, null, null,
            DashboardEhbCoverage.Unavailable, winnerValue: null);
        var rows = new[] { missing, second, first };

        Assert.Equal([second.EventId, first.EventId, missing.EventId],
            DashboardHistoryOrdering.Sort(rows, DashboardHistorySortField.Players, descending: false).Select(value => value.EventId));
        Assert.Equal([first.EventId, second.EventId, missing.EventId],
            DashboardHistoryOrdering.Sort(rows, DashboardHistorySortField.Players, descending: true).Select(value => value.EventId));
        Assert.Equal([missing.EventId, first.EventId, second.EventId],
            DashboardHistoryOrdering.Sort(rows, DashboardHistorySortField.ApprovedSubmissions, descending: false).Select(value => value.EventId));
        Assert.Equal([first.EventId, missing.EventId, second.EventId],
            DashboardHistoryOrdering.Sort(rows, DashboardHistorySortField.ApprovedSubmissions, descending: true).Select(value => value.EventId));
        Assert.Equal([first.EventId, second.EventId, missing.EventId],
            DashboardHistoryOrdering.Sort(rows, DashboardHistorySortField.WinnerBoard, descending: false).Select(value => value.EventId));
        Assert.Equal([second.EventId, first.EventId, missing.EventId],
            DashboardHistoryOrdering.Sort(rows, DashboardHistorySortField.WinnerBoard, descending: true).Select(value => value.EventId));
        Assert.Equal([second.EventId, first.EventId, missing.EventId],
            DashboardHistoryOrdering.Sort(rows, DashboardHistorySortField.Ehb, descending: false).Select(value => value.EventId));
        Assert.Equal([first.EventId, second.EventId, missing.EventId],
            DashboardHistoryOrdering.Sort(rows, DashboardHistorySortField.Ehb, descending: true).Select(value => value.EventId));
        Assert.Equal([first.EventId, second.EventId, missing.EventId],
            DashboardHistoryOrdering.Sort(rows, DashboardHistorySortField.Winner, descending: false).Select(value => value.EventId));
        Assert.Equal([second.EventId, first.EventId, missing.EventId],
            DashboardHistoryOrdering.Sort(rows, DashboardHistorySortField.Winner, descending: true).Select(value => value.EventId));

        var earliest = Row("00000000-0000-0000-0000-000000000004", "Delta", 1, 1, .50m, 1m,
            startedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var latest = Row("00000000-0000-0000-0000-000000000005", "Epsilon", 1, 1, .50m, 1m,
            startedAt: new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero));
        var missingDate = Row("00000000-0000-0000-0000-000000000006", "Zeta", 1, 1, .50m, 1m,
            missingDates: true);
        var dateRows = new[] { missingDate, latest, earliest };
        Assert.Equal([earliest.EventId, latest.EventId, missingDate.EventId],
            DashboardHistoryOrdering.Sort(dateRows, DashboardHistorySortField.EventDate, descending: false).Select(value => value.EventId));
        Assert.Equal([latest.EventId, earliest.EventId, missingDate.EventId],
            DashboardHistoryOrdering.Sort(dateRows, DashboardHistorySortField.EventDate, descending: true).Select(value => value.EventId));
    }

    [Fact]
    public void EqualSortValuesUseStableEventIdTieBreak()
    {
        var lower = Row("00000000-0000-0000-0000-000000000001", "Alpha", 1, 1, .50m, 1m);
        var higher = Row("00000000-0000-0000-0000-000000000002", "Beta", 1, 1, .50m, 1m);

        Assert.Equal([lower.EventId, higher.EventId],
            DashboardHistoryOrdering.Sort([higher, lower], DashboardHistorySortField.WinnerBoard, descending: false)
                .Select(value => value.EventId));
        Assert.Equal([lower.EventId, higher.EventId],
            DashboardHistoryOrdering.Sort([higher, lower], DashboardHistorySortField.WinnerBoard, descending: true)
                .Select(value => value.EventId));
    }

    private static DashboardHistoryRow Row(
        string eventId,
        string? winner,
        long? players,
        long? approved,
        decimal? ratio,
        decimal? ehb,
        DashboardEhbCoverage coverage = DashboardEhbCoverage.Complete,
        DashboardWinner? winnerValue = null,
        DateTimeOffset? startedAt = null,
        bool missingDates = false)
    {
        var id = Guid.Parse(eventId);
        var winners = winner is null
            ? Array.Empty<DashboardWinner>()
            : winnerValue is null
                ? new[] { new DashboardWinner(Guid.NewGuid(), winner, 1) }
                : new[] { winnerValue! };
        DateTimeOffset? actualStart = missingDates ? null : startedAt ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return new(id, winner ?? "Missing", (winner ?? "missing").ToLowerInvariant(), EventState.Archived, false,
            actualStart, actualStart?.AddDays(1), $"/Admin/Events/Manage/{eventId}",
            players is { } playerValue ? DashboardMetric<long>.Measured(playerValue) : DashboardMetric<long>.Unknown(),
            DashboardMetric<long>.Measured(1),
            approved is { } approvedValue ? DashboardMetric<long>.Measured(approvedValue) : DashboardMetric<long>.Unknown(),
            ratio is { } ratioValue ? new DashboardWinnerBoard(1, 2, ratioValue, winners) : null,
            new DashboardEhbSummary(ehb, coverage, 1, coverage == DashboardEhbCoverage.Unavailable ? 0 : 1), winners);
    }
}
