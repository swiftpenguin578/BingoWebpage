using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Events;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task B5Round2EmptyPositionPrecedesStaleCatalogue(bool stateEndpoint)
    {
        var fixture = await SeedApprovalBatchAsync();
        await using var db = new ApplicationDbContext(options);
        (await db.Boards.SingleAsync()).Resize(1, 2, 1);
        await db.SaveChangesAsync();
        var page = Page(db, fixture.Admin.Id);
        await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
        await using (var edit = new ApplicationDbContext(options))
        {
            (await edit.BossActivities.SingleAsync(x => x.Id == fixture.Boss.Id)).Update("Changed boss", "Boss", 20m, null, null, null, CompletionFixtureNow);
            await edit.SaveChangesAsync();
        }
        var baseline = await B5RemediationBoardPersistenceAsync();
        if (stateEndpoint)
        {
            var response = Assert.IsType<BoardModel.BoardActionState>(Assert.IsType<JsonResult>(await page.OnPostApproveStateAsync(fixture.Event.Id, false, CancellationToken.None)).Value);
            Assert.Equal("board-incomplete", Assert.Single(response.Issues).Code);
            Assert.True(response.Current.Known);
        }
        else
        {
            Assert.IsType<RedirectToPageResult>(await page.OnPostApproveAsync(fixture.Event.Id, false, CancellationToken.None));
            Assert.Equal("Fill every board position before approving the board.", page.TempData["StatusMessage"]);
        }
        var issue = Assert.Single(page.ValidationIssues);
        Assert.Equal("board-incomplete", issue.Code);
        Assert.Equal(1, issue.Position);
        Assert.Null(issue.TileId);
        Assert.Equal(baseline, await B5RemediationBoardPersistenceAsync());
        await AssertApprovalBatchUnchangedAsync(fixture);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    public async Task B5Round2ApprovalRefusesTileOutsideFullGrid(int row, int column)
    {
        var fixture = await SeedApprovalBatchAsync(manual: true);
        await using var db = new ApplicationDbContext(options);
        var outside = new BoardTile(Guid.NewGuid(), fixture.Board.Id, fixture.Template.Id, row, column, "Outside grid", "Manual", "", 7);
        db.AddRange(outside, new BoardRequirementSnapshot(Guid.NewGuid(), outside.Id, 1, 1, true, false, "Manual", true));
        await db.SaveChangesAsync();
        var baseline = await B5RemediationBoardPersistenceAsync();
        var page = Page(db, fixture.Admin.Id);
        await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
        var response = Assert.IsType<BoardModel.BoardActionState>(Assert.IsType<JsonResult>(await page.OnPostApproveStateAsync(fixture.Event.Id, false, CancellationToken.None)).Value);
        var issue = Assert.Single(response.Issues);
        Assert.Equal("board-positions", issue.Code);
        Assert.Equal(outside.Id, issue.TileId);
        Assert.Equal(row * fixture.Board.Columns + column, issue.Position);
        Assert.Equal("Outside grid", issue.TileName);
        Assert.Equal("The board has conflicting tile positions. Reload and correct the layout before approving it.", issue.ResourceKey);
        Assert.Empty(issue.Arguments);
        Assert.Equal(baseline, await B5RemediationBoardPersistenceAsync());
        await AssertApprovalBatchUnchangedAsync(fixture);
    }

    [Fact]
    public async Task B5Round2RealApprovalSerializationConflictHasIssue()
    {
        var fixture = await SeedApprovalBatchAsync();
        var race = new B5Round2CatalogueRace(database.GetConnectionString(), fixture.Boss.Id);
        var raceOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(race).Options;
        await using var db = new ApplicationDbContext(raceOptions);
        var page = Page(db, fixture.Admin.Id);
        await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
        var baseline = await B5RemediationBoardPersistenceAsync();
        race.Armed = true;
        var response = Assert.IsType<BoardModel.BoardActionState>(Assert.IsType<JsonResult>(await page.OnPostApproveStateAsync(fixture.Event.Id, false, CancellationToken.None)).Value);
        Assert.True(race.Updated);
        Assert.Equal(PostgresErrorCodes.SerializationFailure, race.SqlState);
        var issue = Assert.Single(response.Issues);
        Assert.Equal("approval-conflict", issue.Code);
        Assert.Null(issue.TileId);
        Assert.Null(issue.Position);
        Assert.Equal("The board or catalogue changed while approval was being prepared. No approval was saved; reload and try again.", issue.ResourceKey);
        Assert.True(response.Current.Known);
        Assert.Equal(BoardState.Draft, response.Current.State!.State);
        Assert.Equal(baseline, await B5RemediationBoardPersistenceAsync());
        await AssertApprovalBatchUnchangedAsync(fixture);
    }

    private sealed class B5Round2CatalogueRace(string connectionString, Guid bossId) : DbCommandInterceptor
    {
        public bool Armed { get; set; }
        public bool Updated { get; private set; }
        public string? SqlState { get; private set; }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Armed && !Updated && command.CommandText.Contains("SELECT * FROM boss_activities WHERE id = ANY", StringComparison.Ordinal))
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var update = new NpgsqlCommand("UPDATE boss_activities SET version = version + 1 WHERE id = @id", connection);
                update.Parameters.AddWithValue("id", bossId);
                Assert.Equal(1, await update.ExecuteNonQueryAsync(cancellationToken));
                Updated = true;
            }
            return result;
        }
        public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            if (eventData.Exception is PostgresException exception) SqlState = exception.SqlState;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task B5Round2ObjectivePositionIssueIsUniquePerTile()
    {
        var fixture = await SeedApprovalBatchAsync();
        await using var db = new ApplicationDbContext(options);
        var page = Page(db, fixture.Admin.Id);
        await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
        // Controlled malformed-data fixture only; the production schema is unchanged.
        await db.Database.ExecuteSqlRawAsync("DROP INDEX \"IX_tile_template_requirements_tile_template_id_position\"");
        db.AddRange(
            new TileTemplateRequirement(Guid.NewGuid(), fixture.Template.Id, 1, 1, true, false, "First", false),
            new TileTemplateRequirement(Guid.NewGuid(), fixture.Template.Id, 1, 2, true, false, "Second", false),
            new BoardRequirementSnapshot(Guid.NewGuid(), fixture.Tile.Id, 1, 2, true, false, "Second", false));
        await db.SaveChangesAsync();
        var baseline = await B5RemediationBoardPersistenceAsync();
        var response = Assert.IsType<BoardModel.BoardActionState>(Assert.IsType<JsonResult>(await page.OnPostApproveStateAsync(fixture.Event.Id, false, CancellationToken.None)).Value);
        var issue = Assert.Single(response.Issues);
        Assert.Equal("objective-positions", issue.Code);
        Assert.Equal(fixture.Tile.Id, issue.TileId);
        Assert.Equal(0, issue.Position);
        Assert.True(response.Current.Known);
        Assert.Equal(2, Assert.Single(response.Current.State!.Working.Tiles).Objectives.Count);
        Assert.Equal(baseline, await B5RemediationBoardPersistenceAsync());
        await AssertApprovalBatchUnchangedAsync(fixture);
    }

    [Fact]
    public async Task B5Round2NewCatalogueObjectiveProjectionMatchesReplacementPublication()
    {
        var fixture = await SeedApprovalBatchAsync(correction: true);
        await using var db = new ApplicationDbContext(options);
        var page = Page(db, fixture.Admin.Id);
        await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
        var before = await B5BoardReadAsync(page, fixture.Event.Id);
        var original = Assert.Single(before.Published!.Tiles);
        // Fractional publication precision also distinguishes the retained frozen rate from the new objective's current rate.
        (await db.BossActivities.SingleAsync(x => x.Id == fixture.Boss.Id)).Update("Batch boss", "Boss", 7m, null, null, null, CompletionFixtureNow);
        var item = new CatalogueItem(Guid.NewGuid(), "New objective item", "NEW OBJECTIVE ITEM");
        item.SetPrice(1, CataloguePriceSource.Manual, CompletionFixtureNow);
        var drop = new SourceDrop(Guid.NewGuid(), fixture.Boss.Id, item.Id, "1/20", .05m, null, CompletionFixtureNow);
        db.AddRange(item, drop);
        await db.SaveChangesAsync();
        page.TileDraft = new BoardModel.TileDraftInput
        {
            TileId = fixture.Tile.Id, Position = 0, Name = fixture.Tile.NameSnapshot, Description = null,
            Requirements = [
                new BoardModel.RequirementInput { RequirementId = fixture.Requirement.Id, Kind = "drops", Target = 1, DuplicatesAllowed = true, BossIds = [fixture.Boss.Id], DropIds = [fixture.Drop.Id] },
                new BoardModel.RequirementInput { Kind = "drops", Target = 2, DuplicatesAllowed = true, BossIds = [fixture.Boss.Id], DropIds = [drop.Id] }
            ]
        };
        Assert.IsType<RedirectToPageResult>(await page.OnPostEditTileAsync(fixture.Event.Id, CancellationToken.None));
        Assert.Equal("committed", page.TempData["BoardTileOutcome"]);
        item.Update("Current new item name", "CURRENT NEW ITEM NAME", null, null);
        await db.SaveChangesAsync();
        await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
        var comparison = await B5BoardReadAsync(page, fixture.Event.Id);
        Assert.Equal(1, comparison.DifferentTileCount);
        Assert.Equal(fixture.Tile.Id, Assert.Single(comparison.DifferentTileIds));
        Assert.Equal(JsonSerializer.Serialize(comparison.PrivateCorrection), JsonSerializer.Serialize(comparison.Working));
        var projected = Assert.Single(comparison.PrivateCorrection!.Tiles);
        Assert.Equal(2, projected.Objectives.Count);
        Assert.Equal(6.7143m, projected.Ehb);
        var added = Assert.Single(projected.Objectives, x => x.RequirementId != fixture.Requirement.Id);
        Assert.Equal("Current new item name", Assert.Single(added.Drops).ItemName);
        Assert.Equal(drop.Id, Assert.Single(added.Drops).SourceDropId);
        Assert.Equal(2, added.Target);
        await page.OnPostApproveAsync(fixture.Event.Id, true, CancellationToken.None);
        Assert.Empty(page.ValidationIssues);
        var after = await B5BoardReadAsync(page, fixture.Event.Id);
        Assert.False(after.CorrectionInProgress);
        Assert.NotEqual(before.ActiveApprovalId, after.ActiveApprovalId);
        var published = Assert.Single(after.Published!.Tiles);
        Assert.Equal(JsonSerializer.Serialize(projected), JsonSerializer.Serialize(published));
        var actualChanged = JsonSerializer.Serialize(original) == JsonSerializer.Serialize(published) ? 0 : 1;
        Assert.Equal(comparison.DifferentTileCount, actualChanged);
    }
}
