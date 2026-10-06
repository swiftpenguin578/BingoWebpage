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

    [Fact]
    public async Task WebsiteAndEmergencyPagingFilteringAndProjectionRemainIndependent()
    {
        await using var db = new ApplicationDbContext(options);
        var page = new IndexModel(db)
        {
            WebsitePage = 2,
            WebsiteRole = GlobalRole.User
        };

        await page.OnGetAsync(CancellationToken.None);

        Assert.Equal(27, page.WebsiteTotalCount);
        Assert.Equal(1, page.WebsiteDisabledCount);
        Assert.Single(page.WebsiteAccounts);
        Assert.Contains("Overview event", page.WebsiteAccounts[0].EventRoleSummary);

        var searchedWebsite = new IndexModel(db) { WebsiteSearch = "overview-user-01", WebsiteRole = GlobalRole.User };
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
        var page = new IndexModel(db) { WebsitePage = int.MaxValue };
        await page.OnGetAsync(CancellationToken.None);

        Assert.Empty(page.WebsiteAccounts);
    }

    [Fact]
    public async Task ManageResetLinkProjectionIsConsumedOnlyForMatchingAccount()
    {
        await using var db = new ApplicationDbContext(options);
        var accounts = await db.Accounts.Where(x => x.AccountType == AccountType.WebsiteAccount).OrderBy(x => x.PublicUsername).Take(2).ToListAsync();
        var first = accounts[0];
        var second = accounts[1];

        var mismatched = CreateManageModel(db);
        mismatched.TempData["CredentialLink"] = "https://example.test/reset/A";
        mismatched.TempData["CredentialLinkTargetId"] = first.Id.ToString();
        mismatched.TempData["CredentialLinkPurpose"] = "reset";
        mismatched.TempData["StatusMessage"] = "Generated a one-time reset link.";
        mismatched.TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
        await mismatched.OnGetAsync(second.Id, CancellationToken.None);

        Assert.Null(mismatched.CredentialLink);
        Assert.False(mismatched.TempData.ContainsKey("CredentialLink"));
        Assert.False(mismatched.TempData.ContainsKey("CredentialLinkTargetId"));
        Assert.False(mismatched.TempData.ContainsKey("CredentialLinkPurpose"));
        Assert.False(mismatched.TempData.ContainsKey("StatusMessage"));
        Assert.False(mismatched.TempData.ContainsKey(UiMessage.TypeKey));

        var matching = CreateManageModel(db);
        matching.TempData["CredentialLink"] = "https://example.test/reset/A";
        matching.TempData["CredentialLinkTargetId"] = first.Id.ToString();
        matching.TempData["CredentialLinkPurpose"] = "reset";
        matching.TempData["StatusMessage"] = "Generated a one-time reset link.";
        matching.TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
        await matching.OnGetAsync(first.Id, CancellationToken.None);

        Assert.Equal("https://example.test/reset/A", matching.CredentialLink);
        Assert.Equal("Generated a one-time reset link.", matching.TempData["StatusMessage"]?.ToString());
        Assert.Equal(UiMessageType.Success.ToString(), matching.TempData[UiMessage.TypeKey]?.ToString());
    }

    private static ManageModel CreateManageModel(ApplicationDbContext db)
    {
        var context = new DefaultHttpContext();
        var page = new ManageModel(db, new AccountAdministrationService(db, new PasswordHasher<Account>(), TimeProvider.System), new AccountIdentityService(db, new PasswordHasher<Account>(), TimeProvider.System))
        {
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(context, new DictionaryTempDataProvider())
        };
        return page;
    }

    private sealed class DictionaryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
