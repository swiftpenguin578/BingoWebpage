using System.Security.Claims;
using Bingo.Application.Boards;
using Bingo.Application.Evidence;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Web.Security;
using LeaderboardPlayer = Bingo.Web.Pages.Events.BoardModel.LeaderboardPlayer;

namespace Bingo.Web.UI;

/// <summary>The existing Board masthead's data, shared by all event overview destinations.</summary>
public sealed class EventMastheadModel
{
    public EventMastheadModel(PublicEventBoard board, EventCompetitionActivityProjection activity, string? submissionTeamSlug)
    {
        Board = board;
        Activity = activity;
        SubmissionTeamSlug = submissionTeamSlug;
        Players = BuildPlayers(board, activity);
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
    }

    public PublicEventBoard Board { get; }
    public EventCompetitionActivityProjection Activity { get; }
    public string? SubmissionTeamSlug { get; }
    public IReadOnlyList<LeaderboardPlayer> Players { get; }
    public LeaderboardPlayer? MostSpooned { get; }
    public LeaderboardPlayer? HighestDropEhb { get; }
    public LeaderboardPlayer? HighestEhb { get; }

    public static async Task<EventMastheadModel> CreateAsync(PublicEventBoard board,
        EventCompetitionActivityProjection activity, IEvidenceAuthority evidenceAuthority,
        ClaimsPrincipal user, TimeProvider time, CancellationToken cancellationToken)
    {
        string? SubmissionTeamSlug = null;
        var accountId = user.GetAccountId();
        if (accountId is Guid actorAccountId)
        {
            try
            {
                var scope = await evidenceAuthority.ResolveActorAsync(actorAccountId, board.EventId, user.GetTeamId(), time.GetUtcNow(), cancellationToken);
                if (scope.Kind != EvidenceActorKind.Administrator && scope.EventId == board.EventId)
                    SubmissionTeamSlug = board.Teams.SingleOrDefault(team => team.TeamId == scope.TeamId)?.TeamSlug;
            }
            catch (InvalidOperationException)
            {
                SubmissionTeamSlug = null;
            }
        }
        return new(board, activity, SubmissionTeamSlug);
    }

    private static List<LeaderboardPlayer> BuildPlayers(PublicEventBoard board, EventCompetitionActivityProjection Activity)
    {
        var rosterPlayers = board.RosterPlayers ?? Activity.Teams
            .SelectMany(team => team.Participants.Select(participant => new PublicRosterPlayer(
                team.TeamId, team.TeamName, participant.ParticipantId, participant.ParticipantName,
                participant.PlayingAccountNames is { Count: > 0 } ? participant.PlayingAccountNames : [participant.ParticipantName])))
            .ToList();
        var activityParticipantsByTeam = Activity.Teams
            .SelectMany(team => team.Participants.Select(participant => (team.TeamId, Participant: participant)))
            .ToDictionary(value => (value.TeamId, value.Participant.ParticipantId), value => value.Participant);
        var dropTeams = board.DropEhbTeams ?? Array.Empty<PublicDropEhbTeam>();
        var dropPlayersByTeam = dropTeams
            .SelectMany(team => team.Players.Select(player => (team.TeamId, Player: player)))
            .ToDictionary(value => (value.TeamId, value.Player.PlayerId), value => value.Player);
        var rosterKeys = rosterPlayers.Select(player => (player.TeamId, player.PlayerId)).ToHashSet();
        // Retained credit is displayed on its owning team, without changing the roster
        // or borrowing activity from the participant's current team.
        var displayPlayers = rosterPlayers.Concat(dropTeams.SelectMany(team => team.Players
            .Where(player => !rosterKeys.Contains((team.TeamId, player.PlayerId)))
            .Select(player => new PublicRosterPlayer(team.TeamId, team.TeamName, player.PlayerId,
                player.PlayerName, player.PlayingAccountNames ?? [player.PlayerName]))));
        var playerRows = displayPlayers
            .Select(roster =>
            {
                var participant = activityParticipantsByTeam.GetValueOrDefault((roster.TeamId, roster.PlayerId));
                var hasActivity = Activity.HasRankings && participant is not null;
                var displayParticipant = participant ?? new EventCompetitionParticipantActivity(
                    roster.PlayerId, roster.PlayerName, 0m, [], PlayingAccountNames: roster.PlayingAccountNames);
                var dropPlayer = dropPlayersByTeam.GetValueOrDefault((roster.TeamId, roster.PlayerId));
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
        return rankedPlayers;
    }
}
