using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Fact]
    public async Task B5Round2ApprovalPageListsEveryUnpricedItemExactly()
    {
        var fixture = await SeedApprovalBatchAsync();
        await using var db = new ApplicationDbContext(options);
        (await db.Boards.SingleAsync()).Resize(1, 2, 1);
        var item = new CatalogueItem(Guid.NewGuid(), "Second unpriced item", "SECOND UNPRICED ITEM");
        var drop = new SourceDrop(Guid.NewGuid(), fixture.Boss.Id, item.Id, "1/20", .05m, null, CompletionFixtureNow);
        var tile = new BoardTile(Guid.NewGuid(), fixture.Board.Id, fixture.Template.Id, 0, 1, "Second tile", "Second", "", 1);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 1, true, false, "Second", false);
        db.AddRange(item, drop, tile, requirement,
            new BoardRequirementBossSnapshot(Guid.NewGuid(), requirement.Id, fixture.Boss.Id, fixture.Boss.Name, fixture.Boss.EfficientCompletionsPerHour),
            new BoardRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, drop.Id, item.Id, fixture.Boss.Name, item.Name, "1/20", .05m, null, null));
        (await db.CatalogueItems.SingleAsync(x => x.Id == fixture.Item.Id)).SetPrice(null, CataloguePriceSource.Missing, null);
        await db.SaveChangesAsync();
        var baseline = await B5RemediationBoardPersistenceAsync();
        var page = Page(db, fixture.Admin.Id);
        await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
        Assert.IsType<RedirectToPageResult>(await page.OnPostApproveAsync(fixture.Event.Id, false, CancellationToken.None));
        Assert.Equal("These drops have no catalogue GP value: Batch item, Second unpriced item. Set a value in Admin Catalogue, then try again. An explicit 0 is valid.", page.TempData["StatusMessage"]);
        Assert.Equal(2, page.ValidationIssues.Count);
        var first = Assert.Single(page.ValidationIssues, x => x.Position == 0);
        Assert.Equal("item-price-missing", first.Code);
        Assert.Equal(fixture.Tile.Id, first.TileId);
        Assert.Equal("Batch item", Assert.Single(first.Arguments));
        var second = Assert.Single(page.ValidationIssues, x => x.Position == 1);
        Assert.Equal("item-price-missing", second.Code);
        Assert.Equal(tile.Id, second.TileId);
        Assert.Equal("Second unpriced item", Assert.Single(second.Arguments));
        Assert.Equal(baseline, await B5RemediationBoardPersistenceAsync());
        await AssertApprovalBatchUnchangedAsync(fixture);
    }
}
