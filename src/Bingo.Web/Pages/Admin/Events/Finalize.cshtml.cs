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
    [BindProperty] public string? ExpectedInspectionKey { get; set; }
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) { _ = audit; var value = await finalization.GetReadinessAsync(id, ct); if (value is null) return NotFound(); Readiness = value; EventTimezone = await db.Events.AsNoTracking().Where(x => x.Id == id).Select(x => x.Timezone).SingleAsync(ct); return Page(); }
    // Retained source-compatible methods are intentionally non-actions. Final
    // review has no override, inspection, or manual completion-correction path.
    [NonAction]
    public Task<IActionResult> OnPostResolveAsync(Guid id, string blockerKey, bool confirmOverride, CancellationToken ct) => RetiredReviewAction();
    [NonAction]
    public Task<IActionResult> OnPostAcknowledgeCompletionAsync(Guid id, Guid teamId, CancellationToken ct) => RetiredReviewAction();
    [NonAction]
    public Task<IActionResult> OnPostCorrectCompletionAsync(Guid id, Guid teamId, string? correctedAtLocal, CancellationToken ct) => RetiredReviewAction();
    public async Task<IActionResult> OnPostFinalizeAsync(Guid id, CancellationToken ct) => await Run(id, async () =>
    {
        if (!ConfirmLifecycleAction && !string.Equals(FinalizeConfirmation, "PUBLISH_OFFICIAL_RESULTS", StringComparison.Ordinal))
            throw new InvalidOperationException(Localize("Confirm that these placements should be published as the official results."));
        var outcome = await finalization.FinalizeAsync(id, Actor, ExpectedVersion, ct);
        if (outcome.Feedback is { Length: > 0 } feedback)
        {
            TempData["StatusMessage"] = feedback;
            TempData[UiMessage.TypeKey] = UiMessageType.Warning.ToString();
        }
    }, ct);
    public async Task<IActionResult> OnPostUnfinalizeAsync(Guid id, CancellationToken ct) => await Run(id, async () => await finalization.UnfinalizeAsync(id, Reason ?? string.Empty, ConfirmLifecycleAction, Actor, ExpectedVersion > 0 ? ExpectedVersion : null, ct), ct);
    [NonAction]
    public Task<IActionResult> OnPostArchiveAsync(Guid id, CancellationToken ct) => RetiredReviewAction();
    private async Task<IActionResult> Run(Guid id, Func<Task> action, CancellationToken ct) { try { await action(); } catch (InvalidOperationException ex) { TempData["StatusMessage"] = ex.Message; TempData[UiMessage.TypeKey] = UiMessageType.Error.ToString(); } return RedirectToPage(new { id }); }
    private string Localize(string key, params object[] arguments) => text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);
    private Task<IActionResult> RetiredReviewAction() => Task.FromResult<IActionResult>(BadRequest(Localize("This final-review action is retired. Resolve the underlying records and publish official results.")));
    private Guid AdminId => User.GetAccountId()!.Value;
    private LifecycleActor Actor => new(AdminId, User.Identity!.Name!);
}
