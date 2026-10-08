using System.Net;
using System.Text.RegularExpressions;
using Bingo.Application.Catalogue;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Bingo.Web.Catalogue;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Theory]
    [InlineData("Vorkath", "vorkath")]
    [InlineData("No known activity", null)]
    public async Task StatsPass1ReviewF1OperatorUsesCurrentNameAndPreservesExplicitMetric(string currentName, string? expectedMetric)
    {
        var (actor, explicitBoss, _, _) = await PriceFixtureAsync();
        Guid renamedId;
        var api = new PriceApi { Metrics = new HashSet<string>(StringComparer.Ordinal) { "zulrah", "vorkath" } };
        await using (var setup = new ApplicationDbContext(options))
        {
            var renamed = await setup.BossActivities.SingleAsync(x => x.Slug == "zulrah");
            renamedId = renamed.Id;
            renamed.Update(currentName, "Boss", 10, null, null, null, PriceApi.Hour);
            (await setup.BossActivities.SingleAsync(x => x.Id == explicitBoss.Id)).Update("Vorkath", "Boss", 10, "zulrah", null, null, PriceApi.Hour);
            await setup.SaveChangesAsync();
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var report = await new CataloguePriceSyncService(db, api, TimeProvider.System).RunAsync(false, null, default);
            Assert.Equal(expectedMetric is null, report.UnresolvedSources.Contains(currentName, StringComparer.Ordinal));
            Assert.Null((await db.BossActivities.SingleAsync(x => x.Id == renamedId)).ExternalIdentifier);
        }
        await using (var db = new ApplicationDbContext(options))
            Assert.True((await new CataloguePriceSyncService(db, api, TimeProvider.System).RunAsync(true, actor.Id, default)).Applied);
        await using var verify = new ApplicationDbContext(options);
        var saved = await verify.BossActivities.SingleAsync(x => x.Id == renamedId);
        Assert.Equal("zulrah", saved.Slug); Assert.Equal(expectedMetric, saved.ExternalIdentifier);
        Assert.Equal(expectedMetric is null ? ApiMappingStatus.NotConfigured : ApiMappingStatus.Verified, saved.MappingStatus);
        var configured = await verify.BossActivities.SingleAsync(x => x.Id == explicitBoss.Id);
        Assert.Equal("zulrah", configured.ExternalIdentifier); Assert.Equal(ApiMappingStatus.Verified, configured.MappingStatus);
    }

    [Theory]
    [InlineData("missing", CataloguePriceSource.Api, true)]
    [InlineData("price-outage", CataloguePriceSource.Api, true)]
    [InlineData("mapping-outage", CataloguePriceSource.Api, true)]
    [InlineData("save", CataloguePriceSource.Api, true)]
    [InlineData("success", CataloguePriceSource.Api, true)]
    [InlineData("missing", CataloguePriceSource.Api, false)]
    [InlineData("missing", CataloguePriceSource.Manual, true)]
    [InlineData("missing", CataloguePriceSource.Untradeable, true)]
    public async Task StatsPass1ReviewF2FeedbackReflectsClearedOrRetainedPrice(string outcome, CataloguePriceSource initialSource, bool changeId)
    {
        var (actor, _, item, drop) = await PriceFixtureAsync();
        var initialValue = initialSource == CataloguePriceSource.Untradeable ? 0L : 100L;
        await using (var setup = new ApplicationDbContext(options))
        {
            var saved = await setup.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            saved.ConfigureApi("4151"); saved.SetPrice(initialValue, initialSource, PriceApi.Hour);
            await setup.SaveChangesAsync();
        }
        var api = new PriceApi
        {
            Items = [new(4151, item.Name, "a.png"), new(6, "Exact variant", "b.png")],
            Prices = outcome == "success" ? new Dictionary<int, long?> { [6] = 900 } : new Dictionary<int, long?>(),
            PricesUnavailable = outcome == "price-outage",
            Unavailable = outcome == "mapping-outage"
        };
        var cleared = initialSource == CataloguePriceSource.Api && changeId && outcome != "success";
        await using (var db = new ApplicationDbContext(options))
        {
            var saved = await db.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            var page = CataloguePage(db, actor.Id, true, api);
            Assert.IsType<RedirectToPageResult>(await page.OnPostItemApiAsync(drop.Id, item.Id, saved.Version, changeId ? "6" : "4151",
                initialSource.ToString(), initialValue, outcome == "save" ? "save" : "validate", default));
            var feedback = Assert.IsType<string>(page.TempData["StatusMessage"]);
            if (cleared)
            {
                Assert.Contains("cleared its old API price", feedback, StringComparison.Ordinal);
                Assert.Contains("manual catalogue value", feedback, StringComparison.Ordinal);
                Assert.Contains("retry validation", feedback, StringComparison.Ordinal);
                Assert.DoesNotContain("unchanged", feedback, StringComparison.Ordinal);
                Assert.Equal("Information", page.TempData[UiMessage.TypeKey]);
                if (outcome == "price-outage") Assert.Contains("price API is temporarily unavailable", feedback, StringComparison.Ordinal);
                if (outcome == "missing") Assert.Contains("No hourly price", feedback, StringComparison.Ordinal);
            }
            else
            {
                Assert.DoesNotContain("cleared", feedback, StringComparison.Ordinal);
                Assert.Contains(outcome == "success" ? "hourly price saved" : "value was kept", feedback, StringComparison.Ordinal);
            }
            if (outcome == "save") Assert.Equal(0, api.Calls);
        }
        await using var verify = new ApplicationDbContext(options);
        var persisted = await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id);
        Assert.Equal(cleared ? null : outcome == "success" ? 900L : initialValue, persisted.CatalogueValueGp);
        Assert.Equal(cleared ? CataloguePriceSource.Missing : initialSource, persisted.PriceSource);
    }

    [Theory]
    [InlineData("mapping-outage", true)]
    [InlineData("unsupported-id", true)]
    [InlineData("no-name-match", true)]
    [InlineData("price-outage", true)]
    [InlineData("missing-price", true)]
    [InlineData("mapping-outage", false)]
    [InlineData("unsupported-id", false)]
    [InlineData("no-name-match", false)]
    [InlineData("price-outage", false)]
    [InlineData("missing-price", false)]
    public async Task StatsPass1ReviewF3FetchAndAddReportsAttemptAndFixedFallback(string failure, bool manualFallback)
    {
        var (actor, boss, _, _) = await PriceFixtureAsync();
        await using (var setup = new ApplicationDbContext(options))
        {
            SetPassword(await setup.Accounts.SingleAsync(x => x.Id == actor.Id), DateTimeOffset.UtcNow);
            await setup.SaveChangesAsync();
        }
        var api = new PriceApi
        {
            Items = [new(6, "Fresh fixture", "fresh.png")],
            Unavailable = failure == "mapping-outage",
            PricesUnavailable = failure == "price-outage",
            Prices = new Dictionary<int, long?>()
        };
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.ConfigureServices(services => { services.RemoveAll<ICatalogueApiClient>(); services.AddSingleton<ICatalogueApiClient>(api); });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, actor.LoginName);
        var path = $"/Admin/Catalogue?activity={boss.Id}";
        var html = await client.GetStringAsync(path);
        Assert.Equal(0, api.Calls);
        var name = failure == "no-name-match" ? "Unknown exact name" : "Fresh fixture";
        var form = new Dictionary<string, string>
        {
            ["BossId"] = boss.Id.ToString(),
            ["BossDrop.BossActivityId"] = boss.Id.ToString(),
            ["BossDrop.ItemName"] = name,
            ["BossDrop.DisplayRate"] = "1/1000",
            ["BossDrop.FetchPrice"] = "true"
        };
        if (failure != "no-name-match") form["BossDrop.InitialWikiItemId"] = failure == "unsupported-id" ? "999" : "6";
        if (manualFallback) form["BossDrop.InitialValueGp"] = "17";
        var attemptedAt = DateTimeOffset.UtcNow;
        using var response = await PostAsync(client, "/Admin/Catalogue?handler=BossDrop", html, form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var result = await client.GetStringAsync(response.Headers.Location!.OriginalString);
        // A10 (T2 Catalogue binding): the plain-form status message is the shared server toast
        // (error tone for Error) instead of the retired data-catalogue-status-* attributes.
        var toast = Regex.Match(result, "<div class=\"toast ?(?<tone>is-error)?\" data-toast[^>]*>.*?<span class=\"grow\" data-component-text>(?<text>[^<]*)</span>", RegexOptions.Singleline);
        Assert.True(toast.Success);
        var feedback = WebUtility.HtmlDecode(toast.Groups["text"].Value);
        var expectedReason = failure switch
        {
            "mapping-outage" => "item mapping API is temporarily unavailable",
            "unsupported-id" => "item ID is not in the tradeable item mapping",
            "no-name-match" => "No unique exact-name item match",
            "price-outage" => "price API is temporarily unavailable",
            _ => "No hourly price is available"
        };
        Assert.Contains(expectedReason, feedback, StringComparison.Ordinal);
        Assert.Equal(failure is "price-outage" or "missing-price" ? 2 : 1, api.Calls);
        await using var verify = new ApplicationDbContext(options);
        var savedItem = await verify.CatalogueItems.SingleOrDefaultAsync(x => x.Name == name);
        if (!manualFallback)
        {
            Assert.Null(savedItem);
            Assert.True(toast.Groups["tone"].Success, "Error status is the error toast");
            Assert.Contains("drop was not added", feedback, StringComparison.Ordinal);
            Assert.Contains("manual catalogue value", feedback, StringComparison.Ordinal);
            return;
        }
        Assert.NotNull(savedItem); Assert.Equal(17L, savedItem.CatalogueValueGp); Assert.Equal(CataloguePriceSource.Manual, savedItem.PriceSource);
        Assert.False(toast.Groups["tone"].Success, "Information status is the ordinary toast");
        Assert.Contains("manual value (17 GP) was used and stays fixed", feedback, StringComparison.Ordinal);
        Assert.Contains("validate again to use API pricing", feedback, StringComparison.Ordinal);
        var expectedStatus = failure switch
        {
            "mapping-outage" => ApiMappingStatus.TemporarilyUnavailable,
            "unsupported-id" => ApiMappingStatus.Unsupported,
            "no-name-match" => ApiMappingStatus.NotConfigured,
            _ => ApiMappingStatus.Verified
        };
        Assert.Equal(expectedStatus, savedItem.MappingStatus);
        if (failure == "no-name-match") { Assert.Null(savedItem.ExternalIdentifier); Assert.Null(savedItem.MappingCheckedAt); }
        else
        {
            Assert.Equal(failure == "unsupported-id" ? "999" : "6", savedItem.ExternalIdentifier);
            Assert.InRange(savedItem.MappingCheckedAt!.Value, attemptedAt, DateTimeOffset.UtcNow);
        }
    }
}
