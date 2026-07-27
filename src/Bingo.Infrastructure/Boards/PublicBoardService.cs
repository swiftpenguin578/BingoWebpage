using Bingo.Application.Boards;
using Bingo.Application.Catalogue;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Boards;

public sealed class PublicBoardService(ApplicationDbContext db) : IPublicBoardService
{
    public async Task<PublicEventBoard?> GetEventBoardAsync(string eventSlug, CancellationToken cancellationToken = default)
    {
        var bingoEvent = await db.Events.AsNoTracking().SingleOrDefaultAsync(value => value.Slug == eventSlug, cancellationToken);
        if (bingoEvent is null) return null;
        if (bingoEvent.State == EventState.Discarded || bingoEvent.State == EventState.Cancelled && (bingoEvent.FirstPublicAt is null || !bingoEvent.BoardPublished)) return null;
        if (bingoEvent.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed) return null;
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(value => value.EventId == bingoEvent.Id && value.State == BoardState.Published, cancellationToken);
        if (board is null) return null;
        var teams = await db.Teams.AsNoTracking().Where(value => value.EventId == bingoEvent.Id && value.Active && value.FinalizedAt != null).OrderBy(value => value.Name).ToListAsync(cancellationToken);
        if (teams.Count == 0) return null;

        var tiles = await db.BoardTiles.AsNoTracking().Where(value => value.BoardId == board.Id).OrderBy(value => value.RowIndex).ThenBy(value => value.ColumnIndex).ToListAsync(cancellationToken);
        var tileIds = tiles.Select(value => value.Id).ToList();
        var requirements = await db.BoardRequirementSnapshots.AsNoTracking().Where(value => tileIds.Contains(value.BoardTileId)).OrderBy(value => value.Position).ToListAsync(cancellationToken);
        var requirementIds = requirements.Select(value => value.Id).ToList();
        var bossArtwork = await (from snapshot in db.BoardRequirementBossSnapshots.AsNoTracking()
                                 join boss in db.BossActivities.AsNoTracking() on snapshot.BossActivityId equals boss.Id
                                 where requirementIds.Contains(snapshot.RequirementId) && boss.ImageUrl != null
                                 select new { snapshot.RequirementId, BossName = boss.Name, boss.ImageUrl }).ToListAsync(cancellationToken);
        var artworkByRequirement = bossArtwork
            .Select(value => new { value.RequirementId, value.BossName, ImageUrl = OsrsWikiImageUrl.Normalize(value.ImageUrl) })
            .Where(value => !string.IsNullOrWhiteSpace(value.ImageUrl))
            .GroupBy(value => value.RequirementId)
            .ToDictionary(group => group.Key, group => group
                .GroupBy(value => BossArtworkFamily.Key(value.BossName), StringComparer.OrdinalIgnoreCase)
                .Select(family => family.OrderBy(value => BossArtworkFamily.Priority(value.BossName)).ThenBy(value => value.BossName).First().ImageUrl!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());
        var teamIds = teams.Select(value => value.Id).ToList();
        var rosterRows = await (from membership in db.TeamMemberships.AsNoTracking()
                                join player in db.PrimaryCharacters().AsNoTracking() on membership.EventParticipantId equals player.ParticipantId
                                where teamIds.Contains(membership.TeamId) && membership.LeftAt == null
                                select new { membership.TeamId, PlayerId = player.ParticipantId, PlayerName = player.Name })
            .ToListAsync(cancellationToken);
        var contributionRows = await (from contribution in db.SubmissionContributions.AsNoTracking()
                                      join submission in db.Submissions.AsNoTracking() on contribution.SubmissionId equals submission.Id
                                      join player in db.PrimaryCharacters().AsNoTracking() on contribution.CreditedParticipantId equals player.ParticipantId
                                      where teamIds.Contains(contribution.TeamId) && requirementIds.Contains(contribution.RequirementId) &&
                                            contribution.ReversedAt == null && submission.Status == SubmissionStatus.Approved
                                      select new { contribution, submission, player }).ToListAsync(cancellationToken);
        var tileByRequirement = requirements.ToDictionary(value => value.Id, value => tiles.Single(tile => tile.Id == value.BoardTileId));
        var requirementTargetByTile = requirements.GroupBy(value => value.BoardTileId).ToDictionary(group => group.Key, group => Math.Max(1, group.Sum(value => value.TargetContribution)));
        var definitions = tiles.Select(tile => new ProgressTileDefinition(
            tile.Id, tile.RowIndex, tile.ColumnIndex, tile.EstimatedEhbSnapshot,
            requirements.Where(requirement => requirement.BoardTileId == tile.Id)
                .Select(requirement => new ProgressRequirementDefinition(requirement.Id, requirement.Position, requirement.TargetContribution)).ToList())).ToList();

        var unranked = teams.Select(team =>
        {
            var creditedByTile = new Dictionary<Guid, int>();
            var contributions = contributionRows
                .Where(row => row.contribution.TeamId == team.Id)
                .OrderBy(row => row.submission.SubmittedAt)
                .ThenBy(row => row.contribution.Id)
                .Select(row =>
            {
                var tile = tileByRequirement[row.contribution.RequirementId];
                var tileTarget = requirementTargetByTile[tile.Id];
                var creditedBefore = creditedByTile.GetValueOrDefault(tile.Id);
                var creditedAfter = Math.Min(tileTarget, creditedBefore + row.contribution.Amount);
                var estimatedBefore = tile.EstimatedEhbSnapshot * creditedBefore / tileTarget;
                var estimatedAfter = creditedAfter == tileTarget
                    ? tile.EstimatedEhbSnapshot
                    : tile.EstimatedEhbSnapshot * creditedAfter / tileTarget;
                creditedByTile[tile.Id] = creditedAfter;
                return new ProgressContribution(
                    row.contribution.Id, row.contribution.RequirementId, row.player.ParticipantId, row.player.Name,
                    row.contribution.Amount, row.submission.SubmittedAt,
                    estimatedAfter - estimatedBefore, row.submission.PublicPlayerHidden);
            }).ToList();
            var progress = PublicProgressCalculator.Calculate(board.Rows, board.Columns, definitions, contributions);
            var contributionByPlayer = progress.Players.ToDictionary(value => value.PlayerId);
            var players = rosterRows
                .Where(value => value.TeamId == team.Id)
                .Select(value => contributionByPlayer.GetValueOrDefault(value.PlayerId) ??
                    new CalculatedPlayerContribution(value.PlayerId, value.PlayerName, 0, 0, 0))
                .OrderByDescending(value => value.EstimatedEhb)
                .ThenByDescending(value => value.ApprovedContribution)
                .ThenBy(value => value.PlayerName)
                .ToList();
            return new UnrankedTeamProgress(team.Id, team.Name, progress with { Players = players });
        }).ToList();
        var ranked = PublicProgressCalculator.Rank(unranked);
        var activeFinalization = bingoEvent.ResultsPublished
            ? await db.EventFinalizations.AsNoTracking().Where(value => value.EventId == bingoEvent.Id && value.UnfinalizedAt == null).OrderByDescending(value => value.Version).FirstOrDefaultAsync(cancellationToken)
            : null;
        var officialPlacements = activeFinalization is null
            ? new Dictionary<Guid, int>()
            : await db.OfficialPlacements.AsNoTracking().Where(value => value.FinalizationId == activeFinalization.Id).ToDictionaryAsync(value => value.TeamId, value => value.Placement, cancellationToken);
        var teamMap = teams.ToDictionary(value => value.Id);
        var tileMap = tiles.ToDictionary(value => value.Id);
        var publicTeams = ranked.Select(value =>
        {
            var team = teamMap[value.TeamId];
            var displayedRank = officialPlacements.GetValueOrDefault(value.TeamId, value.Rank);
            return new PublicTeamBoard(
                team.Id, team.Name, team.Slug, team.AffiliationName, team.ImageUrl,
                displayedRank, displayedRank == 1 && value.Progress.BoardComplete, value.Progress,
                value.Progress.Tiles.Select(progress =>
                {
                    var tile = tileMap[progress.Id];
                    var bossImageUrls = requirements.Where(requirement => requirement.BoardTileId == tile.Id)
                        .SelectMany(requirement => artworkByRequirement.GetValueOrDefault(requirement.Id) ?? [])
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(4)
                        .ToList();
                    return new PublicTileProgress(tile.Id, tile.RowIndex, tile.ColumnIndex, tile.NameSnapshot,
                        tile.DescriptionSnapshot, OsrsWikiImageUrl.Normalize(tile.ImageUrlSnapshot), bossImageUrls,
                        tile.EstimatedEhbSnapshot, progress.Approved, progress.Target,
                        progress.Complete, progress.CompletedAt);
                }).ToList());
        }).ToList();
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

        var recentRows = await (from submission in db.Submissions.AsNoTracking()
                                join player in db.PrimaryCharacters().AsNoTracking() on submission.CreditedParticipantId equals player.ParticipantId
                                join team in db.Teams.AsNoTracking() on submission.TeamId equals team.Id
                                join tile in db.BoardTiles.AsNoTracking() on submission.BoardTileId equals tile.Id
                                where teamIds.Contains(submission.TeamId) && tileIds.Contains(submission.BoardTileId) &&
                                      submission.Status == SubmissionStatus.Approved
                                orderby (submission.ReviewedAt ?? submission.SubmittedAt) descending
                                select new { submission, player, team, tile })
            .Take(24)
            .ToListAsync(cancellationToken);
        var recentDropIds = recentRows.Where(value => value.submission.DropSnapshotId is not null)
            .Select(value => value.submission.DropSnapshotId!.Value).Distinct().ToList();
        var recentDropsById = await db.BoardRequirementDropSnapshots.AsNoTracking()
            .Where(value => recentDropIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, cancellationToken);
        var recentSubmissionIds = recentRows.Select(value => value.submission.Id).ToList();
        var recentAssets = await db.EvidenceAssets.AsNoTracking()
            .Where(value => recentSubmissionIds.Contains(value.SubmissionId) && value.Active)
            .ToDictionaryAsync(value => value.SubmissionId, cancellationToken);
        var recentDrops = recentRows.Select(value =>
        {
            var hidden = value.submission.PublicEvidenceHidden || value.submission.PublicPlayerHidden;
            var drop = value.submission.DropSnapshotId is Guid dropId ? recentDropsById.GetValueOrDefault(dropId) : null;
            return new PublicRecentDrop(
                value.submission.Id, value.tile.Id, value.tile.NameSnapshot,
                value.team.Name, value.team.Slug, hidden ? null : value.player.Name,
                hidden ? null : drop?.BossName, hidden ? null : drop?.ItemName,
                value.submission.ApprovedContribution, value.submission.ReviewedAt ?? value.submission.SubmittedAt,
                hidden ? null : recentAssets.GetValueOrDefault(value.submission.Id)?.Id, hidden);
        }).ToList();

        return new PublicEventBoard(bingoEvent.Id, bingoEvent.Name, bingoEvent.Slug, bingoEvent.State,
            board.Rows, board.Columns, board.TotalEhbEstimate, publicTeams, playerLeaderboard, recentDrops);
    }

    public async Task<PublicTileDetails?> GetTileAsync(string eventSlug, string teamSlug, Guid tileId, CancellationToken cancellationToken = default)
    {
        var boardView = await GetEventBoardAsync(eventSlug, cancellationToken);
        if (boardView is null) return null;
        var team = boardView.Teams.SingleOrDefault(value => value.TeamSlug == teamSlug);
        var tile = team?.Tiles.SingleOrDefault(value => value.TileId == tileId);
        if (team is null || tile is null) return null;
        var tileEntity = await db.BoardTiles.AsNoTracking().SingleAsync(value => value.Id == tileId, cancellationToken);
        var requirements = await db.BoardRequirementSnapshots.AsNoTracking().Where(value => value.BoardTileId == tileId).OrderBy(value => value.Position).ToListAsync(cancellationToken);
        var requirementIds = requirements.Select(value => value.Id).ToList();
        var eligibleDrops = await db.BoardRequirementDropSnapshots.AsNoTracking()
            .Where(value => requirementIds.Contains(value.RequirementId))
            .OrderBy(value => value.BossName).ThenBy(value => value.ItemName)
            .ToListAsync(cancellationToken);
        var contributionTotals = await db.SubmissionContributions.AsNoTracking()
            .Where(value => value.TeamId == team.TeamId && requirementIds.Contains(value.RequirementId) && value.ReversedAt == null)
            .GroupBy(value => value.RequirementId).Select(group => new { Id = group.Key, Total = group.Sum(value => value.Amount) })
            .ToDictionaryAsync(value => value.Id, value => value.Total, cancellationToken);
        var evidenceRows = await (from submission in db.Submissions.AsNoTracking()
                                  join player in db.PrimaryCharacters().AsNoTracking() on submission.CreditedParticipantId equals player.ParticipantId
                                  where submission.TeamId == team.TeamId && submission.BoardTileId == tileId && submission.Status == SubmissionStatus.Approved
                                  orderby submission.SubmittedAt descending
                                  select new { submission, player }).ToListAsync(cancellationToken);
        var dropIds = evidenceRows.Where(value => value.submission.DropSnapshotId is not null).Select(value => value.submission.DropSnapshotId!.Value).Distinct().ToList();
        var drops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(value => dropIds.Contains(value.Id)).ToDictionaryAsync(value => value.Id, cancellationToken);
        var submissionIds = evidenceRows.Select(value => value.submission.Id).ToList();
        var assets = await db.EvidenceAssets.AsNoTracking().Where(value => submissionIds.Contains(value.SubmissionId) && value.Active).ToDictionaryAsync(value => value.SubmissionId, cancellationToken);
        var evidence = evidenceRows.Select(value =>
        {
            var hidden = value.submission.PublicEvidenceHidden || value.submission.PublicPlayerHidden;
            var drop = value.submission.DropSnapshotId is Guid dropId ? drops.GetValueOrDefault(dropId) : null;
            return new PublicApprovedEvidence(
                value.submission.Id, hidden ? null : value.player.Name,
                drop?.BossName, drop?.ItemName, value.submission.ApprovedContribution,
                value.submission.SubmittedAt, hidden ? null : assets.GetValueOrDefault(value.submission.Id)?.Id, hidden);
        }).ToList();
        return new PublicTileDetails(
            boardView.EventName, boardView.EventSlug, team.TeamName, team.TeamSlug,
            tile.TileId, tile.Name, tile.Description, tileEntity.EvidenceInstructionsSnapshot,
            tile.Approved, tile.Target, tile.Complete, tile.CompletedAt,
            requirements.Select(value => new PublicRequirementProgress(
                value.Id, value.Description, Math.Min(value.TargetContribution, contributionTotals.GetValueOrDefault(value.Id)),
                value.TargetContribution, contributionTotals.GetValueOrDefault(value.Id) >= value.TargetContribution,
                eligibleDrops.Where(drop => drop.RequirementId == value.Id)
                    .Select(drop => new PublicEligibleDrop(
                        drop.BossName, drop.ItemName, drop.DisplayRate, drop.CreditedWeight))
                    .ToList())).ToList(),
            evidence);
    }
}
