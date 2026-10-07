using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Navigation;
using Bingo.Web.UI;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed partial class AdminDesignShellIntegrationTests(PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private static readonly string[] ExpectedSwitcherTones = ["tone-review", "tone-live", "tone-closed", "tone-open", "tone-draft", "tone-draft"];
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_u1_shell").WithUsername("bingo").WithPassword("bingo_test_password"));
    private DbContextOptions<ApplicationDbContext> options = null!;
    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
    }
    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task SwitcherUsesEligiblePhasesStableStartOrderPastSelectionAndHiddenOverview()
    {
        var admin = Admin();
        await using var db = new ApplicationDbContext(options);
        db.Add(admin);
        var review = Event(admin, EventState.AwaitingFinalReview, "Review", -5);
        var live = Event(admin, EventState.Live, "Live", -3);
        var closed = Event(admin, EventState.SignupClosed, "Closed", 1);
        var open = Event(admin, EventState.SignupOpen, "Open", 2);
        var draft = Event(admin, EventState.Draft, "Draft", 3);
        var unscheduled = new BingoEvent(Guid.NewGuid(), "Unscheduled", "unscheduled", "UTC", admin.Id, Now, PlacementRule.LegacyScoreTimeThenEhb);
        var past = Event(admin, EventState.Archived, "Past", -8);
        var cancelled = Event(admin, EventState.Cancelled, "Cancelled", -9);
        var finished = Event(admin, EventState.Finalized, "Finished", -10);
        var hidden = Event(admin, EventState.AwaitingFinalReview, "Hidden", -2);
        hidden.Hide(admin.Id, Now, hidden.Name, "Controlled fixture");
        var hiddenPast = Event(admin, EventState.Archived, "Hidden past", -11);
        hiddenPast.Hide(admin.Id, Now, hiddenPast.Name, "Controlled fixture");
        var discarded = Event(admin, EventState.Draft, "Discarded", 5);
        db.AddRange(review, live, closed, open, draft, unscheduled, past, cancelled, finished, hidden, hiddenPast, discarded);
        db.Entry(discarded).Property(item => item.State).CurrentValue = EventState.Discarded;
        await db.SaveChangesAsync();
        var service = new SharedShellService(db, new Text(), new ShellClock());
        var routes = new RouteValueDictionary { ["page"] = "/Admin/Events/Identity", ["id"] = past.Id };
        var ordinary = await service.GetAdminDesignAsync(User("Admin"), routes, CancellationToken.None);
        Assert.Equal(past.Id, ordinary.SelectedEvent!.Id);
        Assert.Equal("ended " + past.EventEndsAt!.Value.ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture), ordinary.SelectedEvent.When);
        Assert.Equal(new[] { review.Id, live.Id, closed.Id, open.Id, draft.Id, unscheduled.Id }, ordinary.Events.Select(item => item.Id));
        Assert.All(ordinary.Events, item => Assert.Equal($"/Admin/Events/Identity/{item.Id}", item.Url));
        Assert.Equal("not announced", ordinary.Events[^1].When);
        Assert.Equal(ExpectedSwitcherTones, ordinary.Events.Select(item => item.Tone));
        Assert.Equal("tone-draft", ordinary.SelectedEvent.Tone);
        Assert.Equal(new[] { "ended " + review.EventEndsAt!.Value.ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture), "ended " + live.EventEndsAt!.Value.ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture),
            "starts " + closed.EventStartsAt!.Value.ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture), "closes " + open.SignupClosesAt!.Value.ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture),
            "starts " + draft.EventStartsAt!.Value.ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture), "not announced" }, ordinary.Events.Select(item => item.When));
        foreach (var terminal in new[] { past, cancelled, finished })
        {
            routes["id"] = terminal.Id;
            var selected = (await service.GetAdminDesignAsync(User("Admin"), routes, CancellationToken.None)).SelectedEvent!;
            Assert.Equal(terminal.State == EventState.Finalized ? "tone-done" : "tone-draft", selected.Tone);
            Assert.Equal((terminal.State == EventState.Cancelled ? "on " + terminal.CancelledAt!.Value.ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture) : "ended " + terminal.EventEndsAt!.Value.ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture)), selected.When);
        }
        routes["id"] = past.Id;
        var super = await service.GetAdminDesignAsync(User("SuperAdmin"), routes, CancellationToken.None);
        var hiddenOption = Assert.Single(super.Events, item => item.Hidden);
        Assert.Equal(hidden.Id, hiddenOption.Id);
        Assert.Equal($"/Admin/Events/Manage/{hidden.Id}?hidden=true", hiddenOption.Url);
        Assert.Equal(new[] { review.Id, live.Id, hidden.Id, closed.Id, open.Id, draft.Id, unscheduled.Id }, super.Events.Select(item => item.Id));
        routes["id"] = hidden.Id;
        Assert.Null((await service.GetAdminDesignAsync(User("Admin"), routes, CancellationToken.None)).SelectedEvent);
        Assert.Equal(hidden.Id, (await service.GetAdminDesignAsync(User("SuperAdmin"), routes, CancellationToken.None)).SelectedEvent!.Id);
        Assert.Equal($"/Admin/Review/Index?eventId={draft.Id}", SharedShellService.AdminDesignEventUrl("/Admin/Review/Index", draft.Id));
    }

    [Fact]
    public async Task SwitcherInvalidStoredTimezoneUsesUtcAndIdentityStillRenders()
    {
        var admin = Admin(); var item = Event(admin, EventState.Draft, "Invalid timezone fixture", 2);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, item);
            db.Entry(item).Property(value => value.Timezone).CurrentValue = "Unsupported/Fixture";
            await db.SaveChangesAsync();
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var persisted = await db.Events.SingleAsync();
            Assert.Equal("Unsupported/Fixture", persisted.Timezone);
            var service = new SharedShellService(db, new Text(), new ShellClock());
            var shell = await service.GetAdminDesignAsync(User("Admin"), new RouteValueDictionary { ["page"] = "/Admin/Events/Identity", ["id"] = item.Id }, CancellationToken.None);
            Assert.Equal("starts " + persisted.EventStartsAt!.Value.ToUniversalTime().ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture), Assert.Single(shell.Events).When);
        }
        await using var factory = IdentityFactory(); using var client = await IdentityClientAsync(factory);
        using var response = await client.GetAsync($"/Admin/Events/Identity/{item.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-admin-design", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SwitcherConvertsCopenhagenDatesAcrossSpringDst()
    {
        var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-GB");
        try
        {
            var admin = Admin(); await using var db = new ApplicationDbContext(options); db.Add(admin);
            foreach (var day in new[] { 27, 28 })
            {
                var start = new DateTimeOffset(2027, 3, day, 22, 30, 0, TimeSpan.Zero);
                var item = new BingoEvent(Guid.NewGuid(), $"DST fixture {day}", $"dst-{day}", "Europe/Copenhagen", admin.Id, Now, PlacementRule.LegacyScoreTimeThenEhb);
                item.ConfigureSchedule(start.AddDays(-3), start.AddDays(-2), null, start, start.AddDays(1), 10);
                db.Add(item);
            }
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var persisted = await db.Events.OrderBy(item => item.EventStartsAt).ToListAsync();
            Assert.All(persisted, item => Assert.Equal("Europe/Copenhagen", item.Timezone));
            Assert.Equal("27,28", string.Join(',', persisted.Select(item => item.EventStartsAt!.Value.UtcDateTime.Day)));
            var service = new SharedShellService(db, new Text(), new ShellClock());
            var shell = await service.GetAdminDesignAsync(User("Admin"), new RouteValueDictionary { ["page"] = "/Admin/Events/Identity" }, CancellationToken.None);
            Assert.Equal("starts 27 Mar|starts 29 Mar", string.Join('|', shell.Events.Select(item => item.When)));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previousCulture; }
    }

    [Fact]
    public async Task BoundIdentityUsesNewLayoutAndRendersLocalAssetsTokenAndTempData()
    {
        var admin = Admin();
        var item = Event(admin, EventState.Draft, "Shell fixture", 2);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, item);
            db.Add(new PersonalNotification(Guid.NewGuid(), admin.Id, "account.admin_granted", "", "/Account/Settings", Now));
            await db.SaveChangesAsync();
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Testing").UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.Configure<RazorPagesOptions>(settings => settings.Conventions.AddPageApplicationModelConvention("/Admin/Events/Identity", model =>
                {
                    model.EndpointMetadata.Add(new AdminDesignAttribute());
                    model.Filters.Add(new FixtureToast());
                }));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var login = await client.GetStringAsync("/Account/Login");
        using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = admin.LoginName, ["Input.Password"] = "synthetic-shell-password",
            ["__RequestVerificationToken"] = Regex.Match(login, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        var html = await client.GetStringAsync($"/Admin/Events/Identity/{item.Id}");
        Assert.Contains("<title>Identity · DK Legacy Admin</title>", System.Net.WebUtility.HtmlDecode(html));
        Assert.Contains("<span class=\"brand-name\">shell-admin", html);
        Assert.Contains("@shell-admin · Administrator", System.Net.WebUtility.HtmlDecode(html));
        Assert.Contains("class=\"crumb-btn\"", html); Assert.Contains("class=\"crumb-sep\"", html); Assert.Contains("class=\"crumb-cur\"", html);
        Assert.Contains("role=\"menuitemradio\" aria-checked=\"true\"", html);
        Assert.Contains("class=\"ic menu-check\"", html);
        Assert.Contains("data-admin-design", html);
        Assert.Contains("data-shell-antiforgery", html);
        Assert.Contains("This event is read-only in its current lifecycle state.", html);
        Assert.Matches("class=\"toast(?: [^\"]*)?\"[^>]*data-toast", html);
        Assert.Contains("/notifications#admin-actions-heading", html);
        Assert.Contains("aria-label=\"Notifications, 1 unread\"", html);
        Assert.Contains("class=\"design-notification-count\" aria-hidden=\"true\">1</span>", html);
        Assert.Contains("<span class=\"crumb-mid\">Shell fixture</span>", html);
        Assert.DoesNotContain("<a class=\"crumb-mid\"", html);
        Assert.Contains("Teams / Draft", html); Assert.Matches("src=\"/images/branding/dk-legacy-admin-mark(?:\\.[A-Za-z0-9_-]+)?\\.png(?:\\?v=[A-Za-z0-9_-]+)?\"", html);
        Assert.Contains("data-page-loading-template=\"identity\"", html); Assert.Contains("aria-label=\"Loading identity\"", html);
        Assert.Contains($"/Admin/Events/Identity/{item.Id}", html);
        foreach (Match link in Regex.Matches(html, "<link[^>]+href=\"([^\"]+)\"")) Assert.StartsWith("/", link.Groups[1].Value);
        Assert.DoesNotContain("fonts.googleapis", html);
        Assert.DoesNotContain("bootstrap.min", html);
        Assert.DoesNotContain("jquery", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("flatpickr", html, StringComparison.OrdinalIgnoreCase);
        var directory = await client.GetStringAsync("/Admin/Events/Index");
        Assert.Contains("data-admin-design", directory);
        Assert.DoesNotContain("admin-shell-body", directory);
        var dashboard = await client.GetStringAsync("/Admin");
        Assert.Contains("data-admin-design", dashboard);
        Assert.DoesNotContain("admin-shell-body", dashboard);
        var old = await client.GetStringAsync($"/Admin/Events/Schedule/{item.Id}");
        Assert.DoesNotContain("data-admin-design", old);
        Assert.Contains("admin-shell-body", old);
        Assert.True(AdminDesignAttribute.AppliesTo(new CompiledPageActionDescriptor { ModelTypeInfo = typeof(Bingo.Web.Pages.Admin.Events.IdentityModel).GetTypeInfo() }));
        Assert.False(AdminDesignAttribute.AppliesTo(new CompiledPageActionDescriptor { ModelTypeInfo = typeof(Bingo.Web.Pages.Admin.Events.ScheduleModel).GetTypeInfo() }));
    }

    [Fact]
    public async Task NewShellWithoutSelectedEventHidesEventNavigation()
    {
        var admin = Admin(); await using (var db = new ApplicationDbContext(options)) { db.Add(admin); await db.SaveChangesAsync(); }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Testing").UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>(); services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.Configure<RazorPagesOptions>(settings => settings.Conventions.AddPageApplicationModelConvention("/Admin/Events/Index", model => model.EndpointMetadata.Add(new AdminDesignAttribute())));
            }));
        using var client = await IdentityClientAsync(factory);
        var html = await client.GetStringAsync("/Admin/Events/Index");
        var nav = Regex.Match(html, "<nav[^>]*data-shell-event-context[^>]*>(.*?)</nav>", RegexOptions.Singleline).Groups[1].Value;
        Assert.NotEmpty(nav); Assert.Contains("Select an event", nav); Assert.DoesNotContain("<a ", nav);
        Assert.Contains("aria-label=\"Notifications, 0 unread\"", html); Assert.DoesNotContain("class=\"design-notification-count\"", html);
    }

    private static ClaimsPrincipal User(string role) => new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "fixture"));
    private static Account Admin()
    {
        var admin = Account.CreateWebsite(Guid.NewGuid(), "shell-admin", "SHELL-ADMIN", Now);
        admin.SetGlobalRole(GlobalRole.Admin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "synthetic-shell-password"), false, Now, incrementVersion: false);
        return admin;
    }
    private static BingoEvent Event(Account admin, EventState state, string name, int day)
    {
        var start = Now.AddDays(day);
        var item = new BingoEvent(Guid.NewGuid(), name, $"shell-{Guid.NewGuid():N}", "UTC", admin.Id, Now.AddDays(-30), PlacementRule.LegacyScoreTimeThenEhb);
        item.ConfigureSchedule(start.AddDays(-3), start.AddDays(-2), null, start, start.AddDays(1), 10);
        item.ConfigureSignup(true, false, null);
        if (state == EventState.Cancelled) { item.Cancel(admin.Id, start.AddDays(1), "Fixture", true); return item; }
        if (state == EventState.Draft) return item;
        item.OpenSignups(start.AddDays(-3)); if (state == EventState.SignupOpen) return item;
        item.CloseSignups(start.AddDays(-2)); if (state == EventState.SignupClosed) return item;
        item.StartEvent(start); if (state == EventState.Live) return item;
        item.EndEvent(start.AddDays(1)); if (state == EventState.AwaitingFinalReview) return item;
        item.FinalizeResults(start.AddDays(2)); if (state == EventState.Archived) item.Archive(start.AddDays(3));
        return item;
    }
    private sealed class FixtureToast : IAsyncPageFilter
    {
        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;
        public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            ((PageModel)context.HandlerInstance).TempData["StatusMessage"] = "This event is read-only in its current lifecycle state.";
            await next();
        }
    }
    private sealed class ShellClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }

    private sealed class Text : IStringLocalizer<Bingo.Web.SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name.Replace("AdminDesign.", string.Empty, StringComparison.Ordinal), arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
