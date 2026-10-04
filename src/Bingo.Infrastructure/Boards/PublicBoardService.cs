using Bingo.Application.Boards;
using Bingo.Application.Catalogue;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Bingo.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Boards;

public sealed class PublicBoardService(ApplicationDbContext db, TimeProvider time) : IPublicBoardService
{
    public async Task<PublicEventBoard?> GetEventBoardAsync(string eventSlug, CancellationToken cancellationToken = default)
        => await GetEventBoardAsync(eventSlug, 24, cancellationToken);

    public Task<PublicEventBoard?> GetEventBoardAsync(string eventSlug, int recentDropCount, CancellationToken cancellationToken = default)
        => GetEventBoardAsync(eventSlug, recentDropCount, null, null, cancellationToken);

    public async Task<PublicEventBoard?> GetEventBoardAsync(string eventSlug, int recentDropCount, string? dropSearch, string? dropTeam, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is not null)
            return await ReadEventBoardAsync(eventSlug, recentDropCount, dropSearch, dropTeam, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        var board = await ReadEventBoardAsync(eventSlug, recentDropCount, dropSearch, dropTeam, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return board;
    }

    private async Task<PublicEventBoard?> ReadEventBoardAsync(string eventSlug, int recentDropCount, string? dropSearch, string? dropTeam, CancellationToken cancellationToken)
    {
        var bingoEvent = await db.Events.AsNoTracking().SingleOrDefaultAsync(value => value.Slug == eventSlug && value.HiddenAt == null, cancellationToken);
        if (bingoEvent is null) return null;
        if (bingoEvent.State == EventState.Discarded) return null;
        if (bingoEvent.State == EventState.Cancelled)
            return bingoEvent.FirstPublicAt is null ? null : new PublicEventBoard(
                bingoEvent.Id, bingoEvent.Name, bingoEvent.Slug, EventState.Cancelled, 0, 0, 0, [], [], []);
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(value => value.EventId == bingoEvent.Id && value.State == BoardState.Published, cancellationToken);
        if (board is null) return null;
        var wiseOldManCompetitionId = await db.EventCompetitionSynchronizations.AsNoTracking()
            .Where(value => value.EventId == bingoEvent.Id && value.CompetitionId != null)
            .OrderByDescending(value => value.Generation)
            .Select(value => value.CompetitionId)
            .FirstOrDefaultAsync(cancellationToken);
        if (board.ActiveApprovalSnapshotId is not { } approvalId) return null;
        var approval = await db.BoardApprovalSnapshots.AsNoTracking().SingleOrDefaultAsync(value => value.Id == approvalId && value.BoardId == board.Id, cancellationToken);
        if (approval is null) return null;
        var publication = await db.ApprovalObjectivesAsync(board.Id, approvalId, cancellationToken);
        if (publication is null) return null;
        var publishedDrops = publication.Drops.ToDictionary(x => x.Id);
        var rosterPublication = await db.ActiveRosterPublicationAsync(bingoEvent.Id, cancellationToken);
        if (rosterPublication is null) return null;
        var rosterEntries = await db.DraftPublicationRosters.AsNoTracking()
            .Where(value => value.DraftPublicationCycleId == rosterPublication.Id)
            .ToListAsync(cancellationToken);
        var rosterTeamIds = rosterEntries.Select(value => value.TeamId).Distinct().ToList();
        var teams = await db.Teams.AsNoTracking().Where(value => value.EventId == bingoEvent.Id && value.Active && rosterTeamIds.Contains(value.Id))
            .OrderBy(value => value.DraftPosition).ThenBy(value => value.Name).ToListAsync(cancellationToken);
        if (teams.Count == 0) return null;

        var frozenTileRows = await db.BoardApprovalTileSnapshots.AsNoTracking()
            .Where(value => value.ApprovalSnapshotId == approvalId)
            .OrderBy(value => value.RowIndex).ThenBy(value => value.ColumnIndex)
            .ToListAsync(cancellationToken);
        if (frozenTileRows.Count != approval.Rows * approval.Columns ||
            frozenTileRows.Select(value => (value.RowIndex, value.ColumnIndex)).Distinct().Count() != frozenTileRows.Count) return null;
        var frozenTiles = frozenTileRows.ToDictionary(value => value.BoardTileId);
        var frozenTilesByApprovalId = frozenTileRows.ToDictionary(value => value.Id);
        var frozenArtworkTileIds = frozenTiles.Values.Where(value => !string.IsNullOrWhiteSpace(value.ArtworkReference)).Select(value => value.BoardTileId).ToList();
        var frozenArtworkTiles = frozenArtworkTileIds.Count == 0
            ? new HashSet<Guid>()
            : await (from tile in db.BoardApprovalTileSnapshots.AsNoTracking()
                     join image in db.BoardTileImageAssets.AsNoTracking() on tile.BoardTileId equals image.BoardTileId
                     where tile.ApprovalSnapshotId == approvalId && tile.ArtworkReference != null &&
                           image.EventId == bingoEvent.Id && image.StorageKey == tile.ArtworkReference
                     select tile.BoardTileId).ToHashSetAsync(cancellationToken);
        var frozenRequirements = await db.BoardApprovalRequirementSnapshots.AsNoTracking().Where(value => value.ApprovalTileSnapshotId != Guid.Empty && value.BoardRequirementSnapshotId != Guid.Empty)
            .Join(db.BoardApprovalTileSnapshots.AsNoTracking().Where(value => value.ApprovalSnapshotId == approvalId), requirement => requirement.ApprovalTileSnapshotId, tile => tile.Id, (requirement, _) => requirement)
            .ToDictionaryAsync(value => value.BoardRequirementSnapshotId, cancellationToken);
        if (frozenRequirements.Count == 0 || frozenRequirements.Values.Any(value => !frozenTilesByApprovalId.ContainsKey(value.ApprovalTileSnapshotId))) return null;
        var requirementIds = frozenRequirements.Keys.ToList();
        var bossArtwork = await (from snapshot in db.BoardApprovalRequirementBossSnapshots.AsNoTracking()
                                 join requirement in db.BoardApprovalRequirementSnapshots.AsNoTracking() on snapshot.ApprovalRequirementSnapshotId equals requirement.Id
                                 join boss in db.BossActivities.AsNoTracking() on snapshot.BossActivityId equals boss.Id
                                 where frozenRequirements.Values.Select(x => x.Id).ToList().Contains(requirement.Id) && boss.ImageUrl != null
                                 select new { RequirementId = requirement.BoardRequirementSnapshotId, BossName = boss.Name, boss.ImageUrl }).ToListAsync(cancellationToken);
        var artworkByRequirement = bossArtwork
            .Select(value => new { value.RequirementId, value.BossName, ImageUrl = OsrsWikiImageUrl.Normalize(value.ImageUrl) })
            .Where(value => !string.IsNullOrWhiteSpace(value.ImageUrl))
            .GroupBy(value => value.RequirementId)
            .ToDictionary(group => group.Key, group => group
                .GroupBy(value => BossArtworkFamily.Key(value.BossName), StringComparer.OrdinalIgnoreCase)
                .Select(family => family.OrderBy(value => BossArtworkFamily.Priority(value.BossName)).ThenBy(value => value.BossName).First().ImageUrl!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());
        var tileByRequirement = frozenRequirements.ToDictionary(
            value => value.Key,
            value => frozenTilesByApprovalId[value.Value.ApprovalTileSnapshotId].BoardTileId);
        var artworkByTile = frozenRequirements
            .GroupBy(value => frozenTilesByApprovalId[value.Value.ApprovalTileSnapshotId].BoardTileId)
            .ToDictionary(group => group.Key, group => group
                .SelectMany(value => artworkByRequirement.GetValueOrDefault(value.Key) ?? [])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList());
        // Public board wording/rates must be read from the immutable approval tree.
        var teamIds = teams.Select(value => value.Id).ToList();
        var completionFactsByTeam = (await db.TileCompletionFacts.AsNoTracking()
                .Where(value => value.EventId == bingoEvent.Id && value.ApprovalSnapshotId == approvalId && teamIds.Contains(value.TeamId))
                .ToListAsync(cancellationToken))
            .GroupBy(value => value.TeamId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var rosterRows = rosterEntries
            .Where(value => teamIds.Contains(value.TeamId))
            .Select(value => new { value.TeamId, PlayerId = value.EventParticipantId, PlayerName = value.PublicCharacterName })
            .ToList();
        var rosterParticipantIds = rosterRows.Select(value => value.PlayerId).Distinct().ToList();
        var playingAccountRows = await (from entry in db.DraftPublicationRosters.AsNoTracking()
                                        join assignment in db.EventParticipantCharacters.AsNoTracking() on entry.EventParticipantId equals assignment.EventParticipantId
                                        join character in db.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                                        where entry.DraftPublicationCycleId == rosterPublication.Id && teamIds.Contains(entry.TeamId) && rosterParticipantIds.Contains(entry.EventParticipantId) &&
                                              assignment.EventId == bingoEvent.Id && assignment.ReleasedAt == null &&
                                              assignment.EventRole == EventCharacterRole.Playing &&
                                              db.EventParticipants.Any(participant => participant.Id == assignment.EventParticipantId && participant.SignupStatus == SignupStatus.Confirmed)
                                        orderby entry.TeamId, assignment.EventParticipantId, assignment.RegistrationOrder, assignment.Id
                                        select new { entry.TeamId, assignment.EventParticipantId, character.DisplayName })
            .ToListAsync(cancellationToken);
        var playingAccountNamesByParticipant = playingAccountRows
            .GroupBy(value => value.EventParticipantId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.Select(value => value.DisplayName).ToList());
        var contributionRows = (await (from contribution in db.SubmissionContributions.AsNoTracking()
                                       join submission in db.Submissions.AsNoTracking() on contribution.SubmissionId equals submission.Id
                                       where teamIds.Contains(contribution.TeamId) && requirementIds.Contains(contribution.RequirementId) &&
                                             contribution.ReversedAt == null && submission.Status == SubmissionStatus.Approved
                                       select new { contribution, submission }).ToListAsync(cancellationToken))
            .Select(row =>
            {
                var drop = row.contribution.DropSnapshotId is { } dropId ? publishedDrops.GetValueOrDefault(dropId) : null;
                return new
                {
                    row.contribution,
                    row.submission,
                    ItemIdSnapshot = drop is null ? (Guid?)null : drop.ItemIdSnapshot,
                    SourceDropId = drop is null ? (Guid?)null : drop.SourceDropId,
                    drop?.MaximumContribution
                };
            }).ToList();
        var requirementTargetByTile = frozenRequirements.Values
            .GroupBy(value => frozenTilesByApprovalId[value.ApprovalTileSnapshotId].BoardTileId)
            .ToDictionary(group => group.Key, group => Math.Max(1, group.Sum(value => value.TargetContribution)));
        var definitions = frozenTileRows.Select(tile => new ProgressTileDefinition(
            tile.BoardTileId, tile.RowIndex, tile.ColumnIndex, tile.EstimatedEhb,
            frozenRequirements.Values.Where(requirement => requirement.ApprovalTileSnapshotId == tile.Id)
                .Select(requirement => new ProgressRequirementDefinition(requirement.BoardRequirementSnapshotId, requirement.Position, requirement.TargetContribution, requirement.DuplicatesAllowed)).ToList())).ToList();
        var effectiveAmountByContributionId = contributionRows
            .GroupBy(value => value.contribution.TeamId)
            .SelectMany(group => PublicProgressCalculator.Allocate(
                definitions,
                group.Select(row => new ProgressContribution(
                    row.contribution.Id, row.contribution.RequirementId, row.contribution.CreditedParticipantId, row.submission.CreditedCharacterName,
                    row.contribution.Amount, row.submission.SubmittedAt, 0,
                    row.ItemIdSnapshot, row.SourceDropId, row.MaximumContribution, row.submission.Id))))
            .ToDictionary(value => value.Id, value => value.Amount);
        var historicalProgressBySubmission = new Dictionary<Guid, (int ProgressAfter, int Target)>();
        var creditedByTeamTile = new Dictionary<(Guid TeamId, Guid TileId), int>();
        foreach (var row in contributionRows
                     .OrderBy(value => value.submission.ReviewedAt ?? value.submission.SubmittedAt)
                     .ThenBy(value => value.submission.Id))
        {
            var tileId = tileByRequirement[row.contribution.RequirementId];
            var target = requirementTargetByTile[tileId];
            var key = (row.contribution.TeamId, tileId);
            var progressAfter = Math.Min(target, creditedByTeamTile.GetValueOrDefault(key) + effectiveAmountByContributionId.GetValueOrDefault(row.contribution.Id));
            creditedByTeamTile[key] = progressAfter;
            historicalProgressBySubmission[row.submission.Id] = (progressAfter, target);
        }

        var unranked = teams.Select(team =>
        {
            var creditedByTile = new Dictionary<Guid, int>();
            var contributions = contributionRows
                .Where(row => row.contribution.TeamId == team.Id)
                .OrderBy(row => row.submission.SubmittedAt)
                .ThenBy(row => row.contribution.Id)
                .Select(row =>
            {
                var tileId = tileByRequirement[row.contribution.RequirementId];
                var tileTarget = requirementTargetByTile[tileId];
                var creditedBefore = creditedByTile.GetValueOrDefault(tileId);
                var effectiveAmount = effectiveAmountByContributionId.GetValueOrDefault(row.contribution.Id);
                var creditedAfter = Math.Min(tileTarget, creditedBefore + effectiveAmount);
                var frozenTile = frozenTiles[tileId];
                var estimatedBefore = frozenTile.EstimatedEhb * creditedBefore / tileTarget;
                var estimatedAfter = creditedAfter == tileTarget
                    ? frozenTile.EstimatedEhb
                    : frozenTile.EstimatedEhb * creditedAfter / tileTarget;
                creditedByTile[tileId] = creditedAfter;
                return new ProgressContribution(
                    row.contribution.Id, row.contribution.RequirementId, row.contribution.CreditedParticipantId, row.submission.CreditedCharacterName,
                    effectiveAmount, row.submission.SubmittedAt,
                    estimatedAfter - estimatedBefore, row.ItemIdSnapshot, row.SourceDropId, row.MaximumContribution);
            }).ToList();
            var progress = PublicProgressCalculator.Calculate(approval.Rows, approval.Columns, definitions, contributions);
            if (completionFactsByTeam.TryGetValue(team.Id, out var persistedFacts) && persistedFacts.Count > 0)
                progress = PublicProgressCalculator.ApplyTileCompletionFacts(progress,
                    persistedFacts.ToDictionary(value => value.BoardTileId, value => new PersistedTileCompletionTime(value.IsComplete, value.CompletedAt)));
            var contributionByPlayer = progress.Players.ToDictionary(value => value.PlayerId);
            var players = rosterRows
                .Where(value => value.TeamId == team.Id)
                .Select(value => contributionByPlayer.GetValueOrDefault(value.PlayerId) is { } contribution
                    ? contribution with { PlayerName = value.PlayerName }
                    :
                    new CalculatedPlayerContribution(value.PlayerId, value.PlayerName, 0, 0, 0))
                // Retained approved credit belongs to its original participant even
                // when that participant is absent from the current published roster.
                .Concat(progress.Players.Where(value => !rosterRows.Any(row => row.TeamId == team.Id && row.PlayerId == value.PlayerId)))
                .OrderByDescending(value => value.EstimatedEhb)
                .ThenByDescending(value => value.ApprovedContribution)
                .ThenBy(value => value.PlayerName)
                .ToList();
            return new UnrankedTeamProgress(team.Id, team.Name, progress with { Players = players });
        }).ToList();
        var ranked = PublicProgressCalculator.Rank(unranked, bingoEvent.PlacementRule);
        var now = time.GetUtcNow();
        var activeFinalization = bingoEvent.ResultsPublished
            ? await db.EventFinalizations.AsNoTracking().Where(value => value.EventId == bingoEvent.Id && value.UnfinalizedAt == null).OrderByDescending(value => value.Version).FirstOrDefaultAsync(cancellationToken)
            : null;
        var officialPlacementRows = activeFinalization is null
            ? []
            : await db.OfficialPlacements.AsNoTracking().Where(value => value.FinalizationId == activeFinalization.Id).ToListAsync(cancellationToken);
        var officialPlacements = officialPlacementRows.ToDictionary(value => value.TeamId, value => value.Placement);
        var teamMap = teams.ToDictionary(value => value.Id);
        var rosterPlayers = rosterRows
            .Select(value => new PublicRosterPlayer(
                value.TeamId, teamMap[value.TeamId].Name, value.PlayerId, value.PlayerName,
                playingAccountNamesByParticipant.GetValueOrDefault(value.PlayerId) ?? [value.PlayerName]))
            .ToList();
        var tileMap = frozenTiles;
        IEnumerable<RankedTeamProgress> displayedTeams = officialPlacements.Count == teams.Count
            ? ranked.OrderBy(value => officialPlacements[value.TeamId]).ThenBy(value => value.TeamName, StringComparer.OrdinalIgnoreCase)
            : ranked;
        var publicTeams = displayedTeams.Select(value =>
        {
            var team = teamMap[value.TeamId];
            var displayedRank = officialPlacements.GetValueOrDefault(value.TeamId, value.Rank);
            return new PublicTeamBoard(
                team.Id, team.Name, team.Slug, team.AffiliationName, team.ImageUrl,
                displayedRank, displayedRank == 1 && value.Progress.BoardComplete, value.Progress,
                value.Progress.Tiles.Select(progress =>
                {
                    var frozen = tileMap[progress.Id];
                    return new PublicTileProgress(frozen.BoardTileId, frozen.RowIndex, frozen.ColumnIndex, frozen.Name,
                        frozen.Description, frozenArtworkTiles.Contains(frozen.BoardTileId) ? $"/Events/{Uri.EscapeDataString(bingoEvent.Slug)}/Board/Tiles/{frozen.BoardTileId}/Image" : null,
                        artworkByTile.GetValueOrDefault(frozen.BoardTileId) ?? [],
                        frozen.EstimatedEhb, progress.Approved, progress.Target,
                        progress.Complete, progress.CompletedAt);
                }).ToList());
        }).ToList();
        var submissionsOpen = bingoEvent.AcceptsNewSubmissions(now);
        PublicEventResult? eventResult = null;
        var resultLifecycle = bingoEvent.State is EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived;
        if (!submissionsOpen && resultLifecycle)
        {
            if (bingoEvent.ResultsPublished)
            {
                var officialWinners = officialPlacementRows
                    .Where(value => value.Placement == 1)
                    .OrderBy(value => value.TeamName, StringComparer.Ordinal)
                    .ToList();
                if (officialWinners.Count > 0)
                {
                    var resultTeams = officialWinners
                        .Select(value => new PublicEventResultTeam(value.TeamName, teamMap.GetValueOrDefault(value.TeamId)?.Slug))
                        .ToList();
                    eventResult = resultTeams.Count == 1
                        ? new PublicEventResult(resultTeams[0].TeamName, resultTeams[0].TeamSlug, true)
                        : new PublicEventResult(string.Join(" / ", resultTeams.Select(value => value.TeamName)), null, true, resultTeams);
                }
            }
            else if (bingoEvent.State == EventState.AwaitingFinalReview)
            {
                var provisionalLeaders = publicTeams.Where(value => value.Rank == 1).ToList();
                if (provisionalLeaders.Count == 1)
                {
                    var provisionalLeader = provisionalLeaders[0];
                    eventResult = new PublicEventResult(provisionalLeader.TeamName, provisionalLeader.TeamSlug, false);
                }
            }
        }
        var playerRows = publicTeams.SelectMany(team => team.Progress.Players
            .Where(player => player.ApprovedSubmissions > 0)
            .Select(player => new { team.TeamName, Player = player }))
            .OrderByDescending(value => value.Player.EstimatedEhb)
            .ThenByDescending(value => value.Player.ApprovedContribution)
            .ThenBy(value => value.Player.PlayerName)
            .ToList();
        var playerLeaderboard = playerRows.Select((value, index) => new PublicPlayerRanking(
            index + 1, value.Player.PlayerId, value.Player.PlayerName, value.TeamName,
            value.Player.EstimatedEhb, value.Player.ApprovedContribution, value.Player.ApprovedSubmissions)).ToList();
        var dropEhbTeams = publicTeams
            .Select(team =>
            {
                var players = team.Progress.Players
                    .Select(player =>
                    {
                        var playingAccountNames = playingAccountNamesByParticipant.GetValueOrDefault(player.PlayerId) ?? [player.PlayerName];
                        return new PublicDropEhbPlayer(
                            player.PlayerId, playingAccountNames[0], player.EstimatedEhb,
                            player.ApprovedContribution, player.ApprovedSubmissions, playingAccountNames);
                    })
                    .OrderByDescending(player => player.ApprovedSubmissions > 0)
                    .ThenByDescending(player => player.DropEhb)
                    .ThenByDescending(player => player.ApprovedContribution)
                    .ThenBy(player => player.PlayerName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(player => player.PlayerId)
                    .ToList();
                var contributors = players.Where(player => player.ApprovedSubmissions > 0).ToList();
                var mvpNames = contributors.Count == 0
                    ? new List<string>()
                    : contributors.Where(player => player.DropEhb == contributors[0].DropEhb)
                        .Select(player => player.PlayerName).ToList();
                return new PublicDropEhbTeam(
                    0, team.TeamId, team.TeamName, rosterRows.Count(row => row.TeamId == team.TeamId), contributors.Count,
                    players.Sum(player => player.ApprovedSubmissions), players.Sum(player => player.DropEhb),
                    mvpNames, players, contributors.Count == 0 ? null : contributors[0].DropEhb);
            })
            .OrderByDescending(team => team.DropEhb)
            .ThenByDescending(team => team.TotalDrops)
            .ThenBy(team => team.TeamName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(team => team.TeamId)
            .ToList();
        var rankedDropEhbTeams = new List<PublicDropEhbTeam>(dropEhbTeams.Count);
        for (var index = 0; index < dropEhbTeams.Count; index++)
        {
            var rank = index == 0 || dropEhbTeams[index].DropEhb != dropEhbTeams[index - 1].DropEhb
                ? index + 1
                : rankedDropEhbTeams[index - 1].Rank;
            rankedDropEhbTeams.Add(dropEhbTeams[index] with { Rank = rank });
        }

        var frozenTileIds = frozenTiles.Keys.ToList();
        var approvedRows = await (from submission in db.Submissions.AsNoTracking()
                                  join team in db.Teams.AsNoTracking() on submission.TeamId equals team.Id
                                  join tile in db.BoardApprovalTileSnapshots.AsNoTracking() on submission.BoardTileId equals tile.BoardTileId
                                  where tile.ApprovalSnapshotId == approvalId && teamIds.Contains(submission.TeamId) && frozenTileIds.Contains(submission.BoardTileId) && submission.Status == SubmissionStatus.Approved
                                  orderby (submission.ReviewedAt ?? submission.SubmittedAt) descending, submission.Id descending
                                  select new { submission, team, tile })
            .ToListAsync(cancellationToken);
        var approvedDropIds = approvedRows.Where(value => value.submission.DropSnapshotId is not null)
            .Select(value => value.submission.DropSnapshotId!.Value).Distinct().ToList();
        var approvedDropsById = publishedDrops;
        var submittedAt = approvedRows.Select(value => value.submission.SubmittedAt).ToList();
        var teamDropCounts = approvedRows.GroupBy(value => value.submission.TeamId)
            .ToDictionary(group => group.Key, group => group.Count());
        var highestDropEhbTeam = publicTeams
            .Where(value => teamDropCounts.ContainsKey(value.TeamId))
            .OrderByDescending(value => value.Progress.EhbTiebreak)
            .ThenBy(value => value.TeamName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var mostIndividualDropsTeam = publicTeams
            .Where(value => teamDropCounts.ContainsKey(value.TeamId))
            .OrderByDescending(value => teamDropCounts[value.TeamId])
            .ThenBy(value => value.TeamName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        var recentDropSummary = new PublicRecentDropSummary(
            approvedRows.Count,
            submittedAt.Count(value => value >= now.AddHours(-24) && value <= now),
            publicTeams.Sum(value => value.Progress.EhbTiebreak),
            approvedRows.Select(value => value.submission.CreditedParticipantId).Distinct().Count(),
            rosterRows.Select(value => value.PlayerId).Distinct().Count(),
            highestDropEhbTeam?.TeamName,
            mostIndividualDropsTeam?.TeamName,
            highestDropEhbTeam?.Progress.EhbTiebreak,
            mostIndividualDropsTeam is { } selectedMostIndividualDropsTeam
                ? teamDropCounts[selectedMostIndividualDropsTeam.TeamId]
                : null);
        var normalizedSearch = string.IsNullOrWhiteSpace(dropSearch) ? null : dropSearch.Trim();
        var searchTerms = normalizedSearch?.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        var normalizedTeam = string.IsNullOrWhiteSpace(dropTeam) ? null : dropTeam.Trim();
        var filteredRows = approvedRows.Where(value =>
        {
            var drop = value.submission.DropSnapshotId is Guid dropId ? approvedDropsById.GetValueOrDefault(dropId) : null;
            var teamMatches = normalizedTeam is null || string.Equals(value.team.Slug, normalizedTeam, StringComparison.OrdinalIgnoreCase);
            var searchMatches = searchTerms.Length == 0 || searchTerms.Any(term => new[]
            {
                drop?.ItemName, drop?.BossName, frozenTiles[value.tile.BoardTileId].Name,
                value.submission.CreditedCharacterName, value.team.Name
            }.Any(text => text?.Contains(term, StringComparison.OrdinalIgnoreCase) == true));
            return teamMatches && searchMatches;
        })
            .OrderByDescending(value => value.submission.SubmittedAt)
            .ThenByDescending(value => value.submission.Id)
            .ToList();
        var visibleRecentDropCount = Math.Min(Math.Max(recentDropCount, 25), filteredRows.Count);
        var recentRows = filteredRows.Take(visibleRecentDropCount).ToList();
        var recentSubmissionIds = recentRows.Select(value => value.submission.Id).ToList();
        var recentAssets = await db.EvidenceAssets.AsNoTracking()
            .Where(value => recentSubmissionIds.Contains(value.SubmissionId) && value.Active)
            .ToDictionaryAsync(value => value.SubmissionId, cancellationToken);
        var recentDrops = recentRows.Select(value =>
        {
            var drop = value.submission.DropSnapshotId is Guid dropId ? approvedDropsById.GetValueOrDefault(dropId) : null;
            var progress = historicalProgressBySubmission.GetValueOrDefault(value.submission.Id);
            return new PublicRecentDrop(
                value.submission.Id, value.tile.BoardTileId, frozenTiles[value.tile.BoardTileId].Name,
                value.team.Name, value.team.Slug, value.submission.CreditedCharacterName,
                drop?.BossName, drop?.ItemName,
                value.submission.ApprovedContribution, value.submission.SubmittedAt, value.submission.ReviewedAt,
                recentAssets.GetValueOrDefault(value.submission.Id)?.Id, progress.ProgressAfter, progress.Target);
        }).ToList();

        return new PublicEventBoard(bingoEvent.Id, bingoEvent.Name, bingoEvent.Slug, bingoEvent.State,
            approval.Rows, approval.Columns, approval.TotalEhbEstimate, publicTeams, playerLeaderboard, recentDrops,
            bingoEvent.EventStartsAt, bingoEvent.EventEndsAt, recentDropSummary, filteredRows.Count, eventResult, submissionsOpen,
            rankedDropEhbTeams, rosterPlayers, wiseOldManCompetitionId, bingoEvent.Timezone);
    }

    public async Task<PublicRecentDrop?> GetRecentDropAsync(string eventSlug, Guid submissionId, CancellationToken cancellationToken = default)
    {
        var feed = await GetRecentDropsCoreAsync(eventSlug, 1, 0, null, null, submissionId, null, cancellationToken);
        return feed is { Drops.Count: > 0 } ? feed.Drops[0] : null;
    }

    public Task<PublicRecentDropFeed?> GetRecentDropsAsync(string eventSlug, int limit = 25, string? dropSearch = null, string? dropTeam = null, IReadOnlyCollection<Guid>? loadedSubmissionIds = null, int offset = 0, CancellationToken cancellationToken = default)
        => GetRecentDropsCoreAsync(eventSlug, limit, offset, dropSearch, dropTeam, null, loadedSubmissionIds, cancellationToken);

    private async Task<PublicRecentDropFeed?> GetRecentDropsCoreAsync(string eventSlug, int limit, int offset, string? dropSearch, string? dropTeam, Guid? submissionId, IReadOnlyCollection<Guid>? loadedSubmissionIds, CancellationToken cancellationToken)
    {
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(value => value.Slug == eventSlug && value.HiddenAt == null, cancellationToken);
        if (ev is null || ev.State is EventState.Cancelled or EventState.Discarded) return null;
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(value => value.EventId == ev.Id && value.State == BoardState.Published, cancellationToken);
        if (board?.ActiveApprovalSnapshotId is not { } approvalId) return null;
        var publication = await db.ApprovalObjectivesAsync(board.Id, approvalId, cancellationToken);
        if (publication is null) return null;
        var publishedDrops = publication.Drops.ToDictionary(value => value.Id);
        var tileNames = db.BoardApprovalTileSnapshots.AsNoTracking()
            .Where(value => value.ApprovalSnapshotId == approvalId)
            .Select(value => new { value.BoardTileId, NameSnapshot = value.Name });
        var rows = from submission in db.Submissions.AsNoTracking()
                   join team in db.Teams.AsNoTracking() on submission.TeamId equals team.Id
                   join tile in tileNames on submission.BoardTileId equals tile.BoardTileId
                   join asset in db.EvidenceAssets.AsNoTracking().Where(value => value.Active) on submission.Id equals asset.SubmissionId into assetJoin
                   from asset in assetJoin.Select(value => (Guid?)value.Id).Take(1).DefaultIfEmpty()
                   where submission.EventId == ev.Id && submission.Status == SubmissionStatus.Approved && submission.ReviewedAt != null
                         && team.EventId == ev.Id && team.Active && team.FinalizedAt != null
                         && (submissionId == null ? true : submission.Id == submissionId.Value)
                   select new { submission, team, tile, EvidenceAssetId = asset };
        // Validate only the requested loaded cards against the same public approval boundary,
        // independently of the latest page and the reader's current search filters.
        var loadedIds = loadedSubmissionIds?.Distinct().Take(100).ToArray() ?? [];
        var validIds = loadedSubmissionIds is null ? null : await rows
            .Where(value => loadedIds.Contains(value.submission.Id))
            .Select(value => value.submission.Id).Distinct().ToListAsync(cancellationToken);
        var normalizedSearch = string.IsNullOrWhiteSpace(dropSearch) ? null : dropSearch.Trim();
        var searchTerms = normalizedSearch?.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        var normalizedTeam = string.IsNullOrWhiteSpace(dropTeam) ? null : dropTeam.Trim();
        if (normalizedTeam is not null) rows = rows.Where(value => value.team.Slug == normalizedTeam);
        foreach (var term in searchTerms)
        {
            var matchingDropIds = publishedDrops.Values
                .Where(value => value.ItemName?.Contains(term) == true || value.BossName?.Contains(term) == true)
                .Select(value => value.Id).ToArray();
            rows = rows.Where(value => (value.submission.DropSnapshotId != null && matchingDropIds.Contains(value.submission.DropSnapshotId.Value)) || value.tile.NameSnapshot.Contains(term) || value.submission.CreditedCharacterName.Contains(term) || value.team.Name.Contains(term));
        }
        var total = await rows.CountAsync(cancellationToken);
        var recentRows = await rows.OrderByDescending(value => value.submission.SubmittedAt).ThenByDescending(value => value.submission.Id)
            .Skip(Math.Max(offset, 0)).Take(Math.Clamp(limit, 1, 100)).ToListAsync(cancellationToken);
        var drops = recentRows.Select(value =>
        {
            var drop = value.submission.DropSnapshotId is Guid dropId ? publishedDrops.GetValueOrDefault(dropId) : null;
            return new PublicRecentDrop(
                value.submission.Id, value.tile.BoardTileId, value.tile.NameSnapshot, value.team.Name, value.team.Slug,
                value.submission.CreditedCharacterName, drop?.BossName, drop?.ItemName,
                value.submission.ApprovedContribution, value.submission.SubmittedAt, value.submission.ReviewedAt, value.EvidenceAssetId, 0, 0);
        }).ToList();
        return new PublicRecentDropFeed(drops, total, validIds);
    }

    public async Task<PublicTileDetails?> GetTileAsync(string eventSlug, string teamSlug, Guid tileId, CancellationToken cancellationToken = default)
    {
        var boardView = await GetEventBoardAsync(eventSlug, cancellationToken);
        if (boardView is null) return null;
        var team = boardView.Teams.SingleOrDefault(value => value.TeamSlug == teamSlug);
        var tile = team?.Tiles.SingleOrDefault(value => value.TileId == tileId);
        if (team is null || tile is null) return null;
        var publishedBoard = await db.Boards.AsNoTracking().SingleAsync(value => value.EventId == boardView.EventId && value.State == BoardState.Published, cancellationToken);
        if (publishedBoard.ActiveApprovalSnapshotId is not { } approvalId) return null;
        var approvalTile = await db.BoardApprovalTileSnapshots.AsNoTracking().SingleOrDefaultAsync(value => value.ApprovalSnapshotId == approvalId && value.BoardTileId == tileId, cancellationToken);
        if (approvalTile is null) return null;
        var frozenRequirements = await db.BoardApprovalRequirementSnapshots.AsNoTracking()
            .Join(db.BoardApprovalTileSnapshots.AsNoTracking().Where(value => value.ApprovalSnapshotId == approvalId), requirement => requirement.ApprovalTileSnapshotId, tileSnapshot => tileSnapshot.Id, (requirement, _) => requirement)
            .ToDictionaryAsync(value => value.BoardRequirementSnapshotId, cancellationToken);
        var tileRequirements = frozenRequirements.Values.Where(value => value.ApprovalTileSnapshotId == approvalTile.Id).OrderBy(value => value.Position).ToList();
        if (tileRequirements.Count == 0) return null;
        var requirementIds = tileRequirements.Select(value => value.BoardRequirementSnapshotId).ToList();
        var frozenApprovalRequirementIds = tileRequirements.Select(value => value.Id).ToList();
        var eligibleDrops = await db.BoardApprovalRequirementDropSnapshots.AsNoTracking()
            .Where(value => frozenApprovalRequirementIds.Contains(value.ApprovalRequirementSnapshotId))
            .OrderBy(value => value.BossName).ThenBy(value => value.ItemName)
            .ToListAsync(cancellationToken);
        var contributionTotals = await db.SubmissionContributions.AsNoTracking()
            .Where(value => value.TeamId == team.TeamId && requirementIds.Contains(value.RequirementId) && value.ReversedAt == null)
            .GroupBy(value => value.RequirementId).Select(group => new { Id = group.Key, Total = group.Sum(value => value.Amount) })
            .ToDictionaryAsync(value => value.Id, value => value.Total, cancellationToken);
        var evidenceRows = await (from submission in db.Submissions.AsNoTracking()
                                  where submission.TeamId == team.TeamId && submission.BoardTileId == tileId && submission.Status == SubmissionStatus.Approved
                                  orderby submission.SubmittedAt descending
                                  select submission).ToListAsync(cancellationToken);
        var dropIds = evidenceRows.Where(value => value.DropSnapshotId is not null).Select(value => value.DropSnapshotId!.Value).Distinct().ToList();
        var publication = await db.ApprovalObjectivesAsync(publishedBoard.Id, approvalId, cancellationToken);
        if (publication is null) return null;
        var drops = publication.Drops.ToDictionary(x => x.Id);
        var submissionIds = evidenceRows.Select(value => value.Id).ToList();
        var assets = await db.EvidenceAssets.AsNoTracking().Where(value => submissionIds.Contains(value.SubmissionId) && value.Active).ToDictionaryAsync(value => value.SubmissionId, cancellationToken);
        var evidence = evidenceRows.Select(value =>
        {
            var drop = value.DropSnapshotId is Guid dropId ? drops.GetValueOrDefault(dropId) : null;
            return new PublicApprovedEvidence(
                value.Id, value.CreditedCharacterName,
                drop?.BossName, drop?.ItemName, value.ApprovedContribution,
                value.SubmittedAt, assets.GetValueOrDefault(value.Id)?.Id);
        }).ToList();
        return new PublicTileDetails(
            boardView.EventName, boardView.EventSlug, team.TeamName, team.TeamSlug,
            tile.TileId, tile.Name, tile.Description, approvalTile.EvidenceInstructions,
            tile.Approved, tile.Target, tile.Complete, tile.CompletedAt,
            tileRequirements.Select(value => new PublicRequirementProgress(
                value.BoardRequirementSnapshotId, value.Description, Math.Min(value.TargetContribution, contributionTotals.GetValueOrDefault(value.BoardRequirementSnapshotId)),
                value.TargetContribution, contributionTotals.GetValueOrDefault(value.BoardRequirementSnapshotId) >= value.TargetContribution,
                eligibleDrops.Where(drop => drop.ApprovalRequirementSnapshotId == value.Id)
                    .Select(drop => new PublicEligibleDrop(
                        drop.BossName, drop.ItemName, drop.DisplayRate, drop.CreditedWeight, drop.NumericProbability, drop.RollsPerCompletion, drop.ProbabilityScope, drop.ConditionalOnParent, drop.ParentProbability, drop.AssumedParticipants, drop.RollGroup, drop.RateCondition))
                    .ToList())).ToList(),
            evidence, boardView.Timezone,
            await new Bingo.Infrastructure.Stats.PublicStatsService(db, time).GetTileAsync(eventSlug, team.TeamId, tileId, cancellationToken));
    }
}
