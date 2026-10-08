using System.Data.Common;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class EventCreationRetryIntegrationTests(PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_creation_retry").WithUsername("bingo").WithPassword("bingo_test_password"));
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task ConcurrentSameKeyAndDifferentPayloadRetriesHaveOneAtomicOutcome()
    {
        var actor = Actor(await AccountAsync("retry-admin"));
        var key = Guid.NewGuid();
        await using var blocker = new ApplicationDbContext(options);
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        var identity = $"event-create:{actor.Id:N}:{key:N}";
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({identity}, 0))");
        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        await firstDb.Database.OpenConnectionAsync();
        await secondDb.Database.OpenConnectionAsync();
        var first = Service(firstDb).CreateAsync(key, " Same name ", " UTC ", actor);
        await AssertAdvisoryWaitAsync(firstDb);
        var second = Service(secondDb).CreateAsync(key, "Same name", "UTC", actor);
        await AssertAdvisoryWaitAsync(secondDb);
        await transaction.CommitAsync();
        var results = await Task.WhenAll(first, second);
        Assert.All(results, x => Assert.Equal(EventCreationOutcome.Completed, x.Outcome));
        Assert.Equal(results[0], results[1]);
        await AssertAggregateCountAsync(1);
        var before = await SnapshotAsync();
        await using var retryDb = new ApplicationDbContext(options);
        Assert.Equal(results[0], await Service(retryDb).CreateAsync(key, "Same name", "UTC", actor));
        Assert.Equal(EventCreationOutcome.Conflict, (await Service(retryDb).CreateAsync(key, "Other", "UTC", actor)).Outcome);
        Assert.Equal(EventCreationOutcome.Conflict, (await Service(retryDb).CreateAsync(key, "Same name", "Europe/Copenhagen", actor)).Outcome);
        Assert.Equal(before, await SnapshotAsync());
        var other = await Service(retryDb).CreateAsync(Guid.NewGuid(), "Same name", "UTC", actor);
        Assert.Equal(EventCreationOutcome.Completed, other.Outcome);
        Assert.NotEqual(results[0].EventId, other.EventId);
        await AssertAggregateCountAsync(2);
        Assert.Equal(2, await retryDb.Events.Select(x => x.Slug).Distinct().CountAsync());
    }

    [Fact]
    public async Task SlugAllocationProbesPastTenCollisionsAndKeepsDiscardedUnicodeSlugs()
    {
        var account = await AccountAsync("slug-exhaustion-admin");
        var actor = Actor(account);
        var results = new List<EventCreationResult>();
        for (var index = 0; index < 11; index++)
        {
            await using var create = new ApplicationDbContext(options);
            results.Add(await Service(create).CreateAsync(Guid.NewGuid(), "😀", "UTC", actor));
        }

        Assert.All(results, result => Assert.Equal(EventCreationOutcome.Completed, result.Outcome));
        Assert.Equal(11, results.Select(result => result.EventId).Distinct().Count());
        await using (var db = new ApplicationDbContext(options))
        {
            var slugs = await db.Events.AsNoTracking().OrderBy(item => item.Slug).Select(item => item.Slug).ToListAsync();
            Assert.Equal(["event", "event-10", "event-11", "event-2", "event-3", "event-4", "event-5", "event-6", "event-7", "event-8", "event-9"], slugs);
            var first = await db.Events.SingleAsync(item => item.Id == results[0].EventId);
            first.Discard(actor.Id, Now, protectedHistoryExists: false);
            await db.SaveChangesAsync();
        }

        await using var afterDiscardDb = new ApplicationDbContext(options);
        var afterDiscard = await Service(afterDiscardDb).CreateAsync(Guid.NewGuid(), "😀", "UTC", actor);
        Assert.Equal(EventCreationOutcome.Completed, afterDiscard.Outcome);
        Assert.Equal("event-12", await afterDiscardDb.Events.Where(item => item.Id == afterDiscard.EventId).Select(item => item.Slug).SingleAsync());
        await AssertAggregateCountAsync(12);
    }

    [Fact]
    public async Task LostCommitResponseCanBeReadBackAndRetriedWithoutAnotherCreation()
    {
        var actor = Actor(await AccountAsync("lost-admin"));
        var key = Guid.NewGuid();
        var lostOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString())
            .AddInterceptors(new LostCommitResponse()).Options;
        await using (var lostDb = new ApplicationDbContext(lostOptions))
            await Assert.ThrowsAsync<OperationCanceledException>(() => new EventCreationService(lostDb, new FixedClock(Now.AddTicks(7)))
                .CreateAsync(key, "Uncertain outcome", "UTC", actor));
        await using var recoveredDb = new ApplicationDbContext(options);
        var result = await Service(recoveredDb).CheckAgainAsync(key, actor);
        Assert.Equal(EventCreationOutcome.Completed, result.Outcome);
        var before = await SnapshotAsync();
        Assert.Equal(result, await Service(recoveredDb).CreateAsync(key, "Uncertain outcome", "UTC", actor));
        Assert.Equal(before, await SnapshotAsync());
        Assert.Equal(Now, (await recoveredDb.Events.SingleAsync()).CreatedAt);
        await AssertAggregateCountAsync(1);
    }

    [Fact]
    public async Task OriginalPayloadSurvivesRenameAndUnavailableResultsNeverFreeTheirKeys()
    {
        var account = await AccountAsync("retained-admin");
        var actor = Actor(account);
        var key = Guid.NewGuid();
        await using var db = new ApplicationDbContext(options);
        var result = await Service(db).CreateAsync(key, "Original", "UTC", actor);
        var item = await db.Events.SingleAsync();
        item.UpdateIdentity("Renamed", item.Slug, null, "Europe/Copenhagen");
        await db.SaveChangesAsync();
        Assert.Equal(result, await Service(db).CreateAsync(key, "Original", "UTC", actor));
        Assert.Equal(EventCreationOutcome.Conflict, (await Service(db).CreateAsync(key, "Renamed", "Europe/Copenhagen", actor)).Outcome);
        item.OpenSignups(Now.AddDays(-3));
        item.CloseSignups(Now.AddDays(-2));
        item.StartEvent(Now.AddDays(-1));
        item.EndEvent(Now);
        item.Hide(actor.Id, Now, item.Name, "Controlled visibility fixture");
        await db.SaveChangesAsync();
        Assert.Equal(EventCreationOutcome.NotFound, (await Service(db).CheckAgainAsync(key, actor)).Outcome);
        Assert.Equal(EventCreationOutcome.NotFound, (await Service(db).CreateAsync(key, "Original", "UTC", actor)).Outcome);
        var persistedAccount = await db.Accounts.SingleAsync(x => x.Id == actor.Id);
        persistedAccount.SetGlobalRole(GlobalRole.SuperAdmin);
        await db.SaveChangesAsync();
        Assert.Equal(result, await Service(db).CheckAgainAsync(key, actor));
        key = Guid.NewGuid();
        var disposable = await Service(db).CreateAsync(key, "Original", "UTC", actor);
        item = await db.Events.SingleAsync(x => x.Id == disposable.EventId);
        item.Discard(actor.Id, Now, protectedHistoryExists: false);
        await db.SaveChangesAsync();
        var before = await SnapshotAsync();
        Assert.Equal(EventCreationOutcome.NotFound, (await Service(db).CheckAgainAsync(key, actor)).Outcome);
        Assert.Equal(EventCreationOutcome.NotFound, (await Service(db).CreateAsync(key, "Original", "UTC", actor)).Outcome);
        Assert.Equal(before, await SnapshotAsync());
        await AssertAggregateCountAsync(2);
    }

    [Fact]
    public async Task AuthenticatedRequestValidatesInputAndKeyAndSupportsAuthorizedCheckAgain()
    {
        var admin = await AccountAsync("http-admin");
        var outsider = await AccountAsync("other-admin");
        var member = await AccountAsync("member", GlobalRole.User);
        await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        await LoginAsync(client, admin.LoginName);
        using var entry = await client.GetAsync("/Admin/Events/Create");
        Assert.Equal(HttpStatusCode.Redirect, entry.StatusCode);
        Assert.Equal("/Admin/Events?create=1", entry.Headers.Location!.OriginalString);
        var page = await client.GetStringAsync(entry.Headers.Location);
        var key = Guid.Parse(InputValue(page, "Input_RequestId"));
        var token = Token(page);
        var unicode50 = string.Concat(Enumerable.Repeat("😀", 50));
        var before = await SnapshotAsync();
        foreach (var (name, timezone, requestKey) in new[]
        {
            (unicode50 + "😀", "UTC", key.ToString()), (" ", "UTC", key.ToString()),
            ("Valid", "Europe/London", key.ToString()), ("Valid", "UTC", ""), ("Valid", "UTC", "not-a-guid")
        })
        {
            // A10 (U10 part 2, Events/Create retired): a refused non-dialog POST returns to the Create dialog with an error toast instead of re-rendering the old page.
            using var rejected = await client.PostAsync("/Admin/Events/Create", Form(token, requestKey, name, timezone));
            Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
            Assert.Equal("/Admin/Events?create=1", rejected.Headers.Location!.OriginalString);
            Assert.Contains("class=\"toast is-error\" data-toast role=\"alert\"", await client.GetStringAsync(rejected.Headers.Location));
            Assert.Equal(before, await SnapshotAsync());
        }
        using (var retired = await client.PostAsync("/Admin/Events/Create", Form(token, key.ToString(), "Valid", "UTC", retired: true)))
        {
            Assert.Equal(HttpStatusCode.Redirect, retired.StatusCode);
            Assert.Equal("/Admin/Events?create=1", retired.Headers.Location!.OriginalString);
            Assert.Contains("only a name and timezone", WebUtility.HtmlDecode(await client.GetStringAsync(retired.Headers.Location)));
        }
        Assert.Equal(before, await SnapshotAsync());
        using (var absent = await client.GetAsync($"/Admin/Events/Create?handler=CheckAgain&requestId={key}"))
            Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        Uri? location;
        using (var created = await client.PostAsync("/Admin/Events/Create", Form(token, key.ToString(), unicode50, "UTC")))
        {
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
            location = created.Headers.Location;
        }
        before = await SnapshotAsync();
        using (var repeated = await client.PostAsync("/Admin/Events/Create", Form(token, key.ToString(), unicode50, "UTC")))
        {
            Assert.Equal(HttpStatusCode.Redirect, repeated.StatusCode);
            Assert.Equal(location, repeated.Headers.Location);
        }
        using (var conflict = await client.PostAsync("/Admin/Events/Create", Form(token, key.ToString(), "Different", "UTC")))
        {
            // A10 (U10 part 2): the same-key conflict is still refused without side effects; without a page it returns to the dialog (the dialog's JSON path keeps the conflict outcome).
            Assert.Equal(HttpStatusCode.Redirect, conflict.StatusCode);
            Assert.Equal("/Admin/Events?create=1", conflict.Headers.Location!.OriginalString);
            Assert.Contains("class=\"toast is-error\" data-toast role=\"alert\"", await client.GetStringAsync(conflict.Headers.Location));
        }
        Assert.Equal(before, await SnapshotAsync());
        using (var check = await client.GetAsync($"/Admin/Events/Create?handler=CheckAgain&requestId={key}"))
        {
            Assert.Equal(HttpStatusCode.OK, check.StatusCode);
            var json = JsonDocument.Parse(await check.Content.ReadAsStringAsync());
            Assert.EndsWith(json.RootElement.GetProperty("eventId").GetGuid().ToString(), location!.OriginalString);
            Assert.True(check.Headers.CacheControl?.NoStore);
        }
        using var other = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        await LoginAsync(other, outsider.LoginName);
        using (var check = await other.GetAsync($"/Admin/Events/Create?handler=CheckAgain&requestId={key}&actorId={admin.Id}"))
        {
            Assert.Equal(HttpStatusCode.NotFound, check.StatusCode);
            Assert.DoesNotContain(location!.OriginalString, await check.Content.ReadAsStringAsync());
        }
        using var ordinary = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        await LoginAsync(ordinary, member.LoginName);
        using (var denied = await ordinary.GetAsync($"/Admin/Events/Create?handler=CheckAgain&requestId={key}"))
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        using var anonymous = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using (var denied = await anonymous.GetAsync($"/Admin/Events/Create?handler=CheckAgain&requestId={key}"))
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Equal(before, await SnapshotAsync());
        await using (var db = new ApplicationDbContext(options))
        {
            var account = await db.Accounts.SingleAsync(x => x.Id == admin.Id);
            account.Disable(Now);
            await db.SaveChangesAsync();
        }
        using (var disabled = await client.GetAsync($"/Admin/Events/Create?handler=CheckAgain&requestId={key}"))
            Assert.Equal(HttpStatusCode.Redirect, disabled.StatusCode);
        using (var disabledPost = await client.PostAsync("/Admin/Events/Create", Form(token, Guid.NewGuid().ToString(), "Disabled", "UTC")))
            Assert.Equal(HttpStatusCode.Redirect, disabledPost.StatusCode);
        Assert.Equal(before, await SnapshotAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ServiceRechecksCurrentAdminAuthorityForCreateReplayAndLookup(bool disabled)
    {
        var account = await AccountAsync("revoked-admin");
        var actor = Actor(account);
        var key = Guid.NewGuid();
        await using var db = new ApplicationDbContext(options);
        Assert.Equal(EventCreationOutcome.Completed, (await Service(db).CreateAsync(key, "Retained", "UTC", actor)).Outcome);
        var retained = await db.Accounts.SingleAsync(x => x.Id == account.Id);
        if (disabled) retained.Disable(Now); else retained.SetGlobalRole(GlobalRole.User);
        await db.SaveChangesAsync();
        var before = await SnapshotAsync();
        Assert.Equal(EventCreationOutcome.Forbidden, (await Service(db).CreateAsync(key, "Retained", "UTC", actor)).Outcome);
        Assert.Equal(EventCreationOutcome.Forbidden, (await Service(db).CreateAsync(Guid.NewGuid(), "Another", "UTC", actor)).Outcome);
        Assert.Equal(EventCreationOutcome.Forbidden, (await Service(db).CheckAgainAsync(key, actor)).Outcome);
        Assert.Equal(before, await SnapshotAsync());
    }

    private static EventCreationService Service(ApplicationDbContext db) => new(db, new FixedClock(Now));
    private static LifecycleActor Actor(Account account) => new(account.Id, account.LoginName);

    private async Task<Account> AccountAsync(string login, GlobalRole role = GlobalRole.Admin)
    {
        var account = Account.CreateWebsite(Guid.NewGuid(), login, login.ToUpperInvariant(), Now);
        account.SetGlobalRole(role);
        account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "synthetic-creation-password"), false, Now, incrementVersion: false);
        await using var db = new ApplicationDbContext(options);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    private async Task AssertAggregateCountAsync(int count)
    {
        await using var db = new ApplicationDbContext(options);
        Assert.Equal(count, await db.Events.CountAsync());
        Assert.Equal(count, await db.EventCreationOperations.CountAsync());
        Assert.Equal(count, await db.SignupForms.CountAsync());
        Assert.Equal(3 * count, await db.SignupQuestions.CountAsync());
        Assert.Equal(count, await db.Boards.CountAsync());
        Assert.Equal(count, await db.AuditEntries.CountAsync(x => x.Action == "event.created"));
    }

    private async Task<string> SnapshotAsync()
    {
        await using var db = new ApplicationDbContext(options);
        return JsonSerializer.Serialize(new
        {
            Events = await db.Events.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            Operations = await db.EventCreationOperations.AsNoTracking().OrderBy(x => x.EventId).ToListAsync(),
            Forms = await db.SignupForms.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            Questions = await db.SignupQuestions.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            Boards = await db.Boards.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            Audits = await db.AuditEntries.AsNoTracking().OrderBy(x => x.Id).ToListAsync()
        });
    }

    private async Task AssertAdvisoryWaitAsync(ApplicationDbContext waiting)
    {
        var pid = ((NpgsqlConnection)waiting.Database.GetDbConnection()).ProcessID;
        await using var observer = new NpgsqlConnection(database.GetConnectionString());
        await observer.OpenAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE pid = @pid AND wait_event = 'advisory')", observer);
            command.Parameters.AddWithValue("pid", pid);
            if (await command.ExecuteScalarAsync(deadline.Token) is true) return;
            await Task.Delay(20, deadline.Token);
        }
    }

    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
        .UseEnvironment("Testing").UseSetting("ConnectionStrings:Database", database.GetConnectionString())
        .ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock(Now));
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        }));

    private static async Task LoginAsync(HttpClient client, string login)
    {
        var page = await client.GetStringAsync("/Account/Login");
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = login,
            ["Input.Password"] = "synthetic-creation-password",
            ["__RequestVerificationToken"] = Token(page)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static FormUrlEncodedContent Form(string token, string key, string name, string timezone, bool retired = false)
    {
        var values = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Input.RequestId"] = key,
            ["Input.Name"] = name,
            ["Input.Timezone"] = timezone
        };
        if (retired) values["Input.Description"] = "Retired wizard value";
        return new(values);
    }

    private static string Token(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    private static string InputValue(string page, string id) => WebUtility.HtmlDecode(Regex.Match(page, $"id=\"{Regex.Escape(id)}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class LostCommitResponse : DbTransactionInterceptor
    {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException("Controlled loss of response after PostgreSQL commit.");
    }
}
