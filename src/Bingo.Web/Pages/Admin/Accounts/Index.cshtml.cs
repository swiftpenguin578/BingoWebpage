using Bingo.Application.Access;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Accounts;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class IndexModel(ApplicationDbContext dbContext, TimeProvider timeProvider) : PageModel
{
    public IReadOnlyList<AccountRow> Accounts { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var accounts = await dbContext.Accounts.AsNoTracking().OrderBy(account => account.Username).ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        Accounts = accounts.Select(account => new AccountRow(
            account.Id,
            account.Username,
            account.Role,
            account.EventId,
            account.TeamId,
            account.ExpiresAt,
            account.GetAccessMode(now))).ToList();
    }

    public sealed record AccountRow(
        Guid Id,
        string Username,
        AccountRole Role,
        Guid? EventId,
        Guid? TeamId,
        DateTimeOffset? ExpiresAt,
        AccountAccessMode AccessMode);
}
