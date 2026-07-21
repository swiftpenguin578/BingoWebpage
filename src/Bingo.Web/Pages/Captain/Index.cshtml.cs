using Bingo.Domain.Boards;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Captain;

public sealed class IndexModel(ApplicationDbContext db, TimeProvider time) : PageModel
{
    public string EventName { get; private set; } = string.Empty; public string TeamName { get; private set; } = string.Empty;
    public string? CurrentCode { get; private set; }
    public bool CodeEnabled { get; private set; }
    public bool NewSubmissionsOpen { get; private set; }
    public IReadOnlyList<TileView> Tiles { get; private set; } = []; public IReadOnlyList<SubmissionView> Submissions { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken ct)
    {
        var eventId = User.GetEventId()!.Value; var teamId = User.GetTeamId()!.Value; var now = time.GetUtcNow(); var ev = await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId, ct); var team = await db.Teams.AsNoTracking().SingleAsync(x => x.Id == teamId, ct); EventName = ev.Name; TeamName = team.Name; CodeEnabled = ev.EvidenceCodeEnabled; NewSubmissionsOpen = ev.AcceptsNewSubmissions(now);
        if (CodeEnabled) CurrentCode = await db.EvidenceCodes.AsNoTracking().Where(x => x.EventId == eventId && x.ActivatesAt <= now && (x.RetiresAt == null || x.RetiresAt > now)).OrderByDescending(x => x.ActivatesAt).Select(x => x.Code).FirstOrDefaultAsync(ct);
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId && x.State == BoardState.Published, ct); if (board is null) return; var tiles = await db.BoardTiles.AsNoTracking().Where(x => x.BoardId == board.Id).OrderBy(x => x.RowIndex).ThenBy(x => x.ColumnIndex).ToListAsync(ct); var tileIds = tiles.Select(x => x.Id).ToList(); var requirements = await db.BoardRequirementSnapshots.AsNoTracking().Where(x => tileIds.Contains(x.BoardTileId)).ToListAsync(ct); var requirementIds = requirements.Select(x => x.Id).ToList(); var contributions = await db.SubmissionContributions.AsNoTracking().Where(x => x.TeamId == teamId && requirementIds.Contains(x.RequirementId) && x.ReversedAt == null).GroupBy(x => x.RequirementId).Select(x => new { x.Key, Total = x.Sum(y => y.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct); Tiles = tiles.Select(t => { var req = requirements.Where(x => x.BoardTileId == t.Id).ToList(); var complete = req.Count > 0 && req.All(x => contributions.GetValueOrDefault(x.Id) >= x.TargetContribution); return new TileView(t.Id, t.RowIndex, t.ColumnIndex, t.NameSnapshot, t.DescriptionSnapshot, complete, req.Sum(x => Math.Min(x.TargetContribution, contributions.GetValueOrDefault(x.Id))), req.Sum(x => x.TargetContribution)); }).ToList();
        Submissions = await (from s in db.Submissions.AsNoTracking() join t in db.BoardTiles on s.BoardTileId equals t.Id join p in db.EventParticipants on s.CreditedParticipantId equals p.Id where s.TeamId == teamId orderby s.SubmittedAt descending select new SubmissionView(s.Id, t.NameSnapshot, p.PrimaryAccountName, s.Status, s.ClaimedWeight, s.ApprovedContribution, s.SubmittedAt, s.CurrentReviewerNote)).Take(100).ToListAsync(ct);
    }
    public sealed record TileView(Guid Id, int Row, int Column, string Name, string Description, bool Complete, int Progress, int Target);
    public sealed record SubmissionView(Guid Id, string Tile, string Player, SubmissionStatus Status, int Claimed, int Approved, DateTimeOffset SubmittedAt, string? Feedback);
}
