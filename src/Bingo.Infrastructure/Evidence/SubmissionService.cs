using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Bingo.Application.Boards;
using Bingo.Application.Evidence;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Evidence;

public sealed class SubmissionService(
    ApplicationDbContext db,
    IEvidenceStorage storage,
    TimeProvider time,
    IProgressNotifier? progressNotifier = null,
    ITeamFocusService? focus = null,
    ITeamFocusNotifier? focusNotifier = null,
    IEvidenceAuthority? authority = null) : ISubmissionService
{
    public async Task<SubmissionResult> CreateAsync(CreateSubmissionCommand command, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow(); var actor = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.ActorAccountId, cancellationToken) ?? throw new InvalidOperationException("Account not found.");
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.EventId, cancellationToken) ?? throw new InvalidOperationException("Event not found.");
        var actorScope = await Authority.AuthorizeAsync(command.ActorAccountId, command.EventId, command.TeamId, command.CreditedParticipantId, now, cancellationToken);
        var isAdmin = actorScope.Kind == EvidenceActorKind.Administrator;
        if (isAdmin) throw new InvalidOperationException("Administrators cannot create evidence submissions.");
        await using var administrativeTransaction = isAdmin ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        if (isAdmin) ev = await LockedAdminReviewEventAsync(command.EventId, cancellationToken);
        if (!isAdmin)
        {
            EnsureMutationWindow(ev, actorScope.Kind, now, "New submissions are not currently open.");
        }
        var creditedWeight = await ValidateTarget(command.EventId, command.TeamId, command.BoardTileId, command.RequirementId, command.DropSnapshotId, command.CreditedParticipantId, cancellationToken);
        var creditedCharacter = await Authority.ResolveCreditedCharacterAsync(command.EventId, command.CreditedParticipantId, now, cancellationToken);
        var expectedCode = ev.EvidenceCodeEnabled ? await ActiveCode(command.EventId, now, cancellationToken) : null;
        if (ev.EvidenceCodeEnabled && expectedCode is null) throw new InvalidOperationException("Evidence codes are enabled, but no code is active at the current time. Ask an administrator to activate one before submitting.");
        var submission = new Submission(Guid.NewGuid(), command.EventId, command.TeamId, command.BoardTileId, command.RequirementId, command.DropSnapshotId, command.CreditedParticipantId, creditedCharacter.OsrsCharacterId, creditedCharacter.Name, command.ActorAccountId, creditedWeight, now, command.CaptainNote, expectedCode);
        StoredEvidence? stored = null;
        var committed = false;
        try
        {
            stored = await storage.StoreAsync(command.EventId, submission.Id, command.OriginalFilename, command.Evidence, cancellationToken);
            db.Submissions.Add(submission); db.EvidenceAssets.Add(Asset(submission.Id, command.ActorAccountId, stored, EvidenceAssetRole.OriginalEvidence, now));
            db.ReviewActions.Add(Action(submission.Id, ReviewActionType.Submitted, command.ActorAccountId, now, command.CaptainNote, null, Snapshot(submission)));
            await db.SaveChangesAsync(cancellationToken);
            if (administrativeTransaction is not null) await administrativeTransaction.CommitAsync(CancellationToken.None);
            committed = true;
        }
        catch { if (!committed && stored is not null) await storage.DeleteAsync(stored.StorageKey, CancellationToken.None); throw; }
        return new SubmissionResult(submission.Id, submission.Status);
    }

    public async Task<SubmissionResult> CorrectAsync(CorrectSubmissionCommand command, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow(); var submission = await db.Submissions.SingleOrDefaultAsync(x => x.Id == command.SubmissionId, cancellationToken) ?? throw new InvalidOperationException("Submission not found.");
        var ev = await db.Events.AsNoTracking().SingleAsync(x => x.Id == submission.EventId, cancellationToken);
        var actorScope = await Authority.AuthorizeAsync(command.ActorAccountId, submission.EventId, submission.TeamId, submission.CreditedParticipantId, now, cancellationToken);
        if (actorScope.Kind is EvidenceActorKind.Administrator) throw new InvalidOperationException("Administrators use the review correction path for this submission.");
        EnsureMutationWindow(ev, actorScope.Kind, now, "Submissions are not currently open.");
        EnsureExpectedVersion(submission, command.ExpectedVersion);
        _ = await ValidateTarget(submission.EventId, submission.TeamId, command.BoardTileId, command.RequirementId, command.DropSnapshotId, command.CreditedParticipantId, cancellationToken);
        var before = Snapshot(submission);
        try
        {
            submission.EditPending(command.BoardTileId, command.RequirementId, command.DropSnapshotId, command.CreditedParticipantId, submission.ClaimedWeight, command.CaptainNote);
            db.ReviewActions.Add(Action(submission.Id, ReviewActionType.EditMetadata, command.ActorAccountId, now, null, before, Snapshot(submission)));
            await db.SaveChangesAsync(cancellationToken); return new SubmissionResult(submission.Id, submission.Status);
        }
        catch { throw; }
    }

    public async Task WithdrawAsync(Guid submissionId, Guid actorAccountId, CancellationToken cancellationToken = default, int? expectedVersion = null)
    {
        var now = time.GetUtcNow(); var s = await db.Submissions.SingleAsync(x => x.Id == submissionId, cancellationToken); var ev = await db.Events.AsNoTracking().SingleAsync(x => x.Id == s.EventId, cancellationToken); var actorScope = await Authority.AuthorizeAsync(actorAccountId, s.EventId, s.TeamId, s.CreditedParticipantId, now, cancellationToken); if (actorScope.Kind is EvidenceActorKind.Administrator) throw new InvalidOperationException("Administrators cannot withdraw submissions through the captain path."); EnsureMutationWindow(ev, actorScope.Kind, now, "Submissions are not currently open."); EnsureExpectedVersion(s, expectedVersion); var before = Snapshot(s); s.Withdraw(now); db.ReviewActions.Add(Action(s.Id, ReviewActionType.Withdraw, actorAccountId, now, null, before, Snapshot(s))); await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SubmissionResult> ResubmitAsync(ResubmitSubmissionCommand command, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var predecessor = await db.Submissions.FromSqlInterpolated($"SELECT * FROM submissions WHERE id = {command.PredecessorSubmissionId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Submission not found.");
        var ev = await db.Events.AsNoTracking().SingleAsync(x => x.Id == predecessor.EventId, cancellationToken);
        var scope = await Authority.AuthorizeAsync(command.ActorAccountId, predecessor.EventId, predecessor.TeamId, predecessor.CreditedParticipantId, now, cancellationToken);
        if (scope.Kind == EvidenceActorKind.Administrator) throw new InvalidOperationException("Administrators use the review correction path for this submission.");
        EnsureMutationWindow(ev, scope.Kind, now, "Resubmissions are not currently open.");
        EnsureExpectedVersion(predecessor, command.ExpectedVersion);
        predecessor.EnsureRejectedResubmissionSource();
        if (await db.Submissions.AnyAsync(x => x.ResubmissionOfSubmissionId == predecessor.Id, cancellationToken))
            throw new InvalidOperationException("This rejected submission already has a linked resubmission.");
        var creditedWeight = await ValidateTarget(predecessor.EventId, predecessor.TeamId, command.BoardTileId, command.RequirementId, command.DropSnapshotId, predecessor.CreditedParticipantId, cancellationToken);
        var expectedCode = ev.EvidenceCodeEnabled ? await ActiveCode(predecessor.EventId, now, cancellationToken) : null;
        if (ev.EvidenceCodeEnabled && expectedCode is null) throw new InvalidOperationException("Evidence codes are enabled, but no code is active at the current time. Ask an administrator to activate one before submitting.");
        var child = new Submission(Guid.NewGuid(), predecessor.EventId, predecessor.TeamId, command.BoardTileId, command.RequirementId, command.DropSnapshotId,
            predecessor.CreditedParticipantId, predecessor.CreditedOsrsCharacterId, predecessor.CreditedCharacterName, command.ActorAccountId,
            creditedWeight, now, command.CaptainNote, expectedCode, predecessor.Id);
        StoredEvidence? stored = null;
        var committed = false;
        try
        {
            stored = await storage.StoreAsync(child.EventId, child.Id, command.OriginalFilename, command.Evidence, cancellationToken);
            db.Submissions.Add(child);
            db.EvidenceAssets.Add(Asset(child.Id, command.ActorAccountId, stored, EvidenceAssetRole.OriginalEvidence, now));
            db.ReviewActions.Add(Action(child.Id, ReviewActionType.Resubmit, command.ActorAccountId, now, command.CaptainNote, Snapshot(predecessor), Snapshot(child)));
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(CancellationToken.None);
            committed = true;
        }
        catch
        {
            if (!committed && stored is not null) await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
        return new SubmissionResult(child.Id, child.Status);
    }

    public async Task RejectAsync(Guid submissionId, Guid adminAccountId, string reason, CancellationToken cancellationToken = default, int? expectedVersion = null)
    {
        await EnsureAdmin(adminAccountId, cancellationToken); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var s = await LockedAdminReviewSubmissionAsync(submissionId, cancellationToken); EnsureExpectedVersion(s, expectedVersion); var now = time.GetUtcNow(); var before = Snapshot(s); s.Reject(reason, now); db.ReviewActions.Add(Action(s.Id, ReviewActionType.Reject, adminAccountId, now, reason, before, Snapshot(s))); await AddRejectionNotificationsAsync(s, reason, now, cancellationToken); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
    }

    public async Task<int> ApproveAsync(Guid submissionId, Guid adminAccountId, CancellationToken cancellationToken = default, int? expectedVersion = null)
    {
        await EnsureAdmin(adminAccountId, cancellationToken); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var s = await LockedAdminReviewSubmissionAsync(submissionId, cancellationToken); EnsureExpectedVersion(s, expectedVersion); if (s.Status != SubmissionStatus.Pending) throw new InvalidOperationException("Only a pending submission can be approved.");
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
        var now = time.GetUtcNow(); var before = Snapshot(s); s.Approve(amount, now); db.SubmissionContributions.Add(new SubmissionContribution(Guid.NewGuid(), s.Id, s.TeamId, s.RequirementId, s.DropSnapshotId, s.CreditedParticipantId, amount, now)); db.ReviewActions.Add(Action(s.Id, ReviewActionType.Approve, adminAccountId, now, $"Approved contribution: {amount}", before, Snapshot(s))); await db.SaveChangesAsync(cancellationToken); var focusCleared = focus is not null && await focus.ClearCompletedTileFocusAsync(s.EventId, s.TeamId, s.BoardTileId, cancellationToken); if (focusCleared) await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); await Notify(s.EventId, cancellationToken); await NotifyFocus(s.EventId, s.TeamId, focusCleared, cancellationToken); return amount;
    }

    public async Task ReverseAsync(Guid submissionId, Guid adminAccountId, string reason, CancellationToken cancellationToken = default, int? expectedVersion = null)
    {
        await EnsureAdmin(adminAccountId, cancellationToken); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var s = await LockedAdminReviewSubmissionAsync(submissionId, cancellationToken); EnsureExpectedVersion(s, expectedVersion); var contribution = await db.SubmissionContributions.SingleAsync(x => x.SubmissionId == submissionId && x.ReversedAt == null, cancellationToken); var now = time.GetUtcNow(); var before = Snapshot(s); s.Reverse(reason, now); contribution.Reverse(now); db.ReviewActions.Add(Action(s.Id, ReviewActionType.ReverseApproval, adminAccountId, now, reason, before, Snapshot(s))); await RebalanceLaterContributions(s, contribution, adminAccountId, now, cancellationToken); await db.SaveChangesAsync(cancellationToken); var focusCleared = focus is not null && await focus.ClearCompletedTileFocusAsync(s.EventId, s.TeamId, s.BoardTileId, cancellationToken); if (focusCleared) await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); await Notify(s.EventId, cancellationToken); await NotifyFocus(s.EventId, s.TeamId, focusCleared, cancellationToken);
    }

    public async Task EditMetadataAsync(EditSubmissionMetadataCommand command, CancellationToken cancellationToken = default)
    {
        await EnsureAdmin(command.AdminAccountId, cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var s = await LockedAdminReviewSubmissionAsync(command.SubmissionId, cancellationToken);
        EnsureExpectedVersion(s, command.ExpectedVersion);
        if (string.IsNullOrWhiteSpace(command.Reason)) throw new InvalidOperationException("A written correction reason is required.");
        var selected = await (from assignment in db.EventParticipantCharacters.AsNoTracking()
                              join participant in db.EventParticipants.AsNoTracking() on assignment.EventParticipantId equals participant.Id
                              join character in db.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                              where assignment.EventId == s.EventId && assignment.OsrsCharacterId == command.CreditedOsrsCharacterId &&
                                    assignment.EventRole == Bingo.Domain.Signups.EventCharacterRole.Playing && assignment.ReleasedAt == null &&
                                    participant.EventId == s.EventId
                              select new { ParticipantId = participant.Id, CharacterId = character.Id, CharacterName = character.DisplayName }).ToListAsync(cancellationToken);
        if (selected.Count != 1) throw new InvalidOperationException("The selected playing character is missing or ambiguous for this event.");
        var credited = selected[0];
        if (!await db.TeamMemberships.AnyAsync(x => x.TeamId == s.TeamId && x.EventParticipantId == credited.ParticipantId && x.LeftAt == null, cancellationToken))
            throw new InvalidOperationException("The selected playing character does not belong to this submission's team.");
        _ = await ValidateTarget(s.EventId, s.TeamId, command.BoardTileId, command.RequirementId, command.DropSnapshotId, credited.ParticipantId, cancellationToken);
        var before = Snapshot(s);
        s.EditPending(command.BoardTileId, command.RequirementId, command.DropSnapshotId, s.CreditedParticipantId, s.ClaimedWeight, s.CaptainNote);
        s.CorrectCreditedAttribution(credited.ParticipantId, credited.CharacterId, credited.CharacterName);
        db.ReviewActions.Add(Action(s.Id, ReviewActionType.EditMetadata, command.AdminAccountId, time.GetUtcNow(), command.Reason, before, Snapshot(s)));
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

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

    private async Task<BingoEvent> LockedAdminReviewEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var item = await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Event not found.");
        EnsureEvidenceReviewCapability(item);
        return item;
    }

    private async Task<Submission> LockedAdminReviewSubmissionAsync(Guid submissionId, CancellationToken cancellationToken)
    {
        var submission = await db.Submissions.FromSqlInterpolated($"SELECT * FROM submissions WHERE id = {submissionId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Submission not found.");
        await LockedAdminReviewEventAsync(submission.EventId, cancellationToken);
        return submission;
    }

    private static void EnsureEvidenceReviewCapability(BingoEvent item)
    {
        if (!EventStatePolicy.Allows(item.State, EventCapability.ReviewEvidence))
            throw new InvalidOperationException("Evidence can only be reviewed while the event is Live or awaiting final review.");
    }
    private async Task<int> ValidateTarget(Guid eventId, Guid teamId, Guid tileId, Guid requirementId, Guid? dropId, Guid participantId, CancellationToken cancellationToken)
    {
        if (!await db.Teams.AnyAsync(x => x.Id == teamId && x.EventId == eventId && x.Active, cancellationToken)) throw new InvalidOperationException("Team not found."); if (!await db.TeamMemberships.AnyAsync(x => x.TeamId == teamId && x.EventParticipantId == participantId && x.LeftAt == null, cancellationToken)) throw new InvalidOperationException("The credited player does not belong to this team.");
        var tile = await (from t in db.BoardTiles join b in db.Boards on t.BoardId equals b.Id where t.Id == tileId && b.EventId == eventId && b.State == BoardState.Published select t).SingleOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("Choose a tile from the published event board.");
        var requirement = await db.BoardRequirementSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requirementId && x.BoardTileId == tile.Id, cancellationToken) ?? throw new InvalidOperationException("Choose a requirement from that tile.");
        var creditedWeight = 1; if (requirement.ManualObjective && dropId is not null) throw new InvalidOperationException("Manual objectives do not use a drop."); if (!requirement.ManualObjective && dropId is null) throw new InvalidOperationException("Choose an eligible drop."); if (!requirement.ManualObjective && dropId is Guid selectedDropId) { var drop = await db.BoardRequirementDropSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.Id == selectedDropId && x.RequirementId == requirementId, cancellationToken) ?? throw new InvalidOperationException("Choose an eligible drop."); creditedWeight = drop.CreditedWeight; var maximum = drop.MaximumContribution ?? (requirement.DuplicatesAllowed ? int.MaxValue : 1); var dropApproved = await db.SubmissionContributions.Where(x => x.TeamId == teamId && x.RequirementId == requirementId && x.DropSnapshotId == selectedDropId && x.ReversedAt == null).SumAsync(x => (int?)x.Amount, cancellationToken) ?? 0; if (dropApproved >= maximum) throw new InvalidOperationException("This drop has already reached its approved contribution limit."); }
        var approved = await db.SubmissionContributions.Where(x => x.TeamId == teamId && x.RequirementId == requirementId && x.ReversedAt == null).SumAsync(x => (int?)x.Amount, cancellationToken) ?? 0; if (approved >= requirement.TargetContribution) throw new InvalidOperationException("This requirement is already complete."); return creditedWeight;
    }
    private async Task EnsureAdmin(Guid id, CancellationToken cancellationToken) { if (!await db.Accounts.AnyAsync(x => x.Id == id && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin) && x.Active, cancellationToken)) throw new InvalidOperationException("Administrator access is required."); }
    private IEvidenceAuthority Authority => authority ??= new EvidenceAuthority(db);
    private static void EnsureExpectedVersion(Submission submission, int? expectedVersion)
    { if (expectedVersion is not null && submission.Version != expectedVersion.Value) throw new InvalidOperationException("This evidence changed in another request. Reload it and review the latest version before saving."); }
    private static void EnsureMutationWindow(BingoEvent ev, EvidenceActorKind kind, DateTimeOffset now, string message)
    {
        var open = kind == EvidenceActorKind.EmergencyCaptain ? ev.AcceptsEmergencySubmissions(now) : ev.AcceptsNewSubmissions(now);
        if (!open) throw new InvalidOperationException(message);
    }
    private async Task Notify(Guid eventId, CancellationToken cancellationToken) { if (progressNotifier is null) return; try { await progressNotifier.NotifyProgressChangedAsync(eventId, cancellationToken); } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; } catch { /* The saved review must remain successful if a live client disconnects. */ } }
    private async Task NotifyFocus(Guid eventId, Guid teamId, bool changed, CancellationToken cancellationToken) { if (!changed || focusNotifier is null) return; try { await focusNotifier.NotifyTeamFocusChangedAsync(eventId, teamId, cancellationToken); } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; } catch { /* The saved review must remain successful if a live client disconnects. */ } }
    private async Task<string?> ActiveCode(Guid eventId, DateTimeOffset at, CancellationToken cancellationToken) => await db.EvidenceCodes.AsNoTracking().Where(x => x.EventId == eventId && x.ActivatesAt <= at && (x.RetiresAt == null || x.RetiresAt > at)).OrderByDescending(x => x.ActivatesAt).Select(x => x.Code).FirstOrDefaultAsync(cancellationToken);
    private static EvidenceAsset Asset(Guid submissionId, Guid actor, StoredEvidence stored, EvidenceAssetRole role, DateTimeOffset now) => new(Guid.NewGuid(), submissionId, stored.StorageKey, stored.OriginalFilename, stored.MediaType, stored.ByteSize, stored.Width, stored.Height, stored.Checksum, now, actor, role);
    private static ReviewAction Action(Guid submissionId, ReviewActionType type, Guid actor, DateTimeOffset now, string? note, string? before, string? after) => new(Guid.NewGuid(), submissionId, type, actor, now, note, before, after);
    private async Task AddRejectionNotificationsAsync(Submission submission, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var context = await (from eventItem in db.Events.AsNoTracking()
                             join tile in db.BoardTiles.AsNoTracking() on submission.BoardTileId equals tile.Id
                             where eventItem.Id == submission.EventId
                             select new { EventName = eventItem.Name, TileName = tile.NameSnapshot }).SingleAsync(cancellationToken);
        var dropName = submission.DropSnapshotId is Guid dropId
            ? await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => x.Id == dropId).Select(x => x.ItemName).SingleOrDefaultAsync(cancellationToken)
            : null;
        var detail = JsonSerializer.Serialize(new { eventName = context.EventName, tile = context.TileName, drop = dropName, reason = reason.Trim() });
        var recipients = await (from membership in db.TeamMemberships.AsNoTracking()
                                join participant in db.EventParticipants.AsNoTracking() on membership.EventParticipantId equals participant.Id
                                where membership.TeamId == submission.TeamId && membership.LeftAt == null &&
                                      (membership.Role == Bingo.Domain.Teams.TeamMembershipRole.Captain || membership.Role == Bingo.Domain.Teams.TeamMembershipRole.CoCaptain) &&
                                      participant.EventId == submission.EventId && participant.AccountId != null
                                select participant.AccountId!.Value).ToListAsync(cancellationToken);
        var creditedAccount = await db.EventParticipants.AsNoTracking().Where(x => x.Id == submission.CreditedParticipantId && x.EventId == submission.EventId).Select(x => x.AccountId).SingleOrDefaultAsync(cancellationToken);
        if (creditedAccount is Guid accountId) recipients.Add(accountId);
        foreach (var recipient in recipients.Distinct())
        {
            var notificationId = DeterministicNotificationId(submission.Id, recipient);
            if (!await db.PersonalNotifications.AnyAsync(x => x.Id == notificationId, cancellationToken))
                db.PersonalNotifications.Add(new Bingo.Domain.Access.PersonalNotification(notificationId, recipient, "evidence.rejected", detail, $"/Captain/Submissions/{submission.Id}", now));
        }
    }

    private static Guid DeterministicNotificationId(Guid submissionId, Guid recipientAccountId)
    {
        Span<byte> input = stackalloc byte[32]; submissionId.TryWriteBytes(input[..16]); recipientAccountId.TryWriteBytes(input[16..]);
        var hash = SHA256.HashData(input); return new Guid(hash.AsSpan(0, 16));
    }

    private static string Snapshot(Submission s) => JsonSerializer.Serialize(new { s.BoardTileId, s.RequirementId, s.DropSnapshotId, s.CreditedParticipantId, s.CreditedOsrsCharacterId, s.CreditedCharacterName, s.ClaimedWeight, s.ApprovedContribution, s.Status, s.Version, s.CurrentReviewerNote, s.ResubmissionOfSubmissionId });
}
