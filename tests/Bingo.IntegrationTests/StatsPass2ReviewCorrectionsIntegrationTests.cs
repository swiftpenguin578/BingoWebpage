using System.Net;
using Bingo.Application.Catalogue;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Theory]
    [InlineData("6", "save", "unused", false)]
    [InlineData(null, "save", "unused", false)]
    [InlineData("6", "validate", "outage", false)]
    [InlineData(null, "validate", "outage", false)]
    [InlineData("6", "validate", "missing", false)]
    [InlineData("4151", "validate", "outage", true)]
    [InlineData("0004151", "validate", "missing", true)]
    [InlineData("4151", "save", "unused", true)]
    public async Task StatsPass2ReviewF1MappingChangeClearsOnlyObsoletePriceRejection(string? identifier, string operation, string response, bool unchanged)
    {
        var (actor, _, item, drop) = await PriceFixtureAsync();
        await TrustPriceAsync(item.Id, 100);
        await using (var reject = new ApplicationDbContext(options))
        {
            var version = await reject.CatalogueItems.Where(x => x.Id == item.Id).Select(x => x.Version).SingleAsync();
            await CataloguePage(reject, actor.Id, true, new PriceApi { Prices = new Dictionary<int, long?> { [4151] = 201 } })
                .OnPostItemApiAsync(drop.Id, item.Id, version, "4151", "Api", null, "validate", default);
        }
        await using (var verifyRejection = new ApplicationDbContext(options))
            Assert.Equal(201, (await verifyRejection.CatalogueItems.SingleAsync(x => x.Id == item.Id)).RejectedPriceGp);
        await using (var change = new ApplicationDbContext(options))
        {
            var version = await change.CatalogueItems.Where(x => x.Id == item.Id).Select(x => x.Version).SingleAsync();
            var api = new PriceApi
            {
                PricesUnavailable = response == "outage",
                Prices = new Dictionary<int, long?>(),
                Items = [new(4151, "Synthetic item", "old.png"), new(6, "New mapped identity", "new.png")]
            };
            await CataloguePage(change, actor.Id, true, api)
                .OnPostItemApiAsync(drop.Id, item.Id, version, identifier, "Api", null, operation, default);
        }
        await using var verify = new ApplicationDbContext(options);
        var saved = await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id);
        Assert.Equal(unchanged ? "4151" : identifier, saved.ExternalIdentifier);
        Assert.Equal(unchanged ? (long?)100 : null, saved.CatalogueValueGp);
        Assert.Equal(unchanged ? (long?)201 : null, saved.RejectedPriceGp);
        Assert.Equal(unchanged ? (DateTimeOffset?)PriceApi.Hour : null, saved.RejectedPriceObservedAt);
        Assert.Equal(2, await verify.AuditEntries.CountAsync(x => x.Action == "catalogue.item_api_updated"));
    }

    [Fact]
    public async Task StatsPass2ReviewF2DanishManageLocalizesReadinessAndFailedStartPriceBlocker()
    {
        var (actor, item, boss, drops, eventId) = await PriceBoardFixtureAsync();
        await CreatePriceBoardTileAsync(eventId, actor.Id, boss.Id, drops);
        await ApprovePriceBoardAsync(eventId, actor.Id); await PublishPriceBoardAsync(eventId, actor.Id);
        await SetBoardFixturePriceAsync(item.Id, null);
        await using (var setup = new ApplicationDbContext(options))
        {
            SetPassword(await setup.Accounts.SingleAsync(x => x.Id == actor.Id), DateTimeOffset.UtcNow);
            await setup.SaveChangesAsync();
        }
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICatalogueApiClient>();
                services.AddSingleton<ICatalogueApiClient>(new PriceApi { Unavailable = true });
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("da");
        await LoginAsync(client, actor.LoginName);
        var path = $"/Admin/Events/Manage/{eventId}?culture=da&ui-culture=da";
        var page = await client.GetStringAsync(path);
        var expected = $"Disse drops mangler en GP-værdi i kataloget: {item.Name}. Angiv en værdi i Admin Katalog, og prøv igen. En udtrykkelig værdi på 0 er gyldig.";
        Assert.Contains(expected, WebUtility.HtmlDecode(page));
        Assert.DoesNotContain("These drops have no catalogue GP value", page, StringComparison.Ordinal);
        long version;
        await using (var read = new ApplicationDbContext(options)) version = await read.Events.Where(x => x.Id == eventId).Select(x => x.Version).SingleAsync();
        using var response = await PostAsync(client, path + "&handler=StartEvent", page, new Dictionary<string, string>
        {
            ["EventVersion"] = version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ConfirmStartEvent"] = "true",
            ["StartReason"] = "Controlled early start"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains($"<span>{expected}</span>", result, StringComparison.Ordinal);
        Assert.DoesNotContain("These drops have no catalogue GP value", result, StringComparison.Ordinal);
        Assert.DoesNotContain("These drops have no catalogue GP value: {0}", result, StringComparison.Ordinal);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(EventState.SignupClosed, await verify.Events.Where(x => x.Id == eventId).Select(x => x.State).SingleAsync());
        Assert.Empty(await verify.EventItemPrices.ToListAsync());
    }
}
