using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Events;
using Bingo.Application.Signups;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Bingo.Infrastructure.Teams;
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
[AdminDesign]
public sealed class ManageModel(ApplicationDbContext dbContext, ISignupService signupService, EventParticipantCharacterService characterService, IAuditWriter auditWriter, IEventReadinessEvaluator readinessEvaluator, IEventSignupLifecycleService signupLifecycle, IEventLifecycleService eventLifecycle, IEventDestructiveLifecycleService destructiveLifecycle, TimeProvider timeProvider, IEventCompetitionSynchronizationService? competitionSynchronization = null, IStringLocalizer<SharedResource>? text = null, IHostEnvironment? environment = null, IEventFinalizationService? finalizationService = null, IEventQuarantineService? quarantine = null, IStringLocalizer<AuditResource>? auditText = null) : PageModel
{
    // U4 / OS-1: Overview.dc.html binding (OverviewPresenter). The old readiness block,
    // its blocker list and labels, the dead captain-access row and the development
    // all-controls preview are retired (brief 85, 42c §1.3).
    public OverviewView? Overview { get; private set; }
    public string CurrentJson { get; private set; } = "{}";
    // Razor HTML-encodes the attribute; keep Danish letters readable in the snapshot.
    private static readonly System.Text.Json.JsonSerializerOptions WebJson = new(System.Text.Json.JsonSerializerDefaults.Web) { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public Guid EventId { get; private set; }
    public string EventTimezone { get; private set; } = "UTC";
    public DateTimeOffset CurrentInstant => timeProvider.GetUtcNow();
    public bool HiddenView { get; private set; }
    public string Fmt(DateTimeOffset value) => DateTimePresentation.Format(value, "d MMM yyyy, HH':'mm", EventTimezone, CultureInfo.CurrentCulture);
    public IReadOnlyList<QuarantineAuditRow> QuarantineAuditHistory { get; private set; } = [];
    [BindProperty, Display(Name = "New participant cap")] public int NewCap { get; set; }
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
    [BindProperty] public bool ConfirmSignupAction { get; set; }
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
    [BindProperty, StringLength(2000), Display(Name = "Reason")] public string? QuarantineReason { get; set; }
    [BindProperty(SupportsGet = true, Name = "confirm")] public string? ConfirmationAction { get; set; }
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) { _ = characterService; _ = environment; return await LoadAsync(id, ct) ? Page() : NotFound(); }
    public async Task<IActionResult> OnPostStateAsync(Guid id, EventState target, CancellationToken ct)
    {
        return BadRequest("The legacy State handler is retired.");
    }
    // U4 transport: lifecycle dialogs post through AdminFetch (Accept: application/json)
    // and receive an OverviewOutcome; plain form posts keep the PRG fallback.
    public async Task<IActionResult> OnPostOpenSignupAsync(Guid id, CancellationToken ct)
    {
        if (WantsJson && await StaleAsync(id, ct) is { } stale) return stale;
        return await SignupResult(await signupLifecycle.OpenAsync(id, EventVersion, [], ConfirmSignupAction, Actor, ct), id, "Signups are open for {0}.", ct);
    }
    public async Task<IActionResult> OnPostCloseSignupAsync(Guid id, CancellationToken ct)
    {
        if (WantsJson && await StaleAsync(id, ct) is { } stale) return stale;
        return await SignupResult(await signupLifecycle.CloseAsync(id, EventVersion, ConfirmSignupAction, Actor, ct), id, "Signups are closed for {0}.", ct);
    }
    public async Task<IActionResult> OnPostReopenSignupAsync(Guid id, CancellationToken ct)
    {
        if (WantsJson && await StaleAsync(id, ct) is { } stale) return stale;
        return await SignupResult(await signupLifecycle.ReopenAsync(id, EventVersion, [], ConfirmSignupAction, Actor, ct), id, "Signups are open again for {0}.", ct);
    }
    // Retired capacity owner (DP:966–971/C4). D16 still refuses terminal POSTs
    // before this stub, preserving test14's exact Manage redirect/read-only result.
    public IActionResult OnPostCapacity(Guid id)
    {
        _ = signupService; // Preserve the existing constructor contract while retiring this handler.
        TempData["StatusMessage"] = Localize("Change the participant capacity on Signup setup.");
        return RedirectToPage("SignupSetup", new { id });
    }
    public async Task<IActionResult> OnPostSignupWindowAsync(Guid id, CancellationToken ct)
    {
        if (!await dbContext.Events.AnyAsync(e => e.Id == id, ct)) return NotFound();
        return RedirectToPage("Schedule", new { id });
    }
    // Retained prepare shims keep old compiled callers source-compatible; the
    // prepare/ladder workflow is no longer reachable from the page markup.
    [NonAction] public Task<IActionResult> OnPostPrepareSignupConfirmationAsync(Guid id, CancellationToken ct) => PrepareConfirmation(id, "signup", ct);
    [NonAction] public Task<IActionResult> OnPostPrepareStartConfirmationAsync(Guid id, CancellationToken ct) => PrepareConfirmation(id, "start", ct);
    [NonAction] public Task<IActionResult> OnPostPrepareEndConfirmationAsync(Guid id, CancellationToken ct) => PrepareConfirmation(id, "end", ct);
    [NonAction] public Task<IActionResult> OnPostPrepareResumeConfirmationAsync(Guid id, CancellationToken ct) => PrepareConfirmation(id, "resume", ct);
    [NonAction] public Task<IActionResult> OnPostPrepareDestructiveConfirmationAsync(Guid id, CancellationToken ct) => PrepareConfirmation(id, "destructive", ct);
    public Task<IActionResult> OnPostConfirmSignupAsync(Guid id, CancellationToken ct) =>
        Task.FromResult<IActionResult>(BadRequest(Localize("The old signup confirmation handler is retired. Use the Open, Close, or Reopen action.")));
    public async Task<IActionResult> OnPostStartEventAsync(Guid id, CancellationToken ct)
    {
        if (WantsJson && await StaleAsync(id, ct) is { } stale) return stale;
        var result = await eventLifecycle.StartNowAsync(id, EventVersion, ConfirmStartEvent, StartReason, Actor, ct);
        if (!result.Succeeded)
        {
            var message = result.Blockers is { Count: > 0 }
                ? string.Join(" ", result.Blockers.Select(blocker => LocalizeRefusal(LocalizeStartBlocker(blocker).Description)))
                : result.Error ?? "The event could not be started.";
            return WantsJson ? Refused(message, id) : await LifecycleFailureAsync(id, "start", Localize(message), ct);
        }
        return await AppliedAsync(id, "{0} is live.", ct);
    }
    public async Task<IActionResult> OnPostEndEventAsync(Guid id, CancellationToken ct)
    {
        if (WantsJson && await StaleAsync(id, ct) is { } stale) return stale;
        var result = await eventLifecycle.EndNowAsync(id, EventVersion, ConfirmEndEvent, EndReason, Actor, ct);
        if (!result.Succeeded)
            return WantsJson ? Refused(result.Error ?? "The event could not be ended.", id) : await LifecycleFailureAsync(id, "end", result.Error ?? Localize("The event could not be ended."), ct);
        return await AppliedAsync(id, "{0} ended and is in final review.", ct);
    }
    public async Task<IActionResult> OnPostResumeEventAsync(Guid id, CancellationToken ct)
    {
        var timezoneId = await dbContext.Events.AsNoTracking().Where(x => x.Id == id).Select(x => x.Timezone).SingleOrDefaultAsync(ct);
        if (timezoneId is null) return NotFound();
        if (WantsJson && await StaleAsync(id, ct) is { } stale) return stale;
        var replacementEnd = ParseEventLocal(ReplacementEventEndsAtLocal, timezoneId, nameof(ReplacementEventEndsAtLocal), "Replacement event end")
            ?? (string.IsNullOrWhiteSpace(ReplacementEventEndsAtLocal) && ReplacementEventEndsAt != default ? ReplacementEventEndsAt.ToUniversalTime() : null);
        if (WantsJson && FieldError(nameof(ReplacementEventEndsAtLocal)) is { } invalidEnd) return Invalid(invalidEnd, "until");
        var result = await eventLifecycle.ResumePrematureEndAsync(id, EventVersion, ConfirmResumeEvent, ResumeReason, replacementEnd, Actor, ct);
        if (!result.Succeeded)
            return WantsJson ? Refused(result.Error ?? "The event could not be resumed.", id) : await LifecycleFailureAsync(id, "resume", Localize(result.Error ?? "The event could not be resumed."), ct);
        return await AppliedAsync(id, "{0} is live again.", ct);
    }
    public async Task<IActionResult> OnPostDiscardAsync(Guid id, CancellationToken ct)
    {
        if (WantsJson && await StaleAsync(id, ct) is { } stale) return stale;
        var discardedName = await NameAsync(id, ct);
        var result = await destructiveLifecycle.DiscardAsync(id, EventVersion, ConfirmDestructiveAction, Actor, ct);
        if (!result.Succeeded)
            return WantsJson ? Refused(result.Error ?? "The event could not be discarded.", id) : await LifecycleFailureAsync(id, "destructive", result.Error ?? Localize("The event could not be discarded."), ct);
        if (WantsJson) return Outcome(new(true, "applied", Message: Localize("{0} was deleted.", discardedName ?? string.Empty), Location: "/Admin/Events/Index"));
        SetStatus(Localize("Event discarded."), UiMessageType.Success);
        return RedirectToPage("Index");
    }
    public async Task<IActionResult> OnPostCancelAsync(Guid id, CancellationToken ct)
    {
        if (WantsJson && await StaleAsync(id, ct) is { } stale) return stale;
        var result = await destructiveLifecycle.CancelAsync(id, EventVersion, ConfirmDestructiveAction, CancellationReason, Actor, ct);
        if (!result.Succeeded)
            return WantsJson ? Refused(result.Error ?? "The event could not be cancelled.", id) : await LifecycleFailureAsync(id, "destructive", result.Error ?? Localize("The event could not be cancelled."), ct);
        return await AppliedAsync(id, "{0} was cancelled.", ct);
    }
    public async Task<IActionResult> OnPostHideAsync(Guid id, CancellationToken ct)
    {
        if (!ConfirmDestructiveAction)
        {
            if (WantsJson) return Refused("Confirm that you want to hide this event.", id);
            SetStatus(Localize("Confirm that you want to hide this event."), UiMessageType.Error);
            return RedirectToPage(new { id });
        }
        var result = await (quarantine ?? throw new InvalidOperationException("Event quarantine is not configured.")).HideAsync(id, EventVersion, EventNameConfirmation, QuarantineReason, Actor, ct);
        if (WantsJson) return QuarantineOutcome(result, id, Localize("{0} is hidden.", await NameAsync(id, ct) ?? string.Empty));
        SetStatus(result.Succeeded ? Localize("Event hidden from all ordinary surfaces.") : result.Error ?? Localize("The event could not be hidden."), QuarantineSeverity(result));
        return result.Succeeded ? RedirectToPage("Index", new { filter = "hidden" }) : RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostRestoreHiddenAsync(Guid id, CancellationToken ct)
    {
        if (!ConfirmDestructiveAction)
        {
            if (WantsJson) return Refused("Confirm that you want to restore this event.", id);
            SetStatus(Localize("Confirm that you want to restore this event."), UiMessageType.Error);
            return RedirectToPage(new { id });
        }
        var result = await (quarantine ?? throw new InvalidOperationException("Event quarantine is not configured.")).RestoreAsync(id, EventVersion, EventNameConfirmation, QuarantineReason, Actor, ct);
        if (WantsJson) return QuarantineOutcome(result, id, Localize("{0} was restored.", await NameAsync(id, ct) ?? string.Empty));
        SetStatus(result.Succeeded ? Localize("Event restored with its lifecycle and retained history unchanged.") : result.Error ?? Localize("The event could not be restored."), QuarantineSeverity(result));
        // U4-Q3 (c): the plain event URL is the Super Admin's hidden view.
        return result.Succeeded ? RedirectToPage("Index") : RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostReopenSubmissionsAsync(Guid id, CancellationToken ct)
    {
        var item = await dbContext.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        var reopenUntil = ParseEventLocal(ReopenUntilLocal, item.Timezone, nameof(ReopenUntilLocal), "Reopen cutoff")
            ?? (string.IsNullOrWhiteSpace(ReopenUntilLocal) ? ReopenUntil?.ToUniversalTime() : null);
        if (WantsJson)
        {
            if (item.Version != EventVersion) return Outcome(new(false, "stale", Localize("This event changed in another request. Reload before reopening submissions.")));
            if (FieldError(nameof(ReopenUntilLocal)) is { } invalidUntil) return Invalid(invalidUntil, "until");
            if (reopenUntil is null) return Invalid(Localize("Choose a date and time."), "until");
            if (string.IsNullOrWhiteSpace(StateReason)) return Invalid(Localize("Enter a reason."), "reason");
        }
        if (reopenUntil is null || string.IsNullOrWhiteSpace(StateReason))
        {
            TempData["StatusMessage"] = Localize("A valid future cutoff and reason are required.");
            return RedirectToPage(new { id });
        }

        string? failure = null;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            if (item.Version != EventVersion)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                if (WantsJson) return Outcome(new(false, "stale", Localize("This event changed in another request. Reload before reopening submissions.")));
                TempData["StatusMessage"] = Localize("This event changed in another request. Reload before reopening submissions.");
                return RedirectToPage(new { id });
            }

            item.ReopenSubmissions(reopenUntil.Value, timeProvider.GetUtcNow());
            await AuditAsync("event.submissions_reopened", item, $"Until {reopenUntil:O}; {StateReason}", ct);
            await transaction.CommitAsync(ct);
            var message = Localize("Uploads are open until {0}.", DateTimePresentation.Format(reopenUntil.Value, "d MMM yyyy, HH':'mm", item.Timezone, CultureInfo.CurrentCulture));
            if (WantsJson) return Outcome(new(true, "applied", Message: message));
            TempData["StatusMessage"] = message;
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            failure = ex.Message;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            failure = "The submission window could not be reopened safely. Reload and try again.";
        }
        if (failure is not null)
        {
            if (WantsJson) return Refused(failure, id);
            TempData["StatusMessage"] = Localize(failure);
        }
        return RedirectToPage(new { id });
    }
    public Task<IActionResult> OnPostEnableEvidenceCodesAsync(Guid id, CancellationToken ct) => SetEvidenceCodeMode(id, true, ct);
    public Task<IActionResult> OnPostDisableEvidenceCodesAsync(Guid id, CancellationToken ct) => SetEvidenceCodeMode(id, false, ct);
    private async Task<IActionResult> SetEvidenceCodeMode(Guid id, bool enabled, CancellationToken ct)
    {
        var item = await dbContext.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            if (item.Version != EventVersion)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                return CodeOutcome(id, false, "This event changed in another request. Reload before changing verification codes.", "stale");
            }

