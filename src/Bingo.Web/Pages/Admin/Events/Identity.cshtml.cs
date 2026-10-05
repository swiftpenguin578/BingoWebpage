using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bingo.Application.Access;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.Navigation;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
[AdminDesign]
public sealed class IdentityModel(
    ApplicationDbContext db,
    TimeProvider time,
    IStringLocalizer<SharedResource>? text = null,
    ILogger<IdentityModel>? logger = null,
    SharedShellService? shell = null) : PageModel
{
    private static readonly Action<ILogger, string, Exception?> LogIdentityFailure =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(630102), "Event identity update failed. Diagnostic reference {DiagnosticReference}.");
    private static readonly IReadOnlyList<TimezoneOption> Defaults =
    [
        new("Europe/Copenhagen", "Copenhagen (Europe/Copenhagen)"),
        new("UTC", "UTC")
    ];

    [BindProperty] public InputModel Input { get; set; } = new();

    public string EventName { get; private set; } = string.Empty;
    public string EventSlug { get; private set; } = string.Empty;
    public Guid EventId { get; private set; }
    public EventState EventState { get; private set; }
    public bool ShowPublicBoard { get; private set; }
    public bool IsSlugLocked { get; private set; } = true;
    public bool CanEditIdentity { get; private set; }
    public bool CanEditTimezone { get; private set; }
    public bool HasUnresolvableTimezone { get; private set; }
    public string StoredTimezoneId { get; private set; } = string.Empty;
    public IReadOnlyList<TimezoneOption> Timezones => Options(Input.Timezone);
    public IReadOnlyList<EventIdentityField> FieldConflicts { get; private set; } = [];
    public bool TimezoneScheduleStale { get; private set; }
    public bool SaveOutcomeUncertain { get; private set; }
    public long CurrentVersion { get; private set; }
    public IReadOnlyList<TimePreview> TimezonePreview { get; private set; } = [];
    public bool HasPublicExposure { get; private set; }
    public IReadOnlyDictionary<string, List<TimePreview>> TimezonePreviews { get; private set; } = new Dictionary<string, List<TimePreview>>();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        Populate(item);
        return Page();
    }

    // Same Admin/visibility/lifecycle read boundary as the rendered Identity page.
    // This observes current values only; it cannot identify which request wrote them.
    public async Task<IActionResult> OnGetCurrentAsync(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        return new JsonResult(new { eventId = item.Id, values = Values(item).Canonical(), version = item.Version });
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        var snapshot = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (snapshot is null) return NotFound();

        if (await HasRetiredBannerMutationAsync(ct))
        {
            ModelState.AddModelError(string.Empty, Localize("AdminDesign.Event banners are no longer supported. Reload the page and submit only identity fields."));
            Populate(snapshot, preserveInput: true);
            return Page();
        }

        if (!HasCompleteBaselineTransport())
            ModelState.AddModelError(string.Empty, Localize("The identity comparison baseline is incomplete. Reload the page before trying again."));
        var proposed = Prepare(snapshot);
        if (proposed is null)
        {
            Populate(snapshot, preserveInput: true);
            return Page();
        }

        // Capture the shell before attempting the write: an uncertain response must
        // render even when the connection (including rollback) is no longer usable.
        if (shell is not null)
            ViewData["AdminDesignShell"] = await shell.GetAdminDesignAsync(User, RouteData.Values, ct);
        var actor = User.GetAccountId()!.Value;
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            db.ChangeTracker.Clear();
            var item = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return NotFound();
            proposed = Prepare(item);
            if (proposed is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                Populate(item, preserveInput: true);
                db.ChangeTracker.Clear();
                return Page();
            }
            if (Values(item) == proposed)
            {
                await transaction.CommitAsync(ct);
                TempData["StatusMessage"] = Localize("No identity changes were made.");
                TempData[UiMessage.TypeKey] = UiMessageType.Information.ToString();
                return RedirectToPage("Identity", new { id });
            }

            var before = AuditState(item);
            item.UpdateIdentity(proposed.Name, item.Slug, proposed.Description, proposed.BuyInDescription, proposed.Timezone);
            var after = AuditState(item);
            db.AuditEntries.Add(new AuditEntry(
                Guid.NewGuid(), now, actor, User.Identity?.Name ?? "Admin", "event.identity_updated",
                "event", item.Id.ToString(), null, item.Id,
                JsonSerializer.Serialize(before), JsonSerializer.Serialize(after)));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            return await StalePageAsync(id, ct);
        }
        catch (Exception exception) when (IsTransientProviderConflict(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            return await StalePageAsync(id, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (Exception rollbackException) when (rollbackException is not OperationCanceledException)
            {
                // The original outcome is still unknown; rollback failure must not
                // destroy the posted draft or require another database operation.
            }
            db.ChangeTracker.Clear();
            var reference = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            if (logger is not null) LogIdentityFailure(logger, reference, exception);
            SaveOutcomeUncertain = true;
            ModelState.AddModelError(string.Empty, Localize("The save outcome is uncertain. Check the current values before trying another save. Diagnostic reference: {0}.", reference));
            Populate(snapshot, preserveInput: true);
            return Page();
        }

        TempData["StatusMessage"] = Localize("Event identity updated.");
        TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
        return RedirectToPage("Identity", new { id });
    }

    private EventIdentityValues? Prepare(BingoEvent item)
    {
        FieldConflicts = [];
        TimezoneScheduleStale = false;
        if (!CanEditIdentityState(item))
            ModelState.AddModelError(string.Empty, Localize("AdminDesign.Event identity cannot be changed in its current state."));
        if (!Input.HasBaseline && Input.Version != item.Version) AddStaleError();
        if (Input.HasBaseline && (string.IsNullOrWhiteSpace(Input.OriginalName) || string.IsNullOrWhiteSpace(Input.OriginalTimezone)))
            ModelState.AddModelError(string.Empty, Localize("The identity comparison baseline is incomplete. Reload the page before trying again."));
        var choices = new[] { Input.NameResolution, Input.DescriptionResolution, Input.BuyInDescriptionResolution, Input.TimezoneResolution };
        if (choices.Any(choice => !Enum.IsDefined(choice))
            || (choices.Any(choice => choice != EventIdentityResolution.None) && !Input.HasReviewedValues)
            || (Input.HasReviewedValues && (string.IsNullOrWhiteSpace(Input.ReviewedName) || string.IsNullOrWhiteSpace(Input.ReviewedTimezone))))
            ModelState.AddModelError(string.Empty, Localize("The identity conflict resolution is incomplete. Review the latest values and try again."));
        var proposed = new EventIdentityValues(Input.Name ?? string.Empty, Input.Description, Input.BuyInDescription, Input.Timezone ?? string.Empty).Canonical();
        if (Input.HasBaseline && ModelState.IsValid)
        {
            var comparison = EventIdentityComparison.Compare(
                new(Input.OriginalName!, Input.OriginalDescription, Input.OriginalBuyInDescription, Input.OriginalTimezone!), proposed, Values(item),
                new(Input.NameResolution, Input.DescriptionResolution, Input.BuyInDescriptionResolution, Input.TimezoneResolution,
                    Input.HasReviewedValues ? new(Input.ReviewedName!, Input.ReviewedDescription, Input.ReviewedBuyInDescription, Input.ReviewedTimezone!) : null));
            proposed = comparison.Values;
            FieldConflicts = comparison.Conflicts;
            foreach (var field in FieldConflicts)
                ModelState.AddModelError($"Input.{field}", Localize("Another administrator changed this field. Resolve it against the latest value before saving."));
        }
        if (string.IsNullOrWhiteSpace(proposed.Name))
            ModelState.AddModelError("Input.Name", Localize("AdminDesign.Enter an event name."));
        else if (!string.Equals(item.Name, proposed.Name, StringComparison.Ordinal)
                 && WiseOldManCompetitionRules.ProviderCharacterCount(proposed.Name) > WiseOldManCompetitionRules.MaximumCompetitionTitleLength)
            ModelState.AddModelError("Input.Name", Localize("Event names must be 50 characters or fewer."));
        if (proposed.Description is { Length: > 4_000 })
            ModelState.AddModelError("Input.Description", Localize("Description must be 4000 characters or fewer."));
        if (proposed.BuyInDescription is { Length: > 2_000 })
            ModelState.AddModelError("Input.BuyInDescription", Localize("Buy-in information must be 2000 characters or fewer."));
        if (!string.IsNullOrWhiteSpace(Input.Slug) && !string.Equals(Input.Slug.Trim(), item.Slug, StringComparison.Ordinal))
            AddPermanentSlugError();
        var timezoneChanged = !string.Equals(item.Timezone, proposed.Timezone, StringComparison.Ordinal);
        var supportedTimezone = TrySupportedTimezone(proposed.Timezone, out _);
        if (!supportedTimezone && timezoneChanged)
            ModelState.AddModelError("Input.Timezone", Localize("Choose a supported timezone."));
        if (timezoneChanged && item.ActualStartedAt is not null)
            ModelState.AddModelError("Input.Timezone", Localize("AdminDesign.The timezone cannot change after the event first goes Live."));
        if (timezoneChanged && supportedTimezone && item.FirstPublicAt is not null)
        {
            TimezoneScheduleStale = !string.IsNullOrEmpty(Input.TimezoneConfirmationSchedule)
                && !string.Equals(Input.TimezoneConfirmationSchedule, ScheduleFingerprint(item), StringComparison.Ordinal);
            if (TimezoneScheduleStale)
                ModelState.AddModelError(string.Empty, Localize("The schedule changed after this timezone review. Review the current dates and confirm again."));
            if (TimezoneScheduleStale || !Input.ConfirmTimezoneChange
                || !string.Equals(Input.TimezoneConfirmationOriginal, item.Timezone, StringComparison.Ordinal)
                || !string.Equals(Input.TimezoneConfirmationProposed, proposed.Timezone, StringComparison.Ordinal)
                || !string.Equals(Input.TimezoneConfirmationSchedule, ScheduleFingerprint(item), StringComparison.Ordinal))
                ModelState.AddModelError("Input.ConfirmTimezoneChange", Localize("Review the participant-facing time preview and confirm this timezone change."));
            if (!ModelState.IsValid) TimezonePreview = Preview(item, item.Timezone, proposed.Timezone);
        }
        return ModelState.IsValid ? proposed : null;
    }

    private bool HasCompleteBaselineTransport()
    {
        if (!Request.HasFormContentType) return true;
        var form = Request.Form;
        var originals = new[] { "OriginalName", "OriginalDescription", "OriginalBuyInDescription", "OriginalTimezone", "Version" };
        var reviewed = new[] { "ReviewedName", "ReviewedDescription", "ReviewedBuyInDescription", "ReviewedTimezone" };
        return (!Input.HasBaseline || originals.All(field => form.ContainsKey($"Input.{field}")))
            && (!Input.HasReviewedValues || reviewed.All(field => form.ContainsKey($"Input.{field}")));
    }

    private static EventIdentityValues Values(BingoEvent item) => new(item.Name, item.Description, item.BuyInDescription, item.Timezone);

    private static string ScheduleFingerprint(BingoEvent item)
    {
        DateTimeOffset?[] values = [item.FirstPublicAt, item.SignupOpensAt, item.SignupClosesAt, item.DraftAt,
            item.EventStartsAt, item.EventEndsAt, item.ActualSignupOpenedAt, item.ActualSignupClosedAt,
            item.ActualStartedAt, item.ActualEndedAt, item.SubmissionCutoffAt, item.SubmissionsClosedAt, item.CancelledAt];
        // PostgreSQL persists microseconds. Never bind review to unpersistable 100ns ticks.
        var payload = string.Join("|", values.Select(value => value is null ? "-" : (value.Value.UtcTicks / 10).ToString(CultureInfo.InvariantCulture)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    public string EventDate(DateTimeOffset value)
    {
        var formatted = DateTimePresentation.Format(value, "d MMM yyyy, HH:mm", DisplayTimezone, CultureInfo.CurrentCulture);
        return HasUnresolvableTimezone ? $"{formatted} — UTC fallback" : formatted;
    }

    private async Task<IActionResult> StalePageAsync(Guid id, CancellationToken ct)
    {
        var latest = await db.Events.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        Prepare(latest);
        AddStaleError();
        Populate(latest, preserveInput: true);
        return Page();
    }

    private async Task<bool> HasRetiredBannerMutationAsync(CancellationToken ct)
    {
        if (!Request.HasFormContentType) return false;
        var form = await Request.ReadFormAsync(ct);
        return form.Keys.Any(key => key.StartsWith("Input.Banner", StringComparison.Ordinal)
            || key.StartsWith("Input.RemoveBanner", StringComparison.Ordinal));
    }

    private static bool CanEditIdentityState(BingoEvent item) =>
        !item.IsHidden && EventStatePolicy.Allows(item.State, EventCapability.ConfigureIdentity);

    private void AddStaleError() =>
        ModelState.AddModelError(string.Empty, Localize("This event changed while you were editing it. Review the latest values and try again."));

    private void Populate(BingoEvent item, bool preserveInput = false)
    {
        CurrentVersion = item.Version;
        EventId = item.Id;
        EventName = item.Name;
        EventSlug = item.Slug;
        EventState = item.State;
        ShowPublicBoard = item.FirstPublicAt is not null && item.BoardPublished;
        CanEditIdentity = CanEditIdentityState(item);
        CanEditTimezone = CanEditIdentity && item.ActualStartedAt is null;
        StoredTimezoneId = item.Timezone;
        HasUnresolvableTimezone = !TryFind(item.Timezone, out _);
        IsSlugLocked = true;
        DisplayTimezone = item.Timezone;
        HasPublicExposure = item.FirstPublicAt is not null;
        TimezonePreviews = Defaults.ToDictionary(option => option.Id, option => Preview(item, item.Timezone, option.Id));
        if (!preserveInput)
        {
            Input = new InputModel
            {
                Name = item.Name,
                Slug = item.Slug,
                Description = item.Description,
                BuyInDescription = item.BuyInDescription,
                Timezone = item.Timezone,
                Version = item.Version,
                HasBaseline = true,
                OriginalName = item.Name,
                OriginalDescription = item.Description,
                OriginalBuyInDescription = item.BuyInDescription,
                OriginalTimezone = item.Timezone,
                TimezoneConfirmationSchedule = ScheduleFingerprint(item),
                TimezoneConfirmationOriginal = item.Timezone
            };
        }
        else
        {
            Input.Slug = item.Slug;
            Input.HasReviewedValues = true;
            Input.ReviewedName = item.Name;
            Input.ReviewedDescription = item.Description;
            Input.ReviewedBuyInDescription = item.BuyInDescription;
            Input.ReviewedTimezone = item.Timezone;
            Input.NameResolution = EventIdentityResolution.None;
            Input.DescriptionResolution = EventIdentityResolution.None;
            Input.BuyInDescriptionResolution = EventIdentityResolution.None;
            Input.TimezoneResolution = EventIdentityResolution.None;
            Input.TimezoneConfirmationSchedule = ScheduleFingerprint(item);
            Input.TimezoneConfirmationOriginal = item.Timezone;
            Input.TimezoneConfirmationProposed = Input.Timezone;
            Input.ConfirmTimezoneChange = false;
            SetAuthoritativeModelState("Input.HasReviewedValues", "true");
            SetAuthoritativeModelState("Input.ReviewedName", Input.ReviewedName);
            SetAuthoritativeModelState("Input.ReviewedDescription", Input.ReviewedDescription);
            SetAuthoritativeModelState("Input.ReviewedBuyInDescription", Input.ReviewedBuyInDescription);
            SetAuthoritativeModelState("Input.ReviewedTimezone", Input.ReviewedTimezone);
            foreach (var field in Enum.GetValues<EventIdentityField>())
                SetAuthoritativeModelState($"Input.{field}Resolution", EventIdentityResolution.None.ToString());
            SetAuthoritativeModelState("Input.TimezoneConfirmationSchedule", Input.TimezoneConfirmationSchedule);
            SetAuthoritativeModelState("Input.ConfirmTimezoneChange", "false");
            SetAuthoritativeModelState("Input.TimezoneConfirmationOriginal", Input.TimezoneConfirmationOriginal);
            SetAuthoritativeModelState("Input.TimezoneConfirmationProposed", Input.TimezoneConfirmationProposed);
        }
    }

    private static object AuditState(BingoEvent item)
    {
        var fixedLength = JsonSerializer.Serialize(new { item.Name, Description = (string?)null,
            BuyInDescription = (string?)null, item.Timezone, item.Slug }).Length;
        // Audit JSON is varchar(4000). Budget escaped excerpts, retaining full identity keys.
        var excerptBudget = (4_000 - fixedLength) / 2 + 4;
        return new { item.Name, Description = AuditDescription(item.Description, excerptBudget),
            BuyInDescription = AuditDescription(item.BuyInDescription, excerptBudget), item.Timezone, item.Slug };
    }

    private static string? AuditDescription(string? description, int jsonBudget)
    {
        if (description is null || (description.Length <= 500 && JsonSerializer.Serialize(description).Length <= jsonBudget))
            return description;
        var prefix = new StringBuilder();
        var used = JsonSerializer.Serialize("…").Length;
        foreach (var rune in description.EnumerateRunes())
        {
            var value = rune.ToString();
            var encodedLength = JsonEncodedText.Encode(value).EncodedUtf8Bytes.Length;
            if (prefix.Length + value.Length > 500 || used + encodedLength > jsonBudget) break;
            prefix.Append(value);
            used += encodedLength;
        }
        return prefix + "…";
    }

    private List<TimePreview> Preview(BingoEvent item, string oldId, string newId)
    {
        (string Label, DateTimeOffset? At)[] rows =
        [
            ("Signups open", item.SignupOpensAt), ("Signups close", item.SignupClosesAt),
            ("Team draft", item.DraftAt), ("AdminDesign.Event starts", item.EventStartsAt), ("AdminDesign.Event ends", item.EventEndsAt)
        ];
        return rows.Select(row => new TimePreview(row.Label, Format(row.At, oldId), Format(row.At, newId), row.At.HasValue, Offset(row.At, oldId), Offset(row.At, newId))).ToList();
    }

    private string Format(DateTimeOffset? value, string timezone) => value is null
        ? Localize("Not scheduled yet")
        : DateTimePresentation.Format(value.Value, "d MMM yyyy, HH:mm", timezone, CultureInfo.CurrentCulture);

    private static string Offset(DateTimeOffset? value, string timezone) => value is null ? string.Empty : "UTC" + DateTimePresentation.Format(value.Value, "zzz", timezone, CultureInfo.InvariantCulture);

    private void AddPermanentSlugError()
    {
        var message = Localize("AdminDesign.The public event link is permanent and cannot be changed.");
        ModelState.AddModelError("Input.Slug", message);
        ModelState.AddModelError(string.Empty, message);
    }

    private void SetAuthoritativeModelState(string key, string? value) =>
        ModelState.SetModelValue(key, new Microsoft.AspNetCore.Mvc.ModelBinding.ValueProviderResult(value ?? string.Empty, CultureInfo.InvariantCulture));

    private static bool IsTransientProviderConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is not PostgresException provider)
                continue;
            return provider.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected;
        }

        return false;
    }

    private List<TimezoneOption> Options(string? selected)
    {
        var options = Defaults.Select(option => new TimezoneOption(option.Id, Label(option.Id))).ToList();
        if (!string.IsNullOrWhiteSpace(selected) && options.All(option => option.Id != selected))
            options.Add(new TimezoneOption(selected, Localize("{0} · current", selected)));
        return options;
    }

    private static bool TrySupportedTimezone(string? timezoneId, out TimeZoneInfo timezone)
    {
        timezone = null!;
        return !string.IsNullOrWhiteSpace(timezoneId)
            && Defaults.Any(option => option.Id == timezoneId.Trim())
            && TryFind(timezoneId.Trim(), out timezone);
    }

    private static bool TryFind(string timezoneId, out TimeZoneInfo timezone)
    {
        try
        {
            timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timezone = null!;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timezone = null!;
            return false;
        }
    }

    private static string Label(string timezoneId)
    {
        if (!TryFind(timezoneId, out var timezone)) return timezoneId;
        var offset = timezone.GetUtcOffset(DateTimeOffset.UtcNow);
        return $"{timezoneId} (UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset.Duration():hh\\:mm})";
    }

    private string Localize(string key, params object[] arguments) =>
        text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);

    private string DisplayTimezone { get; set; } = DateTimePresentation.DefaultTimezoneId;

    public sealed record TimezoneOption(string Id, string Label);
    public sealed record TimePreview(string Label, string Current, string New, bool Scheduled, string CurrentOffset, string NewOffset);

    public sealed class InputModel
    {
        [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
        // Slug is displayed for compatibility with older form submissions but is
        // never editable or generated by this handler.
        [StringLength(120)] public string Slug { get; set; } = string.Empty;
        [StringLength(4_000)] public string? Description { get; set; }
        [StringLength(2_000)] public string? BuyInDescription { get; set; }
        [Required, StringLength(100)] public string Timezone { get; set; } = "Europe/Copenhagen";
        public bool ConfirmTimezoneChange { get; set; }
        public string? TimezoneConfirmationOriginal { get; set; }
        public string? TimezoneConfirmationProposed { get; set; }
        public long Version { get; set; }
        public bool HasBaseline { get; set; }
        public string? OriginalName { get; set; }
        public string? OriginalDescription { get; set; }
        public string? OriginalBuyInDescription { get; set; }
        public string? OriginalTimezone { get; set; }
        public bool HasReviewedValues { get; set; }
        public string? ReviewedName { get; set; }
        public string? ReviewedDescription { get; set; }
        public string? ReviewedBuyInDescription { get; set; }
        public string? ReviewedTimezone { get; set; }
        public EventIdentityResolution NameResolution { get; set; }
        public EventIdentityResolution DescriptionResolution { get; set; }
        public EventIdentityResolution BuyInDescriptionResolution { get; set; }
        public EventIdentityResolution TimezoneResolution { get; set; }
        public string? TimezoneConfirmationSchedule { get; set; }
    }
}
