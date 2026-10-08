using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Catalogue;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Fact]
    public async Task StatsPass2GuardRetainsTrustedValueFlagsOutlierAndClearsOnlyAfterAcceptedValue()
    {
        var (actor, _, item, drop) = await PriceFixtureAsync();
        await TrustPriceAsync(item.Id, 100);
        async Task Apply(PriceApi api)
        {
            await using var db = new ApplicationDbContext(options);
            var version = await db.CatalogueItems.Where(x => x.Id == item.Id).Select(x => x.Version).SingleAsync();
            await CataloguePage(db, actor.Id, true, api).OnPostItemApiAsync(drop.Id, item.Id, version, "4151", "Api", null, "validate", default);
        }
        await Apply(new PriceApi { Prices = new Dictionary<int, long?> { [4151] = 201 } });
        await using (var verify = new ApplicationDbContext(options))
        {
            var saved = await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            Assert.Equal(100, saved.CatalogueValueGp); Assert.Equal(201, saved.RejectedPriceGp);
            Assert.Equal(PriceApi.Hour, saved.RejectedPriceObservedAt); Assert.Equal(ApiMappingStatus.Verified, saved.MappingStatus);
            var path = Path.Combine(Path.GetTempPath(), $"stats-pass2-snapshot-{Guid.NewGuid():N}.json");
            try
            {
                var snapshots = new CatalogueSnapshotService(verify, TimeProvider.System);
                await CatalogueSnapshotTestFixture.WriteAsync(verify, path);
                saved.RestorePriceRejection(null, null); await verify.SaveChangesAsync();
                await snapshots.ApplyAsync(path);
                Assert.Equal(201, (await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id)).RejectedPriceGp);
            }
            finally { File.Delete(path); }
        }
        await Apply(new PriceApi { PricesUnavailable = true });
        await using (var verify = new ApplicationDbContext(options)) Assert.Equal(201, (await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id)).RejectedPriceGp);
        await Apply(new PriceApi { Prices = new Dictionary<int, long?> { [4151] = 200 } });
        await using (var verify = new ApplicationDbContext(options))
        {
            var saved = await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            Assert.Equal(200, saved.CatalogueValueGp); Assert.Null(saved.RejectedPriceGp); Assert.Null(saved.RejectedPriceObservedAt);
        }
    }

    [Fact]
    public async Task StatsPass2GuardAuditFailureAndStaleResponseCannotPersistRejectedFlag()
    {
        var (actor, _, item, drop) = await PriceFixtureAsync();
        await TrustPriceAsync(item.Id, 100);
        var failing = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new ThrowOnAuditInsert()).Options;
        await using (var db = new ApplicationDbContext(failing))
        {
            var version = await db.CatalogueItems.Where(x => x.Id == item.Id).Select(x => x.Version).SingleAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => CataloguePage(db, actor.Id, true,
                new PriceApi { Prices = new Dictionary<int, long?> { [4151] = 201 } })
                .OnPostItemApiAsync(drop.Id, item.Id, version, "4151", "Api", null, "validate", default));
        }
        await using (var verify = new ApplicationDbContext(options))
        {
            var saved = await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            Assert.Equal(100, saved.CatalogueValueGp); Assert.Null(saved.RejectedPriceGp);
        }
        var api = new PriceApi
        {
            Prices = new Dictionary<int, long?> { [4151] = 201 },
            BeforeItems = async () =>
        {
            await using var concurrent = new ApplicationDbContext(options);
            (await concurrent.CatalogueItems.SingleAsync(x => x.Id == item.Id)).SetPrice(75, CataloguePriceSource.Manual, PriceApi.Hour);
            await concurrent.SaveChangesAsync();
        }
        };
        await using (var db = new ApplicationDbContext(options))
        {
            var version = await db.CatalogueItems.Where(x => x.Id == item.Id).Select(x => x.Version).SingleAsync();
            await CataloguePage(db, actor.Id, true, api).OnPostItemApiAsync(drop.Id, item.Id, version, "4151", "Api", null, "validate", default);
        }
        await using (var verify = new ApplicationDbContext(options))
        {
            var saved = await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            Assert.Equal(75, saved.CatalogueValueGp); Assert.Equal(CataloguePriceSource.Manual, saved.PriceSource); Assert.Null(saved.RejectedPriceGp);
            Assert.Empty(await verify.AuditEntries.Where(x => x.Action == "catalogue.item_api_updated").ToListAsync());
        }
    }

    [Fact]
    public async Task StatsPass2OperatorReportExplainsRejectedPriceWithoutWritingAndApplyPersistsFlag()
    {
        var (actor, _, item, _) = await PriceFixtureAsync();
        await TrustPriceAsync(item.Id, 100);
        var api = new PriceApi { Prices = new Dictionary<int, long?> { [4151] = 0 } };
        await using (var report = new ApplicationDbContext(options))
        {
            var result = await new CataloguePriceSyncService(report, api, TimeProvider.System).RunAsync(false, null, default);
            var row = Assert.Single(result.Items, x => x.Id == item.Id);
            Assert.Equal(100, row.ValueGp); Assert.Contains("Rejected unusual API candidate 0", row.Status);
        }
        await using (var verify = new ApplicationDbContext(options)) Assert.Null((await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id)).RejectedPriceGp);
        await using (var apply = new ApplicationDbContext(options))
            Assert.True((await new CataloguePriceSyncService(apply, api, TimeProvider.System).RunAsync(true, actor.Id, default)).Applied);
        await using (var verify = new ApplicationDbContext(options))
        {
            var saved = await verify.CatalogueItems.SingleAsync(x => x.Id == item.Id);
            Assert.Equal(100, saved.CatalogueValueGp); Assert.Equal(0, saved.RejectedPriceGp);
            Assert.Single(await verify.AuditEntries.Where(x => x.Action == "catalogue.item_api_refreshed").ToListAsync());
        }
    }

    private async Task TrustPriceAsync(Guid id, long value)
    {
        await using var db = new ApplicationDbContext(options);
        var item = await db.CatalogueItems.SingleAsync(x => x.Id == id);
        item.ConfigureApi("4151"); item.SetPrice(value, CataloguePriceSource.Api, PriceApi.Hour.AddHours(-1)); await db.SaveChangesAsync();
    }
}
