using Bingo.Application.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Bingo.Web.Teams;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Events;

public sealed class TeamsModel(ApplicationDbContext db, TimeProvider time, IParticipantLiveService? live = null) : PageModel
{
    public string EventName { get; private set; } = string.Empty;
    public DateTimeOffset? EventStartsAt { get; private set; }
    public DateTimeOffset? EventEndsAt { get; private set; }
    public string EventTimezone { get; private set; } = DateTimePresentation.DefaultTimezoneId;
    public int PlayerCount { get; private set; }
    public string? CurrentEvidenceCode { get; private set; }
    public IReadOnlyList<TeamView> Teams { get; private set; } = [];
    public IReadOnlyList<PickView> Picks { get; private set; } = [];
    public ParticipantLiveContext? ParticipantContext { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, Guid? participantId, CancellationToken ct)
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

        var cycle = await (from publication in db.DraftPublicationCycles.AsNoTracking()
                           join draft in db.DraftSessions.AsNoTracking() on publication.DraftSessionId equals draft.Id
                           where draft.EventId == ev.Id && publication.SupersededAt == null
                           select publication).SingleOrDefaultAsync(ct);
        if (cycle is null) return NotFound();
        var rosterEntries = await db.DraftPublicationRosters.AsNoTracking().Where(x => x.DraftPublicationCycleId == cycle.Id).ToListAsync(ct);
        var teamIds = rosterEntries.Select(x => x.TeamId).Distinct().ToList();
        var teams = await db.Teams.AsNoTracking()
            .Where(x => teamIds.Contains(x.Id))
            .OrderBy(x => x.DraftPosition).ThenBy(x => x.Name).ToListAsync(ct);
        if (teams.Count == 0) return NotFound();
        var displayedTeamIds = teams.Select(x => x.Id).ToHashSet();

        EventName = ev.Name;
        EventStartsAt = ev.EventStartsAt;
        EventEndsAt = ev.EventEndsAt;
        EventTimezone = ev.Timezone;
        PlayerCount = rosterEntries.Count(x => displayedTeamIds.Contains(x.TeamId));
        Teams = teams.Select(t => new TeamView(
                t.Name,
                t.AffiliationName,
                t.DraftPosition,
                t.FormationType,
                rosterEntries.Where(m => m.TeamId == t.Id)
                    .OrderBy(m => m.Role switch { TeamMembershipRole.Captain => 0, TeamMembershipRole.CoCaptain => 1, _ => 2 })
                    .ThenBy(m => m.EffectivePickNumber.HasValue ? 0 : 1)
                    .ThenBy(m => m.EffectivePickNumber)
                    .ThenBy(m => m.PublicCharacterName, StringComparer.Ordinal)
                    .Select(m => new MemberView(m.PublicCharacterName, m.Role)).ToList()))
            .ToList();

        var teamNames = teams.ToDictionary(x => x.Id, x => x.Name);
        Picks = rosterEntries.Where(x => x.EffectivePickNumber is not null && teamNames.ContainsKey(x.TeamId))
            .OrderBy(x => x.EffectivePickNumber).Select(x => new PickView(x.EffectivePickNumber!.Value, x.PublicCharacterName, teamNames[x.TeamId])).ToList();

        if (participantId is { } selectedParticipantId)
        {
            var accountId = User.GetAccountId();
            if (accountId is null) return Challenge();
            if (live is null) return StatusCode(500);
            ParticipantContext = await live.GetContextAsync(ev.Id, selectedParticipantId, accountId.Value, ct);
            if (ParticipantContext is null) return Forbid();
        }

        return Page();
    }

    [NonHandler]
    public Task<IActionResult> OnGetAsync(string slug, CancellationToken ct) => OnGetAsync(slug, null, ct);

    public sealed record TeamView(string Name, string? Affiliation, int? DraftPosition, TeamFormationType FormationType, IReadOnlyList<MemberView> Members);

    public sealed record MemberView(string Name, TeamMembershipRole Role);
    public sealed record PickView(int PickNumber, string PlayerName, string TeamName);
}
