using System.ComponentModel.DataAnnotations;
using Bingo.Application.Access;
using Bingo.Domain.Auditing;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Audit;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class IndexModel(ApplicationDbContext dbContext) : PageModel
{
    [BindProperty(SupportsGet = true), StringLength(100)]
    public string? Action { get; set; }

    [BindProperty(SupportsGet = true), StringLength(100)]
    public string? Actor { get; set; }

    public IReadOnlyList<AuditEntry> Entries { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var query = dbContext.AuditEntries.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Action))
        {
            query = query.Where(entry => entry.Action.Contains(Action));
        }

        if (!string.IsNullOrWhiteSpace(Actor))
        {
            query = query.Where(entry => entry.ActorUsername.Contains(Actor));
        }

        Entries = await query.OrderByDescending(entry => entry.OccurredAt).Take(250).ToListAsync(cancellationToken);
    }
}
