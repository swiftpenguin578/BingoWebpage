using Bingo.Web.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bingo.Web.Pages.Admin.Events;

// U7 0c (README item 1, brief 88 "In-place transport"): every Board command answers
// in place. A command posted by the page module (X-Requested-With) that would
// redirect back to this Board answers JSON instead: the definite outcome the
// handler recorded, its localized message, any issues and the authoritative
// no-store readback. Non-script callers keep the redirect and one-time status.
// Route refusals (302 to Manage) and lost sessions (302 to Login) are produced
// before the handler runs and are never rewritten here (C-CMP-2).
public sealed partial class BoardModel
{
    private bool IsInlineRequest =>
        string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var executed = await next();
        if (!HttpMethods.IsPost(Request.Method) || !IsInlineRequest || executed.Exception is not null && !executed.ExceptionHandled) return;
        if (executed.Result is not RedirectToPageResult { PageName: null } redirect) return;
        if (!TryEventId(redirect, out var eventId)) return;
        var message = TempData["StatusMessage"] as string;
        var type = TempData[UiMessage.TypeKey]?.ToString();
        var tileOutcome = TempData["BoardTileOutcome"]?.ToString();
        TempData.Remove("StatusMessage");
        TempData.Remove(UiMessage.TypeKey);
        TempData.Remove("BoardTileOutcome");
        var saved = tileOutcome is not null
            ? tileOutcome == "committed"
            : string.Equals(type, nameof(UiMessageType.Success), StringComparison.Ordinal);
        Response.Headers.CacheControl = "no-store";
        BoardReadback current;
        try { current = await CanReadBoardStateAsync(HttpContext.RequestAborted) ? await ReadBoardStateAsync(eventId, HttpContext.RequestAborted) : new(null); }
        catch (Exception exception) when (exception is not OperationCanceledException) { current = new(null); }
        executed.Result = new JsonResult(new BoardCommandOutcome(
            saved ? "saved" : "refused",
            message,
            (type ?? nameof(UiMessageType.Information)).ToLowerInvariant(),
            ValidationIssues.Select(LocalizeIssue).ToList(),
            current));
    }

    private static bool TryEventId(RedirectToPageResult redirect, out Guid eventId)
    {
        eventId = default;
        return redirect.RouteValues?.TryGetValue("id", out var value) == true && Guid.TryParse(value?.ToString(), out eventId);
    }

    private BoardIssueView LocalizeIssue(BoardValidationIssue issue) =>
        new(issue.Code, issue.TileId, issue.Position, issue.TileName, Localize(issue.ResourceKey, [.. issue.Arguments]), issue.DropNames);

    public sealed record BoardIssueView(string Code, Guid? TileId, int? Position, string? TileName, string Message, IReadOnlyList<string>? DropNames);
    public sealed record BoardCommandOutcome(string Outcome, string? Message, string Type, IReadOnlyList<BoardIssueView> Issues, BoardReadback Current);
}
