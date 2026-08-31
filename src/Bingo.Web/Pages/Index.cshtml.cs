using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Bingo.Web.Pages;

public sealed class IndexModel(ApplicationDbContext db, IHostEnvironment environment) : PageModel
{
    private readonly IHostEnvironment _environment = environment;
    public IReadOnlyList<PublicEventLink> Events { get; private set; } = [];
    public IReadOnlyList<PublicEventLink> PreviousEvents { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var candidates = await (from bingoEvent in db.Events.AsNoTracking()
                                join board in db.Boards.AsNoTracking() on bingoEvent.Id equals board.EventId into boards
                                from board in boards.DefaultIfEmpty()
                                where
                                      bingoEvent.HiddenAt == null && bingoEvent.State != EventState.Discarded && bingoEvent.State != EventState.Cancelled && bingoEvent.State != EventState.Archived &&
                                      ((bingoEvent.State == EventState.SignupOpen && bingoEvent.FirstPublicAt != null) ||
                                       db.DraftPublicationCycles.Any(cycle => cycle.SupersededAt == null && db.DraftSessions.Any(draft => draft.Id == cycle.DraftSessionId && draft.EventId == bingoEvent.Id)) ||
                                       db.Boards.Any(candidate => candidate.EventId == bingoEvent.Id && candidate.State == BoardState.Published))
                                orderby bingoEvent.State == EventState.Live descending, bingoEvent.EventStartsAt descending
                                select new
                                {
                                    bingoEvent,
                                    RosterPublished = db.DraftPublicationCycles.Any(cycle => cycle.SupersededAt == null && db.DraftSessions.Any(draft => draft.Id == cycle.DraftSessionId && draft.EventId == bingoEvent.Id)),
                                    BoardPublished = board != null && board.State == BoardState.Published,
                                    Rows = board == null ? (int?)null : board.Rows,
                                    Columns = board == null ? (int?)null : board.Columns
                                })
            .ToListAsync(cancellationToken);
        Events = candidates.Select(value =>
        {
            var route = EventDestinationPolicy.From(value.bingoEvent, rosterExists: value.RosterPublished, boardPublished: value.BoardPublished);
            return new PublicEventLink(value.bingoEvent.Name, value.bingoEvent.Slug, value.bingoEvent.State, value.bingoEvent.EventStartsAt, value.bingoEvent.EventEndsAt, value.Rows, value.Columns, EventDestinationPolicy.PublicOverview(route), EventDisplayPhaseProjection.From(new(value.bingoEvent.State, value.RosterPublished || value.BoardPublished, value.BoardPublished)), value.bingoEvent.Timezone);
        }).ToList();

        PreviousEvents = await (from bingoEvent in db.Events.AsNoTracking()
                                join board in db.Boards.AsNoTracking() on bingoEvent.Id equals board.EventId
                                where bingoEvent.HiddenAt == null && bingoEvent.State == EventState.Archived && board.State == BoardState.Published
                                orderby bingoEvent.ArchivedAt descending
                                select new PublicEventLink(bingoEvent.Name, bingoEvent.Slug, bingoEvent.State,
                                    bingoEvent.EventStartsAt, bingoEvent.EventEndsAt, board.Rows, board.Columns, EventDestination.Board, EventDisplayPhase.Lifecycle, bingoEvent.Timezone))
            .ToListAsync(cancellationToken);
    }

    public sealed record PublicEventLink(
        string Name, string Slug, Bingo.Domain.Events.EventState State,
        DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, int? Rows, int? Columns, EventDestination Destination, EventDisplayPhase DisplayPhase, string Timezone = DateTimePresentation.DefaultTimezoneId);
}
