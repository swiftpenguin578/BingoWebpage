using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class SignupSetupVersionIntegrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_signup_versions").WithUsername("bingo").WithPassword("bingo_test_password").Build();
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
    [InlineData("Add")]
    [InlineData("AddAccount")]
    [InlineData("Edit")]
    [InlineData("EditAccount")]
    [InlineData("Move")]
    [InlineData("CoCaptain")]
    public async Task SubmittedBaselineRejectsStaleMissingAndMalformedBeforeAnyMutation(string operation)
    {
        var seed = await SeedAsync();
        await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(client, seed.Admin);
        var route = $"/Admin/Events/Questions/{seed.EventId}";
        var page = await client.GetStringAsync(route);
        var originalVersion = FormVersion(page);
        using (var first = await PostAsync(client, route, "EditAccount", page, originalVersion, Fields("EditAccount", seed)))
            Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        var before = await SnapshotAsync(seed.EventId);
        foreach (var baseline in new string?[] { originalVersion, null, "not-a-version" })
        {
            using var rejected = await PostAsync(client, route, operation, page, baseline, Fields(operation, seed));
            Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
            Assert.Contains("This signup form changed", await client.GetStringAsync(rejected.Headers.Location));
            Assert.Equal(before, await SnapshotAsync(seed.EventId));
        }
        page = await client.GetStringAsync(route);
        using var accepted = await PostAsync(client, route, operation, page, FormVersion(page), Fields(operation, seed));
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        await using var verify = new ApplicationDbContext(options);
        Assert.True(await verify.SignupForms.Where(x => x.EventId == seed.EventId).Select(x => x.Version).SingleAsync() > int.Parse(FormVersion(page), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(2, await verify.AuditEntries.CountAsync(x => x.EventId == seed.EventId));
    }

    [Fact]
    public async Task ConcurrentHttpAddsSerializeAndRejectLoserWithoutExtraDefinitionOrAudit()
    {
        var seed = await SeedAsync();
        await using var factory = Factory();
        using var one = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var two = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(one, seed.Admin); await LoginAsync(two, seed.Admin);
        var route = $"/Admin/Events/Questions/{seed.EventId}";
        var pageOne = await one.GetStringAsync(route); var pageTwo = await two.GetStringAsync(route);
        await using var blocker = new ApplicationDbContext(options);
        await using var transaction = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await blocker.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {seed.EventId} FOR UPDATE").SingleAsync();
        var first = PostAsync(one, route, "Add", pageOne, FormVersion(pageOne), new() { ["Input.Label"] = "First", ["Input.Type"] = "Text" });
        var second = PostAsync(two, route, "Add", pageTwo, FormVersion(pageTwo), new() { ["Input.Label"] = "Second", ["Input.Type"] = "Text" });
        await AssertEventLockWaitersAsync(2);
        await transaction.CommitAsync();
        using var resultOne = await first; using var resultTwo = await second;
        Assert.True(resultOne.StatusCode == HttpStatusCode.Redirect, await resultOne.Content.ReadAsStringAsync()); Assert.True(resultTwo.StatusCode == HttpStatusCode.Redirect, await resultTwo.Content.ReadAsStringAsync());
        var bodies = new[] { await one.GetStringAsync(resultOne.Headers.Location), await two.GetStringAsync(resultTwo.Headers.Location) };
        Assert.Single(bodies, x => x.Contains("This signup form changed", StringComparison.Ordinal));
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(5, await verify.SignupQuestions.CountAsync(x => x.EventId == seed.EventId));
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == seed.EventId).ToListAsync());
        Assert.Equal(int.Parse(FormVersion(pageOne), System.Globalization.CultureInfo.InvariantCulture) + 1,
            await verify.SignupForms.Where(x => x.EventId == seed.EventId).Select(x => x.Version).SingleAsync());
    }

    [Fact]
    public async Task SeparateSettingsPostsReturnSavedValuesAndImmutableSubmittedBaseline()
    {
        var seed = await SeedAsync();
        await using var factory = Factory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(client, seed.Admin);
        var route = $"/Admin/Events/Participants/{seed.EventId}";
        var page = await client.GetStringAsync(route);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        var version = await EventVersionAsync(seed.EventId);
        var capacity = await SettingsPostAsync(client, $"/Admin/Events/Manage/{seed.EventId}?handler=Capacity", page,
            new() { ["EventVersion"] = version.ToString(System.Globalization.CultureInfo.InvariantCulture), ["NewCap"] = "7" });
        Assert.True(capacity.Succeeded, capacity.Error);
        Assert.Equal(version, capacity.SubmittedEventVersion);
        Assert.Equal(7, capacity.Settings!.ParticipantCap);
        Assert.True(capacity.Settings.WaitingListEnabled);
        Assert.Equal(await EventVersionAsync(seed.EventId), capacity.Settings.EventVersion);
        Assert.True(capacity.Settings.EventVersion > version);
        var before = await SnapshotAsync(seed.EventId);
        var stale = await SettingsPostAsync(client, route + "?handler=SignupCode", page,
            new() { ["SignupCode.Version"] = version.ToString(System.Globalization.CultureInfo.InvariantCulture), ["SignupCode.RequireSignupCode"] = "true", ["SignupCode.NewSignupCode"] = "synthetic-only" });
        Assert.False(stale.Succeeded);
        Assert.Equal(version, stale.SubmittedEventVersion);
        Assert.Equal(capacity.Settings, stale.Settings);
        Assert.Equal(before, await SnapshotAsync(seed.EventId));
        var code = await SettingsPostAsync(client, route + "?handler=SignupCode", page,
            new() { ["SignupCode.Version"] = capacity.Settings.EventVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), ["SignupCode.RequireSignupCode"] = "true", ["SignupCode.NewSignupCode"] = "synthetic-only" });
        Assert.True(code.Succeeded, code.Error);
        Assert.Equal(capacity.Settings.EventVersion, code.SubmittedEventVersion);
        Assert.True(code.Settings!.RequireSignupCode); Assert.True(code.Settings.HasSignupCode);
        Assert.Equal(7, code.Settings.ParticipantCap);
        Assert.Equal(await EventVersionAsync(seed.EventId), code.Settings.EventVersion);
        Assert.Equal(version, capacity.SubmittedEventVersion); // The earlier operation retains its baseline.
        before = await SnapshotAsync(seed.EventId);
        var staleCapacity = await SettingsPostAsync(client, $"/Admin/Events/Manage/{seed.EventId}?handler=Capacity", page,
            new() { ["EventVersion"] = capacity.Settings.EventVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), ["NewCap"] = "9" });
        Assert.False(staleCapacity.Succeeded); Assert.Equal(code.Settings, staleCapacity.Settings);
        Assert.Equal(before, await SnapshotAsync(seed.EventId));
        var unchanged = await SettingsPostAsync(client, $"/Admin/Events/Manage/{seed.EventId}?handler=Capacity", page,
            new() { ["EventVersion"] = code.Settings.EventVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), ["NewCap"] = "7" });
        Assert.True(unchanged.Succeeded, unchanged.Error);
        Assert.Equal(code.Settings.EventVersion, unchanged.SubmittedEventVersion);
        Assert.True(unchanged.Settings!.EventVersion > code.Settings.EventVersion);
        Assert.Equal(code.Settings with { EventVersion = unchanged.Settings.EventVersion }, unchanged.Settings);
        Assert.Equal(await EventVersionAsync(seed.EventId), unchanged.Settings.EventVersion);
    }

    [Fact]
    public async Task ConcurrentSettingsReturnCommittedWinnerAndFreshLoserSnapshot()
    {
        var seed = await SeedAsync();
        await using var factory = Factory();
        using var one = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var two = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(one, seed.Admin); await LoginAsync(two, seed.Admin);
        var route = $"/Admin/Events/Participants/{seed.EventId}";
        var pageOne = await one.GetStringAsync(route); var pageTwo = await two.GetStringAsync(route);
        one.DefaultRequestHeaders.Accept.ParseAdd("application/json"); two.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        var version = await EventVersionAsync(seed.EventId);
        await using var blocker = new ApplicationDbContext(options);
        await using var transaction = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await blocker.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {seed.EventId} FOR UPDATE").SingleAsync();
        var capacity = SettingsPostAsync(one, $"/Admin/Events/Manage/{seed.EventId}?handler=Capacity", pageOne, new() { ["EventVersion"] = version.ToString(System.Globalization.CultureInfo.InvariantCulture), ["NewCap"] = "7" });
        var code = SettingsPostAsync(two, route + "?handler=SignupCode", pageTwo, new() { ["SignupCode.Version"] = version.ToString(System.Globalization.CultureInfo.InvariantCulture), ["SignupCode.RequireSignupCode"] = "true", ["SignupCode.NewSignupCode"] = "synthetic-only" });
        await AssertEventLockWaitersAsync(2); await transaction.CommitAsync();
        var results = await Task.WhenAll(capacity, code);
        var winner = Assert.Single(results, x => x.Succeeded); var loser = Assert.Single(results, x => !x.Succeeded);
        Assert.Equal(version, winner.SubmittedEventVersion); Assert.Equal(version, loser.SubmittedEventVersion);
        Assert.Equal(winner.Settings, loser.Settings);
        Assert.Equal(await EventVersionAsync(seed.EventId), loser.Settings!.EventVersion);
        await using var verify = new ApplicationDbContext(options);
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == seed.EventId).ToListAsync());
        Assert.True(winner.Settings!.ParticipantCap == 7 ^ winner.Settings.RequireSignupCode);
    }

    [Fact]
    public async Task FirstResponseStillNormalizesAddsAndLocksExistingShape()
    {
        var seed = await SeedAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            (await db.SignupForms.SingleAsync(x => x.EventId == seed.EventId)).RecordAcceptedResponse(Now);
            await db.SaveChangesAsync();
        }
        await using var factory = Factory(); using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        await LoginAsync(client, seed.Admin);
        var route = $"/Admin/Events/Questions/{seed.EventId}"; var page = await client.GetStringAsync(route);
        var before = await SnapshotAsync(seed.EventId);
        using (var rejected = await PostAsync(client, route, "Edit", page, FormVersion(page), Fields("Edit", seed)))
        {
            Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
            Assert.Contains("Answer format is locked", await client.GetStringAsync(rejected.Headers.Location));
        }
        Assert.Equal(before, await SnapshotAsync(seed.EventId));
        using var added = await PostAsync(client, route, "Add", page, FormVersion(page), new() { ["Input.Label"] = "New required", ["Input.Type"] = "Text", ["Input.Required"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        await using var verify = new ApplicationDbContext(options);
        Assert.False((await verify.SignupQuestions.SingleAsync(x => x.EventId == seed.EventId && x.Label == "New required")).Required);
        Assert.Equal(Now, (await verify.SignupForms.SingleAsync(x => x.EventId == seed.EventId)).FirstResponseAt);
    }

    private async Task AssertEventLockWaitersAsync(int count)
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
        .UseSetting("ConnectionStrings:Database", database.GetConnectionString())
        .ConfigureServices(services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FixedClock()); }));
    private async Task<Seed> SeedAsync()
    {
        await using var db = new ApplicationDbContext(options);
        var admin = Account.CreateWebsite(Guid.NewGuid(), "version-admin", "VERSION-ADMIN", Now); admin.SetGlobalRole(GlobalRole.Admin);
        admin.SetPassword(new PasswordHasher<Account>().HashPassword(admin, "synthetic-test-password"), false, Now, incrementVersion: false);
        var ev = new BingoEvent(Guid.NewGuid(), "Version tests", $"versions-{Guid.NewGuid():N}", "UTC", admin.Id, Now, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb); ev.SetParticipantCap(5);
        var form = new SignupForm(Guid.NewGuid(), ev.Id, Now);
        var custom = new SignupQuestion(Guid.NewGuid(), form.Id, ev.Id, "custom", "Custom", SignupQuestionType.Text, false, 1, null);
        var other = new SignupQuestion(Guid.NewGuid(), form.Id, ev.Id, "other", "Other", SignupQuestionType.Text, false, 2, null);
        var account = new SignupQuestion(Guid.NewGuid(), form.Id, ev.Id, "extra", "Extra", SignupQuestionType.Account, false, 3, null, accountAnswerRole: EventCharacterRole.Informational);
        var co = new SignupQuestion(Guid.NewGuid(), form.Id, ev.Id, SignupQuestion.CoCaptainKey, SignupQuestion.CoCaptainLabel, SignupQuestionType.Text, false, 4, null, SignupSystemField.CoCaptainName); co.Deactivate(admin.Id, Now, "disabled");
        db.AddRange(admin, ev, form, custom, other, account, co); await db.SaveChangesAsync();
        return new(admin.LoginName, ev.Id, custom.Id, account.Id, co.Id);
    }
    private async Task<long> EventVersionAsync(Guid id)
    { await using var db = new ApplicationDbContext(options); return await db.Events.Where(x => x.Id == id).Select(x => x.Version).SingleAsync(); }
    private async Task<string> SnapshotAsync(Guid id)
    {
        await using var db = new ApplicationDbContext(options);
        return JsonSerializer.Serialize(new {
            Event = await db.Events.AsNoTracking().SingleAsync(x => x.Id == id),
            Form = await db.SignupForms.AsNoTracking().SingleAsync(x => x.EventId == id),
            Questions = await db.SignupQuestions.AsNoTracking().Where(x => x.EventId == id).OrderBy(x => x.Id).ToListAsync(),
            Answers = await db.SignupAnswers.AsNoTracking().Where(x => db.SignupQuestions.Any(q => q.EventId == id && q.Id == x.SignupQuestionId)).OrderBy(x => x.Id).ToListAsync(),
            Participants = await db.EventParticipants.AsNoTracking().Where(x => x.EventId == id).OrderBy(x => x.Id).ToListAsync(),
            Assignments = await db.EventParticipantCharacters.AsNoTracking().Where(x => x.EventId == id).OrderBy(x => x.Id).ToListAsync(),
            Audits = await db.AuditEntries.AsNoTracking().Where(x => x.EventId == id).OrderBy(x => x.Id).ToListAsync(),
            Notifications = await db.PersonalNotifications.AsNoTracking().Where(x => x.EventId == id).OrderBy(x => x.Id).ToListAsync()
        });
    }
    private static Dictionary<string, string> Fields(string op, Seed s) => op switch {
        "Add" => new() { ["Input.Label"] = "Added", ["Input.Type"] = "Text" },
        "AddAccount" => new() { ["role"] = "Playing" },
        "Edit" => new() { ["questionId"] = s.Custom.ToString(), ["Edit.Label"] = "Edited", ["Edit.Type"] = "Text" },
        "EditAccount" => new() { ["questionId"] = s.Account.ToString(), ["Account.Label"] = "Renamed" },
        "Move" => new() { ["questionId"] = s.Custom.ToString(), ["up"] = "false" },
        _ => new() { ["questionId"] = s.CoCaptain.ToString(), ["enabled"] = "true" }
    };
    private static async Task LoginAsync(HttpClient client, string login)
    {
        var page = await client.GetStringAsync("/Account/Login");
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> { ["Input.Username"] = login, ["Input.Password"] = "synthetic-test-password", ["__RequestVerificationToken"] = Token(page) }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string route, string handler, string page, string? version, Dictionary<string,string> fields)
    {
        if (handler is "Add" or "AddAccount") fields["addRequestId"] = Guid.NewGuid().ToString();
        fields["__RequestVerificationToken"] = Token(page); if (version is not null) fields["expectedFormVersion"] = version;
        return client.PostAsync(handler == "Add" ? route : route + "?handler=" + handler, new FormUrlEncodedContent(fields));
    }
    private static async Task<SignupAdministrationResult> SettingsPostAsync(HttpClient client, string route, string page, Dictionary<string,string> fields)
    {
        fields["__RequestVerificationToken"] = Token(page);
        using var response = await client.PostAsync(route, new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("synthetic-only", body); Assert.DoesNotContain("signupCodeHash", body);
        return (await response.Content.ReadFromJsonAsync<SignupAdministrationResult>())!;
    }
    private static string Token(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    private static string FormVersion(string page) => Regex.Match(page, "name=\"expectedFormVersion\" value=\"([^\"]+)\"").Groups[1].Value;
    private sealed record Seed(string Admin, Guid EventId, Guid Custom, Guid Account, Guid CoCaptain);
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
