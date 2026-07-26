using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Events;

public sealed class TeamsModel(ApplicationDbContext db, TimeProvider time) : PageModel
{
    public string EventName { get; private set; } = string.Empty;
    public string? CurrentEvidenceCode { get; private set; }
    public IReadOnlyList<TeamView> Teams { get; private set; } = [];
    public IReadOnlyList<PickView> Picks { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug, ct);
        if (ev is null) return NotFound();

        var now = time.GetUtcNow();
        if (ev.EvidenceCodeEnabled)
        {
            CurrentEvidenceCode = await db.EvidenceCodes.AsNoTracking()
                .Where(x => x.EventId == ev.Id && x.ActivatesAt <= now && (x.RetiresAt == null || x.RetiresAt > now))
                .OrderByDescending(x => x.ActivatesAt)
                .Select(x => x.Code)
                .FirstOrDefaultAsync(ct);
        }

        var teams = await db.Teams.AsNoTracking()
            .Where(x => x.EventId == ev.Id && x.Active && x.FinalizedAt != null)
            .OrderBy(x => x.DraftPosition).ThenBy(x => x.Name).ToListAsync(ct);
        if (teams.Count == 0) return NotFound();

        var ids = teams.Select(x => x.Id).ToList();
        var memberships = await db.TeamMemberships.AsNoTracking().Where(x => ids.Contains(x.TeamId) && x.LeftAt == null).ToListAsync(ct);
        var participants = await db.PrimaryCharacters().AsNoTracking().Where(x => x.EventId == ev.Id).ToDictionaryAsync(x => x.ParticipantId, ct);
        EventName = ev.Name;
        Teams = teams.Select(t => new TeamView(
                t.Name,
                t.AffiliationName,
                t.ImageUrl,
                t.FormationType,
                memberships.Where(m => m.TeamId == t.Id)
                    .Select(m => new MemberView(participants[m.EventParticipantId].Name, participants[m.EventParticipantId].Ehb, m.Role))
                    .OrderBy(x => x.Role).ThenByDescending(x => x.Ehb).ToList()))
            .Select(t => t with { TotalEhb = t.FormationType == TeamFormationType.Drafted ? t.Members.Sum(x => x.Ehb) : 0m })
            .ToList();

        var draft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == ev.Id && x.State == DraftState.Finalized, ct);
        if (draft is not null)
        {
            var picks = await db.DraftPicks.AsNoTracking().Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null).OrderBy(x => x.PickNumber).ToListAsync(ct);
            var teamNames = teams.ToDictionary(x => x.Id, x => x.Name);
            Picks = picks.Select(x => new PickView(x.PickNumber, x.RoundNumber, participants[x.EventParticipantId].Name, teamNames[x.TeamId])).ToList();
        }

        return Page();
    }

    public sealed record TeamView(string Name, string? Affiliation, string? ImageUrl, TeamFormationType FormationType, IReadOnlyList<MemberView> Members)
    {
        public decimal TotalEhb { get; init; }
    }

    public sealed record MemberView(string Name, decimal Ehb, TeamMembershipRole Role);
    public sealed record PickView(int PickNumber, int RoundNumber, string PlayerName, string TeamName);
}
