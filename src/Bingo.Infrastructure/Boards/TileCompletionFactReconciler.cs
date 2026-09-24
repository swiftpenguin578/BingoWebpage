using System.Text.Json;
using Bingo.Application.Boards;
using Bingo.Domain.Boards;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Boards;

public static class TileCompletionFactReconciler
{
    public static async Task ReconcileAsync(
        ApplicationDbContext db,
        Guid eventId,
        PublishedBoardData publication,
        IEnumerable<Guid> affectedTeamIds,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken = default)
    {
        var teamIds = affectedTeamIds.Distinct().ToList();
        if (teamIds.Count == 0) return;

        var requirementIds = publication.Requirements.Select(value => value.Id).ToList();
        var definitions = publication.Tiles.Select(tile => new ProgressTileDefinition(
            tile.Id,
            tile.RowIndex,
            tile.ColumnIndex,
            tile.EstimatedEhbSnapshot,
            publication.Requirements.Where(value => value.BoardTileId == tile.Id)
                .OrderBy(value => value.Position)
                .Select(value => new ProgressRequirementDefinition(value.Id, value.Position, value.TargetContribution, value.DuplicatesAllowed))
                .ToList())).ToList();
        var publishedDrops = publication.Drops.ToDictionary(value => value.Id);
        var contributionRows = await (from contribution in db.SubmissionContributions.AsNoTracking()
                                      join submission in db.Submissions.AsNoTracking() on contribution.SubmissionId equals submission.Id
                                      where submission.EventId == eventId && teamIds.Contains(contribution.TeamId) &&
                                            requirementIds.Contains(contribution.RequirementId) && contribution.ReversedAt == null &&
                                            submission.Status == SubmissionStatus.Approved
                                      select new { contribution, submission }).ToListAsync(cancellationToken);
        var contributionsByTeam = contributionRows.GroupBy(row => row.contribution.TeamId).ToDictionary(group => group.Key, group =>
            group.Select(row =>
            {
                var drop = row.contribution.DropSnapshotId is { } dropId ? publishedDrops.GetValueOrDefault(dropId) : null;
                return new ProgressContribution(row.contribution.Id, row.contribution.RequirementId,
                    row.contribution.CreditedParticipantId, row.submission.CreditedCharacterName,
                    row.contribution.Amount, row.submission.SubmittedAt, 0,
                    drop?.ItemIdSnapshot, drop?.SourceDropId, drop?.MaximumContribution, row.submission.Id);
            }).ToList());

        var completionByTeam = teamIds.ToDictionary(teamId => teamId, teamId =>
            PublicProgressCalculator.CalculateTileCompletionFacts(definitions, contributionsByTeam.GetValueOrDefault(teamId) ?? []));
        var existing = await db.TileCompletionFacts
            .Where(value => value.EventId == eventId && value.ApprovalSnapshotId == publication.Approval.Id && teamIds.Contains(value.TeamId))
            .ToListAsync(cancellationToken);
        var byKey = existing.ToDictionary(value => (value.TeamId, value.BoardTileId));

        foreach (var teamId in teamIds)
        foreach (var completion in completionByTeam[teamId])
        {
            var provenanceJson = completion.Complete
                ? JsonSerializer.Serialize(completion.QualifyingContributions)
                : "[]";
            if (byKey.TryGetValue((teamId, completion.TileId), out var current))
            {
                current.Reconcile(completion.Complete, completion.CompletedAt, provenanceJson, recordedAt);
                continue;
            }

            db.TileCompletionFacts.Add(new TileCompletionFact(Guid.NewGuid(), eventId, teamId, completion.TileId,
                publication.Approval.Id, completion.Complete, completion.CompletedAt, provenanceJson, recordedAt));
        }
    }
}
