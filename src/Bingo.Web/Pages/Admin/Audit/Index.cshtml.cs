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
    [BindProperty(SupportsGet = true), StringLength(100)] public string? Entity { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? EventId { get; set; }
    [BindProperty(SupportsGet = true)] public DateTimeOffset? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTimeOffset? To { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;

    public IReadOnlyList<AuditEntry> Entries { get; private set; } = [];
    public bool HasNextPage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var query = dbContext.AuditEntries.AsNoTracking().Where(entry => entry.EventId == null || dbContext.Events.Any(eventItem => eventItem.Id == entry.EventId && eventItem.HiddenAt == null));
        if (!string.IsNullOrWhiteSpace(Action))
        {
            query = query.Where(entry => entry.Action.Contains(Action));
        }

        if (!string.IsNullOrWhiteSpace(Actor))
        {
            query = query.Where(entry => entry.ActorUsername.Contains(Actor));
        }
        if (!string.IsNullOrWhiteSpace(Entity)) query = query.Where(entry => entry.TargetType.Contains(Entity));
        if (EventId is not null) query = query.Where(entry => entry.EventId == EventId);
        if (From is not null) query = query.Where(entry => entry.OccurredAt >= From.Value);
        if (To is not null) query = query.Where(entry => entry.OccurredAt <= To.Value);

        var rows = await query.OrderByDescending(entry => entry.OccurredAt).ThenByDescending(entry => entry.Id).Skip(Math.Max(0, PageNumber - 1) * 25).Take(26).ToListAsync(cancellationToken);
        HasNextPage = rows.Count > 25; Entries = rows.Take(25).ToList();
    }
}
