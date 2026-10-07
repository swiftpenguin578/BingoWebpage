using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

/// <summary>T1 Accounts binding: drawer route, JSON outcomes, D5/A1 reset delivery, transfer authorization and C-CMP-2.</summary>
public sealed class AccountsHttpIntegrationTests(PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_accounts_http").WithUsername("bingo").WithPassword("bingo_test_password"));
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task RetiredRoutesRedirectToTheDrawerAndUnknownAccountsStayNotFound()
    {
        var owner = Website("routes-owner", GlobalRole.SuperAdmin);
        var target = Website("routes-target");
        await Seed(owner, target);
        await using var factory = Factory();
        using var client = Client(factory);
        await Login(client, owner);

        using (var manage = await client.GetAsync($"/Admin/Accounts/Manage/{target.Id}"))
        {
            Assert.Equal(HttpStatusCode.Redirect, manage.StatusCode);
            Assert.Equal($"/Admin/Accounts?account={target.Id}", manage.Headers.Location?.OriginalString);
        }
        using (var unknown = await client.GetAsync($"/Admin/Accounts/Manage/{Guid.NewGuid()}")) Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using (var transfer = await client.GetAsync("/Admin/Accounts/Transfer"))
        {
            Assert.Equal(HttpStatusCode.Redirect, transfer.StatusCode);
            Assert.Equal("/Admin/Accounts", transfer.Headers.Location?.OriginalString);
        }
        using (var create = await client.GetAsync("/Admin/Accounts/Create")) Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);

        var drawer = await client.GetStringAsync($"/Admin/Accounts?account={target.Id}");
        Assert.Contains($"data-account-drawer data-account-id=\"{target.Id}\"", drawer, StringComparison.Ordinal);
        Assert.Contains("data-page-family=\"accounts\"", drawer, StringComparison.Ordinal);
        var missing = WebUtility.HtmlDecode(await client.GetStringAsync($"/Admin/Accounts?account={Guid.NewGuid()}"));
        Assert.Contains("data-account-missing=\"true\"", missing, StringComparison.Ordinal);
        Assert.Contains("This account isn’t available", missing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task JsonOutcomesAreDistinguishableAndOnlyCompletedChangesPersist()
    {
        var owner = Website("json-owner", GlobalRole.SuperAdmin);
        var admin = Website("json-admin", GlobalRole.Admin);
        var target = Website("json-target");
        await Seed(owner, admin, target);
        await using var factory = Factory();
        using var client = Client(factory);
        await Login(client, owner);
        var token = Token(await client.GetStringAsync($"/Admin/Accounts?account={target.Id}"));

        var stale = await PostJson(client, target.Id, "Disable", token, new() { ["ExpectedAuthorizationVersion"] = (target.AuthorizationVersion + 5).ToString(CultureInfo.InvariantCulture), ["Reason"] = "fixture" });
        Assert.Equal("stale", stale.GetProperty("outcome").GetString());
        var invalid = await PostJson(client, target.Id, "Disable", token, new() { ["ExpectedAuthorizationVersion"] = target.AuthorizationVersion.ToString(CultureInfo.InvariantCulture), ["Reason"] = "   " });
        Assert.Equal("invalid", invalid.GetProperty("outcome").GetString());
        var refused = await PostJson(client, owner.Id, "Disable", token, new() { ["ExpectedAuthorizationVersion"] = owner.AuthorizationVersion.ToString(CultureInfo.InvariantCulture), ["Reason"] = "fixture" });
        Assert.Equal("refused", refused.GetProperty("outcome").GetString());
        Assert.Equal("You cannot disable or restore your own account.", refused.GetProperty("message").GetString());
        await using (var db = new ApplicationDbContext(options))
        {
            Assert.True(await db.Accounts.Where(x => x.Id == target.Id).Select(x => x.Active).SingleAsync());
            Assert.Empty(await db.AuditEntries.ToListAsync());
        }

        var completed = await PostJson(client, target.Id, "GrantAdmin", token, new() { ["ExpectedAuthorizationVersion"] = target.AuthorizationVersion.ToString(CultureInfo.InvariantCulture) });
        Assert.Equal("completed", completed.GetProperty("outcome").GetString());
        await using (var db = new ApplicationDbContext(options))
        {
            var saved = await db.Accounts.SingleAsync(x => x.Id == target.Id);
            Assert.Equal(GlobalRole.Admin, saved.GlobalRole);
            Assert.Equal(target.AuthorizationVersion + 1, saved.AuthorizationVersion);
        }
        var page = await client.GetStringAsync($"/Admin/Accounts?account={target.Id}");
        Assert.Contains($"data-account-version=\"{target.AuthorizationVersion + 1}\"", page, StringComparison.Ordinal);
        Assert.Contains("data-account-role=\"admin\"", page, StringComparison.Ordinal);

        // Unknown or non-website targets are Not Found, never another account.
        using var notFound = await client.SendAsync(JsonPost($"/Admin/Accounts?account={Guid.NewGuid()}&handler=Restore", token, new() { ["ExpectedAuthorizationVersion"] = "1" }));
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task ResetLinkTravelsOnlyInTheNoStoreResponseForItsOwnAccount()
    {
        var owner = Website("reset-owner", GlobalRole.SuperAdmin);
        var first = Website("reset-first");
        var second = Website("reset-second");
        await Seed(owner, first, second);
        await using var factory = Factory();
        using var client = Client(factory);
        await Login(client, owner);
        var token = Token(await client.GetStringAsync($"/Admin/Accounts?account={first.Id}"));

        using var response = await client.SendAsync(JsonPost($"/Admin/Accounts?account={first.Id}&handler=GenerateResetLink", token, []));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        Assert.Null(response.Headers.Location);
        Assert.DoesNotContain(response.Headers, header => header.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) && header.Value.Any(value => value.Contains("TempData", StringComparison.OrdinalIgnoreCase)));
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("completed", json.GetProperty("outcome").GetString());
        Assert.Equal(first.Id, json.GetProperty("accountId").GetGuid());
        var link = json.GetProperty("link").GetString()!;
        var secret = Regex.Match(link, "/Account/ResetPassword/([A-F0-9]+)$").Groups[1].Value;
        Assert.NotEmpty(secret);
        Assert.Matches("^[0-2][0-9]:[0-5][0-9]$", json.GetProperty("expires").GetString()!);

        // A1: neither account's later reads, nor the directory, contain the secret.
        foreach (var url in new[] { $"/Admin/Accounts?account={first.Id}", $"/Admin/Accounts?account={second.Id}", "/Admin/Accounts" })
            Assert.DoesNotContain(secret, await client.GetStringAsync(url), StringComparison.Ordinal);
        await using var db = new ApplicationDbContext(options);
        var stored = await db.PasswordCredentialTokens.SingleAsync(x => x.AccountId == first.Id);
        Assert.DoesNotContain(secret, stored.TokenHash, StringComparison.Ordinal);
        Assert.DoesNotContain(await db.AuditEntries.ToListAsync(), entry => (entry.Details ?? string.Empty).Contains(secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TransferIsSuperAdminOnlyAndReportsFieldOutcomes()
    {
        var owner = Website("transfer-owner", GlobalRole.SuperAdmin);
        var admin = Website("transfer-admin", GlobalRole.Admin);
        var destination = Website("transfer-destination");
        await Seed(owner, admin, destination);
        await using var factory = Factory();

        using (var adminClient = Client(factory))
        {
            await Login(adminClient, admin);
            var adminPage = await adminClient.GetStringAsync("/Admin/Accounts");
            Assert.DoesNotContain("data-account-transfer-template", adminPage, StringComparison.Ordinal);
            // The page is Admin-scoped; the Transfer handler itself refuses an ordinary Admin.
            using (var forbidden = await adminClient.SendAsync(JsonPost("/Admin/Accounts?handler=Transfer", Token(adminPage), Transfer(destination, "password", destination.PublicUsername!))))
                Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            var plain = Transfer(destination, "password", destination.PublicUsername!);
            plain["__RequestVerificationToken"] = Token(adminPage);
            using var redirected = await adminClient.PostAsync("/Admin/Accounts?handler=Transfer", new FormUrlEncodedContent(plain));
            Assert.Equal(HttpStatusCode.Redirect, redirected.StatusCode);
            Assert.Contains("/Account/AccessDenied", redirected.Headers.Location?.OriginalString, StringComparison.Ordinal);
            await using var unchanged = new ApplicationDbContext(options);
            Assert.Equal(GlobalRole.User, await unchanged.Accounts.Where(x => x.Id == destination.Id).Select(x => x.GlobalRole).SingleAsync());
        }

        using var client = Client(factory);
        await Login(client, owner);
        var page = await client.GetStringAsync("/Admin/Accounts");
        Assert.Contains("data-account-transfer-template", page, StringComparison.Ordinal);
        Assert.Contains($"value=\"{destination.Id}\" data-version=\"{destination.AuthorizationVersion}\"", page, StringComparison.Ordinal);
        var token = Token(page);
        var password = await PostJsonRaw(client, "/Admin/Accounts?handler=Transfer", token, Transfer(destination, "wrong-password", destination.PublicUsername!));
        Assert.Equal("invalid", password.GetProperty("outcome").GetString());
        Assert.Equal("password", password.GetProperty("field").GetString());
        var confirmation = await PostJsonRaw(client, "/Admin/Accounts?handler=Transfer", token, Transfer(destination, "password", "someone-else"));
        Assert.Equal("confirmation", confirmation.GetProperty("field").GetString());
        var stale = await PostJsonRaw(client, "/Admin/Accounts?handler=Transfer", token, Transfer(destination, "password", destination.PublicUsername!, destination.AuthorizationVersion + 3));
        Assert.Equal("recipient", stale.GetProperty("outcome").GetString());
        Assert.Contains(stale.GetProperty("destinations").EnumerateArray(), item => item.GetProperty("id").GetGuid() == destination.Id);
        await using (var db = new ApplicationDbContext(options))
            Assert.Equal(GlobalRole.SuperAdmin, await db.Accounts.Where(x => x.Id == owner.Id).Select(x => x.GlobalRole).SingleAsync());

        var completed = await PostJsonRaw(client, "/Admin/Accounts?handler=Transfer", token, Transfer(destination, "password", destination.PublicUsername!.ToUpperInvariant()));
        Assert.Equal("completed", completed.GetProperty("outcome").GetString());
        Assert.Equal(destination.PublicUsername, completed.GetProperty("destination").GetString());
        Assert.Equal("User", completed.GetProperty("before").GetString());
        // Both sessions end: the former owner's next read is a sign-in redirect (C-CMP-2 path).
        using var after = await client.GetAsync("/Admin/Accounts");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.Contains("accessChanged=true", after.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LostAccessMidSessionSignsOutBeforeAnyChange()
    {
        var owner = Website("lost-owner", GlobalRole.SuperAdmin);
        var admin = Website("lost-admin", GlobalRole.Admin);
        var target = Website("lost-target");
        await Seed(owner, admin, target);
        await using var factory = Factory();
        using var ownerClient = Client(factory);
        using var adminClient = Client(factory);
        await Login(ownerClient, owner);
        await Login(adminClient, admin);
        var adminToken = Token(await adminClient.GetStringAsync($"/Admin/Accounts?account={target.Id}"));
        var ownerToken = Token(await ownerClient.GetStringAsync($"/Admin/Accounts?account={admin.Id}"));
        var revoked = await PostJson(ownerClient, admin.Id, "RevokeAdmin", ownerToken, new() { ["ExpectedAuthorizationVersion"] = admin.AuthorizationVersion.ToString(CultureInfo.InvariantCulture) });
        Assert.Equal("completed", revoked.GetProperty("outcome").GetString());

        using var response = await adminClient.SendAsync(JsonPost($"/Admin/Accounts?account={target.Id}&handler=Disable", adminToken, new() { ["ExpectedAuthorizationVersion"] = target.AuthorizationVersion.ToString(CultureInfo.InvariantCulture), ["Reason"] = "should not apply" }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Contains("accessChanged=true", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
        await using var db = new ApplicationDbContext(options);
        Assert.True(await db.Accounts.Where(x => x.Id == target.Id).Select(x => x.Active).SingleAsync());
    }

    // Early-look bug (T1): "page" is also a Razor Pages route value; only an HTTP request exercises
    // model binding (page-model tests set the property directly), so this is checked over HTTP.
    [Fact]
    public async Task DirectoryPagingBindsTheQueryString()
    {
        var owner = Website("paging-owner", GlobalRole.SuperAdmin);
        var users = Enumerable.Range(0, 30).Select(index => Website($"paging-user-{index:D2}")).ToArray();
        await Seed([owner, .. users]);
        await using var factory = Factory();
        using var client = Client(factory);
        await Login(client, owner);
        static IReadOnlyList<string> Names(string html) => Regex.Matches(html, "data-account-open=\"[^\"]+\">([^<]+)<").Select(match => match.Groups[1].Value).ToList();
        var first = await client.GetStringAsync("/Admin/Accounts?q=paging-user");
        var second = await client.GetStringAsync("/Admin/Accounts?q=paging-user&page=2");
        Assert.Equal(25, Names(first).Count);
        Assert.Equal("paging-user-00", Names(first)[0]);
        Assert.Equal(["paging-user-25", "paging-user-26", "paging-user-27", "paging-user-28", "paging-user-29"], Names(second));
        Assert.Contains("Page 2", second, StringComparison.Ordinal);
        Assert.Contains("data-directory-canonical=\"/Admin/Accounts?q=paging-user&amp;page=2\"", second, StringComparison.Ordinal);
        foreach (var junk in new[] { "abc", "2.5", "0" })
            Assert.Equal(Names(first), Names(await client.GetStringAsync($"/Admin/Accounts?q=paging-user&page={junk}")));
        var english = await client.GetStringAsync("/Admin/Accounts");
        Assert.Contains("data-directory-canonical=\"/Admin/Accounts\"", english, StringComparison.Ordinal);
        Assert.Matches("data-accounts-summary><b class=\"tnum\">\\d+</b> accounts</span><span><b class=\"tnum\">0</b> disabled</span>", english);
        // Batch-gate finding: lowercase "accounts"/"disabled" keys collided with "Accounts"/"Disabled"
        // (resource names ignore case), leaving English words in the Danish summary.
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("da");
        var danish = await client.GetStringAsync("/Admin/Accounts");
        Assert.Matches("data-accounts-summary><b class=\"tnum\">\\d+</b> konti</span><span><b class=\"tnum\">0</b> deaktiveret</span>", danish);
    }

    private static Dictionary<string, string> Transfer(Account destination, string password, string typed, long? version = null) => new()
    {
        ["Input.DestinationId"] = destination.Id.ToString(),
        ["Input.ExpectedAuthorizationVersion"] = (version ?? destination.AuthorizationVersion).ToString(CultureInfo.InvariantCulture),
        ["Input.DestinationUsernameConfirmation"] = typed,
        ["Input.CurrentPassword"] = password
    };
    private static HttpRequestMessage JsonPost(string url, string token, Dictionary<string, string> form)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Add("RequestVerificationToken", token);
        return request;
    }
    private static Task<JsonElement> PostJson(HttpClient client, Guid id, string handler, string token, Dictionary<string, string> form) =>
        PostJsonRaw(client, $"/Admin/Accounts?account={id}&handler={handler}", token, form);
    private static async Task<JsonElement> PostJsonRaw(HttpClient client, string url, string token, Dictionary<string, string> form)
    {
        using var response = await client.SendAsync(JsonPost(url, token, form));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    private static Account Website(string name, GlobalRole role = GlobalRole.User)
    {
        var account = Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), DateTimeOffset.UtcNow);
        if (role != GlobalRole.User) account.SetGlobalRole(role);
        account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "password"), false, DateTimeOffset.UtcNow, incrementVersion: false);
        return account;
    }
    private async Task Seed(params object[] entities)
    {
        await using var db = new ApplicationDbContext(options);
        db.AddRange(entities);
        await db.SaveChangesAsync();
    }
    private static async Task Login(HttpClient client, Account account)
    {
        var page = await client.GetStringAsync("/Account/Login");
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = account.LoginName,
            ["Input.Password"] = "password",
            ["__RequestVerificationToken"] = Token(page)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private static string Token(string page) => WebUtility.HtmlDecode(Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value);
}
