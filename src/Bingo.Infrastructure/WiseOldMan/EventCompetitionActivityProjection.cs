using System.Security.Cryptography;
using System.Text;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.WiseOldMan;

public sealed class CachedEventCompetitionActivityProjection(
    ApplicationDbContext db,
    TimeProvider time) : IEventCompetitionActivityProjection
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(2);

    public Task<EventCompetitionActivityProjection> GetAsync(
        Guid eventId,
        CancellationToken cancellationToken = default) =>
        BuildAsync(eventId, null, cancellationToken);

    public async Task<EventCompetitionTeamActivity?> GetTeamAsync(
        Guid eventId,
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var projection = await BuildAsync(eventId, teamId, cancellationToken);
        return projection.Teams.SingleOrDefault();
    }

    private async Task<EventCompetitionActivityProjection> BuildAsync(
        Guid eventId,
        Guid? teamId,
        CancellationToken cancellationToken)
    {
        if (!await db.Events.AsNoTracking().AnyAsync(value => value.Id == eventId && value.HiddenAt == null, cancellationToken))
            return new(EventCompetitionActivityState.NotConfigured, 0, null, null, []);

        var state = await db.EventCompetitionSynchronizations.AsNoTracking()
            .Where(value => value.EventId == eventId)
            .FirstOrDefaultAsync(cancellationToken);
        if (state is null || state.CompetitionId is null)
            return new(EventCompetitionActivityState.NotConfigured, 0, null, null, []);

        var activityState = GetState(state);
        var retainsPartialCache = activityState == EventCompetitionActivityState.TemporarilyUnavailable && HasRetryablePartialCache(state);
        if (state.LatestComplete != true && activityState != EventCompetitionActivityState.Partial && !retainsPartialCache)
            return new(activityState, state.Generation, state.LastSuccessfulAt, state.LastUpstreamUpdatedAt, []);

        var currentAssignments = await db.EventParticipantCharacters.AsNoTracking()
            .Where(value => value.EventId == eventId && value.EventRole == EventCharacterRole.Playing && value.ReleasedAt == null &&
                            db.EventParticipants.Any(participant => participant.Id == value.EventParticipantId && participant.SignupStatus == SignupStatus.Confirmed))
            .OrderBy(value => value.Id)
            .Select(value => new { value.Id, value.EventParticipantId, value.OsrsCharacterId })
            .ToListAsync(cancellationToken);
        var currentFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', currentAssignments
            .Select(value => $"{value.Id:N}:{value.EventParticipantId:N}:{value.OsrsCharacterId:N}"))))).ToLowerInvariant();
        if (!string.Equals(currentFingerprint, state.AssignmentFingerprint, StringComparison.Ordinal))
            return new(EventCompetitionActivityState.Incomplete, state.Generation, state.LastSuccessfulAt, state.LastUpstreamUpdatedAt, [], currentAssignments.Count, 0);

        var cached = await db.EventCompetitionCharacterActivities.AsNoTracking()
            .Where(value => value.EventId == eventId && value.Generation == state.Generation && value.AssignmentFingerprint == state.AssignmentFingerprint)
            .ToListAsync(cancellationToken);
        var cachedByCharacter = cached.ToDictionary(value => value.OsrsCharacterId);
        var matchedAssignments = currentAssignments.Where(value => cachedByCharacter.ContainsKey(value.OsrsCharacterId)).ToList();
        if (state.LatestComplete == true && matchedAssignments.Count != currentAssignments.Count)
            return new(EventCompetitionActivityState.Incomplete, state.Generation, state.LastSuccessfulAt, state.LastUpstreamUpdatedAt, [], currentAssignments.Count, matchedAssignments.Count);
        var teams = await db.Teams.AsNoTracking()
            .Where(value => value.EventId == eventId && value.Active && value.FinalizedAt != null && (teamId == null || value.Id == teamId))
            .OrderBy(value => value.Name).ThenBy(value => value.Id)
            .Select(value => new { value.Id, value.Name })
            .ToListAsync(cancellationToken);
        if (teams.Count == 0)
            return new(activityState, state.Generation, state.LastSuccessfulAt, state.LastUpstreamUpdatedAt, [], currentAssignments.Count, matchedAssignments.Count);

        var teamIds = teams.Select(value => value.Id).ToList();
        var memberships = await db.TeamMemberships.AsNoTracking()
            .Where(value => teamIds.Contains(value.TeamId) && value.LeftAt == null)
            .OrderBy(value => value.TeamId).ThenBy(value => value.EventParticipantId)
            .Select(value => new { value.TeamId, value.EventParticipantId })
            .ToListAsync(cancellationToken);
        var participantIds = memberships.Select(value => value.EventParticipantId).Distinct().ToList();
        var assignments = await (from assignment in db.EventParticipantCharacters.AsNoTracking()
                                 join character in db.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                                 where assignment.EventId == eventId && participantIds.Contains(assignment.EventParticipantId) &&
                                       assignment.EventRole == EventCharacterRole.Playing && assignment.ReleasedAt == null
                                 orderby assignment.EventParticipantId, assignment.RegistrationOrder, assignment.Id
                                 select new
                                 {
                                     assignment.EventParticipantId,
                                     assignment.OsrsCharacterId,
                                     assignment.RegistrationOrder,
                                     CharacterName = character.DisplayName
                                 }).ToListAsync(cancellationToken);
        var activities = new List<EventCompetitionTeamActivity>(teams.Count);

        foreach (var team in teams)
        {
            var teamParticipantIds = memberships.Where(value => value.TeamId == team.Id)
                .Select(value => value.EventParticipantId).Distinct().ToList();
            var teamAssignments = assignments.Where(value => teamParticipantIds.Contains(value.EventParticipantId)).ToList();
            var teamParticipants = teamParticipantIds
                .Select(participantId =>
                {
                    var expectedAssignments = assignments.Where(value => value.EventParticipantId == participantId).ToList();
                    var participantAssignments = expectedAssignments
                        .Where(value => cachedByCharacter.ContainsKey(value.OsrsCharacterId))
                        .OrderBy(value => value.RegistrationOrder).ThenBy(value => value.OsrsCharacterId)
                        .Select(value => new EventCompetitionAccountActivity(
                            value.OsrsCharacterId,
                            value.CharacterName,
                            cachedByCharacter[value.OsrsCharacterId].GainedEhb,
                            cachedByCharacter[value.OsrsCharacterId].StartEhb,
                            cachedByCharacter[value.OsrsCharacterId].EndEhb))
                        .ToList();
                    if (participantAssignments.Count == 0) return null;
                    var playingAccountNames = expectedAssignments.Select(value => value.CharacterName).ToList();
                    var participantName = playingAccountNames.FirstOrDefault() ?? "Participant";
                    return new EventCompetitionParticipantActivity(
                        participantId,
                        participantName,
                        participantAssignments.Sum(value => value.GainedEhb),
                        participantAssignments,
                        expectedAssignments.Count,
                        participantAssignments.Count,
                        playingAccountNames);
                })
                .Where(value => value is not null)
                .Select(value => value!)
                .OrderByDescending(value => value.TotalGainedEhb)
                .ThenBy(value => value.ParticipantName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.ParticipantId)
                .ToList();
            var total = teamParticipants.Sum(value => value.TotalGainedEhb);
            var average = teamParticipants.Count == 0 ? 0 : total / teamParticipants.Count;
            var maximum = teamParticipants.Count == 0 ? 0 : teamParticipants.Max(value => value.TotalGainedEhb);
            var mvps = teamParticipants.Where(value => value.TotalGainedEhb == maximum)
                .OrderBy(value => value.ParticipantName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.ParticipantId)
                .Select(value => value.ParticipantName)
                .ToList();
            activities.Add(new(team.Id, team.Name, total, average, teamParticipants, mvps,
                teamParticipantIds.Count, teamParticipants.Count, teamAssignments.Count,
                teamAssignments.Count(value => cachedByCharacter.ContainsKey(value.OsrsCharacterId))));
        }

        var orderedActivities = activities
            .OrderByDescending(value => value.TotalGainedEhb)
            .ThenBy(value => value.TeamName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.TeamId)
            .ToList();
        var rankedActivities = new List<EventCompetitionTeamActivity>(orderedActivities.Count);
        for (var index = 0; index < orderedActivities.Count; index++)
        {
            var rank = index == 0 || orderedActivities[index - 1].TotalGainedEhb != orderedActivities[index].TotalGainedEhb
                ? index + 1
                : rankedActivities[index - 1].Rank;
            rankedActivities.Add(orderedActivities[index] with { Rank = rank });
        }

        return new(activityState, state.Generation, state.LastSuccessfulAt, state.LastUpstreamUpdatedAt, rankedActivities,
            currentAssignments.Count, matchedAssignments.Count);
    }

    private EventCompetitionActivityState GetState(EventCompetitionSynchronization state)
    {
        if (state.LastAttemptAt is null)
            return EventCompetitionActivityState.WaitingForFirstSync;
        if (state.LatestComplete != true)
            return state.LastErrorKind == "Incomplete" && state.LastSuccessfulAt is not null
                ? EventCompetitionActivityState.Partial
                : state.LastErrorKind == "Incomplete"
                ? EventCompetitionActivityState.Incomplete
                : EventCompetitionActivityState.TemporarilyUnavailable;
        if (!string.IsNullOrWhiteSpace(state.LastErrorKind))
            return EventCompetitionActivityState.TemporarilyUnavailable;
        return state.LastSuccessfulAt is { } fetchedAt && fetchedAt.Add(StaleAfter) <= time.GetUtcNow()
            ? EventCompetitionActivityState.Stale
            : EventCompetitionActivityState.Complete;
    }

    private static bool HasRetryablePartialCache(EventCompetitionSynchronization state) =>
        state.LastSuccessfulAt is not null && state.LastErrorKind is "RateLimited" or "Unavailable";
}
