using System.Net;
using System.Text.RegularExpressions;
using Bingo.Application.Dashboard;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bingo.BrowserTests;

[Collection(BrowserTestGroup.Name)]
public sealed class U2DashboardHttpTests(BrowserTestApplicationFactory factory)
{
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
            ["Input.Username"] = username, ["Input.Password"] = password, ["__RequestVerificationToken"] = token
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
}
