using System.Net;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
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

    // U7-Q1: a missing-rate approval issue names the affected drops.
    [Fact]
    public async Task U7MissingRateIssueCarriesAffectedDropNames()
    {
        var fixture = await SeedApprovalBatchAsync(missingEstimate: true);
        await using var db = new ApplicationDbContext(options);
        var page = Page(db, fixture.Admin.Id);
        await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
        var result = Assert.IsType<BoardModel.BoardActionState>(Assert.IsType<JsonResult>(await page.OnPostApproveStateAsync(fixture.Event.Id, false, CancellationToken.None)).Value);
        var issue = Assert.Single(result.Issues, x => x.Code == "catalogue-rates-missing");
        Assert.Equal(["Batch item"], issue.DropNames);
        Assert.Equal("{0} needs automatic EHB. Correct the catalogue rates or drop requirements before approval; a manual estimate cannot replace them.", issue.ResourceKey);
        Assert.All(result.Issues.Where(x => x.Code != "catalogue-rates-missing"), x => Assert.Null(x.DropNames));
        await AssertApprovalBatchUnchangedAsync(fixture);
    }

    // U7-Q2: every applicable publication refusal is returned together, with no write.
    [Fact]
    public async Task U7PublishReturnsEveryApplicableRefusalTogether()
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
            var ev = await prepare.Events.SingleAsync();
            prepare.Entry(ev).Property(x => x.EventEndsAt).CurrentValue = DateTimeOffset.UtcNow.AddDays(-1);
            prepare.Entry(ev).Property(x => x.ActualStartedAt).CurrentValue = CompletionFixtureNow;
            (await prepare.DraftPublicationCycles.SingleAsync()).Supersede(CompletionFixtureNow, fixture.Admin.Id, "Controlled unpublished roster");
            (await prepare.CatalogueItems.SingleAsync(x => x.Id == fixture.Item.Id)).SetPrice(null, CataloguePriceSource.Missing, null);
            await prepare.SaveChangesAsync();
        }
        var baseline = await B5RemediationBoardPersistenceAsync();
        await using var db = new ApplicationDbContext(options);
        var actor = Page(db, fixture.Admin.Id);
        await actor.OnGetAsync(fixture.Event.Id, CancellationToken.None);
        var response = Assert.IsType<BoardModel.BoardActionState>(Assert.IsType<JsonResult>(await actor.OnPostPublishStateAsync(fixture.Event.Id, true, CancellationToken.None)).Value);
        Assert.Equal(["roster-unpublished", "event-already-started", "event-end-passed", "item-price-missing"], response.Issues.Select(x => x.Code));
        Assert.Equal(fixture.Tile.Id, response.Issues[^1].TileId);
        Assert.Equal(BoardState.Validated, response.Current.State!.State);
        Assert.Equal(baseline, await B5RemediationBoardPersistenceAsync());
    }

    // D17: Board page, EditorData and Readback load read-only on terminal events;
    // every POST keeps the D16 route refusal (302 to Manage) with no write.
    [Theory]
    [InlineData(EventState.Cancelled)]
    [InlineData(EventState.Finalized)]
    [InlineData(EventState.Archived)]
    public async Task U7TerminalBoardReadsLoadAndPostsStayRefused(EventState state)
    {
        var fixture = await SeedApprovalBatchAsync();
        await using (var prepare = new ApplicationDbContext(options))
        {
            prepare.Entry(await prepare.Events.SingleAsync()).Property(x => x.State).CurrentValue = state;
            await prepare.SaveChangesAsync();
        }
        await using var factory = ApprovalBatchFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, fixture.Admin.LoginName);
        using var page = await client.GetAsync(fixture.Path);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var displayed = await page.Content.ReadAsStringAsync();
        using var editor = await client.GetAsync($"{fixture.Path}?handler=EditorData&tileId={fixture.Tile.Id}");
        Assert.Equal(HttpStatusCode.OK, editor.StatusCode);
        using var readback = await client.GetAsync(fixture.Path + "?handler=Readback");
        Assert.Equal(HttpStatusCode.OK, readback.StatusCode);
        Assert.Contains("\"known\":true", await readback.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        foreach (var handler in new[] { "TeamSize", "ApproveState", "Resize", "TakeEditing" })
        {
            using var refused = await PostAsync(client, $"{fixture.Path}?handler={handler}", displayed, new Dictionary<string, string>
            {
                ["expectedTeamSize"] = "9", ["Rows"] = "2", ["Columns"] = "2",
                ["BoardVersion"] = ApprovalBatchInput(displayed, "BoardVersion")
            });
            Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
            Assert.Equal($"/Admin/Events/Manage/{fixture.Event.Id}", refused.Headers.Location!.OriginalString);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Null(await verify.Events.Where(x => x.Id == fixture.Event.Id).Select(x => x.ExpectedTeamSize).SingleAsync());
        Assert.Equal((1, 1), await verify.Boards.Select(x => new ValueTuple<int, int>(x.Rows, x.Columns)).SingleAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == fixture.Event.Id).ToListAsync());
    }

    // D17: a terminal event without a board shows "no board recorded" and never creates one.
    [Fact]
    public async Task U7TerminalEventWithoutBoardDoesNotCreateOne()
    {
        var fixture = await SeedApprovalBatchAsync();
        var orphan = new BingoEvent(Guid.NewGuid(), "No board", $"no-board-{Guid.NewGuid():N}", "UTC", fixture.Admin.Id, DateTimeOffset.UtcNow, Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        await using (var prepare = new ApplicationDbContext(options))
        {
            prepare.Add(orphan);
            await prepare.SaveChangesAsync();
            prepare.Entry(orphan).Property(x => x.State).CurrentValue = EventState.Cancelled;
            await prepare.SaveChangesAsync();
        }
        await using var factory = ApprovalBatchFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, fixture.Admin.LoginName);
        using var page = await client.GetAsync($"/Admin/Events/Board/{orphan.Id}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("data-board-none", await page.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var readback = await client.GetAsync($"/Admin/Events/Board/{orphan.Id}?handler=Readback");
        Assert.Contains("\"known\":false", await readback.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await using var verify = new ApplicationDbContext(options);
        Assert.False(await verify.Boards.AnyAsync(x => x.EventId == orphan.Id));
        using var unknown = await client.GetAsync($"/Admin/Events/Board/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    // In-place transport: a page-module command answers JSON with the definite outcome
    // and the authoritative readback; a definite refusal is a refusal with the reason.
    [Fact]
    public async Task U7InlineCommandsAnswerOutcomeAndReadbackInPlace()
    {
        var fixture = await SeedApprovalBatchAsync();
        await using var factory = ApprovalBatchFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, fixture.Admin.LoginName);
        var displayed = await client.GetStringAsync(fixture.Path);
        async Task<System.Text.Json.JsonElement> InlineAsync(string handler, Dictionary<string, string> values)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{fixture.Path}?handler={handler}")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>(values) { ["__RequestVerificationToken"] = AntiforgeryToken(displayed) })
            };
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
            return System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
        }
        var version = ApprovalBatchInput(displayed, "BoardVersion");
        var refused = await InlineAsync("TeamSize", new() { ["expectedTeamSize"] = "0", ["BoardVersion"] = version });
        Assert.Equal("refused", refused.GetProperty("outcome").GetString());
        Assert.Equal("Expected team size must be between 1 and 100.", refused.GetProperty("message").GetString());
        var saved = await InlineAsync("TeamSize", new() { ["expectedTeamSize"] = "7", ["BoardVersion"] = version });
        Assert.Equal("saved", saved.GetProperty("outcome").GetString());
        Assert.Equal(7, saved.GetProperty("current").GetProperty("state").GetProperty("expectedTeamSize").GetInt32());
        var stale = await InlineAsync("Resize", new() { ["Rows"] = "2", ["Columns"] = "2", ["BoardVersion"] = version });
        Assert.Equal("refused", stale.GetProperty("outcome").GetString());
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal((1, 1), await verify.Boards.Select(x => new ValueTuple<int, int>(x.Rows, x.Columns)).SingleAsync());
    }
}
