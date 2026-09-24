using Bingo.Application.Announcements;
using Bingo.Application.Boards;
using Bingo.Application.Evidence;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Events;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Events;

public sealed class BoardModel(
    IPublicBoardService boards,
    IEventCompetitionActivityProjection activity,
    IEvidenceAuthority evidenceAuthority,
    IDropAnnouncementService dropAnnouncements,
    TimeProvider time) : PageModel
{
    public const int DefaultRecentDropCount = 25;
    public const int RecentDropPageSize = 25;

    public PublicEventBoard Board { get; private set; } = null!;
    public EventCompetitionActivityProjection Activity { get; private set; } = null!;
    public EventCompetitionMetricLeaderboard MetricLeaderboard { get; private set; } = new(EventCompetitionActivityState.NotConfigured, 0, null, null, [], null, []);
    public IReadOnlyList<LeaderboardPlayer> Players { get; private set; } = [];
    public EventMastheadModel Masthead { get; private set; } = null!;
    public LeaderboardPlayer? MostSpooned => Masthead.MostSpooned;
    public LeaderboardPlayer? HighestDropEhb => Masthead.HighestDropEhb;
    public LeaderboardPlayer? HighestEhb => Masthead.HighestEhb;
    public string ActiveView { get; private set; } = "mission";
    public string ActiveRanking { get; private set; } = "activity";
    public bool IsMetricMode => MetricLeaderboard.IsBossMode;
    public string? SelectedMetric => MetricLeaderboard.Selected?.Metric;
    public string? DropSearch { get; private set; }
    public string? DropTeam { get; private set; }
    public string? SubmissionTeamSlug => Masthead.SubmissionTeamSlug;
    public DateTimeOffset RecentDropsReconcileSince { get; private set; }
    public Guid? SubmissionId { get; private set; }
    public PublicRecentDrop? SelectedDrop { get; private set; }
    public IReadOnlySet<Guid> NewSubmissionIds { get; private set; } = new HashSet<Guid>();
    public int NewDropCount { get; private set; }

    public static string FormatElapsed(DateTimeOffset submittedAt, DateTimeOffset now)
    {
        var elapsed = now - submittedAt;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        var totalMinutes = (int)elapsed.TotalMinutes;
        if (totalMinutes < 60) return $"{totalMinutes} min ago";

        var totalHours = totalMinutes / 60;
        if (totalHours < 24) return Unit(totalHours, "hr") + " ago";

        var totalDays = totalHours / 24;
        if (totalDays < 7)
        {
            var hours = totalHours % 24;
            return JoinUnits(Unit(totalDays, "day"), hours == 0 ? null : Unit(hours, "hr")) + " ago";
        }

        var totalWeeks = totalDays / 7;
        if (totalDays < 30)
        {
            var days = totalDays % 7;
            return JoinUnits(Unit(totalWeeks, "week"), days == 0 ? null : Unit(days, "day")) + " ago";
        }

        var totalMonths = totalDays / 30;
        if (totalDays < 365) return Unit(totalMonths, "month") + " ago";

        var years = totalDays / 365;
        var months = totalDays % 365 / 30;
        return JoinUnits(Unit(years, "year"), months == 0 ? null : Unit(months, "month")) + " ago";
    }

    private static string Unit(int value, string singular) => $"{value} {(value == 1 ? singular : singular + "s")}";

    private static string JoinUnits(string first, string? second) => second is null ? first : $"{first} {second}";

    public async Task<IActionResult> OnGetAsync(string slug, string? view, string? ranking, string? metric, int? dropCount, string? dropSearch, string? dropTeam, Guid? submissionId, CancellationToken cancellationToken)
    {
        RecentDropsReconcileSince = time.GetUtcNow();
        var requestedDropCount = Math.Max(DefaultRecentDropCount, dropCount ?? DefaultRecentDropCount);
        DropSearch = string.IsNullOrWhiteSpace(dropSearch) ? null : dropSearch.Trim();
        DropTeam = string.IsNullOrWhiteSpace(dropTeam) ? null : dropTeam.Trim();
        var board = await boards.GetEventBoardAsync(slug, requestedDropCount, DropSearch, DropTeam, cancellationToken);
        if (board is null) return NotFound();
        Board = board;
        if (board.EventState == EventState.Cancelled) return Page();
        SubmissionId = submissionId;
        if (submissionId is Guid selectedSubmissionId)
            SelectedDrop = await boards.GetRecentDropAsync(board.EventSlug, selectedSubmissionId, cancellationToken);
        var accountId = User.GetAccountId();
        if (accountId is Guid currentAccountId)
        {
            var announcementSnapshot = await dropAnnouncements.GetAsync(currentAccountId, board.EventId, 1, 0, null, cancellationToken);
            NewDropCount = announcementSnapshot?.NewCount ?? 0;
            var loadedSubmissionIds = board.RecentDrops.Select(value => value.SubmissionId).ToArray();
            if (loadedSubmissionIds.Length > 0)
                NewSubmissionIds = (await dropAnnouncements.GetNewSubmissionIdsAsync(currentAccountId, board.EventId, loadedSubmissionIds, cancellationToken)).ToHashSet();
        }
        Activity = await activity.GetAsync(board.EventId, cancellationToken);
        MetricLeaderboard = view == "leaderboards" || !string.IsNullOrWhiteSpace(metric)
            ? await activity.GetMetricLeaderboardAsync(board.EventId, metric, cancellationToken)
            : new(EventCompetitionActivityState.NotConfigured, 0, null, null, [], null, []);
        Masthead = await EventMastheadModel.CreateAsync(board, Activity, evidenceAuthority, User, time, cancellationToken);
        Players = Masthead.Players;
        ActiveView = view is "drops" or "leaderboards" ? view : "mission";
        ActiveRanking = IsMetricMode
            ? ranking is "teams" or "players" ? ranking : "teams"
            : ranking is "drops" or "players" ? ranking : "activity";
        return Page();
    }

    public sealed record LeaderboardPlayer(int Rank, string TeamName, EventCompetitionParticipantActivity Participant, decimal DropEhb, int TotalDrops, bool HasActivity);
}
