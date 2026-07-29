using System.Data;
using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Events;

public sealed class EventLifecycleService(
    ApplicationDbContext db,
    IEventSignupLifecycleService signupLifecycle,
    TimeProvider time) : IEventLifecycleService
{
    private static readonly EventState[] PreLiveStates = [EventState.Draft, EventState.SignupOpen, EventState.SignupClosed];
    private static readonly EventState[] CurrentStates = [EventState.Live, EventState.AwaitingFinalReview, EventState.Finalized];

    public async Task ProcessDueAsync(CancellationToken ct = default)
    {
        await signupLifecycle.ProcessDueSignupAsync(ct);
        var now = time.GetUtcNow();
        var dueStarts = await db.Events.AsNoTracking()
            .Where(x => PreLiveStates.Contains(x.State) && x.EventStartsAt <= now)
            .Select(x => x.Id)
            .ToListAsync(ct);
        foreach (var eventId in dueStarts) await ExecuteScheduledStartAsync(eventId, now, ct);

        var dueEnds = await db.Events.AsNoTracking()
            .Where(x => x.State == EventState.Live && x.EventEndsAt <= now)
            .Select(x => x.Id)
            .ToListAsync(ct);
        foreach (var eventId in dueEnds) await ExecuteScheduledEndAsync(eventId, now, ct);
    }

    public async Task<EventStartReadiness?> GetStartReadinessAsync(Guid eventId, CancellationToken ct = default)
    {
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId, ct);
        return item is null ? null : new EventStartReadiness(await EvaluateStartAsync(item, ct));
    }

    public async Task<EventStartResult> StartNowAsync(Guid eventId, long version, bool confirmed, string? reason, LifecycleActor actor, CancellationToken ct = default)
    {
        if (!confirmed) return new(false, "Confirm that you want to start the event.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await LockCurrentBoundaryAsync(ct);
            var item = await EventAsync(eventId, version, ct);
            var now = time.GetUtcNow();
            if (item.EventStartsAt is { } scheduledFor && now < scheduledFor && string.IsNullOrWhiteSpace(reason))
                return new(false, "Enter a reason when starting the event before its configured start.");
            var blockers = await EvaluateStartAsync(item, ct);
            if (blockers.Count > 0) return new(false, string.Join(" ", blockers.Select(x => x.Description)), blockers);
            var from = item.State;
            item.StartEvent(now);
            var unresolved = await db.ScheduledEventStartAttempts.Where(x => x.EventId == eventId && x.ResolvedAt == null && !x.Started).ToListAsync(ct);
            foreach (var attempt in unresolved) attempt.Resolve(now);
            AddTransitionAndAudit(item, from, actor.Id, actor.Username, false, "event.started", reason, now);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(true);
        }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(ct); return new(false, "This event changed while it was being started. Review its current state and try again."); }
        catch (InvalidOperationException ex) { await tx.RollbackAsync(ct); return new(false, ex.Message); }
        catch (DbUpdateException) { await tx.RollbackAsync(ct); return new(false, "The event could not be started. Review its current state and try again."); }
    }

    public async Task<EventStartResult> EndNowAsync(Guid eventId, long version, bool confirmed, string? reason, LifecycleActor actor, CancellationToken ct = default)
    {
        if (!confirmed) return new(false, "Confirm that you want to end the event.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var item = await EventAsync(eventId, version, ct);
            var now = time.GetUtcNow();
            if (item.EventEndsAt is { } scheduledEnd && now < scheduledEnd && string.IsNullOrWhiteSpace(reason))
                return new(false, "Enter a reason when ending the event before its configured end.");
            var from = item.State;
            item.EndEvent(now);
            AddTransitionAndAudit(item, from, actor.Id, actor.Username, false, "event.ended", reason, now);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(true);
        }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(ct); return new(false, "This event changed while it was being ended. Review its current state and try again."); }
        catch (InvalidOperationException ex) { await tx.RollbackAsync(ct); return new(false, ex.Message); }
        catch (DbUpdateException) { await tx.RollbackAsync(ct); return new(false, "The event could not be ended. Review its current state and try again."); }
    }

    private async Task ExecuteScheduledStartAsync(Guid eventId, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await LockCurrentBoundaryAsync(ct);
            var item = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId, ct);
            if (item is null || !PreLiveStates.Contains(item.State) || item.EventStartsAt is not { } scheduledFor || scheduledFor > now)
                return;
            if (await db.ScheduledEventStartAttempts.AnyAsync(x => x.EventId == eventId && x.ScheduledFor == scheduledFor, ct))
                return;
            var blockers = await EvaluateStartAsync(item, ct);
            if (blockers.Count == 0)
            {
                var from = item.State;
                item.StartEvent(now);
                db.ScheduledEventStartAttempts.Add(new(Guid.NewGuid(), eventId, scheduledFor, now, true, []));
                AddTransitionAndAudit(item, from, null, "System", true, "event.started_automatically", null, now);
            }
            else
            {
                db.ScheduledEventStartAttempts.Add(new(Guid.NewGuid(), eventId, scheduledFor, now, false, blockers.Select(x => x.Code)));
                db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, null, "System", "event.start_postponed", "event", eventId.ToString(), JsonSerializer.Serialize(new { scheduledFor, blockerCodes = blockers.Select(x => x.Code) }), eventId));
                await NotifyAdminsAsync("Automatic start postponed", $"{item.Name}: {string.Join(" ", blockers.Select(x => x.Description))}", $"/Admin/Events/Manage/{eventId}", now, ct);
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
        }
    }

    private async Task ExecuteScheduledEndAsync(Guid eventId, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var item = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId, ct);
            if (item is null || item.State != EventState.Live || item.EventEndsAt is not { } scheduledEnd || scheduledEnd > now)
                return;
            var from = item.State;
            item.EndEvent(scheduledEnd);
            AddTransitionAndAudit(item, from, null, "System", true, "event.ended_automatically", null, now);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
        }
    }

    private async Task<List<ReadinessItem>> EvaluateStartAsync(BingoEvent item, CancellationToken ct)
    {
        var blockers = new List<ReadinessItem>();
        if (item.State == EventState.Draft)
            blockers.Add(new("LIFECYCLE_STATE_INVALID", "Signup has not been opened and closed. Open signup, then close it before starting the event.", $"/Admin/Events/Manage/{item.Id}"));
        else if (item.State == EventState.SignupOpen)
            blockers.Add(new("LIFECYCLE_STATE_INVALID", "Signup is still open. Close signup before starting the event.", $"/Admin/Events/Manage/{item.Id}"));
        else if (item.State != EventState.SignupClosed)
            blockers.Add(new("LIFECYCLE_STATE_INVALID", "The event is not in a pre-live state that can start.", $"/Admin/Events/Manage/{item.Id}"));
        if (item.EventStartsAt is null || item.EventEndsAt is null || item.EventEndsAt <= item.EventStartsAt)
            blockers.Add(new("SCHEDULE_INVALID", "Configure a valid event start and end.", $"/Admin/Events/Schedule/{item.Id}"));
        if (!await db.DraftSessions.AsNoTracking().AnyAsync(x => x.EventId == item.Id && x.State == DraftState.Finalized, ct))
            blockers.Add(new("DRAFT_NOT_FINALIZED", "Finalize the team draft before starting.", $"/Admin/Events/Draft/{item.Id}"));
        if (!await db.Boards.AsNoTracking().AnyAsync(x => x.EventId == item.Id && x.State == BoardState.Published, ct))
            blockers.Add(new("BOARD_NOT_PUBLISHED", "Publish the board before starting.", $"/Admin/Events/Board/{item.Id}"));

        var activeTeams = await db.Teams.AsNoTracking().Where(x => x.EventId == item.Id && x.Active).Select(x => new { x.Id, x.Name }).ToListAsync(ct);
        var activeTeamIds = activeTeams.Select(team => team.Id).ToArray();
        var captainTeams = await (from membership in db.TeamMemberships.AsNoTracking()
                                  join participant in db.EventParticipants.AsNoTracking() on membership.EventParticipantId equals participant.Id
                                  join account in db.Accounts.AsNoTracking() on participant.AccountId equals account.Id
                                  where membership.LeftAt == null && membership.Role == TeamMembershipRole.Captain && activeTeamIds.Contains(membership.TeamId) &&
                                        participant.EventId == item.Id && account.Active && account.AccountType == AccountType.WebsiteAccount
                                  select membership.TeamId).Distinct().ToListAsync(ct);
        var emergencyTeams = await (from access in db.AccountEventAccesses.AsNoTracking()
                                    join account in db.Accounts.AsNoTracking() on access.AccountId equals account.Id
                                    where access.EventId == item.Id && access.Enabled && account.Active && account.AccountType == AccountType.EmergencyCaptain
                                    select access.TeamId).Distinct().ToListAsync(ct);
        foreach (var team in activeTeams.Where(x => !captainTeams.Contains(x.Id) && !emergencyTeams.Contains(x.Id)))
            blockers.Add(new("TEAM_ACCESS_MISSING", $"{team.Name} needs a current Captain or enabled emergency credential.", $"/Admin/Events/Teams/{item.Id}"));

        var developmentMode = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
        var current = await db.Events.AsNoTracking()
            .Where(x => x.Id != item.Id && CurrentStates.Contains(x.State) && !(developmentMode && x.IsDevelopmentFixture))
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.State })
            .FirstOrDefaultAsync(ct);
        if (current is not null)
            blockers.Add(new("CURRENT_EVENT_EXISTS", $"{current.Name} is already the current {ReadableState(current.State)} event.", $"/Admin/Events/Manage/{current.Id}"));
        return blockers;
    }

    private async Task<BingoEvent> EventAsync(Guid eventId, long version, CancellationToken ct)
    {
        var item = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId, ct) ?? throw new InvalidOperationException("Event not found.");
        if (item.Version != version) throw new DbUpdateConcurrencyException();
        return item;
    }

    private async Task LockCurrentBoundaryAsync(CancellationToken ct) =>
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7303004)", ct);

    private async Task NotifyAdminsAsync(string title, string detail, string route, DateTimeOffset now, CancellationToken ct)
    {
        var recipients = await db.Accounts.AsNoTracking()
            .Where(x => x.Active && x.AccountType == AccountType.WebsiteAccount && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin))
            .Select(x => x.Id)
            .ToListAsync(ct);
        foreach (var recipient in recipients)
            db.PersonalNotifications.Add(new PersonalNotification(Guid.NewGuid(), recipient, title, detail, route, now));
    }

    private void AddTransitionAndAudit(BingoEvent item, EventState from, Guid? actorId, string actorName, bool scheduled, string action, string? reason, DateTimeOffset now)
    {
        db.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), item.Id, from, item.State, actorId, now, reason, scheduled));
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actorId, actorName, action, "event", item.Id.ToString(), reason, item.Id, JsonSerializer.Serialize(new { state = from }), JsonSerializer.Serialize(new { state = item.State, item.ActualStartedAt, item.ActualEndedAt })));
    }

    private static string ReadableState(EventState state) => state switch
    {
        EventState.AwaitingFinalReview => "final-review",
        EventState.Finalized => "finalized",
        _ => "live"
    };
}
