using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Accounts;
using Bingo.Web.Security;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class AccountOverviewTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("bingo_account_overview").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
        var ev = new BingoEvent(Guid.NewGuid(), "Overview event", "overview", "test", "UTC", now, now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3).AddMinutes(30), 100, Guid.NewGuid(), now);
        var team = new Team(Guid.NewGuid(), ev.Id, "Overview team", "overview-team", TeamFormationType.Drafted, null, true);
        db.Events.Add(ev); db.Teams.Add(team);
        for (var index = 0; index < 26; index++)
        {
            var username = $"overview-user-{index:D2}";
            var web = Account.CreateWebsite(Guid.NewGuid(), username, AccountAuthenticationService.NormalizeUsername(username), now);
            if (index == 0) web.SetGlobalRole(GlobalRole.Admin);
            var character = new OsrsCharacter(Guid.NewGuid(), username, AccountAuthenticationService.NormalizeUsername(username), now);
            var participant = new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, index, now, SignupSource.Website);
            participant.AssignOwner(web);
            db.Accounts.Add(web); db.OsrsCharacters.Add(character); db.AccountOsrsCharacters.Add(new AccountOsrsCharacter(Guid.NewGuid(), web.Id, character.Id, true, 0, now)); db.EventParticipants.Add(participant);
            db.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), ev.Id, participant.Id, character.Id, 0, now, web.Id, null, EventCharacterRole.Playing, 1, EhbSource.Manual, null));
            db.TeamMemberships.Add(new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, index == 0 ? TeamMembershipRole.Captain : TeamMembershipRole.Participant, now, null, null));
            var emergency = Account.CreateEmergency(Guid.NewGuid(), $"overview-emergency-{index:D2}", $"OVERVIEW-EMERGENCY-{index:D2}", now);
            if (index % 2 == 0) emergency.SetPasswordHash("not-a-real-password-hash", false);
            db.Accounts.Add(emergency); db.AccountEventAccesses.Add(new AccountEventAccess(Guid.NewGuid(), emergency.Id, ev.Id, team.Id, null, null, null, null));
        }
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task WebsiteAndEmergencyPagingFilteringAndProjectionRemainIndependent()
    {
        await using var db = new ApplicationDbContext(options);
        var page = new IndexModel(db)
        {
            WebsitePage = 2,
            EmergencyPage = 1,
            WebsiteRole = GlobalRole.User,
            EmergencySearch = "overview-emergency-24"
        };

        await page.OnGetAsync(CancellationToken.None);

        Assert.Single(page.WebsiteAccounts);
        Assert.Single(page.EmergencyCredentials);
        Assert.DoesNotContain("not-a-real-password-hash", string.Join(' ', page.EmergencyCredentials.Select(row => row.Username)), StringComparison.Ordinal);
        Assert.Contains("Overview event", page.WebsiteAccounts[0].EventRoleSummary);

        var searchedWebsite = new IndexModel(db) { WebsiteSearch = "overview-user-01", WebsiteRole = GlobalRole.User };
        await searchedWebsite.OnGetAsync(CancellationToken.None);
        Assert.Single(searchedWebsite.WebsiteAccounts);
        Assert.Equal("overview-user-01", searchedWebsite.WebsiteAccounts[0].Username);

        var searchedEmergency = new IndexModel(db) { EmergencySearch = "overview-emergency-01" };
        await searchedEmergency.OnGetAsync(CancellationToken.None);
        Assert.Single(searchedEmergency.EmergencyCredentials);
        Assert.Equal("overview-emergency-01", searchedEmergency.EmergencyCredentials[0].Username);
    }

    [Fact]
    public async Task OutOfRangeAndCombinedFiltersReturnEmptyWithoutChangingOtherDataset()
    {
        await using var db = new ApplicationDbContext(options);
        var page = new IndexModel(db) { WebsitePage = 99, EmergencyPage = 99 };
        await page.OnGetAsync(CancellationToken.None);

        Assert.Empty(page.WebsiteAccounts);
        Assert.Empty(page.EmergencyCredentials);
        Assert.False(page.EmergencyHasNextPage);
    }
}
