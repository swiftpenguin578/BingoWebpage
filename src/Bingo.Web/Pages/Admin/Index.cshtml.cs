using Bingo.Application.Access;
using Bingo.Application.Dashboard;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Bingo.Domain.Events;
using System.Globalization;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Admin;

[Authorize(Policy = AuthorizationPolicies.Admin)]
[AdminDesign]
public sealed class IndexModel(IAdminDashboardService dashboard, IStringLocalizer<AdminCommunityResource> text) : PageModel
{
    public AdminDashboardResult? Dashboard { get; private set; }
    public IReadOnlyList<DashboardHistoryRow> History { get; private set; } = [];
    public DashboardHistorySortField Sort { get; private set; } = DashboardHistorySortField.EventDate;
    public bool Descending { get; private set; } = true;
    public sealed record HistoryOrder(Guid[] Asc, Guid[] Desc);
    public IReadOnlyDictionary<string, HistoryOrder> HistoryOrders => Enum.GetValues<DashboardHistorySortField>()
        .ToDictionary(sortField => sortField.ToString(), sortField => new HistoryOrder(
            DashboardHistoryOrdering.Sort(Dashboard!.History, sortField, false).Select(row => row.EventId).ToArray(),
            DashboardHistoryOrdering.Sort(Dashboard!.History, sortField, true).Select(row => row.EventId).ToArray()));
    public string Summary
    {
        get
        {
            if (Dashboard?.History.Count is not > 0) return L("No events held yet");
            var result = L(Dashboard.History.Count == 1 ? "{0} event since {1}" : "{0} events since {1}",
                Number(Dashboard.History.Count), Month(Dashboard.Chart.Count > 0 ? Dashboard.Chart[0].ActualStartedAt : null, Dashboard.Chart.Count > 0 ? Dashboard.Chart[0].Timezone : null));
            if (Dashboard.LatestEndedRecap is { } recap)
            {
                result += L(" · last ended {0}", Date(recap.ActualEndedAt, Dashboard.History.Single(row => row.EventId == recap.EventId).Timezone));
                var zone = Dashboard.History.Single(row => row.EventId == recap.EventId).Timezone;
                var days = (DateTimePresentation.ToTimezone(Dashboard.AsOf, zone).Date - DateTimePresentation.ToTimezone(recap.ActualEndedAt, zone).Date).Days;
                if (days > 0) result += L(days == 1 ? " ({0} day ago)" : " ({0} days ago)", Number(days));
            }
            return result;
        }
    }
    public string L(string key, params object[] args) => text[key, args].Value;
    public static string Number(long value) => value.ToString("N0", CultureInfo.CurrentCulture);
    public static string Value(DashboardMetric<long> metric) => metric.IsAvailable ? Number(metric.Value) : "—";
    public string ChartLabel(DashboardParticipationPoint point)
    {
        var label = !point.Participants.IsAvailable
            ? L("{0}: participant count unavailable, {1} teams.", point.EventName, Number(point.TeamCount))
            : point.IsHistoricalImport ? L("{0}: {1} players, imported, not linked to website accounts.", point.EventName, Number(point.Participants.Value))
            : point.TrackingStarts ? L("{0}: {1} players, all first tracked.", point.EventName, point.Participants.Value)
            : L("{0}: {1} players, {2} returning, {3} first time.", point.EventName, Number(point.Participants.Value), Number(point.ReturningWebsiteParticipants.Value), Number(point.NewWebsiteParticipants.Value));
        if (point.Provisional) label += " " + L(point.State == EventState.Live ? "Live · provisional" : "Provisional, awaiting final review.");
        return label;
    }
    public string Date(DateTimeOffset? value, string? timezone = null) => value is { } at ? DateTimePresentation.Format(at, "d MMM yyyy", timezone, provider: CultureInfo.CurrentCulture) : L("Not announced");
    public string Month(DateTimeOffset? value, string? timezone = null) => value is { } at ? DateTimePresentation.Format(at, "MMM yyyy", timezone, provider: CultureInfo.CurrentCulture) : L("Not recorded");
    public string Range(DateTimeOffset? start, DateTimeOffset? end, string? timezone = null)
    {
        if (start is null) return L("Not recorded");
        if (end is null) return Date(start, timezone);
        var a = DateTimePresentation.ToTimezone(start.Value, timezone);
        var b = DateTimePresentation.ToTimezone(end.Value, timezone);
        var format = a.Year != b.Year ? "d MMM yyyy" : a.Month != b.Month ? "d MMM" : "%d";
        return $"{a.ToString(format, CultureInfo.CurrentCulture)}–{b.ToString("d MMM yyyy", CultureInfo.CurrentCulture)}";
    }
    public static string Percent(decimal? ratio) => Math.Floor((ratio ?? 0m) * 100m + 0.5m).ToString("0", CultureInfo.InvariantCulture);
    public string Winners(IReadOnlyList<DashboardWinner> winners) => winners.Count == 0 ? L("Not recorded") : string.Join(" · ", winners.Select(value => value.TeamName));
    public string ChartTipNote(DashboardParticipationPoint point) => point.IsHistoricalImport
        ? L("Imported history. Players aren’t linked to website accounts, so they aren’t split into returning and first time.")
        : point.Provisional ? point.State == EventState.Live ? L("Figures are provisional and may still change.")
            : L("Ended {0} · awaiting final review. Figures may still change.", DateTimePresentation.Format(point.ActualEndedAt!.Value, "d MMM yyyy", point.Timezone, CultureInfo.CurrentCulture))
        : L("Tracked history starts here, so there’s no earlier event to return from.");
    public string EhbHint(DashboardHistoryRow row) => row.Ehb.Coverage == DashboardEhbCoverage.Unavailable
        ? row.HasLinkedCompetition ? L("No compatible stored EHB coverage is available.") : L("Wise Old Man wasn’t linked to this event.")
        : L("Wise Old Man · {0} of {1} accounts", Number(row.Ehb.MatchedAccounts), Number(row.Ehb.ExpectedAccounts))
            + (row.IsHistoricalImport ? L(" · frozen snapshot") : "");
    public string SubmissionHint(DashboardHistoryRow row) => row.ApprovedSubmissions.IsAvailable
        ? L("Approved evidence submissions. Reconstructed imported contributions are excluded.")
        : row.IsHistoricalImport ? L("Not available for imported history: contributions were reconstructed, not submitted.")
        : L("Published board approval or a usable event interval is unavailable.");
    public string SortUrl(DashboardHistorySortField field) => Url.Page("/Admin/Index", new { sort = field.ToString(), direction = field == Sort ? Descending ? "asc" : "desc" : field == DashboardHistorySortField.Winner ? "asc" : "desc" })!;
    public string NextDate(DashboardEventCard card)
    {
        if (card.IsOverdue) return L("Start was due {0}", Date(card.ScheduledStartAt, card.Timezone));
        if (card.NextDate is null) return L("Not announced");
        var at = DateTimePresentation.Format(card.NextDate.Value, "d MMM", card.Timezone, CultureInfo.CurrentCulture);
        return card.NextDateKind switch
        {
            DashboardNextDateKind.SignupsOpen => L("opens {0}", at),
            DashboardNextDateKind.SignupsClose => L("closes {0}", at),
            DashboardNextDateKind.EventEnds => L("ends {0}", at),
            _ => L("starts {0}", at)
        };
    }
    public string Phase(EventState state) => L(state switch
    {
        EventState.Draft => "Setup", EventState.SignupOpen => "Signups open", EventState.SignupClosed => "Signups closed",
        EventState.Live => "Live", EventState.AwaitingFinalReview => "Final review", EventState.Finalized => "Finalized", _ => "Archived"
    });

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (User.GetAccountId() is not { } actorId) return Forbid();
        try
        {
            Dashboard = await dashboard.GetAsync(actorId, cancellationToken);
            if (Enum.TryParse<DashboardHistorySortField>(Request.Query["sort"], out var sort) && Enum.IsDefined(sort)) Sort = sort;
            Descending = Request.Query["direction"] != "asc";
            History = DashboardHistoryOrdering.Sort(Dashboard.History, Sort, Descending);
            return Page();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
