using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("zero")]
    [InlineData("objective")]
    public async Task StatsPass2TilePriceGateChecksEveryDropAndAcceptsZeroOrNoDrops(string scenario)
    {
        var (actor, item, boss, drops, eventId) = await PriceBoardFixtureAsync();
        if (scenario != "zero") await SetBoardFixturePriceAsync(item.Id, null);
        await using (var db = new ApplicationDbContext(options))
        {
            var page = Page(db, actor.Id);
            page.BoardVersion = 1;
            page.TileDraft = PriceTileInput(boss.Id, drops, scenario == "objective");
            Assert.IsType<RedirectToPageResult>(await page.OnPostCreateTileAsync(eventId, default));
            Assert.Equal(scenario == "missing" ? "failed" : "committed", page.TempData["BoardTileOutcome"]);
            if (scenario == "missing")
            {
                Assert.Contains(item.Name, (string)page.TempData["StatusMessage"]!);
                Assert.Contains("Admin Catalogue", (string)page.TempData["StatusMessage"]!);
            }
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(scenario == "missing" ? 0 : 1, await verify.BoardTiles.CountAsync());
        Assert.Equal(scenario == "missing" ? 0 : 1, await verify.AuditEntries.CountAsync(x => x.Action == "board.tile_created"));
        if (scenario != "missing")
        {
            await ApprovePriceBoardAsync(eventId, actor.Id);
            await PublishPriceBoardAsync(eventId, actor.Id);
            Assert.Equal(BoardState.Published, await verify.Boards.Where(x => x.EventId == eventId).Select(x => x.State).SingleAsync());
            if (scenario == "objective") Assert.Empty(await verify.BoardApprovalRequirementDropSnapshots.ToListAsync());
            await StartPriceBoardAsync(eventId);
            Assert.Equal(EventState.Live, await verify.Events.Where(x => x.Id == eventId).Select(x => x.State).SingleAsync());
        }
    }

    [Theory]
    [InlineData("edit")]
    [InlineData("approve")]
    [InlineData("publish")]
    public async Task StatsPass2LaterMissingCataloguePriceCannotBypassMutationGate(string boundary)
    {
        var (actor, item, boss, drops, eventId) = await PriceBoardFixtureAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            var page = Page(db, actor.Id); page.BoardVersion = 1; page.TileDraft = PriceTileInput(boss.Id, drops);
            await page.OnPostCreateTileAsync(eventId, default);
            Assert.Equal("committed", page.TempData["BoardTileOutcome"]);
        }
        if (boundary == "publish") await ApprovePriceBoardAsync(eventId, actor.Id);
        await SetBoardFixturePriceAsync(item.Id, null);
        await using (var db = new ApplicationDbContext(options))
        {
            var page = Page(db, actor.Id);
            page.BoardVersion = await db.Boards.Where(x => x.EventId == eventId).Select(x => x.Version).SingleAsync();
            if (boundary == "edit")
            {
                page.TileDraft = PriceTileInput(boss.Id, drops);
                page.TileDraft.TileId = await db.BoardTiles.Select(x => x.Id).SingleAsync();
                page.TileDraft.Name = "Rejected edit";
                await page.OnPostEditTileAsync(eventId, default);
            }
            else if (boundary == "approve")
            {
                page.ApprovalCatalogueFingerprint = (await LoadBoardAsync(eventId, actor.Id)).ApprovalCatalogueFingerprint;
                await page.OnPostApproveAsync(eventId, false, default);
            }
            else await page.OnPostPublishAsync(eventId, true, default);
            Assert.Contains(item.Name, (string)page.TempData["StatusMessage"]!);
            Assert.Contains("Admin Catalogue", (string)page.TempData["StatusMessage"]!);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(boundary == "publish" ? BoardState.Validated : BoardState.Draft,
            await verify.Boards.Where(x => x.EventId == eventId).Select(x => x.State).SingleAsync());
        Assert.NotEqual("Rejected edit", await verify.BoardTiles.Select(x => x.NameSnapshot).SingleAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.Action == "board.tile_edited" || x.Action == "board.published").ToListAsync());
        if (boundary != "publish") Assert.Empty(await verify.BoardApprovalSnapshots.ToListAsync());
        await SetBoardFixturePriceAsync(item.Id, 0);
        if (boundary != "publish") await ApprovePriceBoardAsync(eventId, actor.Id);
        await PublishPriceBoardAsync(eventId, actor.Id);
        Assert.Equal(BoardState.Published, await verify.Boards.Where(x => x.EventId == eventId).Select(x => x.State).SingleAsync());
    }

    [Theory]
    [InlineData(false, 250L)]
    [InlineData(true, 0L)]
    public async Task StatsPass2LateIntroductionUsesStoredValueAndKeepsExistingPrices(bool existedUnpriced, long value)
    {
        var (actor, item, boss, drops, eventId) = await PriceBoardFixtureAsync();
        var late = new CatalogueItem(Guid.NewGuid(), "Late item", "LATE ITEM");
        late.ConfigureApi("4151"); // an available historical mapping must not cause any late HTTP fetch
        if (existedUnpriced)
        {
            await using var before = new ApplicationDbContext(options); before.Add(late); await before.SaveChangesAsync();
        }
        await CreatePriceBoardTileAsync(eventId, actor.Id, boss.Id, drops);
        await ApprovePriceBoardAsync(eventId, actor.Id); await PublishPriceBoardAsync(eventId, actor.Id); await StartPriceBoardAsync(eventId);
        var introducedAt = DateTimeOffset.UtcNow;
        introducedAt = introducedAt.AddTicks(-(introducedAt.Ticks % TimeSpan.TicksPerMicrosecond));
        var lateDrop = new SourceDrop(Guid.NewGuid(), boss.Id, late.Id, "1/20", .05m, 2, introducedAt);
        await using (var catalogue = new ApplicationDbContext(options))
        {
            if (existedUnpriced) late = await catalogue.CatalogueItems.SingleAsync(x => x.Id == late.Id);
            else catalogue.Add(late);
            late.SetPrice(value, CataloguePriceSource.Manual, introducedAt);
            catalogue.Add(lateDrop);
            // Existing frozen zero remains sufficient even after the catalogue value is cleared.
            (await catalogue.CatalogueItems.SingleAsync(x => x.Id == item.Id)).SetPrice(null, CataloguePriceSource.Missing, null);
            await catalogue.SaveChangesAsync();
        }
        var oldPrice = await PriceRowAsync(eventId, item.Id);
        await PreparePriceCorrectionAsync(eventId, actor.Id, boss.Id, [.. drops, lateDrop.Id]);
        await using (var beforeApproval = new ApplicationDbContext(options))
            Assert.False(await beforeApproval.EventItemPrices.AnyAsync(x => x.EventId == eventId && x.ItemId == late.Id));
        await ApprovePriceCorrectionAsync(eventId, actor.Id);
        var price = await PriceRowAsync(eventId, late.Id);
        Assert.Equal(value, price.ValueGp); Assert.Equal(EventItemPriceSource.CatalogueIntroduction, price.Source);
        Assert.Equal(introducedAt, price.PriceObservedAt); Assert.True(price.CapturedAt >= introducedAt);
        Assert.Equal(oldPrice.SelectedHour, price.SelectedHour); Assert.Null(price.FallbackReason);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(oldPrice), System.Text.Json.JsonSerializer.Serialize(await PriceRowAsync(eventId, item.Id)));
        await SetBoardFixturePriceAsync(late.Id, 999);
        await PreparePriceCorrectionAsync(eventId, actor.Id, boss.Id, [.. drops, lateDrop.Id]);
        await ApprovePriceCorrectionAsync(eventId, actor.Id);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(price), System.Text.Json.JsonSerializer.Serialize(await PriceRowAsync(eventId, late.Id)));
    }

    [Fact]
    public async Task StatsPass2ConcurrentIntroductionAndRollbackPreservePublishedSnapshot()
    {
        var (actor, _, boss, drops, eventId) = await PriceBoardFixtureAsync();
        await CreatePriceBoardTileAsync(eventId, actor.Id, boss.Id, drops);
        await ApprovePriceBoardAsync(eventId, actor.Id); await PublishPriceBoardAsync(eventId, actor.Id); await StartPriceBoardAsync(eventId);
        var late = new CatalogueItem(Guid.NewGuid(), "Concurrent late", "CONCURRENT LATE"); late.SetPrice(123, CataloguePriceSource.Manual, DateTimeOffset.UtcNow);
        var lateDrop = new SourceDrop(Guid.NewGuid(), boss.Id, late.Id, "1/20", .05m, 2, DateTimeOffset.UtcNow);
        await using (var setup = new ApplicationDbContext(options)) { setup.AddRange(late, lateDrop); await setup.SaveChangesAsync(); }
        await PreparePriceCorrectionAsync(eventId, actor.Id, boss.Id, [.. drops, lateDrop.Id]);
        var displayed = await LoadBoardAsync(eventId, actor.Id);
        Guid? prior;
        await using (var baseline = new ApplicationDbContext(options)) prior = await baseline.Boards.Select(x => x.ActiveApprovalSnapshotId).SingleAsync();
        var failOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new RejectIntroducedPriceSave()).Options;
        await using (var failure = new ApplicationDbContext(failOptions))
        {
            var page = Page(failure, actor.Id); page.BoardVersion = displayed.BoardView!.Version; page.ApprovalCatalogueFingerprint = displayed.ApprovalCatalogueFingerprint;
            await page.OnPostApproveAsync(eventId, true, default);
        }
        await using (var afterFailure = new ApplicationDbContext(options))
        {
            Assert.Equal(prior, await afterFailure.Boards.Select(x => x.ActiveApprovalSnapshotId).SingleAsync());
            Assert.False(await afterFailure.EventItemPrices.AnyAsync(x => x.ItemId == late.Id));
            Assert.Empty(await afterFailure.AuditEntries.Where(x => x.Action == "board.published_corrected").ToListAsync());
        }
        async Task Approve()
        {
            await using var db = new ApplicationDbContext(options);
            var page = Page(db, actor.Id); page.BoardVersion = displayed.BoardView!.Version; page.ApprovalCatalogueFingerprint = displayed.ApprovalCatalogueFingerprint;
            await page.OnPostApproveAsync(eventId, true, default);
        }
        await Task.WhenAll(Approve(), Approve());
        await using var verify = new ApplicationDbContext(options);
        Assert.Single(await verify.EventItemPrices.Where(x => x.ItemId == late.Id).ToListAsync());
        Assert.Equal(2, await verify.BoardApprovalSnapshots.CountAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.Action == "board.published_corrected").ToListAsync());
    }

    [Fact]
    public async Task StatsPass2LateMissingValueRejectsEditAndApprovalWithoutChangingFrozenPrices()
    {
        var (actor, _, boss, drops, eventId) = await PriceBoardFixtureAsync();
        await CreatePriceBoardTileAsync(eventId, actor.Id, boss.Id, drops);
        await ApprovePriceBoardAsync(eventId, actor.Id); await PublishPriceBoardAsync(eventId, actor.Id); await StartPriceBoardAsync(eventId);
        var late = new CatalogueItem(Guid.NewGuid(), "Missing late", "MISSING LATE");
        var lateDrop = new SourceDrop(Guid.NewGuid(), boss.Id, late.Id, "1/20", .05m, 2, DateTimeOffset.UtcNow);
        await using (var setup = new ApplicationDbContext(options)) { setup.AddRange(late, lateDrop); await setup.SaveChangesAsync(); }
        await using (var correction = new ApplicationDbContext(options)) await Page(correction, actor.Id).OnPostCorrectPublishedAsync(eventId, true, "Introduce item", default);
        await using (var edit = new ApplicationDbContext(options))
        {
            var page = Page(edit, actor.Id); page.BoardVersion = await edit.Boards.Select(x => x.Version).SingleAsync();
            page.TileDraft = PriceTileInput(boss.Id, [.. drops, lateDrop.Id]); page.TileDraft.TileId = await edit.BoardTiles.Select(x => x.Id).SingleAsync();
            await page.OnPostEditTileAsync(eventId, default);
            Assert.Equal("failed", page.TempData["BoardTileOutcome"]); Assert.Contains(late.Name, (string)page.TempData["StatusMessage"]!);
        }
        await SetBoardFixturePriceAsync(late.Id, 42);
        // The correction is already open; prepare/edit via its existing workspace.
        await EditPriceCorrectionAsync(eventId, actor.Id, boss.Id, [.. drops, lateDrop.Id]);
        await SetBoardFixturePriceAsync(late.Id, null);
        await using (var approval = new ApplicationDbContext(options))
        {
            var page = Page(approval, actor.Id); page.BoardVersion = await approval.Boards.Select(x => x.Version).SingleAsync();
            page.ApprovalCatalogueFingerprint = (await LoadBoardAsync(eventId, actor.Id)).ApprovalCatalogueFingerprint;
            await page.OnPostApproveAsync(eventId, true, default);
            Assert.Contains(late.Name, (string)page.TempData["StatusMessage"]!);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(2, await verify.EventItemPrices.CountAsync()); Assert.Single(await verify.BoardApprovalSnapshots.ToListAsync());
        Assert.False(await verify.EventItemPrices.AnyAsync(x => x.ItemId == late.Id));
    }

    [Fact]
    public async Task StatsPass2MissingPriceAfterPublicationBlocksStartAndZeroRecovers()
    {
        var (actor, item, boss, drops, eventId) = await PriceBoardFixtureAsync();
        await CreatePriceBoardTileAsync(eventId, actor.Id, boss.Id, drops);
        await ApprovePriceBoardAsync(eventId, actor.Id); await PublishPriceBoardAsync(eventId, actor.Id);
        await SetBoardFixturePriceAsync(item.Id, null);
        await using (var db = new ApplicationDbContext(options))
        {
            var service = new EventLifecycleService(db, null!, TimeProvider.System);
            var version = await db.Events.Where(x => x.Id == eventId).Select(x => x.Version).SingleAsync();
            var result = await service.StartNowAsync(eventId, version, true, "Start", new(Guid.NewGuid(), "admin"));
            Assert.False(result.Succeeded); Assert.Contains(result.Blockers!, x => x.Code == "DROP_PRICE_MISSING");
            Assert.Empty(await db.EventItemPrices.ToListAsync());
            Assert.Null(await db.Events.Where(x => x.Id == eventId).Select(x => x.ActualStartedAt).SingleAsync());
        }
        await SetBoardFixturePriceAsync(item.Id, 0); await StartPriceBoardAsync(eventId);
    }

    [Fact]
    public async Task StatsPass2ConcurrentCatalogueChangeDuringTileGateReturnsRecoveryWithoutResidue()
    {
        var (actor, item, boss, drops, eventId) = await PriceBoardFixtureAsync();
        var barrier = new BeforePriceLock(async () => await SetBoardFixturePriceAsync(item.Id, null));
        var concurrentOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(barrier).Options;
        await using (var db = new ApplicationDbContext(concurrentOptions))
        {
            var page = Page(db, actor.Id); page.BoardVersion = 1; page.TileDraft = PriceTileInput(boss.Id, drops);
            await page.OnPostCreateTileAsync(eventId, default);
            Assert.Equal("failed", page.TempData["BoardTileOutcome"]);
            Assert.Contains("reload", (string)page.TempData["StatusMessage"]!, StringComparison.OrdinalIgnoreCase);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Empty(await verify.BoardTiles.ToListAsync()); Assert.Empty(await verify.AuditEntries.ToListAsync());
    }

    private sealed class BeforePriceLock(Func<Task> before) : DbCommandInterceptor
    {
        private bool entered;
        public override async ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(System.Data.Common.DbCommand command, CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (!entered && command.CommandText.Contains("SELECT * FROM catalogue_items", StringComparison.Ordinal))
            {
                entered = true; await before();
            }
            return result;
        }
    }

    private async Task CreatePriceBoardTileAsync(Guid eventId, Guid actorId, Guid bossId, Guid[] drops)
    {
        await using var db = new ApplicationDbContext(options); var page = Page(db, actorId);
        page.BoardVersion = await db.Boards.Select(x => x.Version).SingleAsync(); page.TileDraft = PriceTileInput(bossId, drops);
        await page.OnPostCreateTileAsync(eventId, default); Assert.Equal("committed", page.TempData["BoardTileOutcome"]);
    }
    private async Task StartPriceBoardAsync(Guid eventId)
    {
        await using var db = new ApplicationDbContext(options);
        var version = await db.Events.Where(x => x.Id == eventId).Select(x => x.Version).SingleAsync();
        var result = await new EventLifecycleService(db, null!, TimeProvider.System).StartNowAsync(eventId, version, true, "Early price fixture start", new(Guid.NewGuid(), "admin"));
        Assert.True(result.Succeeded, result.Error);
    }
    private async Task PreparePriceCorrectionAsync(Guid eventId, Guid actorId, Guid bossId, Guid[] drops)
    {
        await using (var db = new ApplicationDbContext(options)) await Page(db, actorId).OnPostCorrectPublishedAsync(eventId, true, "Introduce item", default);
        await EditPriceCorrectionAsync(eventId, actorId, bossId, drops);
    }
    private async Task EditPriceCorrectionAsync(Guid eventId, Guid actorId, Guid bossId, Guid[] drops)
    {
        await using var db = new ApplicationDbContext(options); var page = Page(db, actorId);
        page.BoardVersion = await db.Boards.Select(x => x.Version).SingleAsync(); page.TileDraft = PriceTileInput(bossId, drops);
        page.TileDraft.TileId = await db.BoardTiles.Select(x => x.Id).SingleAsync();
        await page.OnPostEditTileAsync(eventId, default); Assert.Equal("committed", page.TempData["BoardTileOutcome"]);
    }
    private async Task ApprovePriceCorrectionAsync(Guid eventId, Guid actorId)
    {
        await using var db = new ApplicationDbContext(options); var page = Page(db, actorId);
        page.BoardVersion = await db.Boards.Select(x => x.Version).SingleAsync();
        page.ApprovalCatalogueFingerprint = (await LoadBoardAsync(eventId, actorId)).ApprovalCatalogueFingerprint;
        await page.OnPostApproveAsync(eventId, true, default);
        Assert.False(await db.Boards.AsNoTracking().Select(x => x.PublishedCorrectionInProgress).SingleAsync());
    }
    private async Task<EventItemPrice> PriceRowAsync(Guid eventId, Guid itemId)
    {
        await using var db = new ApplicationDbContext(options);
        return await db.EventItemPrices.SingleAsync(x => x.EventId == eventId && x.ItemId == itemId);
    }
    private sealed class RejectIntroducedPriceSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<EventItemPrice>().Any(x => x.State == EntityState.Added)) throw new DbUpdateException("Controlled introduction failure");
            return ValueTask.FromResult(result);
        }
    }

    private async Task<(Account Actor, CatalogueItem Item, BossActivity Boss, Guid[] Drops, Guid EventId)> PriceBoardFixtureAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var actor = Website($"price-board-{Guid.NewGuid():N}", now); actor.SetGlobalRole(GlobalRole.SuperAdmin);
        var item = new CatalogueItem(Guid.NewGuid(), "Price gate item", "PRICE GATE ITEM"); item.SetPrice(0, CataloguePriceSource.Manual, now);
        var other = new CatalogueItem(Guid.NewGuid(), "Priced companion", "PRICED COMPANION"); other.SetPrice(100, CataloguePriceSource.Manual, now);
        var boss = new BossActivity(Guid.NewGuid(), "Price gate source", "price-gate-source", "Boss", 10, now);
        var first = new SourceDrop(Guid.NewGuid(), boss.Id, other.Id, "1/10", .1m, 1, now);
        var second = new SourceDrop(Guid.NewGuid(), boss.Id, item.Id, "1/10", .1m, 1, now);
        var e = new BingoEvent(Guid.NewGuid(), "Price board", $"price-board-{Guid.NewGuid():N}", "UTC", actor.Id, now);
        e.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddHours(2), now.AddDays(2), 10);
        e.OpenSignups(now.AddDays(-2)); e.CloseSignups(now.AddDays(-1)); e.SetDraftRosterPublication(true);
        var draft = new DraftSession(Guid.NewGuid(), e.Id, 1); draft.Start(now); draft.Finalize(now);
        var board = new Board(Guid.NewGuid(), e.Id, "Price board", 1, 1); board.AcquireEditing(actor.Id, now, TimeSpan.FromMinutes(30));
        await using var db = new ApplicationDbContext(options);
        db.AddRange(actor, item, other, boss, first, second, e, draft, board); await db.SaveChangesAsync();
        return (actor, item, boss, [first.Id, second.Id], e.Id);
    }

    private static BoardModel.TileDraftInput PriceTileInput(Guid bossId, Guid[] dropIds, bool objective = false) => new()
    {
        Position = 0,
        Name = "Price tile",
        ManualEhb = objective ? 2 : null,
        Requirements = [new() { Kind = objective ? "challenge" : "drops", Description = "Complete objective", Target = 1,
            BossIds = objective ? [] : [bossId], DropIds = objective ? [] : dropIds.ToList() }]
    };

    private async Task SetBoardFixturePriceAsync(Guid itemId, long? value)
    {
        await using var db = new ApplicationDbContext(options);
        (await db.CatalogueItems.SingleAsync(x => x.Id == itemId)).SetPrice(value, value is null ? CataloguePriceSource.Missing : CataloguePriceSource.Manual, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    private async Task ApprovePriceBoardAsync(Guid eventId, Guid actorId)
    {
        await using var db = new ApplicationDbContext(options);
        var page = Page(db, actorId);
        page.BoardVersion = await db.Boards.Where(x => x.EventId == eventId).Select(x => x.Version).SingleAsync();
        page.ApprovalCatalogueFingerprint = (await LoadBoardAsync(eventId, actorId)).ApprovalCatalogueFingerprint;
        await page.OnPostApproveAsync(eventId, false, default);
        Assert.Equal(BoardState.Validated, await db.Boards.AsNoTracking().Where(x => x.EventId == eventId).Select(x => x.State).SingleAsync());
    }

    private async Task PublishPriceBoardAsync(Guid eventId, Guid actorId)
    {
        await using var db = new ApplicationDbContext(options);
        var page = Page(db, actorId);
        page.BoardVersion = await db.Boards.Where(x => x.EventId == eventId).Select(x => x.Version).SingleAsync();
        await page.OnPostPublishAsync(eventId, true, default);
        Assert.Equal(BoardState.Published, await db.Boards.AsNoTracking().Where(x => x.EventId == eventId).Select(x => x.State).SingleAsync());
    }
}
