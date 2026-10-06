using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
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

public sealed partial class SignupQuestionCreationRetryIntegrationTests(PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_question_retry").WithUsername("bingo").WithPassword("bingo_test_password"));
    private DbContextOptions<ApplicationDbContext> options = null!;
    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
    }
    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentIdenticalAddsAndLostCommitResponseRecoverOneExactResult(bool account)
    {
        var seed = await SeedAsync(); var request = Request(seed, account);
        await using var blocker = new ApplicationDbContext(options);
        await using var tx = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await blocker.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {seed.EventId} FOR UPDATE").SingleAsync();
        await using var one = new ApplicationDbContext(options); await using var two = new ApplicationDbContext(options);
        var first = Service(one).AddQuestionAsync(request); var second = Service(two).AddQuestionAsync(request);
        await AssertLockWaitersAsync(2); await tx.CommitAsync();
        var results = await Task.WhenAll(first, second);
        Assert.All(results, result => Assert.True(result.Succeeded, result.Error));
        Assert.Equal(results[0].QuestionId, results[1].QuestionId);
        Assert.Single(results, result => result.Replayed);
        await AssertCountsAsync(seed, 1);
        var before = await SnapshotAsync();
        var replay = await AddAsync(request);
        Assert.True(replay.Replayed); Assert.Equal(results[0].QuestionId, replay.QuestionId);
        Assert.Equal(request.ExpectedFormVersion, replay.SubmittedFormVersion);
        Assert.Equal(before, await SnapshotAsync());

        // Throw only after the actual PostgreSQL commit, then recover in a new DbContext.
        var lostRequest = request with { RequestId = Guid.NewGuid(), ExpectedFormVersion = replay.FormVersion };
        var lostOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).AddInterceptors(new LostCommitResponse()).Options;
        await using (var lost = new ApplicationDbContext(lostOptions))
            await Assert.ThrowsAsync<OperationCanceledException>(() => Service(lost).AddQuestionAsync(lostRequest));
        before = await SnapshotAsync();
        var recovered = await AddAsync(lostRequest);
        Assert.True(recovered.Succeeded); Assert.True(recovered.Replayed);
        Assert.NotEqual(replay.QuestionId, recovered.QuestionId);
        Assert.Equal(before, await SnapshotAsync());
        await AssertCountsAsync(seed, 2);
    }

    [Fact]
    public async Task SameLabelOtherAdminAndChangedInputNeverSubstituteForOwnedRequest()
    {
        var seed = await SeedAsync(); var request = Request(seed);
        var first = await AddAsync(request);
        var other = await AccountAsync("other-admin");
        var second = await AddAsync(request with { RequestId = Guid.NewGuid(), ActorAccountId = other.Id, ExpectedFormVersion = first.FormVersion });
        Assert.True(second.Succeeded); Assert.NotEqual(first.QuestionId, second.QuestionId);
        await using (var edit = new ApplicationDbContext(options))
        {
            (await edit.SignupQuestions.SingleAsync(x => x.Id == first.QuestionId)).UpdatePresentation("Renamed later", "Later help");
            await edit.SaveChangesAsync();
        }
        var before = await SnapshotAsync();
        var replay = await AddAsync(request);
        Assert.True(replay.Succeeded); Assert.Equal(first.QuestionId, replay.QuestionId);
        Assert.Equal(first.OriginalDefinition, replay.OriginalDefinition);
        foreach (var changed in new[] {
            request with { Label = "Different" }, request with { Required = true },
            request with { Type = SignupQuestionType.Number }, request with { HelpText = "Different help" },
            request with { ExpectedFormVersion = replay.FormVersion }, request with { ActorAccountId = other.Id } })
        {
            var denied = await AddAsync(changed);
            Assert.Equal(SignupQuestionCreationOutcome.Conflict, denied.Outcome); Assert.Null(denied.QuestionId);
        }
        Assert.Equal(before, await SnapshotAsync());
        var anotherEvent = await SeedAsync();
        before = await SnapshotAsync();
        var crossEvent = await AddAsync(request with { EventId = anotherEvent.EventId, ExpectedFormVersion = anotherEvent.Version });
        Assert.Equal(SignupQuestionCreationOutcome.Conflict, crossEvent.Outcome); Assert.Null(crossEvent.QuestionId);
        Assert.Equal(before, await SnapshotAsync());
        await AssertCountsAsync(seed, 2);
    }

    [Fact]
    public async Task CanonicalChoiceIntentAndPostResponseOptionalNormalizationSurviveReplay()
    {
        var seed = await SeedAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            (await db.SignupForms.SingleAsync(x => x.EventId == seed.EventId)).RecordAcceptedResponse(Now);
            await db.SaveChangesAsync();
        }
        var version = await FormVersionAsync(seed.EventId);
        var request = Request(seed) with { ExpectedFormVersion = version, Label = " Choice ", Type = SignupQuestionType.SingleChoice, Options = " One \n\n Two ", Required = true };
        var added = await AddAsync(request); Assert.True(added.Succeeded, added.Error);
        Assert.Equal(SignupQuestionCreationOutcome.CompletedAsOptional, added.Outcome);
        Assert.True(added.RequiredNormalizedToOptional); Assert.False(added.OriginalDefinition!.Required);
        Assert.Contains("first signup response", added.Message);
        await using (var db = new ApplicationDbContext(options))
        {
            var question = await db.SignupQuestions.SingleAsync(x => x.Id == added.QuestionId);
            Assert.False(question.Required); Assert.Equal("One\nTwo", question.Options);
            Assert.Equal(Now, (await db.SignupForms.SingleAsync(x => x.EventId == seed.EventId)).FirstResponseAt);
        }
        var before = await SnapshotAsync();
        var replay = await AddAsync(request with { Label = "Choice", Options = "One\nTwo" });
        Assert.True(replay.Succeeded); Assert.True(replay.Replayed); Assert.Equal(added.QuestionId, replay.QuestionId);
        Assert.Equal(added.Outcome, replay.Outcome); Assert.Equal(added.OriginalDefinition, replay.OriginalDefinition);
        Assert.Equal(added.Message, replay.Message);
        Assert.Equal(SignupQuestionCreationOutcome.Conflict, (await AddAsync(request with { Required = false })).Outcome);
        Assert.Equal(SignupQuestionCreationOutcome.Conflict, (await AddAsync(request with { Options = "One\nThree" })).Outcome);
        Assert.Equal(before, await SnapshotAsync());
    }

    [Fact]
    public async Task CurrentRolesVisibilityAndDraftStateApplyToNewWritesAndReplay()
    {
        var seed = await SeedAsync(); var request = Request(seed); var created = await AddAsync(request);
        await using (var db = new ApplicationDbContext(options))
        { (await db.Events.SingleAsync(x => x.Id == seed.EventId)).SetDraftLocked(true, Now); await db.SaveChangesAsync(); }
        Assert.True((await AddAsync(request)).Succeeded);
        Assert.Equal(SignupQuestionCreationOutcome.Locked, (await AddAsync(request with { RequestId = Guid.NewGuid(), ExpectedFormVersion = created.FormVersion })).Outcome);
        foreach (var disabled in new[] { false, true })
        {
            await using (var db = new ApplicationDbContext(options))
            {
                var actor = await db.Accounts.SingleAsync(x => x.Id == seed.Admin.Id);
                if (disabled) { actor.SetGlobalRole(GlobalRole.Admin); actor.Disable(Now); }
                else actor.SetGlobalRole(GlobalRole.User);
                await db.SaveChangesAsync();
            }
            var before = await SnapshotAsync();
            var denied = await AddAsync(request); Assert.Equal(SignupQuestionCreationOutcome.Forbidden, denied.Outcome); Assert.Null(denied.QuestionId);
            Assert.Equal(before, await SnapshotAsync());
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var actor = await db.Accounts.SingleAsync(x => x.Id == seed.Admin.Id); actor.Enable(); actor.SetGlobalRole(GlobalRole.SuperAdmin);
            var ev = await db.Events.SingleAsync(x => x.Id == seed.EventId);
            db.Entry(ev).Property(x => x.State).CurrentValue = EventState.Archived;
            ev.Hide(actor.Id, Now, null, "Synthetic quarantine"); await db.SaveChangesAsync();
        }
        var hiddenBefore = await SnapshotAsync();
        var hidden = await AddAsync(request); Assert.Equal(SignupQuestionCreationOutcome.Unavailable, hidden.Outcome); Assert.Null(hidden.QuestionId);
        Assert.Equal(hiddenBefore, await SnapshotAsync());
    }

    [Fact]
    public async Task DeletedFieldAndDiscardedEventRetainIdentityWithoutRecreationOrForeignKeyBlock()
    {
        var seed = await SeedAsync(); var request = Request(seed); var created = await AddAsync(request);
        await using (var db = new ApplicationDbContext(options))
        {
            var question = await db.SignupQuestions.SingleAsync(x => x.Id == created.QuestionId);
            var deleted = await Service(db).ApplyQuestionMutationAsync(new(seed.EventId, question.Id, seed.Admin.Id, seed.Admin.LoginName,
                SignupQuestionMutationKind.DeleteQuestion, true, 0, 0, question.Version));
            Assert.True(deleted.Succeeded, deleted.Error);
        }
        var before = await SnapshotAsync();
        var removed = await AddAsync(request); Assert.Equal(SignupQuestionCreationOutcome.Removed, removed.Outcome);
        Assert.Equal(created.QuestionId, removed.QuestionId); Assert.True(removed.Replayed); Assert.False(removed.Succeeded);
        Assert.Equal(before, await SnapshotAsync());
        await using (var db = new ApplicationDbContext(options))
        {
            var ev = await db.Events.SingleAsync(x => x.Id == seed.EventId);
            var discarded = await new EventDestructiveLifecycleService(db, new FixedClock()).DiscardAsync(seed.EventId, ev.Version, true, new(seed.Admin.Id, seed.Admin.LoginName));
            Assert.True(discarded.Succeeded, discarded.Error);
        }
        before = await SnapshotAsync();
        var unavailable = await AddAsync(request); Assert.Equal(SignupQuestionCreationOutcome.Unavailable, unavailable.Outcome); Assert.Null(unavailable.QuestionId);
        Assert.Equal(before, await SnapshotAsync());
        await using var verify = new ApplicationDbContext(options);
        Assert.Empty(await verify.SignupQuestions.ToListAsync()); Assert.Empty(await verify.SignupForms.ToListAsync());
        Assert.Equal(created.QuestionId, (await verify.SignupQuestionCreationOperations.SingleAsync()).QuestionId);
    }

    [Fact]
    public async Task AuditFailureRollsBackIdentityDefinitionAndVersionAndOriginalRequestCanRetry()
    {
        var seed = await SeedAsync(); var request = Request(seed); var before = await SnapshotAsync();
        await using (var db = new ApplicationDbContext(options))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit_entries ADD CONSTRAINT au06_fail_audit CHECK (action <> 'signup_question.created')");
        await Assert.ThrowsAsync<DbUpdateException>(() => AddAsync(request));
        Assert.Equal(before, await SnapshotAsync());
        await using (var db = new ApplicationDbContext(options))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit_entries DROP CONSTRAINT au06_fail_audit");
        Assert.True((await AddAsync(request)).Succeeded); await AssertCountsAsync(seed, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualHttpFormsBindIdentityBaselineAndEnforceAuthenticationAntiforgery(bool account)
    {
        var seed = await SeedAsync(); await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var route = $"/Admin/Events/Questions/{seed.EventId}";
        using (var anonymous = await client.GetAsync(route)) Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        await LoginAsync(client, seed.Admin.LoginName);
        var page = await client.GetStringAsync(route);
        var renderedIds = Regex.Matches(page, "name=\"addRequestId\" value=\"([^\"]+)\"").Select(x => x.Groups[1].Value).ToArray();
        Assert.Equal(3, renderedIds.Distinct().Count()); Assert.All(renderedIds, x => Assert.NotEqual(Guid.Empty, Guid.Parse(x)));
        var fields = account ? new Dictionary<string,string> { ["role"] = "Playing" } : new() { ["Input.Label"] = "HTTP question", ["Input.Type"] = "Text" };
        fields["expectedFormVersion"] = seed.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fields["addRequestId"] = renderedIds[account ? 0 : 2];
        var postRoute = account ? route + "?handler=AddAccount" : route;
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        var before = await SnapshotAsync();
        using (var noToken = await client.PostAsync(postRoute, new FormUrlEncodedContent(fields))) Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        Assert.Equal(before, await SnapshotAsync());
        fields["__RequestVerificationToken"] = Token(page);
        foreach (var identity in new[] { "", "invalid-guid" })
        {
            var invalid = new Dictionary<string,string>(fields) { ["addRequestId"] = identity };
            using var response = await client.PostAsync(postRoute, new FormUrlEncodedContent(invalid));
            Assert.False((await response.Content.ReadFromJsonAsync<SignupQuestionCreationResult>())!.Succeeded);
            Assert.Equal(before, await SnapshotAsync());
        }
        var first = await PostAsync(client, postRoute, fields); Assert.True(first.Succeeded, first.Error);
        Assert.Equal(Guid.Parse(fields["addRequestId"]), first.RequestId); Assert.Equal(seed.Version, first.SubmittedFormVersion);
        before = await SnapshotAsync();
        var replay = await PostAsync(client, postRoute, fields);
        Assert.True(replay.Succeeded); Assert.True(replay.Replayed); Assert.Equal(first.QuestionId, replay.QuestionId);
        Assert.Equal(seed.Version, replay.SubmittedFormVersion); Assert.Equal(before, await SnapshotAsync());
        fields["addRequestId"] = Guid.NewGuid().ToString();
        Assert.Equal(SignupQuestionCreationOutcome.Stale, (await PostAsync(client, postRoute, fields)).Outcome);
        fields["expectedFormVersion"] = "broken";
        Assert.Equal(SignupQuestionCreationOutcome.Stale, (await PostAsync(client, postRoute, fields)).Outcome);
        Assert.Equal(before, await SnapshotAsync());
        await using (var db = new ApplicationDbContext(options))
        {
            var ev = await db.Events.SingleAsync(x => x.Id == seed.EventId);
            db.Entry(ev).Property(x => x.State).CurrentValue = EventState.Archived;
            await db.SaveChangesAsync();
        }
        before = await SnapshotAsync();
        fields["addRequestId"] = first.RequestId.ToString();
        fields["expectedFormVersion"] = seed.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var laterReplay = await PostAsync(client, postRoute, fields);
        Assert.True(laterReplay.Succeeded); Assert.Equal(first.QuestionId, laterReplay.QuestionId);
        fields["addRequestId"] = Guid.NewGuid().ToString();
        fields["expectedFormVersion"] = first.FormVersion!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await AssertTerminalReadOnlyAsync(client, seed.EventId, postRoute, fields);
        // The replay exemption does not open edit/move/system-field mutations.
        fields["questionId"] = first.QuestionId!.Value.ToString(); fields["up"] = "true";
        using (var move = await client.PostAsync(route + "?handler=Move", new FormUrlEncodedContent(fields)))
            Assert.Equal(HttpStatusCode.Redirect, move.StatusCode);
        Assert.Equal(before, await SnapshotAsync());
        await AssertCountsAsync(seed, 1);
    }

    [Fact]
    public async Task InvalidRolesFormatsAndStaleNewRequestsDoNotAlterProtectedFields()
    {
        var seed = await SeedAsync(); var request = Request(seed);
        var before = await SnapshotAsync();
        foreach (var invalid in new[] { request with { Type = (SignupQuestionType)99 }, request with { AccountRole = EventCharacterRole.Playing },
            Request(seed, true) with { Required = true }, Request(seed, true) with { AccountRole = (EventCharacterRole)99 },
            request with { ExpectedFormVersion = null }, request with { ExpectedFormVersion = seed.Version + 1 } })
            Assert.False((await AddAsync(invalid)).Succeeded);
        Assert.Equal(before, await SnapshotAsync());
        var created = await AddAsync(request with { Required = true }); Assert.True(created.Succeeded);
        Assert.Equal(SignupQuestionCreationOutcome.Completed, created.Outcome);
        Assert.False(created.RequiredNormalizedToOptional); Assert.True(created.OriginalDefinition!.Required); Assert.Null(created.Message);
        await using var db = new ApplicationDbContext(options);
        Assert.True((await db.SignupQuestions.SingleAsync(x => x.Id == created.QuestionId)).Required);
        Assert.Equal(2, await db.SignupQuestions.CountAsync(x => x.SystemField != SignupSystemField.None && x.Active));
        Assert.Empty(await db.OsrsCharacters.ToListAsync()); Assert.Empty(await db.AccountOsrsCharacters.ToListAsync());
    }

    private async Task<SignupQuestionCreationResult> AddAsync(SignupQuestionCreationRequest request)
    { await using var db = new ApplicationDbContext(options); return await Service(db).AddQuestionAsync(request); }
    private static SignupService Service(ApplicationDbContext db) => new(db, new SecretHasher(), new FixedClock());
    private static SignupQuestionCreationRequest Request(Seed seed, bool account = false) => new(Guid.NewGuid(), seed.EventId, seed.Admin.Id, seed.Version,
        account ? string.Empty : "Same label", account ? SignupQuestionType.Account : SignupQuestionType.Text, AccountRole: account ? EventCharacterRole.Playing : null);
    private async Task<Account> AccountAsync(string prefix)
    {
        var name = $"{prefix}-{Guid.NewGuid():N}";
        var account = Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), Now); account.SetGlobalRole(GlobalRole.Admin);
        account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "synthetic-retry-password"), false, Now, incrementVersion: false);
        await using var db = new ApplicationDbContext(options); db.Accounts.Add(account); await db.SaveChangesAsync(); return account;
    }
    private async Task<Seed> SeedAsync()
    {
        var admin = await AccountAsync("retry-admin");
        var ev = new BingoEvent(Guid.NewGuid(), "Retry tests", $"retry-{Guid.NewGuid():N}", "UTC", admin.Id, Now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        var form = new SignupForm(Guid.NewGuid(), ev.Id, Now);
        var primary = new SignupQuestion(Guid.NewGuid(), form.Id, ev.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var captain = new SignupQuestion(Guid.NewGuid(), form.Id, ev.Id, "captain_volunteer", "Captain", SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer);
        await using var db = new ApplicationDbContext(options); db.AddRange(ev, form, primary, captain); await db.SaveChangesAsync();
        return new(admin, ev.Id, form.Version);
    }
    private async Task<int> FormVersionAsync(Guid eventId)
    { await using var db = new ApplicationDbContext(options); return await db.SignupForms.Where(x => x.EventId == eventId).Select(x => x.Version).SingleAsync(); }
    private async Task AssertCountsAsync(Seed seed, int adds)
    {
        await using var db = new ApplicationDbContext(options);
        Assert.Equal(adds + 2, await db.SignupQuestions.CountAsync(x => x.EventId == seed.EventId));
        Assert.Equal(adds, await db.SignupQuestionCreationOperations.CountAsync(x => x.EventId == seed.EventId));
        Assert.Equal(adds, await db.AuditEntries.CountAsync(x => x.EventId == seed.EventId));
        Assert.Equal(seed.Version + adds, await FormVersionAsync(seed.EventId));
    }
    private async Task<string> SnapshotAsync()
    {
        await using var db = new ApplicationDbContext(options);
        return JsonSerializer.Serialize(new {
            Events = await db.Events.AsNoTracking().OrderBy(x => x.Id).ToListAsync(), Accounts = await db.Accounts.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            Forms = await db.SignupForms.AsNoTracking().OrderBy(x => x.Id).ToListAsync(), Questions = await db.SignupQuestions.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            Operations = await db.SignupQuestionCreationOperations.AsNoTracking().OrderBy(x => x.RequestId).ToListAsync(), Audits = await db.AuditEntries.AsNoTracking().OrderBy(x => x.Id).ToListAsync()
        });
    }
    private async Task AssertLockWaitersAsync(int count)
    {
        await using var observer = new NpgsqlConnection(database.GetConnectionString()); await observer.OpenAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND cardinality(pg_blocking_pids(pid)) > 0", observer);
            if ((long)(await command.ExecuteScalarAsync(deadline.Token))! >= count) return;
            await Task.Delay(20, deadline.Token);
        }
    }
    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
        .UseEnvironment("Testing").UseSetting("ConnectionStrings:Database", database.GetConnectionString()).ConfigureServices(services => {
            services.RemoveAll<IHostedService>(); services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FixedClock());
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        }));
    private static async Task LoginAsync(HttpClient client, string name)
    {
        var page = await client.GetStringAsync("/Account/Login");
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["Input.Username"] = name, ["Input.Password"] = "synthetic-retry-password", ["__RequestVerificationToken"] = Token(page) }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private static async Task<SignupQuestionCreationResult> PostAsync(HttpClient client, string route, Dictionary<string,string> fields)
    {
        using var response = await client.PostAsync(route, new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<SignupQuestionCreationResult>())!;
    }
    private static string Token(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    private sealed record Seed(Account Admin, Guid EventId, int Version);
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class LostCommitResponse : DbTransactionInterceptor
    {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException("Controlled response loss after real PostgreSQL commit.");
    }
}
