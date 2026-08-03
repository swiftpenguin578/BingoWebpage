using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Navigation;

public sealed class SharedShellService(ApplicationDbContext db, IStringLocalizer<SharedResource> text)
{
    public async Task<SharedShellData> GetAsync(ClaimsPrincipal user, RouteValueDictionary routeValues, CancellationToken cancellationToken)
    {
        var page = routeValues["page"]?.ToString() ?? string.Empty;
        var breadcrumbs = await BuildBreadcrumbs(page, routeValues, cancellationToken);
        var notifications = user.Identity?.IsAuthenticated == true
            ? await GetNotificationsAsync(user, cancellationToken)
            : NotificationInbox.Empty;
        return new SharedShellData(breadcrumbs, notifications);
    }

    public async Task<NotificationInbox> GetNotificationsAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var personalItems = new List<ShellNotification>();
        if (Guid.TryParse(user.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier), out var accountId))
        {
            var unread = db.PersonalNotifications.AsNoTracking().Where(item => item.RecipientAccountId == accountId && item.ReadAt == null);
            var personal = await unread.OrderByDescending(item => item.CreatedAt).Take(6).ToListAsync(cancellationToken);
            personalItems = personal.Select(item => new ShellNotification(item.Id, NotificationTitle(item.Title), NotificationDetail(item.Title, item.Detail), $"/notifications?read={item.Id}")).ToList();
            var personalCount = await unread.CountAsync(cancellationToken);
            var adminActions = user.IsInRole("Admin") || user.IsInRole("SuperAdmin")
                ? await GetAdminActionsAsync(cancellationToken)
                : new AdminActionProjection([], [], 0);
            var eventIds = await db.Events.AsNoTracking().Where(item => item.State == EventState.Live || item.State == EventState.AwaitingFinalReview).Select(item => item.Id).ToListAsync(cancellationToken);
            return new NotificationInbox(eventIds, personalCount + adminActions.Count, text["Notifications"], text["No notifications."], text["Notifications"], "/notifications", personalItems,
                personalCount, adminActions.Count, text["Admin actions"], text["No unresolved Admin actions."], text["Open Admin actions"], "/Admin", adminActions.Items);
        }
        var anonymousAdminActions = user.IsInRole("Admin") || user.IsInRole("SuperAdmin")
            ? await GetAdminActionsAsync(cancellationToken)
            : new AdminActionProjection([], [], 0);
        var anonymousEventIds = await db.Events.AsNoTracking().Where(item => item.State == EventState.Live || item.State == EventState.AwaitingFinalReview).Select(item => item.Id).ToListAsync(cancellationToken);
        return new NotificationInbox(anonymousEventIds, anonymousAdminActions.Count, text["Notifications"], text["No notifications."], text["Notifications"], "/notifications", personalItems,
            0, anonymousAdminActions.Count, text["Admin actions"], text["No unresolved Admin actions."], text["Open Admin actions"], "/Admin", anonymousAdminActions.Items);
    }

    private string NotificationTitle(string type) => type switch
    {
        "account.admin_granted" => text["Admin access granted"],
        "account.admin_revoked" => text["Admin access revoked"],
        "account.restored" => text["An administrator restored your account."],
        "event.cancelled" => text["Event cancelled"],
        "event.results_published" => text["Official results published"],
        "evidence.rejected" => text["Evidence rejected"],
        "participant.live_withdrawn" => text["Live participant withdrawn"],
        "participant.live_replaced" => text["Live replacement confirmed"],
        _ => type
    };

    private string NotificationDetail(string type, string detail) => type switch
    {
        "account.admin_granted" => text["An administrator granted your account Admin access."],
        "account.admin_revoked" => text["An administrator removed your Admin access."],
        "account.restored" => text["An administrator restored your account."],
        "event.cancelled" => text["Your event has been cancelled."],
        "event.results_published" => detail,
        "evidence.rejected" => FormatEvidenceRejection(detail),
        "participant.live_withdrawn" or "participant.live_replaced" => detail,
        _ => detail
    };

    private string FormatEvidenceRejection(string detail)
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

    public async Task<AdminActionProjection> GetAdminActionsAsync(CancellationToken cancellationToken)
    {
        var activeEvents = await db.Events.AsNoTracking().Where(item => item.State == EventState.Draft || item.State == EventState.SignupOpen || item.State == EventState.SignupClosed || item.State == EventState.Live || item.State == EventState.AwaitingFinalReview).Select(item => new { item.Id, item.Name, item.Timezone }).ToListAsync(cancellationToken);
        var activeEventIds = activeEvents.Select(item => item.Id).ToList();
        var eventMap = activeEvents.ToDictionary(item => item.Id);
        var items = new List<ShellNotification>();
        var pending = from submission in db.Submissions.AsNoTracking()
                      join team in db.Teams.AsNoTracking() on submission.TeamId equals team.Id
                      join tile in db.BoardTiles.AsNoTracking() on submission.BoardTileId equals tile.Id
                      where activeEventIds.Contains(submission.EventId) && submission.Status == SubmissionStatus.Pending
                      orderby submission.SubmittedAt descending
                      select new { submission.Id, submission.EventId, submission.SubmittedAt, Team = team.Name, Tile = tile.NameSnapshot, Player = submission.CreditedCharacterName };
        var pendingRows = await pending.Take(6).ToListAsync(cancellationToken);
        var pendingCount = await pending.CountAsync(cancellationToken);
        items.AddRange(pendingRows.Select(item => new ShellNotification(
            item.Id,
            $"Evidence review · {item.Team} · {item.Tile}",
            text["{0} · {1} · submitted {2}", eventMap[item.EventId].Name, item.Player, FormatDate(item.SubmittedAt, eventMap[item.EventId].Timezone)],
            $"/Admin/Review/Details/{item.Id}")));

        var followups = await (from followUp in db.WaitingListPromotionFollowUps.AsNoTracking()
                               join participant in db.EventParticipants.AsNoTracking() on followUp.PromotedParticipantId equals participant.Id
                               where followUp.CompletedAt == null && activeEventIds.Contains(followUp.EventId)
                               select new { followUp.Id, followUp.EventId, ParticipantId = participant.Id }).Take(6).ToListAsync(cancellationToken);
        var followupCount = await db.WaitingListPromotionFollowUps.CountAsync(x => x.CompletedAt == null && activeEventIds.Contains(x.EventId), cancellationToken);
        items.AddRange(followups.Select(x => new ShellNotification(x.Id, "Waiting-list follow-up", "Contact the promoted participant, then mark the follow-up complete.", $"/Admin/Events/Participant/{x.EventId}/Participants/{x.ParticipantId}")));

        var postponed = await db.ScheduledEventStartAttempts.AsNoTracking().Where(x => activeEventIds.Contains(x.EventId) && !x.Started && x.ResolvedAt == null && x.ScheduledFor <= DateTimeOffset.UtcNow).OrderByDescending(x => x.AttemptedAt).Take(6).ToListAsync(cancellationToken);
        var postponedCount = await db.ScheduledEventStartAttempts.CountAsync(x => activeEventIds.Contains(x.EventId) && !x.Started && x.ResolvedAt == null && x.ScheduledFor <= DateTimeOffset.UtcNow, cancellationToken);
        items.AddRange(postponed.Select(x => new ShellNotification(x.Id, "Postponed start", "Resolve the scheduled start blockers.", $"/Admin/Events/Manage/{x.EventId}")));

        var vacancies = await (from membership in db.TeamMemberships.AsNoTracking()
                               join participant in db.EventParticipants.AsNoTracking() on membership.EventParticipantId equals participant.Id
                               join ev in db.Events.AsNoTracking() on participant.EventId equals ev.Id
                               where (ev.State == EventState.Live || ev.State == EventState.AwaitingFinalReview) && participant.SignupStatus == Bingo.Domain.Signups.SignupStatus.Withdrawn && !db.TeamMemberships.Any(replacement => replacement.ReplacesMembershipId == membership.Id)
                               select new { membership.Id, EventId = ev.Id, ParticipantId = participant.Id }).Take(6).ToListAsync(cancellationToken);
        var vacancyCount = await (from membership in db.TeamMemberships.AsNoTracking()
                                  join participant in db.EventParticipants.AsNoTracking() on membership.EventParticipantId equals participant.Id
                                  join ev in db.Events.AsNoTracking() on participant.EventId equals ev.Id
                                  where (ev.State == EventState.Live || ev.State == EventState.AwaitingFinalReview) && participant.SignupStatus == Bingo.Domain.Signups.SignupStatus.Withdrawn && !db.TeamMemberships.Any(replacement => replacement.ReplacesMembershipId == membership.Id)
                                  select membership.Id).CountAsync(cancellationToken);
        items.AddRange(vacancies.Select(x => new ShellNotification(x.Id, "Open vacancy", "Review the open team vacancy and choose whether to replace it.", $"/Admin/Events/Participant/{x.EventId}/Participants/{x.ParticipantId}")));

        var missingCaptains = await (from team in db.Teams.AsNoTracking()
                                     join ev in db.Events.AsNoTracking() on team.EventId equals ev.Id
                                     where team.Active && (ev.State == EventState.Live || ev.State == EventState.AwaitingFinalReview) &&
                                           !db.TeamMemberships.Any(membership => membership.TeamId == team.Id && membership.LeftAt == null && membership.Role == Bingo.Domain.Teams.TeamMembershipRole.Captain &&
                                               db.EventParticipants.Any(participant => participant.Id == membership.EventParticipantId && participant.AccountId != null && participant.EventId == ev.Id &&
                                                   db.Accounts.Any(account => account.Id == participant.AccountId && account.Active && account.AccountType == Bingo.Domain.Access.AccountType.WebsiteAccount))) &&
                                           !db.AccountEventAccesses.Any(access => access.EventId == ev.Id && access.TeamId == team.Id && access.Enabled &&
                                               db.Accounts.Any(account => account.Id == access.AccountId && account.Active && account.AccountType == Bingo.Domain.Access.AccountType.EmergencyCaptain))
                                     select new { team.Id, team.EventId }).Take(6).ToListAsync(cancellationToken);
        var missingCaptainCount = await (from team in db.Teams.AsNoTracking()
                                         join ev in db.Events.AsNoTracking() on team.EventId equals ev.Id
                                         where team.Active && (ev.State == EventState.Live || ev.State == EventState.AwaitingFinalReview) &&
                                               !db.TeamMemberships.Any(membership => membership.TeamId == team.Id && membership.LeftAt == null && membership.Role == Bingo.Domain.Teams.TeamMembershipRole.Captain &&
                                                   db.EventParticipants.Any(participant => participant.Id == membership.EventParticipantId && participant.AccountId != null && participant.EventId == ev.Id &&
                                                       db.Accounts.Any(account => account.Id == participant.AccountId && account.Active && account.AccountType == Bingo.Domain.Access.AccountType.WebsiteAccount))) &&
                                               !db.AccountEventAccesses.Any(access => access.EventId == ev.Id && access.TeamId == team.Id && access.Enabled &&
                                                   db.Accounts.Any(account => account.Id == access.AccountId && account.Active && account.AccountType == Bingo.Domain.Access.AccountType.EmergencyCaptain))
                                         select team.Id).CountAsync(cancellationToken);
        items.AddRange(missingCaptains.Select(x => new ShellNotification(x.Id, "Missing Captain", "Assign a current Captain through the event roster.", $"/Admin/Events/Manage/{x.EventId}")));

        return new AdminActionProjection(activeEventIds, items.Take(8).ToList(), pendingCount + followupCount + postponedCount + vacancyCount + missingCaptainCount);
    }

    private async Task<IReadOnlyList<BreadcrumbItem>> BuildBreadcrumbs(string page, RouteValueDictionary values, CancellationToken cancellationToken)
    {
        if (page.StartsWith("/Admin/Events/", StringComparison.Ordinal) && page != "/Admin/Events/Index")
            return await BuildAdminEventBreadcrumbs(page, values, cancellationToken);
        if (page.StartsWith("/Admin/Review/", StringComparison.Ordinal) && page != "/Admin/Review/Index")
            return await BuildReviewBreadcrumbs(page, values, cancellationToken);
        if (page.StartsWith("/Admin/Catalogue/", StringComparison.Ordinal) && page != "/Admin/Catalogue/Index")
            return [AdminRoot(), new(text["OSRS catalogue"], "/Admin/Catalogue"), new(PageLabel(page), null)];
        if (page.StartsWith("/Admin/Accounts/", StringComparison.Ordinal) && page != "/Admin/Accounts/Index")
            return await BuildAccountBreadcrumbs(page, values, cancellationToken);
        if (page.StartsWith("/Captain/", StringComparison.Ordinal) && page != "/Captain/Index")
            return await BuildCaptainBreadcrumbs(page, values, cancellationToken);
        if (page.StartsWith("/Events/", StringComparison.Ordinal))
            return await BuildPublicBreadcrumbs(page, values, cancellationToken);
        return [];
    }

    private async Task<IReadOnlyList<BreadcrumbItem>> BuildAdminEventBreadcrumbs(string page, RouteValueDictionary values, CancellationToken cancellationToken)
    {
        var items = new List<BreadcrumbItem> { AdminRoot(), new(text["Events"], "/Admin/Events") };
        if (page == "/Admin/Events/Create") { items.Add(new(text["Create event"], null)); return items; }
        if (!TryGuid(values, "id", out var eventId)) return items;
        var eventView = await db.Events.AsNoTracking().Where(item => item.Id == eventId).Select(item => new { item.Name, item.State }).SingleOrDefaultAsync(cancellationToken);
        if (eventView is null) return items;
        if (page == "/Admin/Events/Manage")
        {
            items.Add(new(eventView.Name, null, StatusLabel(eventView.State), eventView.State.ToString().ToLowerInvariant()));
            return items;
        }
        items.Add(new(eventView.Name, $"/Admin/Events/Manage/{eventId}", StatusLabel(eventView.State), eventView.State.ToString().ToLowerInvariant()));
        var current = page == "/Admin/Events/Participant" && TryGuid(values, "participantId", out var participantId)
            ? await db.PrimaryCharacters().AsNoTracking().Where(item => item.ParticipantId == participantId).Select(item => item.Name).SingleOrDefaultAsync(cancellationToken) ?? text["Player"]
            : PageLabel(page);
        items.Add(new(current, null));
        return items;
    }

    private async Task<IReadOnlyList<BreadcrumbItem>> BuildReviewBreadcrumbs(string page, RouteValueDictionary values, CancellationToken cancellationToken)
    {
        var items = new List<BreadcrumbItem> { AdminRoot(), new(text["Evidence review"], "/Admin/Review") };
        if (TryGuid(values, "id", out var submissionId))
        {
            var tile = await (from submission in db.Submissions.AsNoTracking()
                              join boardTile in db.BoardTiles.AsNoTracking() on submission.BoardTileId equals boardTile.Id
                              where submission.Id == submissionId
                              select boardTile.NameSnapshot).SingleOrDefaultAsync(cancellationToken);
            items.Add(new(tile ?? text["Submission"], null));
        }
        return items;
    }

    private async Task<IReadOnlyList<BreadcrumbItem>> BuildAccountBreadcrumbs(string page, RouteValueDictionary values, CancellationToken cancellationToken)
    {
        var items = new List<BreadcrumbItem> { AdminRoot(), new(text["Accounts"], "/Admin/Accounts") };
        if (page == "/Admin/Accounts/Create") { items.Add(new(text["Create account"], null)); return items; }
        if (TryGuid(values, "id", out var accountId))
        {
            var username = await db.Accounts.AsNoTracking().Where(item => item.Id == accountId).Select(item => item.LoginName).SingleOrDefaultAsync(cancellationToken);
            items.Add(new(username ?? text["Account"], null));
        }
        return items;
    }

    private async Task<IReadOnlyList<BreadcrumbItem>> BuildCaptainBreadcrumbs(string page, RouteValueDictionary values, CancellationToken cancellationToken)
    {
        var items = new List<BreadcrumbItem> { new(text["Team board"], "/Captain") };
        if (page == "/Captain/Submit" && TryGuid(values, "tileId", out var tileId))
        {
            var tile = await db.BoardTiles.AsNoTracking().Where(item => item.Id == tileId).Select(item => item.NameSnapshot).SingleOrDefaultAsync(cancellationToken);
            if (tile is not null) items.Add(new(tile, null));
            items.Add(new(text["Submit drop"], null));
        }
        else if (page == "/Captain/Submission" && TryGuid(values, "id", out var submissionId))
        {
            var tile = await (from submission in db.Submissions.AsNoTracking()
                              join boardTile in db.BoardTiles.AsNoTracking() on submission.BoardTileId equals boardTile.Id
                              where submission.Id == submissionId
                              select boardTile.NameSnapshot).SingleOrDefaultAsync(cancellationToken);
            if (tile is not null) items.Add(new(tile, null));
            items.Add(new(text["Submission"], null));
        }
        return items;
    }

    private async Task<IReadOnlyList<BreadcrumbItem>> BuildPublicBreadcrumbs(string page, RouteValueDictionary values, CancellationToken cancellationToken)
    {
        var slug = values["slug"]?.ToString();
        if (string.IsNullOrWhiteSpace(slug) || page.Contains("/Signup", StringComparison.Ordinal)) return [];
        var eventView = await db.Events.AsNoTracking().Where(item => item.Slug == slug).Select(item => new { item.Id, item.Name, item.State }).SingleOrDefaultAsync(cancellationToken);
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

    private BreadcrumbItem AdminRoot() => new(text["Admin tools"], "/Admin");

    private string PageLabel(string page) => page switch
    {
        "/Admin/Events/Board" => text["Board editor"],
        "/Admin/Events/Draft" => text["Teams and draft"],
        "/Admin/Events/Questions" => text["Signup form"],
        "/Admin/Events/Finalize" => text["Finish event"],
        _ => text["Current page"]
    };

    private string StatusLabel(EventState state) => state switch
    {
        EventState.Draft => text["Setup"],
        EventState.SignupOpen => text["Signups open"],
        EventState.SignupClosed => text["Signups closed"],
        EventState.Live => text["Live"],
        EventState.AwaitingFinalReview => text["Final review"],
        EventState.Finalized => text["Finished"],
        EventState.Archived => text["Archived"],
        _ => state.ToString()
    };

    private static string FormatDate(DateTimeOffset value, string timezoneId)
    {
        try
        {
            return TimeZoneInfo.ConvertTime(value, TimeZoneInfo.FindSystemTimeZoneById(timezoneId)).ToString("g", CultureInfo.CurrentCulture);
        }
        catch (TimeZoneNotFoundException)
        {
            return value.ToString("g", CultureInfo.CurrentCulture);
        }
        catch (InvalidTimeZoneException)
        {
            return value.ToString("g", CultureInfo.CurrentCulture);
        }
    }

    private static bool TryGuid(RouteValueDictionary values, string key, out Guid value) => Guid.TryParse(values[key]?.ToString(), out value);
}

public sealed record SharedShellData(IReadOnlyList<BreadcrumbItem> Breadcrumbs, NotificationInbox Notifications);
public sealed record BreadcrumbItem(string Label, string? Url, string? Status = null, string? StatusClass = null);
public sealed record ShellNotification(Guid Id, string Title, string Detail, string Url);
public sealed record NotificationInbox(IReadOnlyList<Guid> EventIds, int Count, string Heading, string EmptyText, string OverviewLabel, string OverviewUrl, IReadOnlyList<ShellNotification> Items, int PersonalCount, int AdminActionCount, string AdminHeading, string AdminEmptyText, string AdminOverviewLabel, string AdminOverviewUrl, IReadOnlyList<ShellNotification> AdminItems)
{
    public static NotificationInbox Empty { get; } = new([], 0, string.Empty, string.Empty, string.Empty, string.Empty, [], 0, 0, string.Empty, string.Empty, string.Empty, string.Empty, []);
}
public sealed record AdminActionProjection(IReadOnlyList<Guid> EventIds, IReadOnlyList<ShellNotification> Items, int Count);
