using System.Net;
using System.Text.RegularExpressions;
using Bingo.Application.Dashboard;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bingo.BrowserTests;

[Collection(BrowserTestGroup.Name)]
public sealed class U2DashboardHttpTests(BrowserTestApplicationFactory factory)
{
    [Theory]
    [InlineData("en", "Participation by event", "No capacity set", "Not announced", "Unknown is not zero.", EventState.Draft, false, true, "tone-draft", "badge-neutral", "Imported · archived")]
    [InlineData("da", "Deltagelse pr. event", "Ingen kapacitet angivet", "Ikke annonceret", "Ukendt er ikke nul.", EventState.SignupOpen, true, false, "tone-open", "badge-warning", null)]
    [InlineData("en", "Participation by event", "No capacity set", "Not announced", "Unknown is not zero.", EventState.SignupClosed, false, false, "tone-closed", "badge-done", "Finalized")]
    [InlineData("en", "Participation by event", "No capacity set", "Not announced", "Unknown is not zero.", EventState.Live, true, false, "tone-live", "badge-warning", "Provisional")]
    [InlineData("en", "Participation by event", "No capacity set", "Not announced", "Unknown is not zero.", EventState.AwaitingFinalReview, true, false, "tone-review", "badge-warning", "Provisional")]
    public async Task UnknownMetricsAreLocalizedAndNeverExposeInfrastructureReasons(string culture, string chart, string capacity, string date, string unknown,
        EventState cardState, bool provisional, bool imported, string dotTone, string badgeClass, string? recapLabel)
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAdminDashboardService>();
            services.AddSingleton<IAdminDashboardService>(new UnknownDashboard(cardState, provisional, imported));
        }));
        var at = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var username = "u2-render-" + Guid.NewGuid().ToString("N");
        const string password = "U2-render-password-123!";
        var actor = Account.CreateWebsite(Guid.NewGuid(), username, username.ToUpperInvariant(), at);
        actor.SetGlobalRole(GlobalRole.Admin);
        actor.SetPassword(new PasswordHasher<Account>().HashPassword(actor, password), false, at, incrementVersion: false);
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Accounts.Add(actor);
            await db.SaveChangesAsync();
        }
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var login = await client.GetStringAsync("/Account/Login");
        var token = WebUtility.HtmlDecode(Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = username,
            ["Input.Password"] = password,
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        var shell = await client.GetStringAsync("/Admin");
        var languageToken = WebUtility.HtmlDecode(Regex.Match(shell, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        using var language = await client.PostAsync("/Language", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["culture"] = culture,
            ["returnUrl"] = "/Admin",
            ["__RequestVerificationToken"] = languageToken
        }));
        Assert.Equal(HttpStatusCode.Redirect, language.StatusCode);
        Assert.Contains(language.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.Culture=", StringComparison.Ordinal));
        foreach (var sort in Enum.GetValues<DashboardHistorySortField>())
        {
            using var result = await client.GetAsync($"/Admin?sort={sort}&direction=asc");
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            var html = WebUtility.HtmlDecode(await result.Content.ReadAsStringAsync());
            Assert.Contains(chart, html);
            Assert.Contains(capacity, html);
            Assert.Contains(date, html);
            Assert.Contains(unknown, html);
            Assert.Contains("data-dashboard", html);
            Assert.Contains($"class=\"dot {dotTone}\"", html);
            Assert.Contains($"class=\"badge {badgeClass}\"", html);
            if (recapLabel is not null) Assert.Contains($"class=\"badge {badgeClass}\">{recapLabel}</span>", html);
            Assert.Contains("aria-sort=\"ascending\"", html);
            Assert.DoesNotContain("RAW-INFRASTRUCTURE-REASON", html);
            Assert.DoesNotContain("admin-dashboard-retained", html);
        }
    }

    [Fact]
    public async Task AuthorityLostBetweenCookieValidationAndDashboardReadReturnsForbid()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAdminDashboardService>();
            services.AddSingleton<IAdminDashboardService, RefusingDashboard>();
        }));
        var at = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var username = "u2-" + Guid.NewGuid().ToString("N");
        const string password = "U2-test-password-123!";
        var actor = Account.CreateWebsite(Guid.NewGuid(), username, username.ToUpperInvariant(), at);
        actor.SetGlobalRole(GlobalRole.Admin);
        actor.SetPassword(new PasswordHasher<Account>().HashPassword(actor, password), false, at, incrementVersion: false);
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Accounts.Add(actor);
            await db.SaveChangesAsync();
        }
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var login = await client.GetStringAsync("/Account/Login");
        var token = WebUtility.HtmlDecode(Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        using var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = username,
            ["Input.Password"] = password,
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        using var result = await client.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Equal("/Account/AccessDenied", result.Headers.Location?.AbsolutePath);
    }

    private sealed class RefusingDashboard : IAdminDashboardService
    {
        public Task<AdminDashboardResult> GetAsync(Guid actorAccountId, CancellationToken cancellationToken = default) =>
            throw new UnauthorizedAccessException("Test role loss at the read boundary.");

        public Task<IReadOnlyDictionary<Guid, EventParticipationSummary>> GetEventParticipationAsync(Guid actorAccountId,
            IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Dashboard must not read the directory population.");
    }

    private sealed class UnknownDashboard(EventState cardState, bool provisional, bool imported) : IAdminDashboardService
    {
        public Task<AdminDashboardResult> GetAsync(Guid actorAccountId, CancellationToken cancellationToken = default)
        {
            var at = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
            var unknown = DashboardMetric<long>.Unknown("RAW-INFRASTRUCTURE-REASON");
            var rows = new[] { Guid.Parse("00000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000002") }
                .Select((id, i) => new DashboardHistoryRow(id, $"Fixture {i}", $"fixture-{i}", EventState.Live, true,
                    null, null, $"/Admin/Events/Manage/{id}", unknown, unknown, unknown, null,
                    new DashboardEhbSummary(null, DashboardEhbCoverage.Unavailable, 0, 0), [])
                { TeamCount = 3 }).ToArray();
            var points = rows.Select(row => new DashboardParticipationPoint(row.EventId, row.EventName, row.EventSlug,
                row.State, true, null, null, unknown, unknown, unknown, unknown, unknown)
            { TeamCount = 3 }).ToArray();
            return Task.FromResult(new AdminDashboardResult(at,
                new DashboardStatistics(DashboardMetric<long>.Measured(2), unknown, unknown, unknown, unknown, unknown)
                { Provisional = true, ProvisionalEvents = 2 }, points,
                new DashboardRecap(rows[0].EventId, rows[0].EventName, rows[0].EventSlug, at.AddDays(-2), at.AddDays(-1), unknown, unknown, null, [], rows[0].OverviewPath)
                { State = provisional ? EventState.AwaitingFinalReview : EventState.Finalized, Provisional = provisional, IsHistoricalImport = imported }, rows,
                new DashboardEventCard(rows[0].EventId, "Next fixture", "next-fixture", cardState, null, null, false, 0, 0, null, rows[0].OverviewPath),
                new DashboardCommunitySnapshot(DashboardMetric<long>.Measured(1), unknown, unknown, null, false)));
        }

        public Task<IReadOnlyDictionary<Guid, EventParticipationSummary>> GetEventParticipationAsync(Guid actorAccountId,
            IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Dashboard must not read directory populations.");
    }
}
