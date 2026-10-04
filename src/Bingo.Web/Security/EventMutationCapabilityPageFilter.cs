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

        var eventView = await db.Events.AsNoTracking().Where(item => item.Id == eventId).Select(item => new { item.State, item.HiddenAt }).SingleOrDefaultAsync(context.HttpContext.RequestAborted);
        if (eventView is null || eventView.State == EventState.Discarded)
        {
            context.Result = new NotFoundResult();
            return;
        }
        if (eventView.HiddenAt is not null)
        {
            var limitedInspection = IsLimitedHiddenManage(context);
            if (!limitedInspection || (HttpMethods.IsPost(context.HttpContext.Request.Method) && !string.Equals(context.HandlerMethod?.Name, "RestoreHidden", StringComparison.Ordinal)))
            {
                context.Result = new NotFoundResult();
                return;
            }
            await next();
            return;
        }
        // Keep the dedicated Wise Old Man workspace behind its operation-specific
        // lifecycle matrix even when an unknown POST handler leaves HandlerMethod
        // unset. Otherwise a forged handler name could bypass the route boundary
        // and reach the page model without any mutation capability decision.
        if (IsWiseOldManPath(path) && HttpMethods.IsPost(context.HttpContext.Request.Method))
        {
            if (context.HandlerMethod is null || !AllowsWiseOldManMutation(eventView.State, context.HandlerMethod.Name ?? string.Empty))
            {
                if (context.HandlerInstance is PageModel page)
                    page.TempData["StatusMessage"] = text["This event is read-only in its current lifecycle state."].Value;
                context.Result = new RedirectResult($"/Admin/Events/Manage/{eventId}");
                return;
            }

            // Wise Old Man operations have narrower operation-specific guards
            // in their application services than the generic event capability
            // matrix. The route gate limits the lifecycle states; services
            // remain authoritative for version, provenance, credentials,
            // roster, cooldown and remote-write checks.
            await next();
            return;
        }
        // Terminal Questions POSTs may only return an exact, already committed
        // add result. The service verifies request/actor/event/intent under its lock;
        // malformed, conflicting and genuinely new requests keep the route refusal.
        if (HttpMethods.IsPost(context.HttpContext.Request.Method)
            && path.EndsWith("/Questions.cshtml", StringComparison.OrdinalIgnoreCase)
            && eventView.State is EventState.Cancelled or EventState.Finalized or EventState.Archived)
        {
            if (context.HandlerInstance is Bingo.Web.Pages.Admin.Events.QuestionsModel questions
                && context.HandlerMethod is { } handler && handler.Name is null or "AddAccount"
                && string.Equals(context.HttpContext.Request.Query["handler"].ToString(), handler.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                var executed = await next();
                if (questions.HasExactCommittedAddReplay) return;
                executed.Result = new RedirectResult($"/Admin/Events/Manage/{eventId}");
            }
            else context.Result = new RedirectResult($"/Admin/Events/Manage/{eventId}");
            if (context.HandlerInstance is PageModel page)
                page.TempData["StatusMessage"] = text["This event is read-only in its current lifecycle state."].Value;
            return;
        }
        if (context.HandlerMethod is null)
        {
            await next();
            return;
        }
        if (!HttpMethods.IsPost(context.HttpContext.Request.Method))
        {
            var retainedArtworkRead = path.EndsWith("/Board.cshtml", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.HandlerMethod.Name, "TileImage", StringComparison.Ordinal) &&
                context.HandlerArguments.TryGetValue("approvalId", out var approvalId) && approvalId is Guid;
            if (IsTerminalReadOnlyRoute(path, eventView.State) && !retainedArtworkRead && !IsWiseOldManPath(path))
            {
                context.Result = new RedirectResult($"/Admin/Events/Manage/{eventId}");
                return;
            }
            await next();
            return;
        }
        // Field-add POSTs also reconcile a previously committed request. The signup
        // service rechecks current Admin/visibility and gates every genuinely new
        // write; later lifecycle state must not prevent an authorized success replay.
        if (path.EndsWith("/Questions.cshtml", StringComparison.OrdinalIgnoreCase)
            && context.HandlerMethod.Name is null or "AddAccount")
        {
            await next();
            return;
        }
        if (IsExactHideHandler(context))
        {
            await next();
            return;
        }
        if (eventView.State == EventState.Live &&
            path.EndsWith("/Schedule.cshtml", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }
        if (!TryCapability(path, context.HandlerMethod?.Name, out var capability))
        {
            await next();
            return;
        }
        // A published-board correction is an exceptional, separately confirmed
        // lifecycle operation. Its private working copy is editable only while
        // the event remains operational.
        if (IsPublishedBoardCorrection(path, context.HandlerMethod?.Name) ||
            await HasPublishedBoardCorrectionWorkspaceAsync(path, eventId, context.HttpContext.RequestAborted))
        {
            if (eventView.State is EventState.SignupClosed or EventState.Live or EventState.AwaitingFinalReview)
            {
                await next();
                return;
            }
            if (context.HandlerInstance is PageModel page)
                page.TempData["StatusMessage"] = text["This event is read-only in its current lifecycle state."].Value;
            context.Result = new RedirectResult($"/Admin/Events/Manage/{eventId}");
            return;
        }
        if (!EventStatePolicy.Allows(eventView.State, capability))
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
        !path.EndsWith("/Finalize.cshtml", StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith("/Participants.cshtml", StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith("/Participant.cshtml", StringComparison.OrdinalIgnoreCase);

    private static bool IsWiseOldManPath(string path) =>
        path.EndsWith("/WiseOldMan.cshtml", StringComparison.OrdinalIgnoreCase);

    private static bool AllowsWiseOldManMutation(EventState state, string method) => method switch
    {
        "FetchCompetition" or "MakeDevelopmentCompetitionDue" => state == EventState.Live,
        "Competition" or "DisconnectCompetition" or "CreateManagedCompetition" or "AdoptCompetitionCredential" or "DeleteManagedCompetition"
            => state is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed or EventState.Live,
        _ => false
    };

    private static bool TryEventId(PageHandlerExecutingContext context, out Guid eventId)
    {
        if (context.RouteData.Values.TryGetValue("id", out var route) && Guid.TryParse(route?.ToString(), out eventId)) return true;
        if (context.HandlerArguments.TryGetValue("id", out var argument) && Guid.TryParse(argument?.ToString(), out eventId)) return true;
        eventId = default;
        return false;
    }

    private static bool IsLimitedHiddenManage(PageHandlerExecutingContext context) =>
        context.ActionDescriptor.RelativePath is { } path &&
        path.EndsWith("/Manage.cshtml", StringComparison.OrdinalIgnoreCase) &&
        context.HttpContext.User.IsInRole("SuperAdmin") &&
        string.Equals(context.HttpContext.Request.Query["hidden"].ToString(), "true", StringComparison.Ordinal);

    private static bool IsExactHideHandler(PageHandlerExecutingContext context) =>
        context.ActionDescriptor.RelativePath is { } path &&
        path.EndsWith("/Manage.cshtml", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(context.HttpContext.Request.Query["handler"].ToString(), "Hide", StringComparison.Ordinal) &&
        string.Equals(context.HandlerMethod?.Name, "Hide", StringComparison.Ordinal);

    private static bool TryCapability(string path, string? method, out EventCapability capability)
    {
        var name = method ?? string.Empty;
        if (path.EndsWith("/Finalize.cshtml", StringComparison.OrdinalIgnoreCase))
        {
            capability = default; return false; // Publish/reopen own transactional guards.
        }
        if (path.EndsWith("/Manage.cshtml", StringComparison.OrdinalIgnoreCase))
        {
            if (name.Contains("StartEvent", StringComparison.Ordinal) || name.Contains("EndEvent", StringComparison.Ordinal) || name.Contains("Discard", StringComparison.Ordinal) || name.Contains("Cancel", StringComparison.Ordinal)) { capability = default; return false; }
            if (name.Contains("Competition", StringComparison.Ordinal) &&
                !name.Contains("RefreshCompetition", StringComparison.Ordinal) &&
                !name.Contains("MakeDevelopmentCompetitionDue", StringComparison.Ordinal)) { capability = default; return false; }
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
             name.Contains("CompletePromotionFollowUp", StringComparison.Ordinal) ||
             name.Contains("Payment", StringComparison.Ordinal) ||
             name.Contains("AdminNote", StringComparison.Ordinal) ||
             name.Contains("TransferOwnership", StringComparison.Ordinal)))
        {
            capability = default; // Participant lifecycle services perform their own state and authorization checks.
            return false;
        }
        if (path.EndsWith("/Identity.cshtml", StringComparison.OrdinalIgnoreCase))
        {
            capability = EventCapability.ConfigureIdentity;
            return true;
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
