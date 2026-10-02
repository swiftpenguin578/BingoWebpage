using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bingo.Application.Dashboard;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bingo.Infrastructure.Dashboard;

/// <summary>
/// One read-only, repeatable-read projection for the Admin Dashboard. The query
/// deliberately materializes bounded scalar rows in bulk and performs the
/// cross-event identity/interval rules in memory. It never calls a provider,
/// lifecycle service, or write-capable cache while constructing the result.
/// </summary>
public sealed class AdminDashboardService(ApplicationDbContext db, TimeProvider time) : IAdminDashboardService, ICommunityDashboardService
{
    private const string HistoricalImportAction = "historical_import.applied";
    private static readonly EventState[] DashboardStates =
        [EventState.Live, EventState.AwaitingFinalReview, EventState.Finalized, EventState.Archived];
    private static readonly EventState[] PreparationStates =
        [EventState.Draft, EventState.SignupOpen, EventState.SignupClosed];

    public async Task<AdminDashboardResult> GetAsync(Guid actorAccountId, CancellationToken cancellationToken = default)
    {
        RequireConsistentTransaction(db);
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : null;

        var requestClock = time.GetUtcNow().ToUniversalTime();
        var authorized = await db.Accounts.AsNoTracking().AnyAsync(account =>
            account.Id == actorAccountId && account.AccountType == AccountType.WebsiteAccount && account.Active &&
            account.DisabledAt == null && (account.GlobalRole == GlobalRole.Admin || account.GlobalRole == GlobalRole.SuperAdmin),
            cancellationToken);
        if (!authorized)
            throw new UnauthorizedAccessException("An active website Admin is required to read the Dashboard.");

        var allEvents = await db.Events.AsNoTracking()
            .Where(value => value.HiddenAt == null && value.State != EventState.Cancelled && value.State != EventState.Discarded)
            .ToListAsync(cancellationToken);
        var dashboardEvents = allEvents.Where(value => DashboardStates.Contains(value.State)).ToArray();
        var dashboardEventIds = dashboardEvents.Select(value => value.Id).ToArray();
        var allEventIds = allEvents.Select(value => value.Id).ToArray();

        var teams = allEventIds.Length == 0
            ? []
            : await db.Teams.AsNoTracking().Where(value => allEventIds.Contains(value.EventId)).ToListAsync(cancellationToken);
        var teamIds = teams.Select(value => value.Id).ToArray();
        var participants = allEventIds.Length == 0
            ? []
            : await db.EventParticipants.AsNoTracking().Where(value => allEventIds.Contains(value.EventId)).ToListAsync(cancellationToken);
        var participantIds = participants.Select(value => value.Id).ToArray();
        var memberships = teamIds.Length == 0 || participantIds.Length == 0
            ? []
            : await db.TeamMemberships.AsNoTracking()
                .Where(value => teamIds.Contains(value.TeamId) && participantIds.Contains(value.EventParticipantId))
                .ToListAsync(cancellationToken);

        var websiteAccounts = await db.Accounts.AsNoTracking()
            .Where(value => value.AccountType == AccountType.WebsiteAccount)
            .Select(value => new AccountSnapshot(value.Id, value.CreatedAt, value.LastLoginAt))
            .ToListAsync(cancellationToken);
        var websiteAccountIds = websiteAccounts.Select(value => value.Id).ToHashSet();

        var finalizations = dashboardEventIds.Length == 0
            ? []
            : await db.EventFinalizations.AsNoTracking().Where(value => dashboardEventIds.Contains(value.EventId)).ToListAsync(cancellationToken);
        var finalizationIds = finalizations.Select(value => value.Id).ToArray();
        var placements = finalizationIds.Length == 0
            ? []
            : await db.OfficialPlacements.AsNoTracking().Where(value => finalizationIds.Contains(value.FinalizationId)).ToListAsync(cancellationToken);

        var boards = dashboardEventIds.Length == 0
            ? []
            : await db.Boards.AsNoTracking().Where(value => dashboardEventIds.Contains(value.EventId)).ToListAsync(cancellationToken);
        var publishedBoards = boards.Where(value => value.State == BoardState.Published && value.ActiveApprovalSnapshotId is not null).ToArray();
        var approvalIds = publishedBoards.Select(value => value.ActiveApprovalSnapshotId!.Value).ToArray();
        var approvalTiles = approvalIds.Length == 0
            ? []
            : await db.BoardApprovalTileSnapshots.AsNoTracking().Where(value => approvalIds.Contains(value.ApprovalSnapshotId)).ToListAsync(cancellationToken);
        var approvalTileIds = approvalTiles.Select(value => value.Id).ToArray();
        var approvalRequirements = approvalTileIds.Length == 0
            ? []
            : await db.BoardApprovalRequirementSnapshots.AsNoTracking().Where(value => approvalTileIds.Contains(value.ApprovalTileSnapshotId)).ToListAsync(cancellationToken);
        var approvalRequirementIds = approvalRequirements.Select(value => value.Id).ToArray();
        var approvalDrops = approvalRequirementIds.Length == 0
            ? []
            : await db.BoardApprovalRequirementDropSnapshots.AsNoTracking().Where(value => approvalRequirementIds.Contains(value.ApprovalRequirementSnapshotId)).ToListAsync(cancellationToken);
        var workingRequirementIds = approvalRequirements.Select(value => value.BoardRequirementSnapshotId).Distinct().ToArray();
        var workingDrops = workingRequirementIds.Length == 0
            ? []
            : await db.BoardRequirementDropSnapshots.AsNoTracking().Where(value => workingRequirementIds.Contains(value.RequirementId)).ToListAsync(cancellationToken);

        var assignments = dashboardEventIds.Length == 0
            ? []
            : await db.EventParticipantCharacters.AsNoTracking().Where(value => dashboardEventIds.Contains(value.EventId)).ToListAsync(cancellationToken);

        var synchronizations = dashboardEventIds.Length == 0
            ? []
            : await db.EventCompetitionSynchronizations.AsNoTracking().Where(value => dashboardEventIds.Contains(value.EventId)).ToListAsync(cancellationToken);
        var activities = dashboardEventIds.Length == 0
            ? []
            : await db.EventCompetitionCharacterActivities.AsNoTracking().Where(value => dashboardEventIds.Contains(value.EventId)).ToListAsync(cancellationToken);

        var submissionRows = dashboardEventIds.Length == 0
            ? []
            : await db.Submissions.AsNoTracking()
                .Where(value => dashboardEventIds.Contains(value.EventId) && value.Status == SubmissionStatus.Approved)
                .Select(value => new SubmissionRow(value.Id, value.EventId, value.TeamId, value.RequirementId,
                    value.DropSnapshotId, value.SubmittedAt, value.CreditedParticipantId))
                .ToListAsync(cancellationToken);
        var submissionIds = submissionRows.Select(value => value.Id).ToArray();
        var contributionRows = submissionIds.Length == 0
            ? []
            : await db.SubmissionContributions.AsNoTracking()
                .Where(value => submissionIds.Contains(value.SubmissionId))
                .Select(value => new ContributionRow(value.SubmissionId, value.TeamId, value.RequirementId,
                    value.DropSnapshotId, value.CreditedParticipantId, value.ReversedAt))
                .ToListAsync(cancellationToken);

        var importAudits = dashboardEventIds.Length == 0
            ? []
            : await db.AuditEntries.AsNoTracking()
                .Where(value => value.Action == HistoricalImportAction && value.EventId != null && dashboardEventIds.Contains(value.EventId.Value))
                .Select(value => new ImportAuditRow(value.EventId!.Value, value.OccurredAt, value.Details))
                .ToListAsync(cancellationToken);

        var importInfo = BuildImportInfo(dashboardEvents, importAudits, finalizations);
        var populations = BuildPopulations(dashboardEvents, teams, participants, memberships, websiteAccountIds, importInfo, requestClock);
        var approvedCounts = BuildApprovedCounts(dashboardEvents, teams, boards, approvalTiles, approvalRequirements,
            approvalDrops, workingDrops, submissionRows, contributionRows, importInfo);
        var ehb = BuildEhb(dashboardEvents, populations, teams, assignments, synchronizations, activities, memberships, participants, requestClock);

        var points = BuildChart(populations, approvedCounts);
        var history = BuildHistory(dashboardEvents, populations, approvedCounts, ehb, finalizations, placements, publishedBoards,
            approvalTiles);
        var latestEvent = populations.Values.Where(value => value.HasUsableDates)
            .OrderByDescending(value => value.Event.ActualStartedAt)
            .ThenBy(value => value.Event.Id)
            .FirstOrDefault();
        var latestPoint = latestEvent is null ? null : points.Single(value => value.EventId == latestEvent.Event.Id);
        var measuredEvents = populations.Values.Where(value => value.HasUsableDates).ToArray();
        var hasUnusableDates = populations.Values.Any(value => !value.HasUsableDates);
        var linkedPeople = measuredEvents.SelectMany(value => value.WebsiteAccountIds).Distinct().LongCount();
        var uniqueParticipants = hasUnusableDates
            ? DashboardMetric<long>.Unknown("At least one eligible event has no usable actual lifecycle interval.")
            : measuredEvents.Length == 0
            ? DashboardMetric<long>.Measured(0)
            : linkedPeople == 0 && measuredEvents.All(value => value.IsHistoricalImport)
                ? DashboardMetric<long>.Unknown("No linked website identities are available for this population.")
                : DashboardMetric<long>.Measured(linkedPeople);
        var totalParticipations = hasUnusableDates
            ? DashboardMetric<long>.Unknown("At least one eligible event has no usable actual lifecycle interval.")
            : measuredEvents.Length == 0
            ? DashboardMetric<long>.Measured(0)
            : DashboardMetric<long>.Measured(measuredEvents.Sum(value => (long)value.People.Count));
        var approvedSubmissions = dashboardEvents.Length == 0
            ? DashboardMetric<long>.Measured(0)
            : hasUnusableDates || approvedCounts.Values.Any(value => value.Availability == DashboardValueAvailability.Unavailable)
                ? DashboardMetric<long>.Unknown("Approved submission coverage is unavailable for part of the eligible history.")
                : DashboardMetric<long>.Measured(approvedCounts.Values.Sum(value => value.Value));
        var latestNewParticipants = latestPoint is null
            ? DashboardMetric<long>.Unknown("No event has an authoritative actual start.")
            : latestPoint.NewWebsiteParticipants;
        var latestNewSubmissions = latestPoint?.ApprovedSubmissions ?? DashboardMetric<long>.Unknown("No latest event submission coverage is available.");

        var endedCandidates = dashboardEvents.Where(value => value.State is EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived).ToArray();
        var hasUnusableEndedBoundary = endedCandidates.Any(value => !populations[value.Id].HasUsableDates);
        var ended = endedCandidates.Where(value => populations[value.Id].HasUsableDates && value.ActualEndedAt is not null && value.ActualStartedAt is not null)
            .OrderByDescending(value => value.ActualEndedAt)
            .ThenByDescending(value => value.ActualStartedAt)
            .ThenBy(value => value.Id)
            .FirstOrDefault();
        var recap = ended is null ? null : BuildRecap(ended, history, approvedCounts);
        var currentEvent = BuildCurrentEvent(allEvents, participants, requestClock);
        var community = BuildCommunity(websiteAccounts, ended?.ActualEndedAt, endedCandidates.Length > 0,
            hasUnusableEndedBoundary, requestClock);
        var statistics = new DashboardStatistics(
            DashboardMetric<long>.Measured(dashboardEvents.LongLength), uniqueParticipants, totalParticipations,
            approvedSubmissions, latestNewParticipants, latestNewSubmissions);

        return new AdminDashboardResult(requestClock, statistics, points, recap, history, currentEvent, community);
    }

