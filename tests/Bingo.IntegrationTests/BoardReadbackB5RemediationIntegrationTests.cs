using System.Text.Json;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Auditing;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Fact]
    public async Task B5RemediationBoardCollectsDifferentTileIssuesAndEveryEmptyPosition()
    {
        var fixture = await SeedApprovalBatchAsync(missingEstimate: true);
        await using var db = new ApplicationDbContext(options);
        (await db.Boards.SingleAsync()).Resize(1, 4, 1);
        var template = new TileTemplate(Guid.NewGuid(), "Missing manual estimate", "Manual", ObjectiveType.Manual, "", null);
        var tile = new BoardTile(Guid.NewGuid(), fixture.Board.Id, template.Id, 0, 1, template.Name, template.Description, "", 0);
        db.AddRange(template, tile, new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 1, true, false, "Manual", true));
        await db.SaveChangesAsync();
        var page = Page(db, fixture.Admin.Id);
        await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
        var result = Assert.IsType<BoardModel.BoardActionState>(Assert.IsType<JsonResult>(await page.OnPostApproveStateAsync(fixture.Event.Id, false, CancellationToken.None)).Value);
        Assert.Equal(4, result.Issues.Count);
        Assert.Equal(fixture.Tile.Id, Assert.Single(result.Issues, x => x.Code == "catalogue-rates-missing" && x.Position == 0).TileId);
        Assert.Equal(tile.Id, Assert.Single(result.Issues, x => x.Code == "manual-ehb-missing" && x.Position == 1).TileId);
        Assert.Null(Assert.Single(result.Issues, x => x.Code == "board-incomplete" && x.Position == 2).TileId);
        Assert.Null(Assert.Single(result.Issues, x => x.Code == "board-incomplete" && x.Position == 3).TileId);
        Assert.All(result.Issues, x => Assert.False(string.IsNullOrWhiteSpace(x.ResourceKey)));
        await AssertApprovalBatchUnchangedAsync(fixture);
    }

    [Theory]
    [InlineData("missing", "board-not-found")]
    [InlineData("lease", "editing-control-required")]
    [InlineData("stale", "board-stale")]
    [InlineData("confirmation", "confirmation-required")]
    [InlineData("invalid", "approval-refused")]
    [InlineData("generic", "approval-failed")]
    public async Task B5RemediationBoardApprovalRefusalsAlwaysHaveIssues(string failure, string code)
    {
        var fixture = await SeedApprovalBatchAsync(correction: failure == "confirmation");
        var testOptions = failure is "invalid" or "generic"
            ? new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new ApprovalBatchFailure(failure == "generic")).Options
            : options;
        await using var db = new ApplicationDbContext(testOptions);
        if (failure == "lease")
        {
            (await db.Boards.SingleAsync()).ReleaseEditing(fixture.Admin.Id, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        var page = Page(db, fixture.Admin.Id);
        await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
        if (failure == "stale") page.BoardVersion--;
        var result = Assert.IsType<BoardModel.BoardActionState>(Assert.IsType<JsonResult>(await page.OnPostApproveStateAsync(failure == "missing" ? Guid.NewGuid() : fixture.Event.Id, false, CancellationToken.None)).Value);
        Assert.Equal(code, Assert.Single(result.Issues).Code);
        await AssertApprovalBatchUnchangedAsync(fixture);
    }
    [Theory]
    [InlineData("missing", "board-not-found")]
    [InlineData("confirmation", "confirmation-required")]
    [InlineData("stale", "publication-conflict")]
    [InlineData("roster", "roster-unpublished")]
    [InlineData("started", "event-already-started")]
    [InlineData("ended", "event-end-passed")]
    [InlineData("approval", "approval-unavailable")]
    [InlineData("price", "item-price-missing")]
    [InlineData("invalid", "publication-refused")]
    [InlineData("generic", "publication-failed")]
    public async Task B5RemediationPublicationRefusalsHaveIssuesAndNoWrites(string failure, string code)
    {
        var fixture = await SeedApprovalBatchAsync();
        await using (var prepare = new ApplicationDbContext(options))
        {
            prepare.Entry(await prepare.Events.SingleAsync()).Property(x => x.State).CurrentValue = EventState.SignupClosed;
            await prepare.SaveChangesAsync();
        }
        await AddPublicBoardTeamAsync(fixture);
        await using (var prepare = new ApplicationDbContext(options))
        {
            var page = Page(prepare, fixture.Admin.Id);
            await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
            await page.OnPostApproveAsync(fixture.Event.Id, false, CancellationToken.None);
            Assert.Equal(BoardState.Validated, (await prepare.Boards.SingleAsync()).State);
            var ev = await prepare.Events.SingleAsync();
            prepare.Entry(ev).Property(x => x.EventEndsAt).CurrentValue = DateTimeOffset.UtcNow.AddDays(failure == "ended" ? -1 : 3);
            if (failure == "started") prepare.Entry(ev).Property(x => x.ActualStartedAt).CurrentValue = CompletionFixtureNow;
            if (failure == "roster") (await prepare.DraftPublicationCycles.SingleAsync()).Supersede(CompletionFixtureNow, fixture.Admin.Id, "Controlled unpublished roster");
            if (failure == "approval") (await prepare.Boards.SingleAsync()).Unapprove();
            if (failure == "price") (await prepare.CatalogueItems.SingleAsync(x => x.Id == fixture.Item.Id)).SetPrice(null, CataloguePriceSource.Missing, null);
            await prepare.SaveChangesAsync();
        }
        var baseline = await B5RemediationBoardPersistenceAsync();
        var testOptions = failure is "invalid" or "generic"
            ? new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new B5PublicationFailure(failure == "generic")).Options : options;
        await using var db = new ApplicationDbContext(testOptions);
        var actor = Page(db, fixture.Admin.Id);
        await actor.OnGetAsync(fixture.Event.Id, CancellationToken.None);
        if (failure == "stale") actor.BoardVersion--;
        var response = Assert.IsType<BoardModel.BoardActionState>(Assert.IsType<JsonResult>(await actor.OnPostPublishStateAsync(failure == "missing" ? Guid.NewGuid() : fixture.Event.Id, failure != "confirmation", CancellationToken.None)).Value);
        var issue = Assert.Single(response.Issues);
        Assert.Equal(code, issue.Code);
        Assert.Equal(failure == "price" ? fixture.Tile.Id : null, issue.TileId);
        Assert.Equal(failure == "price" ? 0 : (int?)null, issue.Position);
        Assert.Equal(baseline, await B5RemediationBoardPersistenceAsync());
    }

    [Fact]
    public async Task B5RemediationApprovalMissingPricesTargetEveryAffectedTile()
    {
        var fixture = await SeedApprovalBatchAsync();
        await using var db = new ApplicationDbContext(options);
        (await db.Boards.SingleAsync()).Resize(1, 2, 1);
        var tile = new BoardTile(Guid.NewGuid(), fixture.Board.Id, fixture.Template.Id, 0, 1, "Second tile", "Second", "", 1);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 1, true, false, "Second", false);
        db.AddRange(tile, requirement,
            new BoardRequirementBossSnapshot(Guid.NewGuid(), requirement.Id, fixture.Boss.Id, fixture.Boss.Name, fixture.Boss.EfficientCompletionsPerHour),
            new BoardRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, fixture.Drop.Id, fixture.Item.Id, fixture.Boss.Name, fixture.Item.Name, "1/10", .1m, null, null));
        (await db.CatalogueItems.SingleAsync(x => x.Id == fixture.Item.Id)).SetPrice(null, CataloguePriceSource.Missing, null);
        await db.SaveChangesAsync();
        var page = Page(db, fixture.Admin.Id);
        await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
        var response = Assert.IsType<BoardModel.BoardActionState>(Assert.IsType<JsonResult>(await page.OnPostApproveStateAsync(fixture.Event.Id, false, CancellationToken.None)).Value);
        Assert.Equal(2, response.Issues.Count);
        Assert.All(response.Issues, x => { Assert.Equal("item-price-missing", x.Code); Assert.Contains(fixture.Item.Name, Assert.Single(x.Arguments).ToString(), StringComparison.Ordinal); });
        Assert.Equal(fixture.Tile.Id, Assert.Single(response.Issues, x => x.Position == 0).TileId);
        Assert.Equal(tile.Id, Assert.Single(response.Issues, x => x.Position == 1).TileId);
        await AssertApprovalBatchUnchangedAsync(fixture);
    }

    private async Task<string> B5RemediationBoardPersistenceAsync()
    {
        await using var db = new ApplicationDbContext(options);
        return JsonSerializer.Serialize(new
        {
            Boards = await db.Boards.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            Events = await db.Events.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            Audits = await db.AuditEntries.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            Approvals = await db.BoardApprovalSnapshots.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            Prices = await db.EventItemPrices.AsNoTracking().OrderBy(x => x.EventId).ThenBy(x => x.ItemId).ToListAsync()
        });
    }

    private sealed class B5PublicationFailure(bool generic) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AuditEntry>().Any(x => x.State == EntityState.Added && x.Entity.Action == "board.published"))
                throw generic ? new DbUpdateException("Controlled publication failure") : new InvalidOperationException("Controlled publication refusal");
            return ValueTask.FromResult(result);
        }
    }

}
