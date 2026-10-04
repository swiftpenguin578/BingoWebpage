using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Bingo.Application.Boards;
using Bingo.Application.Evidence;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
    private const int MaxReviewReasonLength = 4000;

    public async Task<SubmissionResult> CreateAsync(CreateSubmissionCommand command, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow(); var actor = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == command.ActorAccountId, cancellationToken) ?? throw new InvalidOperationException("Account not found.");
        var actorScope = await Authority.AuthorizeAsync(command.ActorAccountId, command.EventId, command.TeamId, command.CreditedParticipantId, now, cancellationToken);
        if (actorScope.Kind == EvidenceActorKind.Administrator) throw new InvalidOperationException("Administrators cannot create evidence submissions.");
        await using var attributionLock = await ParticipantAttributionLock.AcquireAsync(db, command.CreditedParticipantId, cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        // Share event-before-membership ordering with role changes and live withdrawal.
        // Discard pre-request authority only after acquiring both mutation boundaries.
        var ev = await LockedVisibleEventAsync(command.EventId, cancellationToken);
        await db.TeamMemberships.FromSqlInterpolated($"SELECT m.* FROM team_memberships m JOIN event_participants p ON p.id = m.event_participant_id WHERE p.account_id = {command.ActorAccountId} AND p.event_id = {command.EventId} AND m.team_id = {command.TeamId} AND m.left_at IS NULL FOR UPDATE OF m")
            .AsNoTracking().ToListAsync(cancellationToken);
        now = time.GetUtcNow();
        actorScope = await Authority.AuthorizeAsync(command.ActorAccountId, command.EventId, command.TeamId, command.CreditedParticipantId, now, cancellationToken);
        if (actorScope.Kind == EvidenceActorKind.Administrator) throw new InvalidOperationException("Administrators cannot create evidence submissions.");
        EnsureMutationWindow(ev, actorScope.Kind, now, "New submissions are not currently open.");
        // Keep the actual clock for the upload cutoff; normalize only the stored
        // attribution instant so sub-microsecond lateness cannot reopen the window.
        now = ParticipantAttributionLock.AtDatabasePrecision(now);
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
            var after = Snapshot(submission);
            db.ReviewActions.Add(Action(submission.Id, ReviewActionType.Submitted, command.ActorAccountId, now, command.CaptainNote, null, after));
            AddAudit(submission, command.ActorAccountId, actor.LoginName, "submission.created", submission.CaptainNote, null, after, now);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(CancellationToken.None);
            committed = true;
        }
        catch { if (!committed && stored is not null) await storage.DeleteAsync(stored.StorageKey, CancellationToken.None); throw; }
        return new SubmissionResult(submission.Id, submission.Status);
    }

    public async Task<SubmissionResult> CorrectAsync(CorrectSubmissionCommand command, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var eventId = await db.Submissions.AsNoTracking().Where(x => x.Id == command.SubmissionId).Select(x => (Guid?)x.EventId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Submission not found.");
        var ev = await LockedVisibleEventAsync(eventId, cancellationToken);
        var submission = await db.Submissions.FromSqlInterpolated($"SELECT * FROM submissions WHERE id = {command.SubmissionId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("Submission not found.");
        var actorScope = command.OwnerOnly
            ? await Authority.AuthorizeOwnerAsync(command.ActorAccountId, submission.EventId, submission.TeamId, submission.CreditedParticipantId, now, cancellationToken)
            : await Authority.AuthorizeAsync(command.ActorAccountId, submission.EventId, submission.TeamId, submission.CreditedParticipantId, now, cancellationToken);
        if (actorScope.Kind is EvidenceActorKind.Administrator) throw new InvalidOperationException("Administrators use the review correction path for this submission.");
        EnsureMutationWindow(ev, actorScope.Kind, now, "Submissions are not currently open.");
        EnsureExpectedVersion(submission, command.ExpectedVersion);
        if (submission.Status != SubmissionStatus.Pending) throw new InvalidOperationException("Only a pending submission can be corrected.");
        var creditedWeight = await ValidateTarget(submission.EventId, submission.TeamId, command.BoardTileId, command.RequirementId, command.DropSnapshotId, command.CreditedParticipantId, cancellationToken);
        var actorName = await ActorNameAsync(command.ActorAccountId, cancellationToken);
        var before = Snapshot(submission);
        StoredEvidence? stored = null;
        var committed = false;
        try
        {
            if (command.Evidence is not null)
                stored = await storage.StoreAsync(submission.EventId, submission.Id, command.OriginalFilename ?? "evidence", command.Evidence, cancellationToken);
            submission.EditPending(command.BoardTileId, command.RequirementId, command.DropSnapshotId, command.CreditedParticipantId, creditedWeight, command.CaptainNote);
            var after = Snapshot(submission);
            if (stored is null)
            {
                db.ReviewActions.Add(Action(submission.Id, ReviewActionType.EditMetadata, command.ActorAccountId, now, null, before, after));
            }
            else
            {
                var activeAssets = await db.EvidenceAssets.Where(x => x.SubmissionId == submission.Id && x.Active).ToListAsync(cancellationToken);
                foreach (var asset in activeAssets) asset.Deactivate();
                db.EvidenceAssets.Add(Asset(submission.Id, command.ActorAccountId, stored, EvidenceAssetRole.ReplacementEvidence, now));
                db.ReviewActions.Add(Action(submission.Id, ReviewActionType.ReplaceEvidence, command.ActorAccountId, now, null, before, after));
            }
            AddAudit(submission, command.ActorAccountId, actorName, "submission.corrected", submission.CaptainNote ?? (stored is null ? "Submission metadata corrected." : "Submission evidence replaced."), before, after, now);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(CancellationToken.None);
            committed = true;
            return new SubmissionResult(submission.Id, submission.Status);
        }
        catch
        {
            if (!committed && stored is not null) await storage.DeleteAsync(stored.StorageKey, CancellationToken.None);
            throw;
        }
    }

    public async Task WithdrawAsync(Guid submissionId, Guid actorAccountId, CancellationToken cancellationToken = default, int? expectedVersion = null, bool ownerOnly = false)
    {
        var now = time.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var eventId = await db.Submissions.AsNoTracking().Where(x => x.Id == submissionId).Select(x => (Guid?)x.EventId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Submission not found.");
        var ev = await LockedVisibleEventAsync(eventId, cancellationToken);
        var s = await db.Submissions.FromSqlInterpolated($"SELECT * FROM submissions WHERE id = {submissionId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("Submission not found.");
        var actorScope = ownerOnly ? await Authority.AuthorizeOwnerAsync(actorAccountId, s.EventId, s.TeamId, s.CreditedParticipantId, now, cancellationToken) : await Authority.AuthorizeAsync(actorAccountId, s.EventId, s.TeamId, s.CreditedParticipantId, now, cancellationToken);
        if (actorScope.Kind is EvidenceActorKind.Administrator) throw new InvalidOperationException("Administrators cannot withdraw submissions through the captain path.");
        EnsureMutationWindow(ev, actorScope.Kind, now, "Submissions are not currently open.");
        EnsureExpectedVersion(s, expectedVersion);
        if (s.Status != SubmissionStatus.Pending) throw new InvalidOperationException("Only a pending submission can be withdrawn.");
        var actorName = await ActorNameAsync(actorAccountId, cancellationToken);
        var before = Snapshot(s);
        s.Withdraw(now);
        var after = Snapshot(s);
        db.ReviewActions.Add(Action(s.Id, ReviewActionType.Withdraw, actorAccountId, now, null, before, after));
        AddAudit(s, actorAccountId, actorName, "submission.withdrawn", "Submission withdrawn.", before, after, now);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(CancellationToken.None);
    }

    public async Task RejectAsync(Guid submissionId, Guid adminAccountId, string reason, CancellationToken cancellationToken = default, int? expectedVersion = null)
    {
        var adminName = await EnsureAdmin(adminAccountId, cancellationToken); var normalizedReason = RequireReviewReason(reason); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var s = await LockedAdminReviewSubmissionAsync(submissionId, cancellationToken); EnsureExpectedVersion(s, expectedVersion); var now = time.GetUtcNow(); var before = Snapshot(s); s.Reject(normalizedReason, now); var after = Snapshot(s); db.ReviewActions.Add(Action(s.Id, ReviewActionType.Reject, adminAccountId, now, normalizedReason, before, after)); AddAudit(s, adminAccountId, adminName, "submission.rejected", normalizedReason, before, after, now); await AddRejectionNotificationsAsync(s, normalizedReason, now, cancellationToken); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(CancellationToken.None);
    }

    public async Task<SubmissionApprovalResult> ApproveAsync(Guid submissionId, Guid adminAccountId, CancellationToken cancellationToken = default, int? expectedVersion = null)
    {
        var adminName = await EnsureAdmin(adminAccountId, cancellationToken); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var s = await LockedAdminReviewSubmissionAsync(submissionId, cancellationToken); EnsureExpectedVersion(s, expectedVersion); if (s.Status != SubmissionStatus.Pending) throw new InvalidOperationException("Only a pending submission can be approved.");
        var publication = await LockedPublicationAsync(s.EventId, cancellationToken);
        var requirement = publication.Requirements.SingleOrDefault(x => x.Id == s.RequirementId && x.BoardTileId == s.BoardTileId) ?? throw new InvalidOperationException("The published objective is unavailable.");
        var used = await db.SubmissionContributions.Where(x => x.TeamId == s.TeamId && x.RequirementId == s.RequirementId && x.ReversedAt == null).SumAsync(x => (int?)x.Amount, cancellationToken) ?? 0;
        var remaining = Math.Max(0, requirement.TargetContribution - used); var allowed = s.ClaimedWeight;
        if (s.DropSnapshotId is not null)
        {
            var drop = publication.Drops.Single(x => x.Id == s.DropSnapshotId && x.RequirementId == s.RequirementId);
            var maximum = MaximumContribution(requirement, drop, publication.Drops);
            var dropUsed = await UsedDropContributionAsync(s.TeamId, s.RequirementId, drop, requirement.DuplicatesAllowed, cancellationToken);
            allowed = Math.Min(allowed, Math.Max(0, maximum - dropUsed));
        }
        var amount = Math.Min(remaining, allowed);
        if (amount < 1) throw new InvalidOperationException("This objective has no remaining eligible contribution for this submission. It cannot be approved under the published objective rules.");
        var blockingSubmission = await FindEarlierApprovalBlockAsync(s, requirement, publication, amount, cancellationToken);
        if (blockingSubmission is not null)
        {
            // LockedAdminReviewSubmissionAsync advances the event version in the
            // tracked entity for a decision. A room-order refusal must leave the
            // transaction and context without that pending mutation.
            db.ChangeTracker.Clear();
            return new SubmissionApprovalResult(0, blockingSubmission);
        }
        var ev = await db.Events.SingleAsync(x => x.Id == s.EventId, cancellationToken);
        var completesTile = await CompletesTileAtApprovalAsync(s, amount, cancellationToken);
        var now = time.GetUtcNow(); var before = Snapshot(s); ev.AdvanceStatsEvidenceRevision(additiveApproval: true); var announcementOrdinal = ev.ReserveAnnouncementOrdinal(); s.Approve(amount, now, ev.AnnouncementGeneration, completesTile, announcementOrdinal); var after = Snapshot(s); db.SubmissionContributions.Add(new SubmissionContribution(Guid.NewGuid(), s.Id, s.TeamId, s.RequirementId, s.DropSnapshotId, s.CreditedParticipantId, amount, now)); db.ReviewActions.Add(Action(s.Id, ReviewActionType.Approve, adminAccountId, now, $"Approved contribution: {amount}", before, after)); AddAudit(s, adminAccountId, adminName, "submission.approved", $"Approved contribution: {amount}.", before, after, now); await db.SaveChangesAsync(cancellationToken);
        await TileCompletionFactReconciler.ReconcileAsync(db, s.EventId, publication, [s.TeamId], now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        var focusCleared = focus is not null && await focus.ClearCompletedTileFocusAsync(s.EventId, s.TeamId, s.BoardTileId, cancellationToken); if (focusCleared) await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(CancellationToken.None); await Notify(s.EventId, cancellationToken); await NotifyFocus(s.EventId, s.TeamId, focusCleared, cancellationToken); return new SubmissionApprovalResult(amount);
    }

    public async Task ReverseAsync(Guid submissionId, Guid adminAccountId, string reason, CancellationToken cancellationToken = default, int? expectedVersion = null)
    {
        var adminName = await EnsureAdmin(adminAccountId, cancellationToken); var normalizedReason = RequireReviewReason(reason); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var s = await LockedAdminReviewSubmissionAsync(submissionId, cancellationToken); EnsureExpectedVersion(s, expectedVersion); if (s.Status != SubmissionStatus.Approved) throw new InvalidOperationException("Only an approved submission can be reversed."); var publication = await LockedPublicationAsync(s.EventId, cancellationToken); var contribution = await db.SubmissionContributions.SingleAsync(x => x.SubmissionId == submissionId && x.ReversedAt == null, cancellationToken); var now = time.GetUtcNow(); var before = Snapshot(s); s.Reverse(normalizedReason, now); contribution.Reverse(now); var after = Snapshot(s); db.ReviewActions.Add(Action(s.Id, ReviewActionType.ReverseApproval, adminAccountId, now, normalizedReason, before, after)); AddAudit(s, adminAccountId, adminName, "submission.reversed", normalizedReason, before, after, now); await RebalanceLaterContributions(s, contribution, publication, adminAccountId, adminName, now, cancellationToken); (await db.Events.SingleAsync(x => x.Id == s.EventId, cancellationToken)).AdvanceStatsEvidenceRevision(); await db.SaveChangesAsync(cancellationToken);
        await TileCompletionFactReconciler.ReconcileAsync(db, s.EventId, publication, [s.TeamId], now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        var focusCleared = focus is not null && await focus.ClearCompletedTileFocusAsync(s.EventId, s.TeamId, s.BoardTileId, cancellationToken); if (focusCleared) await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(CancellationToken.None); await Notify(s.EventId, cancellationToken); await NotifyFocus(s.EventId, s.TeamId, focusCleared, cancellationToken);
    }

    public async Task EditMetadataAsync(EditSubmissionMetadataCommand command, CancellationToken cancellationToken = default)
    {
        var adminName = await EnsureAdmin(command.AdminAccountId, cancellationToken);
        var normalizedReason = RequireReviewReason(command.Reason);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var s = await LockedAdminReviewSubmissionAsync(command.SubmissionId, cancellationToken);
        EnsureExpectedVersion(s, command.ExpectedVersion);
        if (s.Status != SubmissionStatus.Pending) throw new InvalidOperationException("Only a pending submission can be corrected.");
        var candidates = await CorrectionCharactersAsync(s, cancellationToken);
        var credited = candidates.SingleOrDefault(x => x.CharacterId == command.CreditedOsrsCharacterId)
            ?? throw new InvalidOperationException("Choose an unambiguous Playing character assigned in this event to a current or former member of this submission's team.");
        var creditedWeight = await ValidateTarget(s.EventId, s.TeamId, command.BoardTileId, command.RequirementId, command.DropSnapshotId, credited.ParticipantId, cancellationToken, retainedTeamAttribution: true);
        var before = Snapshot(s);
        s.EditPending(command.BoardTileId, command.RequirementId, command.DropSnapshotId, s.CreditedParticipantId, creditedWeight, s.CaptainNote);
        s.CorrectCreditedAttribution(credited.ParticipantId, credited.CharacterId, credited.CharacterName);
        var now = time.GetUtcNow(); var after = Snapshot(s); db.ReviewActions.Add(Action(s.Id, ReviewActionType.EditMetadata, command.AdminAccountId, now, normalizedReason, before, after)); AddAudit(s, command.AdminAccountId, adminName, "submission.corrected", normalizedReason, before, after, now);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(CancellationToken.None);
    }

    public async Task<IReadOnlyList<SubmissionCorrectionCharacter>> GetCorrectionCharactersAsync(Guid submissionId, Guid adminAccountId, CancellationToken cancellationToken = default)
    {
        await EnsureAdmin(adminAccountId, cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var submission = await db.Submissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == submissionId, cancellationToken)
            ?? throw new InvalidOperationException("Submission not found.");
        var ev = await db.Events.AsNoTracking().SingleAsync(x => x.Id == submission.EventId, cancellationToken);
        EnsureEvidenceReviewCapability(ev);
        if (submission.Status != SubmissionStatus.Pending) return [];
        var result = await CorrectionCharactersAsync(submission, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<IReadOnlyList<SubmissionCorrectionCharacter>> CorrectionCharactersAsync(Submission submission, CancellationToken ct)
    {
        // Resolve identity across the whole event before restricting membership: a
        // character retained against two participants cannot be attributed safely.
        var rows = await (from assignment in db.EventParticipantCharacters.AsNoTracking()
                          join participant in db.EventParticipants.AsNoTracking() on assignment.EventParticipantId equals participant.Id
                          join character in db.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                          where assignment.EventId == submission.EventId && participant.EventId == submission.EventId
                          select new { assignment.EventParticipantId, character.Id, character.DisplayName, assignment.ReleasedAt, assignment.EventRole }).ToListAsync(ct);
        var memberships = await db.TeamMemberships.AsNoTracking().Where(x => x.TeamId == submission.TeamId)
            .Select(x => new { x.EventParticipantId, x.LeftAt }).ToListAsync(ct);
        return rows.GroupBy(x => x.Id).Where(g => g.Select(x => x.EventParticipantId).Distinct().Count() == 1)
            .Where(g => g.Any(x => x.EventRole == Bingo.Domain.Signups.EventCharacterRole.Playing) &&
                        !g.Any(x => x.ReleasedAt == null && x.EventRole != Bingo.Domain.Signups.EventCharacterRole.Playing))
            .Where(g => memberships.Any(m => m.EventParticipantId == g.First().EventParticipantId))
            .Select(g => new SubmissionCorrectionCharacter(g.Key, g.First().EventParticipantId, g.First().DisplayName,
                g.All(x => x.ReleasedAt != null),
                !memberships.Any(m => m.EventParticipantId == g.First().EventParticipantId && m.LeftAt == null)))
            .OrderBy(x => x.CharacterName).ThenBy(x => x.CharacterId).ToList();
    }

    private async Task RebalanceLaterContributions(Submission reversed, SubmissionContribution reversedContribution, PublishedBoardData publication, Guid adminAccountId, string adminName, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var requirement = publication.Requirements.Single(x => x.Id == reversed.RequirementId && x.BoardTileId == reversed.BoardTileId);
        var active = (await db.SubmissionContributions.FromSqlInterpolated($"SELECT * FROM submission_contributions WHERE team_id = {reversed.TeamId} AND requirement_id = {reversed.RequirementId} AND reversed_at IS NULL FOR UPDATE").OrderBy(x => x.AppliedAt).ToListAsync(cancellationToken)).Where(x => x.Id != reversedContribution.Id).ToList();
        var remaining = Math.Max(0, requirement.TargetContribution - active.Sum(x => x.Amount)); if (remaining == 0) return;
        var later = active.Where(x => x.AppliedAt >= reversedContribution.AppliedAt).ToList(); if (later.Count == 0) return;
        var submissionIds = later.Select(x => x.SubmissionId).ToList(); var submissions = await db.Submissions.Where(x => submissionIds.Contains(x.Id) && x.Status == SubmissionStatus.Approved).ToDictionaryAsync(x => x.Id, cancellationToken);
        var drops = publication.Drops.Where(x => x.RequirementId == requirement.Id).ToDictionary(x => x.Id);
        var usedByDrop = active.Where(x => x.DropSnapshotId is not null).GroupBy(x => x.DropSnapshotId!.Value).ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        var usedByItem = active.Where(x => x.DropSnapshotId is not null && drops.ContainsKey(x.DropSnapshotId.Value)).GroupBy(x => drops[x.DropSnapshotId!.Value].ItemIdSnapshot).ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        if (!requirement.DuplicatesAllowed) EnsureConsistentCaps(drops.Values);
        foreach (var item in later)
        {
            if (remaining == 0 || !submissions.TryGetValue(item.SubmissionId, out var submission)) break; var headroom = Math.Max(0, submission.ClaimedWeight - item.Amount); if (headroom == 0) continue;
            if (item.DropSnapshotId is Guid dropId && drops.TryGetValue(dropId, out var drop))
            {
                var maximum = requirement.DuplicatesAllowed ? drop.MaximumContribution ?? int.MaxValue : drops.Values.Where(value => value.ItemIdSnapshot == drop.ItemIdSnapshot).Select(value => value.MaximumContribution ?? 1).Distinct().Single();
                var used = requirement.DuplicatesAllowed ? usedByDrop.GetValueOrDefault(dropId) : usedByItem.GetValueOrDefault(drop.ItemIdSnapshot);
                headroom = Math.Min(headroom, Math.Max(0, maximum - used));
            }
            var increase = Math.Min(remaining, headroom); if (increase == 0) continue; var oldAmount = item.Amount; var oldSnapshot = Snapshot(submission); item.IncreaseAmount(oldAmount + increase); submission.IncreaseApprovedContribution(oldAmount + increase); if (item.DropSnapshotId is Guid usedDrop) { usedByDrop[usedDrop] = usedByDrop.GetValueOrDefault(usedDrop) + increase; if (drops.TryGetValue(usedDrop, out var usedDropSnapshot)) usedByItem[usedDropSnapshot.ItemIdSnapshot] = usedByItem.GetValueOrDefault(usedDropSnapshot.ItemIdSnapshot) + increase; }
            remaining -= increase;
            var newSnapshot = Snapshot(submission);
            var details = $"Contribution {item.Id:D} adjusted from {oldAmount} to {item.Amount} after reversal of {reversed.Id:D}.";
            db.ReviewActions.Add(Action(submission.Id, ReviewActionType.RebalanceContribution, adminAccountId, now, details, oldSnapshot, newSnapshot));
            AddAudit(submission, adminAccountId, adminName, "submission.contribution_rebalanced", details, oldSnapshot, newSnapshot, now);
        }
    }

    private async Task<BingoEvent> LockedVisibleEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        try
        {
            return await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} AND hidden_at IS NULL FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Event not found.");
        }
        catch (Exception exception) when (exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } or InvalidOperationException { InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } })
        {
            throw new InvalidOperationException("The published board changed while this submission was being prepared. Reload and try again.", exception);
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
        // Match the event-first boundary used by publication and participant mutations.
        var eventId = await db.Submissions.AsNoTracking().Where(x => x.Id == submissionId).Select(x => (Guid?)x.EventId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Submission not found.");
        var ev = await LockedAdminReviewEventAsync(eventId, cancellationToken);
        var submission = await db.Submissions.FromSqlInterpolated($"SELECT * FROM submissions WHERE id = {submissionId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Submission not found.");
        // The caller saves the decision, audit, contribution changes and freshness together.
        ev.AdvanceVersion();
        return submission;
    }

    private static void EnsureEvidenceReviewCapability(BingoEvent item)
    {
        if (item.IsHidden || !EventStatePolicy.Allows(item.State, EventCapability.ReviewEvidence))
            throw new InvalidOperationException("Evidence can only be reviewed while the event is Live or awaiting final review.");
    }
    private async Task<int> ValidateTarget(Guid eventId, Guid teamId, Guid tileId, Guid requirementId, Guid? dropId, Guid participantId, CancellationToken cancellationToken, bool retainedTeamAttribution = false)
    {
        if (!await db.Teams.AnyAsync(x => x.Id == teamId && x.EventId == eventId && x.Active, cancellationToken)) throw new InvalidOperationException("Team not found."); if (!retainedTeamAttribution && !await IsEligibleTeamCreditAsync(teamId, participantId, time.GetUtcNow(), cancellationToken)) throw new InvalidOperationException("The credited player is not eligible for this team at the evidence time.");
        var publication = await LockedPublicationAsync(eventId, cancellationToken);
        var tile = publication.Tiles.SingleOrDefault(x => x.Id == tileId) ?? throw new InvalidOperationException("Choose a tile from the published event board.");
        var requirement = publication.Requirements.SingleOrDefault(x => x.Id == requirementId && x.BoardTileId == tile.Id) ?? throw new InvalidOperationException("Choose a requirement from that tile.");
        var creditedWeight = 1;
        if (requirement.ManualObjective && dropId is not null) throw new InvalidOperationException("Manual objectives do not use a drop.");
        if (!requirement.ManualObjective && dropId is null) throw new InvalidOperationException("Choose an eligible drop.");
        if (!requirement.ManualObjective && dropId is Guid selectedDropId)
        {
            var drop = publication.Drops.SingleOrDefault(x => x.Id == selectedDropId && x.RequirementId == requirementId) ?? throw new InvalidOperationException("Choose an eligible drop.");
            creditedWeight = drop.CreditedWeight;
            var maximum = MaximumContribution(requirement, drop, publication.Drops);
            var dropApproved = await UsedDropContributionAsync(teamId, requirementId, drop, requirement.DuplicatesAllowed, cancellationToken);
            if (dropApproved >= maximum) throw new InvalidOperationException("This drop has already reached its approved contribution limit.");
        }
        var approved = await db.SubmissionContributions.Where(x => x.TeamId == teamId && x.RequirementId == requirementId && x.ReversedAt == null).SumAsync(x => (int?)x.Amount, cancellationToken) ?? 0; if (approved >= requirement.TargetContribution) throw new InvalidOperationException("This objective has already been completed."); return creditedWeight;
    }
    private async Task<PublishedBoardData> LockedPublicationAsync(Guid eventId, CancellationToken ct)
    {
        // Submission callers already hold the event lock; board writers use the same order.
        try
        {
            var board = await db.Boards.FromSqlInterpolated($"SELECT * FROM boards WHERE event_id = {eventId} FOR UPDATE").SingleOrDefaultAsync(ct);
            if (board?.State != BoardState.Published || board.ActiveApprovalSnapshotId is not { } approvalId)
                throw new InvalidOperationException("Choose a tile from the published event board.");
            return await db.ApprovalObjectivesAsync(board.Id, approvalId, ct) ?? throw new InvalidOperationException("The published objective identities are unavailable. Contact an administrator.");
        }
        catch (Exception exception) when (exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } or InvalidOperationException { InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } })
        {
            throw new InvalidOperationException("The published board changed while this submission was being prepared. Reload and try again.", exception);
        }
    }

    private static int MaximumContribution(BoardRequirementSnapshot requirement, BoardRequirementDropSnapshot drop, IEnumerable<BoardRequirementDropSnapshot> drops)
    {
        if (requirement.DuplicatesAllowed) return drop.MaximumContribution ?? int.MaxValue;
        var caps = drops.Where(x => x.RequirementId == requirement.Id && x.ItemIdSnapshot == drop.ItemIdSnapshot).Select(x => x.MaximumContribution ?? 1).Distinct().ToList();
        if (caps.Count != 1) throw new InvalidOperationException("Eligible aliases have inconsistent contribution caps; board approval is required before review can continue.");
        return caps[0];
    }

    private async Task<SubmissionApprovalBlock?> FindEarlierApprovalBlockAsync(Submission current, BoardRequirementSnapshot requirement,
        PublishedBoardData publication, int currentAmount, CancellationToken cancellationToken)
    {
        var pending = await db.Submissions.AsNoTracking()
            .Where(x => x.TeamId == current.TeamId && x.BoardTileId == current.BoardTileId &&
                        x.RequirementId == current.RequirementId && x.Status == SubmissionStatus.Pending)
            .ToListAsync(cancellationToken);
        var earlier = pending
            .Where(x => x.Id != current.Id && (x.SubmittedAt < current.SubmittedAt ||
                x.SubmittedAt == current.SubmittedAt && x.Id.CompareTo(current.Id) < 0))
            .OrderBy(x => x.SubmittedAt).ThenBy(x => x.Id)
            .ToList();
        if (earlier.Count == 0) return null;

        var drops = publication.Drops.Where(x => x.RequirementId == requirement.Id).ToDictionary(x => x.Id);
        if (!requirement.DuplicatesAllowed) EnsureConsistentCaps(drops.Values);
        var activeContributions = await (from contribution in db.SubmissionContributions.AsNoTracking()
                                         join snapshot in db.BoardRequirementDropSnapshots.AsNoTracking() on contribution.DropSnapshotId equals snapshot.Id
                                         where contribution.TeamId == current.TeamId && contribution.RequirementId == current.RequirementId &&
                                               contribution.ReversedAt == null && snapshot.RequirementId == current.RequirementId
                                         select new { contribution.Amount, snapshot.SourceDropId, snapshot.ItemIdSnapshot })
            .ToListAsync(cancellationToken);
        var used = await db.SubmissionContributions.AsNoTracking()
            .Where(x => x.TeamId == current.TeamId && x.RequirementId == current.RequirementId && x.ReversedAt == null)
            .SumAsync(x => (int?)x.Amount, cancellationToken) ?? 0;
        var usedBySourceDrop = activeContributions.GroupBy(x => x.SourceDropId).ToDictionary(x => x.Key, x => x.Sum(value => value.Amount));
        var usedByItem = activeContributions.GroupBy(x => x.ItemIdSnapshot).ToDictionary(x => x.Key, x => x.Sum(value => value.Amount));
        var currentDrop = current.DropSnapshotId is Guid currentDropId ? drops[currentDropId] : null;

        var usedWithoutCurrent = used;
        var usedWithCurrent = used;
        var usedBySourceDropWithoutCurrent = new Dictionary<Guid, int>(usedBySourceDrop);
        var usedBySourceDropWithCurrent = new Dictionary<Guid, int>(usedBySourceDrop);
        var usedByItemWithoutCurrent = new Dictionary<Guid, int>(usedByItem);
        var usedByItemWithCurrent = new Dictionary<Guid, int>(usedByItem);
        AddApprovalState(currentAmount, currentDrop, ref usedWithCurrent, usedBySourceDropWithCurrent, usedByItemWithCurrent);

        foreach (var candidate in earlier)
        {
            var candidateDrop = candidate.DropSnapshotId is Guid candidateDropId ? drops[candidateDropId] : null;
            var withoutCurrent = ApprovalAmount(candidate, requirement, publication.Drops, usedWithoutCurrent,
                usedBySourceDropWithoutCurrent, usedByItemWithoutCurrent, null, 0);
            var withCurrent = ApprovalAmount(candidate, requirement, publication.Drops, usedWithCurrent,
                usedBySourceDropWithCurrent, usedByItemWithCurrent, null, 0);
            if (withCurrent < withoutCurrent) return new SubmissionApprovalBlock(candidate.Id, candidate.SubmittedAt);
            AddApprovalState(withoutCurrent, candidateDrop, ref usedWithoutCurrent, usedBySourceDropWithoutCurrent, usedByItemWithoutCurrent);
            AddApprovalState(withCurrent, candidateDrop, ref usedWithCurrent, usedBySourceDropWithCurrent, usedByItemWithCurrent);
        }
        return null;
    }

    private static void AddApprovalState(int amount, BoardRequirementDropSnapshot? drop, ref int used,
        Dictionary<Guid, int> usedBySourceDrop, Dictionary<Guid, int> usedByItem)
    {
        if (amount < 1) return;
        used += amount;
        if (drop is null) return;
        usedBySourceDrop[drop.SourceDropId] = usedBySourceDrop.GetValueOrDefault(drop.SourceDropId) + amount;
        usedByItem[drop.ItemIdSnapshot] = usedByItem.GetValueOrDefault(drop.ItemIdSnapshot) + amount;
    }

    private static int ApprovalAmount(Submission candidate, BoardRequirementSnapshot requirement,
        IEnumerable<BoardRequirementDropSnapshot> publicationDrops, int used,
        IReadOnlyDictionary<Guid, int> usedBySourceDrop, IReadOnlyDictionary<Guid, int> usedByItem,
        BoardRequirementDropSnapshot? additionalDrop, int additionalAmount)
    {
        var remaining = Math.Max(0, requirement.TargetContribution - used - additionalAmount);
        var allowed = candidate.ClaimedWeight;
        if (candidate.DropSnapshotId is Guid candidateDropId)
        {
            var drops = publicationDrops.Where(x => x.RequirementId == requirement.Id).ToDictionary(x => x.Id);
            if (!drops.TryGetValue(candidateDropId, out var candidateDrop))
                throw new InvalidOperationException("The published objective is unavailable.");
            var maximum = MaximumContribution(requirement, candidateDrop, drops.Values);
            var dropUsed = requirement.DuplicatesAllowed
                ? usedBySourceDrop.GetValueOrDefault(candidateDrop.SourceDropId)
                : usedByItem.GetValueOrDefault(candidateDrop.ItemIdSnapshot);
            if (additionalDrop is not null && (requirement.DuplicatesAllowed
                    ? additionalDrop.SourceDropId == candidateDrop.SourceDropId
                    : additionalDrop.ItemIdSnapshot == candidateDrop.ItemIdSnapshot)) dropUsed += additionalAmount;
            allowed = Math.Min(allowed, Math.Max(0, maximum - dropUsed));
        }
        return Math.Min(remaining, allowed);
    }

    private async Task<int> UsedDropContributionAsync(Guid teamId, Guid requirementId, BoardRequirementDropSnapshot drop, bool duplicatesAllowed, CancellationToken cancellationToken)
    {
        var contributions = await (from contribution in db.SubmissionContributions.AsNoTracking()
                                   join snapshot in db.BoardRequirementDropSnapshots.AsNoTracking() on contribution.DropSnapshotId equals snapshot.Id
                                   where contribution.TeamId == teamId && contribution.RequirementId == requirementId && contribution.ReversedAt == null && snapshot.RequirementId == requirementId
                                   select new { contribution.Amount, snapshot.SourceDropId, snapshot.ItemIdSnapshot })
            .ToListAsync(cancellationToken);
        return contributions.Where(value => duplicatesAllowed
                ? value.SourceDropId == drop.SourceDropId
                : value.ItemIdSnapshot == drop.ItemIdSnapshot)
            .Sum(value => value.Amount);
    }

    private static void EnsureConsistentCaps(IEnumerable<BoardRequirementDropSnapshot> drops)
    {
        foreach (var itemDrops in drops.GroupBy(x => x.ItemIdSnapshot))
        {
            var caps = itemDrops.Select(x => x.MaximumContribution ?? 1).Distinct().ToList();
            if (caps.Count != 1) throw new InvalidOperationException($"Eligible aliases for catalogue item {itemDrops.Key} have inconsistent contribution caps; board approval is required before review can continue.");
        }
    }

    private async Task<string> EnsureAdmin(Guid id, CancellationToken cancellationToken)
    {
        return await db.Accounts.AsNoTracking()
                   .Where(x => x.Id == id && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin) && x.Active)
                   .Select(x => x.LoginName)
                   .SingleOrDefaultAsync(cancellationToken)
               ?? throw new InvalidOperationException("Administrator access is required.");
    }

    private static string RequireReviewReason(string? reason)
    {
        if (reason is { Length: > MaxReviewReasonLength }) throw new InvalidOperationException("Reason must be 4000 characters or fewer.");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A written correction reason is required.");
        var normalized = reason.Trim();
        return normalized;
    }

    private async Task<string> ActorNameAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.Accounts.AsNoTracking().Where(x => x.Id == id && x.Active).Select(x => x.LoginName).SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Account not found.");
    }

    private void AddAudit(Submission submission, Guid actorAccountId, string actorName, string action, string? details, string? before, string? after, DateTimeOffset occurredAt)
    {
        db.AuditEntries.Add(new AuditEntry(
            Guid.NewGuid(), occurredAt, actorAccountId, actorName, action, "submission", submission.Id.ToString("D"), details,
            submission.EventId, before, after));
    }

    private async Task<bool> IsEligibleTeamCreditAsync(Guid teamId, Guid participantId, DateTimeOffset submittedAt, CancellationToken cancellationToken)
    {
        if (await db.TeamMemberships.AnyAsync(x => x.TeamId == teamId && x.EventParticipantId == participantId && x.LeftAt == null, cancellationToken)) return true;
        return await (from participant in db.EventParticipants
                      join membership in db.TeamMemberships on participant.Id equals membership.EventParticipantId
                      where membership.TeamId == teamId && membership.EventParticipantId == participantId && membership.LeftAt != null &&
                            participant.SignupStatus == Bingo.Domain.Signups.SignupStatus.Withdrawn && participant.WithdrawnAt != null && submittedAt.ToUniversalTime() <= participant.WithdrawnAt.Value
                      select participant.Id).AnyAsync(cancellationToken);
    }

    private async Task<bool> CompletesTileAtApprovalAsync(Submission submission, int amount, CancellationToken cancellationToken)
    {
        var publication = await db.PublishedObjectivesAsync(submission.EventId, cancellationToken);
        if (publication is null) return false;
        var requirementIds = publication.Requirements
            .Where(x => x.BoardTileId == submission.BoardTileId)
            .Select(x => new { x.Id, x.TargetContribution })
            .ToList();
        if (requirementIds.Count == 0) return false;
        var totals = await db.SubmissionContributions.AsNoTracking()
            .Where(x => x.TeamId == submission.TeamId && x.ReversedAt == null && requirementIds.Select(r => r.Id).Contains(x.RequirementId))
            .GroupBy(x => x.RequirementId)
            .Select(x => new { RequirementId = x.Key, Amount = x.Sum(item => item.Amount) })
            .ToDictionaryAsync(x => x.RequirementId, x => x.Amount, cancellationToken);
        totals[submission.RequirementId] = totals.GetValueOrDefault(submission.RequirementId) + amount;
        return requirementIds.All(x => totals.GetValueOrDefault(x.Id) >= x.TargetContribution);
    }
    private IEvidenceAuthority Authority => authority ??= new EvidenceAuthority(db);
    private static void EnsureExpectedVersion(Submission submission, int? expectedVersion)
    { if (expectedVersion is not null && submission.Version != expectedVersion.Value) throw new InvalidOperationException("This evidence changed in another request. Reload it and review the latest version before saving."); }
    private static void EnsureMutationWindow(BingoEvent ev, EvidenceActorKind kind, DateTimeOffset now, string message)
    {
        var open = kind != EvidenceActorKind.EmergencyCaptain && ev.AcceptsNewSubmissions(now);
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
                             join board in db.Boards.AsNoTracking() on submission.EventId equals board.EventId
                             join tile in db.BoardApprovalTileSnapshots.AsNoTracking() on submission.BoardTileId equals tile.BoardTileId
                             where eventItem.Id == submission.EventId && eventItem.HiddenAt == null
                             where tile.ApprovalSnapshotId == board.ActiveApprovalSnapshotId
                             select new { EventName = eventItem.Name, TileName = tile.Name }).SingleAsync(cancellationToken);
        var dropName = submission.DropSnapshotId is Guid dropId
            ? await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => x.Id == dropId).Select(x => x.ItemName).SingleOrDefaultAsync(cancellationToken)
            : null;
        var detail = BoundedRejectionNotificationDetail(context.EventName, context.TileName, dropName, reason);
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
                db.PersonalNotifications.Add(new Bingo.Domain.Access.PersonalNotification(notificationId, recipient, "evidence.rejected", detail,
                    $"/Submissions/{submission.Id}", now, submission.EventId));
        }
    }

    private static Guid DeterministicNotificationId(Guid submissionId, Guid recipientAccountId)
    {
        Span<byte> input = stackalloc byte[32]; submissionId.TryWriteBytes(input[..16]); recipientAccountId.TryWriteBytes(input[16..]);
        var hash = SHA256.HashData(input); return new Guid(hash.AsSpan(0, 16));
    }

    private static string BoundedRejectionNotificationDetail(string eventName, string tileName, string? dropName, string reason)
    {
        var fields = new string?[] { eventName, tileName, dropName, reason.Trim() };
        while (true)
        {
            var detail = JsonSerializer.Serialize(new { eventName = fields[0], tile = fields[1], drop = fields[2], reason = fields[3] });
            if (detail.Length <= 1_000) return detail;
            for (var index = fields.Length - 1; index >= 0; index--)
            {
                if (string.IsNullOrEmpty(fields[index])) continue;
                fields[index] = fields[index]![..(fields[index]!.Length / 2)];
                break;
            }
        }
    }

    private static string Snapshot(Submission s) => JsonSerializer.Serialize(new { s.BoardTileId, s.RequirementId, s.DropSnapshotId, s.CreditedParticipantId, s.CreditedOsrsCharacterId, s.CreditedCharacterName, CaptainNotePresent = s.CaptainNote is not null, s.ClaimedWeight, s.ApprovedContribution, s.Status, CurrentReviewerNotePresent = s.CurrentReviewerNote is not null, s.ResubmissionOfSubmissionId });
}
