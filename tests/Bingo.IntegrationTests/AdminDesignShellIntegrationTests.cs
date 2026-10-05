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

public sealed partial class AdminDesignShellIntegrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_u1_shell").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;
    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
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
        var service = new SharedShellService(db, new Text(), null!, null!, null!, TimeProvider.System);
        var routes = new RouteValueDictionary { ["page"] = "/Admin/Events/Identity", ["id"] = past.Id };
        var ordinary = await service.GetAdminDesignAsync(User("Admin"), routes, CancellationToken.None);
        Assert.Equal(past.Id, ordinary.SelectedEvent!.Id);
        Assert.Equal(new[] { review.Id, live.Id, closed.Id, open.Id, draft.Id, unscheduled.Id }, ordinary.Events.Select(item => item.Id));
        Assert.All(ordinary.Events, item => Assert.Equal($"/Admin/Events/Identity/{item.Id}", item.Url));
        Assert.Equal("not announced", ordinary.Events[^1].When);
        Assert.Equal(new[] { "ends " + review.EventEndsAt!.Value.ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture), "ends " + live.EventEndsAt!.Value.ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture),
            "starts " + closed.EventStartsAt!.Value.ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture), "closes " + open.SignupClosesAt!.Value.ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture),
            "starts " + draft.EventStartsAt!.Value.ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture), "not announced" }, ordinary.Events.Select(item => item.When));
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
            var service = new SharedShellService(db, new Text(), null!, null!, null!, TimeProvider.System);
            var shell = await service.GetAdminDesignAsync(User("Admin"), new RouteValueDictionary { ["page"] = "/Admin/Events/Identity", ["id"] = item.Id }, CancellationToken.None);
            Assert.Equal("starts " + persisted.EventStartsAt!.Value.ToUniversalTime().ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture), Assert.Single(shell.Events).When);
        }
        await using var factory = IdentityFactory(); using var client = await IdentityClientAsync(factory);
        using var response = await client.GetAsync($"/Admin/Events/Identity/{item.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-admin-design", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BoundIdentityUsesNewLayoutAndRendersLocalAssetsTokenAndTempData()
    {
        var admin = Admin();
        var item = Event(admin, EventState.Draft, "Shell fixture", 2);
        await using (var db = new ApplicationDbContext(options)) { db.AddRange(admin, item); await db.SaveChangesAsync(); }
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
        Assert.Contains("data-admin-design", html);
        Assert.Contains("data-shell-antiforgery", html);
        Assert.Contains("This event is read-only in its current lifecycle state.", html);
        Assert.Matches("class=\"toast(?: [^\"]*)?\"[^>]*data-toast", html);
        Assert.Contains("/notifications#admin-actions-heading", html);
        Assert.Contains($"/Admin/Events/Identity/{item.Id}", html);
        foreach (Match link in Regex.Matches(html, "<link[^>]+href=\"([^\"]+)\"")) Assert.StartsWith("/", link.Groups[1].Value);
        Assert.DoesNotContain("fonts.googleapis", html);
        Assert.DoesNotContain("bootstrap.min", html);
        Assert.DoesNotContain("jquery", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("flatpickr", html, StringComparison.OrdinalIgnoreCase);
        var old = await client.GetStringAsync("/Admin/Events/Index");
        Assert.DoesNotContain("data-admin-design", old);
        Assert.Contains("admin-shell-body", old);
        Assert.True(AdminDesignAttribute.AppliesTo(new CompiledPageActionDescriptor { ModelTypeInfo = typeof(Bingo.Web.Pages.Admin.Events.IdentityModel).GetTypeInfo() }));
        Assert.False(AdminDesignAttribute.AppliesTo(new CompiledPageActionDescriptor { ModelTypeInfo = typeof(Bingo.Web.Pages.Admin.Events.ScheduleModel).GetTypeInfo() }));
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
    private sealed class Text : IStringLocalizer<Bingo.Web.SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
