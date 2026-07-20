using Bingo.Application.Access;
using Bingo.Web.UI;
using Bingo.Web.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Admin.Catalogue;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class WikiDryRunModel(OsrsWikiCatalogueDryRunService dryRun) : PageModel
{
    public WikiCatalogueDryRunReport? Report { get; private set; }

    public void OnGet() { }

    public async Task OnPostAsync(CancellationToken ct)
    {
        Report = await dryRun.RunAsync(ct);
    }

    public async Task<IActionResult> OnPostImportAsync(CancellationToken ct)
    {
        try
        {
            var result = await dryRun.ApplyReviewedImportAsync(ct);
            TempData["StatusMessage"] = $"Wiki catalogue imported: {result.BossesUpdated} bosses updated, {result.DropsAdded} drops added, {result.DropsUpdated} drops refreshed and {result.DropsRemoved} old drops removed.";
            TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
            return RedirectToPage("Index");
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            TempData["StatusMessage"] = exception.Message;
            TempData[UiMessage.TypeKey] = UiMessageType.Error.ToString();
            return RedirectToPage();
        }
    }
}
