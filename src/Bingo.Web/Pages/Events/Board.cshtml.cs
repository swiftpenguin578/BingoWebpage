using Bingo.Application.Boards;
using Bingo.Application.Evidence;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Events;

public sealed class BoardModel(
    IPublicBoardService boards,
    IEventCompetitionActivityProjection activity,
    IEvidenceAuthority evidenceAuthority,
    TimeProvider time) : PageModel
{
    public const int DefaultRecentDropCount = 25;
    public const int RecentDropPageSize = 25;

    public PublicEventBoard Board { get; private set; } = null!;
    public EventCompetitionActivityProjection Activity { get; private set; } = null!;
    public IReadOnlyList<LeaderboardPlayer> Players { get; private set; } = [];
    public LeaderboardPlayer? MostSpooned { get; private set; }
    public LeaderboardPlayer? HighestDropEhb { get; private set; }
    public LeaderboardPlayer? HighestEhb { get; private set; }
    public string ActiveView { get; private set; } = "mission";
    public string ActiveRanking { get; private set; } = "activity";
    public string? DropSearch { get; private set; }
    public string? DropTeam { get; private set; }
    public string? SubmissionTeamSlug { get; private set; }

    public static string FormatElapsed(DateTimeOffset approvedAt, DateTimeOffset now)
    {
        var elapsed = now - approvedAt;
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

    public async Task<IActionResult> OnGetAsync(string slug, string? view, string? ranking, int? dropCount, string? dropSearch, string? dropTeam, CancellationToken cancellationToken)
    {
        var requestedDropCount = Math.Max(DefaultRecentDropCount, dropCount ?? DefaultRecentDropCount);
        DropSearch = string.IsNullOrWhiteSpace(dropSearch) ? null : dropSearch.Trim();
        DropTeam = string.IsNullOrWhiteSpace(dropTeam) ? null : dropTeam.Trim();
        var board = await boards.GetEventBoardAsync(slug, requestedDropCount, DropSearch, DropTeam, cancellationToken);
        if (board is null) return NotFound();
        Board = board;
        var accountId = User.GetAccountId();
        if (accountId is Guid actorAccountId)
        {
            try
            {
                var scope = await evidenceAuthority.ResolveActorAsync(actorAccountId, board.EventId, User.GetTeamId(), time.GetUtcNow(), cancellationToken);
                if (scope.Kind != EvidenceActorKind.Administrator && scope.EventId == board.EventId)
                    SubmissionTeamSlug = board.Teams.SingleOrDefault(team => team.TeamId == scope.TeamId)?.TeamSlug;
            }
            catch (InvalidOperationException)
            {
                SubmissionTeamSlug = null;
            }
        }
        Activity = await activity.GetAsync(board.EventId, cancellationToken);
        var rosterPlayers = board.RosterPlayers ?? Activity.Teams
            .SelectMany(team => team.Participants.Select(participant => new PublicRosterPlayer(
                team.TeamId, team.TeamName, participant.ParticipantId, participant.ParticipantName,
                participant.PlayingAccountNames is { Count: > 0 } ? participant.PlayingAccountNames : [participant.ParticipantName])))
            .ToList();
        var activityParticipantsById = Activity.Teams
            .SelectMany(team => team.Participants)
            .ToDictionary(participant => participant.ParticipantId);
        var dropPlayersById = (board.DropEhbTeams ?? Array.Empty<PublicDropEhbTeam>())
            .SelectMany(team => team.Players)
            .ToDictionary(player => player.PlayerId);
        var playerRows = rosterPlayers
            .Select(roster =>
            {
                var participant = activityParticipantsById.GetValueOrDefault(roster.PlayerId);
                var hasActivity = Activity.HasRankings && participant is not null;
                var displayParticipant = participant ?? new EventCompetitionParticipantActivity(
                    roster.PlayerId, roster.PlayerName, 0m, [], PlayingAccountNames: roster.PlayingAccountNames);
                var dropPlayer = dropPlayersById.GetValueOrDefault(roster.PlayerId);
                return new
                {
                    roster.TeamName,
                    Participant = displayParticipant,
                    HasActivity = hasActivity,
                    DropEhb = dropPlayer?.DropEhb ?? 0m,
                    TotalDrops = dropPlayer?.ApprovedSubmissions ?? 0
                };
            })
            .OrderByDescending(value => value.HasActivity)
            .ThenByDescending(value => value.HasActivity ? value.Participant.TotalGainedEhb : 0m)
            .ThenBy(value => value.Participant.ParticipantName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.TeamName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Participant.ParticipantId)
            .ToList();
        var rankedPlayers = new List<LeaderboardPlayer>(playerRows.Count);
        var activityRank = 0;
        for (var index = 0; index < playerRows.Count; index++)
        {
            var current = playerRows[index];
            var rank = 0;
            if (current.HasActivity)
            {
                activityRank++;
                rank = index == 0 || !playerRows[index - 1].HasActivity || playerRows[index - 1].Participant.TotalGainedEhb != current.Participant.TotalGainedEhb
                    ? activityRank
                    : rankedPlayers[index - 1].Rank;
            }
            rankedPlayers.Add(new(rank, current.TeamName, current.Participant, current.DropEhb, current.TotalDrops, current.HasActivity));
        }
        Players = rankedPlayers;
        MostSpooned = Players
            .Where(value => value.TotalDrops > 0)
            .OrderByDescending(value => value.TotalDrops)
            .ThenBy(value => value.Participant.ParticipantName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Participant.ParticipantId)
            .FirstOrDefault();
        HighestDropEhb = Players
            .Where(value => value.DropEhb > 0)
            .OrderByDescending(value => value.DropEhb)
            .ThenBy(value => value.Participant.ParticipantName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Participant.ParticipantId)
            .FirstOrDefault();
        HighestEhb = Players
            .Where(value => value.HasActivity && value.Participant.TotalGainedEhb > 0)
            .OrderByDescending(value => value.Participant.TotalGainedEhb)
            .ThenBy(value => value.Participant.ParticipantName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Participant.ParticipantId)
            .FirstOrDefault();
        ActiveView = view is "drops" or "leaderboards" ? view : "mission";
        ActiveRanking = ranking is "drops" or "players" ? ranking : "activity";
        return Page();
    }

    public sealed record LeaderboardPlayer(int Rank, string TeamName, EventCompetitionParticipantActivity Participant, decimal DropEhb, int TotalDrops, bool HasActivity);
}
