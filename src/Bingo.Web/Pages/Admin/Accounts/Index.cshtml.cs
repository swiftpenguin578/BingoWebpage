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
    public IReadOnlyList<EmergencyCredentialRow> EmergencyCredentials { get; private set; } = [];
    public IReadOnlyList<EventOption> Events { get; private set; } = [];
    public bool WebsiteHasNextPage { get; private set; }
    public bool EmergencyHasNextPage { get; private set; }

    [BindProperty(SupportsGet = true)] public string? WebsiteSearch { get; set; }
    [BindProperty(SupportsGet = true)] public GlobalRole? WebsiteRole { get; set; }
    [BindProperty(SupportsGet = true)] public string? WebsiteState { get; set; }
    [BindProperty(SupportsGet = true)] public string? WebsiteDiscord { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? WebsiteEventId { get; set; }
    [BindProperty(SupportsGet = true)] public int WebsitePage { get; set; } = 1;

    [BindProperty(SupportsGet = true)] public string? EmergencySearch { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? EmergencyEventId { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? EmergencyTeamId { get; set; }
    [BindProperty(SupportsGet = true)] public string? EmergencySetup { get; set; }
    [BindProperty(SupportsGet = true)] public string? EmergencyState { get; set; }
    [BindProperty(SupportsGet = true)] public string? EmergencyCutoff { get; set; }
    [BindProperty(SupportsGet = true)] public int EmergencyPage { get; set; } = 1;

    public async Task OnGetAsync(CancellationToken ct)
    {
        WebsiteSearch = Sanitize(WebsiteSearch);
        EmergencySearch = Sanitize(EmergencySearch);
        WebsitePage = Math.Max(1, WebsitePage);
        EmergencyPage = Math.Max(1, EmergencyPage);
        Events = await db.Events.AsNoTracking().OrderBy(x => x.Name).Select(x => new EventOption(x.Id, x.Name)).ToListAsync(ct);
        await LoadWebsiteAsync(ct);
        await LoadEmergencyAsync(ct);
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
        if (WebsiteState == "active") query = query.Where(x => x.Active);
        if (WebsiteState == "disabled") query = query.Where(x => !x.Active);
        if (WebsiteDiscord == "linked") query = query.Where(x => x.DiscordUserId != null);
        if (WebsiteDiscord == "unlinked") query = query.Where(x => x.DiscordUserId == null);
        if (WebsiteEventId is { } eventId)
        {
            var names = db.EventParticipants.Where(x => x.EventId == eventId).Select(x => x.NormalizedPrimaryAccountName);
            query = query.Where(x => db.AccountOsrsCharacters.Any(link => link.AccountId == x.Id && link.Active && db.OsrsCharacters.Any(character => character.Id == link.OsrsCharacterId && names.Contains(character.NormalizedName))));
        }

        var rows = await query.OrderBy(x => x.PublicUsername).ThenBy(x => x.Id)
            .Skip((WebsitePage - 1) * PageSize).Take(PageSize + 1)
            .Select(x => new WebsiteAccountRow(x.Id, x.PublicUsername!, x.GlobalRole!.Value, x.Active, x.DiscordUserId != null, x.LastLoginAt, ""))
            .ToListAsync(ct);
        WebsiteHasNextPage = rows.Count > PageSize;
        WebsiteAccounts = rows.Take(PageSize).ToList();

        var ids = WebsiteAccounts.Select(x => x.Id).ToArray();
        var accountCharacters = await (from link in db.AccountOsrsCharacters.AsNoTracking()
                                       join character in db.OsrsCharacters.AsNoTracking() on link.OsrsCharacterId equals character.Id
                                       where ids.Contains(link.AccountId) && link.Active
                                       select new CharacterLink(link.AccountId, character.NormalizedName)).ToListAsync(ct);
        var characterNames = accountCharacters.Select(x => x.NormalizedName).Distinct().ToArray();
        var participations = await (from participant in db.EventParticipants.AsNoTracking()
                                    join bingoEvent in db.Events.AsNoTracking() on participant.EventId equals bingoEvent.Id
                                    where characterNames.Contains(participant.NormalizedPrimaryAccountName)
                                    select new Participation(participant.Id, participant.NormalizedPrimaryAccountName, bingoEvent.Name)).ToListAsync(ct);
        var participantIds = participations.Select(x => x.Id).ToArray();
        var roles = await (from membership in db.TeamMemberships.AsNoTracking()
                           join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
                           where participantIds.Contains(membership.EventParticipantId) && membership.LeftAt == null
                           select new CurrentRole(membership.EventParticipantId, team.Name, membership.Role)).ToListAsync(ct);
        var summaries = ids.ToDictionary(id => id, id => BuildSummary(id, accountCharacters, participations, roles));
        WebsiteAccounts = WebsiteAccounts.Select(x => x with { EventRoleSummary = summaries[x.Id] }).ToList();
    }

    private async Task LoadEmergencyAsync(CancellationToken ct)
    {
        var query = from account in db.Accounts.AsNoTracking()
                    join access in db.AccountEventAccesses.AsNoTracking() on account.Id equals access.AccountId
                    join bingoEvent in db.Events.AsNoTracking() on access.EventId equals bingoEvent.Id
                    join team in db.Teams.AsNoTracking() on access.TeamId equals team.Id
                    where account.AccountType == AccountType.EmergencyCaptain
                    select new { account, access, EventName = bingoEvent.Name, TeamName = team.Name };
        if (EmergencySearch is { Length: > 0 }) { var normalized = EmergencySearch.ToUpperInvariant(); query = query.Where(x => x.account.NormalizedLoginName.Contains(normalized)); }
        if (EmergencyEventId is { } eventId) query = query.Where(x => x.access.EventId == eventId);
        if (EmergencyTeamId is { } teamId) query = query.Where(x => x.access.TeamId == teamId);
        if (EmergencySetup == "complete") query = query.Where(x => x.account.PasswordHash != null);
        if (EmergencySetup == "pending") query = query.Where(x => x.account.PasswordHash == null);
        if (EmergencyState == "enabled") query = query.Where(x => x.account.Active && x.access.Enabled);
        if (EmergencyState == "disabled") query = query.Where(x => !x.account.Active || !x.access.Enabled);
        if (EmergencyCutoff == "cutoff") query = query.Where(x => x.access.CutoffDisabled);
        if (EmergencyCutoff == "open") query = query.Where(x => !x.access.CutoffDisabled);
        var rows = await query.OrderBy(x => x.account.LoginName).ThenBy(x => x.account.Id).Skip((EmergencyPage - 1) * PageSize).Take(PageSize + 1)
            .Select(x => new EmergencyCredentialRow(x.account.Id, x.account.LoginName, x.EventName, x.TeamName, x.account.PasswordHash != null, x.account.Active && x.access.Enabled, x.access.CutoffDisabled, x.account.LastLoginAt)).ToListAsync(ct);
        EmergencyHasNextPage = rows.Count > PageSize;
        EmergencyCredentials = rows.Take(PageSize).ToList();
    }

    private static string BuildSummary(Guid accountId, IEnumerable<CharacterLink> characters, IEnumerable<Participation> participants, IEnumerable<CurrentRole> roles)
    {
        var names = characters.Where(x => x.AccountId == accountId).Select(x => x.NormalizedName).ToHashSet();
        var items = participants.Where(x => names.Contains(x.NormalizedPrimaryAccountName)).Select(x =>
        {
            var membership = roles.FirstOrDefault(role => role.EventParticipantId == x.Id);
            return membership is null ? x.EventName : $"{x.EventName}: {membership.Role}";
        }).Distinct().Take(3).ToArray();
        return items.Length == 0 ? "No event participation" : string.Join("; ", items);
    }

    private static string? Sanitize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(100, value.Trim().Length)];
    public sealed record WebsiteAccountRow(Guid Id, string Username, GlobalRole Role, bool Active, bool DiscordLinked, DateTimeOffset? LastLoginAt, string EventRoleSummary);
    public sealed record EmergencyCredentialRow(Guid Id, string Username, string EventName, string TeamName, bool SetupComplete, bool Enabled, bool CutoffDisabled, DateTimeOffset? LastLoginAt);
    public sealed record EventOption(Guid Id, string Name);
    private sealed record CharacterLink(Guid AccountId, string NormalizedName);
    private sealed record Participation(Guid Id, string NormalizedPrimaryAccountName, string EventName);
    private sealed record CurrentRole(Guid EventParticipantId, string TeamName, Bingo.Domain.Teams.TeamMembershipRole Role);
}
