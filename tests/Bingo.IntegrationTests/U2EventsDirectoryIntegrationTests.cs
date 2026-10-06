using System.Globalization;
using System.Security.Claims;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Dashboard;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Navigation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Testcontainers.PostgreSql;
using DirectoryPage = Bingo.Web.Pages.Admin.Events.IndexModel;

namespace Bingo.IntegrationTests;

public sealed class U2EventsDirectoryIntegrationTests(PostgreSqlTestFixture fixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] CultureNames = ["alpha", "Alpha", "Zebra", "Ægir", "Ørn", "År"];
    private readonly PostgreSqlTestDatabase database = fixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine"));
    private DbContextOptions<ApplicationDbContext> options = null!;
    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
    }
    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task PagingCountsAndInvalidPhasePartsAreNormalizedWithoutIncludingHiddenOrDiscarded()
    {
        await using var db = new ApplicationDbContext(options);
        var admin = Admin();
        var rows = Enumerable.Range(0, 31).Select(n => Event(admin, $"Page {n:00}")).ToArray();
        var hidden = Event(admin, "Private quarantine"); End(hidden); hidden.Hide(admin.Id, Now, hidden.Name, "Fixture");
        var discarded = Event(admin, "Discarded"); discarded.Discard(admin.Id, Now, false);
        db.AddRange(admin, hidden, discarded); db.AddRange(rows); await db.SaveChangesAsync();
        var page = Page(db, admin); page.PageNumber = 999;
        await page.OnGetAsync(default);
        Assert.Equal(31, page.VisibleCount); Assert.Equal(31, page.CurrentCount);
        Assert.Equal(2, page.PageNumber); Assert.Equal(6, page.Events.Count);
        Assert.Equal(26, page.FirstRow); Assert.Equal(31, page.LastRow); Assert.True(page.DroppedLinkParts);
        Assert.DoesNotContain(hidden.Name, page.DuplicateNames); Assert.DoesNotContain(discarded.Name, page.DuplicateNames);
        page.View = "past"; page.Phase = "live"; page.PageNumber = 1;
        await page.OnGetAsync(default);
        Assert.Equal("all", page.ActiveFilter); Assert.True(page.DroppedLinkParts); Assert.Empty(page.Events);
        page.View = "hidden"; page.Phase = null;
        await page.OnGetAsync(default);
        Assert.Equal("all", page.ActiveView); Assert.True(page.DroppedLinkParts);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("da")]
    public async Task NamesUseRequestCultureIgnoringCaseAndIdsBreakEquivalentNames(string culture)
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            await using var db = new ApplicationDbContext(options);
            var admin = Admin();
            var rows = CultureNames.Select(name => Event(admin, name)).ToArray();
            db.Add(admin); db.AddRange(rows); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var page = Page(db, admin); page.Sort = "identity";
            await page.OnGetAsync(default);
            var expected = rows.OrderBy(row => row.Name, StringComparer.Create(CultureInfo.CurrentCulture, true)).ThenBy(row => row.Id).Select(row => row.Id);
            Assert.Equal(expected, page.Events.Select(row => row.Id));
            Assert.Equal(rows.Where(row => row.Name.Equals("alpha", StringComparison.OrdinalIgnoreCase)).OrderBy(row => row.Id).Select(row => row.Id),
                page.Events.Where(row => row.Name.Equals("alpha", StringComparison.OrdinalIgnoreCase)).Select(row => row.Id));
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }

    [Fact]
    public async Task LegacyHiddenMappingUsesNewestHiddenMicrosecondsAndCancelledRetainsConfirmed()
    {
        await using var db = new ApplicationDbContext(options);
        var admin = Admin(); admin.SetGlobalRole(GlobalRole.SuperAdmin);
        var early = Event(admin, "Zulu older"); End(early); early.Hide(admin.Id, Now.AddTicks(1), early.Name, "Fixture");
        var late = Event(admin, "Alpha newer"); End(late); late.Hide(admin.Id, Now.AddTicks(17), late.Name, "Fixture");
        var cancelled = Event(admin, "Cancelled with people"); cancelled.Cancel(admin.Id, Now, "Fixture", true);
        db.AddRange(admin, early, late, cancelled, new EventParticipant(Guid.NewGuid(), cancelled.Id, SignupStatus.Confirmed, 1, Now.AddDays(-1), SignupSource.AdminCreated));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var page = Page(db, admin); page.Filter = "hidden";
        await page.OnGetAsync(default);
        Assert.Equal("hidden", page.ActiveView); Assert.False(page.DroppedLinkParts);
        Assert.Equal(new[] { late.Id, early.Id }, page.Events.Select(row => row.Id));
        Assert.Equal(Now.AddTicks(10), page.Events[0].HiddenAt);
        Assert.Equal(2, page.HiddenCount); Assert.Equal(1, page.VisibleCount);
        Assert.Contains(early.Name, page.DuplicateNames);
        page.Filter = null; page.View = "all";
        await page.OnGetAsync(default);
        var row = Assert.Single(page.Events);
        Assert.Equal(1L, row.ParticipantCount); Assert.Equal("1 confirmed", page.PeopleMain(row));
        Assert.Equal("when it was cancelled", page.PeopleSub(row));
        Assert.False(db.ChangeTracker.HasChanges());
    }

    private static Account Admin()
    {
        var account = Account.CreateWebsite(Guid.NewGuid(), "U2 directory", "U2DIRECTORY", Now);
        account.SetGlobalRole(GlobalRole.Admin); return account;
    }
    private static BingoEvent Event(Account admin, string name) => new(Guid.NewGuid(), name, $"u2-{Guid.NewGuid():N}", "Europe/Copenhagen", admin.Id, Now.AddDays(-10), PlacementRule.LegacyScoreTimeThenEhb);
    private static void End(BingoEvent item)
    {
        item.ConfigureSchedule(Now.AddDays(-6), Now.AddDays(-5), null, Now.AddDays(-4), Now.AddDays(-3), null);
        item.ConfigureSignup(true, false, null); item.OpenSignups(Now.AddDays(-6)); item.CloseSignups(Now.AddDays(-5));
        item.StartEvent(Now.AddDays(-4)); item.EndEvent(Now.AddDays(-3));
    }
    private static DirectoryPage Page(ApplicationDbContext db, Account actor) => new(db, null!, new Clock(), new Text(),
        new SharedShellService(db, new Text(), null!, null!, null!, new Clock()), new AdminDashboardService(db, new Clock()))
    {
        PageContext = new PageContext(new ActionContext(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()), new Claim(ClaimTypes.Role, "SuperAdmin")], "test"))
        }, new RouteData(), new PageActionDescriptor()))
    };
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Text : IStringLocalizer<Bingo.Web.SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] args] => new(name, string.Format(CultureInfo.CurrentCulture, name, args));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
