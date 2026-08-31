using Bingo.Web.Navigation;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages;

[Authorize]
public sealed class NotificationsModel(Bingo.Infrastructure.Persistence.ApplicationDbContext db, TimeProvider time, IStringLocalizer<SharedResource> text, SharedShellService? shell = null) : PageModel
{
    public IReadOnlyList<NotificationView> Notifications { get; private set; } = [];
    public AdminActionProjection AdminActions { get; private set; } = new([], [], 0);
    public async Task<IActionResult> OnGetAsync(Guid? read, CancellationToken cancellationToken)
    {
        if (read is not null)
            return await MarkReadAsync(read.Value, cancellationToken);
        var accountId = User.GetAccountId();
        if (accountId is null) return Challenge();
        Notifications = await db.PersonalNotifications.AsNoTracking()
            .Where(item => item.RecipientAccountId == accountId && (item.EventId == null || db.Events.Any(eventItem => eventItem.Id == item.EventId && eventItem.HiddenAt == null)))
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new NotificationView(item.Id, item.Title, item.Detail, item.Route, item.CreatedAt, item.ReadAt)).ToListAsync(cancellationToken);
        if (shell is not null && (User.IsInRole("Admin") || User.IsInRole("SuperAdmin"))) AdminActions = await shell.GetAdminActionsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostReadAsync(Guid id, CancellationToken cancellationToken)
    {
        return await MarkReadAsync(id, cancellationToken);
    }

    public async Task<IActionResult> OnPostMarkAllAsReadAsync(CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();
        if (accountId is null) return Challenge();
        var now = time.GetUtcNow();
        var notifications = await db.PersonalNotifications.Where(item => item.RecipientAccountId == accountId && item.ReadAt == null && (item.EventId == null || db.Events.Any(eventItem => eventItem.Id == item.EventId && eventItem.HiddenAt == null))).ToListAsync(cancellationToken);
        foreach (var notification in notifications)
            notification.MarkRead(now);
        await db.SaveChangesAsync(cancellationToken);
        return Redirect("/notifications");
    }

    private async Task<IActionResult> MarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();
        if (accountId is null) return Challenge();
        var notification = await db.PersonalNotifications.SingleOrDefaultAsync(item => item.Id == id && item.RecipientAccountId == accountId && (item.EventId == null || db.Events.Any(eventItem => eventItem.Id == item.EventId && eventItem.HiddenAt == null)), cancellationToken);
        if (notification is null) return NotFound();
        notification.MarkRead(time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return Redirect(string.IsNullOrWhiteSpace(notification.Route) ? "/notifications" : notification.Route);
    }

    public string Title(string type) => NotificationPresentation.Title(text, type);
    public string RelativeAge(DateTimeOffset createdAt)
    {
        var age = time.GetUtcNow() - createdAt;
        if (age < TimeSpan.FromMinutes(1)) return text["just now"];
        if (age < TimeSpan.FromHours(1)) return text[age.TotalMinutes >= 2 ? "{0} minutes ago" : "1 minute ago", Math.Max(1, (int)age.TotalMinutes)];
        if (age < TimeSpan.FromDays(1)) return text[age.TotalHours >= 2 ? "{0} hours ago" : "1 hour ago", Math.Max(1, (int)age.TotalHours)];
        return text[age.TotalDays >= 2 ? "{0} days ago" : "1 day ago", Math.Max(1, (int)age.TotalDays)];
    }
    public string Detail(string type, string detail) => NotificationPresentation.Detail(text, type, detail);
    public sealed record NotificationView(Guid Id, string Type, string Detail, string Route, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
}
