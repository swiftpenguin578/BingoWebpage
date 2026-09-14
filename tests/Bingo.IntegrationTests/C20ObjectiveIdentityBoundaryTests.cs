using System.Data;
using System.Globalization;
using System.Net;
using Bingo.Domain.Boards;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Evidence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.IntegrationTests;

public sealed partial class C20ObjectiveIdentityIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubmissionHoldingPublicationLockWinsAgainstIncompatibleEditOrReplacement(bool publish)
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        await StartCorrectionAsync(admin, f);
        if (publish) await EditAsync(admin, f, target: 7);
        var before = await IntegrityAsync(f);
        var path = $"/Admin/Events/Board/{f.Event.Id}";
        var fields = publish ? new Dictionary<string, string> { ["confirmed"] = "true" } : Fields(f, null, null, 7, 1, true, null);
        fields["__RequestVerificationToken"] = Token(await admin.GetStringAsync(path));
        fields["BoardVersion"] = (await VersionAsync(f)).ToString(CultureInfo.InvariantCulture);
        var stored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Storage.BeforeStore = async eventId => { if (eventId == f.Event.Id) { stored.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(20)); } };
        await using var submitDb = fixture.Db();
        var submissionTask = new SubmissionService(submitDb, fixture.Storage, TimeProvider.System).CreateAsync(Command(f));
        await stored.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var mutation = admin.PostAsync(path + "?handler=" + (publish ? "Approve" : "EditTile"), new FormUrlEncodedContent(fields));
        try
        {
            await AssertDatabaseLockWaitAsync();
            Assert.False(mutation.IsCompleted);
        }
        finally { release.TrySetResult(); fixture.Storage.BeforeStore = null; }
        var submission = await submissionTask;
        using var outcome = await mutation;
        Assert.Equal(HttpStatusCode.Redirect, outcome.StatusCode);
        await using var db = fixture.Db();
        var board = await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id);
        Assert.Equal(f.ApprovalId, board.ActiveApprovalSnapshotId);
        Assert.True(board.PublishedCorrectionInProgress);
        Assert.Single(await db.BoardApprovalSnapshots.Where(x => x.BoardId == f.Board.Id).ToListAsync());
        Assert.Equal(publish ? 7 : 5, (await db.BoardRequirementSnapshots.SingleAsync(x => x.BoardTileId == f.Tile.Id)).TargetContribution);
        var saved = await db.Submissions.SingleAsync(x => x.Id == submission.SubmissionId);
        Assert.Equal(f.Requirement.Id, saved.RequirementId); Assert.Equal(f.Drop.Id, saved.DropSnapshotId);
        using var player = await ClientAsync(f.Owner);
        await AssertOrdinaryReadsAsync(player, f, saved.Id, f.Tile.NameSnapshot);
        Assert.Contains(f.ApprovalId.ToString(), before);
    }

    [Fact]
    public async Task ReplacementPublicationFailureRollsBackItsEntireSnapshotAndPointer()
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        using var player = await ClientAsync(f.Owner);
        var submission = await SubmitAsync(player, f);
        await StartCorrectionAsync(admin, f);
        await EditAsync(admin, f, name: "Safe private correction");
        var before = await IntegrityAsync(f);
        await using var db = fixture.Db();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION c20_fail_approval() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                RAISE EXCEPTION 'controlled C20 approval rollback' USING ERRCODE = '40001';
            END $$;
            CREATE TRIGGER c20_fail_approval BEFORE INSERT ON board_approval_snapshots FOR EACH ROW EXECUTE FUNCTION c20_fail_approval();
            """);
        try { await PublishCorrectionAsync(admin, f); }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER c20_fail_approval ON board_approval_snapshots; DROP FUNCTION c20_fail_approval();"); }
        Assert.Equal(before, await IntegrityAsync(f));
        Assert.Single(await db.BoardApprovalTileSnapshots.Where(x => x.BoardTileId == f.Tile.Id).ToListAsync());
        Assert.Single(await db.BoardApprovalRequirementSnapshots.Where(x => x.BoardRequirementSnapshotId == f.Requirement.Id).ToListAsync());
        await AssertOrdinaryReadsAsync(player, f, submission, f.Tile.NameSnapshot);
        await PublishCorrectionAsync(admin, f);
        Assert.NotEqual(f.ApprovalId, (await db.Boards.AsNoTracking().SingleAsync(x => x.Id == f.Board.Id)).ActiveApprovalSnapshotId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrAmbiguousRetainedIdentityFailsClosed(bool ambiguous)
    {
        var f = await SeedAsync();
        await using var db = fixture.Db();
        if (ambiguous) db.BoardRequirementDropSnapshots.Add(new(Guid.NewGuid(), f.Requirement.Id, f.Drop.SourceDropId, f.Drop.ItemIdSnapshot, f.Drop.BossName, f.Drop.ItemName, f.Drop.DisplayRate, .1m, null, 1m));
        else db.BoardRequirementDropSnapshots.Remove(await db.BoardRequirementDropSnapshots.SingleAsync(x => x.Id == f.Drop.Id));
        await db.SaveChangesAsync();
        Assert.Null(await db.PublishedObjectivesAsync(f.Event.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SubmissionService(db, fixture.Storage, TimeProvider.System).CreateAsync(Command(f)));
        Assert.False(await db.Submissions.AnyAsync(x => x.EventId == f.Event.Id));
        using var player = await ClientAsync(f.Owner);
        using var drawer = await player.GetAsync($"/Captain/Submit/{f.Tile.Id}?handler=Drawer&eventId={f.Event.Id}&teamId={f.Team.Id}");
        Assert.Equal(HttpStatusCode.NotFound, drawer.StatusCode);
    }

    [Fact]
    public async Task ImmutablePublicationRulesIgnoreCachedDropAndCatalogueChanges()
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        using var player = await ClientAsync(f.Owner);
        var first = await SubmitAsync(player, f);
        await ReviewAsync(admin, first, "Approve");
        await using var db = fixture.Db();
        var boss = await db.BossActivities.SingleAsync(x => x.Id == f.Boss.Id);
        boss.Update(boss.Name, boss.Category, 20m, null, null, null, DateTimeOffset.UtcNow);
        var source = await db.SourceDrops.SingleAsync(x => x.Id == f.Drop.SourceDropId);
        source.Update("1/100", .01m, null, 100m, null, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        await StartCorrectionAsync(admin, f);
        await EditAsync(admin, f, name: "Wording after catalogue change");
        await PublishCorrectionAsync(admin, f);
        var publication = (await db.PublishedObjectivesAsync(f.Event.Id))!;
        Assert.Equal(5m, Assert.Single(publication.Tiles).EstimatedEhbSnapshot);
        Assert.Equal(.1m, Assert.Single(publication.Drops).NumericProbability);
        var bridge = await db.BoardRequirementDropSnapshots.SingleAsync(x => x.Id == f.Drop.Id);
        db.Entry(bridge).Property(x => x.CreditedWeight).CurrentValue = 9;
        db.Entry(bridge).Property(x => x.MaximumContribution).CurrentValue = 0;
        await db.SaveChangesAsync();
        var second = await SubmitAsync(player, f);
        await ReviewAsync(admin, second, "Approve");
        var saved = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == second);
        Assert.Equal(1, saved.ClaimedWeight); Assert.Equal(1, saved.ApprovedContribution);
        var boardView = (await new PublicBoardService(db, TimeProvider.System).GetEventBoardAsync(f.Event.Slug))!;
        Assert.Equal(2, Assert.Single(Assert.Single(boardView.Teams).Tiles).Approved);
    }

    [Fact]
    public async Task RetargetedPendingEvidenceStillLocksItsOriginalObjectiveAndEditorRoundTripsSeparateIdentities()
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        using var player = await ClientAsync(f.Owner);
        await StartCorrectionAsync(admin, f);
        var fields = Fields(f, null, null, 5, 1, true, null);
        foreach (var pair in Fields(f, null, null, 5, 1, true, null).Where(x => x.Key.Contains("Requirements[0]", StringComparison.Ordinal)))
            fields[pair.Key.Replace("Requirements[0]", "Requirements[1]", StringComparison.Ordinal)] = pair.Value;
        fields["TileDraft.Requirements[1].RequirementId"] = "";
        await PostBoardAsync(admin, f, "EditTile", fields);
        await using var db = fixture.Db();
        var second = await db.BoardRequirementSnapshots.AsNoTracking().SingleAsync(x => x.BoardTileId == f.Tile.Id && x.Id != f.Requirement.Id);
        var secondDrop = await db.BoardRequirementDropSnapshots.AsNoTracking().SingleAsync(x => x.RequirementId == second.Id);
        var html = await admin.GetStringAsync($"/Admin/Events/Board/{f.Event.Id}");
        Assert.Contains(second.Id.ToString(), html); Assert.Contains(f.Requirement.Id.ToString(), html);
        if (Environment.GetEnvironmentVariable("C20_BROWSER_EXPORT") is { } export)
        {
            Directory.CreateDirectory(export);
            await File.WriteAllTextAsync(Path.Combine(export, "editor.html"), html);
            await File.WriteAllTextAsync(Path.Combine(export, "editor-identities.json"), System.Text.Json.JsonSerializer.Serialize(new { TileId = f.Tile.Id, RequirementIds = new[] { f.Requirement.Id, second.Id } }));
        }
        await PublishCorrectionAsync(admin, f);
        var id = await SubmitAsync(player, f);
        await new SubmissionService(db, fixture.Storage, TimeProvider.System).CorrectAsync(new(id, f.Owner.Id, f.Tile.Id, second.Id, secondDrop.Id, f.Participant.Id, 1, "Controlled retarget", 1));
        Assert.Equal(second.Id, (await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == id)).RequirementId);
        Assert.Contains(f.Requirement.Id, await db.EvidencedObjectiveIdsAsync(f.Event.Id));
        await StartCorrectionAsync(admin, f);
        fields["TileDraft.Requirements[0].Target"] = "7";
        fields["TileDraft.Requirements[1].RequirementId"] = second.Id.ToString();
        var before = await IntegrityAsync(f);
        await PostBoardAsync(admin, f, "EditTile", fields);
        Assert.Equal(before, await IntegrityAsync(f));
    }

    [Fact]
    public async Task ManualWordingPreservesEvidenceWhileManualScoringChangesAreBlocked()
    {
        var f = await SeedAsync(manual: true);
        using var admin = await ClientAsync(f.Admin);
        using var player = await ClientAsync(f.Owner);
        var id = await SubmitAsync(player, f);
        await ReviewAsync(admin, id, "Approve");
        await StartCorrectionAsync(admin, f);
        var fields = Fields(f, "Clearer manual title", null, 5, 1, true, null);
        fields["TileDraft.Requirements[0].Description"] = "Complete 5 runs";
        await PostBoardAsync(admin, f, "EditTile", fields);
        var before = await IntegrityAsync(f);
        fields["TileDraft.ManualEhb"] = "6";
        await PostBoardAsync(admin, f, "EditTile", fields);
        Assert.Equal(before, await IntegrityAsync(f));
        await PublishCorrectionAsync(admin, f);
        await using var db = fixture.Db();
        var publication = (await db.PublishedObjectivesAsync(f.Event.Id))!;
        Assert.Equal(f.Requirement.Id, Assert.Single(publication.Requirements).Id);
        Assert.Equal("Complete 5 runs", Assert.Single(publication.Requirements).Description);
        Assert.Equal(5m, Assert.Single(publication.Tiles).EstimatedEhbSnapshot);
        Assert.Equal(1, (await db.Submissions.SingleAsync(x => x.Id == id)).ApprovedContribution);
        await AssertOrdinaryReadsAsync(player, f, id, "Clearer manual title");
    }

    [Fact]
    public async Task PublicationHoldingBoardLockRejectsAnOldSubmissionWithoutPartialEvidence()
    {
        var f = await SeedAsync();
        using var admin = await ClientAsync(f.Admin);
        await StartCorrectionAsync(admin, f);
        await EditAsync(admin, f, target: 7);
        await using var blocker = fixture.Db();
        await blocker.Database.OpenConnectionAsync();
        await blocker.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock(2020, 1)");
        await blocker.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION c20_pause_approval() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                PERFORM pg_advisory_xact_lock(2020, 1); RETURN NEW;
            END $$;
            CREATE TRIGGER c20_pause_approval BEFORE INSERT ON board_approval_snapshots FOR EACH ROW EXECUTE FUNCTION c20_pause_approval();
            """);
        var publishing = PublishCorrectionAsync(admin, f);
        Task<Bingo.Application.Evidence.SubmissionResult>? submitting = null;
        await using var submitDb = fixture.Db();
        try
        {
            await AssertDatabaseLockWaitAsync();
            submitting = new SubmissionService(submitDb, fixture.Storage, TimeProvider.System).CreateAsync(Command(f));
            await AssertDatabaseLockWaitAsync(2);
        }
        finally { await blocker.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(2020, 1)"); }
        try
        {
            await publishing;
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => submitting!);
            Assert.Contains("board", failure.Message, StringComparison.OrdinalIgnoreCase);
            await using var db = fixture.Db();
            Assert.NotEqual(f.ApprovalId, (await db.Boards.SingleAsync(x => x.Id == f.Board.Id)).ActiveApprovalSnapshotId);
            Assert.False(await db.Submissions.AnyAsync(x => x.EventId == f.Event.Id));
            Assert.False(await db.EvidenceAssets.AnyAsync(x => x.StorageKey.StartsWith("c20/" + f.Event.Id)));
        }
        finally { await blocker.Database.ExecuteSqlRawAsync("DROP TRIGGER c20_pause_approval ON board_approval_snapshots; DROP FUNCTION c20_pause_approval();"); }
    }

    private async Task AssertDatabaseLockWaitAsync(int minimum = 1)
    {
        await using var db = fixture.Db();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var waiting = await db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'").SingleAsync(timeout.Token);
            if (waiting >= minimum) return;
            await Task.Delay(25, timeout.Token);
        }
    }
}
