using System.Globalization;
using System.Net;
using System.Text.Json;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Boards;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class C20ObjectiveIdentityIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscardRecoveryRestoresExactIdentitiesAfterLateEvidenceAndAllowsFreshCorrection(bool removeTile)
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        using var player = await ClientAsync(f.Owner);
        await StartCorrectionAsync(admin, f);
        if (removeTile)
        {
            await PostBoardAsync(admin, f, "Remove", new() { ["tileId"] = f.Tile.Id.ToString() });
            await CreateReplacementAsync(admin, f);
        }
        else await EditAsync(admin, f, name: "Unpublished name", target: 7);
        await PostBoardAsync(admin, f, "Resize", new() { ["Rows"] = "2", ["Columns"] = "2" });
        var submission = await SubmitAsync(player, f);
        await ReviewAsync(admin, submission, "Approve");
        var protectedState = await PublishedIntegrityAsync(f);
        var beforeRejectedPublication = await RecoveryIntegrityAsync(f);
        await PublishCorrectionAsync(admin, f);
        Assert.Equal(beforeRejectedPublication, await RecoveryIntegrityAsync(f));
        var version = await VersionAsync(f);
        await DiscardAsync(admin, f);
        await AssertRestoredAsync(f);
        Assert.Equal(protectedState, await PublishedIntegrityAsync(f));
        Assert.Equal(version + 1, await VersionAsync(f));
        await AssertOrdinaryReadsAsync(player, f, submission, f.Tile.NameSnapshot);
        await using var db = fixture.Db();
        var audit = Assert.Single(await db.AuditEntries.Where(x => x.EventId == f.Event.Id && x.Action == "board.published_correction_discarded").ToListAsync());
        Assert.Contains(f.ApprovalId.ToString(), audit.BeforeState!);
        Assert.Contains("\"confirmed\":true", audit.AfterState!);
        await StartCorrectionAsync(admin, f);
        Assert.True((await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id)).PublishedCorrectionInProgress);
        await EditAsync(admin, f, name: "Recovered wording");
        Assert.Equal(f.Requirement.Id, (await db.BoardRequirementSnapshots.AsNoTracking().SingleAsync(x => x.BoardTileId == f.Tile.Id)).Id);
        Assert.Equal(protectedState, await PublishedIntegrityAsync(f));
        await PublishCorrectionAsync(admin, f);
        Assert.NotEqual(f.ApprovalId, (await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id)).ActiveApprovalSnapshotId);
        Assert.Equal(1, (await new PublicBoardService(db, TimeProvider.System).GetTileAsync(f.Event.Slug, f.Team.Slug, f.Tile.Id))!.Approved);
    }

    [Fact]
    public async Task DiscardRecoveryUsesCurrentPublicationAndRestoresManualScoring()
    {
        var f = await SeedAsync(manual: true);
        using var admin = await ClientAsync(f.Admin);
        await StartCorrectionAsync(admin, f);
        await EditAsync(admin, f, name: "Current published title");
        await PublishCorrectionAsync(admin, f);
        await using var db = fixture.Db();
        var currentId = (await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id)).ActiveApprovalSnapshotId;
        Assert.NotEqual(f.ApprovalId, currentId);
        await StartCorrectionAsync(admin, f);
        var fields = Fields(f, "Unpublished manual title", null, 7, 1, true, null);
        fields["TileDraft.ManualEhb"] = "9";
        await PostBoardAsync(admin, f, "EditTile", fields);
        Assert.NotEqual(f.Requirement.Id, (await db.BoardRequirementSnapshots.AsNoTracking().SingleAsync(x => x.BoardTileId == f.Tile.Id)).Id);
        var protectedState = await PublishedIntegrityAsync(f);
        await DiscardAsync(admin, f);
        await AssertRestoredAsync(f);
        Assert.Equal(protectedState, await PublishedIntegrityAsync(f));
        Assert.Equal(currentId, (await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id)).ActiveApprovalSnapshotId);
        Assert.Equal("Current published title", (await db.BoardTiles.AsNoTracking().SingleAsync(x => x.Id == f.Tile.Id)).NameSnapshot);
        Assert.Equal(5m, (await db.TileTemplates.AsNoTracking().SingleAsync(x => x.Id == f.Tile.TileTemplateId)).ManualEhbOverride);
        await StartCorrectionAsync(admin, f);
        await EditAsync(admin, f, name: "Manual wording after recovery");
        Assert.Equal(f.Requirement.Id, (await db.BoardRequirementSnapshots.AsNoTracking().SingleAsync(x => x.BoardTileId == f.Tile.Id)).Id);
    }

    [Theory]
    [InlineData("unconfirmed")]
    [InlineData("stale")]
    [InlineData("unauthorized")]
    [InlineData("lease")]
    [InlineData("Finalized")]
    [InlineData("Archived")]
    [InlineData("Cancelled")]
    public async Task DiscardRecoveryDeniedRequestsPreserveAllState(string denial)
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        using var player = await ClientAsync(f.Owner);
        await StartCorrectionAsync(admin, f);
        var oldVersion = await VersionAsync(f);
        var adminToken = Token(await admin.GetStringAsync($"/Admin/Events/Board/{f.Event.Id}"));
        await EditAsync(admin, f, name: "Keep this private correction");
        await using var db = fixture.Db();
        if (Enum.TryParse<EventState>(denial, out var state))
        {
            var ev = await db.Events.SingleAsync(x => x.Id == f.Event.Id);
            db.Entry(ev).Property(x => x.State).CurrentValue = state;
            await db.SaveChangesAsync();
        }
        if (denial == "lease")
        {
            var board = await db.Boards.SingleAsync(x => x.Id == f.Board.Id);
            board.AcquireEditing(f.Owner.Id, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), force: true);
            await db.SaveChangesAsync();
        }
        var before = await RecoveryIntegrityAsync(f);
        var protectedState = await PublishedIntegrityAsync(f);
        if (denial == "unauthorized")
        {
            var token = Token(await player.GetStringAsync($"/Submissions?eventId={f.Event.Id}&teamId={f.Team.Id}"));
            using var response = await player.PostAsync($"/Admin/Events/Board/{f.Event.Id}?handler=DiscardCorrection", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["confirmed"] = "true",
                ["BoardVersion"] = (await VersionAsync(f)).ToString(CultureInfo.InvariantCulture)
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("AccessDenied", response.Headers.Location!.ToString());
        }
        else
        {
            using var response = await admin.PostAsync($"/Admin/Events/Board/{f.Event.Id}?handler=DiscardCorrection", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = adminToken,
                ["confirmed"] = (denial != "unconfirmed").ToString(),
                ["BoardVersion"] = (denial == "stale" ? oldVersion : await VersionAsync(f)).ToString(CultureInfo.InvariantCulture)
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        Assert.Equal(before, await RecoveryIntegrityAsync(f));
        Assert.Equal(protectedState, await PublishedIntegrityAsync(f));
        Assert.True((await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id)).PublishedCorrectionInProgress);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscardRecoveryMissingOrAmbiguousIdentityFailsWithoutPartialChanges(bool ambiguous)
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        await StartCorrectionAsync(admin, f);
        await EditAsync(admin, f, name: "Keep private data");
        await using var db = fixture.Db();
        var token = Token(await admin.GetStringAsync($"/Admin/Events/Board/{f.Event.Id}"));
        if (ambiguous) db.BoardRequirementDropSnapshots.Add(new(Guid.NewGuid(), f.Requirement.Id, f.Drop.SourceDropId, f.Drop.ItemIdSnapshot, f.Drop.BossName, f.Drop.ItemName, f.Drop.DisplayRate, .1m, null, 1m));
        else db.BoardRequirementDropSnapshots.Remove(await db.BoardRequirementDropSnapshots.SingleAsync(x => x.Id == f.Drop.Id));
        await db.SaveChangesAsync();
        var before = await RecoveryIntegrityAsync(f);
        using var response = await admin.PostAsync($"/Admin/Events/Board/{f.Event.Id}?handler=DiscardCorrection", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["confirmed"] = "true",
            ["BoardVersion"] = (await VersionAsync(f)).ToString(CultureInfo.InvariantCulture)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(before, await RecoveryIntegrityAsync(f));
        Assert.True((await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id)).PublishedCorrectionInProgress);
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("missing-bytes")]
    [InlineData("unreadable")]
    [InlineData("r2-missing")]
    [InlineData("retained")]
    public async Task DiscardRecoveryArtworkRequiresExactRetainedAssetAndBytes(string scenario)
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        await using var db = fixture.Db();
        var originalImage = new BoardTileImageAsset(Guid.NewGuid(), f.Event.Id, f.Tile.Id, "c20/artwork/" + f.Tile.Id, "fixture.png", "image/png", 3, 1, 1, new string('0', 64), f.Admin.Id, DateTimeOffset.UtcNow);
        db.BoardTileImageAssets.Add(originalImage);
        (await db.BoardTiles.SingleAsync(x => x.Id == f.Tile.Id)).SetActiveImageAsset(originalImage.Id);
        var approvalTile = await db.BoardApprovalTileSnapshots.SingleAsync(x => x.ApprovalSnapshotId == f.ApprovalId);
        db.Entry(approvalTile).Property(x => x.ArtworkReference).CurrentValue = originalImage.StorageKey;
        await db.SaveChangesAsync(); // Controlled fixture's immutable publication is now complete.
        await StartCorrectionAsync(admin, f);
        if (scenario == "removed")
        {
            // Combined C20/C21 behavior retains approval artwork when the working tile is removed.
            await PostBoardAsync(admin, f, "Remove", new() { ["tileId"] = f.Tile.Id.ToString() });
            await CreateReplacementAsync(admin, f);
            Assert.True(await db.BoardTileImageAssets.AsNoTracking().AnyAsync(x => x.Id == originalImage.Id));
            Assert.DoesNotContain(originalImage.StorageKey, fixture.Storage.DeletedKeys);
        }
        else
        {
            var fields = Fields(f, "Private image removal", null, 5, 1, true, null);
            fields["TileDraft.RemoveImage"] = "true";
            await PostBoardAsync(admin, f, "EditTile", fields);
        }
        if (scenario == "missing-bytes") fixture.Storage.MissingKeys.Add(originalImage.StorageKey);
        if (scenario == "unreadable") fixture.Storage.ReadFailures.Add(originalImage.StorageKey, new UnauthorizedAccessException("Controlled unreadable artwork"));
        if (scenario == "r2-missing") fixture.Storage.ReadFailures.Add(originalImage.StorageKey, new Amazon.S3.AmazonS3Exception("Controlled missing artwork", Amazon.Runtime.ErrorType.Sender, "NoSuchKey", "controlled-request", HttpStatusCode.NotFound));
        var before = await RecoveryIntegrityAsync(f);
        var protectedState = await PublishedIntegrityAsync(f);
        try { await DiscardAsync(admin, f); }
        finally { fixture.Storage.MissingKeys.Remove(originalImage.StorageKey); fixture.Storage.ReadFailures.Remove(originalImage.StorageKey); }
        Assert.Equal(protectedState, await PublishedIntegrityAsync(f));
        if (scenario is not ("retained" or "removed"))
        {
            Assert.Equal(before, await RecoveryIntegrityAsync(f));
            Assert.Contains("could not be discarded", await admin.GetStringAsync($"/Admin/Events/Board/{f.Event.Id}"));
            Assert.True((await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id)).PublishedCorrectionInProgress);
        }
        else
        {
            await AssertRestoredAsync(f);
            Assert.DoesNotContain(originalImage.StorageKey, fixture.Storage.DeletedKeys);
            Assert.False((await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id)).PublishedCorrectionInProgress);
            Assert.Equal(originalImage.Id, (await db.BoardTiles.AsNoTracking().SingleAsync(x => x.Id == f.Tile.Id)).ActiveImageAssetId);
            Assert.Null((await db.BoardTileImageAssets.AsNoTracking().SingleAsync(x => x.Id == originalImage.Id)).ReplacedAt);
            using var image = await admin.GetAsync($"/Admin/Events/Board/{f.Event.Id}?handler=TileImage&tileId={f.Tile.Id}");
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal(new byte[] { 1, 2, 3 }, await image.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task DiscardRecoveryPersistenceFailureRollsBackAllWritesAndRetrySucceeds()
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        await StartCorrectionAsync(admin, f);
        await EditAsync(admin, f, target: 7);
        var before = await RecoveryIntegrityAsync(f);
        var protectedState = await PublishedIntegrityAsync(f);
        await using var db = fixture.Db();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION c20_fail_discard() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW.action = 'board.published_correction_discarded' THEN
                    RAISE EXCEPTION 'controlled C20 discard rollback' USING ERRCODE = '40001';
                END IF; RETURN NEW;
            END $$;
            CREATE TRIGGER c20_fail_discard BEFORE INSERT ON audit_entries FOR EACH ROW EXECUTE FUNCTION c20_fail_discard();
            """);
        try { await DiscardAsync(admin, f); }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER c20_fail_discard ON audit_entries; DROP FUNCTION c20_fail_discard();"); }
        Assert.Equal(before, await RecoveryIntegrityAsync(f));
        Assert.Equal(protectedState, await PublishedIntegrityAsync(f));
        await DiscardAsync(admin, f);
        await AssertRestoredAsync(f);
        Assert.Equal(protectedState, await PublishedIntegrityAsync(f));
        var after = await RecoveryIntegrityAsync(f);
        await DiscardAsync(admin, f); // A repeated confirmed POST cannot discard a closed correction.
        Assert.Equal(after, await RecoveryIntegrityAsync(f));
    }

    [Fact]
    public async Task DiscardRecoveryConfirmationRendersOnlyForEditingOpenCorrection()
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        var path = $"/Admin/Events/Board/{f.Event.Id}";
        // A10: was DoesNotContain/Contains on the server-rendered discard markup, the
        // "All unpublished board edits in this correction will be lost." text and
        // "handler=DiscardCorrection". The module offers "Discard correction…" only for
        // data-view mode "correction" (admin-board-publication.js menuItems), enabled for the
        // editing admin (control.who == "me"), and confirms with the served label below.
        const string confirmation = "All unpublished edits in this correction will be lost, and the working board returns to the published version.";
        var closed = await admin.GetStringAsync(path);
        Assert.NotEqual("correction", BoardPageData.View(closed).GetProperty("mode").GetString());
        await StartCorrectionAsync(admin, f);
        var before = await RecoveryIntegrityAsync(f);
        var html = await admin.GetStringAsync(path);
        var open = BoardPageData.View(html);
        Assert.Equal("correction", open.GetProperty("mode").GetString());
        Assert.Equal("me", open.GetProperty("control").GetProperty("who").GetString());
        Assert.Equal(confirmation, BoardPageData.Labels(html).GetProperty(confirmation).GetString());
        Assert.Equal(before, await RecoveryIntegrityAsync(f));
        if (Environment.GetEnvironmentVariable("C20_RECOVERY_BROWSER_EXPORT") is { } export)
        {
            Directory.CreateDirectory(export);
            await File.WriteAllTextAsync(Path.Combine(export, "editor.html"), html);
            await File.WriteAllTextAsync(Path.Combine(export, "version.txt"), (await VersionAsync(f)).ToString(CultureInfo.InvariantCulture));
        }
        await DiscardAsync(admin, f);
        Assert.NotEqual("correction", BoardPageData.View(await admin.GetStringAsync(path)).GetProperty("mode").GetString());
    }

    private Task DiscardAsync(HttpClient client, Fixture f) => PostBoardAsync(client, f, "DiscardCorrection", new() { ["confirmed"] = "true" });

    private async Task AssertRestoredAsync(Fixture f)
    {
        await using var db = fixture.Db();
        var board = await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id);
        var publication = (await db.PublishedObjectivesAsync(f.Event.Id))!;
        Assert.False(board.PublishedCorrectionInProgress);
        Assert.Null(board.EditorAccountId); Assert.Null(board.EditorLeaseExpiresAt);
        Assert.Equal(publication.Approval.Rows, board.Rows); Assert.Equal(publication.Approval.Columns, board.Columns);
        Assert.Equal(publication.Approval.TotalEhbEstimate, board.TotalEhbEstimate);
        var artwork = await db.BoardApprovalTileSnapshots.AsNoTracking().Where(x => x.ApprovalSnapshotId == publication.Approval.Id && x.ArtworkReference != null).ToDictionaryAsync(x => x.BoardTileId, x => x.ArtworkReference);
        foreach (var tile in publication.Tiles)
            if (artwork.TryGetValue(tile.Id, out var key))
                tile.SetActiveImageAsset(await db.BoardTileImageAssets.Where(x => x.EventId == f.Event.Id && x.BoardTileId == tile.Id && x.StorageKey == key).Select(x => x.Id).SingleAsync());
        Assert.Equal(JsonSerializer.Serialize(publication.Tiles.OrderBy(x => x.Id)), JsonSerializer.Serialize(await db.BoardTiles.AsNoTracking().Where(x => x.BoardId == board.Id).OrderBy(x => x.Id).ToListAsync()));
        var tileIds = publication.Tiles.Select(x => x.Id).ToList();
        Assert.Equal(JsonSerializer.Serialize(publication.Requirements.OrderBy(x => x.Id)), JsonSerializer.Serialize(await db.BoardRequirementSnapshots.AsNoTracking().Where(x => tileIds.Contains(x.BoardTileId)).OrderBy(x => x.Id).ToListAsync()));
        var ids = publication.Drops.Select(x => x.Id).ToList();
        // Rate mechanics are intentionally approval-only, unmapped projection values.
        // Compare every persisted working-drop value; published history is checked separately.
        static object WorkingDropState(BoardRequirementDropSnapshot drop) => new
        {
            drop.Id,
            drop.RequirementId,
            drop.SourceDropId,
            drop.ItemIdSnapshot,
            drop.BossName,
            drop.ItemName,
            drop.DisplayRate,
            drop.NumericProbability,
            drop.MaximumContribution,
            drop.EhbPerContribution,
            drop.CreditedWeight
        };
        Assert.Equal(JsonSerializer.Serialize(publication.Drops.OrderBy(x => x.Id).Select(WorkingDropState)),
            JsonSerializer.Serialize((await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync()).Select(WorkingDropState)));
    }

    private async Task<string> RecoveryIntegrityAsync(Fixture f)
    {
        await using var db = fixture.Db();
        var tileIds = await db.BoardTiles.Where(x => x.BoardId == f.Board.Id).Select(x => x.Id).ToListAsync();
        tileIds.Add(f.Tile.Id);
        var reqIds = await db.BoardRequirementSnapshots.Where(x => tileIds.Contains(x.BoardTileId)).Select(x => x.Id).ToListAsync();
        reqIds.Add(f.Requirement.Id);
        var templateIds = await db.BoardTiles.Where(x => tileIds.Contains(x.Id)).Select(x => x.TileTemplateId).ToListAsync();
        templateIds.Add(f.Tile.TileTemplateId);
        var templateReqs = await db.TileTemplateRequirements.AsNoTracking().Where(x => templateIds.Contains(x.TileTemplateId)).OrderBy(x => x.Id).ToListAsync();
        var templateReqIds = templateReqs.Select(x => x.Id).ToList();
        return JsonSerializer.Serialize(new
        {
            Core = await IntegrityAsync(f),
            Requirements = await db.BoardRequirementSnapshots.AsNoTracking().Where(x => tileIds.Contains(x.BoardTileId)).OrderBy(x => x.Id).ToListAsync(),
            Bosses = await db.BoardRequirementBossSnapshots.AsNoTracking().Where(x => reqIds.Contains(x.RequirementId)).OrderBy(x => x.Id).ToListAsync(),
            Drops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => reqIds.Contains(x.RequirementId)).OrderBy(x => x.Id).ToListAsync(),
            Images = await db.BoardTileImageAssets.AsNoTracking().Where(x => x.EventId == f.Event.Id).OrderBy(x => x.Id).ToListAsync(),
            Templates = await db.TileTemplates.AsNoTracking().Where(x => templateIds.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(),
            TemplateRequirements = templateReqs,
            TemplateBosses = await db.TemplateRequirementBosses.AsNoTracking().Where(x => templateReqIds.Contains(x.RequirementId)).OrderBy(x => x.Id).ToListAsync(),
            TemplateDrops = await db.TemplateRequirementDrops.AsNoTracking().Where(x => templateReqIds.Contains(x.RequirementId)).OrderBy(x => x.Id).ToListAsync()
        });
    }

    private async Task<string> PublishedIntegrityAsync(Fixture f)
    {
        await using var db = fixture.Db();
        var board = await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id);
        var approvalIds = await db.BoardApprovalSnapshots.Where(x => x.BoardId == board.Id).Select(x => x.Id).ToListAsync();
        var tiles = await db.BoardApprovalTileSnapshots.AsNoTracking().Where(x => approvalIds.Contains(x.ApprovalSnapshotId)).OrderBy(x => x.Id).ToListAsync();
        var tileIds = tiles.Select(x => x.Id).ToList();
        var requirements = await db.BoardApprovalRequirementSnapshots.AsNoTracking().Where(x => tileIds.Contains(x.ApprovalTileSnapshotId)).OrderBy(x => x.Id).ToListAsync();
        var ids = requirements.Select(x => x.Id).ToList();
        var submissions = await db.Submissions.AsNoTracking().Where(x => x.EventId == f.Event.Id).OrderBy(x => x.Id).ToListAsync();
        var submissionIds = submissions.Select(x => x.Id).ToList();
        return JsonSerializer.Serialize(new
        {
            board.ActiveApprovalSnapshotId,
            board.PublishedAt,
            board.State,
            Event = await db.Events.AsNoTracking().SingleAsync(x => x.Id == f.Event.Id),
            Approvals = await db.BoardApprovalSnapshots.AsNoTracking().Where(x => approvalIds.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(),
            Tiles = tiles,
            Requirements = requirements,
            Bosses = await db.BoardApprovalRequirementBossSnapshots.AsNoTracking().Where(x => ids.Contains(x.ApprovalRequirementSnapshotId)).OrderBy(x => x.Id).ToListAsync(),
            Drops = await db.BoardApprovalRequirementDropSnapshots.AsNoTracking().Where(x => ids.Contains(x.ApprovalRequirementSnapshotId)).OrderBy(x => x.Id).ToListAsync(),
            Submissions = submissions,
            Contributions = await db.SubmissionContributions.AsNoTracking().Where(x => submissionIds.Contains(x.SubmissionId)).OrderBy(x => x.Id).ToListAsync(),
            History = await db.ReviewActions.AsNoTracking().Where(x => submissionIds.Contains(x.SubmissionId)).OrderBy(x => x.Id).ToListAsync()
        });
    }
}
