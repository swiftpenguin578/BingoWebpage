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
        if (!ModelState.IsValid) return await Reload(item, ct);
        var changed = Changed(item, values);
        var now = time.GetUtcNow();
        var unchangedOverdueOpening = item.ScheduledSignupOpeningEnabled && values.ScheduledSignupOpeningEnabled && item.SignupOpensAt == values.SignupOpensAt && item.SignupOpensAt <= now;
        var mode = values.ScheduledSignupOpeningEnabled && !unchangedOverdueOpening ? SignupOpeningMode.ScheduleOpening : item.State == EventState.SignupClosed ? SignupOpeningMode.Reopen : SignupOpeningMode.OpenNow;
        Readiness = await readiness.GetSignupReadinessAsync(id, mode, now, values, ct);
        if (values.ScheduledSignupOpeningEnabled && !unchangedOverdueOpening && Readiness is { CanProceed: false })
        {
            foreach (var blocker in Readiness.Blockers) ModelState.AddModelError(string.Empty, Localize(blocker.Description));
            return await Reload(item, ct, preserveInput: true);
        }
        var warnings = values.ScheduledSignupOpeningEnabled ? Readiness?.Warnings ?? [] : [];
        if (changed && (item.FirstPublicAt is not null || warnings.Count > 0) && !Input.ConfirmChanges)
        {
            ModelState.AddModelError("Input.ConfirmChanges", Localize("Review and confirm these schedule changes."));
            ChangePreview = await PreviewAsync(item, values, timezone, ct);
            return await Reload(item, ct, preserveInput: true);
        }
        var result = await schedules.SaveScheduleAsync(id, Input.Version, values, Input.ConfirmChanges, new LifecycleActor(User.GetAccountId()!.Value, User.Identity!.Name!), Input.EventEndReason, ct);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, Localize(result.Error!)); ChangePreview = changed ? await PreviewAsync(item, values, timezone, ct) : []; return await Reload(item, ct, preserveInput: true); }
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
        CanEditEventStart = item.State == EventState.Live ? false : preLiveSchedule && (finalizedDraft || !draftLockedForEditing) && (item.EventStartsAt is null || item.EventStartsAt > now);
        CanEditEventEnd = item.State == EventState.Live
            ? item.EventEndsAt is { } liveEnd && liveEnd > now
            : preLiveSchedule && (finalizedDraft || !draftLockedForEditing) && (item.EventEndsAt is null || item.EventEndsAt > now);
        CanEditCapacity = preLiveSchedule && !draftLockedForEditing && !finalizedDraft;
    }
    public string EventDate(DateTimeOffset value)
    {
        return DateTimePresentation.Format(value, "dd MMM yyyy, HH:mm", EventTimezone, CultureInfo.CurrentCulture);
    }
    private static bool Changed(BingoEvent item, EventScheduleValues values) => item.SignupOpensAt != values.SignupOpensAt || item.SignupClosesAt != values.SignupClosesAt || item.DraftAt != values.DraftAt || item.EventStartsAt != values.EventStartsAt || item.EventEndsAt != values.EventEndsAt || item.ParticipantCap != values.ParticipantCap || item.ScheduledSignupOpeningEnabled != values.ScheduledSignupOpeningEnabled;
    private EventScheduleValues RestoreLockedValues(BingoEvent item, EventScheduleValues values) => values with
    {
        SignupOpensAt = CanEditScheduledOpening ? values.SignupOpensAt : item.SignupOpensAt,
        SignupClosesAt = CanEditSignupClosing ? values.SignupClosesAt : item.SignupClosesAt,
        DraftAt = CanEditDraftTime ? values.DraftAt : item.DraftAt,
        EventStartsAt = CanEditEventStart ? values.EventStartsAt : item.EventStartsAt,
        EventEndsAt = CanEditEventEnd ? values.EventEndsAt : item.EventEndsAt,
        ParticipantCap = CanEditCapacity ? values.ParticipantCap : item.ParticipantCap,
        ScheduledSignupOpeningEnabled = CanEditScheduledOpening ? values.ScheduledSignupOpeningEnabled && values.SignupOpensAt is not null : item.ScheduledSignupOpeningEnabled
    };
    private Task<DraftState?> DraftStateAsync(Guid eventId, CancellationToken ct) =>
        db.DraftSessions.AsNoTracking().Where(x => x.EventId == eventId).Select(x => (DraftState?)x.State).SingleOrDefaultAsync(ct);
    private async Task<IReadOnlyList<ScheduleChangePreview>> PreviewAsync(BingoEvent item, EventScheduleValues values, TimeZoneInfo timezone, CancellationToken ct)
    {
        var preview = new List<ScheduleChangePreview>();
        Add("Signup opens", item.SignupOpensAt, values.SignupOpensAt);
        Add("Signup closes", item.SignupClosesAt, values.SignupClosesAt);
        Add("Draft time", item.DraftAt, values.DraftAt);
        Add("Event starts", item.EventStartsAt, values.EventStartsAt);
        Add("Event ends", item.EventEndsAt, values.EventEndsAt);
        if (item.EventEndsAt != values.EventEndsAt)
            preview.Add(new("Submission cutoff", Display(item.SubmissionCutoffAt, timezone), Display(values.EventEndsAt?.AddMinutes(30), timezone)));
        if (item.ParticipantCap != values.ParticipantCap)
        {
            var effect = string.Empty;
            if (values.ParticipantCap > item.ParticipantCap && (item.State is EventState.SignupOpen or EventState.SignupClosed) && !item.DraftLocked)
            {
                var participants = db.EventParticipants.Where(participant => participant.EventId == item.Id && !db.TeamMemberships.Any(membership => membership.EventParticipantId == participant.Id && membership.LeftAt == null && db.Teams.Any(team => team.Id == membership.TeamId && team.EventId == item.Id && team.Active && team.FormationType == Bingo.Domain.Teams.TeamFormationType.Preformed)));
                var confirmed = await participants.CountAsync(x => x.SignupStatus == Bingo.Domain.Signups.SignupStatus.Confirmed, ct);
                var waiting = await participants.CountAsync(x => x.SignupStatus == Bingo.Domain.Signups.SignupStatus.WaitingList, ct);
                effect = $" ({Localize("{0} waiting participant(s) will be promoted", Math.Min(waiting, Math.Max(0, values.ParticipantCap!.Value - confirmed)))})";
            }
            preview.Add(new("Maximum players", item.ParticipantCap?.ToString(CultureInfo.CurrentCulture) ?? Localize("Not set"), (values.ParticipantCap?.ToString(CultureInfo.CurrentCulture) ?? Localize("Not set")) + effect));
        }
        if (item.ScheduledSignupOpeningEnabled != values.ScheduledSignupOpeningEnabled)
            preview.Add(new("Automatic signup opening", Localize(item.ScheduledSignupOpeningEnabled ? "On" : "Off"), Localize(values.ScheduledSignupOpeningEnabled ? "On" : "Off")));
        return preview;
        void Add(string label, DateTimeOffset? current, DateTimeOffset? proposed) { if (current != proposed) preview.Add(new(label, Display(current, timezone), Display(proposed, timezone))); }
    }
    private static string? FormValue(DateTimeOffset? value, TimeZoneInfo timezone) => value is null ? null : DateTimePresentation.Format(value.Value, "yyyy-MM-ddTHH:mm", timezone.Id, CultureInfo.InvariantCulture);
    private string Display(DateTimeOffset? value, TimeZoneInfo timezone) => value is null ? Localize("Not set") : DateTimePresentation.Format(value.Value, "dd MMM yyyy, HH:mm", timezone.Id, CultureInfo.CurrentCulture);
    private static bool TryTimezone(string id, out TimeZoneInfo timezone) { try { timezone = TimeZoneInfo.FindSystemTimeZoneById(id); return true; } catch (TimeZoneNotFoundException) { timezone = null!; return false; } catch (InvalidTimeZoneException) { timezone = null!; return false; } }
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
