using System.Net;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

// U7 (brief 88) server rules: B-Board-1 (players per team before Live only, also
// during an open published correction) and B-Board-2 (correction reason, tile
// name and drop weight limits). Each refusal leaves no write and no audit row.
public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Theory]
    [InlineData(EventState.Live)]
    [InlineData(EventState.AwaitingFinalReview)]
    public async Task U7TeamSizeDuringPublishedCorrectionAfterLiveIsRefusedWithoutWrite(EventState state)
    {
        var fixture = await SeedApprovalBatchAsync(correction: true);
        await using (var prepare = new ApplicationDbContext(options))
        {
            prepare.Entry(await prepare.Events.SingleAsync()).Property(x => x.State).CurrentValue = state;
            await prepare.SaveChangesAsync();
        }
        await using var factory = ApprovalBatchFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, fixture.Admin.LoginName);
        var displayed = await client.GetStringAsync(fixture.Path);
        using var response = await PostAsync(client, fixture.Path + "?handler=TeamSize", displayed, new Dictionary<string, string> { ["expectedTeamSize"] = "9", ["BoardVersion"] = ApprovalBatchInput(displayed, "BoardVersion") });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var verify = new ApplicationDbContext(options);
        Assert.Null(await verify.Events.Where(x => x.Id == fixture.Event.Id).Select(x => x.ExpectedTeamSize).SingleAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == fixture.Event.Id).ToListAsync());
    }

    // B-Board-1: also the direct handler answers a refusal, never the domain throw.
    [Fact]
    public async Task U7TeamSizeHandlerRefusesAfterLiveAndStillSavesBeforeLive()
    {
        var fixture = await SeedApprovalBatchAsync(correction: true);
        await using (var db = new ApplicationDbContext(options))
        {
            var page = Page(db, fixture.Admin.Id);
            await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
            Assert.IsType<RedirectToPageResult>(await page.OnPostTeamSizeAsync(fixture.Event.Id, 6, CancellationToken.None));
        }
        await using (var verify = new ApplicationDbContext(options))
            Assert.Equal(6, await verify.Events.Where(x => x.Id == fixture.Event.Id).Select(x => x.ExpectedTeamSize).SingleAsync());
        await using (var prepare = new ApplicationDbContext(options))
        {
            prepare.Entry(await prepare.Events.SingleAsync()).Property(x => x.State).CurrentValue = EventState.Live;
            await prepare.SaveChangesAsync();
        }
        long version;
        await using (var db = new ApplicationDbContext(options))
        {
            var page = Page(db, fixture.Admin.Id);
            await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
            version = page.BoardVersion;
            Assert.IsType<RedirectToPageResult>(await page.OnPostTeamSizeAsync(fixture.Event.Id, 9, CancellationToken.None));
        }
        await using var after = new ApplicationDbContext(options);
        Assert.Equal(6, await after.Events.Where(x => x.Id == fixture.Event.Id).Select(x => x.ExpectedTeamSize).SingleAsync());
        Assert.Equal(version, (await after.Boards.SingleAsync()).Version);
        Assert.Equal(1, await after.AuditEntries.CountAsync(x => x.EventId == fixture.Event.Id && x.Action == "board.expected_team_size_changed"));
    }

    [Theory]
    [InlineData(80, true)]
    [InlineData(81, false)]
    public async Task U7ChangedTileNameLimitIsEightyCharacters(int length, bool saves)
    {
        var fixture = await SeedApprovalBatchAsync();
        var name = new string('n', length);
        var outcome = await U7EditTileAsync(fixture, name, 1);
        Assert.Equal(saves ? "committed" : "failed", outcome);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(saves ? name : "Batch tile", (await verify.BoardTiles.SingleAsync(x => x.Id == fixture.Tile.Id)).NameSnapshot);
        Assert.Equal(saves ? 1 : 0, await verify.AuditEntries.CountAsync(x => x.EventId == fixture.Event.Id && x.Action == "board.tile_edited"));
    }

    [Fact]
    public async Task U7UnchangedStoredLongTileNameStillSavesAndNewLongNameIsRefused()
    {
        var fixture = await SeedApprovalBatchAsync();
        var stored = new string('s', 200);
        await using (var prepare = new ApplicationDbContext(options))
        {
            prepare.Entry(await prepare.BoardTiles.SingleAsync()).Property(x => x.NameSnapshot).CurrentValue = stored;
            (await prepare.Boards.SingleAsync()).Resize(1, 2, 1);
            await prepare.SaveChangesAsync();
        }
        Assert.Equal("committed", await U7EditTileAsync(fixture, stored, 1));
        await using (var db = new ApplicationDbContext(options))
        {
            var page = Page(db, fixture.Admin.Id);
            await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
            page.TileDraft = new BoardModel.TileDraftInput
            {
                Position = 1,
                Name = new string('x', 81),
                Requirements = [new BoardModel.RequirementInput { Kind = "drops", Target = 1, BossIds = [fixture.Boss.Id], DropIds = [fixture.Drop.Id] }]
            };
            Assert.IsType<RedirectToPageResult>(await page.OnPostCreateTileAsync(fixture.Event.Id, CancellationToken.None));
            Assert.Equal("failed", page.TempData["BoardTileOutcome"]?.ToString());
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(1, await verify.BoardTiles.CountAsync(x => x.BoardId == fixture.Board.Id));
        Assert.Equal(stored, (await verify.BoardTiles.SingleAsync()).NameSnapshot);
    }

    [Theory]
    [InlineData(10000, true)]
    [InlineData(10001, false)]
    [InlineData(0, false)]
    public async Task U7DropWeightLimitIsOneToTenThousand(int weight, bool saves)
    {
        var fixture = await SeedApprovalBatchAsync();
        Assert.Equal(saves ? "committed" : "failed", await U7EditTileAsync(fixture, "Batch tile", weight));
        await using var verify = new ApplicationDbContext(options);
        var stored = await verify.BoardRequirementDropSnapshots.Where(x => x.SourceDropId == fixture.Drop.Id).Select(x => x.CreditedWeight).ToListAsync();
        Assert.Equal(saves ? weight : 1, Assert.Single(stored));
        Assert.Equal(saves ? 1 : 0, await verify.AuditEntries.CountAsync(x => x.EventId == fixture.Event.Id && x.Action == "board.tile_edited"));
    }

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public async Task U7CorrectionReasonLimitIsTwoThousandCharacters(int length, bool starts)
    {
        var fixture = await SeedApprovalBatchAsync(correction: true);
        await using (var prepare = new ApplicationDbContext(options))
        {
            prepare.Entry(await prepare.Boards.SingleAsync()).Property(x => x.PublishedCorrectionInProgress).CurrentValue = false;
            await prepare.SaveChangesAsync();
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var page = Page(db, fixture.Admin.Id);
            await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
            Assert.IsType<RedirectToPageResult>(await page.OnPostCorrectPublishedAsync(fixture.Event.Id, true, new string('r', length), CancellationToken.None));
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(starts, (await verify.Boards.SingleAsync()).PublishedCorrectionInProgress);
        Assert.Equal(starts ? 1 : 0, await verify.AuditEntries.CountAsync(x => x.EventId == fixture.Event.Id && x.Action == "board.published_correction_started"));
    }

    private async Task<string?> U7EditTileAsync(ApprovalBatchFixture fixture, string name, int weight)
    {
        await using var db = new ApplicationDbContext(options);
        var requirementId = await db.BoardRequirementSnapshots.Where(x => x.BoardTileId == fixture.Tile.Id).Select(x => x.Id).SingleAsync();
        var page = Page(db, fixture.Admin.Id);
        await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
        page.TileDraft = new BoardModel.TileDraftInput
        {
            TileId = fixture.Tile.Id,
            Position = 0,
            Name = name,
            Requirements = [new BoardModel.RequirementInput
            {
                RequirementId = requirementId,
                Kind = "drops",
                Target = 1,
                DuplicatesAllowed = true,
                BossIds = [fixture.Boss.Id],
                DropIds = [fixture.Drop.Id],
                DropWeights = new Dictionary<Guid, int> { [fixture.Drop.Id] = weight }
            }]
        };
        Assert.IsType<RedirectToPageResult>(await page.OnPostEditTileAsync(fixture.Event.Id, CancellationToken.None));
        return page.TempData["BoardTileOutcome"]?.ToString();
    }
}
