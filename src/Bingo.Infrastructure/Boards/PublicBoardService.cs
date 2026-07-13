using Bingo.Application.Boards;
using Bingo.Domain.Boards;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Boards;

public sealed class PublicBoardService(ApplicationDbContext db) : IPublicBoardService
{
    public async Task<PublicEventBoard?> GetEventBoardAsync(string eventSlug, CancellationToken cancellationToken = default)
    {
        var bingoEvent = await db.Events.AsNoTracking().SingleOrDefaultAsync(value => value.Slug == eventSlug, cancellationToken);
        if (bingoEvent is null) return null;
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(value => value.EventId == bingoEvent.Id && value.State == BoardState.Published, cancellationToken);
        if (board is null) return null;
        var teams = await db.Teams.AsNoTracking().Where(value => value.EventId == bingoEvent.Id && value.Active && value.FinalizedAt != null).OrderBy(value => value.Name).ToListAsync(cancellationToken);
        if (teams.Count == 0) return null;

        var tiles = await db.BoardTiles.AsNoTracking().Where(value => value.BoardId == board.Id).OrderBy(value => value.RowIndex).ThenBy(value => value.ColumnIndex).ToListAsync(cancellationToken);
        var tileIds = tiles.Select(value => value.Id).ToList();
        var requirements = await db.BoardRequirementSnapshots.AsNoTracking().Where(value => tileIds.Contains(value.BoardTileId)).OrderBy(value => value.Position).ToListAsync(cancellationToken);
        var requirementIds = requirements.Select(value => value.Id).ToList();
        var dropEhb = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(value => requirementIds.Contains(value.RequirementId)).ToDictionaryAsync(value => value.Id, value => value.EhbPerContribution, cancellationToken);
        var teamIds = teams.Select(value => value.Id).ToList();
        var contributionRows = await (from contribution in db.SubmissionContributions.AsNoTracking()
                                      join submission in db.Submissions.AsNoTracking() on contribution.SubmissionId equals submission.Id
                                      join player in db.EventParticipants.AsNoTracking() on contribution.CreditedParticipantId equals player.Id
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
            var contributions = contributionRows.Where(row => row.contribution.TeamId == team.Id).Select(row =>
            {
                var tile = tileByRequirement[row.contribution.RequirementId];
                var perContribution = row.contribution.DropSnapshotId is Guid dropId && dropEhb.GetValueOrDefault(dropId) is decimal value
                    ? value
                    : tile.EstimatedEhbSnapshot / requirementTargetByTile[tile.Id];
                return new ProgressContribution(
                    row.contribution.Id, row.contribution.RequirementId, row.player.Id, row.player.PrimaryAccountName,
                    row.contribution.Amount, row.submission.SubmittedAt,
                    perContribution * row.contribution.Amount, row.submission.PublicPlayerHidden);
            }).ToList();
            return new UnrankedTeamProgress(team.Id, team.Name, PublicProgressCalculator.Calculate(board.Rows, board.Columns, definitions, contributions));
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
                    return new PublicTileProgress(tile.Id, tile.RowIndex, tile.ColumnIndex, tile.NameSnapshot,
                        tile.DescriptionSnapshot, tile.EstimatedEhbSnapshot, progress.Approved, progress.Target,
                        progress.Complete, progress.CompletedAt);
                }).ToList());
        }).ToList();
        var playerRows = publicTeams.SelectMany(team => team.Progress.Players.Select(player => new { team.TeamName, Player = player })).OrderByDescending(value => value.Player.EstimatedEhb).ThenByDescending(value => value.Player.ApprovedContribution).ThenBy(value => value.Player.PlayerName).ToList();
        var playerLeaderboard = playerRows.Select((value, index) => new PublicPlayerRanking(
            index + 1, value.Player.PlayerId, value.Player.PlayerName, value.TeamName,
            value.Player.EstimatedEhb, value.Player.ApprovedContribution, value.Player.ApprovedSubmissions)).ToList();

        return new PublicEventBoard(bingoEvent.Id, bingoEvent.Name, bingoEvent.Slug, bingoEvent.State,
            board.Rows, board.Columns, board.TotalEhbEstimate, publicTeams, playerLeaderboard);
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
        var contributionTotals = await db.SubmissionContributions.AsNoTracking()
            .Where(value => value.TeamId == team.TeamId && requirementIds.Contains(value.RequirementId) && value.ReversedAt == null)
            .GroupBy(value => value.RequirementId).Select(group => new { Id = group.Key, Total = group.Sum(value => value.Amount) })
            .ToDictionaryAsync(value => value.Id, value => value.Total, cancellationToken);
        var evidenceRows = await (from submission in db.Submissions.AsNoTracking()
                                  join player in db.EventParticipants.AsNoTracking() on submission.CreditedParticipantId equals player.Id
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
                value.submission.Id, hidden ? null : value.player.PrimaryAccountName,
                drop?.BossName, drop?.ItemName, value.submission.ApprovedContribution,
                value.submission.SubmittedAt, hidden ? null : assets.GetValueOrDefault(value.submission.Id)?.Id, hidden);
        }).ToList();
        return new PublicTileDetails(
            boardView.EventName, boardView.EventSlug, team.TeamName, team.TeamSlug,
            tile.TileId, tile.Name, tile.Description, tileEntity.EvidenceInstructionsSnapshot,
            tile.Approved, tile.Target, tile.Complete, tile.CompletedAt,
            requirements.Select(value => new PublicRequirementProgress(
                value.Id, value.Description, Math.Min(value.TargetContribution, contributionTotals.GetValueOrDefault(value.Id)),
                value.TargetContribution, contributionTotals.GetValueOrDefault(value.Id) >= value.TargetContribution)).ToList(),
            evidence);
    }
}
