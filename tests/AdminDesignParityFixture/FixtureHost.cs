using System.Globalization;
using Bingo.Testing;
using System.Data.Common;
using Npgsql;
using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Text.Json;
using Bingo.Domain.Access;
using Bingo.Application.Dashboard;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Catalogue;
using Bingo.Web.TestData;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace Bingo.AdminDesignParityFixture;

internal static class FixtureHost
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2027-06-02T12:00:00Z", CultureInfo.InvariantCulture);
    public static async Task Main()
    {
        var root = Environment.GetEnvironmentVariable("BINGO_PARITY_ROOT") ?? throw new InvalidOperationException("Set BINGO_PARITY_ROOT to the tested checkout.");
        var urProfile = Environment.GetEnvironmentVariable("BINGO_PARITY_UR_PROFILE");
        if (urProfile is not null && urProfile is not ("live" or "final-review")) throw new InvalidOperationException("Unknown controlled UR profile.");
        await using var database = new PostgreSqlBuilder("postgres:17-alpine").WithLoopbackPort().WithDatabase("bingo_parity").WithUsername("bingo").WithPassword("synthetic_parity_database").Build();
        await database.StartAsync();
        // Planner ruling58-1: pg_isready inside the container does not establish
        // that its published TCP endpoint accepts this fixture's credentials yet.
        using (var readiness = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
        {
            var attempt = 0;
            while (true)
            {
                Console.WriteLine($"PARITY_DATABASE_READY_ATTEMPT {++attempt}");
                try
                {
                    await using var connection = new NpgsqlConnection(database.GetOwnedConnectionString());
                    await connection.OpenAsync(readiness.Token);
                    await using var command = new NpgsqlCommand("SELECT 1", connection);
                    await command.ExecuteScalarAsync(readiness.Token);
                    Console.WriteLine("PARITY_DATABASE_READY");
                    break;
                }
                catch (Exception error) when (error is NpgsqlException or TimeoutException or OperationCanceledException)
                {
                    Console.Error.WriteLine($"PARITY_DATABASE_WAIT {error.GetType().Name}: {error.Message}");
                    if (readiness.IsCancellationRequested)
                        throw new InvalidOperationException("Parity fixture environment failure: owned PostgreSQL TCP connection unavailable after 60 seconds.", error);
                    try { await Task.Delay(500, readiness.Token); }
                    catch (OperationCanceledException) { throw new InvalidOperationException("Parity fixture environment failure: owned PostgreSQL TCP connection unavailable after 60 seconds.", error); }
                }
            }
        }
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetOwnedConnectionString()).Options;
        var account = Account.CreateWebsite(Guid.NewGuid(), "parity-admin", "PARITY-ADMIN", Now.AddYears(-1));
        var directoryFault = Environment.GetEnvironmentVariable("BINGO_PARITY_DIRECTORY_FAULT") == "1";
        account.SetGlobalRole(directoryFault ? GlobalRole.SuperAdmin : GlobalRole.Admin);
        var password = "Synthetic-parity-password-2026";
        account.SetPassword(new PasswordHasher<Account>().HashPassword(account, password), false, Now, incrementVersion: false);
        var ids = new Dictionary<string, Guid>();
        await using (var db = new ApplicationDbContext(options))
        {
            await db.Database.MigrateAsync();
            if (urProfile is null)
            {
            db.Add(account);
            void Add(string slug, string name, EventState state, string timezone, string? description, string? buyIn, string? opens, string? closes, string? draft, string? starts, string? ends)
            {
                static DateTimeOffset? At(string? value) => value is null ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
                var item = new BingoEvent(Guid.NewGuid(), name, slug, timezone, account.Id, Now.AddYears(-1), PlacementRule.LegacyScoreTimeThenEhb);
                item.UpdateIdentity(name, slug, description, buyIn, timezone);
                item.ConfigureInitialSchedule(At(opens), At(closes), At(draft), At(starts), At(ends), null);
                item.ConfigureSignup(true, false, null);
                if (state == EventState.Cancelled) item.Cancel(account.Id, Now, "Controlled parity fixture", true);
                else if (state != EventState.Draft)
                {
                    item.OpenSignups(At(opens)!.Value);
                    if (state != EventState.SignupOpen)
                    {
                        item.CloseSignups(At(closes)!.Value);
                        if (state != EventState.SignupClosed)
                        {
                            item.StartEvent(At(starts)!.Value);
                            if (state != EventState.Live)
                            {
                                item.EndEvent(At(ends)!.Value);
                                if (state != EventState.AwaitingFinalReview) { item.FinalizeResults(At(ends)!.Value.AddHours(1)); if (state == EventState.Archived) item.Archive(At(ends)!.Value.AddHours(2)); }
                            }
                        }
                    }
                }
                if (state != EventState.Draft) item.MarkFirstPublic(At(opens) ?? Now.AddMonths(-1));
                db.Add(item); ids[slug] = item.Id;
            }
            Add("autumn-bingo-2027", "Autumn Bingo 2027", EventState.SignupOpen, "Europe/Copenhagen", "Nine days of team bingo across PvM, skilling and clues. Captains draft teams on 20 June, and every tile needs screenshot evidence.\n\nAll accounts are welcome; ironmen play on their own boards.", "5M GP per player, sent to Nils in game before the draft. The whole pot goes to the winning team.", "2027-06-01T16:00:00Z", "2027-06-15T18:00:00Z", "2027-06-20T17:00:00Z", "2027-06-27T16:00:00Z", "2027-07-06T20:00:00Z");
            Add("winter-bingo-2027", "Winter Bingo 2027", EventState.Draft, "Europe/Copenhagen", null, null, "2027-10-01T16:00:00Z", null, null, "2027-11-26T18:00:00Z", "2027-12-05T21:00:00Z");
            Add("clan-cup-pvm-week", "Clan Cup: PvM Week", EventState.SignupClosed, "UTC", "A week of boss kills against the clock. Teams of eight, one board, bragging rights.", null, "2027-05-20T18:00:00Z", "2027-06-10T18:00:00Z", null, "2027-06-14T18:00:00Z", "2027-06-20T22:00:00Z");
            Add("midsummer-skilling-sprint", Environment.GetEnvironmentVariable("BINGO_PARITY_NARROW_CARD") == "1" ? "Cup" : "Midsummer Skilling Sprint", EventState.Live, "Europe/Copenhagen", "Ten days of skilling tiles. No PvM, no excuses.", "Free to enter.", "2027-05-10T16:00:00Z", "2027-05-25T18:00:00Z", "2027-05-26T17:00:00Z", "2027-05-28T16:00:00Z", "2027-06-06T20:00:00Z");
            foreach (var state in new[] { EventState.Finalized, EventState.Archived, EventState.Cancelled })
                Add("spring-" + state.ToString().ToLowerInvariant(), "Spring Bingo 2027", state, "Europe/Copenhagen", "Seven teams, one board, a photo finish.", null, "2027-02-15T17:00:00Z", "2027-03-01T19:00:00Z", "2027-03-05T18:00:00Z", "2027-03-12T17:00:00Z", "2027-03-21T21:00:00Z");
            if (Environment.GetEnvironmentVariable("BINGO_PARITY_DOLLAR_NAME") is { } dollarName)
                Add("dollar-name", dollarName, EventState.Draft, "UTC", null, null, null, null, null, null, null);
            for (var i = 0; i < 15; i++) Add("scroll-" + i, "Scroll fixture " + i, EventState.Draft, "UTC", null, null, null, null, null, "2027-04-01T12:00:00Z", "2027-04-02T12:00:00Z");
            if (directoryFault)
            {
                Add("hidden-old", "Zulu older hidden", EventState.Finalized, "UTC", null, null, "2027-02-01T12:00:00Z", "2027-02-02T12:00:00Z", null, "2027-02-03T12:00:00Z", "2027-02-04T12:00:00Z");
                Add("hidden-new", "Alpha newer hidden", EventState.Finalized, "UTC", null, null, "2027-02-01T12:00:00Z", "2027-02-02T12:00:00Z", null, "2027-02-03T12:00:00Z", "2027-02-04T12:00:00Z");
                foreach (var entry in db.ChangeTracker.Entries<BingoEvent>().Where(entry => entry.Entity.Slug.StartsWith("hidden-", StringComparison.Ordinal)))
                    entry.Entity.Hide(account.Id, Now.AddDays(entry.Entity.Slug == "hidden-old" ? -2 : -1), entry.Entity.Name, "Controlled parity quarantine");
            }
            foreach (var item in db.ChangeTracker.Entries<BingoEvent>().Select(entry => entry.Entity)
                         .Where(item => item.ActualStartedAt is not null).ToArray())
            {
                var team = new Team(Guid.NewGuid(), item.Id, "Parity team", "parity-team", null, false, item.ActualStartedAt);
                var person = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1,
                    item.ActualStartedAt!.Value.AddDays(-1), SignupSource.AdminCreated);
                person.AssignOwner(account);
                db.AddRange(team, person, new TeamMembership(Guid.NewGuid(), team.Id, person.Id,
                    TeamMembershipRole.Participant, item.ActualStartedAt.Value, null, "Controlled parity membership"));
            }
            await db.SaveChangesAsync();
            }
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .ConfigureKestrel(settings => settings.Listen(IPAddress.Loopback, 0))
            .UseStaticWebAssets().UseEnvironment(urProfile is null ? "Testing" : "Development").UseContentRoot(Path.Combine(root, "src/Bingo.Web"))
            .UseSetting("ConnectionStrings:Database", database.GetOwnedConnectionString())
            .UseSetting("UiReviewEnvironment:Enabled", "false")
            .UseSetting("DevelopmentAdminBootstrap:Enabled", "false")
            .UseSetting("EvidenceStorage:Provider", "Local")
            .UseSetting("EvidenceStorage:LocalPath", Path.Combine(root, "artifacts", "u2-ur", urProfile ?? "parity", "evidence"))
            .UseSetting("CatalogueImageCache:LocalPath", Path.Combine(root, "artifacts", "u2-ur", urProfile ?? "parity", "catalogue-images"))
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>(); services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.AddSingleton<TimeProvider>(new FixtureClock());
                services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, settings =>
                {
                    settings.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    // Login stamps session expiry with wall time. Keep cookie validation on that
                    // same clock; FixtureClock remains authoritative for rendered event dates.
                    settings.TimeProvider = TimeProvider.System;
                });
                services.AddHttpClient("WiseOldMan").ConfigurePrimaryHttpMessageHandler(() => new NoProviderCalls());
                if (directoryFault) services.AddDbContext<ApplicationDbContext>(settings => settings.AddInterceptors(new AttentionFailure()));
            }));
        factory.UseKestrel(0);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var dashboardScope = factory.Services.CreateScope();
        if (urProfile is not null)
        {
            await dashboardScope.ServiceProvider.GetRequiredService<CatalogueSnapshotService>()
                .ApplyAsync(Path.Combine(root, "src/Bingo.Web", CatalogueSnapshotService.DefaultRelativePath));
            var scenarios = await dashboardScope.ServiceProvider.GetRequiredService<UiReviewScenarioSeeder>().SeedAsync(urProfile);
            foreach (var item in scenarios.Events) ids[item.Slug] = item.Id;
            account = await dashboardScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Accounts.SingleAsync(value => value.LoginName == "ReviewAdmin");
            password = UiReviewScenarioSeeder.Password;
        }
        var dashboard = await dashboardScope.ServiceProvider.GetRequiredService<IAdminDashboardService>().GetAsync(account.Id);
        var directory = ActivatorUtilities.CreateInstance<Bingo.Web.Pages.Admin.Events.IndexModel>(dashboardScope.ServiceProvider);
        directory.PageContext = new Microsoft.AspNetCore.Mvc.RazorPages.PageContext(new Microsoft.AspNetCore.Mvc.ActionContext(new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, account.Id.ToString())], "fixture"))
        }, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.RazorPages.PageActionDescriptor()));
        await directory.OnGetAsync(default);
        Console.WriteLine("PARITY_READY " + JsonSerializer.Serialize(new { origin = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single(), username = account.PublicUsername, password, events = ids, dashboard, directory = new { directory.Events } }));
        // Only this process owns the container; closing stdin disposes host and database.
        await Console.In.ReadLineAsync();
    }
    private sealed class FixtureClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class AttentionFailure : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM scheduled_signup_opening_attempts", StringComparison.Ordinal))
                throw new InvalidOperationException("Controlled read-only attention failure");
            return ValueTask.FromResult(result);
        }
    }
    private sealed class NoProviderCalls : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new InvalidOperationException("No live provider requests are allowed in the parity fixture.");
    }
}
