using Bingo.Web.UI;
using Microsoft.AspNetCore.Mvc;

namespace Bingo.Web.Pages.Admin.Events;

// U6 (brief 93): the Teams / Draft page posts every command through AdminFetch and
// reads a JSON outcome. Direct form posts and the existing page-model tests keep the
// post/redirect/get result with the TempData status. A JSON response always carries
// an explicit outcome; a handler that ends without a recorded status is reported as
// refused, never as done (a missing outcome is never success).
public sealed partial class DraftModel
{
    private string? statusMessage;
    private UiMessageType? statusType;
    private bool statusStale;
    private object? outcomeData;

    private bool WantsJson => HttpContext is { } context &&
        context.Request.GetTypedHeaders().Accept?.Any(value => value.MediaType.Value == "application/json") == true;

    private IActionResult Finish(object routeValues)
    {
        if (!WantsJson) return RedirectToPage(routeValues);
        if (HttpContext is { } context) context.Response.Headers.CacheControl = "no-store";
        var refused = statusType is null or UiMessageType.Error;
        return new JsonResult(new
        {
            outcome = refused ? statusStale ? "stale" : "refused" : "done",
            tone = statusType switch { UiMessageType.Warning => "warning", UiMessageType.Information => "info", UiMessageType.Success => "success", _ => "error" },
            message = statusMessage ?? Localize("That didn’t go through."),
            data = outcomeData
        });
    }

    private void SetOutcomeData(object data) => outcomeData = data;
    private void MarkStale() => statusStale = true;
}
