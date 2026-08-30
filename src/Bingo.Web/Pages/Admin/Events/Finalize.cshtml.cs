using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Events;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Admin.Events;

public sealed class FinalizeModel(IEventFinalizationService finalization, ApplicationDbContext db, Bingo.Application.Auditing.IAuditWriter? audit = null, IStringLocalizer<SharedResource>? text = null) : PageModel
{
    public FinalReviewReadiness Readiness { get; private set; } = null!;
    public string EventTimezone { get; private set; } = DateTimePresentation.DefaultTimezoneId;
    [BindProperty, StringLength(2000)] public string? Reason { get; set; }
    [BindProperty] public bool ConfirmLifecycleAction { get; set; }
    [BindProperty] public string? FinalizeConfirmation { get; set; }
    [BindProperty] public long? ExpectedVersion { get; set; }
    [BindProperty] public Guid? ExpectedReviewCycleId { get; set; }
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) { _ = audit; var value = await finalization.GetReadinessAsync(id, ct); if (value is null) return NotFound(); Readiness = value; EventTimezone = await db.Events.AsNoTracking().Where(x => x.Id == id).Select(x => x.Timezone).SingleAsync(ct); return Page(); }
    public async Task<IActionResult> OnPostResolveAsync(Guid id, string blockerKey, bool confirmOverride, CancellationToken ct) => await Run(id, async () => await finalization.ResolveBlockerAsync(id, blockerKey, Reason ?? string.Empty, confirmOverride, AdminId, ExpectedVersion, ExpectedReviewCycleId, ct), ct);
    public async Task<IActionResult> OnPostAcknowledgeCompletionAsync(Guid id, Guid teamId, CancellationToken ct) => await Run(id, async () => await finalization.AcknowledgeCompletionTimeAsync(id, teamId, AdminId, ExpectedVersion, ExpectedReviewCycleId, ct), ct);
    public async Task<IActionResult> OnPostCorrectCompletionAsync(Guid id, Guid teamId, string? correctedAtLocal, CancellationToken ct) => await Run(id, async () =>
    {
        if (!DateTime.TryParseExact(correctedAtLocal, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var entered))
            throw new InvalidOperationException(Localize("Choose a valid completion date and time."));
        var timezoneId = await db.Events.AsNoTracking().Where(x => x.Id == id).Select(x => x.Timezone).SingleAsync(ct);
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        var local = DateTime.SpecifyKind(entered, DateTimeKind.Unspecified);
        if (timezone.IsInvalidTime(local)) throw new InvalidOperationException(Localize("That local time does not exist because the clocks change at that time."));
        if (timezone.IsAmbiguousTime(local)) throw new InvalidOperationException(Localize("That local time is ambiguous because the clocks change at that time. Choose another time."));
        var correctedAt = new DateTimeOffset(local, timezone.GetUtcOffset(local)).ToUniversalTime();
        await finalization.CorrectCompletionAsync(id, teamId, correctedAt, Reason ?? string.Empty, AdminId, ExpectedVersion, ExpectedReviewCycleId, ct);
        TempData["StatusMessage"] = Localize("Completion time corrected to {0}.", DateTimePresentation.Format(correctedAt, "dd MMM yyyy, HH:mm", timezoneId, CultureInfo.CurrentCulture));
    }, ct);
    public async Task<IActionResult> OnPostFinalizeAsync(Guid id, CancellationToken ct) => await Run(id, async () =>
    {
        if (!string.Equals(FinalizeConfirmation, "PUBLISH_OFFICIAL_RESULTS", StringComparison.Ordinal))
            throw new InvalidOperationException(Localize("Confirm that these placements should be published as the official results."));
        await finalization.FinalizeAsync(id, Actor, ExpectedVersion > 0 ? ExpectedVersion : null, ct);
    }, ct);
    public async Task<IActionResult> OnPostUnfinalizeAsync(Guid id, CancellationToken ct) => await Run(id, async () => await finalization.UnfinalizeAsync(id, Reason ?? string.Empty, ConfirmLifecycleAction, Actor, ExpectedVersion > 0 ? ExpectedVersion : null, ct), ct);
    public async Task<IActionResult> OnPostArchiveAsync(Guid id, CancellationToken ct) => await Run(id, async () => await finalization.ArchiveAsync(id, ConfirmLifecycleAction, Actor, ct), ct);
    private async Task<IActionResult> Run(Guid id, Func<Task> action, CancellationToken ct) { try { await action(); } catch (InvalidOperationException ex) { TempData["StatusMessage"] = ex.Message; } return RedirectToPage(new { id }); }
    private string Localize(string key, params object[] arguments) => text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);
    private Guid AdminId => User.GetAccountId()!.Value;
    private LifecycleActor Actor => new(AdminId, User.Identity!.Name!);
}
