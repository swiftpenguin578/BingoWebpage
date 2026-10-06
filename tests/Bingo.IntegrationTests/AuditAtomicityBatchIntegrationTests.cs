using System.Security.Claims;
using System.Text.Json;
using Bingo.Application.Evidence;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Auditing;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Infrastructure.Signups;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class AuditAtomicityBatchIntegrationTests(PostgreSqlTestFixture databaseFixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private readonly PostgreSqlTestDatabase database = databaseFixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("audit_atomicity_batch").WithUsername("bingo").WithPassword("bingo_test_password"));
    private readonly DateTimeOffset now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        // A real PostgreSQL audit INSERT fails, including when it shares an EF batch
        // with business writes. Auto-unapproval must reach the database before the
        // mandatory removal audit fails, proving that both roll back together.
        // Only this disposable database owns the trigger.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_test_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.actor_username = 'audit-failure' AND NEW.action <> 'board.auto_unapproved' THEN
                    RAISE EXCEPTION 'Injected audit persistence failure' USING ERRCODE = 'P0001';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER reject_test_audit BEFORE INSERT ON audit_entries
                FOR EACH ROW EXECUTE FUNCTION reject_test_audit();
            """);
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Theory]
    [InlineData("enable")]
    [InlineData("disable")]
    [InlineData("replace")]
    [InlineData("retain")]
    public async Task SignupCodeAndSafeAuditCommitTogether(string operation)
    {
        var setup = await SeedAsync();
        var hasher = new SecretHasher();
        await using (var db = new ApplicationDbContext(options))
        {
            if (operation != "enable")
            {
                var hash = hasher.Hash("original-test-code");
                (await db.Events.SingleAsync()).ConfigureSignup(true, true, hash);
                (await db.SignupForms.SingleAsync()).ConfigureSignupCode(true, hash);
                await db.SaveChangesAsync();
            }
        }
        var baseline = await PersistedStateAsync();
        long eventVersion;
        await using (var versionDb = new ApplicationDbContext(options))
            eventVersion = await versionDb.Events.Select(item => item.Version).SingleAsync();
        var settings = new ParticipantsModel.SignupCodeInput
        {
            RequireSignupCode = operation != "disable",
            NewSignupCode = operation is "enable" or "replace" ? "replacement-test-code" : null,
            Version = eventVersion
        };
        var observer = new AuditFailureObserver();
        await using (var db = FailureContext(observer))
        {
            var page = Context(new ParticipantsModel(db, null!, new AuditWriter(db, new Clock(now)), hasher), setup.AdminId, true);
            page.SignupCode = settings;
            await Record.ExceptionAsync(() => page.OnPostSignupCodeAsync(setup.EventId, default));
        }
        Assert.True(observer.SawAuditFailure);
        Assert.Equal(baseline, await PersistedStateAsync());
        await using (var db = new ApplicationDbContext(options))
        {
            var page = Context(new ParticipantsModel(db, null!, new AuditWriter(db, new Clock(now)), hasher), setup.AdminId);
            page.SignupCode = settings;
            Assert.IsType<RedirectToPageResult>(await page.OnPostSignupCodeAsync(setup.EventId, default));
        }
        await using var verify = new ApplicationDbContext(options);
        var form = await verify.SignupForms.SingleAsync();
        var ev = await verify.Events.SingleAsync();
        var audit = await AuditAsync(verify, setup, "event.signup_code_changed");
        Assert.Equal(operation != "enable", audit.GetProperty("before").GetProperty("RequireSignupCode").GetBoolean());
        Assert.Equal(settings.RequireSignupCode, audit.GetProperty("after").GetProperty("RequireSignupCode").GetBoolean());
        Assert.Equal(operation is "enable" or "replace", audit.GetProperty("codeReplaced").GetBoolean());
        Assert.Equal(settings.RequireSignupCode, form.RequireSignupCode);
        Assert.Equal(form.RequireSignupCode, ev.RequireSignupCode);
        Assert.Equal(form.SignupCodeHash, ev.SignupCodeHash);
        if (settings.RequireSignupCode)
        {
            Assert.True(hasher.Verify(operation == "retain" ? "original-test-code" : "replacement-test-code", form.SignupCodeHash!));
            Assert.DoesNotContain(form.SignupCodeHash!, audit.GetRawText());
        }
        else Assert.Null(form.SignupCodeHash);
        Assert.DoesNotContain("test-code", audit.GetRawText());
        Assert.DoesNotContain("Hash", audit.GetRawText());
    }

    [Fact]
    public async Task SignupCodeConcurrencyFailureReturnsStaleStatusWithoutPersisting()
    {
        var setup = await SeedAsync();
        long version;
        await using (var versionDb = new ApplicationDbContext(options))
            version = await versionDb.Events.Where(item => item.Id == setup.EventId).Select(item => item.Version).SingleAsync();

        var observer = new ConcurrencyFailureObserver();
        await using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(observer).Options))
        {
            var page = Context(new ParticipantsModel(db, null!, new AuditWriter(db, new Clock(now)), new SecretHasher()), setup.AdminId);
            page.SignupCode = new ParticipantsModel.SignupCodeInput { RequireSignupCode = true, NewSignupCode = "concurrency-code", Version = version };
            Assert.IsType<RedirectToPageResult>(await page.OnPostSignupCodeAsync(setup.EventId, default));
            Assert.Contains("changed while you were editing it", page.TempData["StatusMessage"]?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.False(await verify.Events.Where(item => item.Id == setup.EventId).Select(item => item.RequireSignupCode).SingleAsync());
        Assert.Empty(await verify.AuditEntries.Where(item => item.EventId == setup.EventId && item.Action == "event.signup_code_changed").ToListAsync());
    }

    [Theory]
    [InlineData("update", "team.updated")]
    [InlineData("remove", "draft.team_removed")]
    [InlineData("start", "draft.started")]
    [InlineData("scramble", "draft.order_scrambled")]
    [InlineData("pick", "draft.pick_recorded")]
    [InlineData("undo", "draft.pick_undone")]
    [InlineData("acquire", "draft.control_acquired")]
    [InlineData("takeover", "draft.control_taken_over")]
    [InlineData("release", "draft.control_released")]
    public async Task DraftAndTeamMutationsRollbackOnAuditInsertFailure(string operation, string action)
    {
        var setup = await SeedAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            var draft = await db.DraftSessions.SingleAsync();
            if (operation is not ("start" or "acquire")) draft.AcquireControl(operation == "takeover" ? setup.OtherAdminId : setup.AdminId, now, DraftControlLease.Duration);
            if (operation is "scramble" or "pick" or "undo")
            {
                draft.Start(now);
                (await db.Events.SingleAsync()).SetDraftLocked(true);
                var teams = await db.Teams.Where(x => x.Active).OrderBy(x => x.Name).ToListAsync();
                for (var i = 0; i < teams.Count; i++) teams[i].SetDraftPosition(i + 1);
            }
            if (operation == "remove") db.TeamMemberships.RemoveRange(await db.TeamMemberships.Where(x => x.TeamId == setup.TeamId).ToListAsync());
            if (operation == "undo")
            {
                var pick = new DraftPick(Guid.NewGuid(), draft.Id, setup.TeamId, setup.PickParticipantId, 1, 1, now);
                var membership = new TeamMembership(Guid.NewGuid(), setup.TeamId, setup.PickParticipantId, TeamMembershipRole.Participant, now, pick.Id, "Fixture pick");
                membership.SetSource(TeamMembershipSource.DraftPick);
                draft.RecordFirstPick(now);
                db.AddRange(pick, membership);
            }
            await db.SaveChangesAsync();
        }
        var baseline = await PersistedStateAsync();
        var observer = new AuditFailureObserver();
        var storage = new MemoryStorage();
        var notifier = new Notifier();
        async Task Execute(ApplicationDbContext db, bool fail)
        {
            var page = Context(new DraftModel(db, new Clock(now), new AuditWriter(db, new Clock(now)), notifier, null!, new EventParticipantCharacterService(db, new Clock(now)), storage), setup.AdminId, fail);
            switch (operation)
            {
                case "update": await page.OnPostUpdateTeamAsync(setup.EventId, setup.TeamId, "Renamed", "New affiliation", Upload(), false, (await db.Teams.FindAsync(setup.TeamId))!.Version, default); break;
                case "remove": await page.OnPostRemoveDraftTeamAsync(setup.EventId, setup.TeamId, default); break;
                case "start": await page.OnPostStartAsync(setup.EventId, default); break;
                case "scramble": await page.OnPostScrambleAsync(setup.EventId, default); break;
                case "pick": await page.OnPostPickAsync(setup.EventId, setup.PickParticipantId, default); break;
                case "undo": await page.OnPostUndoAsync(setup.EventId, default); break;
                case "acquire": await page.OnPostAcquireControlAsync(setup.EventId, default); break;
                case "takeover": await page.OnPostTakeControlAsync(setup.EventId, true, default); break;
                case "release": await page.OnPostReleaseControlAsync(setup.EventId, default); break;
            }
        }
        Exception? failure;
        await using (var db = FailureContext(observer)) failure = await Record.ExceptionAsync(() => Execute(db, true));
        Assert.True(observer.SawAuditFailure, failure?.ToString());
        Assert.Equal(baseline, await PersistedStateAsync());
        Assert.Empty(notifier.Events);
        Assert.Empty(storage.Keys);
        await using (var db = new ApplicationDbContext(options)) await Execute(db, false);
        await using var verify = new ApplicationDbContext(options);
        var audit = await AuditAsync(verify, setup, action);
        var before = audit.GetProperty("before");
        var after = audit.GetProperty("after");
        var actualDraft = await verify.DraftSessions.SingleAsync();
        switch (operation)
        {
            case "update":
                Assert.Equal("First", before.GetProperty("Name").GetString());
                Assert.Equal("Renamed", after.GetProperty("Name").GetString());
                Assert.Equal("New affiliation", (await verify.Teams.FindAsync(setup.TeamId))!.AffiliationName);
                Assert.Single(storage.Keys);
                break;
            case "remove": Assert.True(before.GetProperty("Active").GetBoolean()); Assert.False(after.GetProperty("Active").GetBoolean()); Assert.False((await verify.Teams.FindAsync(setup.TeamId))!.Active); break;
            case "start": Assert.False(before.GetProperty("DraftLocked").GetBoolean()); Assert.True(after.GetProperty("DraftLocked").GetBoolean()); Assert.Equal(DraftState.Running, actualDraft.State); Assert.All(await verify.Teams.ToListAsync(), team => Assert.Null(team.DraftPosition)); break;
            case "scramble": Assert.Equal(new List<int> { 1, 2 }, (await verify.Teams.OrderBy(x => x.DraftPosition).Select(x => x.DraftPosition!.Value).ToListAsync()).ToArray()); break;
            case "pick": Assert.Equal(JsonValueKind.Null, before.GetProperty("pick").ValueKind); Assert.Equal(setup.PickParticipantId, after.GetProperty("pick").GetProperty("EventParticipantId").GetGuid()); Assert.Single(await verify.DraftPicks.ToListAsync()); Assert.NotNull(actualDraft.FirstPickRecordedAt); break;
            case "undo": Assert.Equal(JsonValueKind.Null, before.GetProperty("pick").GetProperty("UndoneAt").ValueKind); Assert.Equal(now, after.GetProperty("pick").GetProperty("UndoneAt").GetDateTimeOffset()); Assert.Equal(now, (await verify.TeamMemberships.SingleAsync(x => x.EventParticipantId == setup.PickParticipantId)).LeftAt); break;
            case "acquire": Assert.Equal(JsonValueKind.Null, before.GetProperty("ControllerAccountId").ValueKind); Assert.Equal(setup.AdminId, after.GetProperty("ControllerAccountId").GetGuid()); break;
            case "takeover": Assert.Equal(setup.OtherAdminId, before.GetProperty("ControllerAccountId").GetGuid()); Assert.Equal(setup.AdminId, actualDraft.ControllerAccountId); break;
            case "release": Assert.Equal(setup.AdminId, before.GetProperty("ControllerAccountId").GetGuid()); Assert.Null(actualDraft.ControllerAccountId); Assert.Equal(JsonValueKind.Null, after.GetProperty("ControllerAccountId").ValueKind); break;
        }
        if (operation is not ("update" or "remove")) Assert.Equal(new[] { setup.EventId }, notifier.Events);
    }

    [Theory]
    [InlineData("create", "board.created")]
    [InlineData("acquire", "board.editing_acquired")]
    [InlineData("takeover", "board.editing_taken_over")]
    [InlineData("release", "board.editing_released")]
    [InlineData("createTile", "board.tile_created")]
    [InlineData("editTile", "board.tile_edited")]
    [InlineData("remove", "board.tile_removed")]
    [InlineData("move", "board.tile_moved")]
    [InlineData("swap", "board.tiles_swapped")]
    [InlineData("teamSize", "board.expected_team_size_changed")]
    public async Task BoardMutationsRollbackEverySaveAndNotifyOnlyAfterCommit(string operation, string action)
    {
        var setup = await SeedAsync();
        var boardId = Guid.NewGuid();
        var tileId = Guid.NewGuid();
        await using (var db = new ApplicationDbContext(options))
        {
            if (operation != "create")
            {
                var board = new Board(boardId, setup.EventId, "Main board", 2, 2);
                if (operation != "acquire") board.AcquireEditing(operation == "takeover" ? setup.OtherAdminId : setup.AdminId, operation == "remove" ? now.AddMinutes(-2) : now, BoardEditingLease.Duration);
                board.SetTotalEhb(4);
                var template = new TileTemplate(Guid.NewGuid(), "Original", "Original description", ObjectiveType.Manual, "", 2);
                var tile = new BoardTile(tileId, boardId, template.Id, 0, 0, "Original", "Original description", "", 2);
                var otherTemplate = new TileTemplate(Guid.NewGuid(), "Second", "", ObjectiveType.Manual, "", 2);
                var second = new BoardTile(Guid.NewGuid(), boardId, otherTemplate.Id, 1, 1, "Second", "", "", 2);
                var asset = new BoardTileImageAsset(Guid.NewGuid(), setup.EventId, tileId, "fixture/old.png", "old.png", "image/png", 3, 1, 1, "checksum", setup.AdminId, now);
                db.AddRange(board, template, tile, otherTemplate, second, asset,
                    new TileTemplateRequirement(Guid.NewGuid(), template.Id, 1, 1, true, false, "Original goal", true),
                    new BoardRequirementSnapshot(Guid.NewGuid(), tileId, 1, 1, true, false, "Original goal", true));
                await db.SaveChangesAsync();
                tile.SetActiveImageAsset(asset.Id);
                await db.SaveChangesAsync();
                if (operation == "remove")
                {
                    var approval = new BoardApprovalSnapshot(Guid.NewGuid(), board.Id, 1, now.AddMinutes(-1), setup.AdminId, null,
                        board.Name, board.Rows, board.Columns, board.TotalEhbEstimate, board.CalculationVersion, board.Version, board.State);
                    db.BoardApprovalSnapshots.Add(approval);
                    await db.SaveChangesAsync();
                    board.Approve(approval.Id);
                    await db.SaveChangesAsync();
                }
            }
        }
        var baseline = await PersistedStateAsync();
        var observer = new AuditFailureObserver();
        var storage = new MemoryStorage();
        if (operation != "create") storage.Keys.Add("fixture/old.png");
        var notifier = new Notifier(async () =>
        {
            await using var committed = new ApplicationDbContext(options);
            Assert.True(await committed.AuditEntries.AnyAsync(x => x.Action == action && x.EventId == setup.EventId));
        });
        async Task Execute(ApplicationDbContext db, bool fail)
        {
            var page = Context(new BoardModel(db, new Clock(now), new AuditWriter(db, new Clock(now)), notifier, storage), setup.AdminId, fail);
            page.BoardVersion = (await db.Boards.SingleOrDefaultAsync())?.Version ?? 0;
            page.Rows = 1; page.Columns = 3;
            page.TileDraft = new BoardModel.TileDraftInput
            {
                TileId = tileId,
                Position = 1,
                Name = "Updated tile",
                Description = "Updated description",
                ManualEhb = 7,
                Image = Upload(),
                Requirements = [new() { Kind = "challenge", Description = "Updated goal", Target = 3 }]
            };
            switch (operation)
            {
                case "create": await page.OnPostCreateAsync(setup.EventId, default); break;
                case "acquire" or "takeover": await page.OnPostTakeEditingAsync(setup.EventId, default); break;
                case "release": await page.OnPostReleaseEditingAsync(setup.EventId, default); break;
                case "createTile": await page.OnPostCreateTileAsync(setup.EventId, default); break;
                case "editTile": await page.OnPostEditTileAsync(setup.EventId, default); break;
                case "remove": await page.OnPostRemoveAsync(setup.EventId, tileId, default); break;
                case "move" or "swap": await page.OnPostMoveAsync(setup.EventId, tileId, operation == "move" ? 1 : 3, default); break;
                case "resize": await page.OnPostResizeAsync(setup.EventId, default); break;
                case "teamSize": await page.OnPostTeamSizeAsync(setup.EventId, 7, default); break;
            }
        }
        Exception? failure;
        await using (var db = FailureContext(observer)) failure = await Record.ExceptionAsync(() => Execute(db, true));
        Assert.True(observer.SawAuditFailure, failure?.ToString());
        Assert.Equal(baseline, await PersistedStateAsync());
        Assert.Empty(notifier.Events);
        Assert.Equal(operation == "create" ? Array.Empty<string>() : ["fixture/old.png"], storage.Keys.ToArray());
        await using (var db = new ApplicationDbContext(options)) await Execute(db, false);
        await using var verify = new ApplicationDbContext(options);
        var audit = await AuditAsync(verify, setup, action);
        var before = audit.GetProperty("before"); var after = audit.GetProperty("after");
        var boardAfter = await verify.Boards.SingleAsync();
        switch (operation)
        {
            case "create": Assert.Equal(JsonValueKind.Null, before.ValueKind); Assert.Equal(1, after.GetProperty("Rows").GetInt32()); Assert.Equal(3, boardAfter.Columns); break;
            case "acquire": Assert.Equal(JsonValueKind.Null, before.GetProperty("EditorAccountId").ValueKind); Assert.Equal(setup.AdminId, after.GetProperty("EditorAccountId").GetGuid()); break;
            case "takeover": Assert.Equal(setup.OtherAdminId, before.GetProperty("EditorAccountId").GetGuid()); Assert.Equal(setup.AdminId, boardAfter.EditorAccountId); break;
            case "release": Assert.Equal(setup.AdminId, before.GetProperty("EditorAccountId").GetGuid()); Assert.Equal(JsonValueKind.Null, after.GetProperty("EditorAccountId").ValueKind); Assert.Null(boardAfter.EditorAccountId); break;
            case "createTile" or "editTile":
                if (operation == "createTile") Assert.Equal(JsonValueKind.Null, before.GetProperty("tile").ValueKind);
                else Assert.Equal("Original", before.GetProperty("tile").GetProperty("NameSnapshot").GetString());
                Assert.Equal("Updated tile", after.GetProperty("tile").GetProperty("NameSnapshot").GetString());
                Assert.Equal(3, after.GetProperty("tile").GetProperty("requirements")[0].GetProperty("TargetContribution").GetInt64());
                Assert.Equal(7, (await verify.BoardTiles.SingleAsync(x => x.NameSnapshot == "Updated tile")).EstimatedEhbSnapshot);
                Assert.Equal(2, storage.Keys.Count);
                break;
            case "remove":
                Assert.Equal(tileId, before.GetProperty("tile").GetProperty("Id").GetGuid());
                Assert.Equal(JsonValueKind.Null, after.GetProperty("tile").ValueKind);
                Assert.False(await verify.BoardTiles.AnyAsync(x => x.Id == tileId));
                Assert.Empty(storage.Keys);
                Assert.Equal("Validated", before.GetProperty("board").GetProperty("State").GetString());
                Assert.Equal("Draft", after.GetProperty("board").GetProperty("State").GetString());
                Assert.Equal(now.AddMinutes(-2).Add(BoardEditingLease.Duration), before.GetProperty("board").GetProperty("EditorLeaseExpiresAt").GetDateTimeOffset());
                Assert.Equal(now.Add(BoardEditingLease.Duration), after.GetProperty("board").GetProperty("EditorLeaseExpiresAt").GetDateTimeOffset());
                Assert.Equal(BoardState.Draft, boardAfter.State);
                Assert.Equal(now.Add(BoardEditingLease.Duration), boardAfter.EditorLeaseExpiresAt);
                Assert.Null(boardAfter.ActiveApprovalSnapshotId);
                Assert.Single(await verify.BoardApprovalSnapshots.ToListAsync());
                Assert.Single(await verify.AuditEntries.Where(x => x.Action == "board.auto_unapproved").ToListAsync());
                break;
            case "move" or "swap":
                Assert.Equal(0, before.GetProperty("source").GetProperty("ColumnIndex").GetInt32());
                Assert.Equal(1, after.GetProperty("source").GetProperty("ColumnIndex").GetInt32());
                var moved = await verify.BoardTiles.FindAsync(tileId); Assert.Equal((operation == "move" ? 0 : 1, 1), (moved!.RowIndex, moved.ColumnIndex));
                if (operation == "swap") Assert.Equal((0, 0), ((await verify.BoardTiles.SingleAsync(x => x.Id != tileId)).RowIndex, (await verify.BoardTiles.SingleAsync(x => x.Id != tileId)).ColumnIndex));
                break;
            case "teamSize": Assert.Equal(7, after.GetProperty("ExpectedTeamSize").GetInt32()); Assert.Equal(7, (await verify.Events.SingleAsync()).ExpectedTeamSize); Assert.Equal(2, boardAfter.Version); break;
        }
        Assert.Equal(new[] { setup.EventId }, notifier.Events);
    }

    [Fact]
    public async Task ValidResizeAuditFailureRollsBackApprovalAndNotification()
    {
        var setup = await SeedAsync();
        var boardId = Guid.NewGuid();
        var template = new TileTemplate(Guid.NewGuid(), "Original", "Original description", ObjectiveType.Manual, "", 2);
        var secondTemplate = new TileTemplate(Guid.NewGuid(), "Second", "", ObjectiveType.Manual, "", 2);
        var board = new Board(boardId, setup.EventId, "Main board", 2, 2);
        board.AcquireEditing(setup.AdminId, now, BoardEditingLease.Duration);
        var tile = new BoardTile(Guid.NewGuid(), boardId, template.Id, 0, 0, "Original", "Original description", "", 2);
        var second = new BoardTile(Guid.NewGuid(), boardId, secondTemplate.Id, 1, 1, "Second", "", "", 2);
        var approval = new BoardApprovalSnapshot(Guid.NewGuid(), boardId, 1, now, setup.AdminId, null,
            board.Name, board.Rows, board.Columns, board.TotalEhbEstimate, board.CalculationVersion, board.Version);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(board, template, secondTemplate, tile, second, approval);
            await db.SaveChangesAsync();
            board.Approve(approval.Id);
            await db.SaveChangesAsync();
        }

        var approvedVersion = board.Version;
        var baseline = await PersistedStateAsync();
        var observer = new AuditFailureObserver();
        var notifier = new Notifier();
        Exception? failure;
        await using (var db = FailureContext(observer))
        {
            var page = Context(new BoardModel(db, new Clock(now), new AuditWriter(db, new Clock(now)), notifier, new MemoryStorage()), setup.AdminId, true);
            page.BoardVersion = approvedVersion;
            page.Rows = 2;
            page.Columns = 3;

            failure = await Record.ExceptionAsync(() => page.OnPostResizeAsync(setup.EventId, default));
        }

        Assert.True(observer.SawAuditFailure, failure?.ToString());
        Assert.NotNull(failure);
        Assert.Equal(baseline, await PersistedStateAsync());
        Assert.Empty(notifier.Events);
        await using (var failed = new ApplicationDbContext(options))
        {
            var failedBoard = await failed.Boards.SingleAsync(value => value.Id == boardId);
            Assert.Equal((2, 2, BoardState.Validated, approvedVersion, approval.Id),
                (failedBoard.Rows, failedBoard.Columns, failedBoard.State, failedBoard.Version, failedBoard.ActiveApprovalSnapshotId));
            Assert.Single(await failed.BoardApprovalSnapshots.Where(value => value.Id == approval.Id).ToListAsync());
            Assert.Empty(await failed.AuditEntries.Where(value => value.EventId == setup.EventId &&
                (value.Action == "board.auto_unapproved" || value.Action == "board.resized")).ToListAsync());
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var page = Context(new BoardModel(db, new Clock(now), new AuditWriter(db, new Clock(now)), notifier, new MemoryStorage()), setup.AdminId);
            page.BoardVersion = await db.Boards.Where(value => value.Id == boardId).Select(value => value.Version).SingleAsync();
            page.Rows = 2;
            page.Columns = 3;

            Assert.IsType<RedirectToPageResult>(await page.OnPostResizeAsync(setup.EventId, default));
        }

        await using var verify = new ApplicationDbContext(options);
        var resizedBoard = await verify.Boards.SingleAsync(value => value.Id == boardId);
        Assert.Equal((2, 3, BoardState.Draft, approvedVersion + 2, (Guid?)null),
            (resizedBoard.Rows, resizedBoard.Columns, resizedBoard.State, resizedBoard.Version, resizedBoard.ActiveApprovalSnapshotId));
        Assert.Single(await verify.BoardApprovalSnapshots.Where(value => value.Id == approval.Id).ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(value => value.EventId == setup.EventId && value.Action == "board.auto_unapproved").ToListAsync());
        var resizeAudit = await AuditAsync(verify, setup, "board.resized");
        Assert.Equal(2, resizeAudit.GetProperty("before").GetProperty("Rows").GetInt32());
        Assert.Equal(2, resizeAudit.GetProperty("before").GetProperty("Columns").GetInt32());
        Assert.Equal(2, resizeAudit.GetProperty("after").GetProperty("Rows").GetInt32());
        Assert.Equal(3, resizeAudit.GetProperty("after").GetProperty("Columns").GetInt32());
        var persistedTiles = await verify.BoardTiles.Where(value => value.BoardId == boardId)
            .OrderBy(value => value.RowIndex).ThenBy(value => value.ColumnIndex)
            .Select(value => new { value.Id, value.RowIndex, value.ColumnIndex })
            .ToListAsync();
        Assert.Equal(new[] { (tile.Id, tile.RowIndex, tile.ColumnIndex), (second.Id, second.RowIndex, second.ColumnIndex) },
            persistedTiles.Select(value => (value.Id, value.RowIndex, value.ColumnIndex)));
        Assert.Equal(new[] { setup.EventId }, notifier.Events);
    }

    [Fact]
    public async Task LargeBoardSafeResizeAndLongObjectivesFitTheExistingAuditColumn()
    {
        var setup = await SeedAsync();
        var board = new Board(Guid.NewGuid(), setup.EventId, "Main board", 8, 7);
        board.AcquireEditing(setup.AdminId, now, BoardEditingLease.Duration);
        var template = new TileTemplate(Guid.NewGuid(), "Original", "Original", ObjectiveType.Manual, "", 1);
        var tiles = Enumerable.Range(0, 55).Select(position => new BoardTile(Guid.NewGuid(), board.Id, template.Id, position / 7, position % 7, "Original", "Original", "", 1)).ToArray();
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(board, template); db.AddRange(tiles); await db.SaveChangesAsync();
        }

        var notifier = new Notifier();
        await using (var db = new ApplicationDbContext(options))
        {
            var page = Context(new BoardModel(db, new Clock(now), new AuditWriter(db, new Clock(now)), notifier, new MemoryStorage()), setup.AdminId);
            page.BoardVersion = board.Version; page.Rows = 8; page.Columns = 8;
            Assert.IsType<RedirectToPageResult>(await page.OnPostResizeAsync(setup.EventId, default));
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var page = Context(new BoardModel(db, new Clock(now), new AuditWriter(db, new Clock(now)), notifier, new MemoryStorage()), setup.AdminId);
            page.BoardVersion = await db.Boards.Where(value => value.Id == board.Id).Select(value => value.Version).SingleAsync();
            page.Rows = 8; page.Columns = 7;
            Assert.IsType<RedirectToPageResult>(await page.OnPostResizeAsync(setup.EventId, default));
        }

        await using (var db = new ApplicationDbContext(options))
        {
            var resizeAudits = await db.AuditEntries.Where(value => value.EventId == setup.EventId && value.Action == "board.resized")
                .OrderBy(value => value.OccurredAt).ToListAsync();
            Assert.Equal(2, resizeAudits.Count);
            var observedDimensions = new HashSet<((int Rows, int Columns) Before, (int Rows, int Columns) After)>();
            foreach (var resizeAudit in resizeAudits)
            {
                using var resizeDetails = JsonDocument.Parse(resizeAudit.Details!);
                var before = resizeDetails.RootElement.GetProperty("before");
                var afterSnapshot = resizeDetails.RootElement.GetProperty("after");
                var beforeDimensions = (before.GetProperty("Rows").GetInt32(), before.GetProperty("Columns").GetInt32());
                var afterDimensions = (afterSnapshot.GetProperty("Rows").GetInt32(), afterSnapshot.GetProperty("Columns").GetInt32());
                observedDimensions.Add((beforeDimensions, afterDimensions));
                Assert.Equal(55, before.GetProperty("tiles").GetProperty("Count").GetInt32());
                Assert.True(before.GetProperty("tiles").GetProperty("ValuesOmitted").GetBoolean());
                Assert.True(afterSnapshot.GetProperty("tiles").GetProperty("ValuesOmitted").GetBoolean());
                Assert.Equal(before.GetProperty("tiles").GetProperty("Sha256").GetString(), afterSnapshot.GetProperty("tiles").GetProperty("Sha256").GetString());
                Assert.InRange(resizeAudit.Details!.Length, 1, 4000);
            }
            Assert.Equal(
                new HashSet<((int Rows, int Columns) Before, (int Rows, int Columns) After)> { ((8, 7), (8, 8)), ((8, 8), (8, 7)) },
                observedDimensions);
            var persistedTiles = await db.BoardTiles.Where(value => value.BoardId == board.Id)
                .OrderBy(value => value.RowIndex).ThenBy(value => value.ColumnIndex)
                .Select(value => new { value.Id, value.RowIndex, value.ColumnIndex })
                .ToListAsync();
            Assert.Equal(tiles.Select(value => (value.Id, value.RowIndex, value.ColumnIndex)),
                persistedTiles.Select(value => (value.Id, value.RowIndex, value.ColumnIndex)));
            Assert.Equal(new[] { setup.EventId, setup.EventId }, notifier.Events);
            var page = Context(new BoardModel(db, new Clock(now), new AuditWriter(db, new Clock(now)), new Notifier(), new MemoryStorage()), setup.AdminId);
            page.BoardVersion = (await db.Boards.SingleAsync()).Version;
            page.TileDraft = new BoardModel.TileDraftInput
            {
                TileId = tiles[0].Id,
                Name = new string('ø', 200),
                Description = new string('ø', 2000),
                ManualEhb = 5,
                Requirements = Enumerable.Range(1, 8).Select(i => new BoardModel.RequirementInput { Kind = "challenge", Description = new string('ø', 300), Target = i }).ToList()
            };
            Assert.IsType<RedirectToPageResult>(await page.OnPostEditTileAsync(setup.EventId, default));
        }
        await using var verify = new ApplicationDbContext(options);
        var audit = await AuditAsync(verify, setup, "board.tile_edited");
        var after = audit.GetProperty("after").GetProperty("tile");
        Assert.True(after.GetProperty("DescriptionSnapshot").GetProperty("Truncated").GetBoolean());
        Assert.Equal(2000, after.GetProperty("DescriptionSnapshot").GetProperty("Length").GetInt32());
        Assert.Equal(8, after.GetProperty("requirements").GetProperty("Count").GetInt32());
        Assert.Equal(new string('ø', 2000), (await verify.BoardTiles.FindAsync(tiles[0].Id))!.DescriptionSnapshot);
        Assert.All(await verify.AuditEntries.ToListAsync(), entry => Assert.InRange(entry.Details!.Length, 1, 4000));
    }

    [Fact]
    public async Task LargeTeamOrderKeepsTruthfulBoundedBeforeAndAfterAudit()
    {
        var setup = await SeedAsync();
        await using (var db = new ApplicationDbContext(options))
        {
            var draft = await db.DraftSessions.SingleAsync();
            draft.AcquireControl(setup.AdminId, now, DraftControlLease.Duration); draft.Start(now);
            db.Teams.AddRange(Enumerable.Range(3, 38).Select(i => new Team(Guid.NewGuid(), setup.EventId, $"Team {i}", $"team-{i}", TeamFormationType.Drafted, null, true)));
            await db.SaveChangesAsync();
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var page = Context(new DraftModel(db, new Clock(now), new AuditWriter(db, new Clock(now)), new Notifier(), null!, new EventParticipantCharacterService(db, new Clock(now))), setup.AdminId);
            await page.OnPostScrambleAsync(setup.EventId, default);
        }
        await using var verify = new ApplicationDbContext(options);
        var audit = await AuditAsync(verify, setup, "draft.order_scrambled");
        Assert.Equal(40, audit.GetProperty("before").GetProperty("teams").GetProperty("Count").GetInt32());
        Assert.True(audit.GetProperty("after").GetProperty("teams").GetProperty("ValuesOmitted").GetBoolean());
        Assert.Equal(Enumerable.Range(1, 40), await verify.Teams.OrderBy(x => x.DraftPosition).Select(x => x.DraftPosition!.Value).ToListAsync());
        Assert.InRange((await verify.AuditEntries.SingleAsync()).Details!.Length, 1, 4000);
    }

    private async Task<Setup> SeedAsync()
    {
        var admin = Account.CreateWebsite(Guid.NewGuid(), "admin", "ADMIN", now); admin.SetGlobalRole(GlobalRole.Admin);
        var other = Account.CreateWebsite(Guid.NewGuid(), "other", "OTHER", now); other.SetGlobalRole(GlobalRole.Admin);
        var ev = new BingoEvent(Guid.NewGuid(), "Audit fixture", "audit-fixture", "UTC", admin.Id, now.AddDays(-1), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        ev.ConfigureSchedule(now.AddDays(-1), now.AddHours(-1), null, now.AddHours(1), now.AddDays(1), 10);
        ev.ConfigureSignup(true, false, null); ev.OpenSignups(now.AddDays(-1)); ev.CloseSignups(now.AddHours(-1));
        var form = new SignupForm(Guid.NewGuid(), ev.Id, now);
        var primary = new SignupQuestion(Guid.NewGuid(), form.Id, ev.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var first = new Team(Guid.NewGuid(), ev.Id, "First", "first", TeamFormationType.Drafted, "Old affiliation", true);
        var second = new Team(Guid.NewGuid(), ev.Id, "Second", "second", TeamFormationType.Drafted, null, true);
        var participants = Enumerable.Range(0, 4).Select(i => new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, i + 1, now, SignupSource.Website)).ToArray();
        var firstCaptainLogin = $"audit-captain-a-{Guid.NewGuid():N}";
        var secondCaptainLogin = $"audit-captain-b-{Guid.NewGuid():N}";
        var firstCaptain = Account.CreateWebsite(Guid.NewGuid(), firstCaptainLogin, firstCaptainLogin.ToUpperInvariant(), now);
        var secondCaptain = Account.CreateWebsite(Guid.NewGuid(), secondCaptainLogin, secondCaptainLogin.ToUpperInvariant(), now);
        participants[0].AssignOwner(firstCaptain);
        participants[1].AssignOwner(secondCaptain);
        await using var db = new ApplicationDbContext(options);
        db.AddRange(admin, other, firstCaptain, secondCaptain, ev, form, primary, first, second, new DraftSession(Guid.NewGuid(), ev.Id, 1));
        db.AddRange(participants);
        for (var i = 0; i < participants.Length; i++)
        {
            var character = new OsrsCharacter(Guid.NewGuid(), $"Player {i}", $"PLAYER {i}", now);
            db.AddRange(character, new EventParticipantCharacter(Guid.NewGuid(), ev.Id, participants[i].Id, character.Id, 0, now, null, primary.Id, EventCharacterRole.Playing, i + 1, EhbSource.Manual, null));
        }
        db.AddRange(new TeamMembership(Guid.NewGuid(), first.Id, participants[0].Id, TeamMembershipRole.Captain, now, null, "Fixture"), new TeamMembership(Guid.NewGuid(), second.Id, participants[1].Id, TeamMembershipRole.Captain, now, null, "Fixture"));
        await db.SaveChangesAsync();
        return new(ev.Id, admin.Id, other.Id, first.Id, participants[2].Id);
    }

    private async Task<string> PersistedStateAsync()
    {
        await using var db = new ApplicationDbContext(options);
        // Whole fixture state catches partial early claims, temporary positions,
        // template/snapshot replacement, image rows, leases, versions and pick history.
        return JsonSerializer.Serialize(new
        {
            events = await db.Events.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            forms = await db.SignupForms.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            teams = await db.Teams.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            drafts = await db.DraftSessions.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            picks = await db.DraftPicks.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            members = await db.TeamMemberships.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            teamImages = await db.TeamImageAssets.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            boards = await db.Boards.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            tiles = await db.BoardTiles.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            approvals = await db.BoardApprovalSnapshots.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            templates = await db.TileTemplates.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            templateRequirements = await db.TileTemplateRequirements.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            requirements = await db.BoardRequirementSnapshots.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            images = await db.BoardTileImageAssets.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
            audits = await db.AuditEntries.AsNoTracking().OrderBy(x => x.Id).ToListAsync()
        });
    }

    private static async Task<JsonElement> AuditAsync(ApplicationDbContext db, Setup setup, string action)
    {
        var entry = await db.AuditEntries.SingleAsync(x => x.Action == action);
        Assert.Equal(setup.EventId, entry.EventId);
        Assert.Equal(setup.AdminId, entry.ActorAccountId);
        var targetId = action.StartsWith("event.", StringComparison.Ordinal) ? setup.EventId
            : action.StartsWith("board.", StringComparison.Ordinal) ? await db.Boards.Select(x => x.Id).SingleAsync()
            : action is "team.updated" or "draft.team_removed" ? setup.TeamId
            : action.StartsWith("draft.pick_", StringComparison.Ordinal) ? await db.DraftPicks.Select(x => x.Id).SingleAsync()
            : await db.DraftSessions.Select(x => x.Id).SingleAsync();
        Assert.Equal(targetId.ToString(), entry.TargetId);
        Assert.DoesNotContain("StorageKey", entry.Details);
        using var json = JsonDocument.Parse(entry.Details!);
        Assert.True(json.RootElement.TryGetProperty("before", out _));
        Assert.True(json.RootElement.TryGetProperty("after", out _));
        return json.RootElement.Clone();
    }

    private ApplicationDbContext FailureContext(AuditFailureObserver observer) => new(new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(observer).Options);
    private static T Context<T>(T page, Guid adminId, bool fail = false) where T : PageModel
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, adminId.ToString()), new Claim(ClaimTypes.Name, fail ? "audit-failure" : "admin")], "test")) };
        page.PageContext = new PageContext { HttpContext = http };
        page.TempData = new TempDataDictionary(http, new EmptyTempData());
        return page;
    }
    private static FormFile Upload() => new(new MemoryStream([1, 2, 3]), 0, 3, "image", "image.png");
    private sealed record Setup(Guid EventId, Guid AdminId, Guid OtherAdminId, Guid TeamId, Guid PickParticipantId);
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class EmptyTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
    private sealed class AuditFailureObserver : SaveChangesInterceptor
    {
        public bool SawAuditFailure { get; private set; }
        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            SawAuditFailure |= eventData.Exception is DbUpdateException { InnerException: PostgresException { SqlState: "P0001" } };
            return Task.CompletedTask;
        }
    }
    private sealed class ConcurrencyFailureObserver : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<InterceptionResult<int>>(new DbUpdateConcurrencyException("Injected signup-code concurrency failure."));
    }
    private sealed class Notifier(Func<Task>? inspectCommit = null) : IAdminCollaborationNotifier
    {
        public List<Guid> Events { get; } = [];
        public Task NotifyDraftChangedAsync(Guid eventId, CancellationToken cancellationToken = default) { Events.Add(eventId); return Task.CompletedTask; }
        public async Task NotifyBoardChangedAsync(Guid eventId, CancellationToken cancellationToken = default) { if (inspectCommit is not null) await inspectCommit(); Events.Add(eventId); }
        public Task NotifyEventsControlChangedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class MemoryStorage : IEvidenceStorage
    {
        public HashSet<string> Keys { get; } = [];
        public Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default)
        {
            var key = $"fixture/{submissionId}.png"; Keys.Add(key);
            return Task.FromResult(new StoredEvidence(key, "image.png", "image/png", 3, 1, 1, "checksum"));
        }
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) { Keys.Remove(storageKey); return Task.CompletedTask; }
    }
}
