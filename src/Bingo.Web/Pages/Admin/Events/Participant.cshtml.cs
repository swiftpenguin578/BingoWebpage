using Bingo.Application.Access;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Admin.Events;

// U5 item 1b / brief 87 (A2): the old participant detail URL, still used by persisted
// admin notifications and bookmarks (also with ?overlay=1), redirects to the
// Participants drawer URL. Its write handlers are retired (A10): every editing path
// lives on the Participants page (drawer save, row actions) and roster removal stays
// on Teams (S5). A post to this route is refused before any work.
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class ParticipantModel : PageModel
{
    public IActionResult OnGet(Guid id, Guid participantId) => Redirect(DrawerUrl(id, participantId));

    public static string DrawerUrl(Guid eventId, Guid participantId) => $"/Admin/Events/Participants/{eventId}?participant={participantId}";

    public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        if (context.HandlerMethod is null || !HttpMethods.IsGet(context.HttpContext.Request.Method)) context.Result = new NotFoundResult();
    }
}
