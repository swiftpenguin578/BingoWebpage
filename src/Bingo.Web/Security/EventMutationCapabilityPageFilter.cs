using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Security;

/// <summary>Authoritative route boundary for Admin event mutations; domain/services remain the mutation-time authority.</summary>
public sealed class EventMutationCapabilityPageFilter(ApplicationDbContext db) : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (context.ActionDescriptor.RelativePath is not { } path ||
            !path.Contains("Admin/Events/", StringComparison.OrdinalIgnoreCase) ||
            !TryEventId(context, out var eventId))
        {
            await next();
            return;
        }

        var state = await db.Events.AsNoTracking().Where(item => item.Id == eventId).Select(item => (EventState?)item.State).SingleOrDefaultAsync(context.HttpContext.RequestAborted);
        if (state is null || state == EventState.Discarded)
        {
            context.Result = new NotFoundResult();
            return;
        }
        if (!HttpMethods.IsPost(context.HttpContext.Request.Method))
        {
            if (IsTerminalReadOnlyRoute(path, state.Value))
            {
                context.Result = new RedirectResult($"/Admin/Events/Manage/{eventId}");
                return;
            }
            await next();
            return;
        }
        if (!TryCapability(path, context.HandlerMethod?.Name, out var capability))
        {
            await next();
            return;
        }
        if (!EventStatePolicy.Allows(state.Value, capability))
        {
            if (context.HandlerInstance is PageModel page)
                page.TempData["StatusMessage"] = "This event is read-only in its current lifecycle state.";
            context.Result = new RedirectResult($"/Admin/Events/Manage/{eventId}");
            return;
        }
        await next();
    }

    private static bool IsTerminalReadOnlyRoute(string path, EventState state) =>
        state is (EventState.Cancelled or EventState.Finalized or EventState.Archived) &&
        !path.EndsWith("/Manage.cshtml", StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith("/Finalize.cshtml", StringComparison.OrdinalIgnoreCase);

    private static bool TryEventId(PageHandlerExecutingContext context, out Guid eventId)
    {
        if (context.RouteData.Values.TryGetValue("id", out var route) && Guid.TryParse(route?.ToString(), out eventId)) return true;
        if (context.HandlerArguments.TryGetValue("id", out var argument) && Guid.TryParse(argument?.ToString(), out eventId)) return true;
        eventId = default;
        return false;
    }

    private static bool TryCapability(string path, string? method, out EventCapability capability)
    {
        var name = method ?? string.Empty;
        if (path.EndsWith("/Finalize.cshtml", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("Resolve", StringComparison.Ordinal) || name.Contains("CorrectCompletion", StringComparison.Ordinal)) { capability = EventCapability.ReviewEvidence; return true; }
            capability = default; return false; // Finalize/Archive/Unfinalize own transactional guards.
        }
        if (path.EndsWith("/Manage.cshtml", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("StartEvent", StringComparison.Ordinal) || name.Contains("EndEvent", StringComparison.Ordinal) || name.Contains("Discard", StringComparison.Ordinal) || name.Contains("Cancel", StringComparison.Ordinal)) { capability = default; return false; }
            capability = name.Contains("ReopenSubmissions", StringComparison.Ordinal) ? EventCapability.ReviewEvidence
                : name.Contains("EvidenceCode", StringComparison.Ordinal) ? EventCapability.ConfigureEvidenceCodes
                : EventCapability.ConfigureSignup;
            return true;
        }
        capability = path.EndsWith("/Questions.cshtml", StringComparison.OrdinalIgnoreCase)
            ? EventCapability.ConfigureSignup
            : EventCapability.ConfigureIdentityOrSchedule;
        return true;
    }
}
