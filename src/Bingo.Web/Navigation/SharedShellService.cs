using System.Globalization;
using System.Security.Claims;
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
        if (Guid.TryParse(user.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier), out var accountId))
        {
            var unread = db.PersonalNotifications.AsNoTracking().Where(item => item.RecipientAccountId == accountId && item.ReadAt == null);
            var unreadCount = await unread.CountAsync(cancellationToken);
            if (unreadCount > 0)
            {
                var personal = await unread.OrderByDescending(item => item.CreatedAt).Take(6).ToListAsync(cancellationToken);
                return new NotificationInbox([], unreadCount, text["Notifications"], text["No notifications."], text["Notifications"], "/notifications", personal.Select(item => new ShellNotification(item.Id, NotificationTitle(item.Title), NotificationDetail(item.Title), $"/notifications?read={item.Id}")).ToList());
            }
        }
        if (user.IsInRole("Admin") || user.IsInRole("SuperAdmin")) return await GetAdminNotifications(cancellationToken);
        if (user.IsInRole("Captain")) return await GetCaptainNotifications(user, cancellationToken);
        return new NotificationInbox([], 0, text["Notifications"], text["No notifications."], text["Notifications"], "/notifications", []);
    }

    private string NotificationTitle(string type) => type switch
    {
        "account.admin_granted" => text["Admin access granted"],
        "account.admin_revoked" => text["Admin access revoked"],
        "account.restored" => text["Account restored"],
        _ => text["Notifications"]
    };

    private string NotificationDetail(string type) => type switch
    {
        "account.admin_granted" => text["An administrator granted your account Admin access."],
        "account.admin_revoked" => text["An administrator removed your Admin access."],
        "account.restored" => text["An administrator restored your account."],
        _ => string.Empty
    };

    private async Task<NotificationInbox> GetAdminNotifications(CancellationToken cancellationToken)
    {
        var activeEvents = await db.Events.AsNoTracking()
            .Where(item => item.State == EventState.Live || item.State == EventState.AwaitingFinalReview)
            .OrderByDescending(item => item.EventStartsAt)
            .Select(item => new { item.Id, item.Name, item.Timezone })
            .ToListAsync(cancellationToken);
        if (activeEvents.Count == 0)
        {
            return new NotificationInbox([], 0, text["Notifications"], text["No active event needs attention."], text["Open evidence review"], "/Admin/Review", []);
        }

        var activeEventIds = activeEvents.Select(item => item.Id).ToList();
        var eventMap = activeEvents.ToDictionary(item => item.Id);
        var query = from submission in db.Submissions.AsNoTracking()
                    join team in db.Teams.AsNoTracking() on submission.TeamId equals team.Id
                    join tile in db.BoardTiles.AsNoTracking() on submission.BoardTileId equals tile.Id
                    join player in db.EventParticipants.AsNoTracking() on submission.CreditedParticipantId equals player.Id
                    where activeEventIds.Contains(submission.EventId) && submission.Status == SubmissionStatus.Pending
                    orderby submission.SubmittedAt descending
                    select new { submission.Id, submission.EventId, submission.SubmittedAt, Team = team.Name, Tile = tile.NameSnapshot, Player = player.PrimaryAccountName };
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.Take(6).ToListAsync(cancellationToken);
        var items = rows.Select(item => new ShellNotification(
            item.Id,
            $"{item.Team} · {item.Tile}",
            text["{0} · {1} · submitted {2}", eventMap[item.EventId].Name, item.Player, FormatDate(item.SubmittedAt, eventMap[item.EventId].Timezone)],
            $"/Admin/Review/Details/{item.Id}")).ToList();
        return new NotificationInbox(activeEventIds, count, text["Tile submissions"], text["No tile submissions need review."], text["View all submissions"], "/Admin/Review", items);
    }

    private async Task<NotificationInbox> GetCaptainNotifications(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var eventId = user.GetEventId();
        var teamId = user.GetTeamId();
        if (eventId is null || teamId is null) return NotificationInbox.Empty;
        var eventName = await db.Events.AsNoTracking().Where(item => item.Id == eventId).Select(item => item.Name).SingleOrDefaultAsync(cancellationToken);
        if (eventName is null) return NotificationInbox.Empty;

        var query = from submission in db.Submissions.AsNoTracking()
                    join tile in db.BoardTiles.AsNoTracking() on submission.BoardTileId equals tile.Id
                    where submission.EventId == eventId && submission.TeamId == teamId && submission.Status == SubmissionStatus.ChangesRequested
                    orderby submission.ReviewedAt descending, submission.SubmittedAt descending
                    select new { submission.Id, Tile = tile.NameSnapshot, submission.CurrentReviewerNote };
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.Take(6).ToListAsync(cancellationToken);
        var items = rows.Select(item => new ShellNotification(
            item.Id,
            item.Tile,
            string.IsNullOrWhiteSpace(item.CurrentReviewerNote) ? text["Changes requested"] : item.CurrentReviewerNote,
            $"/Captain/Submissions/{item.Id}")).ToList();
        return new NotificationInbox([eventId.Value], count, text["Submissions to correct"], text["No submissions need changes."], text["Open team board"], "/Captain", items);
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
            ? await db.EventParticipants.AsNoTracking().Where(item => item.Id == participantId).Select(item => item.PrimaryAccountName).SingleOrDefaultAsync(cancellationToken) ?? text["Player"]
            : PageLabel(page);
        items.Add(new(current, null));
        return items;
    }

    private async Task<IReadOnlyList<BreadcrumbItem>> BuildReviewBreadcrumbs(string page, RouteValueDictionary values, CancellationToken cancellationToken)
    {
        var items = new List<BreadcrumbItem> { AdminRoot(), new(text["Evidence review"], "/Admin/Review") };
        if (page == "/Admin/Review/Submit") { items.Add(new(text["Admin submission"], null)); return items; }
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
        "/Admin/Events/Csv" => text["CSV import"],
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
public sealed record NotificationInbox(IReadOnlyList<Guid> EventIds, int Count, string Heading, string EmptyText, string OverviewLabel, string OverviewUrl, IReadOnlyList<ShellNotification> Items)
{
    public static NotificationInbox Empty { get; } = new([], 0, string.Empty, string.Empty, string.Empty, string.Empty, []);
}
