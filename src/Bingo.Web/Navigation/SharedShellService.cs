using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Navigation;

public sealed class SharedShellService(ApplicationDbContext db, IStringLocalizer<SharedResource> text, IEventReadinessEvaluator readinessEvaluator, IEventLifecycleService eventLifecycle, IEventFinalizationService finalizationService, TimeProvider timeProvider, IHttpContextAccessor? httpContextAccessor = null)
{
    public async Task<SharedShellData> GetAsync(ClaimsPrincipal user, RouteValueDictionary routeValues, CancellationToken cancellationToken, string? selectedEventId = null, Guid? contextEventId = null, Guid? contextTeamId = null)
    {
        var page = routeValues["page"]?.ToString() ?? string.Empty;
        await ResolveAdminEventAsync(user, routeValues, selectedEventId, cancellationToken);
        var breadcrumbs = await BuildBreadcrumbs(page, routeValues, cancellationToken);
        var adminEvent = await GetAdminEventContextAsync(page, routeValues, selectedEventId, cancellationToken);
        var adminEvents = user.IsInRole("Admin") || user.IsInRole("SuperAdmin")
            ? await GetAdminEventOptionsAsync(cancellationToken)
            : [];
        var notifications = user.Identity?.IsAuthenticated == true
            ? await GetNotificationsAsync(user, cancellationToken)
            : NotificationInbox.Empty;
        var eventSlug = routeValues["slug"]?.ToString();
        if (page is "/Events/Board" or "/Events/TeamBoard" or "/Events/Teams" or "/Events/Stats")
        {
            // An invalid explicit event must never fall back to the account's preferred event.
            contextEventId = await db.Events.AsNoTracking()
                .Where(item => item.Slug == eventSlug && item.HiddenAt == null)
                .Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken) ?? Guid.Empty;
        }
        var currentEvent = await db.Events.AsNoTracking()
            .Where(item => item.HiddenAt == null &&
                (item.State == EventState.Live || item.State == EventState.AwaitingFinalReview) &&
                db.Boards.Any(board => board.EventId == item.Id && board.State == BoardState.Published))
            .OrderByDescending(item => item.State == EventState.Live)
            .ThenByDescending(item => item.EventStartsAt).ThenBy(item => item.Id)
            .Select(item => new CurrentEventNavigation(item.Id, item.Slug))
            .FirstOrDefaultAsync(cancellationToken);
        var captainNavigation = user.Identity?.IsAuthenticated == true
            ? await GetCaptainNavigationAsync(user, contextEventId, contextTeamId, cancellationToken)
            : null;
        var submissionNavigation = user.Identity?.IsAuthenticated == true
            ? await GetSubmissionNavigationAsync(user, contextEventId, contextTeamId, cancellationToken)
            : null;
        return new SharedShellData(breadcrumbs, notifications, adminEvent, adminEvents, captainNavigation, submissionNavigation, currentEvent);
    }

    public async Task<AdminDesignShell> GetAdminDesignAsync(ClaimsPrincipal user, RouteValueDictionary routes, CancellationToken cancellationToken, string? selectedEventId = null)
    {
        var page = routes["page"]?.ToString() ?? string.Empty;
        var superAdmin = user.IsInRole("SuperAdmin");
        var selected = await ResolveAdminEventAsync(user, routes, selectedEventId, cancellationToken);
        if (!superAdmin && !user.IsInRole("Admin")) return new(null, [], NotificationInbox.Empty);
        var rows = await db.Events.AsNoTracking()
            .Where(item => item.State != EventState.Discarded && (item.HiddenAt == null || superAdmin)
                && (item.Id == selected || item.State == EventState.Draft || item.State == EventState.SignupOpen
                    || item.State == EventState.SignupClosed || item.State == EventState.Live || item.State == EventState.AwaitingFinalReview))
            .OrderBy(item => item.EventStartsAt == null).ThenBy(item => item.EventStartsAt).ThenBy(item => item.Id)
            .Select(item => new { item.Id, item.Name, item.State, item.HiddenAt, item.SignupClosesAt, item.EventStartsAt, item.EventEndsAt, item.Timezone })
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var options = rows.Select(item =>
        {
            var date = item.State switch
            {
                EventState.SignupOpen => item.SignupClosesAt,
                EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived or EventState.Cancelled => item.EventEndsAt,
                _ => item.EventStartsAt
            };
            var shownDate = date.HasValue
                ? DateTimePresentation.Format(date.Value, "d MMM", string.IsNullOrWhiteSpace(item.Timezone) ? "UTC" : item.Timezone, CultureInfo.CurrentCulture)
                : null;
            var ended = item.State is EventState.Finalized or EventState.Archived or EventState.Cancelled
                || (item.State is EventState.Live or EventState.AwaitingFinalReview && item.EventEndsAt <= now);
            var when = shownDate is null ? text["not announced"].Value : ended ? text["AdminDesign.ended {0}", shownDate].Value : item.State switch
            {
                EventState.SignupOpen => text["closes {0}", shownDate].Value,
                EventState.Live or EventState.AwaitingFinalReview => text["Ends {0}", shownDate].Value,
                _ => text["Starts {0}", shownDate].Value
            };
            when = char.ToLower(when[0], CultureInfo.CurrentCulture) + when[1..];
            var tone = item.State switch { EventState.Live or EventState.AwaitingFinalReview => "tone-live", EventState.SignupOpen => "tone-open", _ => "tone-draft" };
            return new AdminDesignEvent(item.Id, item.Name, item.State, item.HiddenAt.HasValue,
                AdminEventStatePresentation.For(item.State, text).Label, tone, when,
                AdminDesignEventUrl(page, item.Id, item.HiddenAt.HasValue));
        }).ToList();
        var accountId = user.GetAccountId();
        var accountName = accountId.HasValue
            ? await db.Accounts.AsNoTracking().Where(account => account.Id == accountId.Value)
                .Select(account => account.PublicUsername).SingleOrDefaultAsync(cancellationToken)
            : null;
        return new(options.SingleOrDefault(item => item.Id == selected),
            options.Where(item => item.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed or EventState.Live or EventState.AwaitingFinalReview).ToList(),
            await GetNotificationsAsync(user, cancellationToken))
        { ShowEventBreadcrumb = IsAdminEventPage(page, routes, selectedEventId), AccountName = accountName ?? string.Empty, AccountRole = superAdmin ? text["AdminDesign.Super admin"].Value : text["Administrator"].Value };
    }

    private static bool IsAdminEventPage(string page, RouteValueDictionary routes, string? selectedEventId) =>
        (page.StartsWith("/Admin/Events/", StringComparison.Ordinal) && Guid.TryParse(routes["id"]?.ToString(), out _))
        || (page.StartsWith("/Admin/Review/", StringComparison.Ordinal) && Guid.TryParse(selectedEventId, out _));

    private async Task<Guid?> ResolveAdminEventAsync(ClaimsPrincipal user, RouteValueDictionary routes, string? selectedEventId, CancellationToken ct)
    {
        var context = httpContextAccessor?.HttpContext;
        var remembered = context?.Request.Cookies[AdminEventSession.CookieName];
        var page = routes["page"]?.ToString() ?? string.Empty;
        var explicitEvent = IsAdminEventPage(page, routes, selectedEventId);
        var raw = explicitEvent ? (page.StartsWith("/Admin/Review/", StringComparison.Ordinal) ? selectedEventId : routes["id"]?.ToString()) : remembered;
        var admin = user.IsInRole("Admin") || user.IsInRole("SuperAdmin");
        if (admin && Guid.TryParse(raw, out var id) && await db.Events.AsNoTracking().AnyAsync(item =>
                item.Id == id && item.State != EventState.Discarded && (item.HiddenAt == null || user.IsInRole("SuperAdmin")), ct))
        {
            if (context is not null && explicitEvent && remembered != id.ToString("D")) AdminEventSession.Remember(context, id);
            return id;
        }
        if (context is not null && remembered is not null) AdminEventSession.Clear(context);
        return null;
    }

    public static string AdminDesignEventUrl(string page, Guid eventId, bool hidden = false)
    {
        if (hidden) return $"/Admin/Events/Manage/{eventId}?hidden=true";
        return page switch
        {
            "/Admin/Events/Identity" or "/Admin/Events/Schedule" or "/Admin/Events/Participants"
                or "/Admin/Events/Questions" or "/Admin/Events/Draft" or "/Admin/Events/Board"
                or "/Admin/Events/WiseOldMan" or "/Admin/Events/Finalize" => $"{page}/{eventId}",
            "/Admin/Review/Index" or "/Admin/Review/Details" => $"/Admin/Review/Index?eventId={eventId}",
            _ => $"/Admin/Events/Manage/{eventId}"
        };
    }

    private async Task<SubmissionNavigation?> GetSubmissionNavigationAsync(ClaimsPrincipal user, Guid? contextEventId, Guid? contextTeamId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)) return null;
        var accountType = await db.Accounts.AsNoTracking()
            .Where(account => account.Id == accountId && account.Active)
            .Select(account => (Bingo.Domain.Access.AccountType?)account.AccountType)
            .SingleOrDefaultAsync(cancellationToken);
        if (accountType != Bingo.Domain.Access.AccountType.WebsiteAccount) return null;

        var rows = await (from participant in db.EventParticipants.AsNoTracking()
                          join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
                          join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
                          join bingoEvent in db.Events.AsNoTracking() on participant.EventId equals bingoEvent.Id
                          where participant.AccountId == accountId && team.Active && membership.LeftAt == null && bingoEvent.HiddenAt == null &&
                                membership.Role == Bingo.Domain.Teams.TeamMembershipRole.Participant && participant.EventId == team.EventId &&
                                (bingoEvent.State == EventState.Live || bingoEvent.State == EventState.AwaitingFinalReview)
                          select new { EventId = participant.EventId, TeamId = team.Id, State = bingoEvent.State })
            .Distinct()
            .ToListAsync(cancellationToken);
        rows = rows.Where(row => (contextEventId == null || row.EventId == contextEventId) &&
            (contextTeamId == null || row.TeamId == contextTeamId)).ToList();
        var preferredRows = rows.Where(row => row.State == EventState.Live).ToList();
        if (preferredRows.Count == 0) preferredRows = rows.Where(row => row.State == EventState.AwaitingFinalReview).ToList();
        return preferredRows.Count == 1 ? new SubmissionNavigation(preferredRows[0].EventId, preferredRows[0].TeamId) : null;
    }

    private async Task<CaptainNavigation?> GetCaptainNavigationAsync(ClaimsPrincipal user, Guid? contextEventId, Guid? contextTeamId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)) return null;
        var accountType = await db.Accounts.AsNoTracking()
            .Where(account => account.Id == accountId && account.Active)
            .Select(account => (Bingo.Domain.Access.AccountType?)account.AccountType)
            .SingleOrDefaultAsync(cancellationToken);
        if (accountType is null) return null;

        var scopes = new List<(Guid EventId, Guid TeamId, EventState State)>();
        if (accountType == Bingo.Domain.Access.AccountType.WebsiteAccount)
        {
            var rows = await (from participant in db.EventParticipants.AsNoTracking()
                              join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
                              join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
                              join bingoEvent in db.Events.AsNoTracking() on participant.EventId equals bingoEvent.Id
                              where participant.AccountId == accountId && team.Active && membership.LeftAt == null &&
                                    participant.EventId == team.EventId && bingoEvent.HiddenAt == null &&
                                    (bingoEvent.State == EventState.Live || bingoEvent.State == EventState.AwaitingFinalReview) &&
                                    (membership.Role == Bingo.Domain.Teams.TeamMembershipRole.Captain || membership.Role == Bingo.Domain.Teams.TeamMembershipRole.CoCaptain)
                              select new { EventId = bingoEvent.Id, TeamId = team.Id, State = bingoEvent.State })
                .Distinct()
                .ToListAsync(cancellationToken);
            scopes = rows.Select(scope => (scope.EventId, scope.TeamId, scope.State)).ToList();
        }
        scopes = scopes.Where(scope => (contextEventId == null || scope.EventId == contextEventId) &&
            (contextTeamId == null || scope.TeamId == contextTeamId)).ToList();
        var preferredScopes = scopes.Where(scope => scope.State == EventState.Live).ToList();
        if (preferredScopes.Count == 0) preferredScopes = scopes.Where(scope => scope.State == EventState.AwaitingFinalReview).ToList();
        return preferredScopes.Count == 1 ? new CaptainNavigation(preferredScopes[0].EventId, preferredScopes[0].TeamId) : null;
    }

    public async Task<NotificationInbox> GetNotificationsAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var personalItems = new List<ShellNotification>();
        if (Guid.TryParse(user.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier), out var accountId))
        {
            var unread = db.PersonalNotifications.AsNoTracking().Where(item => item.RecipientAccountId == accountId && item.ReadAt == null && (item.EventId == null || db.Events.Any(eventItem => eventItem.Id == item.EventId && eventItem.HiddenAt == null)));
            var personal = await unread.OrderByDescending(item => item.CreatedAt).Take(6).ToListAsync(cancellationToken);
            personalItems = personal.Select(item => new ShellNotification(item.Id, NotificationPresentation.Title(text, item.Title), NotificationPresentation.Detail(text, item.Title, item.Detail), $"/notifications?read={item.Id}")).ToList();
            var personalCount = await unread.CountAsync(cancellationToken);
            var adminActions = user.IsInRole("Admin") || user.IsInRole("SuperAdmin")
                ? await GetAdminActionsSafelyAsync(cancellationToken)
                : new AdminActionProjection([], [], 0);
            var eventIds = await db.Events.AsNoTracking().Where(item => item.HiddenAt == null && (item.State == EventState.Live || item.State == EventState.AwaitingFinalReview)).Select(item => item.Id).ToListAsync(cancellationToken);
            return new NotificationInbox(eventIds, personalCount + adminActions.Count, text["Notifications"], text["No notifications."], text["Notifications"], "/notifications", personalItems,
                personalCount, adminActions.Count, text["Admin actions"], text["No unresolved Admin actions."], text["Admin actions"], "/Admin", adminActions.Items, !adminActions.IsAvailable);
        }
        var anonymousAdminActions = user.IsInRole("Admin") || user.IsInRole("SuperAdmin")
            ? await GetAdminActionsSafelyAsync(cancellationToken)
            : new AdminActionProjection([], [], 0);
        var anonymousEventIds = await db.Events.AsNoTracking().Where(item => item.HiddenAt == null && (item.State == EventState.Live || item.State == EventState.AwaitingFinalReview)).Select(item => item.Id).ToListAsync(cancellationToken);
        return new NotificationInbox(anonymousEventIds, anonymousAdminActions.Count, text["Notifications"], text["No notifications."], text["Notifications"], "/notifications", personalItems,
            0, anonymousAdminActions.Count, text["Admin actions"], text["No unresolved Admin actions."], text["Admin actions"], "/Admin", anonymousAdminActions.Items, !anonymousAdminActions.IsAvailable);
    }

    public async Task<AdminActionProjection> GetAdminActionsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var activeEvents = await db.Events.AsNoTracking()
            .Where(item => item.HiddenAt == null && (item.State == EventState.Draft || item.State == EventState.SignupOpen || item.State == EventState.SignupClosed || item.State == EventState.Live || item.State == EventState.AwaitingFinalReview))
            .Select(item => new { item.Id, item.Name, item.Timezone, item.State, item.SignupOpensAt, item.EventStartsAt })
            .ToListAsync(cancellationToken);
        var activeEventIds = activeEvents.Select(item => item.Id).ToList();
        var eventMap = activeEvents.ToDictionary(item => item.Id);
        var pendingByEvent = await db.Submissions.AsNoTracking()
            .Where(item => activeEventIds.Contains(item.EventId) && item.Status == SubmissionStatus.Pending)
            .GroupBy(item => item.EventId)
            .Select(group => new PendingEvidenceAggregate(group.Key, group.Count(), group.Min(item => item.SubmittedAt), group.Max(item => item.SubmittedAt)))
            .ToListAsync(cancellationToken);

        // An attempt is actionable only while the event still owns the same
        // configured boundary and remains in the lifecycle state that can
        // recover it.  This keeps a historical failed attempt from leaking
        // into a later schedule/state projection.
        var failedOpenings = (await db.ScheduledSignupOpeningAttempts.AsNoTracking()
                .Where(item => activeEventIds.Contains(item.EventId) && !item.Opened && item.ResolvedAt == null && item.ScheduledFor <= now)
                .OrderByDescending(item => item.AttemptedAt)
                .ToListAsync(cancellationToken))
            .Where(item => eventMap.TryGetValue(item.EventId, out var eventItem)
                && eventItem.State == EventState.Draft
                && eventItem.SignupOpensAt == item.ScheduledFor)
            .GroupBy(item => item.EventId)
            .Select(group => group.First())
            .ToList();

        var postponedStarts = (await db.ScheduledEventStartAttempts.AsNoTracking()
                .Where(item => activeEventIds.Contains(item.EventId) && !item.Started && item.ResolvedAt == null && item.ScheduledFor <= now)
                .OrderByDescending(item => item.AttemptedAt)
                .ToListAsync(cancellationToken))
            .Where(item => eventMap.TryGetValue(item.EventId, out var eventItem)
                && eventItem.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed
                && eventItem.EventStartsAt == item.ScheduledFor)
            .GroupBy(item => item.EventId)
            .Select(group => group.First())
            .ToList();

        var eventActions = new Dictionary<Guid, AdminActionSummary>();
        foreach (var eventItem in activeEvents)
            eventActions[eventItem.Id] = new(eventItem.Id, 0, false, false);

        foreach (var pending in pendingByEvent)
        {
            var current = eventActions[pending.EventId];
            eventActions[pending.EventId] = current with { PendingEvidenceCount = pending.Count };
        }
        foreach (var opening in failedOpenings)
        {
            var current = eventActions[opening.EventId];
            eventActions[opening.EventId] = current with { ScheduledOpeningFailed = true };
        }
        foreach (var start in postponedStarts)
        {
            var current = eventActions[start.EventId];
            eventActions[start.EventId] = current with { ScheduledStartPostponed = true };
        }

        var actionRows = new List<(DateTimeOffset At, ShellNotification Item)>();
        foreach (var pending in pendingByEvent)
        {
            if (!eventMap.TryGetValue(pending.EventId, out var eventItem)) continue;
            actionRows.Add((pending.LatestSubmittedAt, new ShellNotification(
                eventItem.Id,
                "Evidence review",
                $"{eventItem.Name} · {pending.Count} pending submission(s) · oldest {FormatDate(pending.OldestSubmittedAt, eventItem.Timezone)}",
                $"/Admin/Review/Index?eventId={eventItem.Id}",
                "Evidence review",
                $"{pending.Count} pending")));
        }
        foreach (var opening in failedOpenings)
        {
            if (!eventMap.TryGetValue(opening.EventId, out var eventItem)) continue;
            actionRows.Add((opening.AttemptedAt, new ShellNotification(opening.Id, "Scheduled signup opening failed", $"{eventItem.Name} · Resolve the scheduled signup opening blockers.", $"/Admin/Events/Manage/{opening.EventId}")));
        }
        foreach (var start in postponedStarts)
        {
            if (!eventMap.TryGetValue(start.EventId, out var eventItem)) continue;
            actionRows.Add((start.AttemptedAt, new ShellNotification(start.Id, "Postponed start", $"{eventItem.Name} · Resolve the scheduled start blockers.", $"/Admin/Events/Manage/{start.EventId}")));
        }

        var items = actionRows.OrderByDescending(item => item.At).ThenBy(item => item.Item.Title, StringComparer.Ordinal).Take(8).Select(item => item.Item).ToList();
        var count = pendingByEvent.Sum(item => item.Count) + failedOpenings.Count + postponedStarts.Count;
        return new AdminActionProjection(activeEventIds, items, count, eventActions);
    }

    public async Task<AdminActionProjection> GetAdminActionsSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await GetAdminActionsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AdminActionProjection.Unavailable;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // The shell and directory must remain usable when a read-only
            // action query is temporarily unavailable. The caller renders a
            // retry affordance instead of presenting an empty queue as truth.
            return AdminActionProjection.Unavailable;
        }
    }

    private async Task<IReadOnlyList<BreadcrumbItem>> BuildBreadcrumbs(string page, RouteValueDictionary values, CancellationToken cancellationToken)
    {
        if (page.StartsWith("/Captain/", StringComparison.Ordinal) || page.StartsWith("/Submissions/", StringComparison.Ordinal))
            return await BuildCaptainBreadcrumbs(page, values, cancellationToken);
        if (page.StartsWith("/Events/", StringComparison.Ordinal))
            return await BuildPublicBreadcrumbs(page, values, cancellationToken);
        return [];
    }

    private async Task<AdminEventContext?> GetAdminEventContextAsync(string page, RouteValueDictionary values, string? selectedEventId, CancellationToken cancellationToken)
    {
        Guid eventId;
        if (page == "/Admin/Review/Details" && TryGuid(values, "id", out var submissionId))
        {
            var submissionEventId = await db.Submissions.AsNoTracking()
                .Where(item => item.Id == submissionId)
                .Select(item => (Guid?)item.EventId)
                .SingleOrDefaultAsync(cancellationToken);
            if (submissionEventId is null) return null;
            eventId = submissionEventId.Value;
        }
        else if (page.StartsWith("/Admin/Events/", StringComparison.Ordinal) && page is not "/Admin/Events/Index" and not "/Admin/Events/Create" && TryGuid(values, "id", out var routeEventId))
            eventId = routeEventId;
        else if (page.StartsWith("/Admin/Review/", StringComparison.Ordinal) && Guid.TryParse(selectedEventId, out var queryEventId))
            eventId = queryEventId;
        else
            return null;

        var eventView = await db.Events.AsNoTracking()
            .Where(item => item.Id == eventId && item.HiddenAt == null)
            .Select(item => new { item.Id, item.Name, item.State })
            .SingleOrDefaultAsync(cancellationToken);

        if (eventView is null) return null;
        var blockerCount = await GetAdminEventBlockerCountAsync(eventView.Id, eventView.State, cancellationToken);
        var presentation = AdminEventStatePresentation.For(eventView.State, text);
        return new AdminEventContext(eventView.Id, eventView.Name, eventView.State, presentation.Label, blockerCount);
    }

    private async Task<int> GetAdminEventBlockerCountAsync(Guid eventId, EventState state, CancellationToken cancellationToken)
    {
        var blockers = new HashSet<(string Code, string Description)>();
        void Add(IEnumerable<ReadinessItem>? items)
        {
            if (items is null) return;
            foreach (var item in items) blockers.Add((item.Code, item.Description));
        }

        if (state is EventState.Draft or EventState.SignupOpen)
            Add((await readinessEvaluator.GetSignupReadinessAsync(eventId, SignupOpeningMode.OpenNow, timeProvider.GetUtcNow(), cancellationToken))?.Blockers);
        if (state is EventState.SignupClosed or EventState.Live)
            Add((await eventLifecycle.GetStartReadinessAsync(eventId, cancellationToken))?.Blockers);
        if (state == EventState.AwaitingFinalReview)
            Add((await finalizationService.GetReadinessAsync(eventId, cancellationToken))?.Blockers.Where(item => !item.Resolved).Select(item => new ReadinessItem(item.Key, item.Description)));

        var currentOpening = await db.Events.AsNoTracking()
            .Where(item => item.Id == eventId && item.HiddenAt == null && item.State == EventState.Draft)
            .Select(item => item.SignupOpensAt)
            .SingleOrDefaultAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var failedOpening = currentOpening is null ? null : await db.ScheduledSignupOpeningAttempts.AsNoTracking()
            .Where(item => item.EventId == eventId && !item.Opened && item.ResolvedAt == null && item.ScheduledFor == currentOpening.Value && item.ScheduledFor <= now)
            .OrderByDescending(item => item.AttemptedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (failedOpening is not null)
        {
            if (failedOpening.Details.Count > 0)
                foreach (var detail in failedOpening.Details) blockers.Add(("SCHEDULED_OPENING_FAILED", detail));
            else
                foreach (var code in failedOpening.Blockers) blockers.Add((code, code));
        }

        return blockers.Count;
    }

    private async Task<IReadOnlyList<AdminEventOption>> GetAdminEventOptionsAsync(CancellationToken cancellationToken)
    {
        var events = await db.Events.AsNoTracking()
            .Where(item => item.State != EventState.Discarded && item.HiddenAt == null)
            .OrderBy(item => item.Name)
            .Select(item => new { item.Id, item.Name, item.State })
            .ToListAsync(cancellationToken);
        return events.Select(item =>
        {
            var presentation = AdminEventStatePresentation.For(item.State, text);
            return new AdminEventOption(item.Id, item.Name, item.State, presentation.Label, presentation.Modifier);
        }).ToList();
    }

    private async Task<IReadOnlyList<BreadcrumbItem>> BuildCaptainBreadcrumbs(string page, RouteValueDictionary values, CancellationToken cancellationToken)
    {
        var items = new List<BreadcrumbItem> { new(text["Submissions"], "/Submissions") };
        if (page == "/Captain/Submit" && TryGuid(values, "tileId", out var tileId))
        {
            var tile = await db.BoardTiles.AsNoTracking().Where(item => item.Id == tileId).Select(item => item.NameSnapshot).SingleOrDefaultAsync(cancellationToken);
            if (tile is not null) items.Add(new(tile, null));
            items.Add(new(text["Submit drop"], null));
        }
        else if ((page is "/Captain/Submission" or "/Submissions/Submission") && TryGuid(values, "id", out var submissionId))
        {
            var tile = await (from submission in db.Submissions.AsNoTracking()
                              join boardTile in db.BoardTiles.AsNoTracking() on submission.BoardTileId equals boardTile.Id
                              join bingoEvent in db.Events.AsNoTracking() on submission.EventId equals bingoEvent.Id
                              where submission.Id == submissionId && bingoEvent.HiddenAt == null
                              select boardTile.NameSnapshot).SingleOrDefaultAsync(cancellationToken);
            if (tile is not null) items.Add(new(tile, null));
            items.Add(new(text["Submission"], null));
        }
        return items;
    }

    private async Task<IReadOnlyList<BreadcrumbItem>> BuildPublicBreadcrumbs(string page, RouteValueDictionary values, CancellationToken cancellationToken)
    {
        var slug = values["slug"]?.ToString();
        if (string.IsNullOrWhiteSpace(slug) || page is "/Events/Teams" or "/Events/Confirmation" || page.Contains("/Signup", StringComparison.Ordinal)) return [];
        var eventView = await db.Events.AsNoTracking().Where(item => item.Slug == slug && item.HiddenAt == null).Select(item => new { item.Id, item.Name, item.State }).SingleOrDefaultAsync(cancellationToken);
        if (eventView is null) return [];
        var items = new List<BreadcrumbItem> { new(text["Public boards"], "/") };
        if (page == "/Events/Board") { items.Add(new(eventView.Name, null, StatusLabel(eventView.State), eventView.State.ToString().ToLowerInvariant())); return items; }
        items.Add(new(eventView.Name, $"/Events/{slug}/Board", StatusLabel(eventView.State), eventView.State.ToString().ToLowerInvariant()));
        if (page == "/Events/Teams") { items.Add(new(text["Teams"], null)); return items; }
        var teamSlug = values["teamSlug"]?.ToString();
        if (string.IsNullOrWhiteSpace(teamSlug)) return items;
        var team = await db.Teams.AsNoTracking().Where(item => item.EventId == eventView.Id && item.Slug == teamSlug).Select(item => new { item.Name }).SingleOrDefaultAsync(cancellationToken);
        if (team is null) return items;
        if (page == "/Events/TeamBoard") { items.Add(new(team.Name, null)); return items; }
        items.Add(new(team.Name, $"/Events/{slug}/Board/{teamSlug}"));
        if (page == "/Events/Tile" && TryGuid(values, "tileId", out var tileId))
        {
            var tile = await db.BoardTiles.AsNoTracking().Where(item => item.Id == tileId).Select(item => item.NameSnapshot).SingleOrDefaultAsync(cancellationToken);
            items.Add(new(tile ?? text["Tile"], null));
        }
        return items;
    }

    private string StatusLabel(EventState state) => AdminEventStatePresentation.For(state, text).Label;

    private static string FormatDate(DateTimeOffset value, string timezoneId)
        => DateTimePresentation.Format(value, "dd MMM yyyy, HH:mm", timezoneId, CultureInfo.CurrentCulture);

    private static bool TryGuid(RouteValueDictionary values, string key, out Guid value) => Guid.TryParse(values[key]?.ToString(), out value);

    private sealed record PendingEvidenceAggregate(Guid EventId, int Count, DateTimeOffset OldestSubmittedAt, DateTimeOffset LatestSubmittedAt);
}

