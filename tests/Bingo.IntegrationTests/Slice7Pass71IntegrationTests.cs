using Bingo.Application.Boards;
using Bingo.Application.Events;
using Bingo.Application.Evidence;
using Bingo.Application.Signups;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Bingo.Infrastructure.Teams;
using Bingo.Web.Pages.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice7Pass71IntegrationTests : IAsyncLifetime
{
    private const string PreviousMigration = "20260730183704_AddPublishedBoardCorrectionFlag";
    private const string Slice7FoundationMigration = "20260730212304_AddSlice7LiveAccountAndTeamFocusFoundation";
    private const string FocusConstraintCorrectionMigration = "20260731170051_RemoveTeamFocusEventTeamAlternateKey";
    private const string FocusNormalizationMigration = "20260731180603_NormalizeCompletedTileFocusMarkers";
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice7_pass71")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private readonly DateTimeOffset now = new(2026, 7, 30, 18, 0, 0, TimeSpan.Zero);
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
    public async Task CleanAndRepresentativeRetainedMigrationsSucceed()
    {
        await using var rehearsal = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("bingo_slice7_retained")
            .WithUsername("bingo")
            .WithPassword("bingo_test_password")
            .Build();
        await rehearsal.StartAsync();
        var retainedOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(rehearsal.GetConnectionString()).Options;
        var eventId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var tileId = Guid.NewGuid();
        var requirementId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await using (var retained = new ApplicationDbContext(retainedOptions))
        {
            await retained.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            retained.SystemMetadata.Add(new SystemMetadata { Key = "slice7-retained-sentinel", Value = "retained", UpdatedAt = now });
            await retained.SaveChangesAsync();
            await retained.GetService<IMigrator>().MigrateAsync(Slice7FoundationMigration);

            var owner = Account.CreateWebsite(ownerId, "slice7-retained-owner", "SLICE7 RETAINED OWNER", now);
            var eventItem = new BingoEvent(eventId, "Retained focus event", $"retained-focus-{eventId:N}", "UTC", ownerId, now);
            var board = new Board(boardId, eventId, "Retained focus board", 1, 1);
            var tile = new BoardTile(tileId, boardId, Guid.NewGuid(), 0, 0, "Completed retained tile", "Description", "Evidence", 1);
            var requirement = new BoardRequirementSnapshot(requirementId, tileId, 0, 3, true, true, "Complete it", true);
            var team = new Team(teamId, eventId, "Retained focus team", $"retained-focus-team-{eventId:N}", TeamFormationType.Preformed, null, false);
            retained.Add(owner);
            await retained.SaveChangesAsync();
            await retained.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO events (id, name, slug, description, timezone, state, signup_opens_at, signup_closes_at, event_starts_at, event_ends_at, submission_cutoff_at, participant_cap, waiting_list_enabled, require_signup_code, participant_list_published, draft_results_published, team_rosters_published, board_published, results_published, draft_locked, created_by_account_id, created_at) VALUES ({eventItem.Id}, {eventItem.Name}, {eventItem.Slug}, {""}, {eventItem.Timezone}, {"Draft"}, {now}, {now}, {now}, {now.AddDays(1)}, {now.AddDays(1)}, {20}, {false}, {false}, {false}, {false}, {false}, {false}, {false}, {false}, {ownerId}, {now})");
            retained.AddRange(board, tile, requirement, team);
            await retained.SaveChangesAsync();
            await retained.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO submission_contributions
                    (id, submission_id, team_id, requirement_id, drop_snapshot_id, credited_participant_id, amount, applied_at, reversed_at)
                VALUES
                    ({Guid.NewGuid()}, {Guid.NewGuid()}, {teamId}, {requirementId}, NULL, {Guid.NewGuid()}, 3, {now}, NULL)
                """);
            retained.TeamFocusMarkers.Add(new TeamFocusMarker(
                Guid.NewGuid(), eventId, teamId, TeamFocusTargetKind.Tile, tileId, null, null, true, now, ownerId));
            await retained.SaveChangesAsync();
            Assert.True(await retained.TeamFocusMarkers.Where(marker => marker.TeamId == teamId && marker.TargetKind == TeamFocusTargetKind.Tile).Select(marker => marker.Focused).SingleAsync());
            await retained.GetService<IMigrator>().MigrateAsync(FocusConstraintCorrectionMigration);
            retained.TeamFocusMarkers.AddRange(
                new TeamFocusMarker(Guid.NewGuid(), eventId, teamId, TeamFocusTargetKind.Row, null, 0, null, true, now, ownerId),
                new TeamFocusMarker(Guid.NewGuid(), eventId, teamId, TeamFocusTargetKind.Column, null, null, 0, true, now, ownerId));
            await retained.SaveChangesAsync();
            Assert.True(await retained.TeamFocusMarkers.AnyAsync(marker => marker.TeamId == teamId && marker.TargetKind == TeamFocusTargetKind.Tile && marker.Focused));
            await retained.GetService<IMigrator>().MigrateAsync(FocusNormalizationMigration);
        }

        await using var migrated = new ApplicationDbContext(retainedOptions);
        Assert.Equal("retained", await migrated.SystemMetadata.Where(x => x.Key == "slice7-retained-sentinel").Select(x => x.Value).SingleAsync());
        Assert.Equal(0, await migrated.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM event_participant_character_swaps").SingleAsync());
        var retainedMarkers = await migrated.TeamFocusMarkers.Where(marker => marker.TeamId == teamId).ToListAsync();
        Assert.False(retainedMarkers.Single(marker => marker.TargetKind == TeamFocusTargetKind.Tile).Focused);
        Assert.Equal(2, retainedMarkers.Single(marker => marker.TargetKind == TeamFocusTargetKind.Tile).Version);
        Assert.True(retainedMarkers.Single(marker => marker.TargetKind == TeamFocusTargetKind.Row).Focused);
        Assert.True(retainedMarkers.Single(marker => marker.TargetKind == TeamFocusTargetKind.Column).Focused);
    }

    [Fact]
    public async Task FocusTargetShapeAndSwapInitialUniquenessAreRelationallyProtected()
    {
        var fixture = await SeedFixtureAsync(includeInformational: false, includeTeam: true);
        var tileId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            var board = await setup.Boards.SingleAsync(x => x.EventId == fixture.EventId);
            setup.BoardTiles.Add(new BoardTile(tileId, board.Id, Guid.NewGuid(), 0, 0, "Tile", "Description", "Evidence", 1));
            await setup.SaveChangesAsync();
        }

        await using (var first = new ApplicationDbContext(options))
        {
            first.TeamFocusMarkers.Add(new TeamFocusMarker(Guid.NewGuid(), fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Tile, tileId, null, null, true, now, null));
            await first.SaveChangesAsync();
        }
        await using (var duplicate = new ApplicationDbContext(options))
        {
            duplicate.TeamFocusMarkers.Add(new TeamFocusMarker(Guid.NewGuid(), fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Tile, tileId, null, null, false, now, null));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
        }

        await using (var invalid = new ApplicationDbContext(options))
        {
            await Assert.ThrowsAsync<PostgresException>(() => invalid.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO team_focus_markers
                    (id, event_id, team_id, target_kind, board_tile_id, row_index, column_index, focused, version, updated_at, updated_by_account_id)
                VALUES
                    ({Guid.NewGuid()}, {fixture.EventId}, {fixture.TeamId}, {"Row"}, {tileId}, 0, NULL, TRUE, 1, {now}, NULL)
                """));
        }

        await using (var firstSwap = new ApplicationDbContext(options))
        {
            firstSwap.EventParticipantCharacterSwaps.Add(new EventParticipantCharacterSwap(
                Guid.NewGuid(), fixture.EventId, fixture.ParticipantId, null, fixture.PrimaryCharacterId,
                now, now, null, null));
            await firstSwap.SaveChangesAsync();
        }
        await using (var duplicateSwap = new ApplicationDbContext(options))
        {
            duplicateSwap.EventParticipantCharacterSwaps.Add(new EventParticipantCharacterSwap(
                Guid.NewGuid(), fixture.EventId, fixture.ParticipantId, null, fixture.PrimaryCharacterId,
                now.AddMinutes(1), now.AddMinutes(1), null, null));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateSwap.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task MultipleDistinctFocusMarkersCanBeUnfocusedIndependently()
    {
        var fixture = await SeedFixtureAsync(includeInformational: false, includeTeam: true);
        var firstTileId = Guid.NewGuid();
        var secondTileId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var otherTeamId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            var board = await setup.Boards.SingleAsync(x => x.EventId == fixture.EventId);
            setup.BoardTiles.AddRange(
                new BoardTile(firstTileId, board.Id, Guid.NewGuid(), 0, 0, "First focus tile", "Description", "Evidence", 1),
                new BoardTile(secondTileId, board.Id, Guid.NewGuid(), 0, 1, "Second focus tile", "Description", "Evidence", 1));
            setup.AddRange(
                Account.CreateWebsite(outsiderId, "Focus clear outsider", "FOCUS CLEAR OUTSIDER", now),
                new Team(otherTeamId, fixture.EventId, "Other clear team", "other-clear-team", TeamFormationType.Preformed, null, false),
                new TeamFocusMarker(Guid.NewGuid(), fixture.EventId, otherTeamId, TeamFocusTargetKind.Row, null, 0, null, true, now, null));
            await setup.SaveChangesAsync();
        }

        await using (var mutate = new ApplicationDbContext(options))
        {
            var notifier = new CountingFocusNotifier();
            var service = new TeamFocusService(mutate, new FixedTimeProvider(now), notifier);
            Assert.True((await service.SetFocusAsync(new(fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Row, null, 0, null, true, 0, fixture.OwnerId))).Succeeded);
            Assert.True((await service.SetFocusAsync(new(fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Column, null, null, 0, true, 0, fixture.OwnerId))).Succeeded);
            Assert.True((await service.SetFocusAsync(new(fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Tile, firstTileId, null, null, true, 0, fixture.OwnerId))).Succeeded);
            Assert.True((await service.SetFocusAsync(new(fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Tile, secondTileId, null, null, true, 0, fixture.OwnerId))).Succeeded);

            var first = await mutate.TeamFocusMarkers.SingleAsync(x => x.BoardTileId == firstTileId);
            var unfocused = await service.SetFocusAsync(new(fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Tile, firstTileId, null, null, false, first.Version, fixture.OwnerId));
            Assert.True(unfocused.Succeeded, unfocused.Error);

            var expectedBeforeClear = await mutate.TeamFocusMarkers.AsNoTracking()
                .Where(x => x.EventId == fixture.EventId && x.TeamId == fixture.TeamId)
                .Select(x => new TeamFocusMarkerVersion(x.Id, x.Version)).ToListAsync();
            var beforeClearNotificationCount = notifier.Count;
            var clear = await service.ClearAllFocusAsync(new(fixture.EventId, fixture.TeamId, expectedBeforeClear, fixture.OwnerId));
            Assert.True(clear.Succeeded, clear.Error);
            Assert.Equal(beforeClearNotificationCount + 1, notifier.Count);

            var afterClear = await mutate.TeamFocusMarkers.Where(x => x.EventId == fixture.EventId && x.TeamId == fixture.TeamId).ToListAsync();
            Assert.All(afterClear, marker => Assert.False(marker.Focused));
            Assert.True(await mutate.TeamFocusMarkers.Where(x => x.TeamId == otherTeamId).Select(x => x.Focused).SingleAsync());

            Assert.True((await service.SetFocusAsync(new(fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Row, null, 0, null, true, afterClear.Single(x => x.TargetKind == TeamFocusTargetKind.Row).Version, fixture.OwnerId))).Succeeded);
            var staleExpected = await mutate.TeamFocusMarkers.AsNoTracking()
                .Where(x => x.EventId == fixture.EventId && x.TeamId == fixture.TeamId)
                .Select(x => new TeamFocusMarkerVersion(x.Id, x.Version)).ToListAsync();
            var currentRow = await mutate.TeamFocusMarkers.SingleAsync(x => x.EventId == fixture.EventId && x.TeamId == fixture.TeamId && x.TargetKind == TeamFocusTargetKind.Row);
            Assert.True((await service.SetFocusAsync(new(fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Row, null, 0, null, false, currentRow.Version, fixture.OwnerId))).Succeeded);
            var beforeStale = notifier.Count;
            var stale = await service.ClearAllFocusAsync(new(fixture.EventId, fixture.TeamId, staleExpected, fixture.OwnerId));
            Assert.False(stale.Succeeded);
            Assert.Equal(beforeStale, notifier.Count);
            Assert.All(await mutate.TeamFocusMarkers.Where(x => x.EventId == fixture.EventId && x.TeamId == fixture.TeamId).ToListAsync(), marker => Assert.False(marker.Focused));

            var currentExpected = await mutate.TeamFocusMarkers.AsNoTracking()
                .Where(x => x.EventId == fixture.EventId && x.TeamId == fixture.TeamId)
                .Select(x => new TeamFocusMarkerVersion(x.Id, x.Version)).ToListAsync();
            var unauthorized = await service.ClearAllFocusAsync(new(fixture.EventId, fixture.TeamId, currentExpected, outsiderId));
            Assert.False(unauthorized.Succeeded);
            Assert.Equal(beforeStale, notifier.Count);
        }

        await using var verify = new ApplicationDbContext(options);
        var markers = await verify.TeamFocusMarkers.Where(x => x.TeamId == fixture.TeamId).ToListAsync();
        Assert.Equal(4, markers.Count);
        Assert.All(markers, marker => Assert.False(marker.Focused));
        await using var verifyOtherTeam = new ApplicationDbContext(options);
        Assert.True(await verifyOtherTeam.TeamFocusMarkers
            .Where(x => x.TeamId == otherTeamId)
            .Select(x => x.Focused)
            .SingleAsync());
    }

    [Fact]
    public void TeamBoardGetFocusPresentationMarksOnlyIncompleteFocusedTargets()
    {
        var tileId = Guid.NewGuid();
        var markers = new[]
        {
            new TeamFocusMarkerView(Guid.NewGuid(), TeamFocusTargetKind.Tile, tileId, null, null, true, 1),
            new TeamFocusMarkerView(Guid.NewGuid(), TeamFocusTargetKind.Row, null, 0, null, true, 1),
            new TeamFocusMarkerView(Guid.NewGuid(), TeamFocusTargetKind.Column, null, null, 0, true, 1)
        };
        var completedTile = new PublicTileProgress(tileId, 0, 0, "Completed", "Description", null, [], 1, 1, 1, true, now);
        var incompleteRowTile = new PublicTileProgress(Guid.NewGuid(), 0, 1, "Row member", "Description", null, [], 1, 0, 1, false, null);
        var incompleteColumnTile = new PublicTileProgress(Guid.NewGuid(), 1, 0, "Column member", "Description", null, [], 1, 0, 1, false, null);
        Assert.Equal(new TeamBoardModel.TeamFocusPresentation(false, false, false), TeamBoardModel.GetFocusPresentation(completedTile, markers));
        Assert.Equal(new TeamBoardModel.TeamFocusPresentation(false, true, false), TeamBoardModel.GetFocusPresentation(incompleteRowTile, markers));
        Assert.Equal(new TeamBoardModel.TeamFocusPresentation(false, false, true), TeamBoardModel.GetFocusPresentation(incompleteColumnTile, markers));
    }

    [Fact]
    public async Task TileSubmissionProjectionExcludesAdminWhileAllowingAuthorizedParticipant()
    {
        var eventId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var tileId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var board = new PublicEventBoard(
            eventId, "Evidence board", "evidence-board", EventState.Live, 1, 1, 1,
            [new PublicTeamBoard(teamId, "Evidence team", "evidence-team", null, null, 1, false,
                new([], 0, [], [], false, null, 0, []),
                [new PublicTileProgress(tileId, 0, 0, "Evidence tile", "Description", null, [], 1, 0, 1, false, null)])],
            [], []);
        var tile = new PublicTileDetails("Evidence board", "evidence-board", "Evidence team", "evidence-team", tileId,
            "Evidence tile", "Description", "Instructions", 0, 1, false, null, [], []);
        var authority = new ProjectionEvidenceAuthority(adminId, eventId, teamId);

        async Task<bool> CanSubmit(Guid accountId)
        {
            var context = new DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                    [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, accountId.ToString())], "test"))
            };
            var page = new TileModel(new StubBoardService(board, tile), authority, new FixedTimeProvider(now))
            {
                PageContext = new PageContext(new ActionContext(context, new RouteData(), new PageActionDescriptor()))
            };
            Assert.IsType<PageResult>(await page.OnGetAsync("evidence-board", "evidence-team", tileId, CancellationToken.None));
            return page.CanSubmit;
        }

        Assert.False(await CanSubmit(adminId));
        Assert.True(await CanSubmit(Guid.NewGuid()));
    }

    [Fact]
    public async Task CompletedTileFocusIsRejectedAndClearedThroughApprovalAndReversalRebalance()
    {
        var fixture = await SeedProgressFixtureAsync();
        var notifier = new CountingFocusNotifier();
        await using var db = new ApplicationDbContext(options);
        var focus = new TeamFocusService(db, new FixedTimeProvider(now));
        var initial = await focus.SetFocusAsync(new(fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Tile, fixture.TileId, null, null, true, 0, fixture.OwnerId));
        Assert.True(initial.Succeeded, initial.Error);

        var first = new Submission(Guid.NewGuid(), fixture.EventId, fixture.TeamId, fixture.TileId, fixture.RequirementId, null, fixture.ParticipantId, fixture.CharacterId, "Progress player", fixture.CaptainId, 2, now, "first", null);
        var second = new Submission(Guid.NewGuid(), fixture.EventId, fixture.TeamId, fixture.TileId, fixture.RequirementId, null, fixture.ParticipantId, fixture.CharacterId, "Progress player", fixture.CaptainId, 3, now, "second", null);
        db.Submissions.AddRange(first, second);
        await db.SaveChangesAsync();
        var submissions = new Bingo.Infrastructure.Evidence.SubmissionService(db, new NoopEvidenceStorage(), new FixedTimeProvider(now), focus: focus, focusNotifier: notifier);

        Assert.Equal(2, await submissions.ApproveAsync(first.Id, fixture.OwnerId));
        var afterFirst = await db.TeamFocusMarkers.SingleAsync(x => x.BoardTileId == fixture.TileId);
        Assert.True(afterFirst.Focused);
        Assert.Equal(1, await submissions.ApproveAsync(second.Id, fixture.OwnerId));
        var completed = await db.TeamFocusMarkers.SingleAsync(x => x.BoardTileId == fixture.TileId);
        Assert.False(completed.Focused);
        Assert.Equal(2, completed.Version);
        Assert.Equal(1, notifier.Count);

        var directFocus = await focus.SetFocusAsync(new(fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Tile, fixture.TileId, null, null, true, completed.Version, fixture.OwnerId));
        Assert.False(directFocus.Succeeded);
        Assert.Contains("completed", directFocus.Error, StringComparison.OrdinalIgnoreCase);

        // Simulate a stale focused row left by an older writer; reversal/rebalance
        // must clear it too and must not resurrect it after the completion reverses.
        completed.SetFocused(true, now, fixture.OwnerId);
        db.TeamFocusMarkers.AddRange(
            new TeamFocusMarker(Guid.NewGuid(), fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Row, null, 0, null, true, now, fixture.OwnerId),
            new TeamFocusMarker(Guid.NewGuid(), fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Column, null, null, 0, true, now, fixture.OwnerId));
        await db.SaveChangesAsync();
        var projected = await focus.GetContextAsync(fixture.EventId, fixture.TeamId, fixture.OwnerId, false);
        Assert.NotNull(projected);
        Assert.DoesNotContain(projected!.Markers, marker => marker.TargetKind == TeamFocusTargetKind.Tile);
        Assert.Contains(projected.Markers, marker => marker.TargetKind == TeamFocusTargetKind.Row && marker.Focused);
        Assert.Contains(projected.Markers, marker => marker.TargetKind == TeamFocusTargetKind.Column && marker.Focused);
        await submissions.ReverseAsync(first.Id, fixture.OwnerId, "Reverse first approval");
        var afterReverse = await db.TeamFocusMarkers.SingleAsync(x => x.BoardTileId == fixture.TileId);
        Assert.False(afterReverse.Focused);
        Assert.Equal(4, afterReverse.Version);
        Assert.Equal(2, notifier.Count);
        Assert.Equal(3, await db.Submissions.Where(x => x.Id == second.Id).Select(x => x.ApprovedContribution).SingleAsync());
        Assert.Equal(3, await db.SubmissionContributions.Where(x => x.TeamId == fixture.TeamId && x.ReversedAt == null).SumAsync(x => x.Amount));
    }

    [Fact]
    public async Task SubmissionMutationRejectsStaleExpectedVersionAfterApproval()
    {
        var fixture = await SeedProgressFixtureAsync();
        await using var db = new ApplicationDbContext(options);
        var submission = new Submission(Guid.NewGuid(), fixture.EventId, fixture.TeamId, fixture.TileId, fixture.RequirementId, null,
            fixture.ParticipantId, fixture.CharacterId, "Progress player", fixture.CaptainId, 1, now, "versioned", null);
        db.Submissions.Add(submission);
        await db.SaveChangesAsync();
        var service = new Bingo.Infrastructure.Evidence.SubmissionService(db, new NoopEvidenceStorage(), new FixedTimeProvider(now));

        Assert.Equal(1, submission.Version);
        Assert.Equal(1, await service.ApproveAsync(submission.Id, fixture.OwnerId, expectedVersion: 1));
        Assert.Equal(2, submission.Version);
        var stale = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReverseAsync(submission.Id, fixture.OwnerId, "Stale reversal", expectedVersion: 1));
        Assert.Contains("changed in another request", stale.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EventStartActivatesConfirmedPrimaryExactlyOnceAndExcludesInformationalAssignments()
    {
        var fixture = await SeedFixtureAsync(includeInformational: true);
        await using (var start = new ApplicationDbContext(options))
        {
            var lifecycle = new EventLifecycleService(start, new NoopSignupLifecycleService(), new FixedTimeProvider(now));
            var item = await start.Events.SingleAsync(x => x.Id == fixture.EventId);
            var result = await lifecycle.StartNowAsync(item.Id, item.Version, true, null, new(Guid.NewGuid(), "test-admin"));
            Assert.True(result.Succeeded, result.Error);
        }

        await using (var retry = new ApplicationDbContext(options))
        {
            var lifecycle = new EventLifecycleService(retry, new NoopSignupLifecycleService(), new FixedTimeProvider(now));
            var item = await retry.Events.SingleAsync(x => x.Id == fixture.EventId);
            var result = await lifecycle.StartNowAsync(item.Id, item.Version, true, null, new(Guid.NewGuid(), "test-admin"));
            Assert.False(result.Succeeded);
            Assert.Contains("pre-live state", result.Error, StringComparison.Ordinal);
        }

        await using var verify = new ApplicationDbContext(options);
        var transition = Assert.Single(await verify.EventParticipantCharacterSwaps.Where(x => x.EventParticipantId == fixture.ParticipantId).ToListAsync());
        Assert.Null(transition.PreviousOsrsCharacterId);
        Assert.Equal(fixture.PrimaryCharacterId, transition.NextOsrsCharacterId);
        Assert.Equal(now, transition.EffectiveAtUtc);
        Assert.Empty(await verify.EventParticipantCharacterSwaps.Where(x => x.NextOsrsCharacterId == fixture.InformationalCharacterId).ToListAsync());
    }

    [Fact]
    public async Task WebsiteEventStartUsesBuiltInPrimaryWhenAnotherPlayingAssignmentIsOlder()
    {
        var fixture = await SeedFixtureAsync(includeInformational: false, secondPlaying: true, secondaryPlayingFirst: true, source: SignupSource.Website);
        await using (var start = new ApplicationDbContext(options))
        {
            var lifecycle = new EventLifecycleService(start, new NoopSignupLifecycleService(), new FixedTimeProvider(now));
            var item = await start.Events.SingleAsync(x => x.Id == fixture.EventId);
            var result = await lifecycle.StartNowAsync(item.Id, item.Version, true, null, new(fixture.OwnerId, "owner"));
            Assert.True(result.Succeeded, result.Error);
        }

        await using var verify = new ApplicationDbContext(options);
        var transition = Assert.Single(await verify.EventParticipantCharacterSwaps.Where(x => x.EventParticipantId == fixture.ParticipantId).ToListAsync());
        Assert.Equal(fixture.PrimaryCharacterId, transition.NextOsrsCharacterId);
    }

    [Fact]
    public async Task EventStartFailsClosedWhenAConfirmedParticipantLacksPlayingAuthority()
    {
        var fixture = await SeedFixtureAsync(includeInformational: false, omitPrimary: true);
        await using var db = new ApplicationDbContext(options);
        var lifecycle = new EventLifecycleService(db, new NoopSignupLifecycleService(), new FixedTimeProvider(now));
        var item = await db.Events.SingleAsync(x => x.Id == fixture.EventId);
        var readiness = await lifecycle.GetStartReadinessAsync(item.Id);
        var result = await lifecycle.StartNowAsync(item.Id, item.Version, true, null, new(Guid.NewGuid(), "test-admin"));

        Assert.False(result.Succeeded);
        Assert.Contains("Playing assignment", result.Error, StringComparison.Ordinal);
        Assert.Equal($"/Admin/Events/Participant/{fixture.EventId}/Participants/{fixture.ParticipantId}", Assert.Single(readiness!.Blockers, x => x.Code == "PARTICIPANT_PLAYING_ASSIGNMENT_INVALID").Route);
        Assert.Equal(EventState.SignupClosed, (await db.Events.SingleAsync(x => x.Id == fixture.EventId)).State);
        Assert.Empty(await db.EventParticipantCharacterSwaps.Where(x => x.EventParticipantId == fixture.ParticipantId).ToListAsync());
    }

    [Fact]
    public async Task WebsiteEventStartFailsClosedForMissingOrAmbiguousPrimaryWithoutResidue()
    {
        var missing = await SeedFixtureAsync(includeInformational: false, source: SignupSource.Website, omitPrimary: true);
        var ambiguous = await SeedFixtureAsync(includeInformational: false, source: SignupSource.Website);
        await using (var setup = new ApplicationDbContext(options))
        {
            var secondary = new OsrsCharacter(Guid.NewGuid(), "Ambiguous", "AMBIGUOUS", now);
            setup.OsrsCharacters.Add(secondary);
            setup.EventParticipantCharacters.Add(new EventParticipantCharacter(
                Guid.NewGuid(), ambiguous.EventId, ambiguous.ParticipantId, secondary.Id, 1, now.AddDays(-2),
                ambiguous.OwnerId, ambiguous.PrimaryQuestionId, EventCharacterRole.Playing, 8, EhbSource.Manual, null));
            await setup.SaveChangesAsync();
        }

        foreach (var fixture in new[] { missing, ambiguous })
        {
            await using var start = new ApplicationDbContext(options);
            var lifecycle = new EventLifecycleService(start, new NoopSignupLifecycleService(), new FixedTimeProvider(now));
            var item = await start.Events.SingleAsync(x => x.Id == fixture.EventId);
            var result = await lifecycle.StartNowAsync(item.Id, item.Version, true, null, new(fixture.OwnerId, "owner"));
            Assert.False(result.Succeeded);
            Assert.Contains("Playing assignment", result.Error, StringComparison.Ordinal);
        }

        await using var verify = new ApplicationDbContext(options);
        foreach (var fixture in new[] { missing, ambiguous })
        {
            Assert.Equal(EventState.SignupClosed, (await verify.Events.SingleAsync(x => x.Id == fixture.EventId)).State);
            Assert.Empty(await verify.EventParticipantCharacterSwaps.Where(x => x.EventId == fixture.EventId).ToListAsync());
            Assert.Empty(await verify.EventStateTransitions.Where(x => x.EventId == fixture.EventId).ToListAsync());
            Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == fixture.EventId).ToListAsync());
        }
    }

    [Fact]
    public async Task ActiveCharacterResolutionUsesLatestTransitionAtUtcInstant()
    {
        var fixture = await SeedFixtureAsync(includeInformational: false, secondPlaying: true);
        await using (var start = new ApplicationDbContext(options))
        {
            var lifecycle = new EventLifecycleService(start, new NoopSignupLifecycleService(), new FixedTimeProvider(now));
            var item = await start.Events.SingleAsync(x => x.Id == fixture.EventId);
            var result = await lifecycle.StartNowAsync(item.Id, item.Version, true, null, new(Guid.NewGuid(), "test-admin"));
            Assert.True(result.Succeeded, result.Error);
        }

        var effective = now.AddMinutes(5);
        await using (var append = new ApplicationDbContext(options))
        {
            append.EventParticipantCharacterSwaps.Add(new EventParticipantCharacterSwap(
                Guid.NewGuid(), fixture.EventId, fixture.ParticipantId, fixture.PrimaryCharacterId,
                fixture.SecondPlayingCharacterId!.Value, effective, now, null, "test correction"));
            await append.SaveChangesAsync();
        }

        await using var query = new ApplicationDbContext(options);
        var transitions = await query.EventParticipantCharacterSwaps.Where(x => x.EventParticipantId == fixture.ParticipantId).OrderBy(x => x.EffectiveAtUtc).ToListAsync();
        Assert.Equal(2, transitions.Count);
        Assert.Equal(now, transitions[0].EffectiveAtUtc);
        Assert.Null(await query.ActiveCharacterAtAsync(fixture.EventId, fixture.ParticipantId, now.AddSeconds(-1)));
        Assert.Equal(fixture.PrimaryCharacterId, (await query.ActiveCharacterAtAsync(fixture.EventId, fixture.ParticipantId, now))!.OsrsCharacterId);
        Assert.Equal(fixture.PrimaryCharacterId, (await query.ActiveCharacterAtAsync(fixture.EventId, fixture.ParticipantId, effective.AddTicks(-1)))!.OsrsCharacterId);
        Assert.Equal(fixture.SecondPlayingCharacterId, (await query.ActiveCharacterAtAsync(fixture.EventId, fixture.ParticipantId, effective))!.OsrsCharacterId);
    }

    [Fact]
    public async Task LiveParticipantSwapUsesNextWholeMinuteAndBlocksPendingRetry()
    {
        var fixture = await SeedFixtureAsync(includeInformational: false, secondPlaying: true, includeTeam: true);
        await using (var start = new ApplicationDbContext(options))
        {
            var lifecycle = new EventLifecycleService(start, new NoopSignupLifecycleService(), new FixedTimeProvider(now));
            var item = await start.Events.SingleAsync(x => x.Id == fixture.EventId);
            var result = await lifecycle.StartNowAsync(item.Id, item.Version, true, null, new(fixture.OwnerId, "owner"));
            Assert.True(result.Succeeded, result.Error);
        }

        var requestTime = now.AddSeconds(12);
        await using (var swap = new ApplicationDbContext(options))
        {
            var service = new ParticipantLiveService(swap, new FixedTimeProvider(requestTime));
            var result = await service.SwapAsync(new(fixture.EventId, fixture.ParticipantId, fixture.PrimaryCharacterId,
                fixture.SecondPlayingCharacterId!.Value, fixture.OwnerId, "owner"));
            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(now.AddMinutes(1), result.EffectiveAtUtc);
        }

        await using (var retry = new ApplicationDbContext(options))
        {
            var service = new ParticipantLiveService(retry, new FixedTimeProvider(requestTime));
            var result = await service.SwapAsync(new(fixture.EventId, fixture.ParticipantId, fixture.PrimaryCharacterId,
                fixture.SecondPlayingCharacterId!.Value, fixture.OwnerId, "owner"));
            Assert.False(result.Succeeded);
            Assert.Contains("pending", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(fixture.PrimaryCharacterId, (await verify.ActiveCharacterAtAsync(fixture.EventId, fixture.ParticipantId, requestTime))!.OsrsCharacterId);
        Assert.Equal(fixture.SecondPlayingCharacterId, (await verify.ActiveCharacterAtAsync(fixture.EventId, fixture.ParticipantId, now.AddMinutes(1)))!.OsrsCharacterId);
    }

    [Fact]
    public async Task LiveParticipantSwapProjectionAndMutationRejectAtOrAfterConfiguredEnd()
    {
        var fixture = await SeedFixtureAsync(includeInformational: false, secondPlaying: true, includeTeam: true);
        await using (var start = new ApplicationDbContext(options))
        {
            var lifecycle = new EventLifecycleService(start, new NoopSignupLifecycleService(), new FixedTimeProvider(now));
            var item = await start.Events.SingleAsync(x => x.Id == fixture.EventId);
            var result = await lifecycle.StartNowAsync(item.Id, item.Version, true, null, new(fixture.OwnerId, "owner"));
            Assert.True(result.Succeeded, result.Error);
        }

        var eventEnd = now.AddHours(1);
        foreach (var instant in new[] { eventEnd, eventEnd.AddSeconds(1) })
        {
            await using var attempt = new ApplicationDbContext(options);
            var service = new ParticipantLiveService(attempt, new FixedTimeProvider(instant));
            var context = await service.GetContextAsync(fixture.EventId, fixture.ParticipantId, fixture.OwnerId);
            Assert.NotNull(context);
            Assert.False(context!.CanSwap);

            var result = await service.SwapAsync(new(
                fixture.EventId, fixture.ParticipantId, fixture.PrimaryCharacterId,
                fixture.SecondPlayingCharacterId!.Value, fixture.OwnerId, "owner"));
            Assert.False(result.Succeeded);
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(EventState.Live, (await verify.Events.SingleAsync(x => x.Id == fixture.EventId)).State);
        Assert.Single(await verify.EventParticipantCharacterSwaps.Where(x => x.EventParticipantId == fixture.ParticipantId).ToListAsync());
    }

    [Fact]
    public async Task TeamFocusIsPrivateRoleAwareOptimisticAndReadOnlyAfterEventEnd()
    {
        var fixture = await SeedFixtureAsync(includeInformational: false, includeTeam: true);
        var outsiderId = Guid.NewGuid();
        var superAdminId = Guid.NewGuid();
        var emergencyId = Guid.NewGuid();
        var otherTeamId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            var outsider = Account.CreateWebsite(outsiderId, "Focus outsider", "FOCUS OUTSIDER", now);
            var superAdmin = Account.CreateWebsite(superAdminId, "Focus super admin", "FOCUS SUPER ADMIN", now);
            superAdmin.SetGlobalRole(GlobalRole.SuperAdmin);
            var emergency = Account.CreateEmergency(emergencyId, "Focus emergency", "FOCUS EMERGENCY", now);
            emergency.Enable();
            var emergencyAccess = new AccountEventAccess(Guid.NewGuid(), emergencyId, fixture.EventId, fixture.TeamId, null, now.AddHours(-1), null, null);
            emergencyAccess.Enable();
            setup.AddRange(outsider, superAdmin, emergency, emergencyAccess);
            await setup.SaveChangesAsync();
        }

        await using (var start = new ApplicationDbContext(options))
        {
            var lifecycle = new EventLifecycleService(start, new NoopSignupLifecycleService(), new FixedTimeProvider(now));
            var item = await start.Events.SingleAsync(x => x.Id == fixture.EventId);
            var result = await lifecycle.StartNowAsync(item.Id, item.Version, true, null, new(fixture.OwnerId, "owner"));
            Assert.True(result.Succeeded, result.Error);
        }

        await using (var otherTeam = new ApplicationDbContext(options))
        {
            otherTeam.AddRange(
                new Team(otherTeamId, fixture.EventId, "Other focus team", "other-focus-team", TeamFormationType.Preformed, null, false),
                new TeamFocusMarker(Guid.NewGuid(), fixture.EventId, otherTeamId, TeamFocusTargetKind.Row, null, 0, null, true, now, null));
            await otherTeam.SaveChangesAsync();
        }

        await using (var mutate = new ApplicationDbContext(options))
        {
            var service = new TeamFocusService(mutate, new FixedTimeProvider(now));
            var ownerContext = await service.GetContextAsync(fixture.EventId, fixture.TeamId, fixture.OwnerId, false);
            Assert.NotNull(ownerContext);
            Assert.True(ownerContext!.IsVisible);
            Assert.True(ownerContext.CanMutate);

            var emergencyContext = await service.GetContextAsync(fixture.EventId, fixture.TeamId, emergencyId, false);
            Assert.NotNull(emergencyContext);
            Assert.True(emergencyContext!.IsVisible);
            Assert.True(emergencyContext.CanMutate);
            var emergencyFocus = await service.SetFocusAsync(new(
                fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Column, null, null, 0, true, 0, emergencyId));
            Assert.True(emergencyFocus.Succeeded, emergencyFocus.Error);

            var focused = await service.SetFocusAsync(new(
                fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Row, null, 0, null, true, 0, fixture.OwnerId));
            Assert.True(focused.Succeeded, focused.Error);
            Assert.Equal(1, focused.Version);

            var stale = await service.SetFocusAsync(new(
                fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Row, null, 0, null, false, 0, fixture.OwnerId));
            Assert.False(stale.Succeeded);

            var outsiderContext = await service.GetContextAsync(fixture.EventId, fixture.TeamId, outsiderId, false);
            Assert.NotNull(outsiderContext);
            Assert.False(outsiderContext!.IsVisible);
            Assert.False(outsiderContext.CanInspect);
            Assert.Empty(outsiderContext.Markers);

            var hiddenInspection = await service.GetContextAsync(fixture.EventId, otherTeamId, superAdminId, false);
            Assert.NotNull(hiddenInspection);
            Assert.False(hiddenInspection!.IsVisible);
            Assert.True(hiddenInspection.CanInspect);
            Assert.Empty(hiddenInspection.Markers);

            var inspection = await service.GetContextAsync(fixture.EventId, otherTeamId, superAdminId, true);
            Assert.NotNull(inspection);
            Assert.True(inspection!.IsVisible);
            Assert.True(inspection.IsInspection);
            Assert.False(inspection.CanMutate);
            Assert.Single(inspection.Markers);

            var unauthorized = await service.SetFocusAsync(new(
                fixture.EventId, otherTeamId, TeamFocusTargetKind.Row, null, 0, null, false, 1, superAdminId));
            Assert.False(unauthorized.Succeeded);
        }

        await using (var end = new ApplicationDbContext(options))
        {
            var lifecycle = new EventLifecycleService(end, new NoopSignupLifecycleService(), new FixedTimeProvider(now));
            var item = await end.Events.SingleAsync(x => x.Id == fixture.EventId);
            var result = await lifecycle.EndNowAsync(item.Id, item.Version, true, "test end", new(fixture.OwnerId, "owner"));
            Assert.True(result.Succeeded, result.Error);
        }

        await using var ended = new ApplicationDbContext(options);
        var endedService = new TeamFocusService(ended, new FixedTimeProvider(now));
        var endedContext = await endedService.GetContextAsync(fixture.EventId, fixture.TeamId, fixture.OwnerId, false);
        Assert.NotNull(endedContext);
        Assert.False(endedContext!.CanMutate);
        var afterEnd = await endedService.SetFocusAsync(new(
            fixture.EventId, fixture.TeamId, TeamFocusTargetKind.Row, null, 0, null, false, 1, fixture.OwnerId));
        Assert.False(afterEnd.Succeeded);
    }

    private async Task<Fixture> SeedFixtureAsync(
        bool includeInformational,
        bool secondPlaying = false,
        bool includeTeam = false,
        bool omitPrimary = false,
        bool secondaryPlayingFirst = false,
        SignupSource source = SignupSource.AdminCreated)
    {
        var eventId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var primaryCharacterId = Guid.NewGuid();
        var informationalCharacterId = Guid.NewGuid();
        var secondPlayingCharacterId = secondPlaying ? Guid.NewGuid() : (Guid?)null;
        var adminId = Guid.NewGuid();
        var formId = Guid.NewGuid();
        var primaryQuestionId = Guid.NewGuid();
        var informationalQuestionId = Guid.NewGuid();
        var secondaryQuestionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        await using var db = new ApplicationDbContext(options);
        var adminLogin = $"slice-7-admin-{eventId:N}";
        var admin = Account.CreateWebsite(adminId, adminLogin, adminLogin.ToUpperInvariant(), now);
        var item = new BingoEvent(eventId, "Slice 7 event", $"slice-7-{eventId:N}", "UTC", adminId, now);
        item.UpdateIdentity(item.Name, item.Slug, "A public event description.", "UTC");
        item.ConfigureSchedule(now.AddHours(-2), now.AddHours(-1), null, now, now.AddHours(1), 20);
        item.ConfigureSignup(true, false, null);
        item.OpenSignups(now.AddHours(-2));
        item.CloseSignups(now.AddHours(-1));
        var form = new SignupForm(formId, eventId, now);
        var primaryQuestion = new SignupQuestion(primaryQuestionId, formId, eventId, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing);
        var informationalQuestion = new SignupQuestion(informationalQuestionId, formId, eventId, "support_alt", "Support alt", SignupQuestionType.Account, false, 1, null, SignupSystemField.None, EventCharacterRole.Informational);
        var secondaryQuestion = new SignupQuestion(secondaryQuestionId, formId, eventId, "secondary_regular", "Secondary account", SignupQuestionType.Account, false, 2, null, SignupSystemField.None, EventCharacterRole.Playing);
        var participant = new EventParticipant(participantId, eventId, SignupStatus.Confirmed, 1, now.AddDays(-1), source);
        if (includeTeam) participant.AssignOwner(admin);
        var primaryName = $"Primary {eventId:N}";
        var informationalName = $"Informational {eventId:N}";
        var primary = new OsrsCharacter(primaryCharacterId, primaryName, primaryName.ToUpperInvariant(), now);
        var informational = new OsrsCharacter(informationalCharacterId, informationalName, informationalName.ToUpperInvariant(), now);
        var assignments = new List<EventParticipantCharacter>();
        if (!omitPrimary)
            assignments.Add(new(Guid.NewGuid(), eventId, participantId, primaryCharacterId, secondaryPlayingFirst ? 1 : 0, now.AddDays(-1), adminId, primaryQuestionId, EventCharacterRole.Playing, 10, EhbSource.Manual, null));
        if (secondPlayingCharacterId is { } second)
        {
            var secondName = $"Second {eventId:N}";
            var secondCharacter = new OsrsCharacter(second, secondName, secondName.ToUpperInvariant(), now);
            db.OsrsCharacters.Add(secondCharacter);
            assignments.Add(new(Guid.NewGuid(), eventId, participantId, second, secondaryPlayingFirst ? 0 : assignments.Count, now.AddDays(-1), adminId,
                source == SignupSource.Website ? secondaryQuestionId : null, EventCharacterRole.Playing, 9, EhbSource.Manual, null));
        }
        if (includeInformational)
            assignments.Add(new(Guid.NewGuid(), eventId, participantId, informationalCharacterId, assignments.Count, now.AddDays(-1), adminId, informationalQuestionId, EventCharacterRole.Informational, null, null, null));

        var board = new Board(Guid.NewGuid(), eventId, "Published board", 1, 1);
        var draft = new DraftSession(Guid.NewGuid(), eventId, 1);
        draft.Start(now.AddDays(-2));
        draft.Finalize(now.AddDays(-1));
        db.AddRange(admin, item, form, primaryQuestion, informationalQuestion, secondaryQuestion, participant, primary, informational);
        db.EventParticipantCharacters.AddRange(assignments);
        if (!includeInformational)
            db.OsrsCharacters.Remove(informational);
        db.Boards.Add(board);
        db.DraftSessions.Add(draft);
        await BoardApprovalFixture.PublishAsync(db, board, now);
        if (includeTeam)
        {
            db.Teams.Add(new Team(teamId, eventId, "Focus team", "focus-team", TeamFormationType.Preformed, null, false));
            db.TeamMemberships.Add(new TeamMembership(Guid.NewGuid(), teamId, participantId, TeamMembershipRole.Captain, now, null, "test"));
        }
        await db.SaveChangesAsync();
        return new(eventId, participantId, teamId, board.Id, primaryCharacterId, informationalCharacterId, secondPlayingCharacterId, adminId, primaryQuestionId);
    }

    private sealed record Fixture(Guid EventId, Guid ParticipantId, Guid TeamId, Guid BoardId, Guid PrimaryCharacterId, Guid InformationalCharacterId, Guid? SecondPlayingCharacterId, Guid OwnerId, Guid PrimaryQuestionId);

    private async Task<ProgressFixture> SeedProgressFixtureAsync()
    {
        var eventId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var captainId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var tileId = Guid.NewGuid();
        var requirementId = Guid.NewGuid();
        var characterId = Guid.NewGuid();
        var owner = Account.CreateWebsite(ownerId, $"slice7-owner-{eventId:N}", $"SLICE7-OWNER-{eventId:N}", now);
        owner.SetGlobalRole(GlobalRole.Admin);
        var captain = Account.CreateEmergency(captainId, $"slice7-captain-{eventId:N}", $"SLICE7-CAPTAIN-{eventId:N}", now);
        var item = new BingoEvent(eventId, "Slice 7 progress", $"slice7-progress-{eventId:N}", "UTC", ownerId, now);
        item.ConfigureSchedule(now.AddHours(-2), now.AddHours(-1), null, now.AddHours(-1), now.AddHours(1), 20);
        item.OpenSignups(now.AddHours(-2));
        item.CloseSignups(now.AddHours(-1));
        item.StartEvent(now.AddHours(-1));
        var participant = new EventParticipant(participantId, eventId, SignupStatus.Confirmed, 1, now.AddDays(-1), SignupSource.AdminCreated);
        participant.AssignOwner(owner);
        var character = new OsrsCharacter(characterId, "Progress player", "PROGRESS PLAYER", now);
        var assignment = new EventParticipantCharacter(Guid.NewGuid(), eventId, participantId, characterId, 0, now.AddDays(-1), ownerId, null, EventCharacterRole.Playing, 1, EhbSource.Manual, null);
        var team = new Team(teamId, eventId, "Progress team", $"progress-team-{eventId:N}", TeamFormationType.Preformed, null, false);
        var membership = new TeamMembership(Guid.NewGuid(), teamId, participantId, TeamMembershipRole.Captain, now.AddDays(-1), null, "test");
        var access = new AccountEventAccess(Guid.NewGuid(), captainId, eventId, teamId, participantId, now.AddDays(-1), now.AddHours(1), now.AddHours(2));
        access.Enable();
        var board = new Board(boardId, eventId, "Progress board", 1, 1);
        var tile = new BoardTile(tileId, boardId, Guid.NewGuid(), 0, 0, "Progress tile", "Complete it", "Evidence", 1);
        var requirement = new BoardRequirementSnapshot(requirementId, tileId, 0, 3, true, true, "Complete it", true);
        await using var db = new ApplicationDbContext(options);
        db.AddRange(owner, captain, item, participant, character, assignment, team, membership, access, board, tile, requirement);
        await BoardApprovalFixture.PublishAsync(db, board, now.AddMinutes(-30), [tile], [requirement]);
        return new(eventId, teamId, participantId, ownerId, captainId, tileId, requirementId, characterId);
    }

    private sealed record ProgressFixture(Guid EventId, Guid TeamId, Guid ParticipantId, Guid OwnerId, Guid CaptainId, Guid TileId, Guid RequirementId, Guid CharacterId);

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class NoopSignupLifecycleService : Bingo.Application.Events.IEventSignupLifecycleService
    {
        public Task<Bingo.Application.Events.SignupLifecycleResult> SaveScheduleAsync(Guid eventId, long version, Bingo.Application.Events.EventScheduleValues values, bool confirmChanges, LifecycleActor actor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Bingo.Application.Events.SignupLifecycleResult> SaveScheduleAsync(Guid eventId, long version, Bingo.Application.Events.EventScheduleValues values, bool confirmChanges, LifecycleActor actor, string? reason, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Bingo.Application.Events.SignupLifecycleResult> OpenAsync(Guid eventId, long version, bool acknowledgeWarnings, bool acceptProposedClose, LifecycleActor actor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Bingo.Application.Events.SignupLifecycleResult> CloseAsync(Guid eventId, long version, LifecycleActor actor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Bingo.Application.Events.SignupLifecycleResult> ReopenAsync(Guid eventId, long version, bool acknowledgeWarnings, bool acceptProposedClose, LifecycleActor actor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ProcessDueSignupAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class CountingFocusNotifier : ITeamFocusNotifier
    {
        public int Count { get; private set; }
        public Task NotifyTeamFocusChangedAsync(Guid eventId, Guid teamId, CancellationToken cancellationToken = default) { Count++; return Task.CompletedTask; }
    }

    private sealed class NoopEvidenceStorage : IEvidenceStorage
    {
        public Task<StoredEvidence> StoreAsync(Guid eventId, Guid submissionId, string originalFilename, Stream content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubBoardService(PublicEventBoard board, PublicTileDetails? tile = null) : IPublicBoardService
    {
        public Task<PublicEventBoard?> GetEventBoardAsync(string eventSlug, CancellationToken cancellationToken = default) => Task.FromResult<PublicEventBoard?>(board);
        public Task<PublicTileDetails?> GetTileAsync(string eventSlug, string teamSlug, Guid tileId, CancellationToken cancellationToken = default) => Task.FromResult(tile);
    }

    private sealed class ProjectionEvidenceAuthority(Guid adminId, Guid eventId, Guid teamId) : IEvidenceAuthority
    {
        public Task<EvidenceActorScope> ResolveActorAsync(Guid actorAccountId, Guid? requestedEventId, Guid? requestedTeamId, DateTimeOffset now, CancellationToken cancellationToken = default)
            => Task.FromResult(actorAccountId == adminId
                ? new EvidenceActorScope(EvidenceActorKind.Administrator, actorAccountId, requestedEventId ?? eventId, requestedTeamId ?? teamId, Guid.Empty)
                : new EvidenceActorScope(EvidenceActorKind.Participant, actorAccountId, eventId, teamId, Guid.NewGuid()));
        public Task<EvidenceActorScope> AuthorizeAsync(Guid actorAccountId, Guid requestedEventId, Guid requestedTeamId, Guid creditedParticipantId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EvidenceActorScope> AuthorizeOwnerAsync(Guid actorAccountId, Guid requestedEventId, Guid requestedTeamId, Guid creditedParticipantId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> CanViewPrivateEvidenceAsync(Guid actorAccountId, Guid requestedEventId, Guid requestedTeamId, Guid creditedParticipantId, DateTimeOffset now, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<IReadOnlyList<EvidenceCandidate>> GetCurrentTeamCandidatesAsync(EvidenceActorScope scope, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EvidenceCandidate>>([]);
        public Task<CreditedCharacterSnapshot> ResolveCreditedCharacterAsync(Guid requestedEventId, Guid requestedParticipantId, DateTimeOffset submittedAt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class DictionaryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
