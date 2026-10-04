using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Bingo.Application.Access;
using Bingo.Application.Events;
using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Teams;
using Bingo.Web;
using Bingo.Web.Events;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
    [InlineData("manual")]
    public async Task LiveStartDoesNotRequireCaptainOrEmergencyCredentials(string mode)
    {
        var setup = await SeedAsync();
        await using var factory = Factory();
        using var scope = factory.Services.CreateScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<IEventLifecycleService>();
        var readiness = await lifecycle.GetStartReadinessAsync(setup.EventId);
        Assert.True(readiness!.CanProceed, string.Join(", ", readiness.Blockers.Select(x => x.Code)));
        if (mode == "scheduled") { clock.Set(setup.ScheduledStart); await lifecycle.ProcessDueAsync(); }
        else Assert.True((await lifecycle.StartNowAsync(setup.EventId, await VersionAsync(), true, "Controlled early start", new(setup.AdminId, "readiness-admin"))).Succeeded);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(clock.GetUtcNow(), (await verify.Events.SingleAsync()).ActualStartedAt);
        Assert.Null(scope.ServiceProvider.GetService<EmergencyCredentialLifecycleService>());
        Assert.Null(scope.ServiceProvider.GetService<EmergencyCredentialService>());
        Assert.Null(scope.ServiceProvider.GetService<CaptainAccountProvisioner>());
    }

    [Theory]
    [InlineData(DraftPublicationMethod.WebsiteDraft)]
    [InlineData(DraftPublicationMethod.DirectRoster)]
    public async Task LiveStartReadinessAcceptsEveryApprovedPublicationMethod(DraftPublicationMethod method)
    {
        var setup = await SeedAsync(publicationMethod: method);
        await using var factory = Factory();
        using var scope = factory.Services.CreateScope();
        var lifecycle = scope.ServiceProvider.GetRequiredService<IEventLifecycleService>();

        var readiness = await lifecycle.GetStartReadinessAsync(setup.EventId);
        Assert.True(readiness!.CanProceed, string.Join(", ", readiness.Blockers.Select(x => x.Code)));
        await using var db = new ApplicationDbContext(options);
        Assert.Equal(method, await db.DraftPublicationCycles.Select(x => x.PublicationMethod).SingleAsync());
    }

    [Theory]
    [InlineData(PasswordCredentialTokenPurpose.EmergencySetup)]
    [InlineData(PasswordCredentialTokenPurpose.EmergencyReset)]
    [InlineData(PasswordCredentialTokenPurpose.Reset)]
    public async Task RetainedTokensCannotChangeEmergencyPasswords(PasswordCredentialTokenPurpose purpose)
    {
        var setup = await SeedAsync();
        var emergency = await SeedEmergencyAsync(setup);
        const string raw = "retained-unconsumed-emergency-token";
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.PasswordCredentialTokens.Add(new(Guid.NewGuid(), emergency.Id, purpose, AccountIdentityService.Hash(raw), clock.GetUtcNow().AddHours(1), clock.GetUtcNow(), setup.AdminId));
            await seed.SaveChangesAsync();
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var identities = new AccountIdentityService(db, new PasswordHasher<Account>(), clock);
            await Assert.ThrowsAsync<InvalidOperationException>(() => identities.ConsumeResetAsync(raw, "replacement-password", CancellationToken.None));
        }
        await using var verify = new ApplicationDbContext(options);
        var persisted = await verify.Accounts.SingleAsync(x => x.Id == emergency.Id);
        Assert.Equal(emergency.PasswordHash, persisted.PasswordHash);
        Assert.Equal(emergency.PasswordVersion, persisted.PasswordVersion);
        Assert.Null((await verify.PasswordCredentialTokens.SingleAsync()).UsedAt);
        Assert.Empty(await verify.AuditEntries.ToListAsync());
    }

    [Fact]
    public async Task ExistingCookieLoginAndRemovedAdminRoutesFailClosed()
    {
        var setup = await SeedAsync();
        var emergency = await SeedEmergencyAsync(setup);
        await using var factory = Factory();
        using var admin = Client(factory);
        await LoginAsync(admin, "readiness-admin");
        using (var removed = await admin.GetAsync("/Admin/Accounts/Create")) Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
        using (var removed = await PostAsync(admin, "/Admin/Accounts/Create", "/Admin/Accounts", new())) Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
        using (var removed = await admin.GetAsync($"/Admin/Accounts/Manage/{emergency.Id}")) Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
        foreach (var handler in new[] { "EnableEmergency", "DisableEmergency", "GenerateEmergencyLink", "GenerateResetLink", "Disable", "Restore" })
        {
            using var response = await PostAsync(admin, $"/Admin/Accounts/Manage/{emergency.Id}?handler={handler}", "/Admin/Accounts", new());
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using var legacy = Client(factory);
        Assert.False(await TryLoginAsync(legacy, emergency.LoginName));
        var cookie = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, emergency.Id.ToString()), new Claim(ClaimTypes.Name, emergency.LoginName),
            new Claim(ClaimTypes.Role, "Captain"), new Claim(AccountClaims.AccountType, AccountType.EmergencyCaptain.ToString()),
            new Claim(AccountClaims.AuthenticationMethod, "password"), new Claim(AccountClaims.AuthorizationVersion, emergency.AuthorizationVersion.ToString(CultureInfo.InvariantCulture)),
            new Claim(AccountClaims.PasswordVersion, emergency.PasswordVersion.ToString(CultureInfo.InvariantCulture)), new Claim(AccountClaims.EventId, setup.EventId.ToString()), new Claim(AccountClaims.TeamId, setup.TeamId.ToString())
        }, CookieAuthenticationDefaults.AuthenticationScheme));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) }, CookieAuthenticationDefaults.AuthenticationScheme);
        legacy.DefaultRequestHeaders.Add("Cookie", $"{cookie.Cookie.Name}={cookie.TicketDataFormat.Protect(ticket)}");
        using var rejected = await legacy.GetAsync($"/Submissions?eventId={setup.EventId}&teamId={setup.TeamId}");
        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        Assert.Contains("accessChanged=true", rejected.Headers.Location!.ToString());
        await using var verify = new ApplicationDbContext(options);
        Assert.True((await verify.Accounts.SingleAsync(x => x.Id == emergency.Id)).Active);
        Assert.True((await verify.AccountEventAccesses.SingleAsync()).Enabled);
        Assert.Empty(await verify.AuditEntries.Where(x => x.Action.StartsWith("account.emergency")).ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectEmergencyCommandsAndTeamAuthorityRemainUnavailable(bool hidden)
    {
        var setup = await SeedAsync();
        var emergency = await SeedEmergencyAsync(setup);
        await using var db = new ApplicationDbContext(options);
        if (hidden) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET hidden_at = {clock.GetUtcNow()}, hidden_by_account_id = {setup.AdminId}, hidden_reason = 'Retirement fixture' WHERE id = {setup.EventId}");
        var identities = new AccountIdentityService(db, new PasswordHasher<Account>(), clock);
        var administration = new AccountAdministrationService(db, new PasswordHasher<Account>(), clock);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new EmergencyCredentialService(db, clock).CreateAsync(setup.AdminId, "new-emergency", setup.EventId, setup.TeamId, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => administration.SetEmergencyEnabledAsync(setup.AdminId, emergency.Id, true, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => administration.SetEmergencyEnabledAsync(setup.AdminId, emergency.Id, false, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => administration.RestoreAsync(setup.AdminId, emergency.Id, emergency.AuthorizationVersion, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => identities.GenerateEmergencyCredentialLinkAsync(setup.AdminId, emergency.Id, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => identities.GenerateResetLinkAsync(setup.AdminId, emergency.Id, CancellationToken.None));
        var authority = new EvidenceAuthority(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => authority.GetCurrentTeamCandidatesAsync(new(EvidenceActorKind.EmergencyCaptain, emergency.Id, setup.EventId, setup.TeamId, Guid.Empty)));
        foreach (var teamId in new[] { setup.TeamId, Guid.NewGuid() })
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => authority.AuthorizeAsync(emergency.Id, setup.EventId, teamId, setup.ParticipantId, clock.GetUtcNow()));
            Assert.Null(await new TeamFocusService(db, clock).GetContextAsync(setup.EventId, teamId, emergency.Id, false));
            Assert.False((await new TeamFocusService(db, clock).SetFocusAsync(new(setup.EventId, teamId, TeamFocusTargetKind.Row, null, 0, null, true, 0, emergency.Id))).Succeeded);
        }
        await new EmergencyCredentialLifecycleService(db, clock).ApplyAsync(CancellationToken.None);
        Assert.Empty(await db.PasswordCredentialTokens.ToListAsync());
        Assert.Empty(await db.AuditEntries.ToListAsync());
        Assert.True((await db.AccountEventAccesses.SingleAsync()).Enabled);
    }

    [Theory]
    [InlineData(TeamMembershipRole.Captain)]
    [InlineData(TeamMembershipRole.CoCaptain)]
    public async Task WebsiteLeadershipRetainsMembershipBasedEvidenceAndFocus(TeamMembershipRole role)
    {
        var setup = await SeedAsync();
        await using var db = new ApplicationDbContext(options);
        var account = await db.Accounts.SingleAsync(x => x.LoginName == "readiness-user");
        (await db.EventParticipants.SingleAsync()).AssignOwner(account);
        var member = await db.TeamMemberships.SingleAsync();
        member.ChangeRole(role);
        var item = await db.Events.SingleAsync();
        item.StartEvent(clock.GetUtcNow());
        await db.SaveChangesAsync();
        var authority = new EvidenceAuthority(db);
        var actor = await authority.AuthorizeAsync(account.Id, setup.EventId, setup.TeamId, setup.ParticipantId, clock.GetUtcNow());
        Assert.Equal(EvidenceActorKind.Captain, actor.Kind);
        var principal = new AccountAuthenticationService(db, new PasswordHasher<Account>(), clock).CreatePrincipal(account);
        var requirements = new Microsoft.AspNetCore.Authorization.IAuthorizationRequirement[] { new AccountAccessRequirement(AccountAccessMode.Full), new TeamScopeRequirement() };
        var allowed = new Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext(requirements, principal, new TeamScope(setup.EventId, setup.TeamId));
        var denied = new Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext(requirements, principal, new TeamScope(setup.EventId, Guid.NewGuid()));
        var handler = new AccountAuthorizationHandler(db, clock);
        await handler.HandleAsync(allowed);
        await handler.HandleAsync(denied);
        Assert.True(allowed.HasSucceeded);
        Assert.False(denied.HasSucceeded);
        await Assert.ThrowsAsync<InvalidOperationException>(() => authority.AuthorizeAsync(account.Id, setup.EventId, Guid.NewGuid(), setup.ParticipantId, clock.GetUtcNow()));
        var focus = new TeamFocusService(db, clock);
        Assert.True((await focus.GetContextAsync(setup.EventId, setup.TeamId, account.Id, false))!.CanMutate);
        Assert.True((await focus.SetFocusAsync(new(setup.EventId, setup.TeamId, TeamFocusTargetKind.Row, null, 0, null, true, 0, account.Id))).Succeeded);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET hidden_at = {clock.GetUtcNow()}, hidden_by_account_id = {setup.AdminId}, hidden_reason = 'Retirement fixture' WHERE id = {setup.EventId}");
        await Assert.ThrowsAsync<InvalidOperationException>(() => authority.ResolveActorAsync(account.Id, setup.EventId, setup.TeamId, clock.GetUtcNow()));
        Assert.Null(await focus.GetContextAsync(setup.EventId, setup.TeamId, account.Id, false));
    }

    private async Task<Account> SeedEmergencyAsync(Setup setup)
    {
        await using var db = new ApplicationDbContext(options);
        var account = Account.CreateEmergency(Guid.NewGuid(), "retained-emergency", "RETAINED-EMERGENCY", clock.GetUtcNow());
        account.SetPassword(new PasswordHasher<Account>().HashPassword(account, Password), false, clock.GetUtcNow(), false);
        account.Enable();
        var access = new AccountEventAccess(Guid.NewGuid(), account.Id, setup.EventId, setup.TeamId, null, clock.GetUtcNow().AddHours(-1), null, null);
        access.Enable(); db.AddRange(account, access); await db.SaveChangesAsync(); return account;
    }

    private async Task<Setup> SeedAsync(bool finalize = true, DraftPublicationMethod publicationMethod = DraftPublicationMethod.HistoricalUnknown)
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
        var item = new BingoEvent(Guid.NewGuid(), "Readiness event", "readiness-event", "UTC", admin.Id, now.AddDays(-2), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
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
        var publication = finalize ? new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, now.AddMinutes(-5), admin.Id, publicationMethod) : null;
        var publishedRoster = publication is null ? null : new DraftPublicationRoster(Guid.NewGuid(), publication.Id, team.Id, participant.Id, TeamMembershipRole.Captain, null, character.DisplayName);
        var board = new Board(Guid.NewGuid(), item.Id, "Readiness board", 1, 1);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Objective", "Complete it", "Proof", 1);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, 10, true, false, "Runs", true, 1);
        db.AddRange(admin, ordinary, item, team, draft, participant, character, assignment, membership, board, tile, requirement);
        if (publication is not null) db.Add(publication);
        if (publishedRoster is not null) db.Add(publishedRoster);
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
