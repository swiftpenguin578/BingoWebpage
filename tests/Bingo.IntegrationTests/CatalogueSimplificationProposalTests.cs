using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Fact]
    public async Task Cat01RetiredMetadataSubmissionIsRecoverableAndDoesNotSave()
    {
        var (actor, _, item, drop) = await PriceFixtureAsync();
        await using var db = new ApplicationDbContext(options);
        var page = CataloguePage(db, actor.Id, true);
        page.Request.ContentType = "application/x-www-form-urlencoded";
        page.Request.Form = new FormCollection(new Dictionary<string, StringValues> { ["rollGroup"] = "replacement" });
        await page.OnPostUpdateDropAsync(drop.Id, drop.Version, item.Version, item.Name, "1/500", "1/1000",
            null, null, default, false, null, 0, 0, "replacement", null, null, false, default);
        Assert.Contains("were not saved", page.TempData["StatusMessage"]?.ToString(), StringComparison.Ordinal);
        Assert.Equal("7 x 1/1000", await db.SourceDrops.Where(x => x.Id == drop.Id).Select(x => x.DisplayRate).SingleAsync());
        Assert.False(await db.AuditEntries.AnyAsync(x => x.Action == "catalogue.drop_updated"));
    }

    [Fact]
    public async Task Cat01PermanentDeleteRequiresRoleConfirmationAndCurrentVersion()
    {
        var (actor, _, item, drop) = await PriceFixtureAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            var unauthorized = CataloguePage(db, actor.Id, false);
            Assert.IsType<ForbidResult>(await unauthorized.OnGetDeletionImpactAsync("drop", drop.Id, drop.Version, default));
            Assert.IsType<ForbidResult>(await unauthorized.OnPostDeleteAsync("drop", drop.Id, drop.Version, true, default));
            var page = CataloguePage(db, actor.Id, true);
            Assert.IsType<JsonResult>(await page.OnGetDeletionImpactAsync("drop", drop.Id, drop.Version, default));
            await page.OnPostDeleteAsync("drop", drop.Id, drop.Version, false, default);
            Assert.Contains("confirm", page.TempData["StatusMessage"]?.ToString(), StringComparison.OrdinalIgnoreCase);
            await page.OnPostDeleteAsync("drop", drop.Id, drop.Version + 1, true, default);
            Assert.Contains("changed", page.TempData["StatusMessage"]?.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.True(await db.SourceDrops.AnyAsync(x => x.Id == drop.Id));
            await page.OnPostDeleteAsync("drop", drop.Id, drop.Version, true, default);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.False(await verify.SourceDrops.AnyAsync(x => x.Id == drop.Id));
        Assert.True(await verify.CatalogueItems.AnyAsync(x => x.Id == item.Id));
        Assert.Single(await verify.AuditEntries.Where(x => x.Action == "catalogue.drop_deleted").ToListAsync());
    }
}