            item.SetEvidenceCodeEnabled(enabled, timeProvider.GetUtcNow());
            await AuditAsync("event.evidence_code_mode", item, enabled ? "Enabled" : "Disabled", ct);
            await transaction.CommitAsync(ct);
            return CodeOutcome(id, true, enabled ? "Evidence codes are required." : "Evidence codes are off.");
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return CodeOutcome(id, false, ex.Message);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return CodeOutcome(id, false, "Verification codes could not be changed safely. Reload and try again.");
        }
    }
    public Task<IActionResult> OnPostCreateEvidenceCodeAsync(Guid id, CancellationToken ct) => CreateEvidenceCode(id, NewEvidenceCode, ct);
    private async Task<IActionResult> CreateEvidenceCode(Guid id, string? code, CancellationToken ct)
    {
        var item = await dbContext.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        if (HasBindingErrors(nameof(NewEvidenceCode), nameof(EvidenceCodeActivatesAtLocal), nameof(EvidenceCodeNote)))
            return CodeOutcome(id, false, "Check the verification code details and try again.", "invalid");
        if (string.IsNullOrWhiteSpace(code))
            return CodeOutcome(id, false, "Enter or generate a code first.", "invalid", "code");
        var activates = ParseEventLocal(EvidenceCodeActivatesAtLocal, item.Timezone, nameof(EvidenceCodeActivatesAtLocal), "Activation time");
        if (!string.IsNullOrWhiteSpace(EvidenceCodeActivatesAtLocal) && activates is null)
            return WantsJson && FieldError(nameof(EvidenceCodeActivatesAtLocal)) is { } invalidFrom
                ? Invalid(invalidFrom, "from")
                : CodeOutcome(id, false, "Check the verification code details and try again.", "invalid");
        var now = timeProvider.GetUtcNow();
        var activatedAt = activates ?? EvidenceCodeActivatesAt?.ToUniversalTime() ?? now;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        try
        {
            if (item.Version != EventVersion)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                return CodeOutcome(id, false, "This event changed in another request. Reload before creating a verification code.", "stale");
            }
            if (!item.EvidenceCodeEnabled)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                return CodeOutcome(id, false, "Enable evidence codes first.");
            }
            item.SetEvidenceCodeEnabled(item.EvidenceCodeEnabled, now);
            if (await dbContext.EvidenceCodes.AnyAsync(x => x.EventId == id && x.ActivatesAt == activatedAt, ct))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                return CodeOutcome(id, false, "Another code already activates at that exact time.", "invalid", "from");
            }

            var created = new EvidenceCode(Guid.NewGuid(), id, code, activatedAt, User.GetAccountId()!.Value, now, EvidenceCodeNote);
            dbContext.EvidenceCodes.Add(created);
            var codes = await dbContext.EvidenceCodes.Where(x => x.EventId == id).OrderBy(x => x.ActivatesAt).ToListAsync(ct);
            codes.Add(created);
            codes = codes.OrderBy(x => x.ActivatesAt).ToList();
            for (var index = 0; index < codes.Count; index++)
                codes[index].SetRetiresAt(index + 1 < codes.Count ? codes[index + 1].ActivatesAt : null);
            item.AdvanceVersion();
            await AuditAsync("evidence_code.created", item, $"Activates {activatedAt:O}", ct);
            await transaction.CommitAsync(ct);
            return CodeOutcome(id, true, Localize("Code {0} saved.", created.Code));
        }
        catch (InvalidOperationException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return CodeOutcome(id, false, ex.Message);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return CodeOutcome(id, false, "The verification code could not be saved safely. Reload and try again.");
        }
    }
    // Evidence codes keep their PRG response for plain posts (Pass3 tests construct
    // the page model directly); AdminFetch receives the same outcome as JSON.
    private IActionResult CodeOutcome(Guid id, bool succeeded, string message, string? outcome = null, string? field = null)
    {
        var localized = Localize(message);
        if (WantsJson) return Outcome(new(succeeded, outcome ?? (succeeded ? "applied" : "refused"), succeeded ? null : localized,
            field is null ? null : new Dictionary<string, string> { [field] = localized }, succeeded ? localized : null));
        TempData["StatusMessage"] = localized;
        return RedirectToPage(new { id });
    }
    private async Task<bool> LoadAsync(Guid id, CancellationToken ct)
    {
        // U4-Q3 (c): the plain URL opens the Super Admin's limited hidden view; ?hidden=true is accepted and ignored.
        var item = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id && e.State != EventState.Discarded, ct); if (item is null || item.IsHidden && !User.IsInRole("SuperAdmin")) return false;
        EventId = item.Id; EventTimezone = item.Timezone; HiddenView = item.IsHidden;
        if (item.IsHidden)
        {
            EventNameConfirmation = item.Name;
            QuarantineAuditHistory = await dbContext.AuditEntries.AsNoTracking()
                .Where(entry => entry.EventId == id && (entry.Action == "event.hidden" || entry.Action == "event.restored"))
                .OrderByDescending(entry => entry.OccurredAt)
                .Select(entry => new QuarantineAuditRow(entry.Action, entry.OccurredAt, entry.ActorUsername, entry.Details))
                .ToListAsync(ct);
        }
        var state = await CurrentStateAsync(item, ct);
        Overview = state.View;
        CurrentJson = System.Text.Json.JsonSerializer.Serialize(state, WebJson);
        EventVersion = item.Version;
        return true;
    }

    private async Task<OverviewInput> OverviewInputAsync(BingoEvent item, CancellationToken ct)
    {
        var id = item.Id; var now = timeProvider.GetUtcNow();
        var pre = item.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed;
        var participants = await dbContext.EventParticipants.AsNoTracking().Where(p => p.EventId == id).GroupBy(p => p.SignupStatus).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var activeTeamIds = await dbContext.Teams.AsNoTracking().Where(team => team.EventId == id && team.Active).Select(team => team.Id).ToListAsync(ct);
        var players = activeTeamIds.Count == 0 ? 0 : await dbContext.TeamMemberships.AsNoTracking().CountAsync(m => activeTeamIds.Contains(m.TeamId) && m.LeftAt == null, ct);
        var board = await dbContext.Boards.AsNoTracking().Where(value => value.EventId == id).Select(value => new { value.Id, value.Rows, value.Columns, value.State }).SingleOrDefaultAsync(ct);
        var configured = board is null ? 0 : await dbContext.BoardTiles.AsNoTracking().CountAsync(tile => tile.BoardId == board.Id, ct);
        var submissions = await dbContext.Submissions.AsNoTracking().Where(x => x.EventId == id).GroupBy(x => x.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var startBlockers = item.State == EventState.SignupClosed || pre && !item.IsHidden
            ? (await eventLifecycle.GetStartReadinessAsync(id, ct))?.Blockers.Select(LocalizeStartBlocker).ToArray() ?? [] : [];
        var signup = item.State == EventState.Draft && !item.IsHidden ? await readinessEvaluator.GetSignupReadinessAsync(id, SignupOpeningMode.OpenNow, now, ct) : null;
        var reopen = item.State == EventState.SignupClosed && !item.IsHidden ? await readinessEvaluator.GetSignupReadinessAsync(id, SignupOpeningMode.Reopen, now, ct) : null;
        var overlap = item.State is EventState.Draft or EventState.SignupClosed && !item.IsHidden ? await readinessEvaluator.GetCurrentEventOverlapAsync(id, ct) : null;
        var final = item.State == EventState.AwaitingFinalReview && finalizationService is not null && !item.IsHidden ? await finalizationService.GetReadinessAsync(id, ct) : null;
        var everFinalized = item.State == EventState.AwaitingFinalReview && await dbContext.EventFinalizations.AnyAsync(x => x.EventId == id, ct);
        var postponed = pre ? await dbContext.ScheduledEventStartAttempts.AsNoTracking().Where(x => x.EventId == id && x.ScheduledFor == item.EventStartsAt && x.ScheduledFor <= now && !x.Started && x.ResolvedAt == null).OrderByDescending(x => x.AttemptedAt).FirstOrDefaultAsync(ct) : null;
        var failedOpening = item.State == EventState.Draft ? await dbContext.ScheduledSignupOpeningAttempts.AsNoTracking().Where(x => x.EventId == id && x.ScheduledFor == item.SignupOpensAt && x.ScheduledFor <= now && !x.Opened && x.ResolvedAt == null).OrderByDescending(x => x.AttemptedAt).FirstOrDefaultAsync(ct) : null;
        var wom = competitionSynchronization is null || item.IsHidden ? null : await competitionSynchronization.GetAsync(id, ct);
        var codes = await dbContext.EvidenceCodes.AsNoTracking().Where(x => x.EventId == id).OrderByDescending(x => x.ActivatesAt).Select(x => new EvidenceCodeRow(x.Id, x.Code, x.ActivatesAt, x.RetiresAt, x.Note)).ToListAsync(ct);
        var other = pre || item.State == EventState.AwaitingFinalReview ? await eventLifecycle.GetOtherCurrentEventAsync(id, ct) : null;
        var canDiscard = pre
            && !await dbContext.EventParticipants.AnyAsync(x => x.EventId == id, ct)
            && !await dbContext.Teams.AnyAsync(x => x.EventId == id, ct)
            && !await dbContext.AccountEventAccesses.AnyAsync(x => x.EventId == id, ct)
            && !await dbContext.Submissions.AnyAsync(x => x.EventId == id, ct);
        var cancelledBy = item.State == EventState.Cancelled ? await dbContext.AuditEntries.AsNoTracking().Where(x => x.EventId == id && x.Action == "event.cancelled").OrderByDescending(x => x.OccurredAt).Select(x => x.ActorUsername).FirstOrDefaultAsync(ct) : null;
        var hiddenBy = item.IsHidden ? await dbContext.AuditEntries.AsNoTracking().Where(x => x.EventId == id && x.Action == "event.hidden").OrderByDescending(x => x.OccurredAt).Select(x => x.ActorUsername).FirstOrDefaultAsync(ct) : null;
        var placings = new List<OverviewPlacing>();
        if (item.State is EventState.Archived or EventState.Finalized)
        {
            var official = await dbContext.EventFinalizations.AsNoTracking().Where(x => x.EventId == id && x.UnfinalizedAt == null).OrderByDescending(x => x.Version).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
            if (official is { } finalId)
                placings = await dbContext.OfficialPlacements.AsNoTracking().Where(x => x.FinalizationId == finalId).OrderBy(x => x.Placement).ThenBy(x => x.TeamName)
                    .Select(x => new OverviewPlacing(x.TeamName, x.Placement, x.CompletedTiles)).ToListAsync(ct);
        }
        return new(item, now, HttpContext?.User.IsInRole("SuperAdmin") == true,
            participants.Where(x => x.Key == SignupStatus.Confirmed).Sum(x => x.Count), participants.Where(x => x.Key == SignupStatus.WaitingList).Sum(x => x.Count),
            activeTeamIds.Count, players, configured, board is null ? 0 : board.Rows * board.Columns, board?.State == BoardState.Published,
            await dbContext.ActiveRosterPublications(id).AnyAsync(ct),
            submissions.Where(x => x.Key == SubmissionStatus.Pending).Sum(x => x.Count), submissions.Where(x => x.Key == SubmissionStatus.Approved).Sum(x => x.Count),
            startBlockers, signup, reopen, overlap, final, everFinalized, postponed, failedOpening, wom, codes, other, canDiscard, cancelledBy, hiddenBy, placings,
            HttpContext is null ? string.Empty : $"{Request.Scheme}://{Request.Host}");
    }
    private LifecycleActor Actor => new(User.GetAccountId()!.Value, User.Identity!.Name!);

    // U4 current-state read for Check again, stale and "gone" (42c §1.5 item 14). Same
    // Admin/hidden/discarded filter as the page; it reports current values only and
    // cannot tell which request wrote them.
    public async Task<IActionResult> OnGetCurrentAsync(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var item = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id && e.State != EventState.Discarded, ct);
        if (item is null || item.IsHidden && !User.IsInRole("SuperAdmin")) return NotFound();
        return new JsonResult(await CurrentStateAsync(item, ct));
    }
    private async Task<OverviewCurrentState> CurrentStateAsync(BingoEvent item, CancellationToken ct)
    {
        var input = await OverviewInputAsync(item, ct);
        var view = new OverviewPresenter(input, (key, args) => Localize(key, args), CultureInfo.CurrentCulture).Build();
        var codes = input.Codes.OrderBy(x => x.ActivatesAt).Select(x => new OverviewCurrentCode(x.Code, x.ActivatesAt, x.RetiresAt)).ToList();
        // Stale banner: the latest recorded change to this event (actor and readable action).
        var last = await dbContext.AuditEntries.AsNoTracking().Where(x => x.EventId == item.Id).OrderByDescending(x => x.OccurredAt).Select(x => new { x.Action, x.ActorUsername }).FirstOrDefaultAsync(ct);
        var lastChange = last is null ? null : Localize("{0} ({1})", AuditPresenter.ActionLabels.TryGetValue(last.Action, out var label) ? auditText?[label].Value ?? label : last.Action, last.ActorUsername);
        // L6: the hidden view (U4-Q3 (c)) needs only the restore dialog; no other dialog, evidence code or last change is emitted.
        if (item.IsHidden)
            return new(item.Id, item.Version.ToString(CultureInfo.InvariantCulture), item.State.ToString(), true, null, null, false, [],
                view.Dialogs.Where(pair => pair.Key == "restore").ToDictionary(pair => pair.Key, pair => pair.Value),
                new OverviewCodes(false, false, string.Empty, [], view.Codes.Timezone, string.Empty), null, view);
        return new(item.Id, item.Version.ToString(CultureInfo.InvariantCulture), item.State.ToString(), item.IsHidden,
            item.ReopenedSubmissionCutoffAt, item.EventEndsAt, item.EvidenceCodeEnabled, codes, view.Dialogs, view.Codes, lastChange, view);
    }
    private bool WantsJson => Request.GetTypedHeaders().Accept?.Any(value => value.MediaType.Value == "application/json") == true;
    private static JsonResult Outcome(OverviewOutcome outcome) => new(outcome);
    private async Task<IActionResult?> StaleAsync(Guid id, CancellationToken ct)
    {
        // The version read when the dialog opened is checked first, so a changed
        // event is reported as stale and re-evaluated, not as a refusal.
        var version = await dbContext.Events.AsNoTracking().Where(x => x.Id == id).Select(x => (long?)x.Version).SingleOrDefaultAsync(ct);
        return version is { } current && current != EventVersion ? Outcome(new(false, "stale", Localize("This event changed while this was open."))) : null;
    }
    // Success toasts follow the reference ("Signups are open for X." etc.).
    private async Task<IActionResult> AppliedAsync(Guid id, string message, CancellationToken ct)
    {
        var text = Localize(message, await NameAsync(id, ct) ?? string.Empty);
        if (WantsJson) return Outcome(new(true, "applied", Message: text));
        SetStatus(text, UiMessageType.Success);
        return RedirectToPage(new { id });
    }
    private Task<string?> NameAsync(Guid id, CancellationToken ct) => dbContext.Events.AsNoTracking().Where(x => x.Id == id).Select(x => x.Name).SingleOrDefaultAsync(ct);
    private JsonResult Refused(string error, Guid id)
    {
        // A service refusal that is really a concurrent change stays a stale outcome.
        var stale = error.StartsWith("This event changed while", StringComparison.Ordinal);
        var field = error switch
        {
            "Enter a reason when ending the event before its configured end." or "Enter a reason for resuming the event." or "Enter a reason for cancelling the event." or "Enter a reason for hiding the event." => "reason",
            "Choose a future replacement event end." or "The replacement event end must be in the future." or "Choose a time in five-minute increments." or "The replacement event end must be after the event start." => "until",
            _ => null
        };
        var localized = LocalizeRefusal(error);
        _ = id;
        return Outcome(new(false, stale ? "stale" : field is null ? "refused" : "invalid", localized, field is null ? null : new Dictionary<string, string> { [field] = localized }));
    }
    private static JsonResult Invalid(string error, string field) => Outcome(new(false, "invalid", error, new Dictionary<string, string> { [field] = error }));
    private JsonResult QuarantineOutcome(EventQuarantineResult result, Guid id, string success) => result.Outcome switch
    {
        EventQuarantineOutcome.Applied => Outcome(new(true, "applied", Message: Localize(success))),
        EventQuarantineOutcome.Stale => Outcome(new(false, "stale", Localize(result.Error ?? "This event changed while this was open."))),
        EventQuarantineOutcome.ValidationFailed => Outcome(new(false, "invalid", Localize(result.Error ?? "Enter a reason."), result.FieldErrors.ToDictionary(x => x.Key, x => Localize(x.Value)))),
        _ => Refused(result.Error ?? "The event could not be changed.", id)
    };
    private string? FieldError(string field) => ModelState.TryGetValue(field, out var entry) && entry.Errors.Count > 0 ? entry.Errors[0].ErrorMessage : null;
    // Service refusals that name another event (S2, U4-Q4/Q5) are localized by pattern.
    private string LocalizeRefusal(string error)
    {
        var publish = System.Text.RegularExpressions.Regex.Match(error, "^Publish the results of (.+) first\\.$");
        if (publish.Success) return Localize("Publish the results of {0} first.", publish.Groups[1].Value);
        var still = System.Text.RegularExpressions.Regex.Match(error, "^(.+) is still the current event\\. Contact the Super Admin to archive it\\.$");
        if (still.Success) return Localize("{0} is still the current event. Contact the Super Admin to archive it.", still.Groups[1].Value);
        return Localize(error);
    }
    private async Task<IActionResult> SignupResult(SignupLifecycleResult result, Guid id, string success, CancellationToken ct)
    {
        if (result.Succeeded) return await AppliedAsync(id, success, ct);
        if (WantsJson) return Refused(result.Error ?? "The signup change could not be completed.", id);

        var postedVersion = EventVersion;
        if (!await LoadAsync(id, ct)) return NotFound();
        var versionChanged = EventVersion != postedVersion;
        ModelState.Remove(nameof(EventVersion));
        var message = result.ProposedClose is { } close
            ? $"{result.Error ?? Localize("Review the proposed signup close.")} Proposed close: {DateTimePresentation.Format(close, "dd MMM yyyy, HH:mm", EventTimezone, CultureInfo.CurrentCulture)}."
            : result.Error ?? Localize("The signup change could not be completed.");
        if (versionChanged) message = $"{message} {LifecycleRefreshNotice()}";
        SetStatus(message, result.ProposedClose is not null ? UiMessageType.Information : UiMessageType.Error);
        ConfirmationAction = "signup";
        return Page();
    }
    private ReadinessItem LocalizeStartBlocker(ReadinessItem blocker) =>
        blocker.Code == "DROP_PRICE_MISSING" && blocker.DescriptionArguments is [var itemNames]
            ? blocker with { Description = Localize("These drops have no catalogue GP value: {0}. Set a value in Admin Catalogue, then try again. An explicit 0 is valid.", itemNames) }
            : blocker;

    private async Task<IActionResult> LifecycleFailureAsync(Guid id, string confirmationAction, string message, CancellationToken ct)
    {
        var postedVersion = EventVersion;
        if (!await LoadAsync(id, ct)) return NotFound();
        var versionChanged = EventVersion != postedVersion;
        ModelState.Remove(nameof(EventVersion));
        if (versionChanged) message = $"{message} {LifecycleRefreshNotice()}";
        SetStatus(message, UiMessageType.Error);
        ConfirmationAction = confirmationAction;
        return Page();
    }
    private string LifecycleRefreshNotice() => Localize("The event details were refreshed. Your entries were kept. Review them before retrying.");
    private Task AuditAsync(string action, BingoEvent item, string details, CancellationToken ct) => auditWriter.WriteAndSaveAsync(User.GetAccountId(), User.Identity!.Name!, action, "event", item.Id.ToString(), details, item.Id, ct);
    private void SetStatus(string message, UiMessageType type) { TempData["StatusMessage"] = message; TempData[UiMessage.TypeKey] = type.ToString(); }
    private static UiMessageType QuarantineSeverity(EventQuarantineResult result) => result.Outcome switch
    {
        EventQuarantineOutcome.Applied => UiMessageType.Success,
        EventQuarantineOutcome.Stale => UiMessageType.Warning,
        _ => UiMessageType.Error
    };
    private string Localize(string key, params object[] arguments)
        => text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);
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
    public sealed record EffectiveTimelineInput(DateTimeOffset? SignupOpensAt, DateTimeOffset? SignupClosesAt, DateTimeOffset? DraftAt, DateTimeOffset? EventStartsAt, DateTimeOffset? EventEndsAt, DateTimeOffset? ActualSignupOpenedAt, DateTimeOffset? ActualSignupClosedAt, DateTimeOffset? ActualStartedAt, DateTimeOffset? ActualEndedAt, DateTimeOffset? SubmissionCutoffAt, DateTimeOffset? SubmissionsClosedAt, DateTimeOffset? CancelledAt);
    public sealed record TimelineRow(string Label, DateTimeOffset At);
    public sealed record OverviewOutcome(bool Succeeded, string Outcome, string? Error = null, IReadOnlyDictionary<string, string>? FieldErrors = null, string? Message = null, string? Location = null);
    public sealed record OverviewCurrentCode(string Code, DateTimeOffset ActivatesAt, DateTimeOffset? RetiresAt);
    public sealed record OverviewCurrentState(Guid EventId, string Version, string Phase, bool Hidden, DateTimeOffset? ReopenedUntil, DateTimeOffset? EventEndsAt, bool EvidenceCodesEnabled, IReadOnlyList<OverviewCurrentCode> EvidenceCodes,
        IReadOnlyDictionary<string, OverviewDialog> Dialogs, OverviewCodes Codes, string? LastChange, [property: System.Text.Json.Serialization.JsonIgnore] OverviewView View);
    public sealed record EvidenceCodeRow(Guid Id, string Code, DateTimeOffset ActivatesAt, DateTimeOffset? RetiresAt, string? Note);
    public sealed record QuarantineAuditRow(string Action, DateTimeOffset OccurredAt, string ActorUsername, string? Details);
}
