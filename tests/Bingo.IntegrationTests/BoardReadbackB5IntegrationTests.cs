using System.Text.Json;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class Slice6CatalogueAdministrationIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task B5BoardApprovalIssuesCarryStableCodeAndTileOrBoardTarget(bool boardLevel)
    {
        var fixture = await SeedApprovalBatchAsync(missingEstimate: !boardLevel);
        await using var db = new ApplicationDbContext(options);
        if (boardLevel) { (await db.Boards.SingleAsync()).Resize(1, 2, 1); await db.SaveChangesAsync(); }
        var page = Page(db, fixture.Admin.Id);
        await page.OnGetAsync(fixture.Event.Id, CancellationToken.None);
        var response = Assert.IsType<BoardModel.BoardActionState>(Assert.IsType<JsonResult>(await page.OnPostApproveStateAsync(fixture.Event.Id, false, CancellationToken.None)).Value);
        var issue = Assert.Single(response.Issues);
        Assert.Equal(boardLevel ? "board-incomplete" : "catalogue-rates-missing", issue.Code);
        Assert.Equal(boardLevel ? null : fixture.Tile.Id, issue.TileId);
        Assert.Equal(boardLevel ? null : 0, issue.Position);
        Assert.True(response.Current.Known); Assert.Equal(BoardState.Draft, response.Current.State!.State);
        Assert.Empty(await db.BoardApprovalSnapshots.ToListAsync());
        if (!boardLevel) Assert.Equal(99m, (await db.TileTemplates.SingleAsync()).ManualEhbOverride);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("description")]
    [InlineData("identity")]
    [InlineData("artwork")]
    [InlineData("objective")]
    [InlineData("ehb")]
    public async Task B5BoardCorrectionComparisonCoversFullTileContent(string field)
    {
        var fixture = await SeedApprovalBatchAsync(correction: true);
        await using var db = new ApplicationDbContext(options);
        var page = Page(db, fixture.Admin.Id);
        var before = await B5BoardReadAsync(page, fixture.Event.Id);
        Assert.Equal(0, before.DifferentTileCount);
        Assert.NotNull(before.Published); Assert.NotNull(before.PrivateCorrection);
        var tile = await db.BoardTiles.SingleAsync(x => x.Id == fixture.Tile.Id);
        if (field == "name") db.Entry(tile).Property(x => x.NameSnapshot).CurrentValue = "Changed name";
        if (field == "description") db.Entry(tile).Property(x => x.DescriptionSnapshot).CurrentValue = "Changed description";
        if (field == "identity")
        {
            var template = new TileTemplate(Guid.NewGuid(), "Other template", "Other", ObjectiveType.DropRequirements, "", null);
            db.TileTemplates.Add(template); db.Entry(tile).Property(x => x.TileTemplateId).CurrentValue = template.Id;
        }
        if (field == "ehb") db.Entry(tile).Property(x => x.EstimatedEhbSnapshot).CurrentValue = 4m;
        if (field == "objective") db.Entry(await db.BoardRequirementSnapshots.SingleAsync(x => x.Id == fixture.Requirement.Id)).Property(x => x.Description).CurrentValue = "Changed objective wording";
        if (field == "artwork")
        {
            var imageId = Guid.NewGuid();
            db.BoardTileImageAssets.Add(new(imageId, fixture.Event.Id, tile.Id, "controlled-b5-artwork", "fixture.png", "image/png", 4, 1, 1, "fixture-checksum", fixture.Admin.Id, CompletionFixtureNow));
            db.Entry(tile).Property(x => x.ActiveImageAssetId).CurrentValue = imageId;
        }
        (await db.Boards.SingleAsync()).MarkChanged(); await db.SaveChangesAsync();
        var after = await B5BoardReadAsync(page, fixture.Event.Id);
        Assert.Equal(1, after.DifferentTileCount); Assert.Equal(fixture.Tile.Id, Assert.Single(after.DifferentTileIds));
        Assert.Equal(JsonSerializer.Serialize(before.Published), JsonSerializer.Serialize(after.Published));
        Assert.True(after.Version > before.Version);
        Assert.Equal(after.PrivateCorrection, after.Working);
    }

    [Fact]
    public async Task B5BoardReadbackDistinguishesOtherAdminApprovalPublicationAndCorrectionDisposition()
    {
        var fixture = await SeedApprovalBatchAsync();
        await using (var prepare = new ApplicationDbContext(options))
        {
            prepare.Entry(await prepare.Events.SingleAsync()).Property(x => x.State).CurrentValue = EventState.SignupClosed;
            await prepare.SaveChangesAsync();
        }
        await AddPublicBoardTeamAsync(fixture);
        await using var db = new ApplicationDbContext(options);
        var other = Account.CreateWebsite(Guid.NewGuid(), "other-board-admin", "OTHER-BOARD-ADMIN", CompletionFixtureNow); other.SetGlobalRole(GlobalRole.Admin); db.Accounts.Add(other);
        var ev = await db.Events.SingleAsync(); db.Entry(ev).Property(x => x.State).CurrentValue = EventState.SignupClosed;
        db.Entry(ev).Property(x => x.EventEndsAt).CurrentValue = DateTimeOffset.UtcNow.AddDays(3);
        var board = await db.Boards.SingleAsync(); board.AcquireEditing(other.Id, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10), true); await db.SaveChangesAsync();
        var observer = Page(db, fixture.Admin.Id); var actor = Page(db, other.Id);
        var controlled = await B5BoardReadAsync(observer, ev.Id); Assert.Equal(other.Id, controlled.ControllerId);
        await actor.OnGetAsync(ev.Id, CancellationToken.None);
        await actor.OnPostApproveAsync(ev.Id, false, CancellationToken.None);
        var approved = await B5BoardReadAsync(observer, ev.Id);
        Assert.Equal(BoardState.Validated, approved.State); Assert.NotNull(approved.Approved); Assert.Null(approved.Published);
        Assert.Equal(other.Id, approved.Approved.ApprovedById);
        actor.BoardVersion = approved.Version; await actor.OnPostUnapproveAsync(ev.Id, CancellationToken.None);
        var draft = await B5BoardReadAsync(observer, ev.Id); Assert.Equal(BoardState.Draft, draft.State); Assert.Null(draft.Approved);
        await actor.OnGetAsync(ev.Id, CancellationToken.None); await actor.OnPostApproveAsync(ev.Id, false, CancellationToken.None);
        approved = await B5BoardReadAsync(observer, ev.Id);
        actor.BoardVersion = approved.Version; await actor.OnPostPublishAsync(ev.Id, true, CancellationToken.None);
        var published = await B5BoardReadAsync(observer, ev.Id);
        Assert.Equal(BoardState.Published, published.State); Assert.NotNull(published.Published);
        await actor.OnPostCorrectPublishedAsync(ev.Id, true, "Controlled correction", CancellationToken.None);
        var correcting = await B5BoardReadAsync(observer, ev.Id); Assert.True(correcting.CorrectionInProgress);
        actor.BoardVersion = correcting.Version; await actor.OnPostDiscardCorrectionAsync(ev.Id, true, CancellationToken.None);
        var discarded = await B5BoardReadAsync(observer, ev.Id);
        Assert.False(discarded.CorrectionInProgress); Assert.Equal(published.ActiveApprovalId, discarded.ActiveApprovalId);
        await actor.OnPostCorrectPublishedAsync(ev.Id, true, "Second controlled correction", CancellationToken.None);
        await actor.OnGetAsync(ev.Id, CancellationToken.None);
        await actor.OnPostApproveAsync(ev.Id, true, CancellationToken.None);
        var replacement = await B5BoardReadAsync(observer, ev.Id);
        Assert.False(replacement.CorrectionInProgress); Assert.NotEqual(discarded.ActiveApprovalId, replacement.ActiveApprovalId);
        var state = JsonSerializer.Serialize(replacement); var audits = await db.AuditEntries.CountAsync();
        Assert.Equal(state, JsonSerializer.Serialize(await B5BoardReadAsync(observer, ev.Id)));
        Assert.Equal(audits, await db.AuditEntries.CountAsync());
        Assert.Equal(3, await db.BoardApprovalSnapshots.CountAsync());
    }

    [Fact]
    public async Task B5BoardReadbackFailureIsUnknownAndUnauthorizedReadIsRefused()
    {
        var fixture = await SeedApprovalBatchAsync();
        var failingOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).AddInterceptors(new B5BoardReadFailure()).Options;
        await using (var db = new ApplicationDbContext(failingOptions))
        {
            var result = Assert.IsType<BoardModel.BoardReadback>(Assert.IsType<JsonResult>(await Page(db, fixture.Admin.Id).OnGetReadbackAsync(fixture.Event.Id, CancellationToken.None)).Value);
            Assert.False(result.Known); Assert.Null(result.State);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Empty(await verify.AuditEntries.ToListAsync()); Assert.Empty(await verify.BoardApprovalSnapshots.ToListAsync());
        (await verify.Accounts.SingleAsync()).Disable(CompletionFixtureNow); await verify.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await Page(verify, fixture.Admin.Id).OnGetReadbackAsync(fixture.Event.Id, CancellationToken.None));
    }

    private sealed class B5BoardReadFailure : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM boards", StringComparison.Ordinal)) throw new TimeoutException("Controlled read failure");
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static async Task<BoardModel.BoardCurrentState> B5BoardReadAsync(BoardModel page, Guid eventId)
    {
        var read = Assert.IsType<BoardModel.BoardReadback>(Assert.IsType<JsonResult>(await page.OnGetReadbackAsync(eventId, CancellationToken.None)).Value);
        Assert.True(read.Known); return read.State!;
    }
}
