using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;
using CatalogueModel = Bingo.Web.Pages.Admin.Catalogue.IndexModel;

namespace Bingo.IntegrationTests;

public sealed class AdminStaleChangeIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_admin_stale_change").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;
    private const string StaleMessage = "This record was changed by another administrator.";

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task SharedItemEditRejectsStaleHttpFormAndAuditsCurrentDropAndItemTogether()
    {
        var (admin, first, second, item) = await SeedCatalogue();
        await using var factory = Factory();
        using var firstClient = Client(factory);
        using var secondClient = Client(factory);
        await Login(firstClient, admin);
        await Login(secondClient, admin);
        var stale = await DropForm(firstClient, first);
        var current = await DropForm(secondClient, second);
        Assert.Equal("1", stale["expectedItemVersion"]);
        current["sharedItemConfirmationActivityIds"] = first.BossActivityId.ToString();
        current["itemName"] = "Shared renamed item";
        current["imageUrl"] = "https://oldschool.runescape.wiki/images/Abyssal_whip.png";
        using (var response = await PostDrop(secondClient, second, current)) Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var intervening = await CatalogueState();
        stale["displayRate"] = "1/50";
        using (var response = await PostDrop(firstClient, first, stale))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains(StaleMessage, await firstClient.GetStringAsync(response.Headers.Location), StringComparison.Ordinal);
        }
        Assert.Equal(intervening, await CatalogueState());

        var fresh = await DropForm(firstClient, first);
        Assert.Equal("Shared renamed item", fresh["itemName"]);
        Assert.Equal("2", fresh["expectedItemVersion"]);
        // The item is unchanged in this save; the drop edit must still carry and audit it.
        fresh["displayRate"] = "1/50";
        using (var response = await PostDrop(firstClient, first, fresh)) Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(item.Id, (await verify.SourceDrops.SingleAsync(x => x.Id == first.Id)).ItemId);
        Assert.Equal(item.Id, (await verify.SourceDrops.SingleAsync(x => x.Id == second.Id)).ItemId);
        Assert.Equal("1/50", (await verify.SourceDrops.SingleAsync(x => x.Id == first.Id)).DisplayRate);
        Assert.Equal("1/100", (await verify.SourceDrops.SingleAsync(x => x.Id == second.Id)).DisplayRate);
        var audits = await verify.AuditEntries.Where(x => x.Action == "catalogue.drop_updated").ToListAsync();
        Assert.Equal(2, audits.Count);
        var renamed = audits.Single(x => x.TargetId == second.Id.ToString());
        using var beforeRename = JsonDocument.Parse(renamed.BeforeState!);
        using var afterRename = JsonDocument.Parse(renamed.AfterState!);
        Assert.Equal("Shared item", beforeRename.RootElement.GetProperty("Items")[0].GetProperty("Name").GetString());
        Assert.Equal("Shared renamed item", afterRename.RootElement.GetProperty("Items")[0].GetProperty("Name").GetString());
        Assert.Equal(JsonValueKind.Null, beforeRename.RootElement.GetProperty("Items")[0].GetProperty("ImageUrl").ValueKind);
        Assert.Equal(current["imageUrl"], afterRename.RootElement.GetProperty("Items")[0].GetProperty("ImageUrl").GetString());
        var saved = audits.Single(x => x.TargetId == first.Id.ToString());
        using var beforeSave = JsonDocument.Parse(saved.BeforeState!);
        using var afterSave = JsonDocument.Parse(saved.AfterState!);
        Assert.Equal("1/100", beforeSave.RootElement.GetProperty("Drop").GetProperty("DisplayRate").GetString());
        Assert.Equal("1/50", afterSave.RootElement.GetProperty("Drop").GetProperty("DisplayRate").GetString());
        Assert.Equal("Shared renamed item", afterSave.RootElement.GetProperty("Items")[0].GetProperty("Name").GetString());
        Assert.Equal(current["imageUrl"], afterSave.RootElement.GetProperty("Items")[0].GetProperty("ImageUrl").GetString());
        Assert.Equal(2, beforeSave.RootElement.GetProperty("Items")[0].GetProperty("Version").GetInt64());
        Assert.Equal((await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id)).Version, afterSave.RootElement.GetProperty("Items")[0].GetProperty("Version").GetInt64());
        Assert.Equal((await verify.SourceDrops.SingleAsync(x => x.Id == first.Id)).Version, afterSave.RootElement.GetProperty("Drop").GetProperty("Version").GetInt64());
    }

    [Fact]
    public async Task SharedItemAndDropRollBackWhenAuditPersistenceFails()
    {
        var (admin, first, second, item) = await SeedCatalogue();
        var before = await CatalogueState();
        var failing = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new ThrowOnAuditInsert()).Options;
        await using var db = new ApplicationDbContext(failing);
        var page = CataloguePage(db, admin);
        await Assert.ThrowsAsync<InvalidOperationException>(() => UpdateDrop(page, first, item.Version, "Changed item", [second.BossActivityId]));
        Assert.Equal(before, await CatalogueState());
    }

    [Fact]
    public async Task ConcurrentSharedItemEditRejectsDropOnlySaveAndRollsBackItsAudit()
    {
        var (admin, first, _, item) = await SeedCatalogue();
        var barrier = new BeforeFirstSave(async () =>
        {
            await using var other = new ApplicationDbContext(options);
            var shared = await other.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            shared.Update("Intervening item", "INTERVENING ITEM", null, null);
            await other.SaveChangesAsync();
        });
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(barrier).Options);
        var page = CataloguePage(db, admin);
        Assert.IsType<RedirectToPageResult>(await UpdateDrop(page, first, item.Version, item.Name));
        Assert.Contains(StaleMessage, page.TempData["StatusMessage"]?.ToString(), StringComparison.Ordinal);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal("Intervening item", (await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id)).Name);
        Assert.Equal("1/100", (await verify.SourceDrops.SingleAsync(x => x.Id == first.Id)).DisplayRate);
        Assert.Empty(await verify.AuditEntries.ToListAsync());
    }

    [Theory]
    [InlineData("Disable", false)]
    [InlineData("Restore", false)]
    [InlineData("GrantAdmin", false)]
    [InlineData("RevokeAdmin", false)]
    [InlineData("Disable", true)]
    [InlineData("Restore", true)]
    [InlineData("GrantAdmin", true)]
    [InlineData("RevokeAdmin", true)]
    public async Task AccountConfirmationRejectsCompletedInterveningChangesThenAcceptsFreshAction(string action, bool changeRole)
    {
        var owner = Website("owner", GlobalRole.SuperAdmin);
        var target = Website("target", action == "RevokeAdmin" ? GlobalRole.Admin : GlobalRole.User);
        await Seed(owner, target);
        await using var factory = Factory();
        using var firstClient = Client(factory);
        using var secondClient = Client(factory);
        using var targetClient = Client(factory);
        await Login(firstClient, owner);
        await Login(secondClient, owner);
        await Login(targetClient, target);
        if (action == "Restore") await ConfirmAccount(secondClient, target.Id, "Disable");
        var openedPage = await firstClient.GetStringAsync($"/Admin/Accounts/Manage/{target.Id}?overlay=1");
        Capture(action, "opened", openedPage);
        var opened = ReadForm(openedPage, action);
        opened["Reason"] = "test reason";
        if (changeRole)
        {
            if (action == "Restore")
            {
                // GrantAdmin is intentionally available only for an active User. Restore the
                // disabled target, make the role changes while it is active, then disable it
                // again so the originally opened Restore form remains stale without changing
                // the final state used by the fresh-action assertion below.
                await ConfirmAccount(secondClient, target.Id, "Restore");
                await ConfirmAccount(secondClient, target.Id, "GrantAdmin");
                await ConfirmAccount(secondClient, target.Id, "RevokeAdmin");
                await ConfirmAccount(secondClient, target.Id, "Disable");
            }
            else
            {
                var firstAction = target.GlobalRole == GlobalRole.Admin ? "RevokeAdmin" : "GrantAdmin";
                await ConfirmAccount(secondClient, target.Id, firstAction);
                await ConfirmAccount(secondClient, target.Id, firstAction == "GrantAdmin" ? "RevokeAdmin" : "GrantAdmin");
            }
        }
        else
        {
            var firstAction = action == "Restore" ? "Restore" : "Disable";
            await ConfirmAccount(secondClient, target.Id, firstAction);
            await ConfirmAccount(secondClient, target.Id, firstAction == "Disable" ? "Restore" : "Disable");
        }
        var beforeStale = await AccountState(target.Id);
        using (var response = await PostAccount(firstClient, target.Id, action, opened))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var page = await response.Content.ReadAsStringAsync();
            Assert.Contains(StaleMessage, page, StringComparison.Ordinal);
            Capture(action, "stale", page);
            Assert.Contains("data-account-change-stale=\"true\"", page, StringComparison.Ordinal);
            Assert.NotEqual(opened["ExpectedAuthorizationVersion"], ReadForm(page, action)["ExpectedAuthorizationVersion"]);
        }
        Assert.Equal(beforeStale, await AccountState(target.Id));
        await using var before = new ApplicationDbContext(options);
        var version = await before.Accounts.Where(x => x.Id == target.Id).Select(x => x.AuthorizationVersion).SingleAsync();
        var auditCount = await before.AuditEntries.CountAsync(x => x.TargetId == target.Id.ToString());
        // Get a current target session when active, so this final action itself proves invalidation.
        if (action != "Restore") await Login(targetClient, target);
        await ConfirmAccount(firstClient, target.Id, action);
        await using var verify = new ApplicationDbContext(options);
        var saved = await verify.Accounts.SingleAsync(x => x.Id == target.Id);
        Assert.Equal(version + 1, saved.AuthorizationVersion);
        Assert.Equal(auditCount + 1, await verify.AuditEntries.CountAsync(x => x.TargetId == target.Id.ToString()));
        Assert.Equal(action != "Disable", saved.Active);
        Assert.Equal(action == "GrantAdmin" ? GlobalRole.Admin : GlobalRole.User, saved.GlobalRole);
        using var invalidated = await targetClient.GetAsync("/Account/Settings");
        Assert.Equal(HttpStatusCode.Redirect, invalidated.StatusCode);
        Assert.Contains("/Account/Login", invalidated.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Contains("accessChanged=true", invalidated.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Disable")]
    [InlineData("Restore")]
    [InlineData("GrantAdmin")]
    [InlineData("RevokeAdmin")]
    public async Task AccountActionsRequireFreshnessAndPreserveAuthorization(string action)
    {
        var owner = Website("owner", GlobalRole.SuperAdmin);
        var admin = Website("admin", GlobalRole.Admin);
        var user = Website("user");
        var target = Website("target", action == "RevokeAdmin" ? GlobalRole.Admin : GlobalRole.User);
        if (action == "Restore") target.Disable(DateTimeOffset.UtcNow, owner.Id, "fixture");
        await Seed(owner, admin, user, target);
        await using var factory = Factory();
        using var client = Client(factory);
        await Login(client, owner);
        var form = await AccountForm(client, target.Id, action);
        var before = await AccountState(target.Id);
        form.Remove("ExpectedAuthorizationVersion");
        using (var response = await PostAccount(client, target.Id, action, form))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(StaleMessage, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        Assert.Equal(before, await AccountState(target.Id));
        form = await AccountForm(client, target.Id, action);
        using (var noAntiforgery = await PostAccount(client, target.Id, action, form.Where(x => x.Key != "__RequestVerificationToken").ToDictionary()))
            Assert.Equal(HttpStatusCode.BadRequest, noAntiforgery.StatusCode);
        Assert.Equal(before, await AccountState(target.Id));

        // An ordinary User cannot enter or post into account administration.
        using var userClient = Client(factory);
        await Login(userClient, user);
        using (var forbidden = await PostAccount(userClient, target.Id, action, form))
        {
            Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode);
            Assert.Contains("/Account/AccessDenied", forbidden.Headers.Location?.OriginalString, StringComparison.Ordinal);
        }
        Assert.Equal(before, await AccountState(target.Id));

        // Admin cannot grant/revoke; Admin cannot disable/restore another Admin or itself.
        using var adminClient = Client(factory);
        await Login(adminClient, admin);
        var protectedId = action is "Disable" or "Restore" ? admin.Id : target.Id;
        var manage = await adminClient.GetStringAsync($"/Admin/Accounts/Manage/{protectedId}?overlay=1");
        await using var db = new ApplicationDbContext(options);
        var protectedAccount = await db.Accounts.AsNoTracking().SingleAsync(x => x.Id == protectedId);
        var forbiddenForm = new Dictionary<string, string>
        {
            ["ExpectedAuthorizationVersion"] = protectedAccount.AuthorizationVersion.ToString(CultureInfo.InvariantCulture),
            ["__RequestVerificationToken"] = Token(manage),
            ["overlay"] = "1",
            ["Reason"] = "test reason"
        };
        var protectedBefore = await AccountState(protectedId);
        using (var forbidden = await PostAccount(adminClient, protectedId, action, forbiddenForm))
        {
            Assert.Equal(HttpStatusCode.OK, forbidden.StatusCode);
            var page = await forbidden.Content.ReadAsStringAsync();
            var expected = action is "Disable" or "Restore"
                ? "You cannot disable or restore your own account."
                : "Only the active Super Admin can perform this action.";
            Assert.Contains(expected, page, StringComparison.Ordinal);
        }
        Assert.Equal(protectedBefore, await AccountState(protectedId));
        Assert.Equal(before, await AccountState(target.Id));
        // The owner is protected even from an owner-issued forged target action.
        var ownerBefore = await AccountState(owner.Id);
        form["ExpectedAuthorizationVersion"] = owner.AuthorizationVersion.ToString(CultureInfo.InvariantCulture);
        using (var forbidden = await PostAccount(client, owner.Id, action, form))
        {
            Assert.Equal(HttpStatusCode.OK, forbidden.StatusCode);
            var page = await forbidden.Content.ReadAsStringAsync();
            var expected = action switch
            {
                "GrantAdmin" => "Only a User can be granted Admin access.",
                "RevokeAdmin" => "Only an Admin can be revoked.",
                _ => "You cannot disable or restore your own account."
            };
            Assert.Contains(expected, page, StringComparison.Ordinal);
        }
        Assert.Equal(ownerBefore, await AccountState(owner.Id));
        if (action is "Disable" or "Restore") await ConfirmAccount(adminClient, target.Id, action);
    }

    private static void Capture(string action, string phase, string html)
    {
        if (Environment.GetEnvironmentVariable("BINGO_ADMIN_STALE_EVIDENCE_DIRECTORY") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, $"account-{action}-{phase}.html"), html);
        }
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
    private async Task<(Account, SourceDrop, SourceDrop, CatalogueItem)> SeedCatalogue()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website("admin", GlobalRole.Admin);
        var firstBoss = new BossActivity(Guid.NewGuid(), "First boss", "first-boss", "Boss", 10, now);
        var secondBoss = new BossActivity(Guid.NewGuid(), "Second boss", "second-boss", "Boss", 10, now);
        var item = new CatalogueItem(Guid.NewGuid(), "Shared item", "SHARED ITEM");
        var first = new SourceDrop(Guid.NewGuid(), firstBoss.Id, item.Id, "1/100", .01m, 10m, now);
        var second = new SourceDrop(Guid.NewGuid(), secondBoss.Id, item.Id, "1/100", .01m, 10m, now);
        await Seed(admin, firstBoss, secondBoss, item, first, second);
        return (admin, first, second, item);
    }
    private async Task<string> CatalogueState()
    {
        await using var db = new ApplicationDbContext(options);
        return JsonSerializer.Serialize(new
        {
            Items = await db.CatalogueItems.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            Drops = await db.SourceDrops.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            Audits = await db.AuditEntries.AsNoTracking().OrderBy(x => x.Id).ToListAsync()
        });
    }
    private async Task<string> AccountState(Guid id)
    {
        await using var db = new ApplicationDbContext(options);
        return JsonSerializer.Serialize(new
        {
            Account = await db.Accounts.AsNoTracking().SingleAsync(x => x.Id == id),
            Audits = await db.AuditEntries.AsNoTracking().Where(x => x.TargetId == id.ToString()).OrderBy(x => x.Id).ToListAsync(),
            Notifications = await db.PersonalNotifications.AsNoTracking().Where(x => x.RecipientAccountId == id).OrderBy(x => x.Id).ToListAsync()
        });
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
        Assert.DoesNotContain("/Account/Login", response.Headers.Location?.OriginalString ?? "", StringComparison.Ordinal);
    }
    private static string Token(string page) => WebUtility.HtmlDecode(Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value);
    private static Dictionary<string, string> Inputs(string form)
    {
        Assert.NotEmpty(form);
        var values = new Dictionary<string, string>();
        foreach (Match input in Regex.Matches(form, "<input\\b[^>]*>", RegexOptions.Singleline))
        {
            var name = Regex.Match(input.Value, "name=\"([^\"]+)\"").Groups[1].Value;
            if (name.Length > 0) values[name] = WebUtility.HtmlDecode(Regex.Match(input.Value, "value=\"([^\"]*)\"").Groups[1].Value);
        }
        return values;
    }
    private static Dictionary<string, string> ReadForm(string page, string handler) => Inputs(Regex.Match(page, $"<form[^>]*action=\"[^\"]*handler={handler}(?:&[^\"]*)?\"[^>]*>.*?</form>", RegexOptions.Singleline).Value);
    private static async Task<Dictionary<string, string>> DropForm(HttpClient client, SourceDrop drop)
    {
        var page = await client.GetStringAsync($"/Admin/Catalogue?bossId={drop.BossActivityId}");
        return Inputs(Regex.Match(page, $"<form[^>]*id=\"catalogue-drop-form-{drop.Id}\".*?</form>", RegexOptions.Singleline).Value);
    }
    private static Task<HttpResponseMessage> PostDrop(HttpClient client, SourceDrop drop, Dictionary<string, string> form) => client.PostAsync($"/Admin/Catalogue?bossId={drop.BossActivityId}&handler=UpdateDrop", new FormUrlEncodedContent(form));
    private static async Task<Dictionary<string, string>> AccountForm(HttpClient client, Guid id, string handler)
    {
        var page = await client.GetStringAsync($"/Admin/Accounts/Manage/{id}?overlay=1");
        var form = ReadForm(page, handler);
        Assert.NotEmpty(form["ExpectedAuthorizationVersion"]);
        form["Reason"] = "test reason";
        return form;
    }
    private static Task<HttpResponseMessage> PostAccount(HttpClient client, Guid id, string handler, Dictionary<string, string> form) => client.PostAsync($"/Admin/Accounts/Manage/{id}?handler={handler}&overlay=1", new FormUrlEncodedContent(form));
    private static async Task ConfirmAccount(HttpClient client, Guid id, string handler)
    {
        var form = await AccountForm(client, id, handler);
        using var response = await PostAccount(client, id, handler, form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private static CatalogueModel CataloguePage(ApplicationDbContext db, Account admin)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()), new Claim(ClaimTypes.Name, admin.LoginName), new Claim(ClaimTypes.Role, "Admin") }, "test")) };
        return new CatalogueModel(db, TimeProvider.System) { PageContext = new PageContext { HttpContext = context }, TempData = new TempDataDictionary(context, new EmptyTempDataProvider()) };
    }
    private static Task<IActionResult> UpdateDrop(CatalogueModel page, SourceDrop drop, long itemVersion, string name, Guid[]? confirmedActivities = null) => page.OnPostUpdateDropAsync(drop.Id, drop.Version, itemVersion, name, "1/50", "1/100", .01m, .01m, DropProbabilityScope.Participant, false, null, 1, 1, "default", null, null, false, CancellationToken.None, confirmedActivities);
    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
    private sealed class ThrowOnAuditInsert : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            eventData.Context!.ChangeTracker.Entries<AuditEntry>().Any(x => x.State == EntityState.Added)
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Simulated audit persistence failure."))
                : ValueTask.FromResult(result);
    }
    private sealed class BeforeFirstSave(Func<Task> action) : SaveChangesInterceptor
    {
        private bool invoked;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!invoked) { invoked = true; await action(); }
            return result;
        }
    }
}
