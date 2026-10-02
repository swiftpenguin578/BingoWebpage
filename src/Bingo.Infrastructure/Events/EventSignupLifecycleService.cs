using System.Data;
using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bingo.Infrastructure.Events;

public sealed class EventReadinessEvaluator(ApplicationDbContext db, IConfiguration configuration) : IEventReadinessEvaluator
{
    public async Task<SignupReadiness?> GetSignupReadinessAsync(Guid eventId, SignupOpeningMode mode, DateTimeOffset now, CancellationToken ct = default)
    {
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, ct);
        return item is null ? null : await EvaluateAsync(item, mode, now, ct);
    }

    public async Task<SignupReadiness?> GetSignupReadinessAsync(Guid eventId, SignupOpeningMode mode, DateTimeOffset now, EventScheduleValues proposedValues, CancellationToken ct = default)
    {
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, ct);
        if (item is null) return null;
        try { item.ConfigureSchedule(proposedValues.SignupOpensAt, proposedValues.SignupClosesAt, proposedValues.DraftAt, proposedValues.EventStartsAt, proposedValues.EventEndsAt, proposedValues.ParticipantCap); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return new SignupReadiness([new("PROPOSED_SCHEDULE_INVALID", ex.Message)], [], [], SignupCloseDecision.Evaluate(proposedValues.SignupClosesAt, proposedValues.DraftAt, proposedValues.EventStartsAt, now));
        }
        return await EvaluateAsync(item, mode, now, ct);
    }

    private async Task<SignupReadiness> EvaluateAsync(BingoEvent item, SignupOpeningMode mode, DateTimeOffset now, CancellationToken ct)
    {
        var blockers = new List<ReadinessItem>();
        var warnings = new List<ReadinessItem>();
        var later = new List<ReadinessItem>();
        now = now.ToUniversalTime();
        if (string.IsNullOrWhiteSpace(item.Description)) blockers.Add(new("DESCRIPTION_REQUIRED", "Add a public event description before opening signup."));
        if (item.ParticipantCap is not > 0) blockers.Add(new("PARTICIPANT_CAP_REQUIRED", "Set a participant capacity greater than zero before opening signup."));
        if (item.EventStartsAt is null) blockers.Add(new("EVENT_START_REQUIRED", "Set an event start before opening signup."));
        if (item.EventEndsAt is null) blockers.Add(new("EVENT_END_REQUIRED", "Set an event end before opening signup."));
        if (item.EventStartsAt is { } start && item.EventEndsAt is { } end && end <= start) blockers.Add(new("EVENT_WINDOW_INVALID", "Event end must be after event start."));
        if (mode is SignupOpeningMode.ScheduleOpening or SignupOpeningMode.ScheduledExecution)
        {
            if (item.State != EventState.Draft) blockers.Add(new("LIFECYCLE_STATE_INVALID", "A future signup opening can only be saved for a private draft."));
            if (item.SignupClosesAt is null) blockers.Add(new("SIGNUP_CLOSE_REQUIRED", "Set a signup closing time before saving a future signup opening."));
            else if (item.SignupClosesAt <= now) blockers.Add(new("SIGNUP_CLOSE_NOT_FUTURE", "Signup closing must be in the future."));
            if (item.SignupClosesAt is { } closes && item.EventStartsAt is { } eventStart && closes > eventStart) blockers.Add(new("SIGNUP_CLOSE_AFTER_EVENT_START", "Signup closing must be no later than event start."));
            if (item.SignupOpensAt is null || (mode == SignupOpeningMode.ScheduleOpening && item.SignupOpensAt <= now)) blockers.Add(new("SCHEDULED_OPENING_INVALID", "A scheduled signup opening must be configured."));
            if (item.SignupOpensAt is { } opens && item.SignupClosesAt is { } scheduledClose && scheduledClose <= opens) blockers.Add(new("SCHEDULED_WINDOW_INVALID", "Signup closing must be after the scheduled opening."));
        }
        if (mode == SignupOpeningMode.OpenNow && item.State != EventState.Draft) blockers.Add(new("LIFECYCLE_STATE_INVALID", "Signups can only be opened from a private draft."));
        if (mode == SignupOpeningMode.Reopen && (item.State != EventState.SignupClosed || item.DraftLocked)) blockers.Add(new("LIFECYCLE_STATE_INVALID", "Signups can only reopen from an unlocked signup-closed event."));
        if (item.DraftLocked) blockers.Add(new("DRAFT_LOCKED", "Signups cannot change after the draft is locked."));
        if (string.IsNullOrWhiteSpace(configuration["DiscordAuthentication:ClientId"]) || string.IsNullOrWhiteSpace(configuration["DiscordAuthentication:ClientSecret"])) blockers.Add(new("DISCORD_AUTH_UNAVAILABLE", "Discord authentication is not configured for participant signup."));
        var form = await db.SignupForms.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == item.Id, ct);
        if (form is null) blockers.Add(new("SIGNUP_FORM_MISSING", "Create the event signup form before opening signup."));
        if (item.RequireSignupCode && string.IsNullOrWhiteSpace(item.SignupCodeHash)) blockers.Add(new("SIGNUP_CODE_UNUSABLE", "Signup-code protection is enabled without a usable code."));

        var questions = await db.SignupQuestions.AsNoTracking().Where(x => x.EventId == item.Id && x.Active).ToListAsync(ct);
        var primary = questions.Where(x => x.SystemField == SignupSystemField.PrimaryRegularAccount).ToList();
        var captain = questions.Where(x => x.SystemField == SignupSystemField.CaptainVolunteer).ToList();
        var invalidQuestion = questions.Any(question =>
            string.IsNullOrWhiteSpace(question.Key) || string.IsNullOrWhiteSpace(question.Label) ||
            !Enum.IsDefined(question.Type) || question.Position < 0 ||
            (question.Type == SignupQuestionType.SingleChoice && question.Options?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).Count() is not > 0) ||
            (question.Type == SignupQuestionType.Account && question.AccountAnswerRole is not (EventCharacterRole.Playing or EventCharacterRole.Informational)) ||
            (question.Type != SignupQuestionType.Account && question.AccountAnswerRole is not null) ||
            (question.Type == SignupQuestionType.Account && question.SystemField == SignupSystemField.None && question.Required));
        if (primary.Count != 1 || primary.SingleOrDefault() is not { Active: true, Type: SignupQuestionType.Account, Required: true, AccountAnswerRole: EventCharacterRole.Playing } ||
            captain.Count != 1 || captain.SingleOrDefault() is not { Active: true, Type: SignupQuestionType.YesNo } ||
            questions.Select(x => x.Position).Distinct().Count() != questions.Count || invalidQuestion)
            blockers.Add(new("SIGNUP_QUESTIONS_INVALID", "One or more existing signup questions are incomplete or invalid."));
        if (questions.Any(question => question.SystemField == SignupSystemField.None && question.Type == SignupQuestionType.Text && question.PublicOnSignupBoard))
            warnings.Add(new("PUBLIC_FREE_TEXT", "Answers to text questions will be public on the signup table."));
        if (mode == SignupOpeningMode.Reopen && await db.EventParticipants.AnyAsync(x => x.EventId == item.Id, ct)) warnings.Add(new("REOPENING_POPULATED_SIGNUP", "Reopening signup keeps the existing participant and signup history."));
        if (!await db.ActiveRosterPublications(item.Id).AnyAsync(ct)) later.Add(new("DRAFT_NOT_FINALIZED", "Team draft finalization is a later readiness task."));
        if (!await db.Boards.AnyAsync(x => x.EventId == item.Id && x.State == BoardState.Published, ct)) later.Add(new("BOARD_NOT_PUBLISHED", "Board publication is a later readiness task."));
        return new SignupReadiness(blockers, warnings, later, SignupCloseDecision.Evaluate(item.SignupClosesAt, item.DraftAt, item.EventStartsAt, now));
    }
}

