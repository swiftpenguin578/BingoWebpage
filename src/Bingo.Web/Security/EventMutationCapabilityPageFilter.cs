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
        var policy = AdminEventPagePolicies.For(context.HandlerInstance.GetType());
        if (policy is null && context.ActionDescriptor.RelativePath.StartsWith("/Pages/Admin/Events/", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new NotFoundResult();
            return;
        }
        if (policy is null || !policy.HasEventContext || !TryEventId(context, out var eventId))
        {
            await next();
            return;
        }

        if (context.HandlerMethod is { } selected)
            _ = policy.Handler(selected.HttpMethod, selected.Name);

        var eventView = await db.Events.AsNoTracking().Where(item => item.Id == eventId).Select(item => new { item.State, item.HiddenAt }).SingleOrDefaultAsync(context.HttpContext.RequestAborted);
        if (eventView is null || eventView.State == EventState.Discarded)
        {
            context.Result = new NotFoundResult();
            return;
        }
        if (eventView.HiddenAt is not null)
        {
            var limitedInspection = IsLimitedHiddenManage(context, policy);
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
        if (policy.Kind == AdminEventPageKind.WiseOldMan && HttpMethods.IsPost(context.HttpContext.Request.Method))
        {
            if (context.HandlerMethod is null || !AdminEventPagePolicies.Allows(policy.Handler("POST", context.HandlerMethod.Name), eventView.State))
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
            && policy.Kind == AdminEventPageKind.SignupSetup
            && eventView.State is EventState.Cancelled or EventState.Finalized or EventState.Archived)
        {
            if ((context.HandlerInstance is Bingo.Web.Pages.Admin.Events.SignupSetupModel)
                && context.HandlerMethod is { } handler && handler.Name is null or "AddAccount"
                && string.Equals(context.HttpContext.Request.Query["handler"].ToString(), handler.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                var executed = await next();
                if (context.HandlerInstance is Bingo.Web.Pages.Admin.Events.SignupSetupModel { HasExactCommittedAddReplay: true }) return;
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
            var retainedArtworkRead = policy.Kind == AdminEventPageKind.Board &&
                string.Equals(context.HandlerMethod.Name, "TileImage", StringComparison.Ordinal) &&
                context.HandlerArguments.TryGetValue("approvalId", out var approvalId) && approvalId is Guid;
            if (IsTerminalReadOnlyRoute(policy, eventView.State) && !retainedArtworkRead)
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
        if (policy.Kind == AdminEventPageKind.SignupSetup
            && context.HandlerMethod.Name is null or "AddAccount")
        {
            await next();
            return;
        }
        var gate = policy.Handler("POST", context.HandlerMethod.Name);
        if (gate == AdminEventHandlerGate.Hide && IsExactHideHandler(context))
        {
            await next();
            return;
        }
        // Hide's exact query check is retained; a case-mismatched query follows
        // the same ConfigureSignup route gate as before.
        if (gate == AdminEventHandlerGate.Hide) gate = AdminEventHandlerGate.Signup;
        // A published-board correction is an exceptional, separately confirmed
        // lifecycle operation. Its private working copy is editable only while
        // the event remains operational.
        if (gate == AdminEventHandlerGate.BoardCorrection ||
            await HasPublishedBoardCorrectionWorkspaceAsync(policy, eventId, context.HttpContext.RequestAborted))
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
        if (!AdminEventPagePolicies.Allows(gate, eventView.State))
        {
            if (context.HandlerInstance is PageModel page)
                page.TempData["StatusMessage"] = text["This event is read-only in its current lifecycle state."].Value;
            context.Result = new RedirectResult($"/Admin/Events/Manage/{eventId}");
            return;
        }
        await next();
    }

    private static bool IsTerminalReadOnlyRoute(AdminEventPagePolicy policy, EventState state) =>
        state is (EventState.Cancelled or EventState.Finalized or EventState.Archived) &&
        !policy.ViewableOnTerminalEvents;

    private static bool TryEventId(PageHandlerExecutingContext context, out Guid eventId)
    {
        if (context.RouteData.Values.TryGetValue("id", out var route) && Guid.TryParse(route?.ToString(), out eventId)) return true;
        if (context.HandlerArguments.TryGetValue("id", out var argument) && Guid.TryParse(argument?.ToString(), out eventId)) return true;
        eventId = default;
        return false;
    }

    // C-CMP-1 / U4-Q3 (c): the Super Admin's limited Overview view opens on the plain
    // event URL; a legacy ?hidden=true link is still accepted (and ignored).
    private static bool IsLimitedHiddenManage(PageHandlerExecutingContext context, AdminEventPagePolicy policy) =>
        policy.Kind == AdminEventPageKind.Manage &&
        context.HttpContext.User.IsInRole("SuperAdmin");

    private static bool IsExactHideHandler(PageHandlerExecutingContext context) =>
        string.Equals(context.HttpContext.Request.Query["handler"].ToString(), "Hide", StringComparison.Ordinal) &&
        string.Equals(context.HandlerMethod?.Name, "Hide", StringComparison.Ordinal);

    private Task<bool> HasPublishedBoardCorrectionWorkspaceAsync(AdminEventPagePolicy policy, Guid eventId, CancellationToken ct) =>
        policy.Kind == AdminEventPageKind.Board
            ? db.Boards.AsNoTracking().AnyAsync(board => board.EventId == eventId && board.PublishedCorrectionInProgress, ct)
            : Task.FromResult(false);
}
