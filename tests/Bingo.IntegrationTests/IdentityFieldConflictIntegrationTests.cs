using System.Data;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed partial class IdentityFieldConflictIntegrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_identity_conflicts").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;
    private WebApplicationFactory<Program> factory = null!;
    private Guid eventId;
    private string Route => $"/Admin/Events/Identity/{eventId}";

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
        foreach (var name in new[] { "first-admin", "second-admin", "ordinary-account" })
        {
            var account = Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), Now);
            if (name != "ordinary-account") account.SetGlobalRole(GlobalRole.Admin);
            account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "synthetic-identity-password"), false, Now, incrementVersion: false);
            db.Accounts.Add(account);
        }
        eventId = Guid.NewGuid();
        db.Events.Add(new BingoEvent(eventId, "Original", "identity-conflict", "UTC", db.Accounts.Local.First().Id, Now));
        await db.SaveChangesAsync();
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Testing").UseSetting("ConnectionStrings:Database", database.GetConnectionString())
            .ConfigureServices(services => {
                services.RemoveAll<IHostedService>(); services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new FixedClock());
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            }));
    }

    public async Task DisposeAsync() { await factory.DisposeAsync(); await database.DisposeAsync(); }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualConcurrentClientsMergeDisjointEditsAndRejectSameField(bool sameField)
    {
        using var first = await ClientAsync("first-admin"); using var second = await ClientAsync("second-admin");
        var a = Fields(await first.GetStringAsync(Route)); var b = Fields(await second.GetStringAsync(Route));
        a["Input.Name"] = "First proposal";
        b[sameField ? "Input.Name" : "Input.Description"] = "Second proposal";
        await using var blocker = new ApplicationDbContext(options);
        await using var tx = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await blocker.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} FOR UPDATE").SingleAsync();
        var one = first.PostAsync(Route, new FormUrlEncodedContent(a)); var two = second.PostAsync(Route, new FormUrlEncodedContent(b));
        await WaitForTwoWritersAsync(); await tx.CommitAsync();
        using var responseA = await one; using var responseB = await two;
        var responses = new[] { responseA, responseB };
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Redirect);
        var loser = responseA.StatusCode == HttpStatusCode.OK ? responseA : responseB;
        var client = ReferenceEquals(loser, responseA) ? first : second;
        var failedHtml = await loser.Content.ReadAsStringAsync();
        if (sameField) Assert.Contains("data-identity-conflicts=\"Name\"", failedHtml);
        var retry = Fields(failedHtml);
        using var retried = await client.PostAsync(Route, new FormUrlEncodedContent(retry));
        var saved = await ReadAsync();
        if (sameField)
        {
            Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
            Assert.Contains("data-identity-conflicts=\"Name\"", await retried.Content.ReadAsStringAsync());
            Assert.True(saved.Name is "First proposal" or "Second proposal");
            Assert.Null(saved.Description); Assert.Equal(1, await AuditCountAsync());
        }
        else
        {
            Assert.Equal(HttpStatusCode.Redirect, retried.StatusCode);
            Assert.Equal("First proposal", saved.Name); Assert.Equal("Second proposal", saved.Description);
            Assert.Equal(2, await AuditCountAsync());
        }
    }

    [Theory]
    [InlineData("KeepMine")]
    [InlineData("UseCurrent")]
    public async Task ConflictResolutionIsExplicitAtomicAndMustMatchReviewedCurrentField(string choice)
    {
        using var client = await ClientAsync("first-admin");
        var draft = Fields(await client.GetStringAsync(Route));
        draft["Input.Name"] = "My name"; draft["Input.Description"] = "My description";
        await EditAsync(item => item.UpdateIdentity("Their name", item.Slug, item.Description, "Their buy-in", item.Timezone));
        var originalVersion = draft["Input.Version"];
        var failed = await PostPageAsync(client, draft);
        Assert.Contains("data-identity-conflicts=\"Name\"", failed);
        var resolution = Fields(failed);
        Assert.Equal("Original", resolution["Input.OriginalName"]); Assert.Equal(originalVersion, resolution["Input.Version"]);
        Assert.Equal("My name", resolution["Input.Name"]); Assert.Equal("My description", resolution["Input.Description"]);
        Assert.Equal("Their name", resolution["Input.ReviewedName"]); Assert.Null((await ReadAsync()).Description); Assert.Equal(0, await AuditCountAsync());
        // Reposting without a choice cannot silently advance/rebase the draft.
        failed = await PostPageAsync(client, resolution); Assert.Contains("data-identity-conflicts=\"Name\"", failed);
        resolution["Input.NameResolution"] = choice;
        await EditAsync(item => item.UpdateIdentity("Newer name", item.Slug, item.Description, "Newer buy-in", item.Timezone));
        failed = await PostPageAsync(client, resolution);
        Assert.Contains("data-identity-conflicts=\"Name\"", failed); Assert.Equal(0, await AuditCountAsync());
        resolution = Fields(failed); resolution["Input.NameResolution"] = choice;
        using var savedResponse = await client.PostAsync(Route, new FormUrlEncodedContent(resolution));
        Assert.Equal(HttpStatusCode.Redirect, savedResponse.StatusCode);
        var saved = await ReadAsync();
        Assert.Equal(choice == "KeepMine" ? "My name" : "Newer name", saved.Name);
        Assert.Equal("My description", saved.Description); Assert.Equal("Newer buy-in", saved.BuyInDescription); Assert.Equal(1, await AuditCountAsync());
    }

    [Fact]
    public async Task ScheduleOnlyChangeRequiresFreshTimezoneReviewAtPersistedMicrosecondPrecision()
    {
        var unaligned = Now.AddDays(2).AddTicks(1234567);
        await EditAsync(item => { item.MarkFirstPublic(Now.AddTicks(7)); item.ConfigureInitialSchedule(null, null, null, unaligned, unaligned.AddDays(1), null); });
        var persisted = await ReadAsync();
        Assert.Equal(unaligned.UtcTicks / 10 * 10, persisted.EventStartsAt!.Value.UtcTicks);
        using var client = await ClientAsync("first-admin");
        var draft = Fields(await client.GetStringAsync(Route)); draft["Input.Timezone"] = "Europe/Copenhagen"; draft["Input.Name"] = "Pending timezone name";
        var preview = await PostPageAsync(client, draft); Assert.Contains("data-identity-timezone-preview", preview);
        var confirmed = Fields(preview); confirmed["Input.ConfirmTimezoneChange"] = "true";
        var oldFingerprint = confirmed["Input.TimezoneConfirmationSchedule"];
        await EditAsync(item => item.ConfigureInitialSchedule(null, null, null, unaligned.AddHours(1), unaligned.AddDays(1), null));
        var changedSchedule = await ReadAsync();
        var stale = await PostPageAsync(client, confirmed);
        Assert.Contains("data-identity-schedule-stale=\"true\"", stale); Assert.Contains("data-identity-conflicts=\"\"", stale);
        Assert.Contains("The schedule changed after this timezone review", stale); Assert.Equal("Original", (await ReadAsync()).Name); Assert.Equal(0, await AuditCountAsync());
        confirmed = Fields(stale); Assert.NotEqual(oldFingerprint, confirmed["Input.TimezoneConfirmationSchedule"]);
        confirmed["Input.ConfirmTimezoneChange"] = "true";
        using var success = await client.PostAsync(Route, new FormUrlEncodedContent(confirmed));
        Assert.Equal(HttpStatusCode.Redirect, success.StatusCode);
        var saved = await ReadAsync(); Assert.Equal("Europe/Copenhagen", saved.Timezone); Assert.Equal("Pending timezone name", saved.Name);
        Assert.Equal(changedSchedule.EventStartsAt, saved.EventStartsAt); Assert.Equal(changedSchedule.EventEndsAt, saved.EventEndsAt);
        Assert.Equal(changedSchedule.FirstPublicAt, saved.FirstPublicAt); Assert.Equal(changedSchedule.SubmissionCutoffAt, saved.SubmissionCutoffAt);
        Assert.Equal(1, await AuditCountAsync());
    }

    [Fact]
    public async Task AlreadyMatchingAndUnchangedFieldsAreNoOpsAndRetainOtherAdminsChanges()
    {
        using var client = await ClientAsync("first-admin");
        var draft = Fields(await client.GetStringAsync(Route)); draft["Input.Name"] = "  Matching  "; draft["Input.Description"] = "  ";
        await EditAsync(item => item.UpdateIdentity("Matching", item.Slug, "Their description", "Their buy-in", item.Timezone));
        var before = await ReadAsync();
        using var response = await client.PostAsync(Route, new FormUrlEncodedContent(draft));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = await ReadAsync(); Assert.Equal(before.Version, saved.Version); Assert.Equal("Their description", saved.Description); Assert.Equal("Their buy-in", saved.BuyInDescription); Assert.Equal(0, await AuditCountAsync());
        // Retained unsupported timezone is valid when unchanged, including a stale client.
        await EditAsync(item => item.UpdateIdentity(item.Name, item.Slug, item.Description, item.BuyInDescription, "Legacy/Unknown"));
        draft = Fields(await client.GetStringAsync(Route)); draft["Input.Description"] = "New description";
        await EditAsync(item => item.UpdateIdentity("Latest name", item.Slug, item.Description, item.BuyInDescription, item.Timezone));
        using var updated = await client.PostAsync(Route, new FormUrlEncodedContent(draft)); Assert.Equal(HttpStatusCode.Redirect, updated.StatusCode);
        saved = await ReadAsync(); Assert.Equal("Latest name", saved.Name); Assert.Equal("Legacy/Unknown", saved.Timezone); Assert.Equal("New description", saved.Description);
    }

    [Fact]
    public async Task MissingAndPartialStaleBaselinesFailClosedWithoutAdvancingLegacyVersion()
    {
        using var client = await ClientAsync("first-admin"); var draft = Fields(await client.GetStringAsync(Route));
        var original = draft["Input.Version"]; draft["Input.HasBaseline"] = "false"; draft["Input.Name"] = "Legacy proposal";
        await EditAsync(item => item.UpdateIdentity("Winner", item.Slug, item.Description, item.Timezone));
        var failed = await PostPageAsync(client, draft); var retry = Fields(failed); Assert.Equal(original, retry["Input.Version"]);
        failed = await PostPageAsync(client, retry); Assert.Contains("This event changed while you were editing", failed);
        draft = Fields(await client.GetStringAsync(Route)); draft.Remove("Input.OriginalDescription"); draft["Input.Name"] = "Partial proposal";
        failed = await PostPageAsync(client, draft); Assert.Contains("comparison baseline is incomplete", failed);
        Assert.Equal("Winner", (await ReadAsync()).Name); Assert.Equal(0, await AuditCountAsync());
    }

    [Fact]
    public async Task ActualTransportPreservesUnicodeAndUtf16LimitsAndPermanentSlug()
    {
        using var client = await ClientAsync("first-admin"); var draft = Fields(await client.GetStringAsync(Route));
        draft["Input.Name"] = string.Concat(Enumerable.Repeat("😀", 50));
        draft["Input.Description"] = string.Concat(Enumerable.Repeat("😀", 2000));
        draft["Input.BuyInDescription"] = string.Concat(Enumerable.Repeat("😀", 1000));
        using var valid = await client.PostAsync(Route, new FormUrlEncodedContent(draft)); Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode);
        await using (var verify = new ApplicationDbContext(options))
        {
            var audit = await verify.AuditEntries.SingleAsync(entry => entry.EventId == eventId && entry.Action == "event.identity_updated");
            Assert.True(audit.AfterState!.Length <= 4000);
            using var json = System.Text.Json.JsonDocument.Parse(audit.AfterState);
            var excerpt = json.RootElement.GetProperty("Description").GetString()!;
            Assert.EndsWith("…", excerpt); Assert.DoesNotContain("�", excerpt);
        }
        draft = Fields(await client.GetStringAsync(Route)); draft["Input.Name"] += "😀"; draft["Input.Description"] += "x"; draft["Input.BuyInDescription"] += "x"; draft["Input.Slug"] = "forged";
        var failed = await PostPageAsync(client, draft);
        Assert.Contains("50 characters or fewer", failed); Assert.Contains("4000", failed); Assert.Contains("2000", failed); Assert.Contains("public event link is permanent", failed);
        var saved = await ReadAsync(); Assert.Equal(100, saved.Name.Length); Assert.Equal(4000, saved.Description!.Length); Assert.Equal(2000, saved.BuyInDescription!.Length); Assert.Equal("identity-conflict", saved.Slug); Assert.Equal(1, await AuditCountAsync());
    }

    [Fact]
    public async Task ActualLiveAndFinalReviewTransportAllowsTextButNeverTimezoneChanges()
    {
        await EditAsync(item => { item.OpenSignups(Now); item.CloseSignups(Now); item.StartEvent(Now); });
        using var client = await ClientAsync("first-admin");
        foreach (var finalReview in new[] { false, true })
        {
            if (finalReview) await EditAsync(item => item.EndEvent(Now.AddHours(1)));
            var draft = Fields(await client.GetStringAsync(Route)); draft["Input.Name"] = finalReview ? "Final text" : "Live text";
            using var saved = await client.PostAsync(Route, new FormUrlEncodedContent(draft)); Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
            Assert.Equal(draft["Input.Name"], (await ReadAsync()).Name);
            draft = Fields(await client.GetStringAsync(Route)); draft["Input.Timezone"] = "Europe/Copenhagen";
            var failed = await PostPageAsync(client, draft); Assert.Contains("timezone cannot change after the event first goes Live", failed);
            Assert.Equal("UTC", (await ReadAsync()).Timezone);
        }
        Assert.Equal(2, await AuditCountAsync());
    }

    [Fact]
    public async Task NonAdminActualTransportCannotReadOrSaveIdentity()
    {
        using var admin = await ClientAsync("first-admin"); var draft = Fields(await admin.GetStringAsync(Route)); draft["Input.Name"] = "Unauthorized";
        using var ordinary = await ClientAsync("ordinary-account");
        using var read = await ordinary.GetAsync(Route); Assert.Equal(HttpStatusCode.Redirect, read.StatusCode); Assert.Contains("AccessDenied", read.Headers.Location!.ToString());
        // Use this account's actual antiforgery token; this proves authorization rather than token mismatch.
        draft["__RequestVerificationToken"] = Fields(await ordinary.GetStringAsync("/"))["__RequestVerificationToken"];
        using var post = await ordinary.PostAsync(Route, new FormUrlEncodedContent(draft)); Assert.Equal(HttpStatusCode.Redirect, post.StatusCode); Assert.Contains("AccessDenied", post.Headers.Location!.ToString());
        Assert.Equal("Original", (await ReadAsync()).Name); Assert.Equal(0, await AuditCountAsync());
    }

    private async Task<HttpClient> ClientAsync(string name)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var token = Fields(await client.GetStringAsync("/Account/Login"))["__RequestVerificationToken"];
        using var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["Input.Username"] = name, ["Input.Password"] = "synthetic-identity-password", ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode); return client;
    }
    private async Task<string> PostPageAsync(HttpClient client, Dictionary<string, string> fields)
    {
        using var response = await client.PostAsync(Route, new FormUrlEncodedContent(fields)); Assert.Equal(HttpStatusCode.OK, response.StatusCode); return await response.Content.ReadAsStringAsync();
    }
    private async Task EditAsync(Action<BingoEvent> edit)
    {
        await using var db = new ApplicationDbContext(options); var item = await db.Events.SingleAsync(item => item.Id == eventId); edit(item); await db.SaveChangesAsync();
    }
    private async Task<BingoEvent> ReadAsync() { await using var db = new ApplicationDbContext(options); return await db.Events.AsNoTracking().SingleAsync(item => item.Id == eventId); }
    private async Task<int> AuditCountAsync() { await using var db = new ApplicationDbContext(options); return await db.AuditEntries.CountAsync(entry => entry.EventId == eventId && entry.Action == "event.identity_updated"); }
    private async Task WaitForTwoWritersAsync()
    {
        await using var connection = new NpgsqlConnection(database.GetConnectionString()); await connection.OpenAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%UPDATE events%'", connection);
            if ((long)(await command.ExecuteScalarAsync(timeout.Token))! >= 2) return;
            await Task.Delay(20, timeout.Token);
        }
    }
    private static Dictionary<string, string> Fields(string html)
    {
        var result = new Dictionary<string, string>();
        foreach (Match input in Regex.Matches(html, "<input\\b[^>]*>"))
        {
            var name = Attribute(input.Value, "name"); if (name.Length == 0) continue;
            if (!result.ContainsKey(name)) result[name] = Attribute(input.Value, "value");
        }
        foreach (Match textarea in Regex.Matches(html, "<textarea\\b([^>]*)>(.*?)</textarea>", RegexOptions.Singleline))
            result[Attribute(textarea.Groups[1].Value, "name")] = WebUtility.HtmlDecode(textarea.Groups[2].Value).TrimStart('\r', '\n');
        foreach (Match select in Regex.Matches(html, "<select\\b([^>]*)>(.*?)</select>", RegexOptions.Singleline))
        {
            var selected = Regex.Matches(select.Groups[2].Value, "<option\\b([^>]*)>").Cast<Match>().FirstOrDefault(option => option.Groups[1].Value.Contains("selected", StringComparison.Ordinal));
            if (selected is not null) result[Attribute(select.Groups[1].Value, "name")] = Attribute(selected.Groups[1].Value, "value");
        }
        return result;
    }
    private static string Attribute(string tag, string name) => WebUtility.HtmlDecode(Regex.Match(tag, $"\\b{Regex.Escape(name)}=\"([^\"]*)\"").Groups[1].Value);
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
