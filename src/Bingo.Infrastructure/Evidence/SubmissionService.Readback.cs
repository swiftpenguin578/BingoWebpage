using System.Data;
using System.Text.Json;
using Bingo.Application.Evidence;
using Bingo.Domain.Boards;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Boards;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Evidence;

public sealed partial class SubmissionService
{
    public async Task<SubmissionReviewReadback> GetReviewReadbackAsync(Guid submissionId, Guid adminAccountId, CancellationToken cancellationToken = default)
    {
        var adminValidated = false;
        try
        {
            await EnsureAdmin(adminAccountId, cancellationToken);
            adminValidated = true;
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            var s = await db.Submissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == submissionId, cancellationToken);
            if (s is null || !await db.Events.AsNoTracking().AnyAsync(x => x.Id == s.EventId && x.HiddenAt == null, cancellationToken))
                return new(null);
            var publication = await db.PublishedObjectivesAsync(s.EventId, cancellationToken);
            var requirement = publication?.Requirements.SingleOrDefault(x => x.Id == s.RequirementId && x.BoardTileId == s.BoardTileId);
            if (publication is null || requirement is null) return new(null);
            var allocation = await ContributionAsync(s, publication, requirement, cancellationToken);
            var actions = await db.ReviewActions.AsNoTracking().Where(x => x.SubmissionId == submissionId).ToListAsync(cancellationToken);
            // New actions carry the submission version in their existing snapshot.
            // Legacy equal-time actions have no reliable ordering: omit attribution
            // rather than choose an arbitrary GUID as the purported latest decision.
            var ordered = actions.OrderByDescending(x => SnapshotVersion(x.AfterSnapshot)).ThenByDescending(x => x.PerformedAt).ToList();
            var latest = ordered.FirstOrDefault();
            if (latest is not null && ordered.Skip(1).Any(x => SnapshotVersion(x.AfterSnapshot) == SnapshotVersion(latest.AfterSnapshot) && x.PerformedAt == latest.PerformedAt)) latest = null;
            SubmissionLatestReviewAction? action = null;
            if (latest is not null)
            {
                var actor = await db.Accounts.AsNoTracking().Where(x => x.Id == latest.PerformedByAccountId).Select(x => x.PublicUsername).SingleAsync(cancellationToken);
                action = new(latest.Id, latest.Action, latest.PerformedByAccountId, actor, latest.PerformedAt,
                    latest.Action is ReviewActionType.Reject or ReviewActionType.EditMetadata or ReviewActionType.ReverseApproval && !string.IsNullOrWhiteSpace(latest.Note),
                    SnapshotVersion(latest.BeforeSnapshot), SnapshotVersion(latest.AfterSnapshot));
            }
            var state = new SubmissionReviewState(s.Id, s.EventId, s.TeamId, s.Version, s.Status, s.BoardTileId, s.RequirementId,
                s.DropSnapshotId, s.CreditedParticipantId, s.CreditedOsrsCharacterId, s.CreditedCharacterName, s.ClaimedWeight,
                s.ApprovedContribution, action, allocation);
            await tx.CommitAsync(cancellationToken);
            return new(state);
        }
        catch (Exception ex) when (ex is not OperationCanceledException &&
            (adminValidated || ex is not InvalidOperationException || ex.Message != "Administrator access is required."))
        {
            return new(null);
        }
    }

    private static int? SnapshotVersion(string? json)
    {
        if (json is null) return null;
        try { using var document = JsonDocument.Parse(json); return document.RootElement.TryGetProperty("Version", out var version) && version.TryGetInt32(out var value) ? value : null; }
        catch (JsonException) { return null; }
    }

    private async Task<SubmissionContributionRead> ContributionAsync(Submission s, PublishedBoardData publication, BoardRequirementSnapshot requirement, CancellationToken ct)
    {
        var used = await db.SubmissionContributions.AsNoTracking().Where(x => x.TeamId == s.TeamId && x.RequirementId == s.RequirementId && x.ReversedAt == null)
            .SumAsync(x => (int?)x.Amount, ct) ?? 0;
        if (s.Status == SubmissionStatus.Approved) used -= s.ApprovedContribution;
        var remaining = Math.Max(0, requirement.TargetContribution - used);
        var amount = s.Status == SubmissionStatus.Approved ? s.ApprovedContribution : 0;
        if (s.Status == SubmissionStatus.Pending)
        {
            var allowed = s.ClaimedWeight;
            if (s.DropSnapshotId is Guid dropId)
            {
                var drop = publication.Drops.Single(x => x.Id == dropId && x.RequirementId == s.RequirementId);
                var maximum = MaximumContribution(requirement, drop, publication.Drops);
                var dropUsed = await UsedDropContributionAsync(s.TeamId, s.RequirementId, drop, requirement.DuplicatesAllowed, ct);
                allowed = Math.Min(allowed, Math.Max(0, maximum - dropUsed));
            }
            amount = Math.Min(remaining, allowed);
            if (amount > 0 && await FindEarlierApprovalBlockAsync(s, requirement, publication, amount, ct) is { } block)
                return new(null, block);
        }
        return new(new(amount, s.ClaimedWeight, remaining, used, requirement.TargetContribution, used + amount >= requirement.TargetContribution));
    }
}
