using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Security;

/// <summary>Authoritative route boundary for Admin event mutations; domain/services remain the mutation-time authority.</summary>
public sealed class EventMutationCapabilityPageFilter(ApplicationDbContext db, IStringLocalizer<SharedResource> text) : IAsyncPageFilter
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
        // A published-board correction is an exceptional, separately confirmed
        // lifecycle operation. Its start handler is the authority for the
        // confirmation/reason checks. Once begun, the private working copy must
        // also be editable while the public snapshot remains live, including in
        // Live events.
        if (IsPublishedBoardCorrection(path, context.HandlerMethod?.Name) ||
            await HasPublishedBoardCorrectionWorkspaceAsync(path, eventId, context.HttpContext.RequestAborted))
        {
            await next();
            return;
        }
        if (!EventStatePolicy.Allows(state.Value, capability))
        {
            if (context.HandlerInstance is PageModel page)
                page.TempData["StatusMessage"] = text["This event is read-only in its current lifecycle state."].Value;
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
            if (name.Contains("StartEvent", StringComparison.Ordinal) || name.Contains("EndEvent", StringComparison.Ordinal) || name.Contains("PrepareEndConfirmation", StringComparison.Ordinal) || name.Contains("Discard", StringComparison.Ordinal) || name.Contains("Cancel", StringComparison.Ordinal)) { capability = default; return false; }
            capability = name.Contains("ResumeEvent", StringComparison.Ordinal) ? EventCapability.ResumeEvent
                : name.Contains("ReopenSubmissions", StringComparison.Ordinal) ? EventCapability.ReviewEvidence
                : name.Contains("EvidenceCode", StringComparison.Ordinal) ? EventCapability.ConfigureEvidenceCodes
                : name.Contains("RefreshCompetition", StringComparison.Ordinal) || name.Contains("MakeDevelopmentCompetitionDue", StringComparison.Ordinal) ? EventCapability.CompetitionSynchronization
                : EventCapability.ConfigureSignup;
            return true;
        }
        if (path.EndsWith("/Draft.cshtml", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("ChangeRole", StringComparison.Ordinal))
        {
            capability = default; // roster role boundary performs its own operational-state checks.
            return false;
        }
        if (path.EndsWith("/Participant.cshtml", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("Withdraw", StringComparison.Ordinal) ||
             name.Contains("FillVacancy", StringComparison.Ordinal) ||
             name.Contains("CompletePromotionFollowUp", StringComparison.Ordinal)))
        {
            capability = default; // Participant lifecycle services perform their own state and authorization checks.
            return false;
        }
        capability = path.EndsWith("/Questions.cshtml", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith("/Participant.cshtml", StringComparison.OrdinalIgnoreCase)
            ? EventCapability.ConfigureSignup
            : EventCapability.ConfigureIdentityOrSchedule;
        return true;
    }

    private static bool IsPublishedBoardCorrection(string path, string? method) =>
        path.EndsWith("/Board.cshtml", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(method, "CorrectPublished", StringComparison.Ordinal);

    private Task<bool> HasPublishedBoardCorrectionWorkspaceAsync(string path, Guid eventId, CancellationToken ct) =>
        path.EndsWith("/Board.cshtml", StringComparison.OrdinalIgnoreCase)
            ? db.Boards.AsNoTracking().AnyAsync(board => board.EventId == eventId && board.PublishedCorrectionInProgress, ct)
            : Task.FromResult(false);
}
