using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Review;

public sealed class IndexModel(ApplicationDbContext db) : PageModel
{
    public string EventName { get; private set; } = string.Empty; public string EventTimezone { get; private set; } = DateTimePresentation.DefaultTimezoneId; public Guid? EventId { get; private set; }
    public string Search { get; private set; } = string.Empty;
    public SubmissionStatus? Status { get; private set; }
    public IReadOnlyList<Row> Rows { get; private set; } = [];
    public async Task OnGetAsync(Guid? eventId, string? search, SubmissionStatus? status, CancellationToken ct)
    {
        Search = search?.Trim() ?? string.Empty; Status = status;
        if (eventId is null) return;

        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, ct);
        if (ev is null) return;

        EventId = ev.Id; EventName = ev.Name; EventTimezone = ev.Timezone;
        Rows = await (from s in db.Submissions.AsNoTracking()
                      join team in db.Teams.AsNoTracking() on s.TeamId equals team.Id
                      join tile in db.BoardTiles.AsNoTracking() on s.BoardTileId equals tile.Id
                      where s.EventId == ev.Id
                      orderby s.Status == SubmissionStatus.Pending descending, s.SubmittedAt descending
                      select new Row(s.Id, s.SubmittedAt, s.SubmittedAt > ev.EventEndsAt, team.Name, tile.NameSnapshot, s.CreditedCharacterName, s.Status, s.ClaimedWeight, s.ApprovedContribution, s.ExpectedEvidenceCode, s.CurrentReviewerNote)).ToListAsync(ct);
    }
    public sealed record Row(Guid Id, DateTimeOffset SubmittedAt, bool DuringGrace, string Team, string Tile, string Player, SubmissionStatus Status, int Claimed, int Approved, string? ExpectedCode, string? Note);
}
