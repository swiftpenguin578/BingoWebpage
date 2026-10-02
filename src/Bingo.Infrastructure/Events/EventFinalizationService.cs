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

public sealed class EventFinalizationService(ApplicationDbContext db, IPublicBoardService publicBoards, TimeProvider time, IProgressNotifier? progressNotifier = null, IEventCompetitionSynchronizationService? competitionSynchronization = null) : IEventFinalizationService
{
    private static readonly string[] CompetitiveInputNames = ["board completion", "completion time", "completed lines", "completed tiles", "current score time", "EHB"];

    public async Task<FinalReviewReadiness?> GetReadinessAsync(Guid eventId, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is not null) return await ReadReadinessAsync(eventId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var readiness = await ReadReadinessAsync(eventId, ct);
        await tx.CommitAsync(ct);
        return readiness;
    }

    private async Task<FinalReviewReadiness?> ReadReadinessAsync(Guid eventId, CancellationToken ct)
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
        else if (ev.AcceptsNewSubmissions(now) && ev.State == EventState.AwaitingFinalReview) blockers.Add(new("submission-window", "Submission window is still open", $"Captains can submit until {TimeZoneInfo.ConvertTime(effectiveCutoff.Value, TimeZoneInfo.FindSystemTimeZoneById(ev.Timezone)):dd MMM yyyy, HH:mm} local time.", null, true, false, null));
        var pending = await db.Submissions.AsNoTracking().Where(x => x.EventId == eventId && x.Status == SubmissionStatus.Pending).Select(x => x.Id).ToListAsync(ct);
        if (pending.Count > 0) blockers.Add(new(BlockerKey("pending-submissions", pending), "Pending submissions", $"{pending.Count} submission(s) still need a decision.", $"/Admin/Review?eventId={eventId:D}&status=Pending", true, false, null));

        var placements = new List<ProvisionalPlacement>();
        if (boardView is not null)
        {
            var unranked = boardView.Teams.Select(team =>
            {
                return new UnrankedTeamProgress(team.TeamId, team.TeamName, team.Progress);
            }).ToList();
            var ranked = PublicProgressCalculator.Rank(unranked);
            placements = ranked.Select(value =>
            {
                return new ProvisionalPlacement(value.TeamId, value.TeamName, value.Rank, value.Progress.BoardComplete,
                    value.Progress.BoardCompletedAt, null,
                    value.Progress.CompletedRows.Count + value.Progress.CompletedColumns.Count,
                    value.Progress.CompletedTiles, value.Progress.EhbTiebreak, value.Progress.CurrentScoreReachedAt);
            }).ToList();
            if (placements.Count == 0)
                blockers.Add(new("calculated-placements", "Calculated placements required", "The published board has no active teams to rank. Finalize the roster and recalculate the board before publishing official results.", "/Admin/Events/Board/" + eventId, false, false, null));
            else if (placements.Any(x => x.TeamId == Guid.Empty || x.Placement < 1) || placements.Select(x => x.TeamId).Distinct().Count() != placements.Count)
                blockers.Add(new("calculated-placements", "Calculated placements are invalid", "The current board projection returned duplicate or incomplete team identities. Correct the retained board and try again.", "/Admin/Events/Board/" + eventId, false, false, null));
        }

