using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Catalogue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Fact]
    public async Task Au21AddingAnExistingItemRequiresExplicitSharedItemIntent()
    {
        var (actor, _, item, _) = await PriceFixtureAsync();
        var targetBoss = new BossActivity(Guid.NewGuid(), $"Target {Guid.NewGuid():N}", $"target-{Guid.NewGuid():N}", "Boss", 10m, DateTimeOffset.UtcNow);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.BossActivities.Add(targetBoss);
            await setup.SaveChangesAsync();
        }

        await using (var refused = new ApplicationDbContext(options))
        {
            var page = CataloguePage(refused, actor.Id, true);
            page.PageContext.HttpContext.RequestServices = new ServiceCollection().AddMvcCore().AddDataAnnotations().Services.BuildServiceProvider();
            page.BossDrop = new IndexModel.BossDropInput
            {
                BossActivityId = targetBoss.Id,
                ItemName = item.Name,
                DisplayRate = "1/500",
                UseExistingItem = false
            };

            Assert.IsType<RedirectToPageResult>(await page.OnPostBossDropAsync(CancellationToken.None));
            Assert.Contains("shared item", page.TempData["StatusMessage"]?.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.False(await refused.SourceDrops.AnyAsync(drop => drop.BossActivityId == targetBoss.Id));
        }

        await using (var accepted = new ApplicationDbContext(options))
        {
            var page = CataloguePage(accepted, actor.Id, true);
            page.PageContext.HttpContext.RequestServices = new ServiceCollection().AddMvcCore().AddDataAnnotations().Services.BuildServiceProvider();
            page.BossDrop = new IndexModel.BossDropInput
            {
                BossActivityId = targetBoss.Id,
                ItemName = item.Name,
                DisplayRate = "1/500",
                UseExistingItem = true
            };

            Assert.IsType<RedirectToPageResult>(await page.OnPostBossDropAsync(CancellationToken.None));
        }

        await using var verify = new ApplicationDbContext(options);
        var adopted = await verify.SourceDrops.SingleAsync(drop => drop.BossActivityId == targetBoss.Id);
        Assert.Equal(item.Id, adopted.ItemId);
        Assert.Equal(2, await verify.SourceDrops.CountAsync(drop => drop.ItemId == item.Id));
        Assert.Equal(targetBoss.Id, (await verify.SourceDrops.SingleAsync(drop => drop.Id == adopted.Id)).BossActivityId);
    }

    [Fact]
    public async Task Au21SharedRenameAndImageChangeNamesAffectedActivitiesBeforeSaving()
    {
        var (actor, _, item, drop) = await PriceFixtureAsync();
        var secondBoss = new BossActivity(Guid.NewGuid(), $"Second {Guid.NewGuid():N}", $"second-{Guid.NewGuid():N}", "Boss", 10m, DateTimeOffset.UtcNow);
        var secondDrop = new SourceDrop(Guid.NewGuid(), secondBoss.Id, item.Id, "1/200", .005m, 20m, DateTimeOffset.UtcNow);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(secondBoss, secondDrop);
            await setup.SaveChangesAsync();
        }

        await using (var refused = new ApplicationDbContext(options))
        {
            var current = await refused.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await refused.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            var page = CataloguePage(refused, actor.Id, true);

            Assert.IsType<RedirectToPageResult>(await page.OnPostUpdateDropAsync(
                current.Id, current.Version, shared.Version, "Renamed shared item", current.DisplayRate,
                current.DisplayRate, null, null, default, false, null, 0, 0, null, null,
                "https://example.com/new.png", false, CancellationToken.None));
            Assert.Contains(secondBoss.Name, page.TempData["StatusMessage"]?.ToString(), StringComparison.Ordinal);
            Assert.Equal(item.Name, await refused.CatalogueItems.Where(value => value.Id == item.Id).Select(value => value.Name).SingleAsync());
            Assert.Equal(item.ImageUrl, await refused.CatalogueItems.Where(value => value.Id == item.Id).Select(value => value.ImageUrl).SingleAsync());
        }

        await using (var accepted = new ApplicationDbContext(options))
        {
            var current = await accepted.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await accepted.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            var page = CataloguePage(accepted, actor.Id, true);

            Assert.IsType<RedirectToPageResult>(await page.OnPostUpdateDropAsync(
                current.Id, current.Version, shared.Version, "Renamed shared item", current.DisplayRate,
                current.DisplayRate, null, null, default, false, null, 0, 0, null, null,
                "https://example.com/new.png", false, CancellationToken.None, [secondBoss.Id]));
        }

        await using var verify = new ApplicationDbContext(options);
        var savedItem = await verify.CatalogueItems.SingleAsync(value => value.Id == item.Id);
        Assert.Equal("Renamed shared item", savedItem.Name);
        Assert.Equal("https://example.com/new.png", savedItem.ImageUrl);
        Assert.Equal(item.Id, await verify.SourceDrops.Where(value => value.Id == secondDrop.Id).Select(value => value.ItemId).SingleAsync());
    }
}
