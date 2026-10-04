using Bingo.Domain.Access;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Catalogue;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Fact]
    public async Task Au23RateTextControlsRollCountForOrdinaryAdminAddEditAndReactivation()
    {
        var (actor, boss, item, drop) = await PriceFixtureAsync();
        await using (var role = new ApplicationDbContext(options))
        {
            role.Accounts.Single(account => account.Id == actor.Id).SetGlobalRole(GlobalRole.Admin);
            await role.SaveChangesAsync();
        }

        var addedBoss = new BossActivity(Guid.NewGuid(), $"Added {Guid.NewGuid():N}", $"added-{Guid.NewGuid():N}", "Boss", 10m, DateTimeOffset.UtcNow);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.BossActivities.Add(addedBoss);
            await setup.SaveChangesAsync();
        }

        await using (var add = new ApplicationDbContext(options))
        {
            var page = CataloguePage(add, actor.Id, false);
            page.PageContext.HttpContext.RequestServices = ValidationServices();
            page.BossDrop = new IndexModel.BossDropInput
            {
                BossActivityId = addedBoss.Id,
                ItemName = $"New rate item {Guid.NewGuid():N}",
                DisplayRate = "3/1024",
                InitialValueGp = 0
            };

            Assert.IsType<RedirectToPageResult>(await page.OnPostBossDropAsync(CancellationToken.None));
        }

        await using (var verifyAdded = new ApplicationDbContext(options))
        {
            var added = await verifyAdded.SourceDrops.SingleAsync(value => value.BossActivityId == addedBoss.Id);
            Assert.Equal(1, added.RollsPerCompletion);
            Assert.Equal(3m / 1024m, added.NumericProbability);
            Assert.Equal("default", added.RollGroup);
        }

        await using (var edit = new ApplicationDbContext(options))
        {
            var current = await edit.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await edit.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            var page = CataloguePage(edit, actor.Id, false);
            Assert.IsType<RedirectToPageResult>(await page.OnPostUpdateDropAsync(
                current.Id, current.Version, shared.Version, shared.Name, "3 x 1/1024", current.DisplayRate,
                null, null, default, false, null, 0, 0, null, null, null, false, CancellationToken.None));
        }

        await using (var verifyThree = new ApplicationDbContext(options))
        {
            var edited = await verifyThree.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            Assert.Equal(3, edited.RollsPerCompletion);
            Assert.Equal(1m / 1024m, edited.NumericProbability);
        }

        await using (var editBack = new ApplicationDbContext(options))
        {
            var current = await editBack.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await editBack.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            var page = CataloguePage(editBack, actor.Id, false);
            Assert.IsType<RedirectToPageResult>(await page.OnPostUpdateDropAsync(
                current.Id, current.Version, shared.Version, shared.Name, "3/1024", current.DisplayRate,
                null, null, default, false, null, 0, 0, null, null, null, false, CancellationToken.None));
        }

        await using (var deactivate = new ApplicationDbContext(options))
        {
            var current = await deactivate.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            current.SetActive(false);
            await deactivate.SaveChangesAsync();
        }

        await using (var reactivate = new ApplicationDbContext(options))
        {
            var page = CataloguePage(reactivate, actor.Id, false);
            page.BossDrop = new IndexModel.BossDropInput
            {
                BossActivityId = boss.Id,
                ItemName = item.Name,
                DisplayRate = "3 x 1/1024",
                UseExistingItem = true
            };
            page.PageContext.HttpContext.RequestServices = ValidationServices();
            Assert.IsType<RedirectToPageResult>(await page.OnPostBossDropAsync(CancellationToken.None));
        }

        await using var verifyReactivated = new ApplicationDbContext(options);
        var reactivated = await verifyReactivated.SourceDrops.SingleAsync(value => value.Id == drop.Id);
        Assert.True(reactivated.Active);
        Assert.Equal(3, reactivated.RollsPerCompletion);
        Assert.Equal(1m / 1024m, reactivated.NumericProbability);
    }

    [Fact]
    public async Task Au23RollGroupChangeRequiresDatabaseSuperAdminAndPreservesExistingGroups()
    {
        var (actor, _, item, drop) = await PriceFixtureAsync();

        await using (var ordinaryRole = new ApplicationDbContext(options))
        {
            ordinaryRole.Accounts.Single(account => account.Id == actor.Id).SetGlobalRole(GlobalRole.Admin);
            await ordinaryRole.SaveChangesAsync();
        }

        await using (var refused = new ApplicationDbContext(options))
        {
            var current = await refused.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await refused.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            var page = CataloguePage(refused, actor.Id, false);
            SetRollGroupForm(page, "ordinary-change");
            Assert.IsType<RedirectToPageResult>(await page.OnPostUpdateDropAsync(
                current.Id, current.Version, shared.Version, shared.Name, current.DisplayRate, current.DisplayRate,
                null, null, default, false, null, 0, 0, "ordinary-change", null, null, false, CancellationToken.None));
            Assert.Contains("operator-managed", page.TempData["StatusMessage"]?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        await using (var verifyRefused = new ApplicationDbContext(options))
            Assert.Equal("synthetic", await verifyRefused.SourceDrops.Where(value => value.Id == drop.Id).Select(value => value.RollGroup).SingleAsync());

        await using (var allowed = new ApplicationDbContext(options))
        {
            allowed.Accounts.Single(account => account.Id == actor.Id).SetGlobalRole(GlobalRole.SuperAdmin);
            await allowed.SaveChangesAsync();
            Assert.Equal(GlobalRole.SuperAdmin, await allowed.Accounts.Where(account => account.Id == actor.Id).Select(account => account.GlobalRole).SingleAsync());
            var current = await allowed.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await allowed.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            var page = CataloguePage(allowed, actor.Id, true);
            SetRollGroupForm(page, "superadmin-change");
            Assert.IsType<RedirectToPageResult>(await page.OnPostUpdateDropAsync(
                current.Id, current.Version, shared.Version, shared.Name, current.DisplayRate, current.DisplayRate,
                null, null, default, false, null, 0, 0, "superadmin-change", null, null, false, CancellationToken.None));
            Assert.DoesNotContain("operator-managed", page.TempData["StatusMessage"]?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        await using (var verifyAllowed = new ApplicationDbContext(options))
            Assert.Equal("superadmin-change", await verifyAllowed.SourceDrops.Where(value => value.Id == drop.Id).Select(value => value.RollGroup).SingleAsync());

        await using (var demoted = new ApplicationDbContext(options))
        {
            demoted.Accounts.Single(account => account.Id == actor.Id).SetGlobalRole(GlobalRole.Admin);
            await demoted.SaveChangesAsync();
            var current = await demoted.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            var shared = await demoted.CatalogueItems.SingleAsync(value => value.Id == item.Id);
            var page = CataloguePage(demoted, actor.Id, true);
            SetRollGroupForm(page, "forged-claim-change");
            Assert.IsType<RedirectToPageResult>(await page.OnPostUpdateDropAsync(
                current.Id, current.Version, shared.Version, shared.Name, current.DisplayRate, current.DisplayRate,
                null, null, default, false, null, 0, 0, "forged-claim-change", null, null, false, CancellationToken.None));
            Assert.Contains("operator-managed", page.TempData["StatusMessage"]?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        await using var verifyDemoted = new ApplicationDbContext(options);
        Assert.Equal("superadmin-change", await verifyDemoted.SourceDrops.Where(value => value.Id == drop.Id).Select(value => value.RollGroup).SingleAsync());
    }

    private static ServiceProvider ValidationServices() => new ServiceCollection().AddMvcCore().AddDataAnnotations().Services.BuildServiceProvider();

    private static void SetRollGroupForm(IndexModel page, string group)
    {
        page.Request.ContentType = "application/x-www-form-urlencoded";
        page.Request.Form = new FormCollection(new Dictionary<string, StringValues> { ["rollGroup"] = group });
    }
}
