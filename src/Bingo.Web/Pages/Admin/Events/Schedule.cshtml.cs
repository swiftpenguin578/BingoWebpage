using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Access;
using Bingo.Application.Events;
using Bingo.Domain.Events;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class ScheduleModel(ApplicationDbContext db, IEventSignupLifecycleService schedules, IEventReadinessEvaluator readiness, TimeProvider time, IStringLocalizer<SharedResource>? text = null) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public Guid EventId { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public string EventSlug { get; private set; } = string.Empty;
    public string EventTimezone { get; private set; } = string.Empty;
    public EventState EventState { get; private set; }
    public bool ShowPublicBoard { get; private set; }
    public IReadOnlyList<ManageModel.TimelineRow> EffectiveTimeline { get; private set; } = [];
    public string? ActualSignupOpened { get; private set; }
    public string? ActualSignupClosed { get; private set; }
    public string? ScheduledSignupOpening { get; private set; }
    public string? ScheduledSignupClosing { get; private set; }
    public bool ScheduledSignupOpeningEnabled { get; private set; }
    public bool CanEditScheduledOpening { get; private set; }
    public bool CanEditSignupClosing { get; private set; }
    public bool CanEditDraftTime { get; private set; }
    public bool CanEditEventStart { get; private set; }
    public bool CanEditEventEnd { get; private set; }
    public bool CanEditCapacity { get; private set; }
    public DraftState? CurrentDraftState { get; private set; }
    public SignupReadiness? Readiness { get; private set; }
    public bool ParticipantsRelyOnSchedule { get; private set; }
    // Retained as a server-side diagnostic for older callers; the page no
    // longer renders a preview/acknowledgement ladder.
    public IReadOnlyList<ScheduleChangePreview> ChangePreview { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        CurrentDraftState = await DraftStateAsync(id, ct);
        Populate(item);
        var mode = item.State == EventState.SignupClosed ? SignupOpeningMode.Reopen : item.State == EventState.Draft ? SignupOpeningMode.ScheduleOpening : SignupOpeningMode.OpenNow;
        Readiness = await readiness.GetSignupReadinessAsync(id, mode, time.GetUtcNow(), ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        EventId = item.Id; EventName = item.Name;
        CurrentDraftState = await DraftStateAsync(id, ct);
        SetDisplay(item);
        if (Input.Version != item.Version) { ModelState.AddModelError(string.Empty, Localize("This event changed while you were editing it. Review the latest values and try again.")); return await Reload(item, ct); }
        if (!TryTimezone(item.Timezone, out var timezone)) { ModelState.AddModelError(string.Empty, Localize("The event timezone is unavailable.")); return await Reload(item, ct); }
        var values = RestoreLockedValues(item, Parse(timezone));
        var now = time.GetUtcNow();
        // A newly changed future opening establishes a schedule.  An existing
        // disabled flag remains disabled when an Admin only edits another
        // timestamp, so removing the toggle cannot activate old rows.
        var openingChanged = item.SignupOpensAt != values.SignupOpensAt;
        var preserveExistingSchedule = item.ScheduledSignupOpeningEnabled && item.SignupOpensAt == values.SignupOpensAt;
        values = values with
        {
            ScheduledSignupOpeningEnabled = item.State == EventState.Draft
                && values.SignupOpensAt is { } opening
                && (preserveExistingSchedule || (openingChanged && opening > now))
        };
        if (!ModelState.IsValid) return await Reload(item, ct);
        var unchangedOverdueOpening = item.ScheduledSignupOpeningEnabled && values.ScheduledSignupOpeningEnabled && item.SignupOpensAt == values.SignupOpensAt && item.SignupOpensAt <= now;
        var mode = values.ScheduledSignupOpeningEnabled && !unchangedOverdueOpening ? SignupOpeningMode.ScheduleOpening : item.State == EventState.SignupClosed ? SignupOpeningMode.Reopen : SignupOpeningMode.OpenNow;
        Readiness = await readiness.GetSignupReadinessAsync(id, mode, now, values, ct);
        var result = await schedules.SaveScheduleAsync(id, Input.Version, values, Input.ConfirmChanges, new LifecycleActor(User.GetAccountId()!.Value, User.Identity!.Name!), Input.EventEndReason, ct);
        if (!result.Succeeded)
        {
            var message = Localize(result.Error!);
            if (item.FirstPublicAt is not null && !Input.ConfirmChanges && ConsequenceChanged(item, values))
            {
                ModelState.AddModelError("Input.ConfirmChanges", message);
                ChangePreview = Preview(item, values, timezone);
            }
            else ModelState.AddModelError(string.Empty, message);
            return await Reload(item, ct, preserveInput: true);
        }
        TempData["StatusMessage"] = Localize("Event schedule updated."); TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
        return RedirectToPage("Manage", new { id });
    }

    private EventScheduleValues Parse(TimeZoneInfo timezone) => new(Parse("Input.SignupOpensLocal", Input.SignupOpensLocal, timezone), Parse("Input.SignupClosesLocal", Input.SignupClosesLocal, timezone), Parse("Input.DraftLocal", Input.DraftLocal, timezone), Parse("Input.EventStartsLocal", Input.EventStartsLocal, timezone), Parse("Input.EventEndsLocal", Input.EventEndsLocal, timezone), Input.ParticipantCap, Input.ScheduledSignupOpeningEnabled);
    private DateTimeOffset? Parse(string field, string? text, TimeZoneInfo timezone)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!DateTime.TryParseExact(text, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) { ModelState.AddModelError(field, Localize("Choose a valid local date and time.")); return null; }
        if (local.Minute % 5 != 0) { ModelState.AddModelError(field, Localize("Choose a time in five-minute increments.")); return null; }
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (timezone.IsInvalidTime(local)) { ModelState.AddModelError(field, Localize("That local time does not exist because the clocks change at that time.")); return null; }
        if (timezone.IsAmbiguousTime(local)) { ModelState.AddModelError(field, Localize("That local time is ambiguous because the clocks change at that time. Choose another time.")); return null; }
        return new DateTimeOffset(local, timezone.GetUtcOffset(local)).ToUniversalTime();
    }
    private string Localize(string key, params object[] arguments) => text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);
    private async Task<IActionResult> Reload(BingoEvent item, CancellationToken ct, bool preserveInput = true)
    {
        SetDisplay(item);
        if (!preserveInput) Populate(item);
        var mode = item.State == EventState.SignupClosed ? SignupOpeningMode.Reopen : item.State == EventState.Draft ? SignupOpeningMode.ScheduleOpening : SignupOpeningMode.OpenNow;
        Readiness ??= await readiness.GetSignupReadinessAsync(item.Id, mode, time.GetUtcNow(), ct);
        return Page();
    }
    private void Populate(BingoEvent item)
    {
        SetDisplay(item);
        if (!TryTimezone(item.Timezone, out var timezone)) return;
        Input = new InputModel { SignupOpensLocal = FormValue(item.SignupOpensAt, timezone), SignupClosesLocal = FormValue(item.SignupClosesAt, timezone), DraftLocal = FormValue(item.DraftAt, timezone), EventStartsLocal = FormValue(item.EventStartsAt, timezone), EventEndsLocal = FormValue(item.EventEndsAt, timezone), ParticipantCap = item.ParticipantCap, ScheduledSignupOpeningEnabled = item.ScheduledSignupOpeningEnabled, Version = item.Version };
    }
    private void SetDisplay(BingoEvent item)
    {
        EventId = item.Id;
        EventName = item.Name;
        EventSlug = item.Slug;
        EventTimezone = item.Timezone;
        EventState = item.State;
        ShowPublicBoard = item.FirstPublicAt is not null && item.BoardPublished;
        EffectiveTimeline = ManageModel.EffectiveTimelineFor(new(
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
        if (!TryTimezone(item.Timezone, out var timezone)) return;
        ActualSignupOpened = item.ActualSignupOpenedAt is null ? null : Display(item.ActualSignupOpenedAt, timezone);
        ActualSignupClosed = item.ActualSignupClosedAt is null ? null : Display(item.ActualSignupClosedAt, timezone);
        ScheduledSignupOpening = Display(item.SignupOpensAt, timezone);
        ScheduledSignupClosing = Display(item.SignupClosesAt, timezone);
        ScheduledSignupOpeningEnabled = item.ScheduledSignupOpeningEnabled;
        var now = time.GetUtcNow();
        var draftLockedForEditing = CurrentDraftState is DraftState.Running or DraftState.Paused;
        var finalizedDraft = CurrentDraftState == DraftState.Finalized;
        var preLiveSchedule = item.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed;
        CanEditScheduledOpening = item.State == EventState.Draft && !item.DraftLocked && (item.SignupOpensAt is null || item.SignupOpensAt > now);
        CanEditSignupClosing = preLiveSchedule && !draftLockedForEditing && !finalizedDraft && item.State != EventState.SignupClosed && (item.SignupClosesAt is null || item.SignupClosesAt > now);
        CanEditDraftTime = preLiveSchedule && !draftLockedForEditing && !finalizedDraft && (item.DraftAt is null || item.DraftAt > now);
        // The configured start remains editable until the first actual Live
        // transition.  In particular, a delayed setup must be able to repair
        // a past scheduled start before it starts the event.
        CanEditEventStart = item.State != EventState.Live && preLiveSchedule;
        CanEditEventEnd = item.State == EventState.Live
            ? true
            : preLiveSchedule;
        CanEditCapacity = preLiveSchedule && !draftLockedForEditing && !finalizedDraft;
        ParticipantsRelyOnSchedule = item.FirstPublicAt is not null;
    }
    public string EventDate(DateTimeOffset value)
    {
        return DateTimePresentation.Format(value, "dd MMM yyyy, HH:mm", EventTimezone, CultureInfo.CurrentCulture);
    }
    private static bool Changed(BingoEvent item, EventScheduleValues values) => item.SignupOpensAt != values.SignupOpensAt || item.SignupClosesAt != values.SignupClosesAt || item.DraftAt != values.DraftAt || item.EventStartsAt != values.EventStartsAt || item.EventEndsAt != values.EventEndsAt || item.ParticipantCap != values.ParticipantCap || item.ScheduledSignupOpeningEnabled != values.ScheduledSignupOpeningEnabled;
    private static bool ConsequenceChanged(BingoEvent item, EventScheduleValues values) => item.SignupOpensAt != values.SignupOpensAt || item.SignupClosesAt != values.SignupClosesAt || item.EventStartsAt != values.EventStartsAt || item.EventEndsAt != values.EventEndsAt || item.ParticipantCap != values.ParticipantCap || item.ScheduledSignupOpeningEnabled != values.ScheduledSignupOpeningEnabled;
    private EventScheduleValues RestoreLockedValues(BingoEvent item, EventScheduleValues values) => values with
    {
        SignupOpensAt = CanEditScheduledOpening ? values.SignupOpensAt : item.SignupOpensAt,
        SignupClosesAt = CanEditSignupClosing ? values.SignupClosesAt : item.SignupClosesAt,
        DraftAt = CanEditDraftTime ? values.DraftAt : item.DraftAt,
        EventStartsAt = CanEditEventStart ? values.EventStartsAt : item.EventStartsAt,
        EventEndsAt = CanEditEventEnd ? values.EventEndsAt : item.EventEndsAt,
        // Capacity is administered from Manage; Schedule only carries it
        // through the service boundary for compatibility with existing callers.
        ParticipantCap = item.ParticipantCap,
        ScheduledSignupOpeningEnabled = item.ScheduledSignupOpeningEnabled
    };
    private Task<DraftState?> DraftStateAsync(Guid eventId, CancellationToken ct) =>
        db.DraftSessions.AsNoTracking().Where(x => x.EventId == eventId).Select(x => (DraftState?)x.State).SingleOrDefaultAsync(ct);
    private static string? FormValue(DateTimeOffset? value, TimeZoneInfo timezone) => value is null ? null : DateTimePresentation.Format(value.Value, "yyyy-MM-ddTHH:mm", timezone.Id, CultureInfo.InvariantCulture);
    private string Display(DateTimeOffset? value, TimeZoneInfo timezone) => value is null ? Localize("Not set") : DateTimePresentation.Format(value.Value, "dd MMM yyyy, HH:mm", timezone.Id, CultureInfo.CurrentCulture);
    private static bool TryTimezone(string id, out TimeZoneInfo timezone) { try { timezone = TimeZoneInfo.FindSystemTimeZoneById(id); return true; } catch (TimeZoneNotFoundException) { timezone = null!; return false; } catch (InvalidTimeZoneException) { timezone = null!; return false; } }
    private List<ScheduleChangePreview> Preview(BingoEvent item, EventScheduleValues values, TimeZoneInfo timezone)
    {
        var preview = new List<ScheduleChangePreview>();
        Add("Signup opens", item.SignupOpensAt, values.SignupOpensAt);
        Add("Signup closes", item.SignupClosesAt, values.SignupClosesAt);
        Add("Draft time", item.DraftAt, values.DraftAt);
        Add("Event starts", item.EventStartsAt, values.EventStartsAt);
        Add("Event ends", item.EventEndsAt, values.EventEndsAt);
        if (item.EventEndsAt != values.EventEndsAt)
            preview.Add(new("Submission cutoff", Display(item.SubmissionCutoffAt, timezone), Display(values.EventEndsAt?.AddMinutes(30), timezone)));
        return preview;
        void Add(string label, DateTimeOffset? current, DateTimeOffset? proposed)
        {
            if (current != proposed) preview.Add(new(label, Display(current, timezone), Display(proposed, timezone)));
        }
    }
    public sealed record ScheduleChangePreview(string Label, string Current, string New);
    public sealed class InputModel
    {
        public string? SignupOpensLocal { get; set; }
        public string? SignupClosesLocal { get; set; }
        public string? DraftLocal { get; set; }
        public string? EventStartsLocal { get; set; }
        public string? EventEndsLocal { get; set; }
        [Range(1, 10000)] public int? ParticipantCap { get; set; }
        public bool ScheduledSignupOpeningEnabled { get; set; }
        public bool ConfirmChanges { get; set; }
        [StringLength(2000)] public string? EventEndReason { get; set; }
        public long Version { get; set; }
    }
}
