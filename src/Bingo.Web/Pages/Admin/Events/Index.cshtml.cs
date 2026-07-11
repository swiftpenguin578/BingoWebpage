using Bingo.Application.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class IndexModel(ApplicationDbContext dbContext) : PageModel
{
    public IReadOnlyList<EventRow> Events { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Events = await dbContext.Events.AsNoTracking().OrderByDescending(item => item.CreatedAt)
            .Select(item => new EventRow(item.Id, item.Name, item.Slug, item.State, item.SignupOpensAt, item.SignupClosesAt, item.ParticipantCap,
                dbContext.EventParticipants.Count(p => p.EventId == item.Id && p.SignupStatus == SignupStatus.Confirmed),
                dbContext.EventParticipants.Count(p => p.EventId == item.Id && p.SignupStatus == SignupStatus.WaitingList)))
            .ToListAsync(cancellationToken);
    }
    public sealed record EventRow(Guid Id, string Name, string Slug, EventState State, DateTimeOffset SignupOpensAt, DateTimeOffset SignupClosesAt, int ParticipantCap, int Confirmed, int Waiting);
}
