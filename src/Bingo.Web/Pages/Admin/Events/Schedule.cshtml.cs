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
[AdminDesign]
public sealed class ScheduleModel(ApplicationDbContext db, IEventSignupLifecycleService schedules, IEventReadinessEvaluator readiness, TimeProvider time, IStringLocalizer<SharedResource>? text = null) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public Guid EventId { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public string EventSlug { get; private set; } = string.Empty;
    public string EventTimezone { get; private set; } = string.Empty;
    public EventState EventState { get; private set; }
    public bool HasUnresolvableTimezone => !TryTimezone(EventTimezone, out _);
    public string DisplayTimezone => HasUnresolvableTimezone ? "UTC" : EventTimezone;
    public string EventTimezoneLabel => TryTimezone(EventTimezone, out var zone) ? $"{EventTimezone} (UTC{TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone):zzz})" : "UTC";
    public string LocalDate(string? value) => DateTime.TryParseExact(value, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date.ToString("d MMM yyyy, HH':'mm", CultureInfo.CurrentCulture) : Localize("Not set");
    public DateTimeOffset CurrentInstant => time.GetUtcNow();
    public string? ActualEventStarted { get; private set; }
    public string? ActualEventEnded { get; private set; }
    public string ScheduleNotice => EventState switch
    {
        EventState.Live => Localize("{0} is live, so only its end can change.", EventName),
        EventState.AwaitingFinalReview => Localize("{0} has ended and is in final review, so its schedule is history. To accept uploads again or resume the event, use Overview.", EventName),
        EventState.Archived => Localize("{0} is archived, so its schedule can’t change.", EventName),
        EventState.Cancelled => Localize("{0} was cancelled, so its schedule can’t change.", EventName),
        EventState.Finalized => Localize("{0} is finished, so its schedule can’t change.", EventName),
        _ when CurrentDraftState is DraftState.Running or DraftState.Paused => Localize("The team draft is underway, so signup times and the draft time are locked. The event start and end can still change."),
        _ when CurrentDraftState == DraftState.Finalized => Localize("The team draft is finalized, so only the event start and end can change."),
        _ => string.Empty
    };
    public string FieldLock(string key)
    {
        var drafting = CurrentDraftState is DraftState.Running or DraftState.Paused;
        var finalized = CurrentDraftState == DraftState.Finalized;
        if (EventState == EventState.Cancelled && !(key == "signupOpensAt" && ActualSignupOpened is not null) && !(key == "signupClosesAt" && ActualSignupClosed is not null)) return Localize("Kept as history; the event was cancelled.");
        return key switch
        {
            "signupOpensAt" => ActualSignupOpened is { } opened ? Localize("Signups opened {0}.", opened) : Localize("This time has passed. Open signups on Overview."),
            "signupClosesAt" when ActualSignupClosed is { } closed => Localize("Signups closed {0}.", closed) + (EventState == EventState.SignupClosed && !drafting && !finalized ? " " + Localize("To set a new closing time, reopen signups on Overview.") : ""),
            "signupClosesAt" or "draftAt" when drafting => Localize("Locked while the team draft is underway."),
            "signupClosesAt" or "draftAt" when finalized => Localize("Locked after the team draft was finalized."),
            "signupClosesAt" => Localize("This boundary has passed."),
            "draftAt" => Localize(EventState is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed ? "This time has passed." : "Locked after the event started."),
            "eventStartsAt" when ActualEventStarted is { } started => Localize("Started {0}.", started),
            "eventEndsAt" when ActualEventEnded is { } ended => Localize("Ended {0}.", ended),
            _ => Localize("Locked.")
        };
    }
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
    public DraftState? CurrentDraftState { get; private set; }
    public SignupReadiness? Readiness { get; private set; }
    public bool HasPublicExposure { get; private set; }
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

    // Same Admin/visibility/lifecycle filters as the rendered page. This observes
    // current values only; it cannot identify which request wrote them.
    public async Task<IActionResult> OnGetCurrentAsync(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var current = await db.Events.AsNoTracking().Where(x => x.Id == id)
            .Select(item => new { Item = item, DraftState = db.DraftSessions.Where(draft => draft.EventId == item.Id).Select(draft => (DraftState?)draft.State).SingleOrDefault() })
            .SingleOrDefaultAsync(ct);
        if (current is null) return NotFound();
        var item = current.Item;
        CurrentDraftState = current.DraftState;
        SetDisplay(item);
        return new JsonResult(CurrentSnapshot);
    }

    public object CurrentSnapshot { get; private set; } = new { };
    private object Snapshot(BingoEvent item) => new
        {
            eventId = item.Id,
            version = item.Version.ToString(CultureInfo.InvariantCulture),
            timezone = item.Timezone,
            displayTimezone = DisplayTimezone,
            phase = item.State.ToString(),
            draftState = CurrentDraftState?.ToString(),
            values = new
            {
                signupOpensAt = Instant(item.SignupOpensAt), signupClosesAt = Instant(item.SignupClosesAt),
                draftAt = Instant(item.DraftAt), eventStartsAt = Instant(item.EventStartsAt),
                eventEndsAt = Instant(item.EventEndsAt),
                scheduledSignupOpeningEnabled = item.ScheduledSignupOpeningEnabled
            },
            editable = new
            {
                signupOpensAt = CanEditScheduledOpening, signupClosesAt = CanEditSignupClosing,
                draftAt = CanEditDraftTime, eventStartsAt = CanEditEventStart, eventEndsAt = CanEditEventEnd
            }
        };

    private static string? Instant(DateTimeOffset? value) => value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        EventId = item.Id; EventName = item.Name;
        CurrentDraftState = await DraftStateAsync(id, ct);
        SetDisplay(item);
        if (Input.Version != item.Version) { ModelState.AddModelError(string.Empty, Localize("This event changed while you were editing it. Review the latest values and try again.")); return await Reload(item, ct); }
        if (!TryTimezone(item.Timezone, out var timezone)) { ModelState.AddModelError(string.Empty, Localize("The event timezone is unavailable.")); return await Reload(item, ct); }
        var values = RestoreLockedValues(item, Parse(item, timezone));
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
            var message = LocalizeRefusal(result.Error!);
            if (result.Error is "Confirm the Live event-end change before saving." or "Confirm the schedule consequence before saving.")
            {
                ModelState.AddModelError("Input.ConfirmChanges", message);
                ChangePreview = Preview(item, values, timezone);
            }
            else
                foreach (var field in ScheduleErrorFields(result.Error!, values))
                    ModelState.AddModelError(field, message);
            return await Reload(item, ct, preserveInput: true);
        }
        TempData["StatusMessage"] = Localize("Event schedule updated."); TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
        return RedirectToPage("Schedule", new { id });
    }

    private EventScheduleValues Parse(BingoEvent item, TimeZoneInfo timezone) => new(
        PreserveOrParse("Input.SignupOpensLocal", Input.SignupOpensLocal, item.SignupOpensAt, CanEditScheduledOpening, timezone),
        PreserveOrParse("Input.SignupClosesLocal", Input.SignupClosesLocal, item.SignupClosesAt, CanEditSignupClosing, timezone),
        PreserveOrParse("Input.DraftLocal", Input.DraftLocal, item.DraftAt, CanEditDraftTime, timezone),
        PreserveOrParse("Input.EventStartsLocal", Input.EventStartsLocal, item.EventStartsAt, CanEditEventStart, timezone),
        PreserveOrParse("Input.EventEndsLocal", Input.EventEndsLocal, item.EventEndsAt, CanEditEventEnd, timezone),
        item.ParticipantCap, item.ScheduledSignupOpeningEnabled);

    private DateTimeOffset? PreserveOrParse(string field, string? submitted, DateTimeOffset? original, bool editable, TimeZoneInfo timezone)
    {
        // The version was checked first. Display equality retains the authoritative
        // instant, including precision and either valid repeated-hour offset.
        if (!editable || string.Equals(submitted ?? string.Empty, FormValue(original, timezone) ?? string.Empty, StringComparison.Ordinal))
            return original;
        return Parse(field, submitted, timezone);
    }
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
    private static IReadOnlyList<string> ScheduleErrorFields(string error, EventScheduleValues values)
    {
        if (error == "Published event start and end times cannot be cleared.")
            return new[] { values.EventStartsAt is null ? "Input.EventStartsLocal" : null, values.EventEndsAt is null ? "Input.EventEndsLocal" : null }.OfType<string>().ToArray();
        if (error == "Enter a reason for changing the Live event end.") return ["Input.EventEndReason"];
        if (error is "Confirm the Live event-end change before saving." or "Confirm the schedule consequence before saving.") return ["Input.ConfirmChanges"];
        if (error == "The linked Wise Old Man competition must match the configured website UTC window exactly.")
            return ["Input.EventStartsLocal", "Input.EventEndsLocal"];
        foreach (var (label, field) in new[]
        {
            ("Signup opening", "Input.SignupOpensLocal"), ("Signup closing", "Input.SignupClosesLocal"),
            ("Draft time", "Input.DraftLocal"), ("Event start", "Input.EventStartsLocal"), ("Event end", "Input.EventEndsLocal")
        })
        {
            if (error == $"{label} is locked because that boundary has passed." || error == $"A changed {label.ToLowerInvariant()} must be in the future.") return [field];
        }
        return error switch
        {
            "Automatic signup opening requires a signup opening time." or "A scheduled signup opening must be configured in the future." or
            "Signup opening is locked after signup has opened." => ["Input.SignupOpensLocal"],
            "Automatic signup opening requires a signup closing time." or "Signup closing must be in the future." or
            "Signup closing must be after the scheduled opening." or "Signup closing must be after signup opening." or
            "Signup closing must be no later than event start." or
            "Signup closing is read-only after signup has closed; use Reopen to establish a new closing time." or
            "Signups are open, so they need a closing time. Set a new closing time or close signups now." => ["Input.SignupClosesLocal"],
            "An event start is required." => ["Input.EventStartsLocal"],
            "An event end is required." or "The event end must be in the future." or "Event end must be after event start." => ["Input.EventEndsLocal"],
            _ => [string.Empty] // Cross-event overlap, authorization and stale saves remain form errors.
        };
    }

    private string LocalizeRefusal(string error)
    {
        const string overlap = "This event window overlaps ";
        if (error.StartsWith(overlap, StringComparison.Ordinal) && error.EndsWith('.'))
            return Localize("This event window overlaps {0}.", error[overlap.Length..^1]);
        var capacity = System.Text.RegularExpressions.Regex.Match(error, @"^The participant cap cannot be lower than the (\d+) confirmed participant\(s\)\.$");
        return capacity.Success ? Localize("The participant cap cannot be lower than the {0} confirmed participant(s).", capacity.Groups[1].Value) : Localize(error);
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
        if (!TryTimezone(item.Timezone, out var timezone)) timezone = TimeZoneInfo.Utc;
        Input = new InputModel { SignupOpensLocal = FormValue(item.SignupOpensAt, timezone), SignupClosesLocal = FormValue(item.SignupClosesAt, timezone), DraftLocal = FormValue(item.DraftAt, timezone), EventStartsLocal = FormValue(item.EventStartsAt, timezone), EventEndsLocal = FormValue(item.EventEndsAt, timezone), ScheduledSignupOpeningEnabled = item.ScheduledSignupOpeningEnabled, Version = item.Version };
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
        HasPublicExposure = item.FirstPublicAt is not null;
        CurrentSnapshot = Snapshot(item);
        var supportedTimezone = TryTimezone(item.Timezone, out var timezone);
        if (!supportedTimezone) timezone = TimeZoneInfo.Utc;
        ActualEventStarted = item.ActualStartedAt is null ? null : Display(item.ActualStartedAt, timezone);
        ActualEventEnded = item.ActualEndedAt is null ? null : Display(item.ActualEndedAt, timezone);
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
        HasPublicExposure = item.FirstPublicAt is not null;
        if (!supportedTimezone)
            CanEditScheduledOpening = CanEditSignupClosing = CanEditDraftTime = CanEditEventStart = CanEditEventEnd = false;
        CurrentSnapshot = Snapshot(item);
    }
    public string EventDate(DateTimeOffset value)
    {
        return DateTimePresentation.Format(value, "d MMM yyyy, HH':'mm", EventTimezone, CultureInfo.CurrentCulture);
    }
    private EventScheduleValues RestoreLockedValues(BingoEvent item, EventScheduleValues values) => values with
    {
        SignupOpensAt = CanEditScheduledOpening ? values.SignupOpensAt : item.SignupOpensAt,
        SignupClosesAt = CanEditSignupClosing ? values.SignupClosesAt : item.SignupClosesAt,
        DraftAt = CanEditDraftTime ? values.DraftAt : item.DraftAt,
        EventStartsAt = CanEditEventStart ? values.EventStartsAt : item.EventStartsAt,
        EventEndsAt = CanEditEventEnd ? values.EventEndsAt : item.EventEndsAt,
        // Capacity is administered from Signup setup; Schedule only carries it
        // through the service boundary for compatibility with existing callers.
        ParticipantCap = item.ParticipantCap,
        ScheduledSignupOpeningEnabled = item.ScheduledSignupOpeningEnabled
    };
    private Task<DraftState?> DraftStateAsync(Guid eventId, CancellationToken ct) =>
        db.DraftSessions.AsNoTracking().Where(x => x.EventId == eventId).Select(x => (DraftState?)x.State).SingleOrDefaultAsync(ct);
    private static string? FormValue(DateTimeOffset? value, TimeZoneInfo timezone) => value is null ? null : DateTimePresentation.Format(value.Value, "yyyy-MM-ddTHH:mm", timezone.Id, CultureInfo.InvariantCulture);
    private string Display(DateTimeOffset? value, TimeZoneInfo timezone) => value is null ? Localize("Not set") : DateTimePresentation.Format(value.Value, "d MMM yyyy, HH':'mm", timezone.Id, CultureInfo.CurrentCulture);
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
        public bool ScheduledSignupOpeningEnabled { get; set; }
        public bool ConfirmChanges { get; set; }
        [StringLength(2000)] public string? EventEndReason { get; set; }
        public long Version { get; set; }
    }
}
