using Bingo.Application.Evidence;
using Bingo.Domain.Boards;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Captain;

public sealed class IndexModel(ApplicationDbContext db, IEvidenceAuthority evidenceAuthority, TimeProvider time) : PageModel
{
    public string EventName { get; private set; } = string.Empty; public string TeamName { get; private set; } = string.Empty;
    public Guid EventId { get; private set; }
    public Guid TeamId { get; private set; }
    public string? CurrentCode { get; private set; }
    public bool CodeEnabled { get; private set; }
    public bool NewSubmissionsOpen { get; private set; }
    public IReadOnlyList<TileView> Tiles { get; private set; } = []; public IReadOnlyList<SubmissionView> Submissions { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync(Guid? eventId, Guid? teamId, CancellationToken ct)
    {
        var accountId = User.GetAccountId(); if (accountId is null) return Challenge();
        EvidenceActorScope scope; try { scope = await evidenceAuthority.ResolveActorAsync(accountId.Value, eventId ?? User.GetEventId(), teamId ?? User.GetTeamId(), time.GetUtcNow(), ct); } catch (InvalidOperationException) { return NotFound(); }
        EventId = scope.EventId; TeamId = scope.TeamId;
        var now = time.GetUtcNow(); var ev = await db.Events.AsNoTracking().SingleAsync(x => x.Id == scope.EventId, ct); var team = await db.Teams.AsNoTracking().SingleAsync(x => x.Id == scope.TeamId, ct); EventName = ev.Name; TeamName = team.Name; CodeEnabled = ev.EvidenceCodeEnabled; NewSubmissionsOpen = ev.AcceptsNewSubmissions(now);
        if (CodeEnabled) CurrentCode = await db.EvidenceCodes.AsNoTracking().Where(x => x.EventId == scope.EventId && x.ActivatesAt <= now && (x.RetiresAt == null || x.RetiresAt > now)).OrderByDescending(x => x.ActivatesAt).Select(x => x.Code).FirstOrDefaultAsync(ct);
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == scope.EventId && x.State == BoardState.Published, ct); if (board is null) return Page(); var tiles = await db.BoardTiles.AsNoTracking().Where(x => x.BoardId == board.Id).OrderBy(x => x.RowIndex).ThenBy(x => x.ColumnIndex).ToListAsync(ct); var tileIds = tiles.Select(x => x.Id).ToList(); var requirements = await db.BoardRequirementSnapshots.AsNoTracking().Where(x => tileIds.Contains(x.BoardTileId)).ToListAsync(ct); var requirementIds = requirements.Select(x => x.Id).ToList(); var contributions = await db.SubmissionContributions.AsNoTracking().Where(x => x.TeamId == scope.TeamId && requirementIds.Contains(x.RequirementId) && x.ReversedAt == null).GroupBy(x => x.RequirementId).Select(x => new { x.Key, Total = x.Sum(y => y.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct); Tiles = tiles.Select(t => { var req = requirements.Where(x => x.BoardTileId == t.Id).ToList(); var complete = req.Count > 0 && req.All(x => contributions.GetValueOrDefault(x.Id) >= x.TargetContribution); return new TileView(t.Id, t.RowIndex, t.ColumnIndex, t.NameSnapshot, t.DescriptionSnapshot, complete, req.Sum(x => Math.Min(x.TargetContribution, contributions.GetValueOrDefault(x.Id))), req.Sum(x => x.TargetContribution)); }).ToList();
        var submissions = db.Submissions.AsNoTracking().Where(s => s.TeamId == scope.TeamId); if (scope.Kind == EvidenceActorKind.Participant) submissions = submissions.Where(s => s.CreditedParticipantId == scope.CreditedParticipantId);
        Submissions = await (from s in submissions join t in db.BoardTiles on s.BoardTileId equals t.Id orderby s.SubmittedAt descending select new SubmissionView(s.Id, t.NameSnapshot, s.CreditedCharacterName, s.Status, s.ClaimedWeight, s.ApprovedContribution, s.SubmittedAt, s.CurrentReviewerNote)).Take(100).ToListAsync(ct); return Page();
    }
    public sealed record TileView(Guid Id, int Row, int Column, string Name, string Description, bool Complete, int Progress, int Target);
    public sealed record SubmissionView(Guid Id, string Tile, string Player, SubmissionStatus Status, int Claimed, int Approved, DateTimeOffset SubmittedAt, string? Feedback);
}