    public async Task<IReadOnlyDictionary<Guid, EventParticipationSummary>> GetEventParticipationAsync(
        Guid actorAccountId, IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventIds);
        RequireConsistentTransaction(db);
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            : null;
        var requestClock = time.GetUtcNow().ToUniversalTime();
        var actor = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(account =>
            account.Id == actorAccountId && account.AccountType == AccountType.WebsiteAccount && account.Active &&
            account.DisabledAt == null && (account.GlobalRole == GlobalRole.Admin || account.GlobalRole == GlobalRole.SuperAdmin), cancellationToken);
        if (actor is null) throw new UnauthorizedAccessException("An active website Admin is required to read event participation.");
        var events = await db.Events.AsNoTracking()
            .Where(value => eventIds.Contains(value.Id) && value.State != EventState.Discarded &&
                (value.HiddenAt == null || actor.GlobalRole == GlobalRole.SuperAdmin))
            .ToListAsync(cancellationToken);
        var ids = events.Select(value => value.Id).ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, EventParticipationSummary>();
        var teams = await db.Teams.AsNoTracking().Where(value => ids.Contains(value.EventId)).ToListAsync(cancellationToken);
        var participants = await db.EventParticipants.AsNoTracking().Where(value => ids.Contains(value.EventId)).ToListAsync(cancellationToken);
        var teamIds = teams.Select(value => value.Id).ToArray();
        var participantIds = participants.Select(value => value.Id).ToArray();
        var memberships = await db.TeamMemberships.AsNoTracking()
            .Where(value => teamIds.Contains(value.TeamId) && participantIds.Contains(value.EventParticipantId)).ToListAsync(cancellationToken);
        var accountIds = participants.Where(person => person.AccountId != null).Select(person => person.AccountId!.Value).Distinct().ToArray();
        var websiteIds = await db.Accounts.AsNoTracking()
            .Where(value => value.AccountType == AccountType.WebsiteAccount && accountIds.Contains(value.Id))
            .Select(value => value.Id).ToListAsync(cancellationToken);
        var finalizations = await db.EventFinalizations.AsNoTracking().Where(value => ids.Contains(value.EventId)).ToListAsync(cancellationToken);
        var audits = await db.AuditEntries.AsNoTracking()
            .Where(value => value.Action == HistoricalImportAction && value.EventId != null && ids.Contains(value.EventId.Value))
            .Select(value => new ImportAuditRow(value.EventId!.Value, value.OccurredAt, value.Details)).ToListAsync(cancellationToken);
        var imports = BuildImportInfo(events, audits, finalizations);
        var populations = BuildPopulations(events, teams, participants, memberships, websiteIds.ToHashSet(), imports, requestClock);
        return populations.ToDictionary(value => value.Key, value => new EventParticipationSummary(
            value.Value.HasUsableDates ? DashboardMetric<long>.Measured(value.Value.People.Count)
                : DashboardMetric<long>.Unknown("No usable actual lifecycle interval is retained."),
            imports.ContainsKey(value.Key)));
    }

    private static void RequireConsistentTransaction(ApplicationDbContext context)
    {
        if (context.Database.CurrentTransaction is { } current &&
            current.GetDbTransaction().IsolationLevel is not (IsolationLevel.RepeatableRead or IsolationLevel.Serializable))
            throw new InvalidOperationException("Dashboard requires a consistent database snapshot.");
    }

    private static Dictionary<Guid, ImportInfo> BuildImportInfo(
        IReadOnlyList<BingoEvent> events,
        IReadOnlyList<ImportAuditRow> audits,
        IReadOnlyList<EventFinalizationSnapshot> finalizations)
    {
        var result = new Dictionary<Guid, ImportInfo>();
        foreach (var item in events)
        {
            var audit = audits.Where(value => value.EventId == item.Id).OrderBy(value => value.OccurredAt).FirstOrDefault();
            if (audit is not null)
            {
                result[item.Id] = new(true, audit.OccurredAt);
                continue;
            }

            var finalization = finalizations.Where(value => value.EventId == item.Id)
                .Where(value => HasHistoricalImportMetadata(value.CalculationInputsJson) || HasHistoricalImportMetadata(value.CalculationResultsJson))
                .OrderBy(value => value.FinalizedAt)
                .FirstOrDefault();
            if (finalization is not null)
                result[item.Id] = new(true, finalization.FinalizedAt);
        }
        return result;
    }

    private static bool HasHistoricalImportMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("sourceEventId", out _) && root.TryGetProperty("importHash", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Dictionary<Guid, Population> BuildPopulations(
        IReadOnlyList<BingoEvent> events,
        IReadOnlyList<Team> teams,
        IReadOnlyList<EventParticipant> participants,
        IReadOnlyList<TeamMembership> memberships,
        HashSet<Guid> websiteAccountIds,
        IReadOnlyDictionary<Guid, ImportInfo> imports,
        DateTimeOffset requestClock)
    {
        var teamById = teams.ToDictionary(value => value.Id);
        var participantsById = participants.ToDictionary(value => value.Id);
        var result = new Dictionary<Guid, Population>();
        foreach (var item in events)
        {
            if (item.ActualStartedAt is not { } started)
            {
                result[item.Id] = new(item, false, false, new HashSet<PersonKey>(), new HashSet<Guid>());
                continue;
            }

            DateTimeOffset? end = item.ActualEndedAt;
            var hasUsableDates = end is null
                ? item.State == EventState.Live && started <= requestClock
                : end > started;
            if (!hasUsableDates)
            {
                result[item.Id] = new(item, false, imports.ContainsKey(item.Id), new HashSet<PersonKey>(), new HashSet<Guid>());
                continue;
            }

            var imported = imports.ContainsKey(item.Id);
            var eventTeams = teamById.Values.Where(value => value.EventId == item.Id).Select(value => value.Id).ToHashSet();
            var people = new HashSet<PersonKey>();
            var websitePeople = new HashSet<Guid>();
            foreach (var membership in memberships.Where(value => eventTeams.Contains(value.TeamId)))
            {
                if (!participantsById.TryGetValue(membership.EventParticipantId, out var participant) || participant.EventId != item.Id)
                    continue;
                var intervalStart = started;
                var intervalEnd = end ?? requestClock;
                // The explicit parentheses keep the interval rule readable and
                // make the ended [start,end) and Live inclusive-clock semantics
                // independent of C#'s &&/|| precedence.
                var eligible = IsMembershipEligible(membership, intervalStart, end, requestClock);
                if (!eligible) continue;

                var linked = false;
                Guid accountId = Guid.Empty;
                if (!imported && participant.AccountId is { } candidateAccountId && websiteAccountIds.Contains(candidateAccountId))
                {
                    linked = true;
                    accountId = candidateAccountId;
                }
                var key = linked ? new PersonKey(accountId, Guid.Empty) : new PersonKey(null, participant.Id);
                people.Add(key);
                if (linked) websitePeople.Add(accountId);
            }
            result[item.Id] = new(item, true, imported, people, websitePeople);
        }
        return result;
    }

    private static Dictionary<Guid, DashboardMetric<long>> BuildApprovedCounts(
        IReadOnlyList<BingoEvent> events,
        IReadOnlyList<Team> teams,
        IReadOnlyList<Board> boards,
        IReadOnlyList<BoardApprovalTileSnapshot> approvalTiles,
        IReadOnlyList<BoardApprovalRequirementSnapshot> approvalRequirements,
        IReadOnlyList<BoardApprovalRequirementDropSnapshot> approvalDrops,
        IReadOnlyList<BoardRequirementDropSnapshot> workingDrops,
        IReadOnlyList<SubmissionRow> submissions,
        IReadOnlyList<ContributionRow> contributions,
        IReadOnlyDictionary<Guid, ImportInfo> imports)
    {
        var result = new Dictionary<Guid, DashboardMetric<long>>();
        var teamsByEvent = teams.GroupBy(value => value.EventId).ToDictionary(value => value.Key, value => value.Select(row => row.Id).ToHashSet());
        var boardByEvent = boards.Where(value => value.State == BoardState.Published && value.ActiveApprovalSnapshotId is not null)
            .GroupBy(value => value.EventId).ToDictionary(value => value.Key, value => value.First());
        var tileByApproval = approvalTiles.GroupBy(value => value.ApprovalSnapshotId)
            .ToDictionary(value => value.Key, value => value.Select(row => row.Id).ToHashSet());
        var requirementByApprovalId = approvalRequirements.ToDictionary(value => value.Id);
        var workingById = workingDrops.ToDictionary(value => value.Id);
        var approvedDropIdsByRequirement = new HashSet<Guid>();
        foreach (var drop in approvalDrops)
        {
            if (!requirementByApprovalId.TryGetValue(drop.ApprovalRequirementSnapshotId, out var requirement)) continue;
            var match = workingDrops.FirstOrDefault(value => value.RequirementId == requirement.BoardRequirementSnapshotId &&
                value.SourceDropId == drop.SourceDropId && value.ItemIdSnapshot == drop.ItemIdSnapshot);
            if (match is not null) approvedDropIdsByRequirement.Add(match.Id);
        }

        var activeContributions = contributions.Where(value => value.ReversedAt is null)
            .GroupBy(value => value.SubmissionId)
            .ToDictionary(value => value.Key, value => value.First());
        foreach (var item in events)
        {
            if (!boardByEvent.TryGetValue(item.Id, out var board) || board.ActiveApprovalSnapshotId is not { } approvalId ||
                !tileByApproval.TryGetValue(approvalId, out var tileIds))
            {
                result[item.Id] = DashboardMetric<long>.Unknown("Published board approval is unavailable.");
                continue;
            }
            var publishedRequirements = approvalRequirements.Where(value => tileIds.Contains(value.ApprovalTileSnapshotId)).ToDictionary(value => value.BoardRequirementSnapshotId, value => value);
            var eventTeams = teamsByEvent.GetValueOrDefault(item.Id) ?? [];
            var imported = imports.GetValueOrDefault(item.Id);
            var count = 0L;
            foreach (var submission in submissions.Where(value => value.EventId == item.Id))
            {
                if (!activeContributions.TryGetValue(submission.Id, out var contribution) || !eventTeams.Contains(submission.TeamId) ||
                    contribution.TeamId != submission.TeamId || contribution.RequirementId != submission.RequirementId ||
                    !publishedRequirements.TryGetValue(submission.RequirementId, out var requirement))
                    continue;
                if (imported is not null && submission.SubmittedAt <= imported.AppliedAt)
                    continue;
                if (submission.DropSnapshotId is { } dropId)
                {
                    if (!approvedDropIdsByRequirement.Contains(dropId) || !workingById.TryGetValue(dropId, out var drop) || drop.RequirementId != requirement.BoardRequirementSnapshotId)
                        continue;
                }
                else if (!requirement.ManualObjective)
                    continue;
                count++;
            }

            var hasMeasuredSubmission = imported is { } historical && submissions.Any(value => value.EventId == item.Id && value.SubmittedAt > historical.AppliedAt);
            if (imported is not null && count == 0 && !hasMeasuredSubmission)
                result[item.Id] = DashboardMetric<long>.Unknown("Historical reconstructed submissions have no measured coverage.");
            else
                result[item.Id] = DashboardMetric<long>.Measured(count);
        }
        return result;
    }

    private static Dictionary<Guid, DashboardEhbSummary> BuildEhb(
        IReadOnlyList<BingoEvent> events,
        IReadOnlyDictionary<Guid, Population> populations,
        IReadOnlyList<Team> teams,
        IReadOnlyList<EventParticipantCharacter> assignments,
        IReadOnlyList<EventCompetitionSynchronization> synchronizations,
        IReadOnlyList<EventCompetitionCharacterActivity> activities,
        IReadOnlyList<TeamMembership> memberships,
        IReadOnlyList<EventParticipant> participants,
        DateTimeOffset requestClock)
    {
        var participantById = participants.ToDictionary(value => value.Id);
        var teamById = teams.ToDictionary(value => value.Id);
        var result = new Dictionary<Guid, DashboardEhbSummary>();
        foreach (var item in events)
        {
            if (!populations.TryGetValue(item.Id, out var population) || !population.HasUsableDates)
            {
                result[item.Id] = new(null, DashboardEhbCoverage.Unavailable, 0, 0);
                continue;
            }

            var start = item.ActualStartedAt!.Value;
            var end = item.ActualEndedAt ?? requestClock;
            var qualifyingParticipantIds = memberships.Where(value => teamById.TryGetValue(value.TeamId, out var team) && team.EventId == item.Id)
                .Where(value => participantById.TryGetValue(value.EventParticipantId, out var participant) && participant.EventId == item.Id)
                .Where(value => IsMembershipEligible(value, start, item.ActualEndedAt, requestClock))
                .Select(value => value.EventParticipantId)
                .ToHashSet();
            var expectedAssignments = assignments.Where(value => value.EventId == item.Id && value.EventRole == EventCharacterRole.Playing &&
                    qualifyingParticipantIds.Contains(value.EventParticipantId))
                .Where(value => value.RegisteredAt < end && (value.ReleasedAt is null || value.ReleasedAt > start))
                .GroupBy(value => value.OsrsCharacterId)
                .Select(value => value.OrderBy(row => row.Id).First())
                .ToArray();
            var expected = expectedAssignments.Select(value => value.OsrsCharacterId).ToHashSet();
            var sync = synchronizations.Where(value => value.EventId == item.Id && value.CompetitionId is not null)
                .OrderByDescending(value => value.Generation).ThenByDescending(value => value.LastSuccessfulAt)
                .FirstOrDefault();
            if (sync is null || expected.Count == 0)
            {
                result[item.Id] = new(null, DashboardEhbCoverage.Unavailable, expected.Count, 0);
                continue;
            }
            var rows = activities.Where(value => value.EventId == item.Id && value.Generation == sync.Generation &&
                    value.CompetitionId == sync.CompetitionId && value.AssignmentFingerprint == sync.AssignmentFingerprint)
                .GroupBy(value => value.OsrsCharacterId)
                .Select(value => value.OrderByDescending(row => row.FetchedAt).ThenByDescending(row => row.Id).First())
                .ToDictionary(value => value.OsrsCharacterId);
            var matched = expected.Where(rows.ContainsKey).ToArray();
            var expectedFingerprint = Fingerprint(expectedAssignments);
            if (!string.Equals(expectedFingerprint, sync.AssignmentFingerprint, StringComparison.Ordinal))
            {
                result[item.Id] = new(null, DashboardEhbCoverage.Unavailable, expected.Count, 0);
                continue;
            }
            if (matched.Length == 0)
            {
                result[item.Id] = new(null, DashboardEhbCoverage.Unavailable, expected.Count, 0);
                continue;
            }
            var gain = matched.Sum(character => rows[character].GainedEhb);
            var coverage = matched.Length == expected.Count
                ? gain == 0 ? DashboardEhbCoverage.MeasuredZero : DashboardEhbCoverage.Complete
                : DashboardEhbCoverage.Partial;
            result[item.Id] = new(gain, coverage, expected.Count, matched.Length);
        }
        return result;
    }

    private static string Fingerprint(IEnumerable<EventParticipantCharacter> assignments)
    {
        var value = string.Join('|', assignments.OrderBy(row => row.Id)
            .Select(row => $"{row.Id:N}:{row.EventParticipantId:N}:{row.OsrsCharacterId:N}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static List<DashboardParticipationPoint> BuildChart(
        IReadOnlyDictionary<Guid, Population> populations,
        IReadOnlyDictionary<Guid, DashboardMetric<long>> approvedCounts)
    {
        var ordered = populations.Values.Where(value => value.HasUsableDates)
            .OrderBy(value => value.Event.ActualStartedAt).ThenBy(value => value.Event.Id).ToArray();
        var seen = new HashSet<Guid>();
        var result = new List<DashboardParticipationPoint>(ordered.Length);
        foreach (var cohort in ordered.GroupBy(value => value.Event.ActualStartedAt!.Value))
        {
            foreach (var value in cohort.OrderBy(row => row.Event.Id))
            {
                var returning = value.WebsiteAccountIds.Where(seen.Contains).LongCount();
                var fresh = value.WebsiteAccountIds.Count - returning;
                var unique = value.WebsiteAccountIds.Count == 0 && value.IsHistoricalImport
                    ? DashboardMetric<long>.Unknown("This event has no linked website identities.")
                    : DashboardMetric<long>.Measured(value.WebsiteAccountIds.Count);
                var freshMetric = value.WebsiteAccountIds.Count == 0 && value.IsHistoricalImport
                    ? DashboardMetric<long>.Unknown("Returning classification has no linked website identities.")
                    : DashboardMetric<long>.Measured(fresh);
                var returningMetric = value.WebsiteAccountIds.Count == 0 && value.IsHistoricalImport
                    ? DashboardMetric<long>.Unknown("Returning classification has no linked website identities.")
                    : DashboardMetric<long>.Measured(returning);
                result.Add(new(value.Event.Id, value.Event.Name, value.Event.Slug, value.Event.State,
                    value.Event.State is EventState.Live or EventState.AwaitingFinalReview,
                    value.Event.ActualStartedAt, value.Event.ActualEndedAt,
                    DashboardMetric<long>.Measured(value.People.Count), unique, freshMetric, returningMetric,
                    approvedCounts.GetValueOrDefault(value.Event.Id) ?? DashboardMetric<long>.Unknown()));
            }
            foreach (var value in cohort) seen.UnionWith(value.WebsiteAccountIds);
        }
        return result;
    }

    private static DashboardHistoryRow[] BuildHistory(
        IReadOnlyList<BingoEvent> events,
        IReadOnlyDictionary<Guid, Population> populations,
        IReadOnlyDictionary<Guid, DashboardMetric<long>> approvedCounts,
        IReadOnlyDictionary<Guid, DashboardEhbSummary> ehb,
        IReadOnlyList<EventFinalizationSnapshot> finalizations,
        IReadOnlyList<OfficialPlacementSnapshot> placements,
        IReadOnlyList<Board> publishedBoards,
        IReadOnlyList<BoardApprovalTileSnapshot> approvalTiles)
    {
        var rows = events.Select(item =>
        {
            var population = populations[item.Id];
            var people = population.HasUsableDates
                ? DashboardMetric<long>.Measured(population.People.Count)
                : DashboardMetric<long>.Unknown("The event has no valid actual lifecycle interval.");
            var unique = population.HasUsableDates && (population.WebsiteAccountIds.Count > 0 || !population.IsHistoricalImport)
                ? DashboardMetric<long>.Measured(population.WebsiteAccountIds.Count)
                : population.HasUsableDates
                    ? DashboardMetric<long>.Unknown("The event has no linked website identities.")
                    : DashboardMetric<long>.Unknown("The event has no valid actual lifecycle interval.");
            var official = item.State is EventState.Finalized or EventState.Archived
                ? finalizations.Where(value => value.EventId == item.Id && value.UnfinalizedAt is null)
                    .OrderByDescending(value => value.Version).ThenByDescending(value => value.FinalizedAt).FirstOrDefault()
                : null;
            var winners = official is null
                ? []
                : placements.Where(value => value.FinalizationId == official.Id && value.Placement == 1)
                    .OrderBy(value => value.TeamId).Select(value => new DashboardWinner(value.TeamId, value.TeamName, value.Placement)).ToArray();
            DashboardWinnerBoard? winnerBoard = null;
            if (official is not null && winners.Length > 0)
            {
                var board = publishedBoards.FirstOrDefault(value => value.EventId == item.Id);
                var totalTiles = board?.ActiveApprovalSnapshotId is { } approvalId
                    ? approvalTiles.Count(value => value.ApprovalSnapshotId == approvalId)
                    : 0;
                var completedTiles = placements.Where(value => value.FinalizationId == official.Id && value.Placement == 1)
                    .Select(value => value.CompletedTiles).DefaultIfEmpty().Max();
                winnerBoard = totalTiles <= 0
                    ? null
                    : new DashboardWinnerBoard(completedTiles, totalTiles, (decimal)completedTiles / totalTiles, winners);
            }
            return new DashboardHistoryRow(item.Id, item.Name, item.Slug, item.State,
                item.State is EventState.Live or EventState.AwaitingFinalReview,
                item.ActualStartedAt, item.ActualEndedAt, OverviewPath(item.Id), people, unique,
                approvedCounts.GetValueOrDefault(item.Id) ?? DashboardMetric<long>.Unknown(), winnerBoard,
                ehb.GetValueOrDefault(item.Id) ?? new DashboardEhbSummary(null, DashboardEhbCoverage.Unavailable, 0, 0), winners);
        }).ToList();

        // Default history keeps ongoing events together, then ended events by
        // actual end/start. UI sorting can re-order each nullable column while
        // retaining this stable fallback order.
        return rows.OrderByDescending(value => value.ActualEndedAt is null)
            .ThenByDescending(value => value.ActualEndedAt is null ? value.ActualStartedAt : value.ActualEndedAt)
            .ThenByDescending(value => value.ActualStartedAt)
            .ThenBy(value => value.EventId)
            .ToArray();
    }

    private static DashboardRecap BuildRecap(
        BingoEvent ended,
        IReadOnlyList<DashboardHistoryRow> history,
        IReadOnlyDictionary<Guid, DashboardMetric<long>> approvedCounts)
    {
        var row = history.Single(value => value.EventId == ended.Id);
        return new(ended.Id, ended.Name, ended.Slug, ended.ActualStartedAt!.Value, ended.ActualEndedAt!.Value,
            row.Participants, approvedCounts.GetValueOrDefault(ended.Id) ?? DashboardMetric<long>.Unknown(),
            row.WinnerBoard, row.Winners, row.OverviewPath);
    }

    private static DashboardEventCard? BuildCurrentEvent(
        IReadOnlyList<BingoEvent> events,
        IReadOnlyList<EventParticipant> participants,
        DateTimeOffset requestClock)
    {
        var live = events.Where(value => value.State == EventState.Live)
            .OrderByDescending(value => value.ActualStartedAt).ThenBy(value => value.Id).FirstOrDefault();
        var selected = live;
        if (selected is null)
        {
            var preparation = events.Where(value => PreparationStates.Contains(value.State)).ToArray();
            selected = preparation.Where(value => value.EventStartsAt is not null)
                .OrderBy(value => value.EventStartsAt)
                .ThenBy(value => value.Id)
                .FirstOrDefault()
                ?? preparation.Where(value => value.EventStartsAt is null)
                    .OrderBy(value => value.Id).FirstOrDefault();
        }
        if (selected is null) return null;
        var eventParticipants = participants.Where(value => value.EventId == selected.Id).ToArray();
        return new(selected.Id, selected.Name, selected.Slug, selected.State, selected.EventStartsAt, selected.ActualStartedAt,
            selected.EventStartsAt is { } scheduled && scheduled < requestClock && selected.State != EventState.Live,
            eventParticipants.LongCount(value => value.SignupStatus == SignupStatus.Confirmed),
            eventParticipants.LongCount(value => value.SignupStatus == SignupStatus.WaitingList), selected.ParticipantCap,
            OverviewPath(selected.Id));
    }

    private static DashboardCommunitySnapshot BuildCommunity(
        IReadOnlyList<AccountSnapshot> accounts,
        DateTimeOffset? latestEndedAt,
        bool hasEndedEvent,
        bool hasUnusableEndedBoundary,
        DateTimeOffset requestClock)
    {
        if (hasEndedEvent && (hasUnusableEndedBoundary || latestEndedAt is null))
        {
            var unavailableWebsite = accounts.ToArray();
            var unavailableLogins = unavailableWebsite.LongCount(value => value.LastLoginAt is { } login &&
                login > requestClock.AddDays(-30) && login <= requestClock);
            return new(DashboardMetric<long>.Measured(unavailableWebsite.LongLength),
                DashboardMetric<long>.Unknown("No usable actual ended boundary is available for new-account classification."),
                DashboardMetric<long>.Measured(unavailableLogins), null, false);
        }

        var since = latestEndedAt ?? requestClock.AddDays(-30);
        var loginSince = requestClock.AddDays(-30);
        var website = accounts.ToArray();
        var created = website.LongCount(value => value.CreatedAt > since && value.CreatedAt <= requestClock);
        var logins = website.LongCount(value => value.LastLoginAt is { } login && login > loginSince && login <= requestClock);
        return new(DashboardMetric<long>.Measured(website.LongLength), DashboardMetric<long>.Measured(created),
            DashboardMetric<long>.Measured(logins), since, latestEndedAt is null);
    }

    private static bool IsMembershipEligible(TeamMembership membership, DateTimeOffset start, DateTimeOffset? end, DateTimeOffset clock)
    {
        if (membership.LeftAt is { } left && left <= membership.JoinedAt) return false;
        return end is { } ended
            ? membership.JoinedAt < ended && (membership.LeftAt is null || membership.LeftAt > start)
            : membership.JoinedAt <= clock && (membership.LeftAt is null || membership.LeftAt > start);
    }

    private static string OverviewPath(Guid eventId) => $"/Admin/Events/Manage/{eventId:D}";

    private sealed record ImportAuditRow(Guid EventId, DateTimeOffset OccurredAt, string? Details);
    private sealed record AccountSnapshot(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt);
    private sealed record ImportInfo(bool IsHistorical, DateTimeOffset AppliedAt);
    private sealed record SubmissionRow(Guid Id, Guid EventId, Guid TeamId, Guid RequirementId, Guid? DropSnapshotId,
        DateTimeOffset SubmittedAt, Guid CreditedParticipantId);
    private sealed record ContributionRow(Guid SubmissionId, Guid TeamId, Guid RequirementId, Guid? DropSnapshotId,
        Guid CreditedParticipantId, DateTimeOffset? ReversedAt);
    private sealed record PersonKey(Guid? WebsiteAccountId, Guid ParticipantId);
    private sealed record Population(BingoEvent Event, bool HasUsableDates, bool IsHistoricalImport, IReadOnlySet<PersonKey> People,
        IReadOnlySet<Guid> WebsiteAccountIds);
}
