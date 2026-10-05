using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bingo.IntegrationTests;

public sealed partial class AdminDesignShellIntegrationTests
{
    [Theory]
    [InlineData(EventState.Cancelled)]
    [InlineData(EventState.Finalized)]
    [InlineData(EventState.Archived)]
    public async Task BoundIdentityTerminalReadsAreFullColourAndWritesStillRefused(EventState state)
    {
        var admin = Admin(); var item = Event(admin, state, "Terminal identity", -10);
        await using (var db = new ApplicationDbContext(options)) { db.AddRange(admin, item); await db.SaveChangesAsync(); }
        await using var factory = IdentityFactory(); using var client = await IdentityClientAsync(factory);
        var route = $"/Admin/Events/Identity/{item.Id}";
        var html = await client.GetStringAsync(route);
        Assert.Contains("data-admin-design", html); Assert.Contains("id=\"identity-readonly\"", html);
        Assert.Contains("class=\"ro-value\"", html); Assert.DoesNotContain("data-identity-save>", html);
        Assert.Contains("so its identity can’t be changed", WebUtility.HtmlDecode(html));
        using var read = await client.GetAsync(route + "?handler=Current");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode); Assert.True(read.Headers.CacheControl!.NoStore);
        var json = await read.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(item.Version, json.GetProperty("version").GetInt64());
        var form = IdentityFields(html); form["Input.Name"] = "Must not save";
        using var post = await client.PostAsync(route, new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode); Assert.Equal($"/Admin/Events/Manage/{item.Id}", post.Headers.Location!.ToString());
        await using var verify = new ApplicationDbContext(options); Assert.Equal(item.Version, (await verify.Events.SingleAsync()).Version); Assert.Empty(await verify.AuditEntries.ToListAsync());
    }

    [Fact]
    public async Task BoundIdentityHiddenGetReadbackAndPostAreNotFound()
    {
        var admin = Admin(); var item = Event(admin, EventState.Archived, "Hidden identity", -10);
        item.Hide(admin.Id, Now, item.Name, "Controlled fixture");
        await using (var db = new ApplicationDbContext(options)) { db.AddRange(admin, item); await db.SaveChangesAsync(); }
        await using var factory = IdentityFactory(); using var client = await IdentityClientAsync(factory);
        var route = $"/Admin/Events/Identity/{item.Id}";
        using var get = await client.GetAsync(route); using var read = await client.GetAsync(route + "?handler=Current");
        var token = IdentityFields(await client.GetStringAsync("/Admin/Events/Index"))["__RequestVerificationToken"];
        using var post = await client.PostAsync(route, new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["Input.Name"] = "Never saved" }));
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode); Assert.Equal(HttpStatusCode.NotFound, read.StatusCode); Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        await using var verify = new ApplicationDbContext(options); Assert.Equal(item.Version, (await verify.Events.SingleAsync()).Version); Assert.Empty(await verify.AuditEntries.ToListAsync());
    }

    [Fact]
    public async Task IdentitySaveAndNoChangePrgStayOnIdentityWithQuietSavedAndNoExtraAudit()
    {
        var admin = Admin(); var item = Event(admin, EventState.Draft, "Original", 2);
        await using (var db = new ApplicationDbContext(options)) { db.AddRange(admin, item); await db.SaveChangesAsync(); }
        await using var factory = IdentityFactory(); using var client = await IdentityClientAsync(factory);
        var route = $"/Admin/Events/Identity/{item.Id}";
        var form = IdentityFields(await client.GetStringAsync(route)); form["Input.Name"] = "Saved identity";
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var post = await client.PostAsync(route, new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.Redirect, post.StatusCode); Assert.Equal(route, post.Headers.Location!.ToString());
            var html = await client.GetStringAsync(route); Assert.Matches("data-identity-state role=\"status\">[\\s\\S]*?data-icon=\"check\"[\\s\\S]*?<span data-component-text>Saved</span></span>", html); Assert.Contains("data-toast", html); if (attempt == 0) Assert.Contains("Identity saved.", html); form = IdentityFields(html);
        }
        await using var verify = new ApplicationDbContext(options); Assert.Equal("Saved identity", (await verify.Events.SingleAsync()).Name); Assert.Single(await verify.AuditEntries.Where(entry => entry.Action == "event.identity_updated").ToListAsync());
    }

    [Fact]
    public async Task IdentityLostCommitRendersPostedDraftWithoutReadsEvenWhenRollbackFails()
    {
        var admin = Admin(); var item = Event(admin, EventState.Draft, "Original", 2);
        await using (var db = new ApplicationDbContext(options)) { db.AddRange(admin, item); await db.SaveChangesAsync(); }
        var failure = new IdentityLostCommit();
        await using var factory = IdentityFactory(failure, new IdentityNoReadAfterLoss(failure)); using var client = await IdentityClientAsync(factory);
        var route = $"/Admin/Events/Identity/{item.Id}"; var form = IdentityFields(await client.GetStringAsync(route));
        form["Input.Name"] = "Posted draft"; form["Input.Description"] = "First line\r\nSecond line";
        failure.Armed = true;
        using var post = await client.PostAsync(route, new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var html = WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());
        Assert.Contains("data-identity-save-uncertain=\"true\"", html); Assert.Contains("Posted draft", html); Assert.Contains("First line", html); Assert.Contains("Second line", html);
        Assert.Contains("We couldn’t confirm whether your changes were saved.", html); Assert.DoesNotContain("Event identity updated.", html); Assert.DoesNotContain("Identity saved.", html);
        Assert.True(failure.Lost); Assert.Equal(1, failure.Rollbacks); Assert.Equal(0, failure.ReadsAfterLoss);
        await using var verify = new ApplicationDbContext(options); var saved = await verify.Events.SingleAsync();
        Assert.Equal("Posted draft", saved.Name); Assert.Equal("First line\r\nSecond line", saved.Description); Assert.True(saved.Version > item.Version); Assert.Single(await verify.AuditEntries.ToListAsync());
    }

    [Fact]
    public async Task IdentityTimezoneTemplatesUseAllFiveScheduledMomentsWithDstOffsetsAndPermanentSignupList()
    {
        var admin = Admin(); var item = new BingoEvent(Guid.NewGuid(), "Timezone fixture", "timezone-fixture", "UTC", admin.Id, Now, PlacementRule.LegacyScoreTimeThenEhb);
        var winter = new DateTimeOffset(2027, 1, 5, 12, 0, 0, TimeSpan.Zero);
        var summer = new DateTimeOffset(2027, 7, 5, 12, 0, 0, TimeSpan.Zero);
        item.ConfigureSchedule(winter, winter.AddDays(1), null, summer, summer.AddDays(1), 10);
        await using (var db = new ApplicationDbContext(options)) { db.AddRange(admin, item); await db.SaveChangesAsync(); }
        await using var factory = IdentityFactory(); using var client = await IdentityClientAsync(factory);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Admin/Events/Identity/{item.Id}"));
        var template = Regex.Match(html, "<template[^>]*data-zone=\"Europe/Copenhagen\"[^>]*>(.*?)</template>", RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal(4, Regex.Count(template, "data-identity-timezone-row"));
        Assert.Equal(1, Regex.Count(template, "data-identity-timezone-unset"));
        Assert.Contains("Not scheduled yet: Team draft.", template);
        Assert.Contains("Now · UTC", template); Assert.Contains("After · Europe/Copenhagen", template);
        Assert.Contains("Signups open", template); Assert.Contains("Signups close", template); Assert.Contains("Team draft", template); Assert.Contains("Event starts", template); Assert.Contains("Event ends", template);
        Assert.Contains("Not scheduled yet", template); Assert.Contains("13:00<span class=\"compare-off\">UTC+01:00</span>", template); Assert.Contains("14:00<span class=\"compare-off\">UTC+02:00</span>", template); Assert.Contains("12:00<span class=\"compare-off\">UTC+00:00</span>", template); Assert.DoesNotContain("First public", template);
        Assert.Contains("https://localhost/Events/timezone-fixture/Signups", html); Assert.DoesNotContain("identity-event-information-rail", html); Assert.Contains("Scheduled moments stay the same; they’ll be shown in {0} after you save.", html); Assert.Contains("data-timezone-note role=\"status\" hidden", html);
    }

    [Fact]
    public async Task EditingAfterUseTheirsSavesNewIntentAgainstReviewedCurrentValue()
    {
        var admin = Admin(); var item = Event(admin, EventState.Draft, "Original", 2);
        await using (var db = new ApplicationDbContext(options)) { db.AddRange(admin, item); await db.SaveChangesAsync(); }
        await using var factory = IdentityFactory(); using var client = await IdentityClientAsync(factory);
        var route = $"/Admin/Events/Identity/{item.Id}";
        var mine = IdentityFields(await client.GetStringAsync(route));
        var theirs = new Dictionary<string, string>(mine) { ["Input.Name"] = "Their name" };
        using var theirSave = await client.PostAsync(route, new FormUrlEncodedContent(theirs));
        Assert.Equal(HttpStatusCode.Redirect, theirSave.StatusCode);
        mine["Input.Name"] = "My first draft";
        using var conflict = await client.PostAsync(route, new FormUrlEncodedContent(mine));
        Assert.Equal(HttpStatusCode.OK, conflict.StatusCode);
        var html = await conflict.Content.ReadAsStringAsync(); Assert.Contains("data-identity-conflicts=\"Name\"", html);
        var resolved = IdentityFields(html);
        Assert.Equal("Their name", resolved["Input.ReviewedName"]);
        // Browser proof checks UseCurrent -> KeepMine; this request exercises that merge on PostgreSQL.
        resolved["Input.Name"] = "Edited after theirs"; resolved["Input.NameResolution"] = "KeepMine";
        using var saved = await client.PostAsync(route, new FormUrlEncodedContent(resolved));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode); Assert.Equal(route, saved.Headers.Location!.ToString());
        await using var verify = new ApplicationDbContext(options);
        var current = await verify.Events.SingleAsync();
        Assert.Equal("Edited after theirs", current.Name); Assert.Null(current.Description); Assert.Equal("UTC", current.Timezone);
        Assert.Equal(item.Version + 2, current.Version);
        Assert.Equal(2, await verify.AuditEntries.CountAsync(entry => entry.Action == "event.identity_updated"));
    }

    [Fact]
    public async Task DanishIdentityAndShellUseEventTerminologyWithoutChangingLegacyPages()
    {
        var admin = Admin(); var item = Event(admin, EventState.Draft, "Sproglig kontrol", 2);
        await using (var db = new ApplicationDbContext(options)) { db.AddRange(admin, item); await db.SaveChangesAsync(); }
        await using var factory = IdentityFactory(); using var client = await IdentityClientAsync(factory);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("da");
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Admin/Events/Identity/{item.Id}?culture=da&ui-culture=da"));
        Assert.Contains("<html lang=\"da\"", html); Assert.Contains(">Events</span>", html); Assert.Contains(">Alle events</a>", html);
        Assert.Contains("Navn på event", html); Assert.Contains("Eventlink", html);
        Assert.Contains("Tilmelding åbner", html); Assert.Contains("Tilmelding lukker", html);
        Assert.DoesNotContain("AdminDesign.", html); Assert.DoesNotContain("Bingoer", html); Assert.DoesNotContain("begivenhed", html, StringComparison.OrdinalIgnoreCase);
        var old = WebUtility.HtmlDecode(await client.GetStringAsync("/Admin/Events/Index?culture=da&ui-culture=da"));
        Assert.Contains("Bingoer", old); Assert.DoesNotContain("data-admin-design", old);
    }

    private WebApplicationFactory<Program> IdentityFactory(params IInterceptor[] interceptors) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
        .UseEnvironment("Testing").UseSetting("ConnectionStrings:Database", database.GetConnectionString())
        .ConfigureServices(services => { services.RemoveAll<IHostedService>(); services.AddDataProtection().UseEphemeralDataProtectionProvider(); if (interceptors.Length > 0) services.AddDbContext<ApplicationDbContext>(configuration => configuration.AddInterceptors(interceptors)); }));
    private static async Task<HttpClient> IdentityClientAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var token = IdentityFields(await client.GetStringAsync("/Account/Login"))["__RequestVerificationToken"];
        using var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Input.Username"] = "shell-admin", ["Input.Password"] = "synthetic-shell-password", ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode); return client;
    }
    private static Dictionary<string, string> IdentityFields(string html)
    {
        static string Attribute(string tag, string name) => WebUtility.HtmlDecode(Regex.Match(tag, $"\\b{Regex.Escape(name)}=\"([^\"]*)\"").Groups[1].Value);
        var result = new Dictionary<string, string>();
        foreach (Match input in Regex.Matches(html, "<input\\b[^>]*>")) { var name = Attribute(input.Value, "name"); if (name.Length > 0) result.TryAdd(name, Attribute(input.Value, "value")); }
        foreach (Match textarea in Regex.Matches(html, "<textarea\\b([^>]*)>(.*?)</textarea>", RegexOptions.Singleline)) result[Attribute(textarea.Groups[1].Value, "name")] = WebUtility.HtmlDecode(textarea.Groups[2].Value).TrimStart('\r', '\n');
        foreach (Match select in Regex.Matches(html, "<select\\b([^>]*)>(.*?)</select>", RegexOptions.Singleline))
        { var selected = Regex.Matches(select.Groups[2].Value, "<option\\b([^>]*)>").Cast<Match>().FirstOrDefault(option => option.Groups[1].Value.Contains("selected", StringComparison.Ordinal)); if (selected is not null) result[Attribute(select.Groups[1].Value, "name")] = Attribute(selected.Groups[1].Value, "value"); }
        return result;
    }
    private sealed class IdentityLostCommit : DbTransactionInterceptor
    {
        public bool Armed { get; set; }
        public bool Lost { get; private set; }
        public int Rollbacks { get; private set; }
        public int ReadsAfterLoss { get; set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        { if (!Armed) return Task.CompletedTask; Lost = true; throw new InvalidOperationException("Controlled lost response after real PostgreSQL commit."); }
        public override ValueTask<InterceptionResult> TransactionRollingBackAsync(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        { if (Lost) { Rollbacks++; throw new InvalidOperationException("Controlled rollback failure after lost commit."); } return ValueTask.FromResult(result); }
    }
    private sealed class IdentityNoReadAfterLoss(IdentityLostCommit failure) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { if (failure.Lost) { failure.ReadsAfterLoss++; throw new InvalidOperationException("No database read may be required to render uncertainty."); } return ValueTask.FromResult(result); }
    }
}