public sealed record SharedShellData(IReadOnlyList<BreadcrumbItem> Breadcrumbs, NotificationInbox Notifications, AdminEventContext? AdminEvent, IReadOnlyList<AdminEventOption> AdminEvents, CaptainNavigation? CaptainNavigation, SubmissionNavigation? SubmissionNavigation, CurrentEventNavigation? CurrentEvent);
public sealed record CurrentEventNavigation(Guid EventId, string Slug);
public sealed record CaptainNavigation(Guid EventId, Guid TeamId)
{
    public string Url => $"/Submissions?eventId={EventId}&teamId={TeamId}";
}
public sealed record SubmissionNavigation(Guid EventId, Guid TeamId)
{
    public string Url => $"/Submissions?eventId={EventId}&teamId={TeamId}";
}
public sealed record BreadcrumbItem(string Label, string? Url, string? Status = null, string? StatusClass = null);
public sealed record ShellNotification(Guid Id, string Title, string Detail, string Url, string? TitleLabel = null, string? TitleMetadata = null);
public sealed record AdminEventContext(Guid Id, string Name, EventState State, string StatusLabel, int BlockerCount = 0);
public sealed record AdminEventOption(Guid Id, string Name, EventState State, string StatusLabel, string StatusModifier);
public sealed record NotificationInbox(IReadOnlyList<Guid> EventIds, int Count, string Heading, string EmptyText, string OverviewLabel, string OverviewUrl, IReadOnlyList<ShellNotification> Items, int PersonalCount, int AdminActionCount, string AdminHeading, string AdminEmptyText, string AdminOverviewLabel, string AdminOverviewUrl, IReadOnlyList<ShellNotification> AdminItems, bool AdminActionsUnavailable = false)
{
    public static NotificationInbox Empty { get; } = new([], 0, string.Empty, string.Empty, string.Empty, string.Empty, [], 0, 0, string.Empty, string.Empty, string.Empty, string.Empty, []);
}
public sealed record AdminActionSummary(Guid EventId, int PendingEvidenceCount, bool ScheduledOpeningFailed, bool ScheduledStartPostponed)
{
    public int Count => PendingEvidenceCount + (ScheduledOpeningFailed ? 1 : 0) + (ScheduledStartPostponed ? 1 : 0);
    // Directory categories are deliberately distinct from inbox action units.
    public int CategoryCount => (PendingEvidenceCount > 0 ? 1 : 0) + (ScheduledOpeningFailed ? 1 : 0) + (ScheduledStartPostponed ? 1 : 0);
    public bool HasActions => Count > 0;
}
public sealed record AdminActionProjection(
    IReadOnlyList<Guid> EventIds,
    IReadOnlyList<ShellNotification> Items,
    int Count,
    IReadOnlyDictionary<Guid, AdminActionSummary>? EventActions = null,
    bool IsAvailable = true)
{
    public static AdminActionProjection Unavailable { get; } = new([], [], 0, new Dictionary<Guid, AdminActionSummary>(), false);
    public IReadOnlyDictionary<Guid, AdminActionSummary> ActionsByEvent { get; } = EventActions ?? new Dictionary<Guid, AdminActionSummary>();
    public AdminActionSummary ForEvent(Guid eventId) => ActionsByEvent.TryGetValue(eventId, out var summary)
        ? summary
        : new AdminActionSummary(eventId, 0, false, false);
}

