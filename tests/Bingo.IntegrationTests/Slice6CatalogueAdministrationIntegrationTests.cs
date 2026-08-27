using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Bingo.Application.Access;
using Bingo.Application.Boards;
using Bingo.Application.Catalogue;
using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Auditing;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Teams;
using Bingo.Web;
using Bingo.Web.Boards;
using Bingo.Web.Catalogue;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Testcontainers.PostgreSql;
using CatalogueIndexModel = Bingo.Web.Pages.Admin.Catalogue.IndexModel;

namespace Bingo.IntegrationTests;

public sealed class Slice6CatalogueAdministrationIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice6_catalogue_administration")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task CatalogueAdministrationEnforcesDeleteAuthorityAndRendersCallableIndependentLifecycleForms()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website("slice6-catalogue-admin", now); admin.SetGlobalRole(GlobalRole.Admin); SetPassword(admin, now);
        var superAdmin = Website("slice6-catalogue-owner", now); superAdmin.SetGlobalRole(GlobalRole.SuperAdmin); SetPassword(superAdmin, now);
        var dropItem = new CatalogueItem(Guid.NewGuid(), "Nid", "NID");
        var boss = new BossActivity(Guid.NewGuid(), "Araxxor", $"araxxor-{Guid.NewGuid():N}", "Boss", 10m, now);
        var drop = new SourceDrop(Guid.NewGuid(), boss.Id, dropItem.Id, "1/100", .01m, 10m, now);
        var destroyItem = new CatalogueItem(Guid.NewGuid(), "Nid (Destroy)", "NID (DESTROY)");
        var destroyDrop = new SourceDrop(Guid.NewGuid(), boss.Id, destroyItem.Id, "1/50", .02m, 5m, now);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, superAdmin, dropItem, destroyItem, boss, drop, destroyDrop);
            await setup.SaveChangesAsync();
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var superClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(adminClient, admin.LoginName);
        await LoginAsync(superClient, superAdmin.LoginName);

        var adminPage = await adminClient.GetStringAsync($"/Admin/Catalogue?bossId={boss.Id}");
        var dropForm = Regex.Match(adminPage, $"<form[^>]*id=\"drop-edit-{drop.Id}\".*?</form>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(dropForm);
        Assert.Contains($"drop-edit-{destroyDrop.Id}", adminPage, StringComparison.Ordinal);
        Assert.DoesNotContain("RateVariant", adminPage, StringComparison.Ordinal);
        Assert.DoesNotContain("<h2 id=\"catalogue-items-heading\">Items", adminPage, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=UpdateItem", adminPage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatalogueDeletionFeedbackDistinguishesReferencedFromStaleAndMultipleRollStorageMatchesBoardMath()
    {
        var now = DateTimeOffset.UtcNow;
        var owner = Website($"slice6-delete-owner-{Guid.NewGuid():N}", now); owner.SetGlobalRole(GlobalRole.SuperAdmin);
        var boss = new BossActivity(Guid.NewGuid(), "Zulrah", $"zulrah-{Guid.NewGuid():N}", "Boss", 10m, now);
        var referencedItem = new CatalogueItem(Guid.NewGuid(), "Referenced fang", "REFERENCED FANG");
        var referencedDrop = new SourceDrop(Guid.NewGuid(), boss.Id, referencedItem.Id, "1/100", .01m, 10m, now);
        var template = new TileTemplate(Guid.NewGuid(), "Reference", string.Empty, ObjectiveType.DropRequirements, string.Empty, null);
        var requirement = new TileTemplateRequirement(Guid.NewGuid(), template.Id, 1, 1, true, false, "Collect", false);
        var link = new TemplateRequirementDrop(Guid.NewGuid(), requirement.Id, referencedDrop.Id, null);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(owner, boss, referencedItem, referencedDrop, template, requirement, link);
            await setup.SaveChangesAsync();
        }

        await using (var referencedContext = new ApplicationDbContext(options))
        {
            var page = CataloguePage(referencedContext, owner.Id, superAdmin: true);
            Assert.IsType<RedirectToPageResult>(await page.OnPostDeleteAsync("drop", referencedDrop.Id, referencedDrop.Version, "DELETE", CancellationToken.None));
            Assert.Contains("referenced and cannot be permanently deleted", page.TempData["StatusMessage"]?.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.True(await referencedContext.SourceDrops.AnyAsync(x => x.Id == referencedDrop.Id));
        }

        await using (var staleContext = new ApplicationDbContext(options))
        {
            var page = CataloguePage(staleContext, owner.Id, superAdmin: true);
            Assert.IsType<RedirectToPageResult>(await page.OnPostDeleteAsync("drop", referencedDrop.Id, referencedDrop.Version + 1, "DELETE", CancellationToken.None));
            Assert.Contains("changed by another administrator", page.TempData["StatusMessage"]?.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.True(await staleContext.SourceDrops.AnyAsync(x => x.Id == referencedDrop.Id));
        }

        var parsedRate = DropRateParser.TryParse("2 x 1/1000");
        Assert.NotNull(parsedRate);
        var createdItem = new CatalogueItem(Guid.NewGuid(), "Zulrah two-roll drop", "ZULRAH TWO-ROLL DROP");
        var expectedProbability = 1m - (.999m * .999m);
        var created = new SourceDrop(Guid.NewGuid(), boss.Id, createdItem.Id, "2 x 1/1000", parsedRate.ProbabilityPerRoll,
            1m / (10m * expectedProbability), now);
        created.SetRateMechanics(DropProbabilityScope.Participant, conditionalOnParent: false, parentProbability: null,
            assumedParticipants: 1, rollsPerCompletion: parsedRate.RollsPerCompletion, rollGroup: "zulrah");
        await using (var creationContext = new ApplicationDbContext(options))
        {
            creationContext.AddRange(createdItem, created);
            await creationContext.SaveChangesAsync();
        }

        await using var verify = new ApplicationDbContext(options);
        var persisted = await verify.SourceDrops.SingleAsync(x => x.DisplayRate == "2 x 1/1000");
        Assert.Equal(2, persisted.RollsPerCompletion);
        Assert.Equal(.001m, persisted.NumericProbability);
        Assert.Equal(expectedProbability, persisted.EffectiveProbabilityPerCompletion());
        Assert.Equal(decimal.Round(EhbCalculator.CalculateDropRequirement(1,
            [new EligibleDropRate(10m, persisted.NumericProbability, persisted.ItemId, persisted.BossActivityId, RollsPerCompletion: persisted.RollsPerCompletion)])!.Value, 4),
            persisted.DefaultEhbEstimate);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ReviewedCatalogueImportPersistsEffectiveMultiplierEhbForCatalogueAndBoard(int rolls)
    {
        var now = DateTimeOffset.UtcNow;
        var boss = new BossActivity(Guid.NewGuid(), $"Imported boss {Guid.NewGuid():N}", $"imported-boss-{Guid.NewGuid():N}", "Boss", 10m, now);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.BossActivities.Add(boss);
            await setup.SaveChangesAsync();
        }

        var wikitext = $"{{{{DropsLine|name=Imported item|quantity=1|rarity=1/1000|rolls={rolls}}}}}";
        await using (var importContext = new ApplicationDbContext(options))
        {
            var importer = new OsrsWikiCatalogueDryRunService(new StubHttpClientFactory(wikitext), importContext);
            await importer.ApplyReviewedImportAsync();
        }

        await using var verify = new ApplicationDbContext(options);
        var persisted = await verify.SourceDrops.SingleAsync(x => x.BossActivityId == boss.Id);
        var persistedBoss = await verify.BossActivities.SingleAsync(x => x.Id == persisted.BossActivityId);
        var cataloguePage = CataloguePage(verify, Guid.NewGuid(), superAdmin: true);
        await cataloguePage.OnGetAsync(CancellationToken.None);
        var displayedEhb = cataloguePage.Drops.Single(x => x.Id == persisted.Id).DefaultEhb;
        var boardEhb = EhbCalculator.CalculateDropRequirement(1,
            [new EligibleDropRate(persistedBoss.EfficientCompletionsPerHour, persisted.NumericProbability,
                persisted.ItemId, persisted.BossActivityId, RollsPerCompletion: persisted.RollsPerCompletion)]);
        var expectedEhb = 1m / (10m * SourceDrop.CalculateProbabilityPerCompletion(.001m, rolls)!.Value);

        Assert.Equal(rolls, persisted.RollsPerCompletion);
        Assert.Equal(decimal.Round(expectedEhb, 4), displayedEhb);
        Assert.Equal(decimal.Round(expectedEhb, 4), decimal.Round(boardEhb!.Value, 4));
    }

    [Fact]
    public async Task DraftBoardUsesCurrentCatalogueRatesAndRejectsMismatchedManagedTileImages()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"slice6-board-admin-{Guid.NewGuid():N}", now);
        var firstEvent = new BingoEvent(Guid.NewGuid(), "First board", $"first-board-{Guid.NewGuid():N}", "UTC", admin.Id, now);
        var secondEvent = new BingoEvent(Guid.NewGuid(), "Second board", $"second-board-{Guid.NewGuid():N}", "UTC", admin.Id, now);
        var boss = new BossActivity(Guid.NewGuid(), "Current boss", "current-boss", "Boss", 10m, now);
        var item = new CatalogueItem(Guid.NewGuid(), "Current item", "CURRENT ITEM");
        var drop = new SourceDrop(Guid.NewGuid(), boss.Id, item.Id, "1/10", .1m, null, now);
        var template = new TileTemplate(Guid.NewGuid(), "Current tile", "", ObjectiveType.DropRequirements, string.Empty, null);
        var firstBoard = new Board(Guid.NewGuid(), firstEvent.Id, "Board", 1, 1);
        firstBoard.AcquireEditing(admin.Id, now, TimeSpan.FromMinutes(5));
        var secondBoard = new Board(Guid.NewGuid(), secondEvent.Id, "Other board", 1, 1);
        var firstTile = new BoardTile(Guid.NewGuid(), firstBoard.Id, template.Id, 0, 0, "Current tile", "", string.Empty, 1m);
        var otherTile = new BoardTile(Guid.NewGuid(), secondBoard.Id, Guid.NewGuid(), 0, 0, "Other tile", "", string.Empty, 1m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), firstTile.Id, 1, 1, true, false, "Collect", false);
        var snapshot = new BoardRequirementDropSnapshot(Guid.NewGuid(), requirement.Id, drop.Id, "Old boss", "Old item", "1/100", .01m, null, 10m, 1);
        var mismatchedImage = new BoardTileImageAsset(Guid.NewGuid(), secondEvent.Id, otherTile.Id, "other/image.png", "image.png", "image/png", 3, 1, 1, new string('a', 64), admin.Id, now);

        await using (var seed = new ApplicationDbContext(options))
        {
            seed.AddRange(admin, firstEvent, secondEvent, boss, item, drop, template, firstBoard, secondBoard, firstTile, otherTile, requirement, snapshot, mismatchedImage);
            await seed.SaveChangesAsync();
        }

        var initial = await LoadBoardAsync(firstEvent.Id, admin.Id);
        Assert.Equal(1m, initial.Tiles.Single().Ehb);
        Assert.Equal("Current boss", initial.TileEditors.Single().Requirements.Single().Drops.Single().BossName);
        Assert.Contains("1/10", initial.TileEditors.Single().Requirements.Single().Drops.Single().DisplayRate, StringComparison.Ordinal);

        await using (var catalogueChange = new ApplicationDbContext(options))
        {
            var currentBoss = await catalogueChange.BossActivities.SingleAsync(value => value.Id == boss.Id);
            var currentDrop = await catalogueChange.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            currentBoss.Update("Updated boss", "Boss", 20m, null, "manual", null, now.AddMinutes(1));
            currentDrop.Update("1/5", .2m, null, null, "manual", now.AddMinutes(1));
            await catalogueChange.SaveChangesAsync();
        }

        var updated = await LoadBoardAsync(firstEvent.Id, admin.Id);
        Assert.Equal(.25m, updated.Tiles.Single().Ehb);
        Assert.Equal("Updated boss", updated.TileEditors.Single().Requirements.Single().Drops.Single().BossName);
        Assert.Contains("1/5", updated.TileEditors.Single().Requirements.Single().Drops.Single().DisplayRate, StringComparison.Ordinal);

        await using (var approving = new ApplicationDbContext(options))
        {
            var page = Page(approving, admin.Id);
            page.BoardVersion = 1;
            Assert.IsType<RedirectToPageResult>(await page.OnPostApproveAsync(firstEvent.Id, CancellationToken.None));
        }
        await using (var approved = new ApplicationDbContext(options))
        {
            var frozenDrop = await approved.BoardApprovalRequirementDropSnapshots.SingleAsync();
            Assert.Equal(.2m, frozenDrop.NumericProbability);
            Assert.Equal("1/5", frozenDrop.DisplayRate);
        }

        await using (var laterCatalogueChange = new ApplicationDbContext(options))
        {
            var currentBoss = await laterCatalogueChange.BossActivities.SingleAsync(value => value.Id == boss.Id);
            var currentDrop = await laterCatalogueChange.SourceDrops.SingleAsync(value => value.Id == drop.Id);
            currentBoss.Update("Changed after approval", "Boss", 40m, null, "manual", null, now.AddMinutes(2));
            currentDrop.Update("1/2", .5m, null, null, "manual", now.AddMinutes(2));
            await laterCatalogueChange.SaveChangesAsync();
        }

        var approvedEditor = await LoadBoardAsync(firstEvent.Id, admin.Id);
        Assert.Equal(.25m, approvedEditor.Tiles.Single().Ehb);
        Assert.Equal("Updated boss", approvedEditor.TileEditors.Single().Requirements.Single().Drops.Single().BossName);
        Assert.Contains("1/5", approvedEditor.TileEditors.Single().Requirements.Single().Drops.Single().DisplayRate, StringComparison.Ordinal);

        await using var imageRequest = new ApplicationDbContext(options);
        var imagePage = Page(imageRequest, admin.Id);
        Assert.IsType<NotFoundResult>(await imagePage.OnGetTileImageAsync(firstEvent.Id, firstTile.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ResizeCompactsDraftTilesFromTheTopLeftInExistingOrder()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"slice6-resize-admin-{Guid.NewGuid():N}", now);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "Resize board", $"resize-board-{Guid.NewGuid():N}", "UTC", admin.Id, now);
        var board = new Board(Guid.NewGuid(), bingoEvent.Id, "Board", 2, 2);
        board.AcquireEditing(admin.Id, now, TimeSpan.FromMinutes(5));
        var tiles = new[]
        {
            new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Top left", "", "", 1m),
            new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 1, "Top right", "", "", 1m),
            new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 1, 0, "Bottom left", "", "", 1m)
        };
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.AddRange(admin, bingoEvent, board);
            seed.AddRange(tiles);
            await seed.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var page = Page(db, admin.Id);
            page.Rows = 1;
            page.Columns = 3;
            page.BoardVersion = 1;

            Assert.IsType<RedirectToPageResult>(await page.OnPostResizeAsync(bingoEvent.Id, CancellationToken.None));
        }

        await using var verify = new ApplicationDbContext(options);
        var compacted = await verify.BoardTiles.Where(tile => tile.BoardId == board.Id).OrderBy(tile => tile.ColumnIndex).ToListAsync();
        Assert.Collection(compacted,
            tile => Assert.Equal(("Top left", 0, 0), (tile.NameSnapshot, tile.RowIndex, tile.ColumnIndex)),
            tile => Assert.Equal(("Top right", 0, 1), (tile.NameSnapshot, tile.RowIndex, tile.ColumnIndex)),
            tile => Assert.Equal(("Bottom left", 0, 2), (tile.NameSnapshot, tile.RowIndex, tile.ColumnIndex)));
    }

    [Fact]
    public async Task TeamSizeChangeSucceedsWithTheRenderedBoardVersion()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"slice6-team-size-admin-{Guid.NewGuid():N}", now);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "Team size board", $"team-size-board-{Guid.NewGuid():N}", "UTC", admin.Id, now);
        var board = new Board(Guid.NewGuid(), bingoEvent.Id, "Board", 1, 1);
        board.AcquireEditing(admin.Id, now, TimeSpan.FromMinutes(5));

        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, bingoEvent, board);
            await setup.SaveChangesAsync();
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var page = Page(db, admin.Id);
            page.BoardVersion = board.Version;

            Assert.IsType<RedirectToPageResult>(await page.OnPostTeamSizeAsync(bingoEvent.Id, 7, CancellationToken.None));
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(7, await verify.Events.Where(value => value.Id == bingoEvent.Id).Select(value => value.ExpectedTeamSize).SingleAsync());
        Assert.Equal(2, await verify.Boards.Where(value => value.Id == board.Id).Select(value => value.Version).SingleAsync());
    }

    [Fact]
    public async Task PrivateApprovalFreezesOneSnapshotAndUnapprovalRetainsHistory()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"slice6-approval-admin-{Guid.NewGuid():N}", now);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "Approval board", $"approval-board-{Guid.NewGuid():N}", "UTC", admin.Id, now);
        var board = new Board(Guid.NewGuid(), bingoEvent.Id, "Board", 1, 1);
        board.AcquireEditing(admin.Id, now, TimeSpan.FromMinutes(5));
        var template = new TileTemplate(Guid.NewGuid(), "Manual tile", "Private description", ObjectiveType.Manual, string.Empty, 4m);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, template.Id, 0, 0, "Manual tile", "Private description", string.Empty, 4m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 1, true, false, "Complete the challenge", true);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, bingoEvent, board, template, tile, requirement);
            await setup.SaveChangesAsync();
        }

        await using (var approving = new ApplicationDbContext(options))
        {
            var page = Page(approving, admin.Id);
            page.BoardVersion = 1;
            Assert.IsType<RedirectToPageResult>(await page.OnPostApproveAsync(bingoEvent.Id, CancellationToken.None));
        }
        await using (var verify = new ApplicationDbContext(options))
        {
            var approved = await verify.Boards.SingleAsync(x => x.Id == board.Id);
            Assert.Equal(BoardState.Validated, approved.State);
            var approval = await verify.BoardApprovalSnapshots.SingleAsync(x => x.Id == approved.ActiveApprovalSnapshotId);
            Assert.Equal(admin.Id, approval.ApprovedByAccountId);
            Assert.Equal(BoardState.Validated, approval.LifecycleState);
            Assert.Equal(4m, approval.TotalEhbEstimate);
            Assert.Equal(1, await verify.BoardApprovalTileSnapshots.CountAsync(x => x.ApprovalSnapshotId == approval.Id));
            Assert.Single(await verify.AuditEntries.Where(x => x.Action == "board.approved" && x.EventId == bingoEvent.Id).ToListAsync());
            Assert.False(await verify.Events.Where(x => x.Id == bingoEvent.Id).Select(x => x.BoardPublished).SingleAsync());
        }

        await using (var unapproving = new ApplicationDbContext(options))
        {
            var page = Page(unapproving, admin.Id);
            page.BoardVersion = 2;
            Assert.IsType<RedirectToPageResult>(await page.OnPostUnapproveAsync(bingoEvent.Id, CancellationToken.None));
        }
        await using (var verify = new ApplicationDbContext(options))
        {
            var unapproved = await verify.Boards.SingleAsync(x => x.Id == board.Id);
            Assert.Equal(BoardState.Draft, unapproved.State);
            Assert.Null(unapproved.ActiveApprovalSnapshotId);
            Assert.Single(await verify.BoardApprovalSnapshots.Where(x => x.BoardId == board.Id).ToListAsync());
        }

        await using (var reapproving = new ApplicationDbContext(options))
        {
            var page = Page(reapproving, admin.Id);
            page.BoardVersion = 3;
            Assert.IsType<RedirectToPageResult>(await page.OnPostApproveAsync(bingoEvent.Id, CancellationToken.None));
        }
        await using (var editing = new ApplicationDbContext(options))
        {
            var page = Page(editing, admin.Id);
            page.BoardVersion = 4;
            Assert.IsType<RedirectToPageResult>(await page.OnPostRemoveAsync(bingoEvent.Id, tile.Id, CancellationToken.None));
            Assert.Equal(
                "Board returned to Draft because a competitive edit was saved. The preserved approval remains in history.",
                page.TempData["StatusMessage"]);
        }
        await using (var verify = new ApplicationDbContext(options))
        {
            var afterEdit = await verify.Boards.SingleAsync(x => x.Id == board.Id);
            Assert.Equal(BoardState.Draft, afterEdit.State);
            Assert.Null(afterEdit.ActiveApprovalSnapshotId);
            Assert.Equal(2, await verify.BoardApprovalSnapshots.CountAsync(x => x.BoardId == board.Id));
            Assert.Single(await verify.AuditEntries.Where(x => x.Action == "board.auto_unapproved" && x.EventId == bingoEvent.Id).ToListAsync());
        }
    }

    [Fact]
    public async Task FinalizedRosterPublishesTheActiveApprovalAndPublishedCorrectionSupersedesIt()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"slice6-publish-admin-{Guid.NewGuid():N}", now); admin.SetGlobalRole(GlobalRole.Admin); SetPassword(admin, now);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "Publication board", $"publication-board-{Guid.NewGuid():N}", "UTC", admin.Id, now);
        bingoEvent.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddDays(2), now.AddDays(4), 10);
        bingoEvent.OpenSignups(now.AddDays(-2));
        bingoEvent.CloseSignups(now.AddDays(-1));
        var draft = new Bingo.Domain.Teams.DraftSession(Guid.NewGuid(), bingoEvent.Id, 1);
        draft.Start(now); draft.Finalize(now);
        bingoEvent.SetDraftRosterPublication(true);
        var team = new Bingo.Domain.Teams.Team(Guid.NewGuid(), bingoEvent.Id, "Frozen team", "frozen-team", Bingo.Domain.Teams.TeamFormationType.Drafted, null, true);
        team.Finalize(now);
        var board = new Board(Guid.NewGuid(), bingoEvent.Id, "Board", 1, 1);
        board.AcquireEditing(admin.Id, now, TimeSpan.FromMinutes(5));
        var template = new TileTemplate(Guid.NewGuid(), "Manual tile", "Frozen", ObjectiveType.Manual, string.Empty, 4m);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, template.Id, 0, 0, "Manual tile", "Frozen", string.Empty, 4m);
        var image = new BoardTileImageAsset(Guid.NewGuid(), bingoEvent.Id, tile.Id, "frozen-board-image.png", "image.png", "image/png", 3, 1, 1, new string('a', 64), admin.Id, now);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 1, true, false, "Complete", true);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, bingoEvent, draft, team, board, template, tile, requirement);
            await setup.SaveChangesAsync();
            setup.Add(image);
            await setup.SaveChangesAsync();
            tile.SetActiveImageAsset(image.Id);
            await setup.SaveChangesAsync();
        }
        await using (var approval = new ApplicationDbContext(options))
        {
            var page = Page(approval, admin.Id); page.BoardVersion = 1;
            await page.OnPostApproveAsync(bingoEvent.Id, CancellationToken.None);
        }
        await using (var release = new ApplicationDbContext(options))
        {
            var approvedBoard = await release.Boards.SingleAsync(x => x.Id == board.Id);
            approvedBoard.ReleaseEditing(admin.Id, now);
            await release.SaveChangesAsync();
        }
        await using (var prePublication = new ApplicationDbContext(options))
        {
            var readiness = await new Bingo.Infrastructure.Events.EventLifecycleService(prePublication, null!, TimeProvider.System).GetStartReadinessAsync(bingoEvent.Id);
            Assert.Contains(readiness!.Blockers, x => x.Code == "BOARD_NOT_PUBLISHED");
        }
        await using (var publication = new ApplicationDbContext(options))
        {
            var page = Page(publication, admin.Id); page.BoardVersion = 2;
            await page.OnPostPublishAsync(bingoEvent.Id, CancellationToken.None);
        }
        await using var verify = new ApplicationDbContext(options);
        var published = await verify.Boards.SingleAsync(x => x.Id == board.Id);
        Assert.Equal(BoardState.Published, published.State);
        Assert.NotNull(published.ActiveApprovalSnapshotId);
        var originalApprovalId = published.ActiveApprovalSnapshotId.Value;
        Assert.True(await verify.Events.Where(x => x.Id == bingoEvent.Id).Select(x => x.BoardPublished).SingleAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == bingoEvent.Id && x.Action == "board.published").ToListAsync());
        var mutableTile = await verify.BoardTiles.SingleAsync(x => x.Id == tile.Id);
        var mutableTemplate = await verify.TileTemplates.SingleAsync(x => x.Id == template.Id);
        mutableTile.UpdateContent("Changed after publication", "Changed", string.Empty, 99m);
        mutableTemplate.Update("Manual tile", "Frozen", ObjectiveType.Manual, string.Empty, 99m);
        await verify.SaveChangesAsync();
        verify.ChangeTracker.Clear();
        var publicBoard = await new Bingo.Infrastructure.Boards.PublicBoardService(verify).GetEventBoardAsync(bingoEvent.Slug);
        Assert.NotNull(publicBoard);
        Assert.Equal("Manual tile", Assert.Single(publicBoard!.Teams.Single().Tiles).Name);
        Assert.Equal($"/Events/{bingoEvent.Slug}/Board/Tiles/{tile.Id}/Image", publicBoard.Teams.Single().Tiles.Single().ImageUrl);
        var validImage = await new PublicBoardImageService(verify, new TestStorage()).OpenAsync(bingoEvent.Slug, tile.Id, CancellationToken.None);
        var unknownImage = await new PublicBoardImageService(verify, new TestStorage()).OpenAsync(bingoEvent.Slug, Guid.NewGuid(), CancellationToken.None);
        Assert.NotEqual(404, (validImage as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(404, (unknownImage as IStatusCodeHttpResult)?.StatusCode);

        var rejectedCorrection = Page(verify, admin.Id);
        await rejectedCorrection.OnPostCorrectPublishedAsync(bingoEvent.Id, false, null, CancellationToken.None);
        Assert.Equal(originalApprovalId, await verify.Boards.Where(x => x.Id == board.Id).Select(x => x.ActiveApprovalSnapshotId).SingleAsync());
        Assert.Single(await verify.BoardApprovalSnapshots.Where(x => x.BoardId == board.Id).ToListAsync());

        var liveEvent = await verify.Events.SingleAsync(value => value.Id == bingoEvent.Id);
        liveEvent.StartEvent(now);
        await verify.SaveChangesAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Database", database.GetConnectionString()));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, admin.LoginName);
        var boardPage = await client.GetStringAsync($"/Admin/Events/Board/{bingoEvent.Id}");
        using var correctedResponse = await PostAsync(client, $"/Admin/Events/Board/{bingoEvent.Id}?handler=CorrectPublished", boardPage, new Dictionary<string, string>
        {
            ["confirmed"] = "true",
            ["reason"] = "Correct the public name."
        });
        Assert.Equal(HttpStatusCode.Redirect, correctedResponse.StatusCode);
        verify.ChangeTracker.Clear();
        var correction = await verify.Boards.SingleAsync(x => x.Id == board.Id);
        Assert.Equal(BoardState.Published, correction.State);
        Assert.True(correction.PublishedCorrectionInProgress);
        Assert.Equal(originalApprovalId, correction.ActiveApprovalSnapshotId);
        Assert.Single(await verify.BoardApprovalSnapshots.Where(x => x.BoardId == board.Id).ToListAsync());
        var stillPublicBoard = await new Bingo.Infrastructure.Boards.PublicBoardService(verify).GetEventBoardAsync(bingoEvent.Slug);
        Assert.Equal("Manual tile", Assert.Single(stillPublicBoard!.Teams.Single().Tiles).Name);

        Assert.Equal(admin.Id, correction.EditorAccountId);
        Assert.NotNull(correction.EditorLeaseExpiresAt);

        await using (var edit = new ApplicationDbContext(options))
        {
            var currentVersion = await edit.Boards.Where(value => value.Id == board.Id).Select(value => value.Version).SingleAsync();
            var page = Page(edit, admin.Id);
            page.BoardVersion = currentVersion;
            page.TileDraft = new BoardModel.TileDraftInput
            {
                TileId = tile.Id,
                Position = 0,
                Name = "Corrected public tile",
                Description = "Corrected private working copy",
                ManualEhb = 55m,
                Requirements = [new BoardModel.RequirementInput { Kind = "challenge", Description = "Complete corrected objective", Target = 1 }]
            };
            Assert.IsType<RedirectToPageResult>(await page.OnPostEditTileAsync(bingoEvent.Id, CancellationToken.None));
        }

        verify.ChangeTracker.Clear();
        var afterEditPublicBoard = await new Bingo.Infrastructure.Boards.PublicBoardService(verify).GetEventBoardAsync(bingoEvent.Slug);
        Assert.Equal("Manual tile", Assert.Single(afterEditPublicBoard!.Teams.Single().Tiles).Name);

        await using (var replaceApproval = new ApplicationDbContext(options))
        {
            var currentVersion = await replaceApproval.Boards.Where(value => value.Id == board.Id).Select(value => value.Version).SingleAsync();
            var page = Page(replaceApproval, admin.Id);
            page.BoardVersion = currentVersion;
            Assert.IsType<RedirectToPageResult>(await page.OnPostApproveAsync(bingoEvent.Id, CancellationToken.None));
        }

        verify.ChangeTracker.Clear();
        var corrected = await verify.Boards.SingleAsync(x => x.Id == board.Id);
        Assert.Equal(BoardState.Published, corrected.State);
        Assert.False(corrected.PublishedCorrectionInProgress);
        Assert.NotEqual(originalApprovalId, corrected.ActiveApprovalSnapshotId);
        var replacement = await verify.BoardApprovalSnapshots.SingleAsync(x => x.Id == corrected.ActiveApprovalSnapshotId);
        Assert.Equal(originalApprovalId, replacement.SupersedesApprovalSnapshotId);
        Assert.Equal(BoardState.Published, replacement.LifecycleState);
        Assert.Equal(2, await verify.BoardApprovalSnapshots.CountAsync(x => x.BoardId == board.Id));
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == bingoEvent.Id && x.Action == "board.published_corrected").ToListAsync());
        var correctedPublicBoard = await new Bingo.Infrastructure.Boards.PublicBoardService(verify).GetEventBoardAsync(bingoEvent.Slug);
        Assert.Equal("Corrected public tile", Assert.Single(correctedPublicBoard!.Teams.Single().Tiles).Name);
        Assert.Equal(55m, Assert.Single(correctedPublicBoard.Teams.Single().Tiles).EstimatedEhb);
        var afterPublicationReadiness = await new Bingo.Infrastructure.Events.EventLifecycleService(verify, null!, TimeProvider.System).GetStartReadinessAsync(bingoEvent.Id);
        Assert.DoesNotContain(afterPublicationReadiness!.Blockers, x => x.Code == "BOARD_NOT_PUBLISHED");

        var retiredImageAsset = await verify.BoardTileImageAssets.SingleAsync(value => value.Id == image.Id);
        retiredImageAsset.Replace(now.AddMinutes(1));
        await verify.SaveChangesAsync();
        verify.ChangeTracker.Clear();
        var withoutRetiredArtwork = await new Bingo.Infrastructure.Boards.PublicBoardService(verify).GetEventBoardAsync(bingoEvent.Slug);
        Assert.Equal($"/Events/{bingoEvent.Slug}/Board/Tiles/{tile.Id}/Image", Assert.Single(withoutRetiredArtwork!.Teams.Single().Tiles).ImageUrl);
        var retiredStorage = new TestStorage();
        var retiredImage = await new PublicBoardImageService(verify, retiredStorage).OpenAsync(bingoEvent.Slug, tile.Id, CancellationToken.None);
        Assert.NotEqual(404, (retiredImage as IStatusCodeHttpResult)?.StatusCode);
        Assert.Equal(1, retiredStorage.OpenReadCount);
    }

    [Fact]
    public async Task PrivatePreviewUsesDeterministicDemoProgressWithoutAnyCompetitiveWrite()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"slice6-preview-admin-{Guid.NewGuid():N}", now);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "Preview", $"preview-{Guid.NewGuid():N}", "UTC", admin.Id, now);
        var board = new Board(Guid.NewGuid(), bingoEvent.Id, "Preview board", 1, 3);
        var template = new TileTemplate(Guid.NewGuid(), "Preview tile", string.Empty, ObjectiveType.Manual, string.Empty, 4m);
        var tiles = Enumerable.Range(0, 3).Select(index => new BoardTile(Guid.NewGuid(), board.Id, template.Id, 0, index, $"Tile {index + 1}", string.Empty, string.Empty, 4m)).ToArray();
        var requirements = tiles.Select(tile => new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 4, true, false, "Complete", true)).ToArray();
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, bingoEvent, board, template);
            setup.AddRange(tiles);
            setup.AddRange(requirements);
            await setup.SaveChangesAsync();
        }

        await using var previewContext = new ApplicationDbContext(options);
        var before = new
        {
            Boards = await previewContext.Boards.CountAsync(),
            Tiles = await previewContext.BoardTiles.CountAsync(),
            Requirements = await previewContext.BoardRequirementSnapshots.CountAsync(),
            Audits = await previewContext.AuditEntries.CountAsync()
        };
        var preview = new BoardPreviewModel(previewContext);
        Assert.IsType<PageResult>(await preview.OnGetAsync(bingoEvent.Id, null, null, CancellationToken.None));
        Assert.Collection(preview.Tiles,
            first => Assert.Equal(4, first.Approved),
            second => Assert.Equal(2, second.Approved),
            third => Assert.Equal(0, third.Approved));
        Assert.Collection(preview.Teams,
            first => Assert.Equal("Preview team Alpha", first.Name),
            second => Assert.Equal("Preview team Bravo", second.Name));
        var team = preview.Teams[0];
        Assert.IsType<PageResult>(await preview.OnGetAsync(bingoEvent.Id, team.Slug, null, CancellationToken.None));
        Assert.Equal(team, preview.SelectedTeam);
        var tile = preview.Tiles[0];
        Assert.IsType<PageResult>(await preview.OnGetAsync(bingoEvent.Id, team.Slug, tile.Id, CancellationToken.None));
        Assert.Equal(tile, preview.SelectedTile);
        Assert.Contains(typeof(BoardPreviewModel).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Cast<AuthorizeAttribute>(), x => x.Policy == AuthorizationPolicies.Admin);
        var after = new
        {
            Boards = await previewContext.Boards.CountAsync(),
            Tiles = await previewContext.BoardTiles.CountAsync(),
            Requirements = await previewContext.BoardRequirementSnapshots.CountAsync(),
            Audits = await previewContext.AuditEntries.CountAsync()
        };
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task ConcurrentPublicationHasOneWinnerAndNoDuplicatePublicationResidue()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"slice6-publication-race-{Guid.NewGuid():N}", now);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "Publication race", $"publication-race-{Guid.NewGuid():N}", "UTC", admin.Id, now);
        bingoEvent.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddDays(2), now.AddDays(4), 10);
        bingoEvent.OpenSignups(now.AddDays(-2));
        bingoEvent.CloseSignups(now.AddDays(-1));
        var draft = new Bingo.Domain.Teams.DraftSession(Guid.NewGuid(), bingoEvent.Id, 1);
        draft.Start(now); draft.Finalize(now);
        bingoEvent.SetDraftRosterPublication(true);
        var board = new Board(Guid.NewGuid(), bingoEvent.Id, "Board", 1, 1);
        board.AcquireEditing(admin.Id, now, TimeSpan.FromMinutes(5));
        var template = new TileTemplate(Guid.NewGuid(), "Manual", string.Empty, ObjectiveType.Manual, string.Empty, 2m);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, template.Id, 0, 0, "Manual", string.Empty, string.Empty, 2m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 1, true, false, "Manual", true);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, bingoEvent, draft, board, template, tile, requirement);
            await setup.SaveChangesAsync();
        }
        await using (var approval = new ApplicationDbContext(options))
        {
            var page = Page(approval, admin.Id); page.BoardVersion = 1;
            await page.OnPostApproveAsync(bingoEvent.Id, CancellationToken.None);
        }
        await using (var release = new ApplicationDbContext(options))
        {
            var approved = await release.Boards.SingleAsync(x => x.Id == board.Id);
            approved.ReleaseEditing(admin.Id, now);
            await release.SaveChangesAsync();
        }

        await using var firstContext = new ApplicationDbContext(options);
        await using var secondContext = new ApplicationDbContext(options);
        var first = Page(firstContext, admin.Id); first.BoardVersion = 2;
        var second = Page(secondContext, admin.Id); second.BoardVersion = 2;
        await Task.WhenAll(first.OnPostPublishAsync(bingoEvent.Id, CancellationToken.None), second.OnPostPublishAsync(bingoEvent.Id, CancellationToken.None));

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(BoardState.Published, await verify.Boards.Where(x => x.Id == board.Id).Select(x => x.State).SingleAsync());
        Assert.True(await verify.Events.Where(x => x.Id == bingoEvent.Id).Select(x => x.BoardPublished).SingleAsync());
        Assert.Single(await verify.BoardApprovalSnapshots.Where(x => x.BoardId == board.Id).ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == bingoEvent.Id && x.Action == "board.published").ToListAsync());
    }

    [Fact]
    public async Task ApprovalRejectsIncompleteOrMissingEhbBoardsWithoutSnapshotResidue()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"slice6-approval-invalid-{Guid.NewGuid():N}", now);
        var incompleteEvent = new BingoEvent(Guid.NewGuid(), "Incomplete", $"incomplete-{Guid.NewGuid():N}", "UTC", admin.Id, now);
        var incompleteBoard = new Board(Guid.NewGuid(), incompleteEvent.Id, "Board", 1, 2);
        var incompleteTemplate = new TileTemplate(Guid.NewGuid(), "Tile", string.Empty, ObjectiveType.Manual, string.Empty, 3m);
        var incompleteTile = new BoardTile(Guid.NewGuid(), incompleteBoard.Id, incompleteTemplate.Id, 0, 0, "Tile", string.Empty, string.Empty, 3m);
        var incompleteRequirement = new BoardRequirementSnapshot(Guid.NewGuid(), incompleteTile.Id, 1, 1, true, false, "Manual", true);
        var missingEhbEvent = new BingoEvent(Guid.NewGuid(), "Missing EHB", $"missing-ehb-{Guid.NewGuid():N}", "UTC", admin.Id, now);
        var missingEhbBoard = new Board(Guid.NewGuid(), missingEhbEvent.Id, "Board", 1, 1);
        var missingEhbTemplate = new TileTemplate(Guid.NewGuid(), "No EHB", string.Empty, ObjectiveType.Manual, string.Empty, null);
        var missingEhbTile = new BoardTile(Guid.NewGuid(), missingEhbBoard.Id, missingEhbTemplate.Id, 0, 0, "No EHB", string.Empty, string.Empty, 0m);
        var missingEhbRequirement = new BoardRequirementSnapshot(Guid.NewGuid(), missingEhbTile.Id, 1, 1, true, false, "Manual", true);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, incompleteEvent, incompleteBoard, incompleteTemplate, incompleteTile, incompleteRequirement, missingEhbEvent, missingEhbBoard, missingEhbTemplate, missingEhbTile, missingEhbRequirement);
            await setup.SaveChangesAsync();
        }

        await using (var incompleteAttempt = new ApplicationDbContext(options))
        {
            var page = Page(incompleteAttempt, admin.Id); page.BoardVersion = 1;
            Assert.IsType<RedirectToPageResult>(await page.OnPostApproveAsync(incompleteEvent.Id, CancellationToken.None));
        }
        await using (var missingEhbAttempt = new ApplicationDbContext(options))
        {
            var page = Page(missingEhbAttempt, admin.Id); page.BoardVersion = 1;
            Assert.IsType<RedirectToPageResult>(await page.OnPostApproveAsync(missingEhbEvent.Id, CancellationToken.None));
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(BoardState.Draft, await verify.Boards.Where(x => x.Id == incompleteBoard.Id).Select(x => x.State).SingleAsync());
        Assert.Equal(BoardState.Draft, await verify.Boards.Where(x => x.Id == missingEhbBoard.Id).Select(x => x.State).SingleAsync());
        Assert.Empty(await verify.BoardApprovalSnapshots.Where(x => x.BoardId == incompleteBoard.Id || x.BoardId == missingEhbBoard.Id).ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => (x.EventId == incompleteEvent.Id || x.EventId == missingEhbEvent.Id) && x.Action == "board.approved").ToListAsync());
    }

    [Fact]
    public async Task ConcurrentApprovalCreatesOneCoherentSnapshotAndOneAudit()
    {
        var now = DateTimeOffset.UtcNow;
        var admin = Website($"slice6-approval-race-{Guid.NewGuid():N}", now);
        var bingoEvent = new BingoEvent(Guid.NewGuid(), "Approval race", $"approval-race-{Guid.NewGuid():N}", "UTC", admin.Id, now);
        var board = new Board(Guid.NewGuid(), bingoEvent.Id, "Board", 1, 1);
        board.AcquireEditing(admin.Id, now, TimeSpan.FromMinutes(5));
        var template = new TileTemplate(Guid.NewGuid(), "Manual", string.Empty, ObjectiveType.Manual, string.Empty, 2m);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, template.Id, 0, 0, "Manual", string.Empty, string.Empty, 2m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 1, true, false, "Manual", true);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, bingoEvent, board, template, tile, requirement);
            await setup.SaveChangesAsync();
        }
        await using var firstContext = new ApplicationDbContext(options);
        await using var secondContext = new ApplicationDbContext(options);
        var first = Page(firstContext, admin.Id); first.BoardVersion = 1;
        var second = Page(secondContext, admin.Id); second.BoardVersion = 1;
        await Task.WhenAll(first.OnPostApproveAsync(bingoEvent.Id, CancellationToken.None), second.OnPostApproveAsync(bingoEvent.Id, CancellationToken.None));

        await using var verify = new ApplicationDbContext(options);
        var finalBoard = await verify.Boards.SingleAsync(x => x.Id == board.Id);
        Assert.Equal(BoardState.Validated, finalBoard.State);
        Assert.NotNull(finalBoard.ActiveApprovalSnapshotId);
        Assert.Single(await verify.BoardApprovalSnapshots.Where(x => x.BoardId == board.Id).ToListAsync());
        Assert.Single(await verify.BoardApprovalTileSnapshots.Where(x => x.ApprovalSnapshotId == finalBoard.ActiveApprovalSnapshotId).ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == bingoEvent.Id && x.Action == "board.approved").ToListAsync());
    }

    private static Account Website(string name, DateTimeOffset now) => Account.CreateWebsite(Guid.NewGuid(), name, name.ToUpperInvariant(), now);

    private async Task<BoardModel> LoadBoardAsync(Guid eventId, Guid accountId)
    {
        var db = new ApplicationDbContext(options);
        var page = Page(db, accountId);
        Assert.IsType<PageResult>(await page.OnGetAsync(eventId, CancellationToken.None));
        return page;
    }

    private static BoardModel Page(ApplicationDbContext db, Guid accountId)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, accountId.ToString()), new Claim(ClaimTypes.Name, "admin")], "test"))
        };
        return new BoardModel(db, TimeProvider.System, new AuditWriter(db, TimeProvider.System), new NullAdminCollaborationNotifier(), new TestStorage())
        {
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(context, new TestTempDataProvider())
        };
    }

    private static CatalogueIndexModel CataloguePage(ApplicationDbContext db, Guid accountId, bool superAdmin)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, accountId.ToString()),
            new(ClaimTypes.Name, "owner")
        };
        if (superAdmin) claims.Add(new Claim(ClaimTypes.Role, GlobalRole.SuperAdmin.ToString()));
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        return new CatalogueIndexModel(db, TimeProvider.System)
        {
            PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor())),
            TempData = new TempDataDictionary(context, new TestTempDataProvider())
        };
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestStorage : IEvidenceStorage
    {
        public int OpenReadCount { get; private set; }
        public Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            OpenReadCount++;
            return Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        }
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static void SetPassword(Account account, DateTimeOffset now) => account.SetPassword(new PasswordHasher<Account>().HashPassword(account, "password"), false, now, incrementVersion: false);
    private static async Task LoginAsync(HttpClient client, string username)
    {
        var page = await client.GetStringAsync("/Account/Login");
        using var response = await PostAsync(client, "/Account/Login", page, new Dictionary<string, string> { ["Input.Username"] = username, ["Input.Password"] = "password" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string page, IReadOnlyDictionary<string, string> values)
    {
        var form = new Dictionary<string, string>(values) { ["__RequestVerificationToken"] = AntiforgeryToken(page) };
        return client.PostAsync(path, new FormUrlEncodedContent(form));
    }
    private static string AntiforgeryToken(string page) => Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private sealed class StubHttpClientFactory(string wikitext) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubWikiHandler(wikitext)) { BaseAddress = new Uri("https://oldschool.runescape.wiki/") };
    }

    private sealed class StubWikiHandler(string wikitext) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var query = request.RequestUri?.Query ?? string.Empty;
            var payload = query.Contains("prop=sections", StringComparison.Ordinal)
                ? "{\"parse\":{\"sections\":[{\"index\":\"1\",\"line\":\"Uniques\",\"fromtitle\":\"Imported boss\"}]}}"
                : $"{{\"parse\":{{\"wikitext\":{System.Text.Json.JsonSerializer.Serialize(wikitext)}}}}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
