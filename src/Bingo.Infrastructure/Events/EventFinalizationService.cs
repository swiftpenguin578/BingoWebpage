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

namespace Bingo.Infrastructure.Events;

public sealed class EventFinalizationService(ApplicationDbContext db, IPublicBoardService publicBoards, TimeProvider time) : IEventFinalizationService
{
    public async Task<FinalReviewReadiness?> GetReadinessAsync(Guid eventId, CancellationToken ct = default)
    {
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId, ct); if (ev is null) return null;
        var now = time.GetUtcNow();
        var scheduledStart = ev.ActualStartedAt ?? ev.EventStartsAt;
        var scheduledEnd = ev.ActualEndedAt ?? ev.EventEndsAt;
        var effectiveCutoff = ev.ReopenedSubmissionCutoffAt is { } reopened && (ev.SubmissionCutoffAt is null || reopened > ev.SubmissionCutoffAt.Value)
            ? reopened
            : ev.SubmissionCutoffAt;
        var blockers = new List<FinalReviewBlocker>();
        if (ev.State != EventState.AwaitingFinalReview && ev.State is not (EventState.Finalized or EventState.Archived)) blockers.Add(new("event-state", "Event has not ended", "End the live event before final review.", null, false, false, null));
        var boardView = await publicBoards.GetEventBoardAsync(ev.Slug, ct);
        if (boardView is null) blockers.Add(new("published-board", "Published board and teams required", "The event needs a published board and finalized active teams.", $"/Admin/Events/Board/{eventId}", false, false, null));
        if (effectiveCutoff is null) blockers.Add(new("submission-cutoff", "Submission cutoff required", "Configure a submission cutoff before final review.", null, false, false, null));
        else if (now <= effectiveCutoff && ev.State == EventState.AwaitingFinalReview) blockers.Add(new("submission-window", "Submission window is still open", $"Captains can submit until {effectiveCutoff.Value.ToLocalTime():g} local time.", null, true, false, null));
        var pending = await db.Submissions.AsNoTracking().Where(x => x.EventId == eventId && x.Status == SubmissionStatus.Pending).Select(x => x.Id).ToListAsync(ct);
        if (pending.Count > 0) blockers.Add(new(BlockerKey("pending-submissions", pending), "Pending submissions", $"{pending.Count} submission(s) still need a decision.", "/Admin/Review?status=Pending", true, false, null));