internal static class NotificationPresentation
{
    public static string Title(IStringLocalizer<SharedResource> text, string type) => type switch
    {
        "account.admin_granted" => text["Admin access granted"],
        "account.admin_revoked" => text["Admin access revoked"],
        "account.restored" => text["An administrator restored your account."],
        "event.cancelled" => text["Event cancelled"],
        "event.results_published" => text["Official results published"],
        "evidence.rejected" => text["Evidence rejected"],
        "participant.ownership_transferred" => text["Participant ownership transferred"],
        "participant.withdrawn" => text["Signup withdrawn"],
        "participant.restored" => text["Signup restored"],
        "participant.promoted" => text["Signup promoted to confirmed"],
        "participant.prelive_withdrawn" => text["Participant withdrawn before event start"],
        "participant.prelive_replaced" => text["Replacement confirmed before event start"],
        "participant.live_withdrawn" => text["Live participant withdrawn"],
        "participant.live_replaced" => text["Live replacement confirmed"],
        _ => type
    };

    public static string Detail(IStringLocalizer<SharedResource> text, string type, string detail) => type switch
    {
        "account.admin_granted" => text["An administrator granted your account Admin access."],
        "account.admin_revoked" => text["An administrator removed your Admin access."],
        "account.restored" => text["An administrator restored your account."],
        "event.cancelled" => text["Your event has been cancelled."],
        "event.results_published" => detail,
        "evidence.rejected" => FormatEvidenceRejection(text, detail),
        "participant.live_withdrawn" or "participant.live_replaced" => detail,
        _ => detail
    };

    private static string FormatEvidenceRejection(IStringLocalizer<SharedResource> text, string detail)
    {
        try
        {
            using var document = JsonDocument.Parse(detail);
            var root = document.RootElement;
            var eventName = root.GetProperty("eventName").GetString() ?? text["Unknown event"];
            var tile = root.GetProperty("tile").GetString() ?? text["Unknown tile"];
            var drop = root.TryGetProperty("drop", out var dropValue) && dropValue.ValueKind != JsonValueKind.Null ? $" · {dropValue.GetString()}" : string.Empty;
            var reason = root.GetProperty("reason").GetString() ?? string.Empty;
            return text["Evidence for {0} · {1}{2} was rejected. Reason: {3}", eventName, tile, drop, reason];
        }
        catch (JsonException)
        {
            return text["Evidence rejected."];
        }
    }
}

public sealed record AdminDesignShell(AdminDesignEvent? SelectedEvent, IReadOnlyList<AdminDesignEvent> Events, NotificationInbox Notifications)
{
    public bool ShowEventBreadcrumb { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public string AccountRole { get; init; } = string.Empty;
}
public sealed record AdminDesignEvent(Guid Id, string Name, EventState State, bool Hidden, string Stage, string Tone, string When, string Url);
