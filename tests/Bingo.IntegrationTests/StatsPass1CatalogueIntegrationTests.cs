using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bingo.Application.Catalogue;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Bingo.Web.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Fact]
    public async Task StatsPass1SharedItemSaveAuditsStoredVersionAndPreservesPersonalMechanics()
    {
        var (actor, boss, item, drop) = await PriceFixtureAsync();
        var secondBoss = new BossActivity(Guid.NewGuid(), "Second source", $"second-{Guid.NewGuid():N}", "Boss", 10, DateTimeOffset.UtcNow);
        var secondDrop = new SourceDrop(Guid.NewGuid(), secondBoss.Id, item.Id, "1/20", .05m, 2, DateTimeOffset.UtcNow);
        await using (var setup = new ApplicationDbContext(options)) { setup.AddRange(secondBoss, secondDrop); await setup.SaveChangesAsync(); }
        await using (var db = new ApplicationDbContext(options))
        {
            var page = CataloguePage(db, actor.Id, true, new PriceApi());
            Assert.IsType<RedirectToPageResult>(await page.OnPostItemApiAsync(drop.Id, item.Id, item.Version, "0004151", "Api", null, "validate", default));
        }
        await using var verify = new ApplicationDbContext(options);
        var saved = await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id);
        Assert.Equal("4151", saved.ExternalIdentifier); Assert.Equal(101L, saved.CatalogueValueGp);
        Assert.Equal(CataloguePriceSource.Api, saved.PriceSource); Assert.Equal(ApiMappingStatus.Verified, saved.MappingStatus);
        Assert.Equal("Synthetic item", saved.MatchedApiName); Assert.Equal(PriceApi.Hour, saved.PriceObservedAt);
        Assert.Equal(2, await verify.SourceDrops.CountAsync(x => x.ItemId == item.Id));
        var retained = await verify.SourceDrops.SingleAsync(x => x.Id == drop.Id);
        Assert.Equal(.001m, retained.NumericProbability); Assert.Equal(7, retained.RollsPerCompletion);
        Assert.Equal(DropProbabilityScope.Participant, retained.ProbabilityScope); Assert.Equal(10m, retained.DefaultEhbEstimate);
        Assert.Equal(10m, (await verify.BossActivities.SingleAsync(x => x.Id == boss.Id)).EfficientCompletionsPerHour);
        var audit = await verify.AuditEntries.SingleAsync(x => x.Action == "catalogue.item_api_updated");
        Assert.Equal(actor.Id, audit.ActorAccountId);
        using var after = JsonDocument.Parse(audit.AfterState!);
        Assert.Equal(saved.Version, after.RootElement.GetProperty("Version").GetInt64());
        Assert.Equal(101L, after.RootElement.GetProperty("CatalogueValueGp").GetInt64());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatsPass1ConcurrentItemEditOrDropRebindRejectsStaleApiResponse(bool rebind)
    {
        var (actor, _, item, drop) = await PriceFixtureAsync();
        var replacement = new CatalogueItem(Guid.NewGuid(), "Replacement", "REPLACEMENT");
        await using (var setup = new ApplicationDbContext(options)) { setup.Add(replacement); await setup.SaveChangesAsync(); }
        var api = new PriceApi
        {
            BeforeItems = async () =>
        {
            await using var concurrent = new ApplicationDbContext(options);
            if (rebind) (await concurrent.SourceDrops.SingleAsync(x => x.Id == drop.Id)).ChangeItem(replacement.Id);
            else (await concurrent.CatalogueItems.SingleAsync(x => x.Id == item.Id)).SetPrice(777, CataloguePriceSource.Manual, PriceApi.Hour);
            await concurrent.SaveChangesAsync();
        }
        };
        await using (var db = new ApplicationDbContext(options))
        {
            var page = CataloguePage(db, actor.Id, true, api);
            await page.OnPostItemApiAsync(drop.Id, item.Id, item.Version, "4151", "Api", null, "validate", default);
            Assert.Contains("changed by another administrator", page.TempData["StatusMessage"]?.ToString(), StringComparison.Ordinal);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.False(await verify.AuditEntries.AnyAsync(x => x.Action == "catalogue.item_api_updated"));
        var saved = await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id);
        Assert.Equal(rebind ? null : 777L, saved.CatalogueValueGp);
        Assert.Equal(rebind ? replacement.Id : item.Id, (await verify.SourceDrops.SingleAsync(x => x.Id == drop.Id)).ItemId);
    }

    [Fact]
    public async Task StatsPass1AuditFailureRollsBackEditorAndOperatorWrites()
    {
        var (actor, _, item, drop) = await PriceFixtureAsync();
        var failing = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new ThrowOnAuditInsert()).Options;
        await using (var db = new ApplicationDbContext(failing))
            await Assert.ThrowsAsync<InvalidOperationException>(() => CataloguePage(db, actor.Id, true).OnPostItemApiAsync(drop.Id, item.Id, item.Version, null, "Manual", 0, "save", default));
        await using (var db = new ApplicationDbContext(failing))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new CataloguePriceSyncService(db, new PriceApi(), TimeProvider.System).RunAsync(true, actor.Id, default));
        await using var verify = new ApplicationDbContext(options);
        Assert.Null((await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id)).CatalogueValueGp);
        Assert.False(await verify.AuditEntries.AnyAsync(x => x.Action.StartsWith("catalogue.item_api_")));
    }

    [Fact]
    public async Task StatsPass1OutageSavesManualZeroAndMappingChangeClearsOnlyApiPrice()
    {
        var (actor, boss, item, drop) = await PriceFixtureAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            var page = CataloguePage(db, actor.Id, true, new PriceApi { Unavailable = true });
            await page.OnPostItemApiAsync(drop.Id, item.Id, item.Version, "4151", "Manual", 0, "validate", default);
            await page.OnPostBossApiAsync(boss.Id, boss.Version, "zulrah", "validate", default);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var saved = await db.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            Assert.Equal(0L, saved.CatalogueValueGp); Assert.Equal(CataloguePriceSource.Manual, saved.PriceSource);
            Assert.Equal(ApiMappingStatus.TemporarilyUnavailable, saved.MappingStatus);
            Assert.Equal(ApiMappingStatus.TemporarilyUnavailable, (await db.BossActivities.SingleAsync(x => x.Id == boss.Id)).MappingStatus);
            await CataloguePage(db, actor.Id, true).OnPostItemApiAsync(drop.Id, item.Id, saved.Version, "456", "Manual", 0, "save", default);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var saved = await db.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            Assert.Equal(0L, saved.CatalogueValueGp); Assert.Equal(ApiMappingStatus.NotConfigured, saved.MappingStatus);
            saved.SetPrice(12, CataloguePriceSource.Api, PriceApi.Hour); await db.SaveChangesAsync();
            await CataloguePage(db, actor.Id, true).OnPostItemApiAsync(drop.Id, item.Id, saved.Version, "789", "Api", null, "save", default);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Null((await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id)).CatalogueValueGp);
    }

    [Fact]
    public async Task StatsPass1OperatorReportIsReadOnlyApplyAuditedAndKeepsFallbacks()
    {
        var (actor, boss, item, _) = await PriceFixtureAsync();
        var manual = new CatalogueItem(Guid.NewGuid(), "Manual", "MANUAL"); manual.ConfigureApi("6"); manual.SetPrice(55, CataloguePriceSource.Manual, PriceApi.Hour);
        var pet = new CatalogueItem(Guid.NewGuid(), "Pet", "PET"); pet.SetPrice(0, CataloguePriceSource.Untradeable, PriceApi.Hour);
        var fallback = new CatalogueItem(Guid.NewGuid(), "Fallback", "FALLBACK"); fallback.ConfigureApi("7"); fallback.SetPrice(33, CataloguePriceSource.Api, PriceApi.Hour.AddHours(-1));
        var ambiguous = new CatalogueItem(Guid.NewGuid(), "Ambiguous", "AMBIGUOUS");
        var api = new PriceApi { Items = [new(4151, item.Name, "item.png"), new(6, "Manual", "manual.png"), new(7, "Fallback", "fallback.png"), new(8, "Ambiguous", "a.png"), new(9, "Ambiguous", "b.png")] };
        await using (var setup = new ApplicationDbContext(options)) { setup.AddRange(manual, pet, fallback, ambiguous); await setup.SaveChangesAsync(); }
        await using (var db = new ApplicationDbContext(options))
        {
            var report = await new CataloguePriceSyncService(db, api, TimeProvider.System).RunAsync(false, null, default);
            Assert.False(report.Applied); Assert.Null(report.Error);
            Assert.Equal(101L, report.Items.Single(x => x.Id == item.Id).ValueGp);
            Assert.Null(report.Items.Single(x => x.Id == ambiguous.Id).ItemId);
        }
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Null((await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id)).CatalogueValueGp);
            Assert.False(await verify.AuditEntries.AnyAsync());
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var report = await new CataloguePriceSyncService(db, api, TimeProvider.System).RunAsync(true, actor.Id, default);
            Assert.True(report.Applied);
        }
        await using var saved = new ApplicationDbContext(options);
        Assert.Equal(55L, (await saved.CatalogueItems.SingleAsync(x => x.Id == manual.Id)).CatalogueValueGp);
        Assert.Equal(CataloguePriceSource.Untradeable, (await saved.CatalogueItems.SingleAsync(x => x.Id == pet.Id)).PriceSource);
        Assert.Equal(33L, (await saved.CatalogueItems.SingleAsync(x => x.Id == fallback.Id)).CatalogueValueGp);
        Assert.Null((await saved.CatalogueItems.SingleAsync(x => x.Id == ambiguous.Id)).CatalogueValueGp);
        var changed = await saved.CatalogueItems.SingleAsync(x => x.Id == item.Id);
        Assert.Equal(101L, changed.CatalogueValueGp);
        var audit = await saved.AuditEntries.SingleAsync(x => x.TargetId == item.Id.ToString() && x.Action == "catalogue.item_api_refreshed");
        using var state = JsonDocument.Parse(audit.AfterState!);
        Assert.Equal(changed.Version, state.RootElement.GetProperty("Version").GetInt64());
        Assert.Equal(10m, (await saved.BossActivities.SingleAsync(x => x.Id == boss.Id)).EfficientCompletionsPerHour);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("admin")]
    [InlineData("inactive")]
    public async Task StatsPass1OperatorRequiresActiveSuperAdmin(string kind)
    {
        var (actor, _, item, _) = await PriceFixtureAsync();
        await using var db = new ApplicationDbContext(options);
        var account = await db.Accounts.SingleAsync(x => x.Id == actor.Id);
        if (kind == "admin") account.SetGlobalRole(GlobalRole.Admin);
        if (kind == "inactive") account.Disable(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CataloguePriceSyncService(db, new PriceApi(), TimeProvider.System).RunAsync(true, kind == "missing" ? null : actor.Id, default));
        Assert.Null((await db.CatalogueItems.SingleAsync(x => x.Id == item.Id)).CatalogueValueGp);
    }

    [Fact]
    public async Task StatsPass1SnapshotV2RoundtripAndV1PreserveMetadataAndRates()
    {
        var (_, boss, item, drop) = await PriceFixtureAsync();
        var path = Path.Combine(Path.GetTempPath(), $"stats-pass1-{Guid.NewGuid():N}.json");
        try
        {
            await using (var db = new ApplicationDbContext(options))
            {
                var saved = await db.CatalogueItems.SingleAsync(x => x.Id == item.Id);
                saved.ConfigureApi("4151"); saved.SetPrice(0, CataloguePriceSource.Manual, PriceApi.Hour); saved.RecordMapping(ApiMappingStatus.Verified, PriceApi.Hour, "Variant", "variant.png");
                (await db.BossActivities.SingleAsync(x => x.Id == boss.Id)).RecordMapping(ApiMappingStatus.Verified, PriceApi.Hour);
                await db.SaveChangesAsync();
                await CatalogueSnapshotTestFixture.WriteAsync(db, path);
                saved.SetPrice(900, CataloguePriceSource.Api, PriceApi.Hour); await db.SaveChangesAsync();
            }
            await using (var db = new ApplicationDbContext(options)) await new CatalogueSnapshotService(db, TimeProvider.System).ApplyAsync(path);
            var json = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
            Assert.Equal(2, json["schemaVersion"]!.GetValue<int>());
            json["schemaVersion"] = 1;
            foreach (var row in json["items"]!.AsArray().Select(x => x!.AsObject()))
                foreach (var key in new[] { "externalIdentifier", "catalogueValueGp", "priceSource", "priceObservedAt", "mappingStatus", "mappingCheckedAt", "matchedApiName", "matchedApiIcon" }) row.Remove(key);
            foreach (var row in json["bosses"]!.AsArray().Select(x => x!.AsObject()))
                foreach (var key in new[] { "externalIdentifier", "mappingStatus", "mappingCheckedAt" }) row.Remove(key);
            await File.WriteAllTextAsync(path, json.ToJsonString());
            await using (var db = new ApplicationDbContext(options)) await new CatalogueSnapshotService(db, TimeProvider.System).ApplyAsync(path);
            await using var verify = new ApplicationDbContext(options);
            var restored = await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            Assert.Equal(0L, restored.CatalogueValueGp); Assert.Equal(CataloguePriceSource.Manual, restored.PriceSource);
            Assert.Equal("4151", restored.ExternalIdentifier); Assert.Equal("Variant", restored.MatchedApiName); Assert.Equal("variant.png", restored.MatchedApiIcon);
            Assert.Equal(PriceApi.Hour, restored.PriceObservedAt); Assert.Equal(ApiMappingStatus.Verified, restored.MappingStatus);
            var source = await verify.BossActivities.SingleAsync(x => x.Id == boss.Id);
            Assert.Equal("zulrah", source.ExternalIdentifier); Assert.Equal(ApiMappingStatus.Verified, source.MappingStatus);
            var rate = await verify.SourceDrops.SingleAsync(x => x.Id == drop.Id);
            Assert.Equal(.001m, rate.NumericProbability); Assert.Equal(7, rate.RollsPerCompletion); Assert.Equal(10m, rate.DefaultEhbEstimate);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task StatsPass1AdditiveMigrationKeepsLegacyValueMissingAndEnforcesPriceConstraint()
    {
        await using var db = new ApplicationDbContext(options);
        var migrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var catalogueIndex = Array.FindIndex(migrations, x => x.EndsWith("AddCatalogueApiMappingAndPrices", StringComparison.Ordinal));
        Assert.True(catalogueIndex > 0);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[catalogueIndex - 1]);
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO catalogue_items (id, name, normalized_name, active, version) VALUES ({id}, 'Legacy fixture', 'LEGACY FIXTURE', true, 1)");
        await migrator.MigrateAsync();
        var legacy = await db.CatalogueItems.SingleAsync(x => x.Id == id);
        Assert.Null(legacy.CatalogueValueGp); Assert.Equal(CataloguePriceSource.Missing, legacy.PriceSource); Assert.Equal(ApiMappingStatus.NotConfigured, legacy.MappingStatus);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE catalogue_items SET catalogue_value_gp = -1, price_source = 'Manual' WHERE id = {id}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE catalogue_items SET catalogue_value_gp = 1, price_source = 'Untradeable' WHERE id = {id}"));
    }

    [Fact]
    public async Task StatsPass1HttpFormsEnforceAuthorityAntiforgeryAndExplicitFetchOnly()
    {
        var (actor, boss, item, drop) = await PriceFixtureAsync();
        var member = Website($"price-member-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
        var api = new PriceApi { Items = [new(4151, item.Name, "item.png"), new(6, "Fresh tradeable", "fresh.png"), new(8, "Ambiguous", "a.png"), new(9, "Ambiguous", "b.png")] };
        await using (var setup = new ApplicationDbContext(options))
        {
            SetPassword(await setup.Accounts.SingleAsync(x => x.Id == actor.Id), DateTimeOffset.UtcNow);
            SetPassword(member, DateTimeOffset.UtcNow); setup.Add(member); await setup.SaveChangesAsync();
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.ConfigureServices(services => { services.RemoveAll<ICatalogueApiClient>(); services.AddSingleton<ICatalogueApiClient>(api); });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var unauthorized = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, actor.LoginName); await LoginAsync(unauthorized, member.LoginName);
        var path = $"/Admin/Catalogue?bossId={boss.Id}";
        var html = await client.GetStringAsync(path);
        Assert.Equal(0, api.Calls);
        Assert.Contains("API mapping and price", html, StringComparison.Ordinal);
        foreach (var handler in new[] { "ItemApi", "BossApi", "SuggestItemApi", "SuggestBossApi" })
        {
            using var forbidden = await PostAsync(unauthorized, $"/Admin/Catalogue?handler={handler}", await unauthorized.GetStringAsync("/"), new Dictionary<string, string>());
            Assert.True(forbidden.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Redirect);
            using var noToken = await client.PostAsync($"/Admin/Catalogue?handler={handler}", new FormUrlEncodedContent(new Dictionary<string, string>()));
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        }
        var settings = new Dictionary<string, string> { ["recordId"] = drop.Id.ToString(), ["expectedItemId"] = item.Id.ToString(), ["expectedItemVersion"] = item.Version.ToString(System.Globalization.CultureInfo.InvariantCulture), ["externalIdentifier"] = "0004151", ["priceMode"] = "Manual", ["manualValue"] = "0", ["operation"] = "save" };
        using (var response = await PostAsync(client, "/Admin/Catalogue?handler=ItemApi", html, settings)) Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(0, api.Calls);
        await using (var verify = new ApplicationDbContext(options))
        {
            var saved = await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            Assert.Equal(0L, saved.CatalogueValueGp); Assert.Equal("4151", saved.ExternalIdentifier);
            settings["expectedItemVersion"] = saved.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        settings["manualValue"] = "not a number";
        using (var response = await PostAsync(client, "/Admin/Catalogue?handler=ItemApi", html, settings)) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        foreach (var name in new[] { "Ambiguous", "Synthetic item (variant)" })
        {
            using var response = await PostAsync(client, "/Admin/Catalogue?handler=SuggestItemApi", html, new Dictionary<string, string> { ["itemName"] = name });
            var suggestion = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            Assert.Null(suggestion["id"]); Assert.NotNull(suggestion["error"]);
        }
        using (var response = await PostAsync(client, "/Admin/Catalogue?handler=SuggestBossApi", html, new Dictionary<string, string> { ["itemName"] = "Zulrah" }))
            Assert.Equal("zulrah", JsonNode.Parse(await response.Content.ReadAsStringAsync())!["id"]!.GetValue<string>());
        using (var response = await PostAsync(client, "/Admin/Catalogue?handler=BossApi", html, new Dictionary<string, string> { ["recordId"] = boss.Id.ToString(), ["expectedVersion"] = boss.Version.ToString(System.Globalization.CultureInfo.InvariantCulture), ["externalIdentifier"] = "unsupported_mode", ["operation"] = "validate" }))
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var add = new Dictionary<string, string> { ["BossDrop.BossActivityId"] = boss.Id.ToString(), ["BossDrop.ItemName"] = "Fresh tradeable", ["BossDrop.DisplayRate"] = "1/1000" };
        var calls = api.Calls;
        using (var response = await PostAsync(client, "/Admin/Catalogue?handler=BossDrop", html, add)) Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(calls, api.Calls);
        await using (var verify = new ApplicationDbContext(options)) Assert.False(await verify.CatalogueItems.AnyAsync(x => x.Name == "Fresh tradeable"));
        add["BossDrop.FetchPrice"] = "true";
        add["BossDrop.InitialWikiItemId"] = "invalid-id";
        using (var invalid = await PostAsync(client, "/Admin/Catalogue?handler=BossDrop", html, add)) Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(calls, api.Calls);
        add.Remove("BossDrop.InitialWikiItemId");
        using (var response = await PostAsync(client, "/Admin/Catalogue?handler=BossDrop", html, add)) Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Equal(999L, (await verify.CatalogueItems.SingleAsync(x => x.Name == "Fresh tradeable")).CatalogueValueGp);
            Assert.Equal(ApiMappingStatus.Unsupported, (await verify.BossActivities.SingleAsync(x => x.Id == boss.Id)).MappingStatus);
        }
        add["BossDrop.ItemName"] = "New untradeable"; add["BossDrop.Untradeable"] = "true";
        calls = api.Calls;
        using (var response = await PostAsync(client, "/Admin/Catalogue?handler=BossDrop", html, add)) Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(calls, api.Calls);
        await using (var verify = new ApplicationDbContext(options)) Assert.Equal(CataloguePriceSource.Untradeable, (await verify.CatalogueItems.SingleAsync(x => x.Name == "New untradeable")).PriceSource);
        add["BossDrop.ItemName"] = item.Name; add["BossDrop.InitialValueGp"] = "500";
        using (var response = await PostAsync(client, "/Admin/Catalogue?handler=BossDrop", html, add)) Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(calls, api.Calls);
        await using (var verify = new ApplicationDbContext(options)) Assert.Equal(0L, (await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id)).CatalogueValueGp);
    }

    private async Task<(Account Actor, BossActivity Boss, CatalogueItem Item, SourceDrop Drop)> PriceFixtureAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var actor = Website($"price-admin-{Guid.NewGuid():N}", now); actor.SetGlobalRole(GlobalRole.SuperAdmin);
        var boss = new BossActivity(Guid.NewGuid(), "Synthetic boss", $"synthetic-{Guid.NewGuid():N}", "Boss", 10, now); boss.ConfigureApi("zulrah");
        var item = new CatalogueItem(Guid.NewGuid(), "Synthetic item", "SYNTHETIC ITEM");
        var drop = new SourceDrop(Guid.NewGuid(), boss.Id, item.Id, "7 x 1/1000", .001m, 10, now);
        drop.SetRateMechanics(DropProbabilityScope.Participant, false, null, 1, 7, "synthetic");
        await using var setup = new ApplicationDbContext(options); setup.AddRange(actor, boss, item, drop); await setup.SaveChangesAsync();
        return (actor, boss, item, drop);
    }

    private sealed class PriceApi : ICatalogueApiClient
    {
        public static readonly DateTimeOffset Hour = new(2026, 9, 15, 11, 0, 0, TimeSpan.Zero);
        public bool Unavailable { get; init; }
        public bool PricesUnavailable { get; init; }
        public IReadOnlyDictionary<int, long?> Prices { get; init; } = new Dictionary<int, long?> { [4151] = 101, [6] = 999 };
        public IReadOnlySet<string> Metrics { get; init; } = new HashSet<string>(StringComparer.Ordinal) { "zulrah" };
        public Func<Task>? BeforeItems { get; init; }
        public int Calls { get; private set; }
        public IReadOnlyList<ApiItem> Items { get; init; } = [new(4151, "Synthetic item", "item.png")];
        public async Task<CatalogueApiResult<IReadOnlyList<ApiItem>>> GetItemsAsync(CancellationToken ct) { Calls++; if (BeforeItems is not null) await BeforeItems(); return new(Unavailable ? null : Items); }
        public Task<CatalogueApiResult<ApiHourlyPrices>> GetHourlyPricesAsync(CancellationToken ct) { Calls++; return Task.FromResult(new CatalogueApiResult<ApiHourlyPrices>(Unavailable || PricesUnavailable ? null : new(Hour, Prices))); }
        public Task<CatalogueApiResult<ApiHourlyPrices>> GetHourlyPricesAsync(DateTimeOffset hour, CancellationToken ct) => GetHourlyPricesAsync(ct);
        public Task<CatalogueApiResult<IReadOnlySet<string>>> GetBossMetricsAsync(CancellationToken ct) { Calls++; return Task.FromResult(new CatalogueApiResult<IReadOnlySet<string>>(Unavailable ? null : Metrics)); }
    }
}
