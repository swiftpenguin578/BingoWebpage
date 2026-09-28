using Bingo.Application.Access;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Accounts;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class IndexModel(ApplicationDbContext db) : PageModel
{
    private const int PageSize = 25;

    public IReadOnlyList<WebsiteAccountRow> WebsiteAccounts { get; private set; } = [];
    public bool WebsiteHasNextPage { get; private set; }

    [BindProperty(SupportsGet = true)] public string? WebsiteSearch { get; set; }
    [BindProperty(SupportsGet = true)] public GlobalRole? WebsiteRole { get; set; }
    [BindProperty(SupportsGet = true)] public int WebsitePage { get; set; } = 1;


    public async Task OnGetAsync(CancellationToken ct)
    {
        WebsiteSearch = Sanitize(WebsiteSearch);
        WebsitePage = Math.Max(1, WebsitePage);
        await LoadWebsiteAsync(ct);
    }

    private async Task LoadWebsiteAsync(CancellationToken ct)
    {
        var query = db.Accounts.AsNoTracking().Where(x => x.AccountType == AccountType.WebsiteAccount);
        if (WebsiteSearch is { Length: > 0 })
        {
            var normalized = WebsiteSearch.ToUpperInvariant();
            query = query.Where(x => x.NormalizedPublicUsername != null && x.NormalizedPublicUsername.Contains(normalized));
        }
        if (WebsiteRole is not null) query = query.Where(x => x.GlobalRole == WebsiteRole);
        var rows = await query.OrderBy(x => x.PublicUsername).ThenBy(x => x.Id)
            .Skip((WebsitePage - 1) * PageSize).Take(PageSize + 1)
            .Select(x => new WebsiteAccountRow(x.Id, x.PublicUsername!, x.GlobalRole!.Value, x.Active, x.DiscordUserId != null, x.DiscordDisplayName, x.LastLoginAt, ""))
            .ToListAsync(ct);
        WebsiteHasNextPage = rows.Count > PageSize;
        WebsiteAccounts = rows.Take(PageSize).ToList();

        var ids = WebsiteAccounts.Select(x => x.Id).ToArray();
        var participations = await (from participant in db.EventParticipants.AsNoTracking()
                                    join bingoEvent in db.Events.AsNoTracking() on participant.EventId equals bingoEvent.Id
                                    where participant.AccountId != null && ids.Contains(participant.AccountId.Value) && bingoEvent.HiddenAt == null
                                    select new Participation(participant.Id, participant.AccountId ?? Guid.Empty, bingoEvent.Name)).ToListAsync(ct);
        var participantIds = participations.Select(x => x.Id).ToArray();
        var roles = await (from membership in db.TeamMemberships.AsNoTracking()
                           join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
                           where participantIds.Contains(membership.EventParticipantId) && membership.LeftAt == null
                           select new CurrentRole(membership.EventParticipantId, team.Name, membership.Role)).ToListAsync(ct);
        var summaries = ids.ToDictionary(id => id, id => BuildSummary(id, participations, roles));
        WebsiteAccounts = WebsiteAccounts.Select(x => x with { EventRoleSummary = summaries[x.Id] }).ToList();
    }

    private static string BuildSummary(Guid accountId, IEnumerable<Participation> participants, IEnumerable<CurrentRole> roles)
    {
        var items = participants.Where(x => x.AccountId == accountId).Select(x =>
        {
            var membership = roles.FirstOrDefault(role => role.EventParticipantId == x.Id);
            return membership is null ? x.EventName : $"{x.EventName}: {membership.Role}";
        }).Distinct().Take(3).ToArray();
        return items.Length == 0 ? "No event participation" : string.Join("; ", items);
    }

    private static string? Sanitize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(100, value.Trim().Length)];
    public sealed record WebsiteAccountRow(Guid Id, string Username, GlobalRole Role, bool Active, bool DiscordLinked, string? DiscordDisplayName, DateTimeOffset? LastLoginAt, string EventRoleSummary);
    private sealed record Participation(Guid Id, Guid AccountId, string EventName);
    private sealed record CurrentRole(Guid EventParticipantId, string TeamName, Bingo.Domain.Teams.TeamMembershipRole Role);
}
