using Bingo.Web.Navigation;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages;

[Authorize]
public sealed class NotificationsModel(Bingo.Infrastructure.Persistence.ApplicationDbContext db, TimeProvider time, IStringLocalizer<SharedResource> text) : PageModel
{
    public IReadOnlyList<NotificationView> Notifications { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync(Guid? read, CancellationToken cancellationToken)
    {
        if (read is not null)
            return await MarkReadAsync(read.Value, cancellationToken);
        var accountId = User.GetAccountId();
        if (accountId is null) return Challenge();
        Notifications = await db.PersonalNotifications.AsNoTracking().Where(item => item.RecipientAccountId == accountId).OrderByDescending(item => item.CreatedAt).Select(item => new NotificationView(item.Id, item.Title, item.Detail, item.Route, item.CreatedAt, item.ReadAt)).ToListAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostReadAsync(Guid id, CancellationToken cancellationToken)
    {
        return await MarkReadAsync(id, cancellationToken);
    }

    private async Task<IActionResult> MarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();
        if (accountId is null) return Challenge();
        var notification = await db.PersonalNotifications.SingleOrDefaultAsync(item => item.Id == id && item.RecipientAccountId == accountId, cancellationToken);
        if (notification is null) return NotFound();
        notification.MarkRead(time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return Redirect(string.IsNullOrWhiteSpace(notification.Route) ? "/notifications" : notification.Route);
    }

    public string Title(string type) => type switch { "account.admin_granted" => text["Admin access granted"], "account.admin_revoked" => text["Admin access revoked"], "account.restored" => text["Account restored"], "event.cancelled" => text["Event cancelled"], _ => type };
    public string Detail(string type) => type switch { "account.admin_granted" => text["An administrator granted your account Admin access."], "account.admin_revoked" => text["An administrator removed your Admin access."], "account.restored" => text["An administrator restored your account."], "event.cancelled" => text["Your event has been cancelled."], _ => string.Empty };
    public sealed record NotificationView(Guid Id, string Type, string Detail, string Route, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
}
