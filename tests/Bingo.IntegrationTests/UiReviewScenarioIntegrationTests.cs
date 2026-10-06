using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Bingo.Application.Dashboard;
using Bingo.Application.Evidence;
using Bingo.Infrastructure.Events;
using Bingo.Web.Navigation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Routing;
using Xunit.Abstractions;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Catalogue;
using Bingo.Web.TestData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class UiReviewScenarioIntegrationTests(ITestOutputHelper output) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 6, 12, 34, 56, TimeSpan.Zero).AddTicks(1234567);
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_ur_tests").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private readonly string evidence = Path.Combine(Path.GetTempPath(), "bingo-ur-tests-" + Guid.NewGuid());
    public Task InitializeAsync() => database.StartAsync();
    public async Task DisposeAsync()
    {
        await database.DisposeAsync();
        if (Directory.Exists(evidence)) Directory.Delete(evidence, recursive: true);
    }

    [Theory]
    [InlineData("live", EventState.Live)]
    [InlineData("final-review", EventState.AwaitingFinalReview)]
    public async Task ScenariosRespectRealLifecycleAndPublishedEvidenceBoundaries(string profile, EventState currentState)
    {
        await using var factory = Factory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        await scope.ServiceProvider.GetRequiredService<CatalogueSnapshotService>()
            .ApplyAsync(Path.Combine(environment.ContentRootPath, CatalogueSnapshotService.DefaultRelativePath));
        var result = await scope.ServiceProvider.GetRequiredService<UiReviewScenarioSeeder>().SeedAsync(profile);
        db.ChangeTracker.Clear();
        var events = await db.Events.AsNoTracking().ToListAsync();
        var current = Assert.Single(events, value => !value.IsHidden && value.State is EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized);
        Assert.Equal(currentState, current.State);
        Assert.Equal(result.CurrentEventId, current.Id);
        Assert.All(events, value => Assert.False(value.IsDevelopmentFixture));
        Assert.All(events.Where(value => value.ActualSignupOpenedAt is not null), value => Assert.Equal(value.ActualSignupOpenedAt, value.FirstPublicAt));
        Assert.All(events.Where(value => value.IsHidden), value => Assert.Contains(value.State, new[] { EventState.AwaitingFinalReview, EventState.Finalized, EventState.Archived }));
        Assert.Equal(3, events.Count(value => value.IsHidden));
        Assert.Contains(events, value => value.IsHidden && value.State == EventState.Finalized);
        Assert.All(await db.Accounts.ToListAsync(), value => Assert.False(value.MustChangePassword));
        Assert.Equal(11, await db.Accounts.CountAsync());
        var plainWebsite = await db.Accounts.SingleAsync(value => value.LoginName == "ReviewWebsite");
        Assert.False(await db.EventParticipants.AnyAsync(value => value.AccountId == plainWebsite.Id));
        Assert.True(events.Count(value => !value.IsHidden && value.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed) > 8);
        var expected = new DateTimeOffset(Now.Ticks - Now.Ticks % 10, TimeSpan.Zero);
        Assert.Equal(expected, result.BuiltAt);
        Assert.Equal(expected.AddDays(-90), current.CreatedAt);
        Assert.Equal(expected, await db.Accounts.Where(value => value.LoginName == "ReviewOwner").Select(value => value.PasswordChangedAt).SingleAsync());
        var closed = events.Single(value => value.Slug == "ur-signups-closed");
        var readiness = await scope.ServiceProvider.GetRequiredService<IEventLifecycleService>().GetStartReadinessAsync(closed.Id);
        Assert.Contains(readiness!.Blockers, value => value.Code == "CURRENT_EVENT_EXISTS");
        var owner = await db.Accounts.SingleAsync(value => value.LoginName == "ReviewOwner");
        var refusal = await scope.ServiceProvider.GetRequiredService<ISubmissionService>().ApproveAsync(result.BlockedSubmissionId, owner.Id);
        Assert.NotNull(refusal.BlockingSubmission);
        Assert.Equal(0, refusal.ApprovedContribution);
        var lifecycle = new EventLifecycleService(db, scope.ServiceProvider.GetRequiredService<IEventSignupLifecycleService>(), new ReviewClock());
        var deniedStart = await lifecycle.StartNowAsync(closed.Id, closed.Version, true, "Synthetic boundary check", new LifecycleActor(owner.Id, owner.LoginName));
        Assert.False(deniedStart.Succeeded);
        Assert.Contains(deniedStart.Blockers!, value => value.Code == "CURRENT_EVENT_EXISTS");
        Assert.False(await db.SubmissionContributions.AnyAsync());
        Assert.True((await db.Boards.SingleAsync(value => value.EventId == current.Id)).PublishedCorrectionInProgress);
        Assert.Equal(2, await db.DraftPublicationRosters.Where(value => db.Teams.Any(team => team.Id == value.TeamId && team.EventId == current.Id && team.AffiliationName != null)).Select(value => value.TeamId).Distinct().CountAsync());
        Assert.True(await db.TeamMemberships.AnyAsync(value => value.LeftAt != null));
        Assert.Equal(3, await db.AuditEntries.CountAsync(value => value.Action == "event.hidden"));
        var outcomes = await db.EventCompetitionSynchronizations.Select(value => value.EndUpdateStatus).ToListAsync();
        Assert.Contains(EventCompetitionEndUpdateStatus.Pending, outcomes);
        Assert.Contains(EventCompetitionEndUpdateStatus.Rejected, outcomes);
        Assert.Contains(EventCompetitionEndUpdateStatus.CouldNotUpdate, outcomes);
        var shell = scope.ServiceProvider.GetRequiredService<SharedShellService>();
        foreach (var username in new[] { "ReviewOwner", "ReviewAdmin" })
        {
            var account = await db.Accounts.SingleAsync(value => value.LoginName == username);
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()), new Claim(ClaimTypes.Role, account.GlobalRole!.Value.ToString()) }, "review-proof"));
            var navigation = await shell.GetAsync(user, new RouteValueDictionary { ["page"] = "/Admin/Index" }, CancellationToken.None);
            Assert.Equal(current.Id, navigation.CurrentEvent!.EventId);
            var dashboard = await scope.ServiceProvider.GetRequiredService<IAdminDashboardService>().GetAsync(account.Id);
            // Existing Dashboard card selects Live, otherwise the next preparation event (PR Dashboard contract).
            var dashboardId = currentState == EventState.Live ? current.Id : events.Single(value => value.Slug == "ur-draft").Id;
            Assert.Equal(dashboardId, dashboard.CurrentEvent!.EventId);
            Assert.Equal(3L, dashboard.Statistics.EventsHeld.Value); // One visible current plus two visible archived events.
            Assert.DoesNotContain(dashboard.History, value => value.EventId == result.DiscardedEventId);
            Assert.DoesNotContain(dashboard.ParticipationChart, value => value.EventId == result.DiscardedEventId);
            var populations = await scope.ServiceProvider.GetRequiredService<IAdminDashboardService>()
                .GetEventParticipationAsync(account.Id, events.Select(value => value.Id).ToArray());
            Assert.False(populations.ContainsKey(result.DiscardedEventId));
            Assert.DoesNotContain(events.Where(value => value.IsHidden).Select(value => value.Id), value => value == dashboard.CurrentEvent.EventId);
            var design = await shell.GetAdminDesignAsync(user, new RouteValueDictionary { ["page"] = "/Admin/Index" }, CancellationToken.None);
            Assert.DoesNotContain(design.Events, value => value.Id == result.DiscardedEventId);
            if (username == "ReviewAdmin") Assert.DoesNotContain(design.Events, value => value.Hidden);
        }
        var attention = await shell.GetAdminActionsAsync(CancellationToken.None);
        Assert.DoesNotContain(result.DiscardedEventId, attention.EventIds);
        Assert.False(attention.ActionsByEvent.ContainsKey(result.DiscardedEventId));
        Assert.DoesNotContain(attention.Items, value => value.Url.Contains(result.DiscardedEventId.ToString(), StringComparison.Ordinal));
        var dashboardPage = ActivatorUtilities.CreateInstance<Bingo.Web.Pages.Admin.IndexModel>(scope.ServiceProvider);
        await dashboardPage.OnGetAsync(CancellationToken.None);
        Assert.DoesNotContain(dashboardPage.ActiveEvents, value => value.Id == result.DiscardedEventId);
        Assert.DoesNotContain(dashboardPage.AttentionEvents, value => value.Id == result.DiscardedEventId);
        Assert.DoesNotContain(dashboardPage.LifecycleReadiness, value => value.Id == result.DiscardedEventId);
        Assert.DoesNotContain(dashboardPage.PendingEvidence, value => value.EventId == result.DiscardedEventId);
        Assert.DoesNotContain(dashboardPage.UpcomingMilestones, value => value.EventId == result.DiscardedEventId);
        var discardAudit = await db.AuditEntries.SingleAsync(value => value.EventId == result.DiscardedEventId && value.Action == "event.discarded");
        Assert.Contains(dashboardPage.RecentAudits, value => value.Entry.Id == discardAudit.Id);
        using var localWom = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("WiseOldMan");
        using var localResponse = await localWom.GetAsync("players/Ur%20Participant");
        Assert.Equal(HttpStatusCode.OK, localResponse.StatusCode); // 127.0.0.1:1 is unserved: only the local handler can answer.
        await AssertPrintedUrlsAsync(factory, result, discardAudit.Id);

    }

    private async Task AssertPrintedUrlsAsync(WebApplicationFactory<Program> factory, UiReviewScenarios scenarios, Guid discardAuditId)
    {
        var links = UiReviewScenarioCatalogue.Links(scenarios, new Uri("http://127.0.0.1:5310"));
        var clients = new Dictionary<string, HttpClient>(StringComparer.Ordinal);
        var authCookies = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var account in scenarios.Accounts)
            {
                var login = await LoginAsync(factory, account.Username, account.Disabled);
                clients.Add(account.Username, login.Client);
                authCookies.Add(account.Username, login.AuthCookie);
            }
            foreach (var link in links)
            {
                using var response = await clients[link.Username].GetAsync(new Uri(link.Url).PathAndQuery);
                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{scenarios.Profile}: {link.Username} GET {link.Url} returned {(int)response.StatusCode}");
                if (link.Hidden)
                {
                    using var refused = await clients["ReviewAdmin"].GetAsync(new Uri(link.Url).PathAndQuery);
                    Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
                }
            }
            var discardedName = scenarios.Events.Single(value => value.Id == scenarios.DiscardedEventId).Name;
            var hidden = scenarios.Events.Where(value => value.Hidden).ToArray();
            foreach (var username in new[] { "ReviewAdmin", "ReviewOwner", "ReviewParticipant", "ReviewWebsite" })
            {
                var routes = username is "ReviewAdmin" or "ReviewOwner"
                    ? new[] { "/Admin/Events/Index", "/Admin/Index", "/Account/MyEvents" }
                    : new[] { "/", "/Account/MyEvents" };
                foreach (var route in routes)
                {
                    var html = WebUtility.HtmlDecode(await clients[username].GetStringAsync(route));
                    if (route == "/Admin/Index")
                    {
                        var audit = Regex.Match(html, "<section[^>]*aria-labelledby=\"recent-audit-heading\".*?</section>", RegexOptions.Singleline).Value;
                        Assert.Contains(discardedName, audit, StringComparison.Ordinal);
                        Assert.Contains($"data-audit-entry=\"{discardAuditId}\"", audit, StringComparison.Ordinal);
                        Assert.DoesNotContain($"/Admin/Events/Identity/{scenarios.DiscardedEventId}", audit, StringComparison.Ordinal);
                        Assert.DoesNotContain(discardedName, html.Replace(audit, string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
                    }
                    else Assert.DoesNotContain(discardedName, html, StringComparison.Ordinal);
                    if (username != "ReviewOwner")
                        Assert.All(hidden, value => Assert.DoesNotContain(value.Name, html, StringComparison.Ordinal));
                }
                if (username is "ReviewAdmin" or "ReviewOwner")
                {
                    // Bypass the fixture cookie jar so its earlier valid selection cannot replace this stale cookie.
                    using var staleClient = factory.CreateClient(new WebApplicationFactoryClientOptions
                    { AllowAutoRedirect = false, HandleCookies = false, BaseAddress = new Uri("https://localhost") });
                    staleClient.DefaultRequestHeaders.Add("Cookie", $"{authCookies[username]}; {AdminEventSession.CookieName}={scenarios.DiscardedEventId:D}");
                    using var remembered = await staleClient.GetAsync("/Admin/Index");
                    Assert.Equal(HttpStatusCode.OK, remembered.StatusCode);
                    Assert.Contains(remembered.Headers.GetValues("Set-Cookie"), value => value.StartsWith(AdminEventSession.CookieName + "=;", StringComparison.Ordinal));
                    Assert.DoesNotContain($"data-selected-event-id=\"{scenarios.DiscardedEventId}\"", await remembered.Content.ReadAsStringAsync(), StringComparison.Ordinal);
                    var auditRoute = $"/Admin/Audit/Index?eventId={scenarios.DiscardedEventId}";
                    var auditHtml = await clients[username].GetStringAsync(auditRoute);
                    Assert.Contains($"data-audit-entry=\"{discardAuditId}\"", auditHtml, StringComparison.Ordinal);
                    var auditLink = WebUtility.HtmlDecode(Regex.Match(auditHtml, "class=\"admin-audit-entry-link\"[^>]*href=\"([^\"]+)\"").Groups[1].Value);
                    Assert.NotEmpty(auditLink);
                    using var auditEntryResponse = await clients[username].GetAsync(auditLink);
                    Assert.Equal(HttpStatusCode.OK, auditEntryResponse.StatusCode);
                    output.WriteLine($"{scenarios.Profile} {username}: discard audit link {auditLink} => {(int)auditEntryResponse.StatusCode}; Dashboard discard audit has no event link.");
                    using var discarded = await clients[username].GetAsync($"/Admin/Events/Identity/{scenarios.DiscardedEventId}");
                    Assert.Equal(HttpStatusCode.NotFound, discarded.StatusCode);
                }
            }
            var discardedScenario = scenarios.Events.Single(value => value.Id == scenarios.DiscardedEventId);
            foreach (var route in new[] { $"/Events/{discardedScenario.Slug}/Board", $"/Events/{discardedScenario.Slug}/Teams" })
            {
                using var refused = await clients["ReviewWebsite"].GetAsync(route);
                Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
            }
            foreach (var item in hidden)
            {
                foreach (var route in new[] { $"/Events/{item.Slug}/Board", $"/Events/{item.Slug}/Teams", $"/Admin/Events/Identity/{item.Id}" })
                {
                    using var refused = await clients["ReviewAdmin"].GetAsync(route);
                    Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
                }
            }
        }
        finally { foreach (var client in clients.Values) client.Dispose(); }
    }

    private static async Task<(HttpClient Client, string AuthCookie)> LoginAsync(WebApplicationFactory<Program> factory, string username, bool disabled)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var html = await client.GetStringAsync("/Account/Login");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(token);
        using var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Input.Username"] = username, ["Input.Password"] = UiReviewScenarioSeeder.Password, ["__RequestVerificationToken"] = token }));
        Assert.Equal(disabled ? HttpStatusCode.OK : HttpStatusCode.Redirect, login.StatusCode);
        Assert.DoesNotContain("ChangePassword", login.Headers.Location?.ToString() ?? string.Empty, StringComparison.Ordinal);
        if (disabled)
        {
            using var protectedPage = await client.GetAsync("/Account/MyEvents");
            Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
            Assert.Contains("/Account/Login", protectedPage.Headers.Location!.ToString(), StringComparison.Ordinal);
        }
        var cookieName = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme).Cookie.Name;
        var authCookie = disabled ? string.Empty : Assert.Single(login.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(cookieName + "=", StringComparison.Ordinal)).Split(';')[0];
        return (client, authCookie);
    }

    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
        .UseEnvironment("Development")
        .UseSetting("ConnectionStrings:Database", database.GetConnectionString())
        .UseSetting("DevelopmentAdminBootstrap:Enabled", "false")
        .UseSetting("EvidenceStorage:Provider", "Local")
        .UseSetting("EvidenceStorage:LocalPath", evidence)
        .UseSetting("WiseOldMan:DevelopmentFake:Enabled", "true")
        .UseSetting("WiseOldMan:BaseUrl", "http://127.0.0.1:1/")
        .ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new ReviewClock());
            services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, settings => settings.TimeProvider = TimeProvider.System);
        }));
    private sealed class ReviewClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
