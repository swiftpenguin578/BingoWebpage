using System.Data;
using System.Security.Cryptography;
using System.Text;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.WiseOldMan;

public sealed class CachedEventCompetitionActivityProjection(
    ApplicationDbContext db,
    TimeProvider time) : IEventCompetitionActivityProjection
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(1);

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

    public async Task<EventCompetitionMetricLeaderboard> GetMetricLeaderboardAsync(
        Guid eventId,
        string? metric,
        CancellationToken cancellationToken = default)
    {
        await using var ownedTransaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : null;

        var eventRow = await db.Events.AsNoTracking()
            .Where(value => value.Id == eventId && value.HiddenAt == null)
            .Select(value => new { value.State })
            .SingleOrDefaultAsync(cancellationToken);
        if (eventRow is null || eventRow.State is EventState.Cancelled or EventState.Discarded)
            return new(EventCompetitionActivityState.NotConfigured, 0, null, null, [], null, []);

        var state = await db.EventCompetitionSynchronizations.AsNoTracking()
            .Where(value => value.EventId == eventId)
            .OrderByDescending(value => value.Generation)
            .FirstOrDefaultAsync(cancellationToken);
        if (state?.CompetitionId is null)
            return new(EventCompetitionActivityState.NotConfigured, state?.Generation ?? 0, state?.LastSuccessfulAt, state?.LastUpstreamUpdatedAt, [], null, [],
                false, false, false, state?.LastMetricAttemptAt);

        var sources = await db.LuckSourceRequestAsync(eventId, cancellationToken);
        var publicationBoard = await db.Boards.AsNoTracking()
            .SingleOrDefaultAsync(value => value.EventId == eventId && value.State == BoardState.Published, cancellationToken);
        var publication = publicationBoard?.ActiveApprovalSnapshotId is { } approvalId
            ? await db.ApprovalObjectivesAsync(publicationBoard.Id, approvalId, cancellationToken)
            : null;
        var publishedDrops = publication?.Drops ?? [];
        var bossNameByOutcome = publishedDrops
            .GroupBy(value => (value.SourceDropId, value.ItemIdSnapshot))
            .ToDictionary(value => value.Key, value => value.Select(drop => drop.BossName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? string.Empty);
        var options = sources.Outcomes
            .Where(value => value.Basis?.Metric is { Length: > 0 } metricName && CompetitionMetricContract.SupportedMetrics.Contains(metricName))
            .GroupBy(value => value.Basis!.Metric!, StringComparer.Ordinal)
            .Select(group => new EventCompetitionMetricOption(
                group.Key,
                bossNameByOutcome.GetValueOrDefault((group.First().SourceDropId, group.First().ItemIdSnapshot)) is { Length: > 0 } name
                    ? name
                    : HumanizeMetric(group.Key)))
            .OrderBy(value => value.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.Metric, StringComparer.Ordinal)
            .ToList();
        var selected = options.SingleOrDefault(value => string.Equals(value.Metric, metric, StringComparison.Ordinal));
        var baseState = state.LastMetricAttemptAt is null
            ? EventCompetitionActivityState.WaitingForFirstSync
            : EventCompetitionActivityState.Incomplete;
        if (selected is null)
            return new(baseState, state?.Generation ?? 0, state?.LastSuccessfulAt, state?.LastUpstreamUpdatedAt, options, null, [],
                false, false, false, state?.LastMetricAttemptAt);

        // This is the only raw metric read. It enforces the source/assignment/generation/batch
        // boundary and participates in the repeatable-read snapshot above.
        var cache = await EventCompetitionSynchronizationService.ReadMetricCacheAsync(db, time, eventId, cancellationToken);
        var teams = await db.Teams.AsNoTracking()
            .Where(value => value.EventId == eventId && value.Active && value.FinalizedAt != null)
            .OrderBy(value => value.Name).ThenBy(value => value.Id)
            .Select(value => new { value.Id, value.Name, value.Slug })
            .ToListAsync(cancellationToken);
        var teamIds = teams.Select(value => value.Id).ToArray();
        var memberships = await (from membership in db.TeamMemberships.AsNoTracking()
                                 join participant in db.EventParticipants.AsNoTracking() on membership.EventParticipantId equals participant.Id
                                 where teamIds.Contains(membership.TeamId) && membership.LeftAt == null && participant.SignupStatus == SignupStatus.Confirmed
                                 select new { membership.TeamId, membership.EventParticipantId })
            .ToListAsync(cancellationToken);
        var participantIds = memberships.Select(value => value.EventParticipantId).Distinct().ToArray();
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
                                     assignment.Id,
                                     CharacterName = character.DisplayName
                                 }).ToListAsync(cancellationToken);
        var assignmentsByParticipant = assignments
            .GroupBy(value => value.EventParticipantId)
            .ToDictionary(value => value.Key, value => value
                .OrderBy(item => item.RegistrationOrder).ThenBy(item => item.Id)
                .GroupBy(item => item.OsrsCharacterId)
                .Select(item => item.First())
                .ToList());

        var selectedSourceKeys = sources.Outcomes
            .Where(value => string.Equals(value.Basis?.Metric, selected.Metric, StringComparison.Ordinal))
            .Select(value => (value.SourceDropId, value.ItemIdSnapshot))
            .ToHashSet();
        var selectedDropIds = publishedDrops
            .Where(value => selectedSourceKeys.Contains((value.SourceDropId, value.ItemIdSnapshot)))
            .Select(value => value.Id)
            .ToHashSet();
        var publishedRequirementIds = publication?.Requirements.Select(value => value.Id).ToHashSet() ?? [];
        var approvedRows = selectedDropIds.Count == 0 || teamIds.Length == 0
            ? []
            : (await (from contribution in db.SubmissionContributions.AsNoTracking()
                      join submission in db.Submissions.AsNoTracking() on contribution.SubmissionId equals submission.Id
                      where teamIds.Contains(contribution.TeamId) && submission.EventId == eventId &&
                            publishedRequirementIds.Contains(contribution.RequirementId) && contribution.ReversedAt == null && submission.Status == SubmissionStatus.Approved &&
                            submission.DropSnapshotId != null && selectedDropIds.Contains(submission.DropSnapshotId.Value)
                      select new
                      {
                          SubmissionId = submission.Id,
                          contribution.TeamId,
                          submission.CreditedParticipantId,
                          submission.CreditedOsrsCharacterId
                      }).ToListAsync(cancellationToken))
                .GroupBy(value => value.SubmissionId)
                .Select(value => value.First())
                .ToList();
        var dropCountByParticipant = approvedRows
            .GroupBy(value => value.CreditedParticipantId)
            .ToDictionary(value => value.Key, value => value.Count());
        var dropsByParticipantCharacter = approvedRows
            .GroupBy(value => (value.CreditedParticipantId, value.CreditedOsrsCharacterId))
            .Select(value => value.Key)
            .ToHashSet();
        var selectedRowsByCharacter = cache?.Compatible == true
            ? cache.Rows.Where(value => string.Equals(value.Metric, selected.Metric, StringComparison.Ordinal))
                .GroupBy(value => value.OsrsCharacterId)
                .ToDictionary(value => value.Key, value => value.OrderByDescending(row => row.FetchedAt).ThenByDescending(row => row.LastAttemptAt).First())
            : new Dictionary<Guid, EventCompetitionCharacterMetricActivity>();
        var expectedCharacterIds = assignments.Select(value => value.OsrsCharacterId).Distinct().ToArray();
        var selectedComplete = cache?.Compatible == true && state.LatestMetricsComplete == true && cache.ActivityBatchId is not null &&
                               expectedCharacterIds.All(characterId => selectedRowsByCharacter.TryGetValue(characterId, out var row) &&
                                   row.ActivityBatchId == cache.ActivityBatchId && row.LastIssue is null && row.RecordedActivity() is not null);
        var selectedStale = selectedRowsByCharacter.Values.Any(row => row.FetchedAt is { } fetchedAt && fetchedAt.Add(StaleAfter) <= time.GetUtcNow());
        var stateForMetric = MetricState(state, eventRow.State, cache, selectedComplete, selectedStale, selectedRowsByCharacter.Values.Any(row => row.RecordedActivity() is not null));
        var metricTeams = new List<EventCompetitionMetricTeamActivity>(teams.Count);
        var fetchedRows = selectedRowsByCharacter.Values.Where(row => row.FetchedAt is not null).ToList();
        foreach (var team in teams)
        {
            var teamParticipantIds = memberships.Where(value => value.TeamId == team.Id)
                .Select(value => value.EventParticipantId).Distinct().ToArray();
            var participantSnapshots = teamParticipantIds.Select(participantId =>
            {
                var expectedAssignments = assignmentsByParticipant.GetValueOrDefault(participantId) ?? [];
                var accountRows = expectedAssignments.Select(assignment =>
                {
                    var row = selectedRowsByCharacter.GetValueOrDefault(assignment.OsrsCharacterId);
                    var hasDrop = dropsByParticipantCharacter.Contains((participantId, assignment.OsrsCharacterId));
                    var recorded = row?.RecordedActivity();
                    var availability = row?.Availability(hasDrop) ?? MetricActivityAvailability.WaitingForActivityData;
                    var stale = row?.FetchedAt is { } fetchedAt && fetchedAt.Add(StaleAfter) <= time.GetUtcNow();
                    return new EventCompetitionMetricAccountActivity(
                        assignment.OsrsCharacterId, assignment.CharacterName, row?.Gained, row?.Start, row?.End,
                        recorded, row?.Coverage ?? MetricActivityCoverage.Missing, availability,
                        row?.FetchedAt, row?.UpstreamUpdatedAt, stale);
                }).ToList();
                var total = accountRows.Where(value => value.RecordedActivity is > 0).Sum(value => value.RecordedActivity!.Value);
                var incomplete = accountRows.Any(value => value.Availability is MetricActivityAvailability.WaitingForActivityData or
                    MetricActivityAvailability.WaitingForActivityUpdate or MetricActivityAvailability.RetainedWithIssue);
                var participantName = accountRows.FirstOrDefault()?.CharacterName ?? "Participant";
                return new EventCompetitionMetricParticipantActivity(
                    participantId, participantName, total, accountRows,
                    dropCountByParticipant.GetValueOrDefault(participantId), incomplete);
            }).ToList();
            var contributors = participantSnapshots
                .Where(value => value.TotalGained > 0)
                .OrderByDescending(value => value.TotalGained)
                .ThenBy(value => value.ParticipantName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.ParticipantId)
                .ToList();
            var totalGained = contributors.Sum(value => value.TotalGained);
            var maximum = contributors.Count == 0 ? 0m : contributors.Max(value => value.TotalGained);
            var mvpNames = contributors.Where(value => value.TotalGained == maximum)
                .Select(value => value.ParticipantName)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
            metricTeams.Add(new(
                team.Id, team.Name, team.Slug, totalGained,
                contributors.Count == 0 ? 0m : totalGained / contributors.Count,
                contributors, mvpNames, teamParticipantIds.Length, contributors.Count,
                participantSnapshots.Any(value => value.Incomplete),
                contributors.SelectMany(value => value.Accounts).Any(value => value.Stale),
                maximum > 0 ? maximum : null));
        }
        var orderedTeams = metricTeams
            .OrderByDescending(value => value.TotalGained)
            .ThenBy(value => value.TeamName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value.TeamId)
            .ToList();
        var rankedTeams = new List<EventCompetitionMetricTeamActivity>(orderedTeams.Count);
        for (var index = 0; index < orderedTeams.Count; index++)
        {
            var rank = index == 0 || orderedTeams[index - 1].TotalGained != orderedTeams[index].TotalGained
                ? index + 1
                : rankedTeams[index - 1].Rank;
            rankedTeams.Add(orderedTeams[index] with { Rank = rank });
        }
        return new(stateForMetric, cache?.Generation ?? state.Generation,
            fetchedRows.Count == 0 ? state.LastSuccessfulAt : fetchedRows.Min(value => value.FetchedAt),
            fetchedRows.Count == 0 || fetchedRows.Any(value => value.UpstreamUpdatedAt is null) ? state.LastUpstreamUpdatedAt : fetchedRows.Min(value => value.UpstreamUpdatedAt),
            options, selected, rankedTeams, cache?.Compatible == true, selectedComplete, selectedStale, state.LastMetricAttemptAt);
    }

    private async Task<EventCompetitionActivityProjection> BuildAsync(
        Guid eventId,
        Guid? teamId,
        CancellationToken cancellationToken)
    {
        var eventState = await db.Events.AsNoTracking()
            .Where(value => value.Id == eventId && value.HiddenAt == null)
            .Select(value => (EventState?)value.State)
            .SingleOrDefaultAsync(cancellationToken);
        if (eventState is null)
            return new(EventCompetitionActivityState.NotConfigured, 0, null, null, []);

        var state = await db.EventCompetitionSynchronizations.AsNoTracking()
            .Where(value => value.EventId == eventId)
            .FirstOrDefaultAsync(cancellationToken);
        if (state is null || state.CompetitionId is null)
            return new(EventCompetitionActivityState.NotConfigured, 0, null, null, []);

        var activityState = GetState(state, eventState == EventState.Archived);
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
            var mvps = teamParticipants.Where(value => value.TotalGainedEhb > 0 && value.TotalGainedEhb == maximum)
                .OrderBy(value => value.ParticipantName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.ParticipantId)
                .Select(value => value.ParticipantName)
                .ToList();
            activities.Add(new(team.Id, team.Name, total, average, teamParticipants, mvps,
                teamParticipantIds.Count, teamParticipants.Count, teamAssignments.Count,
                teamAssignments.Count(value => cachedByCharacter.ContainsKey(value.OsrsCharacterId)), maximum > 0 ? maximum : null));
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

    private EventCompetitionActivityState GetState(EventCompetitionSynchronization state, bool historical)
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
        return !historical && state.LastSuccessfulAt is { } fetchedAt && fetchedAt.Add(StaleAfter) <= time.GetUtcNow()
            ? EventCompetitionActivityState.Stale
            : EventCompetitionActivityState.Complete;
    }

    private static bool HasRetryablePartialCache(EventCompetitionSynchronization state) =>
        state.LastSuccessfulAt is not null && state.LastErrorKind is "RateLimited" or "Unavailable";

    private static EventCompetitionActivityState MetricState(
        EventCompetitionSynchronization? state,
        EventState eventState,
        CompetitionMetricCache? cache,
        bool complete,
        bool stale,
        bool hasRetainedActivity)
    {
        if (state is null || state.CompetitionId is null) return EventCompetitionActivityState.NotConfigured;
        if (state.LastMetricAttemptAt is null) return EventCompetitionActivityState.WaitingForFirstSync;
        if (cache?.Compatible != true) return EventCompetitionActivityState.Incomplete;
        if (complete && stale && eventState != EventState.Archived) return EventCompetitionActivityState.Stale;
        if (complete) return string.IsNullOrWhiteSpace(state.LastErrorKind) ? EventCompetitionActivityState.Complete : EventCompetitionActivityState.TemporarilyUnavailable;
        if (hasRetainedActivity && state.LastErrorKind is "RateLimited" or "Unavailable") return EventCompetitionActivityState.TemporarilyUnavailable;
        return hasRetainedActivity ? EventCompetitionActivityState.Partial : EventCompetitionActivityState.Incomplete;
    }

    private static string HumanizeMetric(string metric)
    {
        var words = metric.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..])
            .ToArray();
        return string.Join(' ', words);
    }
}
