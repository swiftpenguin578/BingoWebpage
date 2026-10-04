using System.Text.Json;
using Bingo.Application.Catalogue;
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
    public async Task Au21RateOnlyEditOnSharedLegacyImageNormalizesBothSidesAndDoesNotRewriteImage()
    {
        var (actor, _, item, drop) = await PriceFixtureAsync();
        var second = await AddSharedActivityAsync(item.Id, "legacy-image-second");
        const string oldImage = "https://oldschool.runescape.wiki/Special:Redirect/file/Long_bone.png";
        await using (var setup = new ApplicationDbContext(options))
        {
            var shared = await setup.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            shared.Update(shared.Name, shared.NormalizedName, shared.ExternalIdentifier, shared.Notes, oldImage);
            await setup.SaveChangesAsync();
        }

        await using (var edit = new ApplicationDbContext(options))
        {
            var current = await edit.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await edit.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            var page = CataloguePage(edit, actor.Id, true);
            Assert.IsType<RedirectToPageResult>(await UpdateDropAsync(page, current, shared, shared.Name,
                OsrsWikiImageUrl.Normalize(oldImage), "1/900"));
            Assert.DoesNotContain("shared item", page.TempData["StatusMessage"]?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal("1/900", await verify.SourceDrops.Where(value => value.Id == drop.Id).Select(value => value.DisplayRate).SingleAsync());
        Assert.Equal(oldImage, await verify.CatalogueItems.Where(value => value.Id == item.Id).Select(value => value.ImageUrl).SingleAsync());
        Assert.Equal(second.Boss.Id, await verify.SourceDrops.Where(value => value.Id == second.Drop.Id).Select(value => value.BossActivityId).SingleAsync());
    }

    [Fact]
    public async Task Au21SharedRenameAndImageChangesRequireExactAffectedActivityConfirmation()
    {
        var (actor, _, item, drop) = await PriceFixtureAsync();
        var second = await AddSharedActivityAsync(item.Id, "rename-second");
        var third = await AddSharedActivityAsync(item.Id, "rename-third");
        const string originalImage = "https://oldschool.runescape.wiki/w/Special:Redirect/file/Original.png";
        await using (var setup = new ApplicationDbContext(options))
        {
            var shared = await setup.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            shared.Update(shared.Name, shared.NormalizedName, shared.ExternalIdentifier, shared.Notes, originalImage);
            await setup.SaveChangesAsync();
        }

        long itemVersionBeforeRefusal;
        var auditCountBeforeRefusal = 0;
        await using (var refused = new ApplicationDbContext(options))
        {
            var current = await refused.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await refused.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            itemVersionBeforeRefusal = shared.Version;
            auditCountBeforeRefusal = await refused.AuditEntries.CountAsync();
            var page = CataloguePage(refused, actor.Id, true);
            Assert.IsType<RedirectToPageResult>(await UpdateDropAsync(page, current, shared, "Renamed shared item", originalImage));
            AssertAffected(page, [second.Boss, third.Boss]);
            AssertNoCatalogueMutation(refused, item.Id, itemVersionBeforeRefusal, auditCountBeforeRefusal);
        }

        await using (var partial = new ApplicationDbContext(options))
        {
            var current = await partial.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await partial.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            var page = CataloguePage(partial, actor.Id, true);
            Assert.IsType<RedirectToPageResult>(await UpdateDropAsync(page, current, shared, "Renamed shared item", originalImage, "", [second.Boss.Id]));
            AssertAffected(page, [second.Boss, third.Boss]);
            AssertNoCatalogueMutation(partial, item.Id, itemVersionBeforeRefusal, auditCountBeforeRefusal);
        }

        await using (var rename = new ApplicationDbContext(options))
        {
            var current = await rename.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await rename.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            var page = CataloguePage(rename, actor.Id, true);
            Assert.IsType<RedirectToPageResult>(await UpdateDropAsync(page, current, shared, "Renamed shared item", originalImage, "", [second.Boss.Id, third.Boss.Id]));
        }

        long renamedVersion;
        int renamedAuditCount;
        await using (var verifyRename = new ApplicationDbContext(options))
        {
            var saved = await verifyRename.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            Assert.Equal("Renamed shared item", saved.Name);
            renamedVersion = saved.Version;
            renamedAuditCount = await verifyRename.AuditEntries.CountAsync();
        }

        const string changedImage = "https://example.com/changed.png";
        await using (var refusedImage = new ApplicationDbContext(options))
        {
            var current = await refusedImage.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await refusedImage.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            var page = CataloguePage(refusedImage, actor.Id, true);
            Assert.IsType<RedirectToPageResult>(await UpdateDropAsync(page, current, shared, shared.Name, changedImage));
            AssertAffected(page, [second.Boss, third.Boss]);
            AssertNoCatalogueMutation(refusedImage, item.Id, renamedVersion, renamedAuditCount);
        }

        await using (var image = new ApplicationDbContext(options))
        {
            var current = await image.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await image.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            var page = CataloguePage(image, actor.Id, true);
            Assert.IsType<RedirectToPageResult>(await UpdateDropAsync(page, current, shared, shared.Name, changedImage, "", [second.Boss.Id, third.Boss.Id]));
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(changedImage, await verify.CatalogueItems.Where(value => value.Id == item.Id).Select(value => value.ImageUrl).SingleAsync());
        Assert.Equal(item.Id, await verify.SourceDrops.Where(value => value.Id == third.Drop.Id).Select(value => value.ItemId).SingleAsync());
    }

    [Fact]
    public async Task Au21NewSharedActivityBetweenRefusalAndConfirmationRequiresFreshActivitySet()
    {
        var (actor, _, item, drop) = await PriceFixtureAsync();
        var second = await AddSharedActivityAsync(item.Id, "new-dependency-second");
        var third = await AddSharedActivityAsync(item.Id, "new-dependency-third");
        var changedName = "Fresh shared name";
        Guid[] initiallyAffected;
        long itemVersion;
        int auditCount;
        await using (var refused = new ApplicationDbContext(options))
        {
            var current = await refused.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await refused.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            var page = CataloguePage(refused, actor.Id, true);
            Assert.IsType<RedirectToPageResult>(await UpdateDropAsync(page, current, shared, changedName, null));
            initiallyAffected = AssertAffected(page, [second.Boss, third.Boss]).Select(value => value.Id).ToArray();
            itemVersion = shared.Version;
            auditCount = await refused.AuditEntries.CountAsync();
        }

        var fourth = await AddSharedActivityAsync(item.Id, "new-dependency-fourth");
        await using (var staleConfirmation = new ApplicationDbContext(options))
        {
            var current = await staleConfirmation.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await staleConfirmation.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            var page = CataloguePage(staleConfirmation, actor.Id, true);
            Assert.IsType<RedirectToPageResult>(await UpdateDropAsync(page, current, shared, changedName, null, "", initiallyAffected));
            AssertAffected(page, [second.Boss, third.Boss, fourth.Boss]);
            AssertNoCatalogueMutation(staleConfirmation, item.Id, itemVersion, auditCount);
        }

        await using (var accepted = new ApplicationDbContext(options))
        {
            var current = await accepted.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await accepted.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            var page = CataloguePage(accepted, actor.Id, true);
            Assert.IsType<RedirectToPageResult>(await UpdateDropAsync(page, current, shared, changedName, null, "", [second.Boss.Id, third.Boss.Id, fourth.Boss.Id]));
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(changedName, await verify.CatalogueItems.Where(value => value.Id == item.Id).Select(value => value.Name).SingleAsync());
    }

    [Fact]
    public async Task Au21AddSharedItemImageRequiresConfirmationAndIgnoresEquivalentImageChange()
    {
        var (actor, boss, item, _) = await PriceFixtureAsync();
        var second = await AddSharedActivityAsync(item.Id, "add-image-second");
        const string currentImage = "https://oldschool.runescape.wiki/w/Special:Redirect/file/Adopted.png";
        const string changedImage = "https://oldschool.runescape.wiki/w/Special:Redirect/file/Adopted-new.png";
        await using (var setup = new ApplicationDbContext(options))
        {
            var shared = await setup.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            shared.Update(shared.Name, shared.NormalizedName, shared.ExternalIdentifier, shared.Notes, currentImage);
            await setup.SaveChangesAsync();
        }

        Guid[] affected;
        long itemVersion;
        int auditCount;
        var target = new BossActivity(Guid.NewGuid(), "Adoption target", $"adoption-target-{Guid.NewGuid():N}", "Boss", 10m, DateTimeOffset.UtcNow);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.BossActivities.Add(target);
            await setup.SaveChangesAsync();
        }

        await using (var refused = new ApplicationDbContext(options))
        {
            var shared = await refused.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            itemVersion = shared.Version;
            auditCount = await refused.AuditEntries.CountAsync();
            var page = CataloguePage(refused, actor.Id, true);
            ConfigureAdd(page, target, item, changedImage);
            Assert.IsType<RedirectToPageResult>(await page.OnPostBossDropAsync(CancellationToken.None));
            affected = AssertAffected(page, [boss, second.Boss]).Select(value => value.Id).ToArray();
            Assert.False(await refused.SourceDrops.AnyAsync(value => value.BossActivityId == target.Id));
            AssertNoCatalogueMutation(refused, item.Id, itemVersion, auditCount);
        }

        await using (var accepted = new ApplicationDbContext(options))
        {
            var page = CataloguePage(accepted, actor.Id, true);
            ConfigureAdd(page, target, item, changedImage);
            Assert.IsType<RedirectToPageResult>(await page.OnPostBossDropAsync(CancellationToken.None, affected));
        }

        var equivalentTarget = new BossActivity(Guid.NewGuid(), "Equivalent image target", $"equivalent-target-{Guid.NewGuid():N}", "Boss", 10m, DateTimeOffset.UtcNow);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.BossActivities.Add(equivalentTarget);
            await setup.SaveChangesAsync();
        }
        await using (var equivalent = new ApplicationDbContext(options))
        {
            var page = CataloguePage(equivalent, actor.Id, true);
            ConfigureAdd(page, equivalentTarget, item, "https://oldschool.runescape.wiki/Special:Redirect/file/Adopted-new.png");
            Assert.IsType<RedirectToPageResult>(await page.OnPostBossDropAsync(CancellationToken.None));
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(changedImage, await verify.CatalogueItems.Where(value => value.Id == item.Id).Select(value => value.ImageUrl).SingleAsync());
        Assert.True(await verify.SourceDrops.AnyAsync(value => value.BossActivityId == target.Id && value.ItemId == item.Id));
        Assert.True(await verify.SourceDrops.AnyAsync(value => value.BossActivityId == equivalentTarget.Id && value.ItemId == item.Id));
    }

    [Fact]
    public async Task Au21SingleActivityRenameDoesNotRequireSharedConfirmation()
    {
        var (actor, _, item, drop) = await PriceFixtureAsync();
        await using (var edit = new ApplicationDbContext(options))
        {
            var current = await edit.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await edit.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            var page = CataloguePage(edit, actor.Id, true);
            Assert.IsType<RedirectToPageResult>(await UpdateDropAsync(page, current, shared, "Single activity rename", null));
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal("Single activity rename", await verify.CatalogueItems.Where(value => value.Id == item.Id).Select(value => value.Name).SingleAsync());
    }

    private async Task<(BossActivity Boss, SourceDrop Drop)> AddSharedActivityAsync(Guid itemId, string slugRoot)
    {
        var now = DateTimeOffset.UtcNow;
        var boss = new BossActivity(Guid.NewGuid(), $"{slugRoot} activity", $"{slugRoot}-{Guid.NewGuid():N}", "Boss", 10m, now);
        var drop = new SourceDrop(Guid.NewGuid(), boss.Id, itemId, "1/200", .005m, 20m, now);
        await using var setup = new ApplicationDbContext(options);
        setup.AddRange(boss, drop);
        await setup.SaveChangesAsync();
        return (boss, drop);
    }

    private static void ConfigureAdd(IndexModel page, BossActivity boss, CatalogueItem item, string imageUrl)
    {
        page.PageContext.HttpContext.RequestServices = new ServiceCollection().AddMvcCore().AddDataAnnotations().Services.BuildServiceProvider();
        page.BossDrop = new IndexModel.BossDropInput
        {
            BossActivityId = boss.Id,
            ItemName = item.Name,
            DisplayRate = "1/500",
            ImageUrl = imageUrl,
            UseExistingItem = true
        };
    }

    private static Task<IActionResult> UpdateDropAsync(IndexModel page, SourceDrop drop, CatalogueItem item, string name, string? imageUrl,
        string rate = "", Guid[]? confirmation = null)
    {
        var displayRate = string.IsNullOrEmpty(rate) ? drop.DisplayRate : rate;
        return page.OnPostUpdateDropAsync(drop.Id, drop.Version, item.Version, name, displayRate, drop.DisplayRate,
            null, null, default, false, null, 0, 0, null, null, imageUrl, false, CancellationToken.None, confirmation);
    }

    private static IndexModel.SharedItemActivity[] AssertAffected(IndexModel page, IReadOnlyCollection<BossActivity> expected)
    {
        var json = Assert.IsType<string>(page.TempData[IndexModel.SharedItemAffectedActivitiesTempDataKey]);
        var affected = JsonSerializer.Deserialize<IndexModel.SharedItemActivity[]>(json);
        Assert.NotNull(affected);
        Assert.Equal(expected.Select(value => value.Id).OrderBy(value => value), affected!.Select(value => value.Id).OrderBy(value => value));
        Assert.Equal(expected.Select(value => value.Name).OrderBy(value => value), affected.Select(value => value.Name).OrderBy(value => value));
        return affected;
    }

    private static void AssertNoCatalogueMutation(ApplicationDbContext db, Guid itemId, long itemVersion, int auditCount)
    {
        Assert.Equal(itemVersion, db.CatalogueItems.Where(value => value.Id == itemId).Select(value => value.Version).Single());
        Assert.Equal(auditCount, db.AuditEntries.Count());
    }
}
