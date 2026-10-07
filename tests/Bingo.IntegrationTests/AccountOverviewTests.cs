using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Accounts;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class AccountOverviewTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").WithLoopbackPort().WithDatabase("bingo_account_overview").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await PostgreSqlReadiness.StartAsync(database);
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetOwnedConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
        var ev = new BingoEvent(Guid.NewGuid(), "Overview event", "overview", "test", "UTC", now, now.AddDays(1), now.AddDays(2), now.AddDays(3), now.AddDays(3).AddMinutes(30), 100, Guid.NewGuid(), now);
        var team = new Team(Guid.NewGuid(), ev.Id, "Overview team", "overview-team", TeamFormationType.Drafted, null, true);
        db.Events.Add(ev); db.Teams.Add(team);
        for (var index = 0; index < 27; index++)
        {
            var username = $"overview-user-{index:D2}";
            var web = Account.CreateWebsite(Guid.NewGuid(), username, AccountAuthenticationService.NormalizeUsername(username), now);
            if (index == 0) web.SetGlobalRole(GlobalRole.Admin);
            if (index == 1) web.Disable(now, reason: "overview fixture");
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

    // A10 (T1): the directory now binds the reference query names q/role/page; the counts,
    // normalized search and paging rules are unchanged.
    [Fact]
    public async Task WebsiteAndEmergencyPagingFilteringAndProjectionRemainIndependent()
    {
        await using var db = new ApplicationDbContext(options);
        var page = AccountsPageTestFactory.Create(db, role: "user", page: "2");

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(27, page.WebsiteTotalCount);
        Assert.Equal(1, page.WebsiteDisabledCount);
        Assert.Single(page.WebsiteAccounts);
        Assert.Contains("Overview event", page.WebsiteAccounts[0].EventRoleSummary);

        var searchedWebsite = AccountsPageTestFactory.Create(db, q: "overview-user-01", role: "user");
        await searchedWebsite.OnGetAsync(CancellationToken.None);
        Assert.Equal(27, searchedWebsite.WebsiteTotalCount);
        Assert.Equal(1, searchedWebsite.WebsiteDisabledCount);
        Assert.Single(searchedWebsite.WebsiteAccounts);
        Assert.Equal("overview-user-01", searchedWebsite.WebsiteAccounts[0].Username);
    }

    [Fact]
    public async Task OutOfRangeAndCombinedFiltersReturnEmptyWithoutChangingOtherDataset()
    {
        await using var db = new ApplicationDbContext(options);
        var page = AccountsPageTestFactory.Create(db, page: "999999999");
        await page.OnGetAsync(CancellationToken.None);

        Assert.Empty(page.WebsiteAccounts);
        Assert.True(page.BeyondResults);
        Assert.Equal(27, page.MatchingCount);
    }

    [Fact]
    public async Task SearchIgnoresCaseAndInvalidQueryPartsFallBackToTheirDefaults()
    {
        await using var db = new ApplicationDbContext(options);
        // A7: username search is normalized on both sides.
        var upper = AccountsPageTestFactory.Create(db, q: "  OVERVIEW-USER-02  ");
        await upper.OnGetAsync(CancellationToken.None);
        Assert.Equal("overview-user-02", Assert.Single(upper.WebsiteAccounts).Username);
        Assert.Equal("OVERVIEW-USER-02", upper.Search);

        foreach (var junk in new[] { "2abc", "0", "-1", "+2", " 2", "1e1", "99999999999" })
        {
            var page = AccountsPageTestFactory.Create(db, page: junk, role: "nobody");
            await page.OnGetAsync(CancellationToken.None);
            Assert.Equal(1, page.PageNumber);
            Assert.Null(page.Role);
            Assert.Equal(25, page.WebsiteAccounts.Count);
            Assert.Equal("/Admin/Accounts", page.DirectoryUrl());
        }

        var longSearch = AccountsPageTestFactory.Create(db, q: new string('x', 140), role: "SuperAdmin", page: "3");
        await longSearch.OnGetAsync(CancellationToken.None);
        Assert.Equal(100, longSearch.Search.Length);
        Assert.Equal(GlobalRole.SuperAdmin, longSearch.Role);
        Assert.Equal("/Admin/Accounts?q=" + new string('x', 100) + "&role=superadmin&page=3", longSearch.DirectoryUrl());
    }

    [Fact]
    public async Task RowsNameOnlyCurrentCaptainRolesAndDrawerKeepsHiddenEventsOut()
    {
        await using var db = new ApplicationDbContext(options);
        var page = AccountsPageTestFactory.Create(db, q: "overview-user-0");
        await page.OnGetAsync(CancellationToken.None);
        Assert.Equal("Overview event: Captain", page.WebsiteAccounts.Single(row => row.Username == "overview-user-00").EventRoleSummary);
        Assert.Equal("Overview event", page.WebsiteAccounts.Single(row => row.Username == "overview-user-03").EventRoleSummary);

        var target = await db.Accounts.SingleAsync(account => account.PublicUsername == "overview-user-00");
        var drawer = AccountsPageTestFactory.Create(db, account: target.Id.ToString());
        await drawer.OnGetAsync(CancellationToken.None);
        Assert.Equal("overview-user-00", drawer.AccountView!.Username);
        Assert.Single(drawer.AccountView.EventRoles);

        var emergency = await db.Accounts.FirstAsync(account => account.AccountType == AccountType.EmergencyCaptain);
        var missing = AccountsPageTestFactory.Create(db, account: emergency.Id.ToString());
        await missing.OnGetAsync(CancellationToken.None);
        Assert.True(missing.DrawerRequested);
        Assert.Null(missing.AccountView);
    }

    private sealed class DictionaryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
