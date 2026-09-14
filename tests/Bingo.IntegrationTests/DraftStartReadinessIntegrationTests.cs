using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Bingo.Application.Events;
using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Bingo.Web.Events;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class DraftStartReadinessIntegrationTests : IAsyncLifetime
{
    private const string Password = "readiness-test-password";
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_c10_c38").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private readonly MutableClock clock = new(new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero));
    private readonly RecordingStorage storage = new();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }
    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Theory]
    [InlineData("scheduled")]
    [InlineData("early")]
    [InlineData("postponed")]
    public async Task AdminSetupEnableAndActualStartUnlockEmergencyOnlyTeamWithoutEarlyEvidence(string mode)
    {
        var setup = await SeedAsync();
        await using var factory = Factory();
        using var admin = Client(factory);
        using var ordinary = Client(factory);
        using var emergency = Client(factory);
        await LoginAsync(admin, "readiness-admin");
        await LoginAsync(ordinary, "readiness-user");
        var createPath = $"/Admin/Accounts/Create?eventId={setup.EventId}&teamId={setup.TeamId}";
        var createHtml = await admin.GetStringAsync(createPath);
        Assert.Contains("Input.Username", createHtml);
        using (var denied = await PostAsync(ordinary, "/Admin/Accounts/Create", "/Account/Settings", new()
        {
            ["Input.Username"] = "unauthorized-emergency",
            ["Input.EventId"] = setup.EventId.ToString(),
            ["Input.TeamId"] = setup.TeamId.ToString()
        })) Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        using (var created = await PostAsync(admin, createPath, createPath, new()
        {
            ["Input.Username"] = "readiness-emergency",
            ["Input.EventId"] = setup.EventId.ToString(),
            ["Input.TeamId"] = setup.TeamId.ToString()
        })) Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Guid credentialId;
        await using (var db = new ApplicationDbContext(options))
        {
            Assert.False(await db.Accounts.AnyAsync(x => x.LoginName == "unauthorized-emergency"));
            var credential = await db.Accounts.SingleAsync(x => x.LoginName == "readiness-emergency");
            credentialId = credential.Id;
            Assert.False(credential.Active);
            Assert.Null(credential.PasswordHash);
            var access = await db.AccountEventAccesses.SingleAsync(x => x.AccountId == credential.Id);
            Assert.False(access.Enabled);
            Assert.Equal(setup.ScheduledStart, access.ActiveFrom);
        }
        var manage = $"/Admin/Accounts/Manage/{credentialId}";
        // A forged enable request cannot bypass initial setup.
        using (var unset = await PostAsync(admin, manage + "?handler=EnableEmergency", manage, new()))
            Assert.Equal(HttpStatusCode.OK, unset.StatusCode);
        await AssertCredentialAsync(credentialId, false);
        using (var denied = await PostAsync(ordinary, manage + "?handler=EnableEmergency", "/Account/Settings", new()))
            Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        await AssertCredentialAsync(credentialId, false);
        using (var link = await PostAsync(admin, manage + "?handler=GenerateEmergencyLink", manage, new()))
            Assert.Equal(HttpStatusCode.Redirect, link.StatusCode);
        var revealed = await admin.GetStringAsync(manage);
        var resetPath = Regex.Match(revealed, @"/Account/ResetPassword/[A-F0-9]+").Value;
        Assert.NotEmpty(resetPath);
        using (var completed = await PostAsync(emergency, resetPath, resetPath, new()
        {
            ["Input.Password"] = Password,
            ["Input.ConfirmPassword"] = Password
        })) Assert.Equal(HttpStatusCode.Redirect, completed.StatusCode);
        await AssertCredentialAsync(credentialId, false);
        Assert.False(await TryLoginAsync(emergency, "readiness-emergency"));
        if (mode == "postponed")
        {
            clock.Set(setup.ScheduledStart);
            await ProcessDueAsync(factory);
            await using var blocked = new ApplicationDbContext(options);
            Assert.Null((await blocked.Events.SingleAsync()).ActualStartedAt);
            var attempt = await blocked.ScheduledEventStartAttempts.SingleAsync();
            Assert.False(attempt.Started);
            Assert.Null(attempt.ResolvedAt);
            clock.Set(setup.ScheduledStart.AddMinutes(5));
        }
        var grantTime = clock.GetUtcNow();
        using (var enabled = await PostAsync(admin, manage + "?handler=EnableEmergency", manage, new()))
            Assert.Equal(HttpStatusCode.Redirect, enabled.StatusCode);
        await AssertCredentialAsync(credentialId, true);
        await using (var db = new ApplicationDbContext(options))
        {
            var access = await db.AccountEventAccesses.SingleAsync();
            Assert.Equal(grantTime, access.ActiveFrom);
            Assert.Equal(AccountAccessMode.Disabled, access.GetAccessMode(grantTime.AddTicks(-1)));
            Assert.Equal(AccountAccessMode.Full, access.GetAccessMode(grantTime));
            Assert.Single(await db.AuditEntries.Where(x => x.Action == "account.emergency_enabled").ToListAsync());
        }
        await LoginAsync(emergency, "readiness-emergency");
        var drawer = Drawer(setup);
        var prestart = await SubmitAsync(emergency, setup, drawer);
        Assert.DoesNotContain("\"success\":true", prestart);
        Assert.Contains("We could not complete that request", prestart, StringComparison.Ordinal);
        using (var scope = factory.Services.CreateScope())
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<ISubmissionService>().CreateAsync(
                new(credentialId, setup.EventId, setup.TeamId, setup.TileId, setup.RequirementId, null, setup.ParticipantId, 1, null, "proof.png", new MemoryStream([1, 2, 3]))));
            Assert.Equal("New submissions are not currently open.", failure.Message);
        }
        await using (var db = new ApplicationDbContext(options)) Assert.Empty(await db.Submissions.ToListAsync());
        Assert.Equal(0, storage.Writes);
        using (var scope = factory.Services.CreateScope())
        {
            var readiness = await scope.ServiceProvider.GetRequiredService<IEventLifecycleService>().GetStartReadinessAsync(setup.EventId);
            Assert.NotNull(readiness);
            Assert.True(readiness.CanProceed, string.Join(";", readiness.Blockers));
        }
        var eventManage = $"/Admin/Events/Manage/{setup.EventId}";
        if (mode == "scheduled")
        {
            clock.Set(setup.ScheduledStart);
            await ProcessDueAsync(factory);
        }
        else
        {
            // Neither an ordinary actor nor a stale event version may start the ready event.
            using (var unauthorized = await PostAsync(ordinary, eventManage + "?handler=StartEvent", "/Account/Settings", new() { ["ConfirmStartEvent"] = "true" }))
                Assert.Equal(HttpStatusCode.Redirect, unauthorized.StatusCode);
            var version = await VersionAsync();
            using (var stale = await PostAsync(admin, eventManage + "?handler=StartEvent", eventManage, new()
            {
                ["EventVersion"] = (version - 1).ToString(CultureInfo.InvariantCulture),
                ["ConfirmStartEvent"] = "true",
                ["StartReason"] = "Authorized early start"
            })) Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
            await using (var unchanged = new ApplicationDbContext(options)) Assert.Null((await unchanged.Events.SingleAsync()).ActualStartedAt);
            if (mode == "early")
            {
                using var missingReason = await PostAsync(admin, eventManage + "?handler=StartEvent", eventManage, new()
                {
                    ["EventVersion"] = version.ToString(CultureInfo.InvariantCulture),
                    ["ConfirmStartEvent"] = "true"
                });
                Assert.Equal(HttpStatusCode.OK, missingReason.StatusCode);
            }
            using var started = await PostAsync(admin, eventManage + "?handler=StartEvent", eventManage, new()
            {
                ["EventVersion"] = version.ToString(CultureInfo.InvariantCulture),
                ["ConfirmStartEvent"] = "true",
                ["StartReason"] = mode == "early" ? "Authorized early start" : ""
            });
            Assert.Equal(HttpStatusCode.Redirect, started.StatusCode);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var item = await db.Events.SingleAsync();
            Assert.Equal(EventState.Live, item.State);
            Assert.Equal(clock.GetUtcNow(), item.ActualStartedAt);
            Assert.Equal(setup.ScheduledStart, item.EventStartsAt);
            Assert.Single(await db.EventStateTransitions.Where(x => x.ToState == EventState.Live).ToListAsync());
            if (mode == "early") Assert.True(item.ActualStartedAt < item.EventStartsAt);
            if (mode == "postponed") Assert.NotNull((await db.ScheduledEventStartAttempts.SingleAsync()).ResolvedAt);
        }
        var submitted = await SubmitAsync(emergency, setup, drawer);
        Assert.Contains("\"success\":true", submitted);
        await using (var db = new ApplicationDbContext(options))
        {
            var row = await db.Submissions.SingleAsync();
            Assert.Equal(setup.ParticipantId, row.CreditedParticipantId);
            Assert.Equal(credentialId, row.SubmittedByAccountId);
        }
        Assert.Equal(1, storage.Writes);
        if (mode != "early") return;
        // Existing sessions lose access immediately on disable; re-enabling does not revive the old cookie.
        using (var disabled = await PostAsync(admin, manage + "?handler=DisableEmergency", manage, new())) Assert.Equal(HttpStatusCode.Redirect, disabled.StatusCode);
        using (var denied = await emergency.GetAsync(drawer)) Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        using (var enabled = await PostAsync(admin, manage + "?handler=EnableEmergency", manage, new())) Assert.Equal(HttpStatusCode.Redirect, enabled.StatusCode);
        await LoginAsync(emergency, "readiness-emergency");
        clock.Set(setup.Cutoff);
        await ProcessDueAsync(factory);
        await AssertCredentialAsync(credentialId, false);
        using (var denied = await emergency.GetAsync(drawer)) Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        using (var denied = await PostAsync(admin, manage + "?handler=EnableEmergency", manage, new())) Assert.Equal(HttpStatusCode.OK, denied.StatusCode);
        await AssertCredentialAsync(credentialId, false);
        using (var reopened = await PostAsync(admin, eventManage + "?handler=ReopenSubmissions", eventManage, new()
        {
            ["EventVersion"] = (await VersionAsync()).ToString(CultureInfo.InvariantCulture),
            ["ReopenUntil"] = clock.GetUtcNow().AddMinutes(10).ToString("O", CultureInfo.InvariantCulture),
            ["StateReason"] = "Controlled reopen check"
        })) Assert.Equal(HttpStatusCode.Redirect, reopened.StatusCode);
        await AssertCredentialAsync(credentialId, false);
        using (var enabled = await PostAsync(admin, manage + "?handler=EnableEmergency", manage, new())) Assert.Equal(HttpStatusCode.Redirect, enabled.StatusCode);
        await LoginAsync(emergency, "readiness-emergency");
        Assert.Contains("\"success\":true", await SubmitAsync(emergency, setup, drawer));
        clock.Set(clock.GetUtcNow().AddMinutes(10));
        await ProcessDueAsync(factory);
        await AssertCredentialAsync(credentialId, false);
        await using var history = new ApplicationDbContext(options);
        Assert.Equal(2, await history.Submissions.CountAsync());
        Assert.Equal(2, await history.AuditEntries.CountAsync(x => x.Action == "account.emergency_cutoff_disabled"));
        Assert.True((await history.AccountEventAccesses.SingleAsync()).CutoffDisabled);
    }

    [Theory]
    [InlineData("unset")]
    [InlineData("password-change")]
    [InlineData("disabled-account")]
    [InlineData("disabled-access")]
    [InlineData("expired")]
    [InlineData("future-grant")]
    [InlineData("wrong-event")]
    [InlineData("wrong-team")]
    public async Task ReadinessAndStartRejectUnusableEmergencyAccess(string fault)
    {
        var setup = await SeedAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            var account = Account.CreateEmergency(Guid.NewGuid(), "unusable", "UNUSABLE", clock.GetUtcNow());
            if (fault != "unset") account.SetPassword(new PasswordHasher<Account>().HashPassword(account, Password), fault == "password-change", clock.GetUtcNow(), false);
            if (fault != "disabled-account") account.Enable();
            var access = new AccountEventAccess(Guid.NewGuid(), account.Id, fault == "wrong-event" ? Guid.NewGuid() : setup.EventId,
                fault == "wrong-team" ? Guid.NewGuid() : setup.TeamId, null, fault == "future-grant" ? setup.ScheduledStart : clock.GetUtcNow(), null,
                fault == "expired" ? clock.GetUtcNow() : null);
            if (fault != "disabled-access") access.Enable();
            db.AddRange(account, access);
            await db.SaveChangesAsync();
        }
        await using var factory = Factory();
        using var scope = factory.Services.CreateScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<IEventLifecycleService>();
        var readiness = await lifecycle.GetStartReadinessAsync(setup.EventId);
        Assert.Contains(readiness!.Blockers, x => x.Code == "TEAM_ACCESS_MISSING");
        var result = await lifecycle.StartNowAsync(setup.EventId, await VersionAsync(), true, "Controlled early start", new(setup.AdminId, "readiness-admin"));
        Assert.False(result.Succeeded);
        await using var verify = new ApplicationDbContext(options);
        Assert.Null((await verify.Events.SingleAsync()).ActualStartedAt);
        Assert.Empty(await verify.EventStateTransitions.ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.Action == "event.started").ToListAsync());
    }

    [Theory]
    [InlineData("draft-unfinished")]
    [InlineData("end-passed")]
    [InlineData("cancelled")]
    [InlineData("expired")]
    [InlineData("wrong-team")]
    [InlineData("disabled-admin")]
    public async Task PrestartEnableDenialsPreserveCredentialAndAuditAtomically(string fault)
    {
        var setup = await SeedAsync(finalize: fault != "draft-unfinished");
        Guid accountId;
        await using (var db = new ApplicationDbContext(options))
        {
            var account = Account.CreateEmergency(Guid.NewGuid(), "denied", "DENIED", clock.GetUtcNow());
            account.SetPassword(new PasswordHasher<Account>().HashPassword(account, Password), false, clock.GetUtcNow(), false);
            accountId = account.Id;
            var access = new AccountEventAccess(Guid.NewGuid(), account.Id, setup.EventId, fault == "wrong-team" ? Guid.NewGuid() : setup.TeamId, null,
                setup.ScheduledStart, null, fault == "expired" ? clock.GetUtcNow() : null);
            db.AddRange(account, access);
            if (fault == "disabled-admin") (await db.Accounts.SingleAsync(x => x.Id == setup.AdminId)).Disable(clock.GetUtcNow(), null, "test");
            await db.SaveChangesAsync();
            if (fault == "cancelled") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET state = 'Cancelled' WHERE id = {setup.EventId}");
        }
        if (fault == "end-passed") clock.Set(setup.Cutoff);
        await using (var db = new ApplicationDbContext(options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new AccountAdministrationService(db, new PasswordHasher<Account>(), clock).SetEmergencyEnabledAsync(setup.AdminId, accountId, true, CancellationToken.None));
        await AssertCredentialAsync(accountId, false);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(setup.ScheduledStart, (await verify.AccountEventAccesses.SingleAsync()).ActiveFrom);
        Assert.Empty(await verify.AuditEntries.ToListAsync());
    }

    [Fact]
    public async Task FailedEnableAuditRollsBackAccountAndGrantTogether()
    {
        var setup = await SeedAsync();
        Guid accountId;
        await using (var db = new ApplicationDbContext(options))
        {
            var account = Account.CreateEmergency(Guid.NewGuid(), "rollback", "ROLLBACK", clock.GetUtcNow());
            account.SetPassword(new PasswordHasher<Account>().HashPassword(account, Password), false, clock.GetUtcNow(), false);
            accountId = account.Id;
            db.AddRange(account, new AccountEventAccess(Guid.NewGuid(), account.Id, setup.EventId, setup.TeamId, null, setup.ScheduledStart, null, null));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION reject_enable_audit() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.action = 'account.emergency_enabled' THEN RAISE EXCEPTION 'Injected enable audit failure'; END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER reject_enable_audit BEFORE INSERT ON audit_entries FOR EACH ROW EXECUTE FUNCTION reject_enable_audit();
                """);
        }
        await using (var db = new ApplicationDbContext(options))
            await Assert.ThrowsAsync<DbUpdateException>(() => new AccountAdministrationService(db, new PasswordHasher<Account>(), clock).SetEmergencyEnabledAsync(setup.AdminId, accountId, true, CancellationToken.None));
        await AssertCredentialAsync(accountId, false);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(setup.ScheduledStart, (await verify.AccountEventAccesses.SingleAsync()).ActiveFrom);
        Assert.Empty(await verify.AuditEntries.ToListAsync());
    }

    private async Task<Setup> SeedAsync(bool finalize = true)
    {
        var now = clock.GetUtcNow();
        await using var db = new ApplicationDbContext(options);
        Account Website(string name, GlobalRole role)
        {
            var account = Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), now);
            account.SetGlobalRole(role);
            account.SetPassword(new PasswordHasher<Account>().HashPassword(account, Password), false, now, false);
            return account;
        }
        var admin = Website("readiness-admin", GlobalRole.Admin);
        var ordinary = Website("readiness-user", GlobalRole.User);
        var item = new BingoEvent(Guid.NewGuid(), "Readiness event", "readiness-event", "UTC", admin.Id, now.AddDays(-2));
        item.ConfigureSchedule(now.AddDays(-1), now.AddHours(-1), null, now.AddHours(1), now.AddHours(3), 10);
        item.ConfigureSignup(true, false, null);
        item.OpenSignups(now.AddDays(-1)); item.CloseSignups(now.AddHours(-1));
        var team = new Team(Guid.NewGuid(), item.Id, "Emergency only", "emergency-only", TeamFormationType.Preformed, null, false);
        var draft = new DraftSession(Guid.NewGuid(), item.Id, 1);
        if (finalize) { draft.Start(now.AddMinutes(-10)); draft.Finalize(now.AddMinutes(-5)); team.Finalize(now.AddMinutes(-5)); item.SetDraftRosterPublication(true); }
        var participant = new EventParticipant(Guid.NewGuid(), item.Id, SignupStatus.Confirmed, 1, now.AddHours(-2), SignupSource.AdminCreated);
        var character = new OsrsCharacter(Guid.NewGuid(), "External One", "EXTERNAL ONE", now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), item.Id, participant.Id, character.Id, 0, now.AddHours(-2), admin.Id, null, EventCharacterRole.Playing, 10, EhbSource.Manual, null);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Captain, now.AddHours(-2), null, "Unowned preformed Captain");
        var board = new Board(Guid.NewGuid(), item.Id, "Readiness board", 1, 1);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Objective", "Complete it", "Proof", 1);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 10, true, false, "Runs", true, 1);
        db.AddRange(admin, ordinary, item, team, draft, participant, character, assignment, membership, board, tile, requirement);
        await BoardApprovalFixture.PublishAsync(db, board, now, [tile], [requirement]);
        return new(item.Id, team.Id, participant.Id, admin.Id, tile.Id, requirement.Id, item.EventStartsAt!.Value, item.SubmissionCutoffAt!.Value);
    }
    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
            // Execute the lifecycle services explicitly at the exact fixture instants.
            foreach (var worker in services.Where(x => x.ServiceType == typeof(IHostedService) &&
                (x.ImplementationType == typeof(EventLifecycleWorker) || x.ImplementationType?.Name == "EventCompetitionSynchronizationWorker")).ToList()) services.Remove(worker);
            services.RemoveAll<IEvidenceStorage>(); services.AddSingleton<IEvidenceStorage>(storage);
        }));
    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    private async Task<long> VersionAsync() { await using var db = new ApplicationDbContext(options); return (await db.Events.SingleAsync()).Version; }
    private static async Task ProcessDueAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IEventLifecycleService>().ProcessDueAsync();
        await scope.ServiceProvider.GetRequiredService<EmergencyCredentialLifecycleService>().ApplyAsync(CancellationToken.None);
    }
    private async Task AssertCredentialAsync(Guid id, bool enabled)
    {
        await using var db = new ApplicationDbContext(options);
        Assert.Equal(enabled, (await db.Accounts.SingleAsync(x => x.Id == id)).Active);
        Assert.Equal(enabled, (await db.AccountEventAccesses.SingleAsync(x => x.AccountId == id)).Enabled);
    }
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string destination, string tokenPage, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = Token(await client.GetStringAsync(tokenPage));
        return await client.PostAsync(destination, new FormUrlEncodedContent(fields));
    }
    private static string Token(string html)
    {
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token); return WebUtility.HtmlDecode(token);
    }
    private static async Task<bool> TryLoginAsync(HttpClient client, string username)
    {
        using var response = await PostAsync(client, "/Account/Login", "/Account/Login", new() { ["Input.Username"] = username, ["Input.Password"] = Password });
        return response.StatusCode == HttpStatusCode.Redirect;
    }
    private static async Task LoginAsync(HttpClient client, string username) => Assert.True(await TryLoginAsync(client, username));
    private static string Drawer(Setup setup) => $"/Captain/Submit?handler=Drawer&tileId={setup.TileId}&eventId={setup.EventId}&teamId={setup.TeamId}";
    private static async Task<string> SubmitAsync(HttpClient client, Setup setup, string drawer)
    {
        var token = Token(await client.GetStringAsync(drawer));
        using var form = new MultipartFormDataContent();
        foreach (var field in new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["Input.TileId"] = setup.TileId.ToString(),
            ["Input.RequirementId"] = setup.RequirementId.ToString(),
            ["Input.CreditedParticipantId"] = setup.ParticipantId.ToString(),
            ["Input.ClaimedWeight"] = "1"
        }) form.Add(new StringContent(field.Value), field.Key);
        form.Add(new ByteArrayContent([1, 2, 3]), "Input.Evidence", "proof.png");
        using var response = await client.PostAsync(drawer, form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }
    private sealed record Setup(Guid EventId, Guid TeamId, Guid ParticipantId, Guid AdminId, Guid TileId, Guid RequirementId, DateTimeOffset ScheduledStart, DateTimeOffset Cutoff);
    private sealed class MutableClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; public void Set(DateTimeOffset value) => now = value; }
    private sealed class RecordingStorage : IEvidenceStorage
    {
        public int Writes { get; private set; }
        public Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default)
        {
            Writes++;
            return Task.FromResult(new StoredEvidence($"{eventId}/{submissionId}.png", originalFilename, "image/png", 3, 1, 1, new string('a', 64)));
        }
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
