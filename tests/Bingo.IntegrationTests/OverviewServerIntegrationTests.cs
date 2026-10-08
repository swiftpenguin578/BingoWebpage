using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

// U4 item 0 (brief 85): Overview server reads and handler transport against PostgreSQL.
// S2 structured current event, A-Overview-3 overlap read, U4-Q3 (c) hidden view,
// U4-Q4/Q5 refusal wording, JSON outcomes with PRG fallback, and the no-store Current read.
public sealed class OverviewServerIntegrationTests(PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    // Microsecond-aligned fixture clock (AGENTS.md timestamp rules).
    private static readonly DateTimeOffset Now = new(2027, 6, 2, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_overview_u4").WithUsername("bingo").WithPassword("bingo_test_password"));
    private DbContextOptions<ApplicationDbContext> options = null!;
    private Guid adminId;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
        var admin = Account(db, "overview-admin", GlobalRole.Admin);
        Account(db, "overview-super", GlobalRole.SuperAdmin);
        adminId = admin.Id;
        await db.SaveChangesAsync();
    }
    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task StartReadinessNamesTheOtherCurrentEventAsStructuredData()
    {
        var closed = await AddAsync("Closed setup", EventState.SignupClosed);
        var live = await AddAsync("Running Bingo", EventState.Live);
        await using var factory = Factory();
        using var scope = factory.Services.CreateScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<IEventLifecycleService>();
        var blocker = Assert.Single((await lifecycle.GetStartReadinessAsync(closed))!.Blockers, x => x.Code == "CURRENT_EVENT_EXISTS");
        // S2: the row text and link target come from structured data, not the description.
        Assert.Equal(new ReadinessSubject(live, "Running Bingo", EventState.Live), blocker.Subject);
        Assert.Equal("Publish the results of Running Bingo first.", blocker.Description);
        Assert.Equal($"/Admin/Events/Manage/{live}", blocker.Route);
        Assert.Equal(new ReadinessSubject(live, "Running Bingo", EventState.Live), await lifecycle.GetOtherCurrentEventAsync(closed));
        Assert.Null(await lifecycle.GetOtherCurrentEventAsync(live));
    }

    [Fact]
    public async Task LegacyFinishedCurrentEventAsksForTheSuperAdmin()
    {
        var closed = await AddAsync("Closed setup", EventState.SignupClosed);
        await AddAsync("Legacy Finished", EventState.Finalized);
        await using var factory = Factory();
        using var scope = factory.Services.CreateScope();
        var blocker = Assert.Single((await scope.ServiceProvider.GetRequiredService<IEventLifecycleService>().GetStartReadinessAsync(closed))!.Blockers, x => x.Code == "CURRENT_EVENT_EXISTS");
        // U4-Q5 (a).
        Assert.Equal("Legacy Finished is still the current event. Contact the Super Admin to archive it.", blocker.Description);
        Assert.Equal(EventState.Finalized, blocker.Subject!.State);
    }

    [Fact]
    public async Task CrossEventLinkUsesTheFixedOpenEventTextAndKeepsTheNameInTheRow()
    {
        var closed = await AddAsync("Closed setup", EventState.SignupClosed);
        var live = await AddAsync("Running Bingo", EventState.Live);
        await using var factory = Factory();
        using var client = await LoginAsync(factory, "overview-admin");
        var page = await client.GetStringAsync($"/Admin/Events/Manage/{closed}");
        // U4-L1: fixed link text; the other event's name stays in the row text; link target and data-shell-link kept.
        Assert.Contains("Publish the results of Running Bingo first", page);
        Assert.Matches(new Regex($"""<a class="text-btn" href="/Admin/Events/Manage/{live}"[^>]*data-shell-link[^>]*>Open event</a>"""), page);
    }

    [Fact]
    public async Task ResumeRefusalUsesThePublishWordingAndChangesNothing()
    {
        var review = await AddAsync("Paused Bingo", EventState.AwaitingFinalReview);
        await AddAsync("Other Live", EventState.Live);
        await using var factory = Factory();
        using var client = await LoginAsync(factory, "overview-admin");
        var page = await client.GetStringAsync($"/Admin/Events/Manage/{review}");
        // Read after the host started: its lifecycle worker may already have closed uploads.
        var before = await VersionAsync(review);
        using var response = await PostJsonAsync(client, review, "ResumeEvent", page, before, new()
        {
            ["ConfirmResumeEvent"] = "true",
            ["ResumeReason"] = "Ended by mistake",
            ["ReplacementEventEndsAtLocal"] = "2027-06-10T18:00"
        });
        var outcome = await JsonAsync(response);
        // U4-Q4 (b): replaces "Archive it before resuming this event."
        Assert.False(outcome.GetProperty("succeeded").GetBoolean());
        Assert.Equal("refused", outcome.GetProperty("outcome").GetString());
        Assert.Equal("Publish the results of Other Live first.", outcome.GetProperty("error").GetString());
        Assert.Equal(before, await VersionAsync(review));
        await using var db = new ApplicationDbContext(options);
        Assert.Equal(EventState.AwaitingFinalReview, (await db.Events.SingleAsync(x => x.Id == review)).State);
    }

    [Fact]
    public async Task OverlapReadMatchesTheTransitionBoundaryWithoutChangingAnything()
    {
        var draft = await AddAsync("Overlapping draft", EventState.Draft);
        var open = await AddAsync("Public neighbour", EventState.SignupOpen);
        var undated = await AddAsync("Undated draft", EventState.Draft, dates: false);
        await using var factory = Factory();
        using var scope = factory.Services.CreateScope();
        var readiness = scope.ServiceProvider.GetRequiredService<IEventReadinessEvaluator>();
        var overlap = await readiness.GetCurrentEventOverlapAsync(draft);
        Assert.NotNull(overlap);
        Assert.Equal("EVENT_WINDOW_OVERLAP", overlap.Code);
        Assert.Equal(open, overlap.Subject!.EventId);
        Assert.Equal("Public neighbour", overlap.DescriptionArguments[0]);
        // A missing window is already its own readiness row (EVENT_START/END_REQUIRED).
        Assert.Null(await readiness.GetCurrentEventOverlapAsync(undated));
        // Read only: nothing about either event changed.
        Assert.Equal(0, await AuditCountAsync(draft));
        Assert.Equal(0, await AuditCountAsync(open));
    }

    [Fact]
    public async Task LifecycleJsonOutcomesAppliedStaleInvalidAndPlainPostFallback()
    {
        var open = await AddAsync("Open Bingo", EventState.SignupOpen);
        await using var factory = Factory();
        using var client = await LoginAsync(factory, "overview-admin");
        var page = await client.GetStringAsync($"/Admin/Events/Manage/{open}");
        var version = await VersionAsync(open);

        // Stale: the version read when the dialog opened is checked before the service.
        using (var stale = await PostJsonAsync(client, open, "CloseSignup", page, version - 1, new() { ["ConfirmSignupAction"] = "true" }))
        {
            var outcome = await JsonAsync(stale);
            Assert.Equal("stale", outcome.GetProperty("outcome").GetString());
            Assert.Equal(version, await VersionAsync(open));
        }
        using (var applied = await PostJsonAsync(client, open, "CloseSignup", page, version, new() { ["ConfirmSignupAction"] = "true" }))
        {
            var outcome = await JsonAsync(applied);
            Assert.True(outcome.GetProperty("succeeded").GetBoolean());
            Assert.Equal("applied", outcome.GetProperty("outcome").GetString());
            Assert.Equal("Signups are closed for Open Bingo.", outcome.GetProperty("message").GetString());
        }
        // Plain form posts keep the PRG fallback (no Accept: application/json).
        var live = await AddAsync("Live Bingo", EventState.Live, other: true);
        page = await client.GetStringAsync($"/Admin/Events/Manage/{live}");
        using (var invalid = await PostJsonAsync(client, live, "EndEvent", page, await VersionAsync(live), new() { ["ConfirmEndEvent"] = "true" }))
        {
            // README: a reason only before the scheduled end; the service asks for it.
            var outcome = await JsonAsync(invalid);
            Assert.Equal("invalid", outcome.GetProperty("outcome").GetString());
            Assert.True(outcome.GetProperty("fieldErrors").TryGetProperty("reason", out _));
        }
        using var plain = await client.PostAsync($"/Admin/Events/Manage/{live}?handler=EndEvent", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(page),
            ["EventVersion"] = (await VersionAsync(live)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ConfirmEndEvent"] = "true",
            ["EndReason"] = "Called early"
        }));
        Assert.Equal(HttpStatusCode.Redirect, plain.StatusCode);
        Assert.Equal($"/Admin/Events/Manage/{live}", plain.Headers.Location!.OriginalString);
        await using var db = new ApplicationDbContext(options);
        Assert.Equal(EventState.AwaitingFinalReview, (await db.Events.SingleAsync(x => x.Id == live)).State);
    }

    [Fact]
    public async Task CurrentReadIsNoStoreAndHiddenEventsFollowTheLimitedSuperAdminView()
    {
        var hidden = await AddAsync("Hidden Bingo", EventState.Archived, hide: true);
        var visible = await AddAsync("Visible Bingo", EventState.SignupOpen);
        await using var factory = Factory();
        using (var admin = await LoginAsync(factory, "overview-admin"))
        {
            using var current = await admin.GetAsync($"/Admin/Events/Manage/{visible}?handler=Current");
            Assert.Equal(HttpStatusCode.OK, current.StatusCode);
            Assert.True(current.Headers.CacheControl!.NoStore);
            var json = JsonDocument.Parse(await current.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal("SignupOpen", json.GetProperty("phase").GetString());
            Assert.Equal((await VersionAsync(visible)).ToString(System.Globalization.CultureInfo.InvariantCulture), json.GetProperty("version").GetString());
            // C-CMP-1: hidden events stay Not Found for ordinary admins, on every read.
            foreach (var url in new[] { $"/Admin/Events/Manage/{hidden}", $"/Admin/Events/Manage/{hidden}?hidden=true", $"/Admin/Events/Manage/{hidden}?handler=Current" })
                Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(url)).StatusCode);
        }
        using var super = await LoginAsync(factory, "overview-super");
        // U4-Q3 (c): the plain URL opens the limited view; the old flag still works.
        var page = await super.GetStringAsync($"/Admin/Events/Manage/{hidden}");
        Assert.Equal(HttpStatusCode.OK, (await super.GetAsync($"/Admin/Events/Manage/{hidden}?hidden=true")).StatusCode);
        var state = JsonDocument.Parse(await super.GetStringAsync($"/Admin/Events/Manage/{hidden}?handler=Current")).RootElement;
        Assert.True(state.GetProperty("hidden").GetBoolean());
        // L6: the limited view carries only the restore dialog, no evidence codes and no last change.
        Assert.Equal(["restore"], state.GetProperty("dialogs").EnumerateObject().Select(x => x.Name).ToArray());
        Assert.Empty(state.GetProperty("evidenceCodes").EnumerateArray());
        Assert.Empty(state.GetProperty("codes").GetProperty("list").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, state.GetProperty("lastChange").ValueKind);
        var version = await VersionAsync(hidden);
        // Only Restore can be posted on the hidden view.
        using (var refused = await PostJsonAsync(super, hidden, "Hide", page, version, new() { ["ConfirmDestructiveAction"] = "true", ["QuarantineReason"] = "Again" }))
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        using var restored = await PostJsonAsync(super, hidden, "RestoreHidden", page, version, new() { ["ConfirmDestructiveAction"] = "true" });
        var outcome = await JsonAsync(restored);
        Assert.True(outcome.GetProperty("succeeded").GetBoolean());
        await using var db = new ApplicationDbContext(options);
        Assert.False((await db.Events.SingleAsync(x => x.Id == hidden)).IsHidden);
    }

    [Fact]
    public async Task EvidenceCodeJsonOutcomesKeepTheDuplicateTimeRefusal()
    {
        var live = await AddAsync("Code Bingo", EventState.Live);
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync(x => x.Id == live);
            item.SetEvidenceCodeEnabled(true, Now);
            db.EvidenceCodes.Add(new EvidenceCode(Guid.NewGuid(), live, "ABC123", Now.AddHours(1), adminId, Now, null));
            await db.SaveChangesAsync();
        }
        await using var factory = Factory();
        using var client = await LoginAsync(factory, "overview-admin");
        var page = await client.GetStringAsync($"/Admin/Events/Manage/{live}");
        using (var duplicate = await PostJsonAsync(client, live, "CreateEvidenceCode", page, await VersionAsync(live), new()
        { ["NewEvidenceCode"] = "zzz999", ["EvidenceCodeActivatesAtLocal"] = "2027-06-02T15:00" }))
        {
            var outcome = await JsonAsync(duplicate);
            Assert.Equal("invalid", outcome.GetProperty("outcome").GetString());
            Assert.True(outcome.GetProperty("fieldErrors").TryGetProperty("from", out _));
        }
        using var saved = await PostJsonAsync(client, live, "CreateEvidenceCode", page, await VersionAsync(live), new()
        { ["NewEvidenceCode"] = "zzz999", ["EvidenceCodeActivatesAtLocal"] = "2027-06-03T14:00" });
        var applied = await JsonAsync(saved);
        Assert.True(applied.GetProperty("succeeded").GetBoolean());
        Assert.Equal("Code ZZZ999 saved.", applied.GetProperty("message").GetString());
        var current = JsonDocument.Parse(await client.GetStringAsync($"/Admin/Events/Manage/{live}?handler=Current")).RootElement;
        Assert.Equal("ABC123,ZZZ999", string.Join(",", current.GetProperty("evidenceCodes").EnumerateArray().Select(x => x.GetProperty("code").GetString())));
    }

    // L5 (U4 review): Copenhagen 2028 spring-forward 02:30 does not exist (26 March) and fall-back 02:30 is ambiguous (29 October).
    // Each Overview picker field is refused as an invalid field on the server, and nothing changes.
    [Theory]
    [InlineData("2028-03-26T02:30", "does not exist")]
    [InlineData("2028-10-29T02:30", "ambiguous")]
    public async Task DaylightSavingGapAndOverlapTimesAreRefusedOnEveryOverviewPicker(string local, string reason)
    {
        var review = await AddAsync("DST review", EventState.AwaitingFinalReview);
        var live = await AddAsync("DST live", EventState.Live);
        await using (var db = new ApplicationDbContext(options))
        {
            (await db.Events.SingleAsync(x => x.Id == live)).SetEvidenceCodeEnabled(true, Now);
            await db.SaveChangesAsync();
        }
        await using var factory = Factory();
        using var client = await LoginAsync(factory, "overview-admin");
        var reviewPage = await client.GetStringAsync($"/Admin/Events/Manage/{review}");
        var livePage = await client.GetStringAsync($"/Admin/Events/Manage/{live}");
        var reviewVersion = await VersionAsync(review);
        var liveVersion = await VersionAsync(live);
        var audits = await AuditCountAsync(review) + await AuditCountAsync(live);
        var cases = new (Guid Id, string Handler, string Page, long Version, Dictionary<string, string> Fields, string Field)[]
        {
            (review, "ResumeEvent", reviewPage, reviewVersion, new() { ["ConfirmResumeEvent"] = "true", ["ResumeReason"] = "Mistake", ["ReplacementEventEndsAtLocal"] = local }, "until"),
            (review, "ReopenSubmissions", reviewPage, reviewVersion, new() { ["StateReason"] = "More time", ["ReopenUntilLocal"] = local }, "until"),
            (live, "CreateEvidenceCode", livePage, liveVersion, new() { ["NewEvidenceCode"] = "DST123", ["EvidenceCodeActivatesAtLocal"] = local }, "from")
        };
        foreach (var (id, handler, page, version, fields, field) in cases)
        {
            using var response = await PostJsonAsync(client, id, handler, page, version, fields);
            var outcome = await JsonAsync(response);
            Assert.Equal("invalid", outcome.GetProperty("outcome").GetString());
            var message = outcome.GetProperty("fieldErrors").GetProperty(field).GetString();
            Assert.Contains(reason, message, StringComparison.Ordinal);
        }
        Assert.Equal(reviewVersion, await VersionAsync(review));
        Assert.Equal(liveVersion, await VersionAsync(live));
        Assert.Equal(audits, await AuditCountAsync(review) + await AuditCountAsync(live));
        await using var check = new ApplicationDbContext(options);
        Assert.Equal(EventState.AwaitingFinalReview, (await check.Events.SingleAsync(x => x.Id == review)).State);
        Assert.Empty(await check.EvidenceCodes.Where(x => x.EventId == live).ToListAsync());
    }

    // L5: the early-end ceiling-minute text and the +30 minutes upload time with a clock that is neither
    // minute-aligned nor microsecond-aligned (AGENTS.md timestamp rules): 12:34:00.5678001 UTC.
    [Fact]
    public async Task EarlyEndTextRoundsUpToTheNextMinuteForAClockOffTheWholeMinute()
    {
        var live = await AddAsync("Ceiling Bingo", EventState.Live);
        var clock = Now.AddMinutes(34).AddTicks(5_678_001);
        await using var factory = Factory(clock);
        using var client = await LoginAsync(factory, "overview-admin");
        var current = JsonDocument.Parse(await client.GetStringAsync($"/Admin/Events/Manage/{live}?handler=Current")).RootElement;
        var effects = current.GetProperty("dialogs").GetProperty("end").GetProperty("effects").EnumerateArray().Select(x => x.GetString()).ToArray();
        // Copenhagen is UTC+2 on 2 June 2027: the end becomes 12:35Z = 14:35, uploads stay open until 13:04Z = 15:04 (truncated to the minute).
        Assert.Contains(effects, x => x!.EndsWith("Its end time becomes 2 Jun, 14:35.", StringComparison.Ordinal));
        Assert.Contains(effects, x => x == "Uploads stay open for 30 minutes, until 2 Jun, 15:04, for drops from before the end.");
    }

    [Fact]
    public async Task RetiredHandlersKeepTheirPinnedResponses()
    {
        var draft = await AddAsync("Retired Bingo", EventState.Draft);
        await using var factory = Factory();
        using var client = await LoginAsync(factory, "overview-admin");
        var page = await client.GetStringAsync($"/Admin/Events/Manage/{draft}");
        Task<HttpResponseMessage> Post(string handler) => client.PostAsync($"/Admin/Events/Manage/{draft}?handler={handler}", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(page) }));
        Assert.Equal(HttpStatusCode.BadRequest, (await Post("State")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post("ConfirmSignup")).StatusCode);
        var capacity = await Post("Capacity");
        Assert.Equal(HttpStatusCode.Redirect, capacity.StatusCode);
        Assert.Equal($"/Admin/Events/SignupSetup/{draft}", capacity.Headers.Location!.OriginalString);
        var window = await Post("SignupWindow");
        Assert.Equal($"/Admin/Events/Schedule/{draft}", window.Headers.Location!.OriginalString);
    }

    private static Account Account(ApplicationDbContext db, string login, GlobalRole role)
    {
        var account = Domain.Access.Account.CreateWebsite(Guid.NewGuid(), login, login.ToUpperInvariant(), Now.AddYears(-1));
        account.SetGlobalRole(role);
        account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "synthetic-test-password"), false, Now, incrementVersion: false);
        db.Add(account);
        return account;
    }

    // Each event gets its own window so overlap only happens where a test asks for it.
    private int windows;
    private async Task<Guid> AddAsync(string name, EventState state, bool dates = true, bool hide = false, bool other = false)
    {
        await using var db = new ApplicationDbContext(options);
        var offset = name.StartsWith("Overlapping", StringComparison.Ordinal) || name.StartsWith("Public neighbour", StringComparison.Ordinal) ? 0 : 60 * ++windows;
        var past = state is EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived;
        var start = past ? Now.AddDays(-3 - offset) : Now.AddDays(20 + offset);
        var end = past && state == EventState.Live ? Now.AddDays(2 + (other ? 0 : 0)) : start.AddDays(5);
        var item = new BingoEvent(Guid.NewGuid(), name, "u4-" + Guid.NewGuid().ToString("N"), "Europe/Copenhagen", adminId, Now.AddYears(-1), PlacementRule.LegacyScoreTimeThenEhb);
        if (dates) item.ConfigureInitialSchedule(start.AddDays(-10), start.AddDays(-2), null, start, end, null);
        if (state != EventState.Draft)
        {
            item.OpenSignups(start.AddDays(-10));
            item.MarkFirstPublic(start.AddDays(-10));
            if (state != EventState.SignupOpen)
            {
                item.CloseSignups(start.AddDays(-2));
                if (state != EventState.SignupClosed)
                {
                    item.StartEvent(start);
                    if (state != EventState.Live)
                    {
                        item.EndEvent(start.AddDays(1));
                        if (state is EventState.Finalized or EventState.Archived) item.FinalizeResults(start.AddDays(1).AddHours(1));
                        if (state == EventState.Archived) item.Archive(start.AddDays(1).AddHours(2));
                    }
                }
            }
        }
        if (hide) item.Hide(adminId, Now.AddDays(-1), name, "Synthetic quarantine");
        db.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }
    private async Task<int> AuditCountAsync(Guid id)
    {
        await using var db = new ApplicationDbContext(options);
        return await db.AuditEntries.CountAsync(x => x.EventId == id);
    }
    private async Task<long> VersionAsync(Guid id)
    {
        await using var db = new ApplicationDbContext(options);
        return await db.Events.Where(x => x.Id == id).Select(x => x.Version).SingleAsync();
    }
    private WebApplicationFactory<Program> Factory(DateTimeOffset? clock = null) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
        .UseSetting("ConnectionStrings:Database", database.GetConnectionString())
        .ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FixedClock(clock ?? Now));
            // Login stamps the session with wall time; validate the cookie on that clock
            // while the fixed clock stays authoritative for event dates (parity fixture rule).
            services.PostConfigure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(
                Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, settings => settings.TimeProvider = TimeProvider.System);
        }));
    private static async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> factory, string login)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var page = await client.GetStringAsync("/Account/Login");
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Input.Username"] = login, ["Input.Password"] = "synthetic-test-password", ["__RequestVerificationToken"] = Token(page) }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }
    private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, Guid id, string handler, string page, long version, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = Token(page);
        fields["EventVersion"] = version.ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/Admin/Events/Manage/{id}?handler={handler}") { Content = new FormUrlEncodedContent(fields) };
        request.Headers.Accept.ParseAdd("application/json");
        return await client.SendAsync(request);
    }
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }
    private static string Token(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
