using System.Data;
using System.Text.Json;
using Bingo.Application.Boards;
using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Evidence;

public sealed class SubmissionService(ApplicationDbContext db, IEvidenceStorage storage, TimeProvider time, IProgressNotifier? progressNotifier = null) : ISubmissionService
{
    public async Task<SubmissionResult> CreateAsync(CreateSubmissionCommand command, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow(); var actor = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.ActorAccountId, cancellationToken) ?? throw new InvalidOperationException("Account not found.");
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.EventId, cancellationToken) ?? throw new InvalidOperationException("Event not found.");
        var isAdmin = actor.GlobalRole is GlobalRole.Admin or GlobalRole.SuperAdmin;
        if (!isAdmin)
        {
            var access = await db.AccountEventAccesses.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actor.Id, cancellationToken);
            if (actor.AccountType != AccountType.EmergencyCaptain || access?.EventId != command.EventId || access.TeamId != command.TeamId || access.GetAccessMode(now) != AccountAccessMode.Full) throw new InvalidOperationException("This captain account cannot submit for that team.");
            if (!ev.AcceptsEmergencySubmissions(now)) throw new InvalidOperationException("New submissions are not currently open.");
        }
        var creditedWeight = await ValidateTarget(command.EventId, command.TeamId, command.BoardTileId, command.RequirementId, command.DropSnapshotId, command.CreditedParticipantId, cancellationToken);
        var expectedCode = ev.EvidenceCodeEnabled ? await ActiveCode(command.EventId, now, cancellationToken) : null;
        if (ev.EvidenceCodeEnabled && expectedCode is null) throw new InvalidOperationException("Evidence codes are enabled, but no code is active at the current time. Ask an administrator to activate one before submitting.");
        var submission = new Submission(Guid.NewGuid(), command.EventId, command.TeamId, command.BoardTileId, command.RequirementId, command.DropSnapshotId, command.CreditedParticipantId, command.ActorAccountId, creditedWeight, now, command.CaptainNote, expectedCode, command.RequestPublicPrivacy);
        StoredEvidence? stored = null;
        try
        {
            stored = await storage.StoreAsync(command.EventId, submission.Id, command.OriginalFilename, command.Evidence, cancellationToken);
            db.Submissions.Add(submission); db.EvidenceAssets.Add(Asset(submission.Id, command.ActorAccountId, stored, EvidenceAssetRole.OriginalEvidence, now));
            db.ReviewActions.Add(Action(submission.Id, ReviewActionType.Submitted, command.ActorAccountId, now, command.CaptainNote, null, Snapshot(submission)));
            await db.SaveChangesAsync(cancellationToken); await Notify(submission.EventId, cancellationToken); return new SubmissionResult(submission.Id, submission.Status);
        }
        catch { if (stored is not null) await storage.DeleteAsync(stored.StorageKey, cancellationToken); throw; }
    }

    public async Task<SubmissionResult> CorrectAsync(CorrectSubmissionCommand command, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow(); var submission = await db.Submissions.SingleOrDefaultAsync(x => x.Id == command.SubmissionId, cancellationToken) ?? throw new InvalidOperationException("Submission not found.");
        var actor = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.ActorAccountId, cancellationToken) ?? throw new InvalidOperationException("Account not found.");
        var access = await db.AccountEventAccesses.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actor.Id, cancellationToken);
        var ev = await db.Events.AsNoTracking().SingleAsync(x => x.Id == submission.EventId, cancellationToken);
        if (actor.AccountType != AccountType.EmergencyCaptain || access?.EventId != submission.EventId || access.TeamId != submission.TeamId || access.GetAccessMode(now) == AccountAccessMode.Disabled) throw new InvalidOperationException("This captain account cannot correct that submission.");
        if (!ev.AcceptsEmergencySubmissions(now)) throw new InvalidOperationException("Submissions are not currently open.");
        var creditedWeight = await ValidateTarget(submission.EventId, submission.TeamId, command.BoardTileId, command.RequirementId, command.DropSnapshotId, command.CreditedParticipantId, cancellationToken);
        var before = Snapshot(submission); StoredEvidence? stored = null;
        try
        {
            if (command.ReplacementEvidence is not null && command.ReplacementFilename is not null) stored = await storage.StoreAsync(submission.EventId, submission.Id, command.ReplacementFilename, command.ReplacementEvidence, cancellationToken);
            submission.EditPending(command.BoardTileId, command.RequirementId, command.DropSnapshotId, command.CreditedParticipantId, creditedWeight, command.CaptainNote, command.RequestPublicPrivacy);
            if (stored is not null)
            {
                foreach (var asset in await db.EvidenceAssets.Where(x => x.SubmissionId == submission.Id && x.Active).ToListAsync(cancellationToken)) asset.Deactivate();
                db.EvidenceAssets.Add(Asset(submission.Id, command.ActorAccountId, stored, EvidenceAssetRole.ReplacementEvidence, now));
                db.ReviewActions.Add(Action(submission.Id, ReviewActionType.ReplaceEvidence, command.ActorAccountId, now, null, before, Snapshot(submission)));
            }
            if (submission.Status == SubmissionStatus.ChangesRequested) { submission.Resubmit(); db.ReviewActions.Add(Action(submission.Id, ReviewActionType.Resubmit, command.ActorAccountId, now, null, before, Snapshot(submission))); }
            else db.ReviewActions.Add(Action(submission.Id, ReviewActionType.EditMetadata, command.ActorAccountId, now, null, before, Snapshot(submission)));
            await db.SaveChangesAsync(cancellationToken); await Notify(submission.EventId, cancellationToken); return new SubmissionResult(submission.Id, submission.Status);
        }
        catch { if (stored is not null) await storage.DeleteAsync(stored.StorageKey, cancellationToken); throw; }
    }

    public async Task WithdrawAsync(Guid submissionId, Guid actorAccountId, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow(); var s = await db.Submissions.SingleAsync(x => x.Id == submissionId, cancellationToken); var a = await db.Accounts.AsNoTracking().SingleAsync(x => x.Id == actorAccountId, cancellationToken); var access = await db.AccountEventAccesses.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorAccountId, cancellationToken); var ev = await db.Events.AsNoTracking().SingleAsync(x => x.Id == s.EventId, cancellationToken); if (a.AccountType != AccountType.EmergencyCaptain || access?.EventId != s.EventId || access.TeamId != s.TeamId || access.GetAccessMode(now) == AccountAccessMode.Disabled) throw new InvalidOperationException("This captain cannot withdraw that submission."); if (!ev.AcceptsEmergencySubmissions(now)) throw new InvalidOperationException("Submissions are not currently open."); var before = Snapshot(s); s.Withdraw(now); db.ReviewActions.Add(Action(s.Id, ReviewActionType.Withdraw, actorAccountId, now, null, before, Snapshot(s))); await db.SaveChangesAsync(cancellationToken); await Notify(s.EventId, cancellationToken);
    }

    public Task RequestChangesAsync(Guid submissionId, Guid adminAccountId, string note, CancellationToken cancellationToken = default) => ReviewSimple(submissionId, adminAccountId, ReviewActionType.RequestChanges, note, (s, n, at) => s.RequestChanges(n!, at), cancellationToken);
    public async Task RejectAsync(Guid submissionId, Guid adminAccountId, string note, Guid? duplicateOfSubmissionId = null, CancellationToken cancellationToken = default)
    {
        await EnsureAdmin(adminAccountId, cancellationToken); var s = await db.Submissions.SingleAsync(x => x.Id == submissionId, cancellationToken); if (duplicateOfSubmissionId is not null && !await db.Submissions.AnyAsync(x => x.Id == duplicateOfSubmissionId && x.EventId == s.EventId, cancellationToken)) throw new InvalidOperationException("The original submission was not found in this event."); var now = time.GetUtcNow(); var before = Snapshot(s); s.Reject(note, now, duplicateOfSubmissionId); db.ReviewActions.Add(Action(s.Id, duplicateOfSubmissionId is null ? ReviewActionType.Reject : ReviewActionType.MarkDuplicate, adminAccountId, now, note, before, Snapshot(s))); await db.SaveChangesAsync(cancellationToken); await Notify(s.EventId, cancellationToken);
    }

    public async Task<int> ApproveAsync(Guid submissionId, Guid adminAccountId, CancellationToken cancellationToken = default)
    {
        await EnsureAdmin(adminAccountId, cancellationToken); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var s = await db.Submissions.FromSqlInterpolated($"SELECT * FROM submissions WHERE id = {submissionId} FOR UPDATE").SingleAsync(cancellationToken); if (s.Status != SubmissionStatus.Pending) throw new InvalidOperationException("Only a pending submission can be approved.");
        var requirement = await db.BoardRequirementSnapshots.AsNoTracking().SingleAsync(x => x.Id == s.RequirementId, cancellationToken);
        var used = await db.SubmissionContributions.Where(x => x.TeamId == s.TeamId && x.RequirementId == s.RequirementId && x.ReversedAt == null).SumAsync(x => (int?)x.Amount, cancellationToken) ?? 0;
        var remaining = Math.Max(0, requirement.TargetContribution - used); var allowed = s.ClaimedWeight;
        if (s.DropSnapshotId is not null)
        {
            var drop = await db.BoardRequirementDropSnapshots.AsNoTracking().SingleAsync(x => x.Id == s.DropSnapshotId && x.RequirementId == s.RequirementId, cancellationToken);
            var dropUsed = await db.SubmissionContributions.Where(x => x.TeamId == s.TeamId && x.RequirementId == s.RequirementId && x.DropSnapshotId == s.DropSnapshotId && x.ReversedAt == null).SumAsync(x => (int?)x.Amount, cancellationToken) ?? 0;
            var maximum = drop.MaximumContribution ?? (requirement.DuplicatesAllowed ? int.MaxValue : 1); allowed = Math.Min(allowed, Math.Max(0, maximum - dropUsed));
        }
        var amount = Math.Min(remaining, allowed); if (amount < 1) throw new InvalidOperationException("This requirement has no remaining eligible contribution. Mark the submission as a duplicate or reject it.");
        var now = time.GetUtcNow(); var before = Snapshot(s); s.Approve(amount, now); if (s.PublicPrivacyRequested) s.SetPublicEvidenceHidden(true); db.SubmissionContributions.Add(new SubmissionContribution(Guid.NewGuid(), s.Id, s.TeamId, s.RequirementId, s.DropSnapshotId, s.CreditedParticipantId, amount, now)); db.ReviewActions.Add(Action(s.Id, ReviewActionType.Approve, adminAccountId, now, $"Approved contribution: {amount}", before, Snapshot(s))); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); await Notify(s.EventId, cancellationToken); return amount;
    }

    public async Task ReverseAsync(Guid submissionId, Guid adminAccountId, string reason, CancellationToken cancellationToken = default)
    {
        await EnsureAdmin(adminAccountId, cancellationToken); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var s = await db.Submissions.FromSqlInterpolated($"SELECT * FROM submissions WHERE id = {submissionId} FOR UPDATE").SingleAsync(cancellationToken); var contribution = await db.SubmissionContributions.SingleAsync(x => x.SubmissionId == submissionId && x.ReversedAt == null, cancellationToken); var now = time.GetUtcNow(); var before = Snapshot(s); s.Reverse(reason, now); contribution.Reverse(now); db.ReviewActions.Add(Action(s.Id, ReviewActionType.ReverseApproval, adminAccountId, now, reason, before, Snapshot(s))); await RebalanceLaterContributions(s, contribution, adminAccountId, now, cancellationToken); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); await Notify(s.EventId, cancellationToken);
    }

    public async Task SetVisibilityAsync(Guid submissionId, Guid adminAccountId, bool hidden, CancellationToken cancellationToken = default)
    { await EnsureAdmin(adminAccountId, cancellationToken); var s = await db.Submissions.SingleAsync(x => x.Id == submissionId, cancellationToken); var now = time.GetUtcNow(); var before = Snapshot(s); s.SetPublicEvidenceHidden(hidden); db.ReviewActions.Add(Action(s.Id, hidden ? ReviewActionType.HidePublicEvidence : ReviewActionType.ShowPublicEvidence, adminAccountId, now, null, before, Snapshot(s))); await db.SaveChangesAsync(cancellationToken); await Notify(s.EventId, cancellationToken); }

    public async Task EditMetadataAsync(EditSubmissionMetadataCommand command, CancellationToken cancellationToken = default)
    { await EnsureAdmin(command.AdminAccountId, cancellationToken); var s = await db.Submissions.SingleAsync(x => x.Id == command.SubmissionId, cancellationToken); var creditedWeight = await ValidateTarget(s.EventId, s.TeamId, command.BoardTileId, command.RequirementId, command.DropSnapshotId, command.CreditedParticipantId, cancellationToken); var before = Snapshot(s); s.EditPending(command.BoardTileId, command.RequirementId, command.DropSnapshotId, command.CreditedParticipantId, creditedWeight, s.CaptainNote); db.ReviewActions.Add(Action(s.Id, ReviewActionType.EditMetadata, command.AdminAccountId, time.GetUtcNow(), command.Note, before, Snapshot(s))); await db.SaveChangesAsync(cancellationToken); }

    private async Task RebalanceLaterContributions(Submission reversed, SubmissionContribution reversedContribution, Guid adminAccountId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var requirement = await db.BoardRequirementSnapshots.AsNoTracking().SingleAsync(x => x.Id == reversed.RequirementId, cancellationToken);
        var active = (await db.SubmissionContributions.FromSqlInterpolated($"SELECT * FROM submission_contributions WHERE team_id = {reversed.TeamId} AND requirement_id = {reversed.RequirementId} AND reversed_at IS NULL FOR UPDATE").OrderBy(x => x.AppliedAt).ToListAsync(cancellationToken)).Where(x => x.Id != reversedContribution.Id).ToList();
        var remaining = Math.Max(0, requirement.TargetContribution - active.Sum(x => x.Amount)); if (remaining == 0) return;
        var later = active.Where(x => x.AppliedAt >= reversedContribution.AppliedAt).ToList(); if (later.Count == 0) return;
        var submissionIds = later.Select(x => x.SubmissionId).ToList(); var submissions = await db.Submissions.Where(x => submissionIds.Contains(x.Id) && x.Status == SubmissionStatus.Approved).ToDictionaryAsync(x => x.Id, cancellationToken);
        var dropIds = later.Where(x => x.DropSnapshotId is not null).Select(x => x.DropSnapshotId!.Value).Distinct().ToList(); var drops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => dropIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var usedByDrop = active.Where(x => x.DropSnapshotId is not null).GroupBy(x => x.DropSnapshotId!.Value).ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        foreach (var item in later)
        {
            if (remaining == 0 || !submissions.TryGetValue(item.SubmissionId, out var submission)) break; var headroom = Math.Max(0, submission.ClaimedWeight - item.Amount); if (headroom == 0) continue;
            if (item.DropSnapshotId is Guid dropId && drops.TryGetValue(dropId, out var drop)) { var maximum = drop.MaximumContribution ?? (requirement.DuplicatesAllowed ? int.MaxValue : 1); headroom = Math.Min(headroom, Math.Max(0, maximum - usedByDrop.GetValueOrDefault(dropId))); }
            var increase = Math.Min(remaining, headroom); if (increase == 0) continue; var oldAmount = item.Amount; var oldSnapshot = Snapshot(submission); item.IncreaseAmount(oldAmount + increase); submission.IncreaseApprovedContribution(oldAmount + increase); if (item.DropSnapshotId is Guid usedDrop) usedByDrop[usedDrop] = usedByDrop.GetValueOrDefault(usedDrop) + increase; remaining -= increase; db.ReviewActions.Add(Action(submission.Id, ReviewActionType.RebalanceContribution, adminAccountId, now, $"Contribution adjusted from {oldAmount} to {item.Amount} after reversal of {reversed.Id}.", oldSnapshot, Snapshot(submission)));
        }
    }

    private async Task ReviewSimple(Guid id, Guid admin, ReviewActionType type, string? note, Action<Submission, string?, DateTimeOffset> change, CancellationToken cancellationToken) { await EnsureAdmin(admin, cancellationToken); var s = await db.Submissions.SingleAsync(x => x.Id == id, cancellationToken); var now = time.GetUtcNow(); var before = Snapshot(s); change(s, note, now); db.ReviewActions.Add(Action(s.Id, type, admin, now, note, before, Snapshot(s))); await db.SaveChangesAsync(cancellationToken); await Notify(s.EventId, cancellationToken); }
    private async Task<int> ValidateTarget(Guid eventId, Guid teamId, Guid tileId, Guid requirementId, Guid? dropId, Guid participantId, CancellationToken cancellationToken)
    {
        if (!await db.Teams.AnyAsync(x => x.Id == teamId && x.EventId == eventId && x.Active, cancellationToken)) throw new InvalidOperationException("Team not found."); if (!await db.TeamMemberships.AnyAsync(x => x.TeamId == teamId && x.EventParticipantId == participantId && x.LeftAt == null, cancellationToken)) throw new InvalidOperationException("The credited player does not belong to this team.");
        var tile = await (from t in db.BoardTiles join b in db.Boards on t.BoardId equals b.Id where t.Id == tileId && b.EventId == eventId && b.State == BoardState.Published select t).SingleOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("Choose a tile from the published event board.");
        var requirement = await db.BoardRequirementSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requirementId && x.BoardTileId == tile.Id, cancellationToken) ?? throw new InvalidOperationException("Choose a requirement from that tile.");
        var creditedWeight = 1; if (requirement.ManualObjective && dropId is not null) throw new InvalidOperationException("Manual objectives do not use a drop."); if (!requirement.ManualObjective && dropId is null) throw new InvalidOperationException("Choose an eligible drop."); if (!requirement.ManualObjective && dropId is Guid selectedDropId) { var drop = await db.BoardRequirementDropSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == selectedDropId && x.RequirementId == requirementId, cancellationToken) ?? throw new InvalidOperationException("Choose an eligible drop."); creditedWeight = drop.CreditedWeight; var maximum = drop.MaximumContribution ?? (requirement.DuplicatesAllowed ? int.MaxValue : 1); var dropApproved = await db.SubmissionContributions.Where(x => x.TeamId == teamId && x.RequirementId == requirementId && x.DropSnapshotId == selectedDropId && x.ReversedAt == null).SumAsync(x => (int?)x.Amount, cancellationToken) ?? 0; if (dropApproved >= maximum) throw new InvalidOperationException("This drop has already reached its approved contribution limit."); }
        var approved = await db.SubmissionContributions.Where(x => x.TeamId == teamId && x.RequirementId == requirementId && x.ReversedAt == null).SumAsync(x => (int?)x.Amount, cancellationToken) ?? 0; if (approved >= requirement.TargetContribution) throw new InvalidOperationException("This requirement is already complete."); return creditedWeight;
    }
    private async Task EnsureAdmin(Guid id, CancellationToken cancellationToken) { if (!await db.Accounts.AnyAsync(x => x.Id == id && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin) && x.Active, cancellationToken)) throw new InvalidOperationException("Administrator access is required."); }
    private async Task Notify(Guid eventId, CancellationToken cancellationToken) { if (progressNotifier is null) return; try { await progressNotifier.NotifyProgressChangedAsync(eventId, cancellationToken); } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; } catch { /* The saved review must remain successful if a live client disconnects. */ } }
    private async Task<string?> ActiveCode(Guid eventId, DateTimeOffset at, CancellationToken cancellationToken) => await db.EvidenceCodes.AsNoTracking().Where(x => x.EventId == eventId && x.ActivatesAt <= at && (x.RetiresAt == null || x.RetiresAt > at)).OrderByDescending(x => x.ActivatesAt).Select(x => x.Code).FirstOrDefaultAsync(cancellationToken);
    private static EvidenceAsset Asset(Guid submissionId, Guid actor, StoredEvidence stored, EvidenceAssetRole role, DateTimeOffset now) => new(Guid.NewGuid(), submissionId, stored.StorageKey, stored.OriginalFilename, stored.MediaType, stored.ByteSize, stored.Width, stored.Height, stored.Checksum, now, actor, role);
    private static ReviewAction Action(Guid submissionId, ReviewActionType type, Guid actor, DateTimeOffset now, string? note, string? before, string? after) => new(Guid.NewGuid(), submissionId, type, actor, now, note, before, after);
    private static string Snapshot(Submission s) => JsonSerializer.Serialize(new { s.BoardTileId, s.RequirementId, s.DropSnapshotId, s.CreditedParticipantId, s.ClaimedWeight, s.ApprovedContribution, s.Status, s.PublicPrivacyRequested, s.PublicEvidenceHidden, s.PublicPlayerHidden, s.CurrentReviewerNote, s.DuplicateOfSubmissionId });
}
