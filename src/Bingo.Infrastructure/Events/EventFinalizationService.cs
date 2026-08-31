using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bingo.Application.Boards;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Infrastructure.Events;

public sealed class EventFinalizationService(ApplicationDbContext db, IPublicBoardService publicBoards, TimeProvider time) : IEventFinalizationService
{
    public async Task<FinalReviewReadiness?> GetReadinessAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, ct);
        if (ev is null) return null;

        var now = time.GetUtcNow();
        var finals = await db.EventFinalizations.AsNoTracking().Where(x => x.EventId == eventId).OrderByDescending(x => x.Version).ToListAsync(ct);
        var activeFinal = finals.FirstOrDefault(x => x.UnfinalizedAt is null);
        var cycleId = activeFinal?.ReviewCycleId ?? await db.EventStateTransitions.AsNoTracking()
            .Where(x => x.EventId == eventId && x.ToState == EventState.AwaitingFinalReview)
            .OrderByDescending(x => x.EffectiveAt).ThenByDescending(x => x.PerformedAt).ThenByDescending(x => x.Id)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct) ?? Guid.Empty;

        var scheduledStart = ev.ActualStartedAt ?? ev.EventStartsAt;
        var scheduledEnd = ev.ActualEndedAt ?? ev.EventEndsAt;
        var effectiveCutoff = ev.ReopenedSubmissionCutoffAt is { } reopened && (ev.SubmissionCutoffAt is null || reopened > ev.SubmissionCutoffAt.Value)
            ? reopened
            : ev.SubmissionCutoffAt;
        var blockers = new List<FinalReviewBlocker>();
        if (cycleId == Guid.Empty && ev.State == EventState.AwaitingFinalReview)
            blockers.Add(new("review-cycle", "Final-review cycle is missing", "The retained lifecycle history does not identify this review cycle. Correct the retained state before continuing.", "/Admin/Audit", false, false, null));
        if (ev.State != EventState.AwaitingFinalReview && ev.State is not (EventState.Finalized or EventState.Archived)) blockers.Add(new("event-state", "Event has not ended", "End the live event before final review.", null, false, false, null));
        var boardView = await publicBoards.GetEventBoardAsync(ev.Slug, ct);
        if (boardView is null) blockers.Add(new("published-board", "Published board and teams required", "The event needs a published board and finalized active teams.", $"/Admin/Events/Board/{eventId}", false, false, null));
        if (effectiveCutoff is null) blockers.Add(new("submission-cutoff", "Submission cutoff required", "Configure a submission cutoff before final review.", null, false, false, null));
        else if (now <= effectiveCutoff && ev.State == EventState.AwaitingFinalReview) blockers.Add(new("submission-window", "Submission window is still open", $"Captains can submit until {TimeZoneInfo.ConvertTime(effectiveCutoff.Value, TimeZoneInfo.FindSystemTimeZoneById(ev.Timezone)):dd MMM yyyy, HH:mm} local time.", null, true, false, null));
        var pending = await db.Submissions.AsNoTracking().Where(x => x.EventId == eventId && x.Status == SubmissionStatus.Pending).Select(x => x.Id).ToListAsync(ct);
        if (pending.Count > 0) blockers.Add(new(BlockerKey("pending-submissions", pending), "Pending submissions", $"{pending.Count} submission(s) still need a decision.", "/Admin/Review?status=Pending", true, false, null));

        var corrections = cycleId == Guid.Empty
            ? new Dictionary<Guid, TeamCompletionCorrection>()
            : await db.TeamCompletionCorrections.AsNoTracking().Where(x => x.EventId == eventId && x.ReviewCycleId == cycleId).ToDictionaryAsync(x => x.TeamId, ct);
        var placements = new List<ProvisionalPlacement>();
        if (boardView is not null)
        {
            var unranked = boardView.Teams.Select(team => new UnrankedTeamProgress(team.TeamId, team.TeamName,
                corrections.TryGetValue(team.TeamId, out var correction) && team.Progress.BoardComplete ? team.Progress with { BoardCompletedAt = correction.CorrectedCompletedAt } : team.Progress)).ToList();
            var ranked = PublicProgressCalculator.Rank(unranked);
            placements = ranked.Select(value => { var original = boardView.Teams.Single(x => x.TeamId == value.TeamId); return new ProvisionalPlacement(value.TeamId, value.TeamName, value.Rank, value.Progress.BoardComplete, original.Progress.BoardCompletedAt, corrections.GetValueOrDefault(value.TeamId)?.CorrectedCompletedAt, value.Progress.CompletedRows.Count + value.Progress.CompletedColumns.Count, value.Progress.CompletedTiles, value.Progress.EhbTiebreak); }).ToList();
            foreach (var completed in placements.Where(x => x.BoardComplete))
                blockers.Add(new(CompletionAcknowledgementKey(completed.TeamId), "Completion time inspected", $"Confirm that {completed.TeamName}'s completion time was inspected.", null, false, false, null, true, completed.TeamId));
            foreach (var tie in placements.GroupBy(x => x.Placement).Where(x => x.Count() > 1)) blockers.Add(new(BlockerKey($"placement-tie-{tie.Key}", tie.Select(x => x.TeamId)), $"Tie at placement {tie.Key}", $"{string.Join(", ", tie.Select(x => x.TeamName))} currently have identical ranking values. Confirm the tie or correct a completion time.", null, true, false, null));
        }

        var resolutions = cycleId == Guid.Empty ? [] : await db.FinalReviewResolutions.AsNoTracking().Where(x => x.ReviewCycleId == cycleId).OrderByDescending(x => x.ResolvedAt).ToListAsync(ct);
        blockers = blockers.Select(blocker =>
        {
            var resolution = resolutions.FirstOrDefault(x => x.BlockerKey == blocker.Key && (blocker.IsCompletionTimeAcknowledgement ? x.Kind == FinalReviewResolutionKind.CompletionTimeAcknowledgement : x.Kind == FinalReviewResolutionKind.ExceptionalOverride));
            return resolution is null ? blocker : blocker with { Resolved = true, ResolutionReason = resolution.Reason };
        }).ToList();

        var finalIds = finals.Select(x => x.Id).ToList();
        var official = finalIds.Count == 0 ? [] : await db.OfficialPlacements.AsNoTracking().Where(x => finalIds.Contains(x.FinalizationId)).OrderBy(x => x.Placement).ThenBy(x => x.TeamName).ToListAsync(ct);
        var history = finals.Select(f => new FinalizationHistoryRow(f.Id, f.Version, f.FinalizedAt, f.UnfinalizedAt is null, f.UnfinalizedAt, f.UnfinalizeReason, official.Where(x => x.FinalizationId == f.Id).Select(x => new OfficialPlacementRow(x.Placement, x.TeamName, x.BoardComplete, x.BoardCompletedAt, x.CompletedLines, x.CompletedTiles, x.EhbTiebreak)).ToList())).ToList();
        if (activeFinal is not null && (ev.State is EventState.Finalized or EventState.Archived)) placements = official.Where(x => x.FinalizationId == activeFinal.Id).Select(x => new ProvisionalPlacement(x.TeamId, x.TeamName, x.Placement, x.BoardComplete, x.BoardCompletedAt, null, x.CompletedLines, x.CompletedTiles, x.EhbTiebreak)).ToList();
        return new(eventId, ev.Name, ev.State, scheduledStart, scheduledEnd, effectiveCutoff, effectiveCutoff is not null && now <= effectiveCutoff, blockers, placements, history, cycleId, ev.Version);
    }

    public async Task ResolveBlockerAsync(Guid eventId, string blockerKey, string reason, bool confirmed, Guid adminId, long? expectedVersion = null, Guid? expectedReviewCycleId = null, CancellationToken ct = default)
    {
        if (!confirmed) throw new InvalidOperationException("Confirm that this exceptional override is safe.");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A reason is required.");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var (ev, readiness, now, actorName, stale) = await LockReviewMutationAsync(eventId, adminId, expectedVersion, expectedReviewCycleId, ct);
            var blocker = readiness.Blockers.SingleOrDefault(x => x.Key == blockerKey) ?? throw new InvalidOperationException("This item has already resolved automatically.");
            if (!blocker.CanOverride || blocker.IsCompletionTimeAcknowledgement) throw new InvalidOperationException("This item must be fixed or explicitly inspected.");
            var existing = await db.FinalReviewResolutions.SingleOrDefaultAsync(x => x.ReviewCycleId == readiness.ReviewCycleId && x.BlockerKey == blocker.Key, ct);
            if (existing is not null)
            {
                if (string.Equals(existing.Reason, reason.Trim(), StringComparison.Ordinal)) { await tx.CommitAsync(ct); return; }
                throw new InvalidOperationException("This final-review item was resolved differently in another request. Reload the current cycle.");
            }
            if (stale) throw StaleReviewMutation();
            db.FinalReviewResolutions.Add(new FinalReviewResolution(Guid.NewGuid(), ev.Id, readiness.ReviewCycleId, blocker.Key, blocker.Description, reason, adminId, now));
            AddReviewAudit(ev, adminId, actorName, now, "event.final_review_overridden", JsonSerializer.Serialize(new { blockerKey, reason = reason.Trim(), reviewCycleId = readiness.ReviewCycleId }));
            ev.AdvanceVersion();
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(CancellationToken.None);
        }
        catch (Exception ex) when (IsReviewPersistenceConflict(ex)) { throw StaleReviewMutation(); }
    }

    public async Task AcknowledgeCompletionTimeAsync(Guid eventId, Guid teamId, Guid adminId, long? expectedVersion = null, Guid? expectedReviewCycleId = null, CancellationToken ct = default)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var (ev, readiness, now, actorName, stale) = await LockReviewMutationAsync(eventId, adminId, expectedVersion, expectedReviewCycleId, ct);
            var blocker = readiness.Blockers.SingleOrDefault(x => x.TeamId == teamId && x.IsCompletionTimeAcknowledgement) ?? throw new InvalidOperationException("That team does not require completion-time inspection.");
            var existing = await db.FinalReviewResolutions.SingleOrDefaultAsync(x => x.ReviewCycleId == readiness.ReviewCycleId && x.BlockerKey == blocker.Key, ct);
            if (existing is not null)
            {
                if (existing.Kind == FinalReviewResolutionKind.CompletionTimeAcknowledgement && existing.TeamId == teamId) { await tx.CommitAsync(ct); return; }
                throw new InvalidOperationException("This completion review was resolved differently in another request. Reload the current cycle.");
            }
            if (stale) throw StaleReviewMutation();
            db.FinalReviewResolutions.Add(new FinalReviewResolution(Guid.NewGuid(), ev.Id, readiness.ReviewCycleId, blocker.Key, blocker.Description, null, adminId, now, FinalReviewResolutionKind.CompletionTimeAcknowledgement, teamId));
            AddReviewAudit(ev, adminId, actorName, now, "event.completion_time_inspected", JsonSerializer.Serialize(new { teamId, reviewCycleId = readiness.ReviewCycleId }));
            ev.AdvanceVersion();
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(CancellationToken.None);
        }
        catch (Exception ex) when (IsReviewPersistenceConflict(ex)) { throw StaleReviewMutation(); }
    }

    public async Task CorrectCompletionAsync(Guid eventId, Guid teamId, DateTimeOffset correctedAt, string reason, Guid adminId, long? expectedVersion = null, Guid? expectedReviewCycleId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A reason is required.");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var (ev, readiness, now, actorName, stale) = await LockReviewMutationAsync(eventId, adminId, expectedVersion, expectedReviewCycleId, ct);
            var team = readiness.Placements.SingleOrDefault(x => x.TeamId == teamId) ?? throw new InvalidOperationException("Team not found.");
            if (!team.BoardComplete) throw new InvalidOperationException("Only a completed board has a completion time.");
            if (readiness.EventStartsAt is not { } startsAt || readiness.EventEndsAt is not { } endsAt || correctedAt < startsAt || correctedAt > endsAt) throw new InvalidOperationException("The corrected completion time must be within the event window.");
            var row = await db.TeamCompletionCorrections.SingleOrDefaultAsync(x => x.EventId == eventId && x.ReviewCycleId == readiness.ReviewCycleId && x.TeamId == teamId, ct);
            if (row is not null && row.CorrectedCompletedAt == correctedAt.ToUniversalTime() && string.Equals(row.Reason, reason.Trim(), StringComparison.Ordinal)) { await tx.CommitAsync(ct); return; }
            if (stale) throw StaleReviewMutation();
            if (row is null) db.TeamCompletionCorrections.Add(new TeamCompletionCorrection(Guid.NewGuid(), ev.Id, readiness.ReviewCycleId, teamId, correctedAt, reason, adminId, now)); else row.Update(correctedAt, reason, adminId, now);
            var acknowledgementKey = CompletionAcknowledgementKey(teamId);
            db.FinalReviewResolutions.RemoveRange(await db.FinalReviewResolutions.Where(x => x.ReviewCycleId == readiness.ReviewCycleId && x.BlockerKey == acknowledgementKey && x.Kind == FinalReviewResolutionKind.CompletionTimeAcknowledgement).ToListAsync(ct));
            AddReviewAudit(ev, adminId, actorName, now, "event.completion_time_corrected", JsonSerializer.Serialize(new { teamId, correctedAt = correctedAt.ToUniversalTime(), reason = reason.Trim(), reviewCycleId = readiness.ReviewCycleId }));
            ev.AdvanceVersion();
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(CancellationToken.None);
        }
        catch (Exception ex) when (IsReviewPersistenceConflict(ex)) { throw StaleReviewMutation(); }
    }

    public async Task FinalizeAsync(Guid eventId, LifecycleActor actor, long? expectedVersion = null, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7303004)", ct);
        var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, ct) ?? throw new InvalidOperationException("Event not found.");
        var activeFinal = await db.EventFinalizations.AnyAsync(x => x.EventId == eventId && x.UnfinalizedAt == null, ct);
        if ((ev.State is EventState.Finalized or EventState.Archived) && activeFinal) { await tx.CommitAsync(ct); return; }
        if (expectedVersion is { } supplied && supplied != ev.Version) throw new InvalidOperationException("This event changed in another session. Reload before finalizing.");
        if (ev.State != EventState.AwaitingFinalReview) throw new InvalidOperationException("Only an event in final review can be finalized.");
        var development = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
        var current = await db.Events.AsNoTracking().Where(value => value.Id != eventId && value.HiddenAt == null && (value.State == EventState.Live || value.State == EventState.AwaitingFinalReview || value.State == EventState.Finalized) && !(development && value.IsDevelopmentFixture)).OrderBy(value => value.Name).Select(value => value.Name).FirstOrDefaultAsync(ct);
        if (current is not null) throw new InvalidOperationException($"{current} is already the current event. Archive it before finalizing this event.");
        var readiness = await GetReadinessAsync(eventId, ct) ?? throw new InvalidOperationException("Event not found.");
        if (readiness.EventVersion != ev.Version || !readiness.CanFinalize) throw new InvalidOperationException("Resolve every final-review item before finalizing.");
        if (readiness.ReviewCycleId == Guid.Empty) throw new InvalidOperationException("The final-review cycle is unavailable.");
        var now = time.GetUtcNow();
        var version = (await db.EventFinalizations.Where(x => x.EventId == eventId).MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
        var resolutions = await db.FinalReviewResolutions.Where(x => x.ReviewCycleId == readiness.ReviewCycleId).OrderBy(x => x.ResolvedAt).ToListAsync(ct);
        var calcInputs = JsonSerializer.Serialize(new { readiness.ReviewCycleId, readiness.EventStartsAt, readiness.EventEndsAt, readiness.SubmissionCutoff, resolutions = resolutions.Select(x => new { x.Id, x.BlockerKey, x.Kind, x.TeamId }), teams = readiness.Placements.Select(x => new { x.TeamId, x.TeamName, x.BoardComplete, x.CalculatedCompletedAt, x.CorrectedCompletedAt }) });
        var calcResults = JsonSerializer.Serialize(readiness.Placements);
        var snapshot = new EventFinalizationSnapshot(Guid.NewGuid(), eventId, version, now, actor.Id, readiness.ReviewCycleId, JsonSerializer.Serialize(resolutions.Select(x => x.Id)), calcInputs, calcResults);
        db.EventFinalizations.Add(snapshot);
        foreach (var row in readiness.Placements) db.OfficialPlacements.Add(new OfficialPlacementSnapshot(Guid.NewGuid(), snapshot.Id, eventId, row.TeamId, row.TeamName, row.Placement, row.BoardComplete, row.CorrectedCompletedAt ?? row.CalculatedCompletedAt, row.CompletedLines, row.CompletedTiles, row.EhbTiebreak));
        var from = ev.State;
        ev.FinalizeResults(now);
        foreach (var access in await db.AccountEventAccesses.Where(x => x.EventId == eventId).ToListAsync(ct)) access.Disable();
        AddLifecycleHistory(ev, from, actor, "event.finalized", "Official placements snapshotted and published", now);
        await AddResultNotificationsAsync(ev, now, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task<(BingoEvent Event, FinalReviewReadiness Readiness, DateTimeOffset Now, string ActorName, bool VersionStale)> LockReviewMutationAsync(Guid eventId, Guid adminId, long? expectedVersion, Guid? expectedReviewCycleId, CancellationToken ct)
    {
        if (expectedVersion is not > 0 || expectedReviewCycleId is not { } suppliedCycle || suppliedCycle == Guid.Empty)
            throw new InvalidOperationException("This final-review form is stale or incomplete. Reload the current final-review cycle.");
        var ev = await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} AND hidden_at IS NULL FOR UPDATE").SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Event not found.");
        var actorName = await db.Accounts.AsNoTracking().Where(x => x.Id == adminId && x.Active && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin)).Select(x => x.LoginName).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Administrator access is required.");
        if (ev.State != EventState.AwaitingFinalReview) throw new InvalidOperationException("Final-review changes are available only while the event is awaiting final review.");
        var readiness = await GetReadinessAsync(eventId, ct) ?? throw new InvalidOperationException("Event not found.");
        if (readiness.EventVersion != ev.Version || readiness.State != EventState.AwaitingFinalReview) throw new InvalidOperationException("This final-review cycle changed in another request. Reload before saving.");
        if (readiness.ReviewCycleId == Guid.Empty) throw new InvalidOperationException("The final-review cycle is unavailable.");
        if (readiness.ReviewCycleId != suppliedCycle || !await db.EventStateTransitions.AsNoTracking().AnyAsync(x => x.Id == suppliedCycle && x.EventId == eventId && x.ToState == EventState.AwaitingFinalReview, ct))
            throw new InvalidOperationException("This final-review cycle is stale. Reload the current final-review cycle.");
        return (ev, readiness, time.GetUtcNow().ToUniversalTime(), actorName, expectedVersion.Value != ev.Version);
    }

    private static InvalidOperationException StaleReviewMutation() => new("This final-review item changed while you were saving. Reload the current final-review cycle and try again.");
    private static bool IsReviewPersistenceConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException or PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.UniqueViolation }) return true;
        return false;
    }

    private void AddReviewAudit(BingoEvent ev, Guid actorId, string actorName, DateTimeOffset at, string action, string details)
        => db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), at, actorId, actorName, action, "event", ev.Id.ToString(), details, ev.Id));

    public async Task UnfinalizeAsync(Guid eventId, string reason, bool confirmed, LifecycleActor actor, long? expectedVersion = null, CancellationToken ct = default)
    {
        if (!confirmed) throw new InvalidOperationException("Confirm that you want to reopen the official results.");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A reason is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7303004)", ct);
        var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, ct) ?? throw new InvalidOperationException("Event not found.");
        if (expectedVersion is { } supplied && supplied != ev.Version) throw new InvalidOperationException("This event changed in another session. Reload before reopening it.");
        if (ev.State is not (EventState.Finalized or EventState.Archived)) throw new InvalidOperationException("Only finalized or archived results can be reopened.");
        if (ev.State == EventState.Archived || ev.State == EventState.Finalized)
        {
            var development = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
            var current = await db.Events.AsNoTracking().Where(x => x.Id != eventId && x.HiddenAt == null && (x.State == EventState.Live || x.State == EventState.AwaitingFinalReview || x.State == EventState.Finalized) && !(development && x.IsDevelopmentFixture)).OrderBy(x => x.Name).Select(x => x.Name).FirstOrDefaultAsync(ct);
            if (current is not null) throw new InvalidOperationException($"{current} is already the current event. Archive it before reopening this event.");
        }
        var active = await db.EventFinalizations.Where(x => x.EventId == eventId && x.UnfinalizedAt == null).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("No active official result exists.");
        var now = time.GetUtcNow();
        var from = ev.State;
        active.Unfinalize(now, actor.Id, reason);
        ev.Unfinalize(reason);
        AddLifecycleHistory(ev, from, actor, "event.unfinalized", reason.Trim(), now);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task ArchiveAsync(Guid eventId, bool confirmed, LifecycleActor actor, CancellationToken ct = default)
    {
        if (!confirmed) throw new InvalidOperationException("Confirm that you want to archive this finalized event.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7303004)", ct);
        var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, ct) ?? throw new InvalidOperationException("Event not found.");
        if (ev.State == EventState.Archived) { await tx.CommitAsync(ct); return; }
        if (ev.State != EventState.Finalized) throw new InvalidOperationException("Only finalized results can be archived.");
        var now = time.GetUtcNow();
        var from = ev.State;
        ev.Archive(now);
        AddLifecycleHistory(ev, from, actor, "event.archived", "Official event archived", now);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task AddResultNotificationsAsync(BingoEvent ev, DateTimeOffset now, CancellationToken ct)
    {
        var owners = await db.EventParticipants.Where(x => x.EventId == ev.Id && x.AccountId != null).Select(x => x.AccountId!.Value).Distinct().ToListAsync(ct);
        foreach (var owner in owners)
        {
            var id = DeterministicId("event-results", ev.Id, owner);
            if (!await db.PersonalNotifications.AnyAsync(x => x.Id == id, ct)) db.PersonalNotifications.Add(new PersonalNotification(id, owner, "event.results_published", $"Official results are available for {ev.Name}.", $"/Events/{Uri.EscapeDataString(ev.Slug)}/Board", now, ev.Id));
        }
    }

    private void AddLifecycleHistory(BingoEvent ev, EventState from, LifecycleActor actor, string action, string detail, DateTimeOffset now)
    {
        db.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), ev.Id, from, ev.State, actor.Id, now, detail, effectiveAt: now));
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actor.Id, actor.Username, action, "event", ev.Id.ToString(), detail, ev.Id, JsonSerializer.Serialize(new { state = from }), JsonSerializer.Serialize(new { state = ev.State })));
    }

    private static Guid DeterministicId(string purpose, Guid target, Guid recipient)
    { var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}:{target:N}:{recipient:N}")); return new Guid(bytes[..16]); }
    private static string CompletionAcknowledgementKey(Guid teamId) => $"completion-time-inspected-{teamId:N}";
    private static string BlockerKey(string prefix, IEnumerable<Guid> ids) { var value = string.Join(',', ids.OrderBy(x => x)); var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16]; return $"{prefix}-{hash}"; }
}
