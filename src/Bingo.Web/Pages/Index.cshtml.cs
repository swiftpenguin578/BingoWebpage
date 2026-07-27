using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Bingo.Web.Pages;

public sealed class IndexModel(ApplicationDbContext db, IHostEnvironment environment) : PageModel
{
    public IReadOnlyList<PublicEventLink> Events { get; private set; } = [];
    public IReadOnlyList<PublicEventLink> PreviousEvents { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var development = environment.IsDevelopment();
        var currentIds = await db.Events.AsNoTracking()
            .Where(x => (x.State == EventState.Live || x.State == EventState.AwaitingFinalReview || x.State == EventState.Finalized)
                && !(development && x.IsDevelopmentFixture))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (currentIds.Count == 1)
            Events = await (from bingoEvent in db.Events.AsNoTracking()
                            join board in db.Boards.AsNoTracking() on bingoEvent.Id equals board.EventId
                            where bingoEvent.Id == currentIds[0] &&
                                  board.State == BoardState.Published &&
                                  db.Teams.Any(team => team.EventId == bingoEvent.Id && team.Active && team.FinalizedAt != null)
                            orderby bingoEvent.State == EventState.Live descending,
                                    bingoEvent.EventStartsAt descending
                            select new PublicEventLink(
                                bingoEvent.Name, bingoEvent.Slug, bingoEvent.State,
                                bingoEvent.EventStartsAt, bingoEvent.EventEndsAt,
                                board.Rows, board.Columns))
                .ToListAsync(cancellationToken);

        PreviousEvents = await (from bingoEvent in db.Events.AsNoTracking()
                                join board in db.Boards.AsNoTracking() on bingoEvent.Id equals board.EventId
                                where bingoEvent.State == EventState.Archived && board.State == BoardState.Published
                                orderby bingoEvent.ArchivedAt descending
                                select new PublicEventLink(bingoEvent.Name, bingoEvent.Slug, bingoEvent.State,
                                    bingoEvent.EventStartsAt, bingoEvent.EventEndsAt, board.Rows, board.Columns))
            .ToListAsync(cancellationToken);
    }

    public sealed record PublicEventLink(
        string Name, string Slug, Bingo.Domain.Events.EventState State,
        DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, int Rows, int Columns);
}
