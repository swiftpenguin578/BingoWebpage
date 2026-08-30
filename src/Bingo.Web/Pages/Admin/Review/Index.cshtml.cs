using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Review;

public sealed class IndexModel(ApplicationDbContext db) : PageModel
{
    public string EventName { get; private set; } = string.Empty; public string EventTimezone { get; private set; } = DateTimePresentation.DefaultTimezoneId; public Guid? EventId { get; private set; }
    public Guid? TeamId { get; private set; }
    public Guid? TileId { get; private set; }
    public SubmissionStatus? Status { get; private set; }
    public IReadOnlyList<EventOption> Events { get; private set; } = []; public IReadOnlyList<TeamOption> Teams { get; private set; } = []; public IReadOnlyList<TileOption> Tiles { get; private set; } = []; public IReadOnlyList<Row> Rows { get; private set; } = [];
    public async Task OnGetAsync(Guid? eventId, Guid? teamId, Guid? tileId, SubmissionStatus? status, CancellationToken ct)
    {
        TeamId = teamId; TileId = tileId; Status = status;
        Events = await db.Events.AsNoTracking().Where(x => x.State == EventState.Live || x.State == EventState.AwaitingFinalReview).OrderByDescending(x => x.EventStartsAt).Select(x => new EventOption(x.Id, x.Name)).ToListAsync(ct);
        var selectedId = eventId ?? (Events.Count == 1 ? Events[0].Id : null); if (selectedId is null) return;
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == selectedId && (x.State == EventState.Live || x.State == EventState.AwaitingFinalReview), ct); if (ev is null) return; EventId = ev.Id; EventName = ev.Name; EventTimezone = ev.Timezone; Teams = await db.Teams.AsNoTracking().Where(x => x.EventId == ev.Id && x.Active).OrderBy(x => x.Name).Select(x => new TeamOption(x.Id, x.Name)).ToListAsync(ct); var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == ev.Id, ct); if (board is not null) Tiles = await db.BoardTiles.AsNoTracking().Where(x => x.BoardId == board.Id).OrderBy(x => x.RowIndex).ThenBy(x => x.ColumnIndex).Select(x => new TileOption(x.Id, x.NameSnapshot)).ToListAsync(ct);
        var query = from s in db.Submissions.AsNoTracking() join team in db.Teams on s.TeamId equals team.Id join tile in db.BoardTiles on s.BoardTileId equals tile.Id where s.EventId == ev.Id select new { s, team, tile }; if (teamId is not null) query = query.Where(x => x.s.TeamId == teamId); if (tileId is not null) query = query.Where(x => x.s.BoardTileId == tileId); if (status is not null) query = query.Where(x => x.s.Status == status); Rows = await query.OrderByDescending(x => x.s.SubmittedAt).Select(x => new Row(x.s.Id, x.s.SubmittedAt, x.s.SubmittedAt > ev.EventEndsAt, x.team.Name, x.tile.NameSnapshot, x.s.CreditedCharacterName, x.s.Status, x.s.ClaimedWeight, x.s.ApprovedContribution, x.s.ExpectedEvidenceCode, x.s.CurrentReviewerNote)).ToListAsync(ct);
    }
    public sealed record EventOption(Guid Id, string Name); public sealed record TeamOption(Guid Id, string Name); public sealed record TileOption(Guid Id, string Name); public sealed record Row(Guid Id, DateTimeOffset SubmittedAt, bool DuringGrace, string Team, string Tile, string Player, SubmissionStatus Status, int Claimed, int Approved, string? ExpectedCode, string? Note);
}