        var finalIds = finals.Select(x => x.Id).ToList();
        var official = finalIds.Count == 0 ? [] : await db.OfficialPlacements.AsNoTracking().Where(x => finalIds.Contains(x.FinalizationId)).OrderBy(x => x.Placement).ThenBy(x => x.TeamName).ToListAsync(ct);
        var history = finals.Select(f => new FinalizationHistoryRow(f.Id, f.Version, f.FinalizedAt, f.UnfinalizedAt is null, f.UnfinalizedAt, f.UnfinalizeReason, official.Where(x => x.FinalizationId == f.Id).Select(x => new OfficialPlacementRow(x.Placement, x.TeamName, x.BoardComplete, x.BoardCompletedAt, x.CompletedLines, x.CompletedTiles, x.EhbTiebreak, x.CurrentScoreReachedAt)).ToList())).ToList();
        if (activeFinal is not null && (ev.State is EventState.Finalized or EventState.Archived)) placements = official.Where(x => x.FinalizationId == activeFinal.Id).Select(x => new ProvisionalPlacement(x.TeamId, x.TeamName, x.Placement, x.BoardComplete, x.BoardCompletedAt, null, x.CompletedLines, x.CompletedTiles, x.EhbTiebreak, x.CurrentScoreReachedAt)).ToList();
        return new(eventId, ev.Name, ev.State, scheduledStart, scheduledEnd, effectiveCutoff, ev.AcceptsNewSubmissions(now), blockers, placements, history, cycleId, ev.Version);
    }

    public Task ResolveBlockerAsync(Guid eventId, string blockerKey, string reason, bool confirmed, Guid adminId, long? expectedVersion = null, Guid? expectedReviewCycleId = null, CancellationToken ct = default)
        => throw new InvalidOperationException("Final-review overrides are retired. Resolve the underlying blocker before publishing official results.");

    public Task AcknowledgeCompletionTimeAsync(Guid eventId, Guid teamId, Guid adminId, long? expectedVersion = null, Guid? expectedReviewCycleId = null, string? expectedInspectionKey = null, CancellationToken ct = default)
        => throw new InvalidOperationException("Completion-time inspection is retired. Calculated completion facts are authoritative.");

    public Task CorrectCompletionAsync(Guid eventId, Guid teamId, DateTimeOffset correctedAt, string reason, Guid adminId, long? expectedVersion = null, Guid? expectedReviewCycleId = null, CancellationToken ct = default)
        => throw new InvalidOperationException("Manual completion-time corrections are retired. Calculated completion facts are authoritative.");

    public async Task<FinalizationOperationResult> FinalizeAsync(Guid eventId, LifecycleActor actor, long? expectedVersion = null, CancellationToken ct = default)
    {
        string? operationFeedback = null;
        try
        {
            actor = await EventMutationAuthorization.EnsureAuthorizedAsync(db, actor, ct);
            if (expectedVersion is not > 0) throw new InvalidOperationException("This final-review form is stale or incomplete. Reload before finalizing.");
            var preflight = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, ct)
                ?? throw new InvalidOperationException("Event not found.");
            var hasActiveFinalization = await db.EventFinalizations.AsNoTracking()
                .AnyAsync(x => x.EventId == eventId && x.UnfinalizedAt == null, ct);
            var archivedRetry = preflight.State == EventState.Archived && hasActiveFinalization && expectedVersion == preflight.Version - 1;
            if (preflight.Version != expectedVersion && !archivedRetry) throw new InvalidOperationException("This event changed in another session. Reload before finalizing.");
            EventCompetitionRefreshResult? refreshResult = null;
            Exception? refreshFailure = null;
            if (!archivedRetry && preflight.State == EventState.AwaitingFinalReview && competitionSynchronization is not null)
            {
                // The fetch owns its own short lease transaction and HTTP request.
                // A skip or provider failure intentionally leaves publication available,
                // while its outcome is retained in the publication operation feedback.
                try { refreshResult = await competitionSynchronization.RefreshForFinalReviewAsync(eventId, ct); }
                catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) { refreshFailure = ex; }
                catch (Exception ex) { refreshFailure = ex; }
            }

            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            actor = await EventMutationAuthorization.EnsureAuthorizedAsync(db, actor, ct);
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7303004)", ct);
            var ev = await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} AND hidden_at IS NULL FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new InvalidOperationException("Event not found.");
            var activeFinal = await db.EventFinalizations.Where(x => x.EventId == eventId && x.UnfinalizedAt == null).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            if (ev.State == EventState.Archived && activeFinal is not null && expectedVersion == ev.Version - 1) { await tx.CommitAsync(ct); return new(true, true); }
            // A pre-existing Finalized row is a legacy resting state from the
            // old two-step workflow. It may only be completed when its
            // immutable official snapshot is already present; never recalculate
            // or create a replacement snapshot during this compatibility path.
            if (ev.State == EventState.Finalized)
            {
                if (activeFinal is null || !ev.ResultsPublished)
                    throw new InvalidOperationException("This legacy Finalized event has no complete official snapshot. Resolve the retained history through the controlled rollout procedure.");
                if (expectedVersion != ev.Version) throw new InvalidOperationException("This event changed in another session. Reload before completing its legacy publication.");
                var legacyNow = time.GetUtcNow();
                ev.Archive(legacyNow);
                ev.AdvanceVersion();
                AddLifecycleHistory(ev, EventState.Finalized, actor, "event.legacy_finalized_archived", "Retained official results completed the legacy Finalized transition", legacyNow);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new(true, false);
            }
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
            var ties = readiness.Placements.GroupBy(x => x.Placement).Where(x => x.Count() > 1).Select(group => new
            {
                Placement = group.Key,
                Teams = group.Select(x => new
                {
                    x.TeamId,
                    x.TeamName,
                    x.BoardComplete,
                    CompletedAt = x.CalculatedCompletedAt,
                    x.CompletedLines,
                    x.CompletedTiles,
                    x.CurrentScoreReachedAt,
                    x.EhbTiebreak
                }).ToArray(),
                Explanation = "Shared rank: every competitive input is exactly equal; team name is presentation order only."
            }).ToArray();
            var calcInputs = JsonSerializer.Serialize(new
            {
                readiness.ReviewCycleId,
                readiness.EventStartsAt,
                readiness.EventEndsAt,
                readiness.SubmissionCutoff,
                competitiveInputs = CompetitiveInputNames,
                teams = readiness.Placements.Select(x => new { x.TeamId, x.TeamName, x.BoardComplete, x.CalculatedCompletedAt, x.CompletedLines, x.CompletedTiles, x.CurrentScoreReachedAt, x.EhbTiebreak }),
                exactTieExplanations = ties
            });
            var calcResults = JsonSerializer.Serialize(readiness.Placements);
            var snapshot = new EventFinalizationSnapshot(Guid.NewGuid(), eventId, version, now, actor.Id, readiness.ReviewCycleId, null, calcInputs, calcResults);
            db.EventFinalizations.Add(snapshot);
            foreach (var row in readiness.Placements) db.OfficialPlacements.Add(new OfficialPlacementSnapshot(Guid.NewGuid(), snapshot.Id, eventId, row.TeamId, row.TeamName, row.Placement, row.BoardComplete, row.CalculatedCompletedAt, row.CompletedLines, row.CompletedTiles, row.EhbTiebreak, row.CurrentScoreReachedAt));
            var from = ev.State;
            ev.ClearAnnouncements();
            ev.PublishOfficialResults(now);
            ev.AdvanceVersion();
            AddLifecycleHistory(ev, from, actor, "event.results_published", PublicationDetail(refreshResult, refreshFailure), now);
            operationFeedback = PublicationFeedback(refreshResult, refreshFailure);
            await AddResultNotificationsAsync(ev, now, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex) when (IsReviewPersistenceConflict(ex)) { throw new InvalidOperationException("This event changed in another session. Reload before finalizing."); }
        await NotifyProgressAsync(eventId, ct);
        return new(true, false, operationFeedback);
    }

    private static bool IsReviewPersistenceConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException or PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.UniqueViolation }) return true;
        return false;
    }

    public async Task UnfinalizeAsync(Guid eventId, string reason, bool confirmed, LifecycleActor actor, long? expectedVersion = null, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        actor = await EventMutationAuthorization.EnsureAuthorizedAsync(db, actor, ct);

        if (!confirmed) throw new InvalidOperationException("Confirm that you want to reopen the official results.");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A reason is required.");
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
        ev.AdvanceVersion();
        AddLifecycleHistory(ev, from, actor, "event.unfinalized", reason.Trim(), now);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    [Obsolete("Official publication now archives the event atomically; use FinalizeAsync.")]
    public Task ArchiveAsync(Guid eventId, bool confirmed, LifecycleActor actor, CancellationToken ct = default)
        => Task.FromException(new InvalidOperationException("The separate Archive action is retired. Publish official results from final review."));

    private async Task AddResultNotificationsAsync(BingoEvent ev, DateTimeOffset now, CancellationToken ct)
    {
        var owners = await db.EventParticipants.Where(x => x.EventId == ev.Id && x.AccountId != null).Select(x => x.AccountId!.Value).Distinct().ToListAsync(ct);
        foreach (var owner in owners)
        {
            var id = DeterministicId("event-results", ev.Id, owner);
            if (!await db.PersonalNotifications.AnyAsync(x => x.Id == id, ct)) db.PersonalNotifications.Add(new PersonalNotification(id, owner, "event.results_published", $"Official results are available for {ev.Name}.", $"/Events/{Uri.EscapeDataString(ev.Slug)}/Board", now, ev.Id));
        }
    }

    private static string? PublicationFeedback(EventCompetitionRefreshResult? refreshResult, Exception? refreshFailure)
    {
        const string published = "Official results were published.";
        if (refreshFailure is not null)
            return published + " Final-review competition refresh failed before completion.";
        if (refreshResult is { Succeeded: false })
        {
            var state = refreshResult.Skipped ? "skipped" : "failed";
            return published + $" Final-review competition refresh {state}: " +
                BoundedFeedback(refreshResult.Message ?? refreshResult.ErrorKind ?? "no detail");
        }
        if (refreshResult?.Message is { Length: > 0 } message)
            return published + " Final-review competition refresh note: " + BoundedFeedback(message);
        return null;
    }

    private static string PublicationDetail(EventCompetitionRefreshResult? refreshResult, Exception? refreshFailure)
    {
        const string published = "Official placements snapshotted and event archived";
        if (refreshFailure is not null)
            return published + ". Final-review competition refresh failed: " + BoundedFeedback(refreshFailure.Message);
        if (refreshResult is { Succeeded: false })
        {
            var state = refreshResult.Skipped ? "skipped" : "failed";
            return published + $". Final-review competition refresh {state}: " + BoundedFeedback(refreshResult.Message ?? refreshResult.ErrorKind ?? "no detail");
        }
        if (refreshResult?.Message is { Length: > 0 } message)
            return published + ". Final-review competition refresh note: " + BoundedFeedback(message);
        return published;
    }

    private static string BoundedFeedback(string? value)
    {
        var detail = string.IsNullOrWhiteSpace(value) ? "no detail" : value.Trim().Replace('\n', ' ').Replace('\r', ' ');
        return detail.Length <= 240 ? detail : detail[..240];
    }

    private void AddLifecycleHistory(BingoEvent ev, EventState from, LifecycleActor actor, string action, string detail, DateTimeOffset now)
    {
        db.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), ev.Id, from, ev.State, actor.Id, now, detail, effectiveAt: now));
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actor.Id, actor.Username, action, "event", ev.Id.ToString(), detail, ev.Id, JsonSerializer.Serialize(new { state = from }), JsonSerializer.Serialize(new { state = ev.State })));
    }

    private static Guid DeterministicId(string purpose, Guid target, Guid recipient)
    { var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}:{target:N}:{recipient:N}")); return new Guid(bytes[..16]); }
    private static string BlockerKey(string prefix, IEnumerable<Guid> ids)
    {
        var value = string.Join(',', ids.OrderBy(x => x));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];
        return $"{prefix}-{hash}";
    }

    private async Task NotifyProgressAsync(Guid eventId, CancellationToken ct)
    {
        if (progressNotifier is null) return;
        try { await progressNotifier.NotifyProgressChangedAsync(eventId, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { /* Finalization remains committed if connected clients disconnect. */ }
    }
}
