using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using Bingo.Application.Access;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
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
public sealed class IdentityModel(
    ApplicationDbContext db,
    TimeProvider time,
    IStringLocalizer<SharedResource>? text = null,
    ILogger<IdentityModel>? logger = null) : PageModel
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
    public IReadOnlyList<TimePreview> TimezonePreview { get; private set; } = [];
    public IReadOnlyList<ManageModel.TimelineRow> EffectiveTimeline { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        Populate(item);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        var snapshot = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (snapshot is null) return NotFound();

        if (await HasRetiredBannerMutationAsync(ct))
        {
            ModelState.AddModelError(string.Empty, Localize("Event banners are no longer supported. Reload the page and submit only identity fields."));
            Populate(snapshot, preserveInput: true);
            return Page();
        }

        if (!CanEditIdentityState(snapshot))
        {
            ModelState.AddModelError(string.Empty, Localize("Event identity cannot be changed in its current state."));
            Populate(snapshot, preserveInput: true);
            return Page();
        }

        if (Input.Version != snapshot.Version)
        {
            AddStaleError();
            Populate(snapshot, preserveInput: true);
            return Page();
        }

        var name = Input.Name?.Trim() ?? string.Empty;
        var description = Clean(Input.Description);
        var buyInDescription = Clean(Input.BuyInDescription);
        var timezone = Input.Timezone?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            ModelState.AddModelError("Input.Name", Localize("Enter an event name."));
        else if (!string.Equals(snapshot.Name, name, StringComparison.Ordinal)
                 && WiseOldManCompetitionRules.ProviderCharacterCount(name) > WiseOldManCompetitionRules.MaximumCompetitionTitleLength)
            ModelState.AddModelError("Input.Name", Localize("Event names must be 50 characters or fewer."));
        if (description is { Length: > 4_000 })
            ModelState.AddModelError("Input.Description", Localize("Description must be 4000 characters or fewer."));
        if (buyInDescription is { Length: > 2_000 })
            ModelState.AddModelError("Input.BuyInDescription", Localize("Buy-in information must be 2000 characters or fewer."));

        var proposedSlug = string.IsNullOrWhiteSpace(Input.Slug) ? snapshot.Slug : Input.Slug.Trim();
        if (!string.Equals(proposedSlug, snapshot.Slug, StringComparison.Ordinal))
            AddPermanentSlugError();

        var timezoneChanged = !string.Equals(snapshot.Timezone, timezone, StringComparison.Ordinal);
        var supportedTimezone = TrySupportedTimezone(timezone, out _);
        if (!supportedTimezone && timezoneChanged)
            ModelState.AddModelError("Input.Timezone", Localize("Choose a supported timezone."));
        if (timezoneChanged && snapshot.ActualStartedAt is not null)
            ModelState.AddModelError("Input.Timezone", Localize("The timezone cannot change after the event first goes Live."));

        var requiresPreview = timezoneChanged && supportedTimezone && snapshot.FirstPublicAt is not null;
        if (requiresPreview && (!Input.ConfirmTimezoneChange
                                 || !string.Equals(Input.TimezoneConfirmationOriginal, snapshot.Timezone, StringComparison.Ordinal)
                                 || !string.Equals(Input.TimezoneConfirmationProposed, timezone, StringComparison.Ordinal)))
        {
            ModelState.AddModelError("Input.ConfirmTimezoneChange", Localize("Review the participant-facing time preview and confirm this timezone change."));
            TimezonePreview = Preview(snapshot, snapshot.Timezone, timezone);
        }

        if (!ModelState.IsValid)
        {
            Populate(snapshot, preserveInput: true);
            if (requiresPreview && TimezonePreview.Count == 0)
                TimezonePreview = Preview(snapshot, snapshot.Timezone, timezone);
            return Page();
        }

        var hasChanges = !string.Equals(snapshot.Name, name, StringComparison.Ordinal)
            || !string.Equals(snapshot.Description, description, StringComparison.Ordinal)
            || !string.Equals(snapshot.BuyInDescription, buyInDescription, StringComparison.Ordinal)
            || timezoneChanged;
        if (!hasChanges)
        {
            TempData["StatusMessage"] = Localize("No identity changes were made.");
            TempData[UiMessage.TypeKey] = UiMessageType.Information.ToString();
            return RedirectToPage("Manage", new { id });
        }

        var actor = User.GetAccountId()!.Value;
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            db.ChangeTracker.Clear();
            var item = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return NotFound();
            if (item.Version != Input.Version || !CanEditIdentityState(item)
                || (timezoneChanged && item.ActualStartedAt is not null)
                || (requiresPreview && (!Input.ConfirmTimezoneChange
                    || !string.Equals(Input.TimezoneConfirmationOriginal, item.Timezone, StringComparison.Ordinal)
                    || !string.Equals(Input.TimezoneConfirmationProposed, timezone, StringComparison.Ordinal))))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                var latest = await db.Events.AsNoTracking().SingleAsync(x => x.Id == id, ct);
                AddStaleError();
                Populate(latest, preserveInput: true);
                return Page();
            }

            var before = AuditState(item);
            item.UpdateIdentity(name, item.Slug, description, buyInDescription, timezone);
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
        catch (DbUpdateException exception) when (IsTransientProviderConflict(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            return await StalePageAsync(id, ct);
        }
        catch (PostgresException exception) when (IsTransientProviderConflict(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            return await StalePageAsync(id, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            var reference = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            if (logger is not null) LogIdentityFailure(logger, reference, exception);
            ModelState.AddModelError(string.Empty, Localize("The identity update could not be saved. Try again. Diagnostic reference: {0}.", reference));
            var latest = await db.Events.AsNoTracking().SingleAsync(x => x.Id == id, ct);
            Populate(latest, preserveInput: true);
            return Page();
        }

        TempData["StatusMessage"] = Localize("Event identity updated.");
        TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
        return RedirectToPage("Manage", new { id });
    }

    public string EventDate(DateTimeOffset value)
    {
        var formatted = DateTimePresentation.Format(value, "dd MMM yyyy, HH:mm (zzz)", DisplayTimezone, CultureInfo.CurrentCulture);
        return HasUnresolvableTimezone ? $"{formatted} — UTC fallback" : formatted;
    }

    private async Task<IActionResult> StalePageAsync(Guid id, CancellationToken ct)
    {
        AddStaleError();
        var latest = await db.Events.AsNoTracking().SingleAsync(x => x.Id == id, ct);
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
                TimezoneConfirmationOriginal = item.Timezone
            };
        }
        else
        {
            Input.Slug = item.Slug;
            Input.Version = item.Version;
            Input.TimezoneConfirmationOriginal = item.Timezone;
            Input.TimezoneConfirmationProposed = Input.Timezone;
            Input.ConfirmTimezoneChange = false;
            SetAuthoritativeModelState("Input.Version", Input.Version.ToString(CultureInfo.InvariantCulture));
            SetAuthoritativeModelState("Input.TimezoneConfirmationOriginal", Input.TimezoneConfirmationOriginal);
            SetAuthoritativeModelState("Input.TimezoneConfirmationProposed", Input.TimezoneConfirmationProposed);
        }
    }

    private static object AuditState(BingoEvent item) => new
    {
        item.Name,
        Description = AuditDescription(item.Description),
        BuyInDescription = AuditDescription(item.BuyInDescription),
        item.Timezone,
        item.Slug
    };

    private static string? AuditDescription(string? description) => description is null
        ? null
        : description.Length <= 500 ? description : $"{description[..500]}…";

    private static List<TimePreview> Preview(BingoEvent item, string oldId, string newId)
    {
        var input = new ManageModel.EffectiveTimelineInput(
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
            item.CancelledAt);
        var rows = ManageModel.EffectiveTimelineFor(input).Select(row => (row.Label, row.At));
        if (item.FirstPublicAt is { } firstPublicAt)
            rows = rows.Prepend(("First public", firstPublicAt));
        return rows.Select(value => new TimePreview(value.Label, Format(value.At, oldId), Format(value.At, newId))).ToList();
    }

    private static string Format(DateTimeOffset? value, string timezone) => value is null
        ? "Not set"
        : DateTimePresentation.Format(value.Value, "dd MMM yyyy, HH:mm (zzz)", timezone, CultureInfo.CurrentCulture);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void AddPermanentSlugError()
    {
        var message = Localize("The public event link is permanent and cannot be changed.");
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

    private static List<TimezoneOption> Options(string? selected)
    {
        var options = Defaults.Select(option => new TimezoneOption(option.Id, Label(option.Id, option.Label))).ToList();
        if (!string.IsNullOrWhiteSpace(selected) && options.All(option => option.Id != selected))
            options.Add(new TimezoneOption(selected, $"Stored timezone ({selected})"));
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

    private static string Label(string timezoneId, string place)
    {
        if (!TryFind(timezoneId, out var timezone)) return place;
        var offset = timezone.GetUtcOffset(DateTimeOffset.UtcNow);
        return $"{place} (UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset.Duration():hh\\:mm})";
    }

    private string Localize(string key, params object[] arguments) =>
        text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);

    private string DisplayTimezone { get; set; } = DateTimePresentation.DefaultTimezoneId;

    public sealed record TimezoneOption(string Id, string Label);
    public sealed record TimePreview(string Label, string Current, string New);

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
    }
}
