using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Access;
using Bingo.Application.Events;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class ScheduleModel(ApplicationDbContext db, IEventSignupLifecycleService schedules, IEventReadinessEvaluator readiness, TimeProvider time) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public Guid EventId { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public string EventTimezone { get; private set; } = string.Empty;
    public EventState EventState { get; private set; }
    public string? ActualSignupOpened { get; private set; }
    public string? ActualSignupClosed { get; private set; }
    public string? ScheduledSignupOpening { get; private set; }
    public bool ScheduledSignupOpeningEnabled { get; private set; }
    public bool CanEditScheduledOpening => EventState == EventState.Draft;
    public SignupReadiness? Readiness { get; private set; }
    public IReadOnlyList<TimePreview> PublicPreview { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
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
        SetDisplay(item);
        if (Input.Version != item.Version) { ModelState.AddModelError(string.Empty, "This event changed while you were editing it. Review the latest values and try again."); return await Reload(item, ct); }
        if (!TryTimezone(item.Timezone, out var timezone)) { ModelState.AddModelError(string.Empty, "The event timezone is unavailable."); return await Reload(item, ct); }
        var values = Parse(timezone, item.ParticipantCap);
        if (!ModelState.IsValid) return await Reload(item, ct);
        values = ApplyLifecycleScheduleRules(item, values);
        if (item.State == EventState.Draft && values.SignupOpensAt is not null)
        {
            var now = time.GetUtcNow();
            if (values.SignupOpensAt is null || values.SignupOpensAt <= now) ModelState.AddModelError("Input.SignupOpensLocal", "A scheduled signup opening must be in the future.");
            if (values.SignupClosesAt is null) ModelState.AddModelError("Input.SignupClosesLocal", "A scheduled signup opening requires a signup closing time.");
            if (values.SignupOpensAt is { } opens && values.SignupClosesAt is { } closes && closes <= opens) ModelState.AddModelError("Input.SignupClosesLocal", "Signup closing must be after the scheduled opening.");
            if (!ModelState.IsValid) return await Reload(item, ct);
        }
        var changed = Changed(item, values);
        if (changed && item.FirstPublicAt is not null && !Input.ConfirmPublicScheduleChange)
        {
            ModelState.AddModelError("Input.ConfirmPublicScheduleChange", "Review the participant-facing time preview and confirm this schedule change.");
            PublicPreview = Preview(item, values, timezone);
            return await Reload(item, ct, preserveInput: true);
        }
        var result = await schedules.SaveScheduleAsync(id, Input.Version, values, Input.AcknowledgeScheduledWarnings, Input.ConfirmPublicScheduleChange, Input.Reason, new LifecycleActor(User.GetAccountId()!.Value, User.Identity!.Name!), ct);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); PublicPreview = changed ? Preview(item, values, timezone) : []; return await Reload(item, ct, preserveInput: true); }
        TempData["StatusMessage"] = "Event schedule updated."; TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
        return RedirectToPage("Manage", new { id });
    }

    private EventScheduleValues Parse(TimeZoneInfo timezone, int? participantCap) => new(Parse("Input.SignupOpensLocal", Input.SignupOpensLocal, timezone), Parse("Input.SignupClosesLocal", Input.SignupClosesLocal, timezone), Parse("Input.DraftLocal", Input.DraftLocal, timezone), Parse("Input.EventStartsLocal", Input.EventStartsLocal, timezone), Parse("Input.EventEndsLocal", Input.EventEndsLocal, timezone), participantCap);
    private DateTimeOffset? Parse(string field, string? text, TimeZoneInfo timezone)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!DateTime.TryParseExact(text, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) { ModelState.AddModelError(field, "Choose a valid local date and time."); return null; }
        if (local.Minute % 5 != 0) { ModelState.AddModelError(field, "Choose a time in five-minute increments."); return null; }
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (timezone.IsInvalidTime(local)) { ModelState.AddModelError(field, "That local time does not exist because the clocks change at that time."); return null; }
        return new DateTimeOffset(local, timezone.GetUtcOffset(local)).ToUniversalTime();
    }
    private async Task<IActionResult> Reload(BingoEvent item, CancellationToken ct, bool preserveInput = true)
    {
        SetDisplay(item);
        if (!preserveInput) Populate(item);
        var mode = item.State == EventState.SignupClosed ? SignupOpeningMode.Reopen : item.State == EventState.Draft ? SignupOpeningMode.ScheduleOpening : SignupOpeningMode.OpenNow;
        Readiness = await readiness.GetSignupReadinessAsync(item.Id, mode, time.GetUtcNow(), ct);
        return Page();
    }
    private void Populate(BingoEvent item)
    {
        SetDisplay(item);
        if (!TryTimezone(item.Timezone, out var timezone)) return;
        Input = new InputModel { SignupOpensLocal = FormValue(item.SignupOpensAt, timezone), SignupClosesLocal = FormValue(item.SignupClosesAt, timezone), DraftLocal = FormValue(item.DraftAt, timezone), EventStartsLocal = FormValue(item.EventStartsAt, timezone), EventEndsLocal = FormValue(item.EventEndsAt, timezone), ParticipantCap = item.ParticipantCap, Version = item.Version };
    }
    private void SetDisplay(BingoEvent item)
    {
        EventId = item.Id;
        EventName = item.Name;
        EventTimezone = item.Timezone;
        EventState = item.State;
        if (!TryTimezone(item.Timezone, out var timezone)) return;
        ActualSignupOpened = Display(item.ActualSignupOpenedAt, timezone);
        ActualSignupClosed = Display(item.ActualSignupClosedAt, timezone);
        ScheduledSignupOpening = Display(item.SignupOpensAt, timezone);
        ScheduledSignupOpeningEnabled = item.ScheduledSignupOpeningEnabled;
    }
    private static bool Changed(BingoEvent item, EventScheduleValues values) => item.SignupOpensAt != values.SignupOpensAt || item.SignupClosesAt != values.SignupClosesAt || item.DraftAt != values.DraftAt || item.EventStartsAt != values.EventStartsAt || item.EventEndsAt != values.EventEndsAt || item.ParticipantCap != values.ParticipantCap;
    private static EventScheduleValues ApplyLifecycleScheduleRules(BingoEvent item, EventScheduleValues values)
        => item.State is EventState.SignupOpen or EventState.SignupClosed
            ? values with { SignupOpensAt = item.SignupOpensAt }
            : values;
    private static TimePreview[] Preview(BingoEvent item, EventScheduleValues values, TimeZoneInfo timezone) => [new TimePreview("Signup opens", Display(item.SignupOpensAt, timezone), Display(values.SignupOpensAt, timezone)), new TimePreview("Signup closes", Display(item.SignupClosesAt, timezone), Display(values.SignupClosesAt, timezone)), new TimePreview("Draft time", Display(item.DraftAt, timezone), Display(values.DraftAt, timezone)), new TimePreview("Event starts", Display(item.EventStartsAt, timezone), Display(values.EventStartsAt, timezone)), new TimePreview("Event ends", Display(item.EventEndsAt, timezone), Display(values.EventEndsAt, timezone))];
    private static string? FormValue(DateTimeOffset? value, TimeZoneInfo timezone) => value is null ? null : TimeZoneInfo.ConvertTime(value.Value, timezone).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
    private static string Display(DateTimeOffset? value, TimeZoneInfo timezone) => value is null ? "Not set" : TimeZoneInfo.ConvertTime(value.Value, timezone).ToString("dd MMM yyyy, HH:mm", CultureInfo.CurrentCulture);
    private static bool TryTimezone(string id, out TimeZoneInfo timezone) { try { timezone = TimeZoneInfo.FindSystemTimeZoneById(id); return true; } catch (TimeZoneNotFoundException) { timezone = null!; return false; } catch (InvalidTimeZoneException) { timezone = null!; return false; } }
    public sealed record TimePreview(string Label, string Current, string New);
    public sealed class InputModel
    {
        public string? SignupOpensLocal { get; set; }
        public string? SignupClosesLocal { get; set; }
        public string? DraftLocal { get; set; }
        public string? EventStartsLocal { get; set; }
        public string? EventEndsLocal { get; set; }
        [Range(1, 10000)] public int? ParticipantCap { get; set; }
        public bool AcknowledgeScheduledWarnings { get; set; }
        public bool ConfirmPublicScheduleChange { get; set; }
        [StringLength(2000)] public string? Reason { get; set; }
        public long Version { get; set; }
    }
}
