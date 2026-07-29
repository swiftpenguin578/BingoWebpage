using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using Bingo.Application.Access;
using Bingo.Application.Evidence;
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

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class IdentityModel(ApplicationDbContext db, IEvidenceStorage storage, TimeProvider time) : PageModel
{
    private static readonly IReadOnlyList<TimezoneOption> Defaults = [new("Europe/Copenhagen", "Copenhagen (Europe/Copenhagen)"), new("UTC", "UTC"), new("Europe/London", "London"), new("America/New_York", "New York"), new("America/Chicago", "Chicago"), new("America/Los_Angeles", "Los Angeles"), new("Australia/Sydney", "Sydney")];
    [BindProperty] public InputModel Input { get; set; } = new();
    public string EventName { get; private set; } = string.Empty;
    public Guid EventId { get; private set; }
    public bool HasBanner { get; private set; }
    public bool IsSlugLocked { get; private set; }
    public bool RequiresTimezoneReason { get; private set; }
    public IReadOnlyList<TimezoneOption> Timezones => Options(Input.Timezone);
    public IReadOnlyList<TimePreview> TimezonePreview { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        Populate(item);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return NotFound();
        EventName = item.Name;
        RequiresTimezoneReason = item.ActualStartedAt is not null;
        if (Input.Version != item.Version) { ModelState.AddModelError(string.Empty, "This event changed while you were editing it. Review the latest values and try again."); Populate(item, preserveInput: true); return Page(); }
        var validTimezone = TryTimezone(Input.Timezone, out _);
        if (!validTimezone) ModelState.AddModelError("Input.Timezone", "Choose a supported timezone.");
        var slug = EventSlugGenerator.Generate(string.IsNullOrWhiteSpace(Input.Slug) ? Input.Name : Input.Slug);
        if (!string.Equals(slug, Input.Slug?.Trim(), StringComparison.Ordinal)) ModelState.AddModelError("Input.Slug", "Use lowercase letters, numbers, and hyphens for the event link.");
        if (item.FirstPublicAt is not null && !string.Equals(slug, item.Slug, StringComparison.Ordinal)) ModelState.AddModelError("Input.Slug", "The public event link is locked after first publication.");
        if (await db.Events.AnyAsync(x => x.Id != id && x.Slug == slug, ct)) ModelState.AddModelError("Input.Slug", "That event link is already in use.");
        var timezoneChanged = !string.Equals(item.Timezone, Input.Timezone, StringComparison.Ordinal);
        if (validTimezone && timezoneChanged && item.FirstPublicAt is not null && !Input.ConfirmTimezoneChange)
        {
            ModelState.AddModelError("Input.ConfirmTimezoneChange", "Review the participant-facing time preview and confirm this timezone change.");
            TimezonePreview = Preview(item, item.Timezone, Input.Timezone);
        }
        if (timezoneChanged && item.ActualStartedAt is not null && string.IsNullOrWhiteSpace(Input.TimezoneReason)) ModelState.AddModelError("Input.TimezoneReason", "Enter a reason for changing timezone after event start.");
        if (!ModelState.IsValid) { Populate(item, preserveInput: true); return Page(); }

        var actor = User.GetAccountId()!.Value;
        var now = time.GetUtcNow();
        StoredEvidence? uploaded = null;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var before = AuditState(item);
            if (Input.RemoveBanner) { if (item.BannerAssetId is { } oldId) (await db.EventBannerAssets.SingleOrDefaultAsync(x => x.Id == oldId, ct))?.Replace(now); item.SetBannerAsset(null); }
            if (Input.Banner is { Length: > 0 })
            {
                var assetId = Guid.NewGuid();
                await using var content = Input.Banner.OpenReadStream();
                uploaded = await storage.StoreAsync(item.Id, assetId, Input.Banner.FileName, content, ct);
                if (item.BannerAssetId is { } oldId) (await db.EventBannerAssets.SingleOrDefaultAsync(x => x.Id == oldId, ct))?.Replace(now);
                db.EventBannerAssets.Add(new EventBannerAsset(assetId, item.Id, uploaded.StorageKey, uploaded.OriginalFilename, uploaded.MediaType, uploaded.ByteSize, uploaded.Width, uploaded.Height, uploaded.Checksum, actor, now));
                item.SetBannerAsset(assetId);
            }
            item.UpdateIdentity(Input.Name, slug, Input.Description, Input.Timezone);
            var after = AuditState(item);
            db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actor, User.Identity!.Name!, "event.identity_updated", "event", item.Id.ToString(), JsonSerializer.Serialize(new { timezoneReason = string.IsNullOrWhiteSpace(Input.TimezoneReason) ? null : Input.TimezoneReason.Trim() }), item.Id, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after)));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct); await DeleteUploadedAsync(uploaded, ct); db.ChangeTracker.Clear(); ModelState.AddModelError(string.Empty, "This event changed while you were editing it. Review the latest values and try again."); var latest = await db.Events.AsNoTracking().SingleAsync(x => x.Id == id, ct); Populate(latest, true); return Page();
        }
        catch (DbUpdateException ex) when (IsSlugCollision(ex))
        {
            await transaction.RollbackAsync(ct);
            await DeleteUploadedAsync(uploaded, ct);
            db.ChangeTracker.Clear();
            ModelState.AddModelError("Input.Slug", "That event link is already in use.");
            var latest = await db.Events.AsNoTracking().SingleAsync(x => x.Id == id, ct);
            Populate(latest, true);
            return Page();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct); await DeleteUploadedAsync(uploaded, ct); db.ChangeTracker.Clear(); ModelState.AddModelError(string.Empty, "The identity update could not be saved. Try again."); Populate(item, true); return Page();
        }
        TempData["StatusMessage"] = "Event identity updated."; TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
        return RedirectToPage("Manage", new { id });
    }

    private void Populate(BingoEvent item, bool preserveInput = false)
    {
        EventId = item.Id;
        EventName = item.Name;
        HasBanner = item.BannerAssetId is not null;
        IsSlugLocked = item.FirstPublicAt is not null;
        RequiresTimezoneReason = item.ActualStartedAt is not null;
        if (!preserveInput) Input = new InputModel { Name = item.Name, Slug = item.Slug, Description = item.Description, Timezone = item.Timezone, Version = item.Version };
    }
    private async Task DeleteUploadedAsync(StoredEvidence? uploaded, CancellationToken ct)
    {
        if (uploaded is null) return;
        try { await storage.DeleteAsync(uploaded.StorageKey, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
    }
    private static List<TimePreview> Preview(BingoEvent item, string oldId, string newId) => Instants(item).Select(value => new TimePreview(value.Label, Format(value.Value, oldId), Format(value.Value, newId))).ToList();
    private static IEnumerable<(string Label, DateTimeOffset? Value)> Instants(BingoEvent item) { yield return ("Signup opens", item.SignupOpensAt); yield return ("Signup closes", item.SignupClosesAt); yield return ("Draft time", item.DraftAt); yield return ("Event starts", item.EventStartsAt); yield return ("Event ends", item.EventEndsAt); }
    private static string Format(DateTimeOffset? value, string timezone) { if (value is null) return "Not set"; return TimeZoneInfo.ConvertTime(value.Value, TimeZoneInfo.FindSystemTimeZoneById(timezone)).ToString("dd MMM yyyy, HH:mm", CultureInfo.CurrentCulture); }
    private static object AuditState(BingoEvent item) => new { item.Name, item.Slug, Description = AuditDescription(item.Description), item.Timezone, item.BannerAssetId };
    private static string? AuditDescription(string? description) => description is null ? null : description.Length <= 500 ? description : $"{description[..500]}…";
    private static bool IsSlugCollision(DbUpdateException exception) => exception.InnerException?.Message.Contains("events_slug", StringComparison.OrdinalIgnoreCase) == true || exception.InnerException?.Message.Contains("slug", StringComparison.OrdinalIgnoreCase) == true;
    private static List<TimezoneOption> Options(string? selected)
    {
        var result = Defaults.Select(option => new TimezoneOption(option.Id, Label(option.Id, option.Label))).ToList();
        if (!string.IsNullOrWhiteSpace(selected) && result.All(x => x.Id != selected) && TryFind(selected, out _)) result.Add(new(selected, Label(selected, selected)));
        return result;
    }
    private static string Label(string timezoneId, string place)
    {
        if (!TryFind(timezoneId, out var timezone)) return place;
        var offset = timezone.GetUtcOffset(DateTimeOffset.UtcNow);
        return $"{place} (UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset.Duration():hh\\:mm})";
    }
    private static bool TryTimezone(string? timezone, out TimeZoneInfo value) { value = null!; return !string.IsNullOrWhiteSpace(timezone) && Options(timezone).Any(x => x.Id == timezone) && TryFind(timezone, out value); }
    private static bool TryFind(string timezone, out TimeZoneInfo value) { try { value = TimeZoneInfo.FindSystemTimeZoneById(timezone); return true; } catch (TimeZoneNotFoundException) { value = null!; return false; } catch (InvalidTimeZoneException) { value = null!; return false; } }
    public sealed record TimezoneOption(string Id, string Label); public sealed record TimePreview(string Label, string Current, string New);
    public sealed class InputModel { [Required, StringLength(200)] public string Name { get; set; } = string.Empty; [Required, StringLength(120)] public string Slug { get; set; } = string.Empty; [StringLength(4000)] public string? Description { get; set; } [Required] public string Timezone { get; set; } = "Europe/Copenhagen"; public IFormFile? Banner { get; set; } public bool RemoveBanner { get; set; } public bool ConfirmTimezoneChange { get; set; } [StringLength(2000)] public string? TimezoneReason { get; set; } public long Version { get; set; } }
}