        var corrections = await db.TeamCompletionCorrections.AsNoTracking().Where(x => x.EventId == eventId).ToDictionaryAsync(x => x.TeamId, ct);
        var placements = new List<ProvisionalPlacement>();
        if (boardView is not null)
        {
            var unranked = boardView.Teams.Select(team => new UnrankedTeamProgress(team.TeamId, team.TeamName,
                corrections.TryGetValue(team.TeamId, out var correction) && team.Progress.BoardComplete ? team.Progress with { BoardCompletedAt = correction.CorrectedCompletedAt } : team.Progress)).ToList();
            var ranked = PublicProgressCalculator.Rank(unranked);
            placements = ranked.Select(value => { var original = boardView.Teams.Single(x => x.TeamId == value.TeamId); return new ProvisionalPlacement(value.TeamId, value.TeamName, value.Rank, value.Progress.BoardComplete, original.Progress.BoardCompletedAt, corrections.GetValueOrDefault(value.TeamId)?.CorrectedCompletedAt, value.Progress.CompletedRows.Count + value.Progress.CompletedColumns.Count, value.Progress.CompletedTiles, value.Progress.EhbTiebreak); }).ToList();
            foreach (var tie in placements.GroupBy(x => x.Placement).Where(x => x.Count() > 1)) blockers.Add(new(BlockerKey($"placement-tie-{tie.Key}", tie.Select(x => x.TeamId)), $"Tie at placement {tie.Key}", $"{string.Join(", ", tie.Select(x => x.TeamName))} currently have identical ranking values. Confirm the tie or correct a completion time.", null, true, false, null));
        }
        var resolutions = await db.FinalReviewResolutions.AsNoTracking().Where(x => x.EventId == eventId).OrderByDescending(x => x.ResolvedAt).ToListAsync(ct);
        blockers = blockers.Select(blocker => { var resolution = resolutions.FirstOrDefault(x => x.BlockerKey == blocker.Key); return resolution is null ? blocker : blocker with { Resolved = true, ResolutionReason = resolution.Reason }; }).ToList();
        var finals = await db.EventFinalizations.AsNoTracking().Where(x => x.EventId == eventId).OrderByDescending(x => x.Version).ToListAsync(ct); var finalIds = finals.Select(x => x.Id).ToList(); var official = await db.OfficialPlacements.AsNoTracking().Where(x => finalIds.Contains(x.FinalizationId)).OrderBy(x => x.Placement).ThenBy(x => x.TeamName).ToListAsync(ct);
        var history = finals.Select(f => new FinalizationHistoryRow(f.Id, f.Version, f.FinalizedAt, f.UnfinalizedAt is null, f.UnfinalizedAt, f.UnfinalizeReason, official.Where(x => x.FinalizationId == f.Id).Select(x => new OfficialPlacementRow(x.Placement, x.TeamName, x.BoardComplete, x.BoardCompletedAt, x.CompletedLines, x.CompletedTiles, x.EhbTiebreak)).ToList())).ToList();
        var activeFinal = finals.FirstOrDefault(x => x.UnfinalizedAt is null); if (activeFinal is not null && (ev.State is EventState.Finalized or EventState.Archived)) placements = official.Where(x => x.FinalizationId == activeFinal.Id).Select(x => new ProvisionalPlacement(x.TeamId, x.TeamName, x.Placement, x.BoardComplete, x.BoardCompletedAt, null, x.CompletedLines, x.CompletedTiles, x.EhbTiebreak)).ToList();
        return new(eventId, ev.Name, ev.State, scheduledStart, scheduledEnd, effectiveCutoff, effectiveCutoff is not null && now <= effectiveCutoff, blockers, placements, history);
    }

    public async Task ResolveBlockerAsync(Guid eventId, string blockerKey, string reason, Guid adminId, CancellationToken ct = default)
    { var readiness = await GetReadinessAsync(eventId, ct) ?? throw new InvalidOperationException("Event not found."); var blocker = readiness.Blockers.SingleOrDefault(x => x.Key == blockerKey) ?? throw new InvalidOperationException("This item has already resolved automatically."); if (!blocker.CanOverride) throw new InvalidOperationException("This item must be fixed rather than overridden."); if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A reason is required."); db.FinalReviewResolutions.Add(new FinalReviewResolution(Guid.NewGuid(), eventId, blocker.Key, blocker.Description, reason, adminId, time.GetUtcNow())); await db.SaveChangesAsync(ct); }

    public async Task CorrectCompletionAsync(Guid eventId, Guid teamId, DateTimeOffset correctedAt, string reason, Guid adminId, CancellationToken ct = default)
    { var readiness = await GetReadinessAsync(eventId, ct) ?? throw new InvalidOperationException("Event not found."); var team = readiness.Placements.SingleOrDefault(x => x.TeamId == teamId) ?? throw new InvalidOperationException("Team not found."); if (!team.BoardComplete) throw new InvalidOperationException("Only a completed board has a completion time."); if (readiness.EventStartsAt is not { } startsAt || readiness.EventEndsAt is not { } endsAt || correctedAt < startsAt || correctedAt > endsAt) throw new InvalidOperationException("The corrected completion time must be within the event window."); var row = await db.TeamCompletionCorrections.SingleOrDefaultAsync(x => x.EventId == eventId && x.TeamId == teamId, ct); if (row is null) db.TeamCompletionCorrections.Add(new TeamCompletionCorrection(Guid.NewGuid(), eventId, teamId, correctedAt, reason, adminId, time.GetUtcNow())); else row.Update(correctedAt, reason, adminId, time.GetUtcNow()); await db.SaveChangesAsync(ct); }

    public async Task FinalizeAsync(Guid eventId, LifecycleActor actor, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7303004)", ct);
        var development = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
        var current = await db.Events.AsNoTracking()
            .Where(value => value.Id != eventId && (value.State == EventState.Live || value.State == EventState.AwaitingFinalReview || value.State == EventState.Finalized) && !(development && value.IsDevelopmentFixture))
            .OrderBy(value => value.Name)
            .Select(value => value.Name)
            .FirstOrDefaultAsync(ct);
        if (current is not null)
            throw new InvalidOperationException($"{current} is already the current event. Archive it before finalizing this event.");
        var readiness = await GetReadinessAsync(eventId, ct) ?? throw new InvalidOperationException("Event not found.");
        if (!readiness.CanFinalize) throw new InvalidOperationException("Resolve every final-review item before finalizing.");
        var ev = await db.Events.SingleAsync(x => x.Id == eventId, ct);
        var from = ev.State;
        var version = (await db.EventFinalizations.Where(x => x.EventId == eventId).MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
        var now = time.GetUtcNow();
        var snapshot = new EventFinalizationSnapshot(Guid.NewGuid(), eventId, version, now, actor.Id);
        db.EventFinalizations.Add(snapshot);
        foreach (var row in readiness.Placements)
            db.OfficialPlacements.Add(new OfficialPlacementSnapshot(Guid.NewGuid(), snapshot.Id, eventId, row.TeamId, row.TeamName, row.Placement, row.BoardComplete, row.CorrectedCompletedAt ?? row.CalculatedCompletedAt, row.CompletedLines, row.CompletedTiles, row.EhbTiebreak));
        ev.FinalizeResults(now);
        foreach (var access in await db.AccountEventAccesses.Where(x => x.EventId == eventId).ToListAsync(ct)) access.Disable();
        AddLifecycleHistory(ev, from, actor, "event.finalized", "Official placements snapshotted and published", now);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task UnfinalizeAsync(Guid eventId, string reason, bool confirmed, LifecycleActor actor, CancellationToken ct = default)
    {
        if (!confirmed) throw new InvalidOperationException("Confirm that you want to reopen the official results.");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A reason is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7303004)", ct);
        var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId, ct) ?? throw new InvalidOperationException("Event not found.");
        if (ev.State == EventState.Archived)
        {
            var development = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
            var current = await db.Events.AsNoTracking().Where(x => x.Id != eventId && (x.State == EventState.Live || x.State == EventState.AwaitingFinalReview || x.State == EventState.Finalized) && !(development && x.IsDevelopmentFixture)).OrderBy(x => x.Name).Select(x => x.Name).FirstOrDefaultAsync(ct);
            if (current is not null) throw new InvalidOperationException($"{current} is already the current event. Archive it before reopening this event.");
        }
        var active = await db.EventFinalizations.Where(x => x.EventId == eventId && x.UnfinalizedAt == null).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("No active official result exists.");
        var now = time.GetUtcNow();
        var from = ev.State;
        active.Unfinalize(now, actor.Id, reason);
        ev.Unfinalize(reason);
        db.FinalReviewResolutions.RemoveRange(await db.FinalReviewResolutions.Where(x => x.EventId == eventId).ToListAsync(ct));
        AddLifecycleHistory(ev, from, actor, "event.unfinalized", reason.Trim(), now);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task ArchiveAsync(Guid eventId, bool confirmed, LifecycleActor actor, CancellationToken ct = default)
    {
        if (!confirmed) throw new InvalidOperationException("Confirm that you want to archive this finalized event.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7303004)", ct);
        var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId, ct) ?? throw new InvalidOperationException("Event not found.");
        var now = time.GetUtcNow();
        var from = ev.State;
        ev.Archive(now);
        AddLifecycleHistory(ev, from, actor, "event.archived", "Official event archived", now);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private void AddLifecycleHistory(BingoEvent ev, EventState from, LifecycleActor actor, string action, string detail, DateTimeOffset now)
    {
        db.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), ev.Id, from, ev.State, actor.Id, now, detail));
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actor.Id, actor.Username, action, "event", ev.Id.ToString(), detail, ev.Id,
            JsonSerializer.Serialize(new { state = from }), JsonSerializer.Serialize(new { state = ev.State })));
    }
    private static string BlockerKey(string prefix, IEnumerable<Guid> ids) { var value = string.Join(',', ids.OrderBy(x => x)); var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16]; return $"{prefix}-{hash}"; }
}