public sealed class EventSignupLifecycleService(ApplicationDbContext db, IEventReadinessEvaluator readiness, TimeProvider time, ILogger<EventSignupLifecycleService>? logger = null) : IEventSignupLifecycleService
{
    public Task<SignupLifecycleResult> SaveScheduleAsync(Guid eventId, long version, EventScheduleValues values, bool confirmChanges, LifecycleActor actor, CancellationToken ct = default)
        => SaveScheduleAsync(eventId, version, values, confirmChanges, actor, null, ct);

    public async Task<SignupLifecycleResult> SaveScheduleAsync(Guid eventId, long version, EventScheduleValues values, bool confirmChanges, LifecycleActor actor, string? reason, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var authorizedActor = await EventMutationAuthorization.GetAuthorizedActorAsync(db, actor, ct);
            if (authorizedActor is null)
            {
                await tx.RollbackAsync(ct);
                return new(false, EventMutationAuthorization.UnauthorizedMessage);
            }
            actor = authorizedActor;

            await LockCurrentEventBoundary(ct);
            var item = await EventAsync(eventId, version, ct);
            var now = time.GetUtcNow();
            var draftState = await DraftStateAsync(item.Id, ct);
            var validationError = await ValidateScheduleChangeAsync(db, item, values, now, draftState, confirmChanges, reason, ct);
            if (validationError is not null) return new(false, validationError);
            var confirmedSignupCount = await SignupParticipants(item.Id)
                .CountAsync(participant => participant.SignupStatus == SignupStatus.Confirmed, ct);
            if (values.ParticipantCap is { } requestedCapacity && requestedCapacity < confirmedSignupCount)
                return new(false, $"The participant cap cannot be lower than the {confirmedSignupCount} confirmed participant(s).");
            var previousCapacity = item.ParticipantCap;
            var before = ScheduleState(item);
            if (item.State == EventState.Live)
                item.ChangeLiveEventEnd(values.EventEndsAt ?? throw new InvalidOperationException("The event end must be in the future."), now);
            else if (draftState == DraftState.Finalized)
                item.ConfigureFinalizedDraftEventWindow(values.EventStartsAt ?? throw new InvalidOperationException("An event start is required."), values.EventEndsAt ?? throw new InvalidOperationException("An event end is required."));
            else
                item.ConfigureSchedule(values.SignupOpensAt, values.SignupClosesAt, values.DraftAt, values.EventStartsAt, values.EventEndsAt, values.ParticipantCap);
            await db.SaveChangesAsync(ct);
            if (item.State == EventState.Draft && values.ScheduledSignupOpeningEnabled)
            {
                var scheduledReadiness = await readiness.GetSignupReadinessAsync(eventId, SignupOpeningMode.ScheduleOpening, now, ct) ?? throw new InvalidOperationException("Event not found.");
                if (item.SignupOpensAt > now)
                {
                    item.ConfigureScheduledSignupOpening(true, scheduledReadiness.Warnings.Select(x => x.Code));
                }
            }
            else if (item.State == EventState.Draft)
            {
                item.ConfigureScheduledSignupOpening(false, []);
            }
            var promoted = await PromoteForCapacityIncreaseAsync(item, previousCapacity, actor, ct);
            var after = ScheduleState(item);
            db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actor.Id, actor.Username, "event.schedule_updated", "event", item.Id.ToString(),
                item.State == EventState.Live && !string.IsNullOrWhiteSpace(reason) ? reason.Trim() : null,
                item.Id, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after)));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(true, null, null, promoted);
        }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(ct); return new(false, "This event changed while you were editing it. Review the latest values and try again."); }
        catch (InvalidOperationException ex) { await tx.RollbackAsync(ct); return new(false, ex.Message); }
        catch (DbUpdateException) { await tx.RollbackAsync(ct); return new(false, "The schedule update could not be saved. Try again."); }
        catch (Exception ex) { if (logger is not null) LogScheduleFailure(logger, eventId, ex); await tx.RollbackAsync(ct); return new(false, "The schedule update could not be completed. Try again."); }
    }

    public Task<SignupLifecycleResult> OpenAsync(Guid eventId, long version, IReadOnlyCollection<string> acknowledgedWarningCodes, bool acceptProposedClose, LifecycleActor actor, CancellationToken ct = default)
        => TransitionAsync(eventId, version, SignupOpeningMode.OpenNow, acknowledgedWarningCodes, acceptProposedClose, actor, ct);
    public Task<SignupLifecycleResult> ReopenAsync(Guid eventId, long version, IReadOnlyCollection<string> acknowledgedWarningCodes, bool acceptProposedClose, LifecycleActor actor, CancellationToken ct = default)
        => TransitionAsync(eventId, version, SignupOpeningMode.Reopen, acknowledgedWarningCodes, acceptProposedClose, actor, ct);

    public async Task ProcessDueSignupAsync(CancellationToken ct = default)
    {
        var now = time.GetUtcNow();
        var dueOpenings = await db.Events.AsNoTracking()
            .Where(x => x.HiddenAt == null && x.State == EventState.Draft && x.ScheduledSignupOpeningEnabled && x.SignupOpensAt <= now)
            .Select(x => x.Id)
            .ToListAsync(ct);
        foreach (var eventId in dueOpenings) await ExecuteScheduledOpeningAsync(eventId, now, ct);

        var dueClosings = await db.Events.AsNoTracking()
            .Where(x => x.HiddenAt == null && x.State == EventState.SignupOpen && x.SignupClosesAt <= now)
            .Select(x => x.Id)
            .ToListAsync(ct);
        foreach (var eventId in dueClosings) await ExecuteScheduledClosingAsync(eventId, now, ct);
    }

    private async Task ExecuteScheduledOpeningAsync(Guid eventId, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await LockCurrentEventBoundary(ct);
            var item = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, ct);
            if (item is null || item.State != EventState.Draft || !item.ScheduledSignupOpeningEnabled || item.SignupOpensAt is not { } scheduledFor || scheduledFor > now)
                return;
            if (await db.ScheduledSignupOpeningAttempts.AnyAsync(x => x.EventId == eventId && x.ScheduledFor == scheduledFor, ct))
                return;

            var evaluated = await readiness.GetSignupReadinessAsync(eventId, SignupOpeningMode.ScheduledExecution, now, ct)
                ?? throw new InvalidOperationException("Event not found.");
            var blockerCodes = evaluated.Blockers.Select(x => x.Code).ToList();
            var boundaryConflict = await CurrentEventBoundaryConflictAsync(db, item, ct);
            if (boundaryConflict is not null) blockerCodes.Add("EVENT_WINDOW_OVERLAP");

            if (blockerCodes.Count == 0)
            {
                var from = item.State;
                item.OpenSignups(now);
                item.MarkFirstPublic(now);
                var failedOpenings = await db.ScheduledSignupOpeningAttempts
                    .Where(x => x.EventId == eventId && !x.Opened && x.ResolvedAt == null)
                    .ToListAsync(ct);
                foreach (var failedOpening in failedOpenings)
                    failedOpening.Resolve(now);
                db.ScheduledSignupOpeningAttempts.Add(new(Guid.NewGuid(), eventId, scheduledFor, now, true, []));
                AddScheduledTransitionAndAudit(item, from, "event.signup_opened_automatically", JsonSerializer.Serialize(new { scheduledFor }), scheduledFor);
            }
            else
            {
                item.ConfigureScheduledSignupOpening(false, []);
                var descriptions = evaluated.Blockers.Select(x => x.Description)
                    .Concat(boundaryConflict is null ? [] : [boundaryConflict.Description])
                    .ToArray();
                db.ScheduledSignupOpeningAttempts.Add(new(Guid.NewGuid(), eventId, scheduledFor, now, false, blockerCodes, descriptions));
                db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, null, "System", "event.signup_opening_failed", "event", eventId.ToString(), JsonSerializer.Serialize(new { scheduledFor, blockerCodes }), eventId));
                await NotifyAdminsAsync(eventId, "Scheduled signup opening failed", $"{item.Name}: {string.Join(" ", descriptions)}", $"/Admin/Events/Manage/{eventId}", now, ct);
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

    private async Task ExecuteScheduledClosingAsync(Guid eventId, DateTimeOffset now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await LockCurrentEventBoundary(ct);
            var item = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, ct);
            if (item is null || item.State != EventState.SignupOpen || item.SignupClosesAt is not { } scheduledFor || scheduledFor > now)
                return;
            var from = item.State;
            item.CloseSignups(now);
            AddScheduledTransitionAndAudit(item, from, "event.signup_closed_automatically", JsonSerializer.Serialize(new { scheduledFor }), scheduledFor);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
        }
    }

    private async Task NotifyAdminsAsync(Guid itemId, string title, string detail, string route, DateTimeOffset now, CancellationToken ct)
    {
        var recipients = await db.Accounts.AsNoTracking()
            .Where(x => x.Active && x.AccountType == AccountType.WebsiteAccount && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin))
            .Select(x => x.Id)
            .ToListAsync(ct);
        foreach (var recipient in recipients)
            db.PersonalNotifications.Add(new PersonalNotification(Guid.NewGuid(), recipient, title, detail, route, now, itemId));
    }

    private void AddScheduledTransitionAndAudit(BingoEvent item, EventState from, string action, string? details, DateTimeOffset effectiveAt)
    {
        var now = time.GetUtcNow();
        db.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), item.Id, from, item.State, null, now, null, scheduled: true, effectiveAt: effectiveAt));
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, null, "System", action, "event", item.Id.ToString(), details, item.Id, JsonSerializer.Serialize(new { state = from }), JsonSerializer.Serialize(new { state = item.State, item.ActualSignupOpenedAt, item.ActualSignupClosedAt })));
    }

    public async Task<SignupLifecycleResult> CloseAsync(Guid eventId, long version, bool confirmed, LifecycleActor actor, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var authorizedActor = await EventMutationAuthorization.GetAuthorizedActorAsync(db, actor, ct);
            if (authorizedActor is null)
            {
                await tx.RollbackAsync(ct);
                return new(false, EventMutationAuthorization.UnauthorizedMessage);
            }
            actor = authorizedActor;
            await LockCurrentEventBoundary(ct);
            var item = await EventAsync(eventId, version, ct);
            if (!confirmed) return new(false, "Confirm that you want to change the signup lifecycle.");
            var from = item.State;
            item.CloseSignups(time.GetUtcNow());
            AddTransitionAndAudit(item, from, actor, "event.signup_closed", null);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(true);
        }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(ct); return new(false, "This event changed while you were editing it. Review the latest values and try again."); }
        catch (InvalidOperationException ex) { await tx.RollbackAsync(ct); return new(false, ex.Message); }
        catch (DbUpdateException) { await tx.RollbackAsync(ct); return new(false, "The signup lifecycle change could not be saved. Try again."); }
    }

    private async Task<SignupLifecycleResult> TransitionAsync(Guid eventId, long version, SignupOpeningMode mode, IReadOnlyCollection<string> acknowledgedWarningCodes, bool acceptProposedClose, LifecycleActor actor, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var authorizedActor = await EventMutationAuthorization.GetAuthorizedActorAsync(db, actor, ct);
            if (authorizedActor is null)
            {
                await tx.RollbackAsync(ct);
                return new(false, EventMutationAuthorization.UnauthorizedMessage);
            }
            actor = authorizedActor;

            await LockCurrentEventBoundary(ct);
            var item = await EventAsync(eventId, version, ct);
            var now = time.GetUtcNow();
            var evaluated = await readiness.GetSignupReadinessAsync(eventId, mode, now, ct) ?? throw new InvalidOperationException("Event not found.");
            if (!evaluated.CanProceed) return new(false, string.Join(" ", evaluated.Blockers.Select(x => x.Description)));
            var boundaryConflict = await CurrentEventBoundaryConflictAsync(db, item, ct);
            if (boundaryConflict is not null) return new(false, boundaryConflict.Description);
            var closeDecision = evaluated.CloseDecision;
            if (!closeDecision.IsValid && !closeDecision.RequiresAcceptance) return new(false, "Set a future draft time or event start that can be used as the signup closing time.");
            if (!acceptProposedClose) return new(false, "Confirm that you want to change the signup lifecycle.", closeDecision.ProposedClose);
            if (!closeDecision.IsValid)
                item.ConfigureSchedule(item.SignupOpensAt, closeDecision.ProposedClose, item.DraftAt, item.EventStartsAt, item.EventEndsAt, item.ParticipantCap);
            var from = item.State;
            item.OpenSignups(now);
            item.MarkFirstPublic(now);
            var failedScheduledOpenings = await db.ScheduledSignupOpeningAttempts.Where(x => x.EventId == item.Id && !x.Opened && x.ResolvedAt == null).ToListAsync(ct);
            foreach (var failedOpening in failedScheduledOpenings) failedOpening.Resolve(now);
            AddTransitionAndAudit(item, from, actor, mode == SignupOpeningMode.Reopen ? "event.signup_reopened" : "event.signup_opened", evaluated.Warnings.Count > 0 ? JsonSerializer.Serialize(new { warnings = evaluated.Warnings.Select(x => x.Code) }) : null);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(true);
        }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(ct); return new(false, "This event changed while you were editing it. Review the latest values and try again."); }
        catch (InvalidOperationException ex) { await tx.RollbackAsync(ct); return new(false, ex.Message); }
        catch (DbUpdateException) { await tx.RollbackAsync(ct); return new(false, "The signup lifecycle change could not be saved. Try again."); }
        catch (Exception ex) { if (logger is not null) LogLifecycleFailure(logger, eventId, ex); await tx.RollbackAsync(ct); return new(false, "The signup lifecycle change could not be completed. Try again."); }
    }

    private async Task<BingoEvent> EventAsync(Guid id, long version, CancellationToken ct)
    { var item = await db.Events.SingleOrDefaultAsync(x => x.Id == id && x.HiddenAt == null, ct) ?? throw new InvalidOperationException("Event not found."); if (item.Version != version) throw new DbUpdateConcurrencyException(); return item; }
    private async Task LockCurrentEventBoundary(CancellationToken ct) => await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7303003)", ct);
    private void AddTransitionAndAudit(BingoEvent item, EventState from, LifecycleActor actor, string action, string? details)
    { var now = time.GetUtcNow(); db.EventStateTransitions.Add(new EventStateTransition(Guid.NewGuid(), item.Id, from, item.State, actor.Id, now, null)); db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actor.Id, actor.Username, action, "event", item.Id.ToString(), details, item.Id, JsonSerializer.Serialize(new { state = from }), JsonSerializer.Serialize(new { state = item.State, item.ActualSignupOpenedAt, item.ActualSignupClosedAt }))); }
    private async Task<int> PromoteForCapacityIncreaseAsync(BingoEvent item, int? previousCapacity, LifecycleActor actor, CancellationToken ct)
    {
        if (item.State is not (EventState.SignupOpen or EventState.SignupClosed) || item.ParticipantCap is null || item.ParticipantCap <= previousCapacity) return 0;
        var signupParticipants = SignupParticipants(item.Id);
        var confirmed = await signupParticipants.CountAsync(x => x.SignupStatus == SignupStatus.Confirmed, ct);
        var waiting = await signupParticipants.Where(x => x.SignupStatus == SignupStatus.WaitingList)
            .OrderBy(x => x.WaitingListedAt ?? x.SignedUpAt)
            .ThenBy(x => x.SignupSequence)
            .ThenBy(x => x.Id)
            .Take(Math.Max(0, item.ParticipantCap.Value - confirmed)).ToListAsync(ct);
        var now = time.GetUtcNow();
        var admins = await db.Accounts.Where(x => x.Active && x.AccountType == AccountType.WebsiteAccount && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin)).Select(x => x.Id).ToListAsync(ct);
        foreach (var participant in waiting)
        {
            participant.Promote(now);
            db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actor.Id, actor.Username, "participant.promoted", "participant", participant.Id.ToString(), null, item.Id, SignupStatus.WaitingList.ToString(), SignupStatus.Confirmed.ToString()));
            if (participant.AccountId is { } owner) db.PersonalNotifications.Add(new PersonalNotification(Guid.NewGuid(), owner, "participant.promoted", $"Your signup for {item.Name} is confirmed.", $"/Events/{Uri.EscapeDataString(item.Slug)}/Signup/Confirmation?participantId={participant.Id}", now, item.Id));
            foreach (var admin in admins) db.PersonalNotifications.Add(new PersonalNotification(Guid.NewGuid(), admin, "participant.promoted", $"A participant was promoted for {item.Name} (capacity increased).", $"/Admin/Events/Manage/{item.Id}", now, item.Id));
        }
        return waiting.Count;
    }

    private IQueryable<EventParticipant> SignupParticipants(Guid eventId) =>
        db.EventParticipants.Where(participant => participant.EventId == eventId &&
            !db.TeamMemberships.Any(membership => membership.EventParticipantId == participant.Id && membership.LeftAt == null &&
                db.Teams.Any(team => team.Id == membership.TeamId && team.EventId == eventId && team.Active && !team.IncludedInDraft)));
    internal static async Task<string?> ValidateScheduleChangeAsync(ApplicationDbContext db, BingoEvent item, EventScheduleValues values, DateTimeOffset now, DraftState? draftState, bool confirmChanges, string? reason, CancellationToken ct, WiseOldManCompetition? proposedCompetition = null)
    {
        now = now.ToUniversalTime();
        var proposedOpening = values.SignupOpensAt?.ToUniversalTime();
        var proposedClosing = values.SignupClosesAt?.ToUniversalTime();
        var proposedDraft = values.DraftAt?.ToUniversalTime();
        var proposedStart = values.EventStartsAt?.ToUniversalTime();
        var proposedEnd = values.EventEndsAt?.ToUniversalTime();
        var openingChanged = item.SignupOpensAt != proposedOpening;
        var closingChanged = item.SignupClosesAt != proposedClosing;
        var draftChanged = item.DraftAt != proposedDraft;
        var startChanged = item.EventStartsAt != proposedStart;
        var endChanged = item.EventEndsAt != proposedEnd;
        var capacityChanged = item.ParticipantCap != values.ParticipantCap;
        var scheduledOpeningChanged = item.ScheduledSignupOpeningEnabled != values.ScheduledSignupOpeningEnabled;

        if (item.State == EventState.Live)
        {
            if (openingChanged || closingChanged || draftChanged || startChanged || capacityChanged || scheduledOpeningChanged)
                return "Only the event end can change while the event is Live.";
            if (endChanged && !confirmChanges) return "Confirm the Live event-end change before saving.";
            if (endChanged && string.IsNullOrWhiteSpace(reason)) return "Enter a reason for changing the Live event end.";
        }
        else if (draftState is DraftState.Running or DraftState.Paused)
        {
            if (openingChanged || closingChanged || draftChanged || capacityChanged || scheduledOpeningChanged)
                return "Signup and draft boundaries are locked while the draft is running or paused.";
        }
        else if (item.State == EventState.SignupClosed && draftState == DraftState.Finalized)
        {
            if (openingChanged || closingChanged || draftChanged || capacityChanged || scheduledOpeningChanged)
                return "Only future event start and end times can change after draft finalization.";
        }
        var boundaries = new[]
        {
            ("Signup opening", item.SignupOpensAt, proposedOpening),
            ("Signup closing", item.SignupClosesAt, proposedClosing),
            ("Draft time", item.DraftAt, proposedDraft),
            ("Event start", item.EventStartsAt, proposedStart),
            ("Event end", item.EventEndsAt, proposedEnd)
        };
        foreach (var (label, current, proposed) in boundaries)
        {
            var normalized = proposed?.ToUniversalTime();
            if (current == normalized) continue;
            var mayCorrectPreLiveBoundary = (label is "Event start" or "Event end") && (item.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed);
            if (current <= now && !mayCorrectPreLiveBoundary) return $"{label} is locked because that boundary has passed.";
            if (normalized is { } changed && changed <= now) return $"A changed {label.ToLowerInvariant()} must be in the future.";
        }
        if (item.State != EventState.Draft && openingChanged) return "Signup opening is locked after signup has opened.";
        if (item.State == EventState.SignupClosed && closingChanged) return "Signup closing is read-only after signup has closed; use Reopen to establish a new closing time.";
        if (item.FirstPublicAt is not null && (proposedStart is null || proposedEnd is null)) return "Published event start and end times cannot be cleared.";
        var participantConsequenceChanged = openingChanged || closingChanged || startChanged || endChanged || capacityChanged || scheduledOpeningChanged;
        if (participantConsequenceChanged && item.FirstPublicAt is not null && !confirmChanges) return "Confirm the schedule consequence before saving.";
        var unchangedOverdueScheduledOpening = item.ScheduledSignupOpeningEnabled
            && values.ScheduledSignupOpeningEnabled
            && item.SignupOpensAt == proposedOpening
            && proposedOpening <= now;
        if (values.ScheduledSignupOpeningEnabled && proposedOpening is null) return "Automatic signup opening requires a signup opening time.";
        if (values.ScheduledSignupOpeningEnabled && !unchangedOverdueScheduledOpening)
        {
            if (proposedClosing is null) return "Automatic signup opening requires a signup closing time.";
            if (proposedOpening <= now) return "A scheduled signup opening must be configured in the future.";
            if (proposedClosing <= now) return "Signup closing must be in the future.";
            if (proposedClosing <= proposedOpening) return "Signup closing must be after the scheduled opening.";
            if (proposedStart is { } start && proposedClosing > start) return "Signup closing must be no later than event start.";
        }
        if (item.SignupOpensAt <= now && item.ScheduledSignupOpeningEnabled != values.ScheduledSignupOpeningEnabled) return "Automatic signup opening is locked because the opening boundary has passed.";
        if (item.State != EventState.Draft && scheduledOpeningChanged) return "Automatic signup opening can only be changed for a private draft.";
        if (item.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed or EventState.Live
            && proposedStart is not null && proposedEnd is not null)
        {
            var proposed = new BingoEvent(item.Id, item.Name, item.Slug, item.Timezone, item.CreatedByAccountId, item.CreatedAt);
            proposed.ConfigureSchedule(item.ActualSignupOpenedAt ?? proposedOpening, proposedClosing, proposedDraft, proposedStart, proposedEnd, values.ParticipantCap);
            var conflict = await CurrentEventBoundaryConflictAsync(db, proposed, ct);
            if (conflict is not null) return conflict.Description;
        }
        var competitionStart = proposedCompetition?.StartsAt;
        var competitionEnd = proposedCompetition?.EndsAt;
        var hasCompetition = proposedCompetition is not null;
        var managedCompetition = await db.EventCompetitionManagements.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EventId == item.Id && x.Status != EventCompetitionManagementStatus.Deleted, ct);
        if (proposedCompetition is null)
        {
            var linkedCompetition = await db.EventCompetitionSynchronizations.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == item.Id && x.CompetitionId != null, ct);
            hasCompetition = linkedCompetition is not null;
            competitionStart = linkedCompetition?.CompetitionStartsAt;
            competitionEnd = linkedCompetition?.CompetitionEndsAt;
        }
        if (hasCompetition && managedCompetition is null)
            if (proposedStart is not { } start || proposedEnd is not { } end || competitionStart is not { } expectedStart || competitionEnd is not { } expectedEnd || Math.Abs((start - expectedStart).TotalMinutes) > 5 || Math.Abs((end - expectedEnd).TotalMinutes) > 5)
                return "The linked Wise Old Man competition must remain within five minutes of the event window.";
        return null;
    }

    private Task<DraftState?> DraftStateAsync(Guid eventId, CancellationToken ct) =>
        db.DraftSessions.AsNoTracking().Where(x => x.EventId == eventId).Select(x => (DraftState?)x.State).SingleOrDefaultAsync(ct);
    private static async Task<BoundaryConflict?> CurrentEventBoundaryConflictAsync(ApplicationDbContext db, BingoEvent item, CancellationToken ct)
    {
        if (item.EventStartsAt is not { } start || item.EventEndsAt is not { } end)
            return new(Guid.Empty, string.Empty, "Set an event start and end before opening signup.");
        var states = new[] { EventState.SignupOpen, EventState.SignupClosed, EventState.Live, EventState.AwaitingFinalReview, EventState.Finalized };
        var developmentMode = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);
        var candidates = await db.Events.AsNoTracking().Where(x => x.Id != item.Id && x.HiddenAt == null && states.Contains(x.State) && !(x.IsDevelopmentFixture && developmentMode)).ToListAsync(ct);
        var overlap = candidates
            .Where(x => x.EventStartsAt is { } otherStart && x.EventEndsAt is { } otherEnd && start < otherEnd && otherStart < end)
            .OrderBy(x => x.EventStartsAt)
            .ThenBy(x => x.Name)
            .FirstOrDefault();
        if (overlap is null) return null;
        var window = FormatWindow(overlap.EventStartsAt!.Value, overlap.EventEndsAt!.Value, item.Timezone);
        return new(overlap.Id, overlap.Name, $"This event window overlaps {overlap.Name} ({window}).");
    }
    private static string FormatWindow(DateTimeOffset start, DateTimeOffset end, string timezoneId)
    {
        try
        {
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return $"{TimeZoneInfo.ConvertTime(start, timezone):dd MMM yyyy, HH:mm} – {TimeZoneInfo.ConvertTime(end, timezone):dd MMM yyyy, HH:mm} {timezoneId}";
        }
        catch (TimeZoneNotFoundException) { return $"{start:dd MMM yyyy, HH:mm} – {end:dd MMM yyyy, HH:mm} UTC"; }
        catch (InvalidTimeZoneException) { return $"{start:dd MMM yyyy, HH:mm} – {end:dd MMM yyyy, HH:mm} UTC"; }
    }
    private static readonly Action<ILogger, Guid, Exception?> LogScheduleFailure = LoggerMessage.Define<Guid>(LogLevel.Error, new EventId(730301), "Unexpected schedule update failure for event {EventId}");
    private static readonly Action<ILogger, Guid, Exception?> LogLifecycleFailure = LoggerMessage.Define<Guid>(LogLevel.Error, new EventId(730302), "Unexpected signup lifecycle failure for event {EventId}");
    private static object ScheduleState(BingoEvent item) => new { item.SignupOpensAt, item.SignupClosesAt, item.DraftAt, item.EventStartsAt, item.EventEndsAt, item.ParticipantCap, item.SubmissionCutoffAt, item.ScheduledSignupOpeningEnabled, item.ScheduledSignupWarningCodes };
    private sealed record BoundaryConflict(Guid EventId, string EventName, string Description);
}
