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
    public string Summary
    {
        get
        {
            if (Dashboard?.History.Count is not > 0) return L("No events held yet");
            var result = L(Dashboard.History.Count == 1 ? "{0} event since {1}" : "{0} events since {1}",
                Dashboard.History.Count, Month(Dashboard.Chart.Count > 0 ? Dashboard.Chart[0].ActualStartedAt : null));
            if (Dashboard.LatestEndedRecap is { } recap)
            {
                result += L(" · last ended {0}", Date(recap.ActualEndedAt));
                var days = (DateTimePresentation.ToTimezone(Dashboard.AsOf).Date - DateTimePresentation.ToTimezone(recap.ActualEndedAt).Date).Days;
                if (days > 0) result += L(days == 1 ? " ({0} day ago)" : " ({0} days ago)", days);
            }
            return result;
        }
    }
    public string L(string key, params object[] args) => text[key, args].Value;
    public static string Number(long value) => value.ToString("N0", CultureInfo.CurrentCulture);
    public static string Value(DashboardMetric<long> metric) => metric.IsAvailable ? Number(metric.Value) : "—";
    public string ChartLabel(DashboardParticipationPoint point) => point.Participants.IsAvailable
        ? L("{0}: {1} players, {2} teams.", point.EventName, point.Participants.Value, point.TeamCount)
        : L("{0}: participant count unavailable, {1} teams.", point.EventName, point.TeamCount);
    public string Date(DateTimeOffset? value) => value is { } at ? DateTimePresentation.Format(at, "d MMM yyyy", provider: CultureInfo.CurrentCulture) : L("Not announced");
    public string Month(DateTimeOffset? value) => value is { } at ? DateTimePresentation.Format(at, "MMM yyyy", provider: CultureInfo.CurrentCulture) : L("Not recorded");
    public string Range(DateTimeOffset? start, DateTimeOffset? end)
    {
        if (start is null) return L("Not recorded");
        if (end is null) return Date(start);
        var a = DateTimePresentation.ToTimezone(start.Value);
        var b = DateTimePresentation.ToTimezone(end.Value);
        var format = a.Year != b.Year ? "d MMM yyyy" : a.Month != b.Month ? "d MMM" : "%d";
        return $"{a.ToString(format, CultureInfo.CurrentCulture)}–{b.ToString("d MMM yyyy", CultureInfo.CurrentCulture)}";
    }
    public static string Percent(decimal? ratio) => ((ratio ?? 0m) * 100m).ToString("0.##", CultureInfo.InvariantCulture);
    public string Winners(IReadOnlyList<DashboardWinner> winners) => winners.Count == 0 ? L("Not recorded") : string.Join(" · ", winners.Select(value => value.TeamName));
    public string EhbHint(DashboardHistoryRow row) => row.Ehb.Coverage == DashboardEhbCoverage.Unavailable
        ? L("No compatible stored EHB coverage is available.")
        : L("Wise Old Man · {0} of {1} accounts", row.Ehb.MatchedAccounts, row.Ehb.ExpectedAccounts)
            + (row.IsHistoricalImport ? L(" · frozen snapshot") : "")
            + (row.Ehb.Coverage == DashboardEhbCoverage.Partial ? L(" · partial coverage") : "");
    public string SubmissionHint(DashboardHistoryRow row) => row.ApprovedSubmissions.IsAvailable
        ? L("Approved evidence submissions. Reconstructed imported contributions are excluded.")
        : row.IsHistoricalImport ? L("Imported history has no measured evidence submissions; its contributions were reconstructed.")
        : L("Published board approval or a usable event interval is unavailable.");
    public string SortUrl(DashboardHistorySortField field) => Url.Page("/Admin/Index", new { sort = field.ToString(), direction = field == Sort && Descending ? "asc" : "desc" })!;
    public string NextDate(DashboardEventCard card)
    {
        if (card.IsOverdue) return L("Start was due {0}", Date(card.ScheduledStartAt));
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
