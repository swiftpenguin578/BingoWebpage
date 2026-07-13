using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages;

public sealed class IndexModel(ApplicationDbContext db) : PageModel
{
    public IReadOnlyList<PublicEventLink> Events { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Events = await (from bingoEvent in db.Events.AsNoTracking()
                        join board in db.Boards.AsNoTracking() on bingoEvent.Id equals board.EventId
                        where board.State == BoardState.Published &&
                              db.Teams.Any(team => team.EventId == bingoEvent.Id && team.Active && team.FinalizedAt != null)
                        orderby bingoEvent.State == EventState.Live descending,
                                bingoEvent.EventStartsAt descending
                        select new PublicEventLink(
                            bingoEvent.Name, bingoEvent.Slug, bingoEvent.State,
                            bingoEvent.EventStartsAt, bingoEvent.EventEndsAt,
                            board.Rows, board.Columns))
            .ToListAsync(cancellationToken);
    }

    public sealed record PublicEventLink(
        string Name, string Slug, Bingo.Domain.Events.EventState State,
        DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Rows, int Columns);
}
