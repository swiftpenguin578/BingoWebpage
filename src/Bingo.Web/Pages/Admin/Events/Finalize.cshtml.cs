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

[AdminDesign]
public sealed partial class FinalizeModel(IEventFinalizationService finalization, ApplicationDbContext db, Bingo.Application.Auditing.IAuditWriter? audit = null, IStringLocalizer<SharedResource>? text = null) : PageModel
{
    public FinalReviewReadiness Readiness { get; private set; } = null!;
    public string EventTimezone { get; private set; } = DateTimePresentation.DefaultTimezoneId;
    [BindProperty, StringLength(IEventFinalizationService.MaximumUnfinalizeReasonLength)] public string? Reason { get; set; }
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
        if (!WantsJson && outcome.Feedback is { Length: > 0 } feedback)
        {
            TempData["StatusMessage"] = feedback;
            TempData[UiMessage.TypeKey] = UiMessageType.Warning.ToString();
        }
    }, ct);
    public async Task<IActionResult> OnPostUnfinalizeAsync(Guid id, CancellationToken ct) => await Run(id, async () =>
    {
        if (Reason is { Length: > IEventFinalizationService.MaximumUnfinalizeReasonLength })
            throw new InvalidOperationException(Localize("The reopening reason must be 2000 characters or fewer."));
        await finalization.UnfinalizeAsync(id, Reason ?? string.Empty, ConfirmLifecycleAction, Actor, ExpectedVersion, ct);
    }, ct);
    [NonAction]
    public Task<IActionResult> OnPostArchiveAsync(Guid id, CancellationToken ct) => RetiredReviewAction();
    public async Task<IActionResult> OnGetCurrentAsync(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var current = await finalization.GetReadinessAsync(id, ct);
        return current is null ? NotFound() : new JsonResult(CurrentState(current));
    }
    private bool WantsJson => Request.GetTypedHeaders().Accept?.Any(value => value.MediaType.Value == "application/json") == true;
    private static object CurrentState(FinalReviewReadiness value) => new
    {
        value.EventId, version = value.EventVersion.ToString(CultureInfo.InvariantCulture), state = value.State.ToString(),
        latestFinalization = (value.History.Count > 0 ? value.History[0] : null), value.History, value.ReviewCycleId,
        finalRefresh = (value.History.Count > 0 ? value.History[0] : null)?.FinalWomRefresh, value.BlockingCurrentEvent,
        value.CanFinalize, value.SubmissionWindowOpen, value.Blockers, value.Placements,
        womEndUpdateStatus = value.WomEndUpdateStatus.ToString()
    };
    private async Task<IActionResult> Run(Guid id, Func<Task> action, CancellationToken ct)
    {
        try
        {
            await action();
            if (WantsJson)
            {
                var current = await finalization.GetReadinessAsync(id, ct);
                return new JsonResult(new { succeeded = true, outcome = "applied", current = current is null ? null : CurrentState(current) });
            }
        }
        catch (InvalidOperationException ex)
        {
            if (WantsJson) return new JsonResult(new { succeeded = false, outcome = "refused", error = Localize(ex.Message) });
            TempData["StatusMessage"] = ex.Message;
            TempData[UiMessage.TypeKey] = UiMessageType.Error.ToString();
        }
        return RedirectToPage(new { id });
    }
    public static string FinalWomRefreshDescription(FinalWomRefreshOutcome? outcome) => outcome switch
    {
        null => "Not recorded",
        { Status: FinalWomRefreshStatus.Succeeded } => "Succeeded",
        { Status: FinalWomRefreshStatus.Failed } => "Failed",
        { SkipReason: EventCompetitionRefreshSkipReason.NoCompetition } => "Skipped: no competition is configured.",
        { SkipReason: EventCompetitionRefreshSkipReason.RefreshInProgress } => "Skipped: a refresh is already in progress.",
        { SkipReason: EventCompetitionRefreshSkipReason.RetryDelay } => "Skipped: the retry delay has not elapsed.",
        { SkipReason: EventCompetitionRefreshSkipReason.NotDue } => "Skipped: the refresh window has not elapsed.",
        { SkipReason: EventCompetitionRefreshSkipReason.IncompleteEventWindow } => "Skipped: the event window is incomplete.",
        { SkipReason: EventCompetitionRefreshSkipReason.ServiceUnavailable } => "Skipped: the refresh service is unavailable.",
        { SkipReason: EventCompetitionRefreshSkipReason.EventUnavailable or EventCompetitionRefreshSkipReason.EventNotInFinalReview } => "Skipped: the event is not available for final refresh.",
        { SkipReason: EventCompetitionRefreshSkipReason.EndCouldNotBeUpdated } => "Skipped: WOM end could not be updated; the last pre-end data was retained.",
        { SkipReason: EventCompetitionRefreshSkipReason.EndWindowUnmatched } => "Skipped: the WOM end did not match the configured end.",
        _ => "Skipped"
    };
    private string Localize(string key, params object[] arguments) => text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);
    private Task<IActionResult> RetiredReviewAction() => Task.FromResult<IActionResult>(BadRequest(Localize("This final-review action is retired. Resolve the underlying records and publish official results.")));
    private Guid AdminId => User.GetAccountId()!.Value;
    private LifecycleActor Actor => new(AdminId, User.Identity!.Name!);
}
