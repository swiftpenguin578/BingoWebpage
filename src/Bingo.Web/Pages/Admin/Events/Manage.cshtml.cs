using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Signups;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class ManageModel(ApplicationDbContext dbContext, ISignupService signupService, EventParticipantCharacterService characterService, IAuditWriter auditWriter, IEventReadinessEvaluator readinessEvaluator, IEventSignupLifecycleService signupLifecycle, IEventLifecycleService eventLifecycle, IEventDestructiveLifecycleService destructiveLifecycle, TimeProvider timeProvider, IEventCompetitionSynchronizationService? competitionSynchronization = null, IStringLocalizer<SharedResource>? text = null, IHostEnvironment? environment = null, IEventFinalizationService? finalizationService = null, IEventQuarantineService? quarantine = null) : PageModel
{
    public EventDetails? EventView { get; private set; }
    public IReadOnlyList<EvidenceCodeRow> EvidenceCodes { get; private set; } = [];
    public SignupReadiness? SignupReadiness { get; private set; }
    public EventStartReadiness? StartReadiness { get; private set; }
    public FinalReviewReadiness? FinalReviewReadiness { get; private set; }
    public IReadOnlyList<ReadinessItem> OverviewBlockers { get; private set; } = [];
    public IReadOnlyList<TimelineRow> EffectiveTimeline { get; private set; } = [];
    public ScheduledActionView? ScheduledAction { get; private set; }
    public int PendingReviewCount { get; private set; }
    public int SubmissionCount { get; private set; }
    public bool CanDiscard { get; private set; }
    public bool ShowDevelopmentCompetitionControl { get; private set; }
    public bool ShowAllControlStages { get; private set; }
    public EventCompetitionView? CompetitionIntegration { get; private set; }
    public string? PrivateCancellationReason { get; private set; }
    public IReadOnlyList<QuarantineAuditRow> QuarantineAuditHistory { get; private set; } = [];
    public bool SignupWarningAcknowledged => EventView is not null && TempData.Peek(SignupConfirmationKey(EventView.Id, "warnings")) is not null;
    public bool SignupCloseAcknowledged => EventView is not null && TempData.Peek(SignupConfirmationKey(EventView.Id, "close")) is not null;
    public bool SignupCloseRequiresAcceptance => (EventView?.State is EventState.Draft or EventState.SignupClosed) && SignupReadiness?.CloseDecision.RequiresAcceptance == true;
    [BindProperty, Range(1, 10000), Display(Name = "New participant cap")] public int NewCap { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "Signups open")] public DateTimeOffset NewSignupOpening { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "New signup closing")] public DateTimeOffset NewSignupClosing { get; set; }
    [BindProperty, StringLength(1000), Display(Name = "Reason for reopening")] public string? StateReason { get; set; }
    [BindProperty, StringLength(100), Display(Name = "Evidence code")] public string? NewEvidenceCode { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "Activates at")] public DateTimeOffset? EvidenceCodeActivatesAt { get; set; }
    [BindProperty, Display(Name = "Activates at")] public string? EvidenceCodeActivatesAtLocal { get; set; }
    [BindProperty, StringLength(1000), Display(Name = "Code note")] public string? EvidenceCodeNote { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "Reopen until")] public DateTimeOffset? ReopenUntil { get; set; }
    [BindProperty, Display(Name = "Reopen until")] public string? ReopenUntilLocal { get; set; }
    [BindProperty] public long EventVersion { get; set; }
    [BindProperty, Range(1, long.MaxValue)] public long? CompetitionId { get; set; }
    [BindProperty] public bool SynchronizeCompetitionSchedule { get; set; }
    [BindProperty] public bool AcknowledgeSignupWarnings { get; set; }
    [BindProperty] public bool AcceptProposedClose { get; set; }
    [BindProperty] public bool ConfirmStartEvent { get; set; }
    [BindProperty, StringLength(2000)] public string? StartReason { get; set; }
    [BindProperty] public bool ConfirmEndEvent { get; set; }
    [BindProperty, StringLength(2000)] public string? EndReason { get; set; }
    [BindProperty] public bool ConfirmResumeEvent { get; set; }
    [BindProperty, DataType(DataType.DateTime), Display(Name = "Replacement event end")] public DateTimeOffset ReplacementEventEndsAt { get; set; }
    [BindProperty, Display(Name = "Replacement event end")] public string? ReplacementEventEndsAtLocal { get; set; }
    [BindProperty, StringLength(2000), Display(Name = "Reason for resuming")]
    public string? ResumeReason { get; set; }
    [BindProperty] public bool ConfirmDestructiveAction { get; set; }
    [BindProperty, StringLength(2000)] public string? CancellationReason { get; set; }
    [BindProperty(SupportsGet = true, Name = "hidden")] public bool HiddenInspection { get; set; }
    [BindProperty, StringLength(200), Display(Name = "Event name confirmation")] public string? EventNameConfirmation { get; set; }
    [BindProperty, Required, StringLength(2000), Display(Name = "Reason")] public string? QuarantineReason { get; set; }
    [BindProperty(SupportsGet = true, Name = "confirm")] public string? ConfirmationAction { get; set; }
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) { _ = characterService; return await LoadAsync(id, ct) ? Page() : NotFound(); }
    public static OverviewMilestone NextMilestoneFor(EventDetails eventView) => eventView.State switch
    {
        EventState.Draft => new("Signup opens", eventView.SignupOpensAt),
        EventState.SignupOpen => new("Signup closes", eventView.SignupClosesAt),
        EventState.SignupClosed => new("Event starts", eventView.StartsAt),
        EventState.Live => new("Event ends", eventView.EndsAt),
        EventState.AwaitingFinalReview => new("Submission cutoff", eventView.SubmissionCutoffAt),
        EventState.Finalized => new("Finalized", eventView.FinalizedAt),
        EventState.Archived => new("Archived", eventView.ArchivedAt),
        EventState.Cancelled => new("Cancelled", eventView.CancelledAt),
        _ => new("Event ends", eventView.EndsAt)
    };
    public async Task<IActionResult> OnPostStateAsync(Guid id, EventState target, CancellationToken ct)
    {
        if (target == EventState.SignupClosed) return await OnPostCloseSignupAsync(id, ct);
        var current = await dbContext.Events.AsNoTracking().Where(x => x.Id == id).Select(x => x.State).SingleOrDefaultAsync(ct);
        return current == EventState.SignupClosed ? await OnPostReopenSignupAsync(id, ct) : await OnPostOpenSignupAsync(id, ct);
    }
    public async Task<IActionResult> OnPostOpenSignupAsync(Guid id, CancellationToken ct) => await SignupResult(await signupLifecycle.OpenAsync(id, EventVersion, AcknowledgeSignupWarnings, AcceptProposedClose, Actor, ct), id, "Signups opened.");
    public async Task<IActionResult> OnPostCloseSignupAsync(Guid id, CancellationToken ct) => await SignupResult(await signupLifecycle.CloseAsync(id, EventVersion, Actor, ct), id, "Signups closed.");
    public async Task<IActionResult> OnPostReopenSignupAsync(Guid id, CancellationToken ct) => await SignupResult(await signupLifecycle.ReopenAsync(id, EventVersion, AcknowledgeSignupWarnings, AcceptProposedClose, Actor, ct), id, "Signups reopened.");
    public async Task<IActionResult> OnPostCapacityAsync(Guid id, CancellationToken ct)
    {
        if (HasBindingErrors(nameof(NewCap))) { TempData["StatusMessage"] = Localize("Enter a valid player cap."); return RedirectToPage(new { id }); }
        if (await dbContext.Events.AnyAsync(e => e.Id == id && e.DraftLocked, ct)) { TempData["StatusMessage"] = Localize("The participant cap cannot change after the draft has started."); return RedirectToPage(new { id }); }
        var before = await dbContext.Events.AsNoTracking().Where(e => e.Id == id).Select(e => e.ParticipantCap).SingleOrDefaultAsync(ct);
        try { var promoted = await signupService.IncreaseCapacityAndPromoteAsync(id, NewCap, ct); await AuditAsync("event.capacity_increased", await dbContext.Events.FindAsync([id], ct) ?? throw new InvalidOperationException(), $"{before} → {NewCap}; promoted {promoted}", ct); TempData["StatusMessage"] = Localize("Capacity increased. {0} participant(s) promoted.", promoted); }
        catch (InvalidOperationException ex) { TempData["StatusMessage"] = ex.Message; }
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostSignupWindowAsync(Guid id, CancellationToken ct)
    {
        if (!await dbContext.Events.AnyAsync(e => e.Id == id, ct)) return NotFound();
        return RedirectToPage("Schedule", new { id });
    }
    public Task<IActionResult> OnPostPrepareSignupConfirmationAsync(Guid id, CancellationToken ct) => PrepareConfirmation(id, "signup", ct);
    public Task<IActionResult> OnPostPrepareStartConfirmationAsync(Guid id, CancellationToken ct) => PrepareConfirmation(id, "start", ct);
    public Task<IActionResult> OnPostPrepareEndConfirmationAsync(Guid id, CancellationToken ct) => PrepareConfirmation(id, "end", ct);
    public Task<IActionResult> OnPostPrepareResumeConfirmationAsync(Guid id, CancellationToken ct) => PrepareConfirmation(id, "resume", ct);
    public Task<IActionResult> OnPostPrepareDestructiveConfirmationAsync(Guid id, CancellationToken ct) => PrepareConfirmation(id, "destructive", ct);
    public async Task<IActionResult> OnPostConfirmSignupAsync(Guid id, CancellationToken ct)
    {
        var state = await dbContext.Events.AsNoTracking().Where(item => item.Id == id).Select(item => item.State).SingleOrDefaultAsync(ct);
        var readiness = await readinessEvaluator.GetSignupReadinessAsync(id, state == EventState.SignupClosed ? SignupOpeningMode.Reopen : SignupOpeningMode.OpenNow, timeProvider.GetUtcNow(), ct);
        var acknowledgeWarnings = Request.Form.ContainsKey(nameof(AcknowledgeSignupWarnings));
        var acceptProposedClose = Request.Form.ContainsKey(nameof(AcceptProposedClose));
        if (acknowledgeWarnings) TempData[SignupConfirmationKey(id, "warnings")] = true;
        if (acceptProposedClose) TempData[SignupConfirmationKey(id, "close")] = true;
        var warningsConfirmed = (readiness?.Warnings.Count ?? 0) == 0 || acknowledgeWarnings || TempData.Peek(SignupConfirmationKey(id, "warnings")) is not null;
        var closeConfirmed = state is not (EventState.Draft or EventState.SignupClosed) || readiness?.CloseDecision.RequiresAcceptance != true || acceptProposedClose || TempData.Peek(SignupConfirmationKey(id, "close")) is not null;
        if (!warningsConfirmed || !closeConfirmed)
        {
            SetStatus(Localize("Confirm each listed signup consequence before continuing."), UiMessageType.Error);
            return RedirectToPage(new { id, confirm = "signup" });
        }
        TempData.Remove(SignupConfirmationKey(id, "warnings"));
        TempData.Remove(SignupConfirmationKey(id, "close"));
        if (state == EventState.SignupOpen) return await OnPostCloseSignupAsync(id, ct);
        return await SignupResult(state == EventState.SignupClosed
            ? await signupLifecycle.ReopenAsync(id, EventVersion, warningsConfirmed, closeConfirmed, Actor, ct)
            : await signupLifecycle.OpenAsync(id, EventVersion, warningsConfirmed, closeConfirmed, Actor, ct), id, state == EventState.SignupClosed ? "Signups reopened." : "Signups opened.");
    }
    public async Task<IActionResult> OnPostStartEventAsync(Guid id, CancellationToken ct)
    {
        var result = await eventLifecycle.StartNowAsync(id, EventVersion, ConfirmStartEvent, StartReason, Actor, ct);
        SetStatus(result.Succeeded ? Localize("Event started.") : result.Error ?? Localize("The event could not be started."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostEndEventAsync(Guid id, CancellationToken ct)
    {
        var result = await eventLifecycle.EndNowAsync(id, EventVersion, ConfirmEndEvent, EndReason, Actor, ct);
        SetStatus(result.Succeeded ? Localize("Event ended and moved to final review.") : result.Error ?? Localize("The event could not be ended."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostResumeEventAsync(Guid id, CancellationToken ct)
    {
        var timezoneId = await dbContext.Events.AsNoTracking().Where(x => x.Id == id).Select(x => x.Timezone).SingleOrDefaultAsync(ct);
        if (timezoneId is null) return NotFound();
        var replacementEnd = ParseEventLocal(ReplacementEventEndsAtLocal, timezoneId, nameof(ReplacementEventEndsAtLocal), "Replacement event end")
            ?? (string.IsNullOrWhiteSpace(ReplacementEventEndsAtLocal) && ReplacementEventEndsAt != default ? ReplacementEventEndsAt.ToUniversalTime() : null);
        if (replacementEnd is null)
        {
            SetStatus(Localize("Choose a valid replacement event end."), UiMessageType.Error);
            return RedirectToPage(new { id });
        }
        var result = await eventLifecycle.ResumePrematureEndAsync(id, EventVersion, ConfirmResumeEvent, ResumeReason, replacementEnd.Value, Actor, ct);
        SetStatus(result.Succeeded ? Localize("Event resumed and returned to live play.") : result.Error ?? Localize("The event could not be resumed."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostCompetitionAsync(Guid id, CancellationToken ct)
    {
        if (ModelState.ErrorCount > 0) { SetStatus(Localize("Enter a valid competition ID."), UiMessageType.Error); return RedirectToPage(new { id }); }
        try
        {
            var result = await (competitionSynchronization ?? throw new InvalidOperationException("Competition synchronization is not configured.")).ConfigureAsync(id, EventVersion, CompetitionId, SynchronizeCompetitionSchedule, Actor, ct);
            SetStatus(result.Succeeded ? CompetitionId is null ? Localize("Competition integration cleared.") : Localize("Competition linked and validated.") : result.Error ?? Localize("The competition could not be configured."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        }
        catch (UnauthorizedAccessException exception) { SetStatus(exception.Message, UiMessageType.Error); }
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostClearCompetitionAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await (competitionSynchronization ?? throw new InvalidOperationException("Competition synchronization is not configured.")).ConfigureAsync(id, EventVersion, null, false, Actor, ct);
            SetStatus(result.Succeeded ? Localize("Competition integration cleared.") : result.Error ?? Localize("The competition could not be cleared."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        }
        catch (UnauthorizedAccessException exception) { SetStatus(exception.Message, UiMessageType.Error); }
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostRefreshCompetitionAsync(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await (competitionSynchronization ?? throw new InvalidOperationException("Competition synchronization is not configured.")).RefreshAsync(id, Actor, ct);
            var message = result.Succeeded
                ? Localize("Competition refresh completed.")
                : result.Skipped
                    ? Localize("The cached competition result is still within its refresh window.")
                    : CompetitionRefreshFailure(result);
            SetStatus(message, result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        }
        catch (UnauthorizedAccessException exception) { SetStatus(exception.Message, UiMessageType.Error); }
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostMakeDevelopmentCompetitionDueAsync(Guid id, CancellationToken ct)
    {
        if (environment?.IsDevelopment() != true) return NotFound();
        try
        {
            var madeDue = await (competitionSynchronization ?? throw new InvalidOperationException("Competition synchronization is not configured.")).MakeDevelopmentRefreshDueAsync(id, Actor, ct);
            SetStatus(madeDue ? Localize("Development TEST 15 competition refresh is due.") : Localize("The Development TEST 15 refresh control is unavailable."), madeDue ? UiMessageType.Success : UiMessageType.Error);
        }
        catch (UnauthorizedAccessException exception) { SetStatus(exception.Message, UiMessageType.Error); }
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostDiscardAsync(Guid id, CancellationToken ct)
    {
        var result = await destructiveLifecycle.DiscardAsync(id, EventVersion, ConfirmDestructiveAction, Actor, ct);
        SetStatus(result.Succeeded ? Localize("Event discarded.") : result.Error ?? Localize("The event could not be discarded."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return result.Succeeded ? RedirectToPage("Index") : RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostCancelAsync(Guid id, CancellationToken ct)
    {
        var result = await destructiveLifecycle.CancelAsync(id, EventVersion, ConfirmDestructiveAction, CancellationReason, Actor, ct);
        SetStatus(result.Succeeded ? Localize("Event cancelled.") : result.Error ?? Localize("The event could not be cancelled."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostHideAsync(Guid id, CancellationToken ct)
    {
        var result = await (quarantine ?? throw new InvalidOperationException("Event quarantine is not configured.")).HideAsync(id, EventVersion, EventNameConfirmation, QuarantineReason, Actor, ct);
        SetStatus(result.Succeeded ? Localize("Event hidden from all ordinary surfaces.") : result.Error ?? Localize("The event could not be hidden."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return result.Succeeded ? RedirectToPage("Index", new { filter = "hidden" }) : RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostRestoreHiddenAsync(Guid id, CancellationToken ct)
    {
        var result = await (quarantine ?? throw new InvalidOperationException("Event quarantine is not configured.")).RestoreAsync(id, EventVersion, EventNameConfirmation, QuarantineReason, Actor, ct);
        SetStatus(result.Succeeded ? Localize("Event restored with its lifecycle and retained history unchanged.") : result.Error ?? Localize("The event could not be restored."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return result.Succeeded ? RedirectToPage("Index") : RedirectToPage(new { id, hidden = true });
    }
    public async Task<IActionResult> OnPostReopenSubmissionsAsync(Guid id, CancellationToken ct)
    { var item = await dbContext.Events.SingleOrDefaultAsync(x => x.Id == id, ct); if (item is null) return NotFound(); var reopenUntil = ParseEventLocal(ReopenUntilLocal, item.Timezone, nameof(ReopenUntilLocal), "Reopen cutoff") ?? (string.IsNullOrWhiteSpace(ReopenUntilLocal) ? ReopenUntil?.ToUniversalTime() : null); if (reopenUntil is null || string.IsNullOrWhiteSpace(StateReason)) { TempData["StatusMessage"] = Localize("A valid future cutoff and reason are required."); return RedirectToPage(new { id }); } try { item.ReopenSubmissions(reopenUntil.Value, timeProvider.GetUtcNow()); await dbContext.SaveChangesAsync(ct); await AuditAsync("event.submissions_reopened", item, $"Until {reopenUntil:O}; {StateReason}", ct); TempData["StatusMessage"] = Localize("Submissions reopened until {0}.", DateTimePresentation.Format(reopenUntil.Value, "dd MMM yyyy, HH:mm", item.Timezone, CultureInfo.CurrentCulture)); } catch (InvalidOperationException ex) { TempData["StatusMessage"] = ex.Message; } return RedirectToPage(new { id }); }
    public Task<IActionResult> OnPostEnableEvidenceCodesAsync(Guid id, CancellationToken ct) => SetEvidenceCodeMode(id, true, ct);
    public Task<IActionResult> OnPostDisableEvidenceCodesAsync(Guid id, CancellationToken ct) => SetEvidenceCodeMode(id, false, ct);
    private async Task<IActionResult> SetEvidenceCodeMode(Guid id, bool enabled, CancellationToken ct)
    {
        var item = await dbContext.Events.SingleOrDefaultAsync(x => x.Id == id, ct); if (item is null) return NotFound(); item.SetEvidenceCodeEnabled(enabled); await dbContext.SaveChangesAsync(ct); await AuditAsync("event.evidence_code_mode", item, enabled ? "Enabled" : "Disabled", ct); TempData["StatusMessage"] = enabled ? Localize("Verification codes enabled.") : Localize("Verification codes disabled."); return RedirectToPage(new { id });
    }
    public Task<IActionResult> OnPostCreateEvidenceCodeAsync(Guid id, CancellationToken ct) => CreateEvidenceCode(id, NewEvidenceCode, ct);
    private async Task<IActionResult> CreateEvidenceCode(Guid id, string? code, CancellationToken ct)
    {
        var item = await dbContext.Events.SingleOrDefaultAsync(x => x.Id == id, ct); if (item is null) return NotFound(); if (HasBindingErrors(nameof(NewEvidenceCode), nameof(EvidenceCodeActivatesAtLocal), nameof(EvidenceCodeNote))) { TempData["StatusMessage"] = Localize("Check the verification code details and try again."); return RedirectToPage(new { id }); }
        if (!item.EvidenceCodeEnabled) { TempData["StatusMessage"] = Localize("Enable evidence codes first."); return RedirectToPage(new { id }); }
        if (string.IsNullOrWhiteSpace(code)) { TempData["StatusMessage"] = Localize("Enter or generate a code first."); return RedirectToPage(new { id }); }
        var activates = ParseEventLocal(EvidenceCodeActivatesAtLocal, item.Timezone, nameof(EvidenceCodeActivatesAtLocal), "Activation time"); if (!string.IsNullOrWhiteSpace(EvidenceCodeActivatesAtLocal) && activates is null) { TempData["StatusMessage"] = Localize("Check the verification code details and try again."); return RedirectToPage(new { id }); }
        activates ??= EvidenceCodeActivatesAt?.ToUniversalTime() ?? timeProvider.GetUtcNow(); var activatedAt = activates.Value; if (await dbContext.EvidenceCodes.AnyAsync(x => x.EventId == id && x.ActivatesAt == activatedAt, ct)) { TempData["StatusMessage"] = Localize("Another code already activates at that exact time."); return RedirectToPage(new { id }); }
        var created = new EvidenceCode(Guid.NewGuid(), id, code, activatedAt, User.GetAccountId()!.Value, timeProvider.GetUtcNow(), EvidenceCodeNote); dbContext.EvidenceCodes.Add(created); var codes = await dbContext.EvidenceCodes.Where(x => x.EventId == id).OrderBy(x => x.ActivatesAt).ToListAsync(ct); codes.Add(created); codes = codes.OrderBy(x => x.ActivatesAt).ToList(); for (var index = 0; index < codes.Count; index++) codes[index].SetRetiresAt(index + 1 < codes.Count ? codes[index + 1].ActivatesAt : null); await dbContext.SaveChangesAsync(ct); await AuditAsync("evidence_code.created", item, $"{created.Code}; activates {activatedAt:O}", ct); TempData["StatusMessage"] = Localize("Evidence code {0} saved.", created.Code); return RedirectToPage(new { id });
    }
    private async Task<bool> LoadAsync(Guid id, CancellationToken ct)
    {
        var item = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id && e.State != EventState.Discarded, ct); if (item is null || item.IsHidden && (!HiddenInspection || !User.IsInRole("SuperAdmin"))) return false;
        var allParticipants = await dbContext.EventParticipants.AsNoTracking().Where(p => p.EventId == id).OrderBy(p => p.SignedUpAt).ThenBy(p => p.SignupSequence).ToListAsync(ct);
        var activeTeamIds = await dbContext.Teams.AsNoTracking().Where(team => team.EventId == id && team.Active).Select(team => team.Id).ToListAsync(ct);
        var membershipCounts = activeTeamIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await dbContext.TeamMemberships.AsNoTracking().Where(membership => activeTeamIds.Contains(membership.TeamId) && membership.LeftAt == null).GroupBy(membership => membership.TeamId).ToDictionaryAsync(group => group.Key, group => group.Count(), ct);
        var teamSizes = activeTeamIds.Select(teamId => membershipCounts.GetValueOrDefault(teamId)).ToList();
        var actualTeamSize = teamSizes.Count == 0 ? null : teamSizes.Min() == teamSizes.Max() ? teamSizes[0].ToString(CultureInfo.InvariantCulture) : $"{teamSizes.Min()}–{teamSizes.Max()}";
        var board = await dbContext.Boards.AsNoTracking().Where(value => value.EventId == id).Select(value => new { value.Rows, value.Columns, value.State }).SingleOrDefaultAsync(ct);
        var boardSize = board is null ? null : $"{board.Rows} × {board.Columns}";
        int? boardRows = board?.Rows ?? item.ExpectedBoardRows;
        int? boardColumns = board?.Columns ?? item.ExpectedBoardColumns;
        int? configuredBoardTileCount = board is null ? null : await dbContext.BoardTiles.AsNoTracking().CountAsync(tile => tile.BoardId == id, ct);
        int? expectedBoardCellCount = boardRows is { } rows && boardColumns is { } columns ? rows * columns : null;
        var draftReady = await dbContext.DraftSessions.AsNoTracking().AnyAsync(session => session.EventId == id && session.State == DraftState.Finalized, ct);
        var canStartEvent = board?.State == BoardState.Published && draftReady;
        var postponed = await dbContext.ScheduledEventStartAttempts.AsNoTracking().Where(x => x.EventId == id && x.ScheduledFor <= timeProvider.GetUtcNow() && !x.Started && x.ResolvedAt == null).OrderByDescending(x => x.AttemptedAt).FirstOrDefaultAsync(ct);
        StartReadiness = await eventLifecycle.GetStartReadinessAsync(id, ct);
        EvidenceCodes = await dbContext.EvidenceCodes.AsNoTracking().Where(x => x.EventId == id).OrderByDescending(x => x.ActivatesAt).Select(x => new EvidenceCodeRow(x.Id, x.Code, x.ActivatesAt, x.RetiresAt, x.Note)).ToListAsync(ct);
        SubmissionCount = await dbContext.Submissions.CountAsync(x => x.EventId == id, ct);
        PendingReviewCount = await dbContext.Submissions.CountAsync(x => x.EventId == id && x.Status == SubmissionStatus.Pending, ct);
        FinalReviewReadiness = item.State == EventState.AwaitingFinalReview && finalizationService is not null ? await finalizationService.GetReadinessAsync(id, ct) : null;
        EventView = new EventDetails(item.Id, item.Name, item.Slug, item.Description, item.Timezone, item.State, item.FirstPublicAt, item.BoardPublished, item.SignupOpensAt, item.SignupClosesAt, item.DraftAt, item.EventStartsAt, item.EventEndsAt, item.ActualSignupOpenedAt, item.ActualSignupClosedAt, item.ActualStartedAt, item.ActualEndedAt, item.ActualEndedAt ?? item.EventEndsAt, item.SubmissionCutoffAt, item.SubmissionsClosedAt, item.ScheduledSignupOpeningEnabled, item.ReopenedSubmissionCutoffAt, item.ParticipantCap ?? 0, allParticipants.Count(p => p.SignupStatus == SignupStatus.Confirmed), allParticipants.Count(p => p.SignupStatus == SignupStatus.WaitingList), item.DraftLocked, item.EvidenceCodeEnabled, activeTeamIds.Count, actualTeamSize, boardSize, boardRows, boardColumns, configuredBoardTileCount, expectedBoardCellCount, item.ExpectedTeamCount, item.ExpectedTeamSize, item.ExpectedBoardRows is not null && item.ExpectedBoardColumns is not null ? $"{item.ExpectedBoardRows} × {item.ExpectedBoardColumns}" : null, canStartEvent, item.FinalizedAt, item.ArchivedAt, item.CancelledAt, item.IsHidden, item.HiddenAt, item.HiddenByAccountId, item.HiddenReason);
        if (item.IsHidden)
        {
            EventNameConfirmation = item.Name;
            QuarantineAuditHistory = await dbContext.AuditEntries.AsNoTracking()
                .Where(entry => entry.EventId == id && (entry.Action == "event.hidden" || entry.Action == "event.restored"))
                .OrderByDescending(entry => entry.OccurredAt)
                .Select(entry => new QuarantineAuditRow(entry.Action, entry.OccurredAt, entry.ActorUsername, entry.Details))
                .ToListAsync(ct);
            EventVersion = item.Version;
            return true;
        }
        EffectiveTimeline = EffectiveTimelineFor(new(
            item.SignupOpensAt,
            item.SignupClosesAt,
            item.DraftAt,
            item.EventStartsAt,
            item.EventEndsAt,
            item.ActualSignupOpenedAt,
            item.ActualSignupClosedAt,
            item.ActualStartedAt,
            item.ActualEndedAt,
            item.SubmissionCutoffAt,
            item.SubmissionsClosedAt,
            item.CancelledAt));
        SignupReadiness = await readinessEvaluator.GetSignupReadinessAsync(id, item.State == EventState.SignupClosed ? SignupOpeningMode.Reopen : SignupOpeningMode.OpenNow, timeProvider.GetUtcNow(), ct);
        CanDiscard = item.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed
            && !await dbContext.EventParticipants.AnyAsync(x => x.EventId == id, ct)
            && !await dbContext.Teams.AnyAsync(x => x.EventId == id, ct)
            && !await dbContext.AccountEventAccesses.AnyAsync(x => x.EventId == id, ct)
            && !await dbContext.Submissions.AnyAsync(x => x.EventId == id, ct);
        PrivateCancellationReason = item.State == EventState.Cancelled ? item.CancellationReason : null;
        CompetitionIntegration = competitionSynchronization is null ? null : await competitionSynchronization.GetAsync(id, ct);
        ShowAllControlStages = environment?.IsDevelopment() == true && Request.Query.ContainsKey("preview-all-controls");
        ShowDevelopmentCompetitionControl = environment?.IsDevelopment() == true && item.IsDevelopmentFixture && item.Slug == "test-15-dkl-live" && item.State == EventState.Live && CompetitionIntegration?.Configured == true;
        var failedOpening = await dbContext.ScheduledSignupOpeningAttempts.AsNoTracking().Where(x => x.EventId == id && !x.Opened && x.ResolvedAt == null).OrderByDescending(x => x.AttemptedAt).FirstOrDefaultAsync(ct);
        ScheduledAction = postponed is not null && item.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed
            ? new("Automatic start postponed", postponed.ScheduledFor, postponed.AttemptedAt, StartReadiness?.Blockers ?? [])
            : failedOpening is not null ? new("Scheduled signup opening failed", failedOpening.ScheduledFor, failedOpening.AttemptedAt,
                failedOpening.Details.Count > 0
                    ? failedOpening.Details.Select(detail => new ReadinessItem("SCHEDULED_OPENING_FAILED", detail, $"/Admin/Events/Schedule/{id}")).ToArray()
                    : failedOpening.Blockers.Select(code => DescribeBlocker(code, id, item.State)).ToArray()) : null;
        var overviewBlockers = new List<ReadinessItem>();
        if (item.State is EventState.Draft or EventState.SignupOpen)
            overviewBlockers.AddRange(SignupReadiness?.Blockers.Select(x => AddResolutionRoute(x, id, item.State)) ?? []);
        if (item.State is EventState.SignupClosed or EventState.Live)
            overviewBlockers.AddRange(StartReadiness?.Blockers ?? []);
        if (item.State == EventState.AwaitingFinalReview)
            overviewBlockers.AddRange(FinalReviewReadiness?.Blockers.Where(x => !x.Resolved).Select(x => AddResolutionRoute(new ReadinessItem(x.Key, x.Description, x.Link), id, item.State)) ?? []);
        if (ScheduledAction is not null)
            overviewBlockers.AddRange(ScheduledAction.Blockers);
        OverviewBlockers = overviewBlockers.DistinctBy(x => (x.Code, x.Description, x.Route)).ToList();
        EventVersion = item.Version; CompetitionId = CompetitionIntegration?.CompetitionId; NewCap = item.ParticipantCap ?? 0; NewSignupOpening = item.SignupOpensAt ?? timeProvider.GetUtcNow(); NewSignupClosing = item.SignupClosesAt ?? timeProvider.GetUtcNow().AddDays(1); EvidenceCodeActivatesAt = timeProvider.GetUtcNow(); EvidenceCodeActivatesAtLocal = DateTimePresentation.Format(EvidenceCodeActivatesAt.Value, "yyyy-MM-ddTHH:mm", item.Timezone, CultureInfo.InvariantCulture); ReopenUntil = timeProvider.GetUtcNow().AddHours(1); ReopenUntilLocal = DateTimePresentation.Format(ReopenUntil.Value, "yyyy-MM-ddTHH:mm", item.Timezone, CultureInfo.InvariantCulture); ReplacementEventEndsAt = item.EventEndsAt ?? timeProvider.GetUtcNow().AddHours(1); ReplacementEventEndsAtLocal = DateTimePresentation.Format(ReplacementEventEndsAt, "yyyy-MM-ddTHH:mm", item.Timezone, CultureInfo.InvariantCulture); return true;
    }
    private LifecycleActor Actor => new(User.GetAccountId()!.Value, User.Identity!.Name!);
    private Task<IActionResult> SignupResult(SignupLifecycleResult result, Guid id, string success)
    { TempData["StatusMessage"] = result.Succeeded ? Localize(success) : result.ProposedClose is { } close ? $"{result.Error} Proposed close: {DateTimePresentation.Format(close, "dd MMM yyyy, HH:mm", EventView?.Timezone, CultureInfo.CurrentCulture)}." : result.Error; if (result.Succeeded) TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString(); return Task.FromResult<IActionResult>(RedirectToPage(new { id })); }
    private Task AuditAsync(string action, BingoEvent item, string details, CancellationToken ct) => auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, action, "event", item.Id.ToString(), details, ct);
    private void SetStatus(string message, UiMessageType type) { TempData["StatusMessage"] = message; TempData[UiMessage.TypeKey] = type.ToString(); }
    private string CompetitionRefreshFailure(EventCompetitionRefreshResult result)
    {
        var retryAt = result.RetryAt is { } value ? DateTimePresentation.Format(value, "dd MMM yyyy, HH:mm", provider: CultureInfo.CurrentCulture) : null;
        return result.ErrorKind switch
        {
            "RateLimited" when retryAt is not null => Localize("Wise Old Man refresh is temporarily rate-limited. Try again after {0}.", retryAt),
            "RateLimited" => Localize("Wise Old Man refresh is temporarily rate-limited."),
            "NotFound" => Localize("Wise Old Man could not find that competition."),
            "Invalid" => Localize("Wise Old Man returned invalid competition details."),
            _ when retryAt is not null => Localize("Wise Old Man refresh is temporarily unavailable. Try again after {0}.", retryAt),
            _ => Localize("Wise Old Man refresh failed.")
        };
    }

    private string Localize(string key, params object[] arguments)
        => text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);
    private static string SignupConfirmationKey(Guid id, string kind) => $"ManageSignupConfirmation:{id}:{kind}";
    private async Task<IActionResult> PrepareConfirmation(Guid id, string action, CancellationToken ct)
    {
        if (!await dbContext.Events.AnyAsync(item => item.Id == id && item.State != EventState.Discarded, ct)) return NotFound();
        return RedirectToPage(new { id, confirm = action });
    }
    private bool HasBindingErrors(params string[] fields) => fields.Any(field => ModelState.TryGetValue(field, out var entry) && entry.Errors.Count > 0);
    private DateTimeOffset? ParseEventLocal(string? value, string timezoneId, string field, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!DateTime.TryParseExact(value, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var entered) || entered.Minute % 5 != 0)
        {
            ModelState.AddModelError(field, Localize("Choose a valid local date and time."));
            return null;
        }
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        var local = DateTime.SpecifyKind(entered, DateTimeKind.Unspecified);
        if (timezone.IsInvalidTime(local)) { ModelState.AddModelError(field, Localize("That local time does not exist because the clocks change at that time.")); return null; }
        if (timezone.IsAmbiguousTime(local)) { ModelState.AddModelError(field, Localize("{0} is ambiguous because of daylight-saving time. Choose another time.", label)); return null; }
        return new DateTimeOffset(local, timezone.GetUtcOffset(local)).ToUniversalTime();
    }
    public string EventDate(DateTimeOffset? value, string missing = "Not set")
    {
        return FormatEventDate(value, "dd MMM yyyy, HH:mm", missing);
    }
    public string EventDateOnly(DateTimeOffset? value, string missing = "Not set") => FormatEventDate(value, "dd MMM yyyy", missing);
    private string FormatEventDate(DateTimeOffset? value, string format, string missing)
    {
        if (value is null) return missing;
        return DateTimePresentation.Format(value.Value, format, EventView?.Timezone, CultureInfo.CurrentCulture);
    }
    public static IReadOnlyList<TimelineRow> EffectiveTimelineFor(EffectiveTimelineInput input)
    {
        var rows = new List<TimelineRow>();
        AddActualOrScheduled(rows, "Signups opened", "Signup opens", input.ActualSignupOpenedAt, input.SignupOpensAt, input.CancelledAt);
        AddActualOrScheduled(rows, "Signups closed", "Signup closes", input.ActualSignupClosedAt, input.SignupClosesAt, input.CancelledAt);
        AddScheduled(rows, "Draft time", input.DraftAt, input.CancelledAt);
        AddActualOrScheduled(rows, "Event started", "Event starts", input.ActualStartedAt, input.EventStartsAt, input.CancelledAt);
        AddActualOrScheduled(rows, "Event ended", "Event ends", input.ActualEndedAt, input.EventEndsAt, input.CancelledAt);
        AddActualOrScheduled(rows, "Submissions closed", "Submission cutoff", input.SubmissionsClosedAt, input.SubmissionCutoffAt, input.CancelledAt);
        if (input.CancelledAt is { } cancelledAt) rows.Add(new("Event cancelled", cancelledAt));
        return rows.OrderBy(row => row.At).ThenBy(row => row.Label, StringComparer.Ordinal).ToList();
    }
    private static void AddActualOrScheduled(List<TimelineRow> rows, string actualLabel, string scheduledLabel, DateTimeOffset? actual, DateTimeOffset? scheduled, DateTimeOffset? cancelledAt)
    {
        if (actual is { } actualAt) rows.Add(new(actualLabel, actualAt));
        else AddScheduled(rows, scheduledLabel, scheduled, cancelledAt);
    }
    private static void AddScheduled(List<TimelineRow> rows, string label, DateTimeOffset? scheduled, DateTimeOffset? cancelledAt)
    {
        if (scheduled is { } scheduledAt && (cancelledAt is null || scheduledAt <= cancelledAt)) rows.Add(new(label, scheduledAt));
    }
    public sealed record EventDetails(Guid Id, string Name, string Slug, string? Description, string Timezone, EventState State, DateTimeOffset? FirstPublicAt, bool BoardPublished, DateTimeOffset? SignupOpensAt, DateTimeOffset? SignupClosesAt, DateTimeOffset? DraftAt, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, DateTimeOffset? ActualSignupOpenedAt, DateTimeOffset? ActualSignupClosedAt, DateTimeOffset? ActualStartedAt, DateTimeOffset? ActualEndedAt, DateTimeOffset? EffectiveEndsAt, DateTimeOffset? SubmissionCutoffAt, DateTimeOffset? SubmissionsClosedAt, bool ScheduledSignupOpeningEnabled, DateTimeOffset? ReopenedCutoff, int ParticipantCap, int Confirmed, int Waiting, bool DraftLocked, bool EvidenceCodeEnabled, int ActualTeamCount, string? ActualTeamSize, string? ActualBoardSize, int? BoardRows, int? BoardColumns, int? ConfiguredBoardTileCount, int? ExpectedBoardCellCount, int? ExpectedTeamCount, int? ExpectedTeamSize, string? ExpectedBoardSize, bool CanStartEvent, DateTimeOffset? FinalizedAt, DateTimeOffset? ArchivedAt, DateTimeOffset? CancelledAt, bool IsHidden = false, DateTimeOffset? HiddenAt = null, Guid? HiddenByAccountId = null, string? HiddenReason = null);
    public sealed record EffectiveTimelineInput(DateTimeOffset? SignupOpensAt, DateTimeOffset? SignupClosesAt, DateTimeOffset? DraftAt, DateTimeOffset? EventStartsAt, DateTimeOffset? EventEndsAt, DateTimeOffset? ActualSignupOpenedAt, DateTimeOffset? ActualSignupClosedAt, DateTimeOffset? ActualStartedAt, DateTimeOffset? ActualEndedAt, DateTimeOffset? SubmissionCutoffAt, DateTimeOffset? SubmissionsClosedAt, DateTimeOffset? CancelledAt);
    public sealed record TimelineRow(string Label, DateTimeOffset At);
    public sealed record OverviewMilestone(string Label, DateTimeOffset? At);
    public static ReadinessItem ResolveBlocker(ReadinessItem item, Guid eventId, EventState eventState) => AddResolutionRoute(item, eventId, eventState);
    public static string BlockerActionLabel(ReadinessItem item) => item.Code switch
    {
        "PARTICIPANT_PLAYING_ASSIGNMENT_INVALID" => "Review participants",
        "DRAFT_NOT_FINALIZED" or "TEAM_ACCESS_MISSING" or "DRAFT_LOCKED" => "Review teams and draft",
        "BOARD_NOT_PUBLISHED" => "Review board",
        "SIGNUP_FORM_MISSING" or "SIGNUP_QUESTIONS_INVALID" or "SIGNUP_CODE_UNUSABLE" => "Review signup form",
        "CURRENT_EVENT_EXISTS" or "EVENT_WINDOW_OVERLAP" => "Review events",
        "SCHEDULE_INVALID" or "EVENT_START_REQUIRED" or "EVENT_END_REQUIRED" or "EVENT_WINDOW_INVALID" or "SIGNUP_CLOSE_REQUIRED" or "SIGNUP_CLOSE_NOT_FUTURE" or "SIGNUP_CLOSE_AFTER_EVENT_START" or "SCHEDULED_OPENING_INVALID" or "SCHEDULED_WINDOW_INVALID" or "SCHEDULED_OPENING_FAILED" => "Review schedule",
        _ when item.Code.StartsWith("UNACKNOWLEDGED_", StringComparison.Ordinal) => "Review schedule",
        _ when item.Route?.Contains("/Participant/", StringComparison.Ordinal) == true => "Review participants",
        _ => "Review configuration"
    };
    private static ReadinessItem AddResolutionRoute(ReadinessItem item, Guid eventId, EventState eventState)
        => item.Route is not null ? item : item with { Route = DescribeBlocker(item.Code, eventId, eventState).Route };
    private static ReadinessItem DescribeBlocker(string code, Guid eventId, EventState eventState) => code switch
    {
        "DRAFT_NOT_FINALIZED" => new(code, "Finalize the team draft.", $"/Admin/Events/Draft/{eventId}"),
        "BOARD_NOT_PUBLISHED" => new(code, "Publish the approved board.", $"/Admin/Events/Board/{eventId}"),
        "TEAM_ACCESS_MISSING" => new(code, "Give every active team a current Captain or enabled emergency credential.", $"/Admin/Events/Draft/{eventId}"),
        "PARTICIPANT_PLAYING_ASSIGNMENT_INVALID" => new(code, "Review the participant's current Playing assignment."),
        "LIFECYCLE_STATE_INVALID" when eventState == EventState.Draft => new(code, "Signup has not been opened and closed. Open signup, then close it before starting the event.", $"/Admin/Events/Manage/{eventId}"),
        "LIFECYCLE_STATE_INVALID" when eventState == EventState.SignupOpen => new(code, "Signup is still open. Close signup before starting the event.", $"/Admin/Events/Manage/{eventId}"),
        "LIFECYCLE_STATE_INVALID" => new(code, "The scheduled start was postponed until signup lifecycle requirements are resolved.", $"/Admin/Events/Manage/{eventId}"),
        "SCHEDULE_INVALID" => new(code, "Configure a valid event start and end.", $"/Admin/Events/Schedule/{eventId}"),
        "EVENT_START_REQUIRED" or "EVENT_END_REQUIRED" or "EVENT_WINDOW_INVALID" or "SIGNUP_CLOSE_REQUIRED" or "SIGNUP_CLOSE_NOT_FUTURE" or "SIGNUP_CLOSE_AFTER_EVENT_START" or "SCHEDULED_OPENING_INVALID" or "SCHEDULED_WINDOW_INVALID" => new(code, "Review the event schedule.", $"/Admin/Events/Schedule/{eventId}"),
        "SIGNUP_FORM_MISSING" or "SIGNUP_QUESTIONS_INVALID" or "SIGNUP_CODE_UNUSABLE" => new(code, "Review the signup form and its questions.", $"/Admin/Events/Questions/{eventId}"),
        "DRAFT_LOCKED" => new(code, "The draft has started; review the teams and draft.", $"/Admin/Events/Draft/{eventId}"),
        "CURRENT_EVENT_EXISTS" => new(code, "Another event is already Live, in final review, or finalized.", "/Admin/Events"),
        "EVENT_WINDOW_OVERLAP" => new(code, "The event window overlaps another active lifecycle window.", "/Admin/Events"),
        "DESCRIPTION_REQUIRED" => new(code, "Add a public event description.", $"/Admin/Events/Identity/{eventId}"),
        "PARTICIPANT_CAP_REQUIRED" => new(code, "Set a participant capacity.", $"/Admin/Events/Schedule/{eventId}"),
        _ when code.StartsWith("UNACKNOWLEDGED_", StringComparison.Ordinal) => new(code, "A signup warning became active after scheduling and needs Admin review.", $"/Admin/Events/Schedule/{eventId}"),
        _ => new(code, "Review the event configuration and resolve this lifecycle blocker.", $"/Admin/Events/Manage/{eventId}")
    };
    public sealed record ScheduledActionView(string Title, DateTimeOffset ScheduledFor, DateTimeOffset AttemptedAt, IReadOnlyList<ReadinessItem> Blockers);
    public sealed record EvidenceCodeRow(Guid Id, string Code, DateTimeOffset ActivatesAt, DateTimeOffset? RetiresAt, string? Note);
    public sealed record QuarantineAuditRow(string Action, DateTimeOffset OccurredAt, string ActorUsername, string? Details);
}
