using System.Collections.Concurrent;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Bingo.Application.Dashboard;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Dashboard;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class AdminDashboardRemediationIntegrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Clock = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = CreateOptions();
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task LiveMembershipEligibilityUsesStartAndRequestClockEndpoints()
    {
        var adminId = Guid.NewGuid();
        var admin = Admin(adminId, "dashboard-r1-admin");
        var start = Clock.AddHours(-2);
        var live = new BingoEvent(Guid.NewGuid(), "R1 live", "dashboard-r1-live", null, "UTC",
            Clock.AddDays(-3), Clock.AddDays(-2), start, Clock.AddHours(2), Clock.AddHours(2), 10, adminId,
            Clock.AddDays(-3));
        live.OpenSignups(Clock.AddDays(-3));
        live.CloseSignups(Clock.AddDays(-2));
        live.SetDraftLocked(true, Clock.AddDays(-2));
        live.StartEvent(start);
        var team = new Team(Guid.NewGuid(), live.Id, "R1 team", "r1-team", "fixture", false, start);
        var atStart = Participant(live.Id, start.AddHours(-1), sequence: 1);
        var atClock = Participant(live.Id, start, sequence: 2);
        var joinedAtClock = Participant(live.Id, Clock, sequence: 3);
        var reversed = Participant(live.Id, Clock, sequence: 4);
        var midInterval = Participant(live.Id, start.AddMinutes(1), sequence: 5);
        var atStartMembership = Membership(team.Id, atStart.Id, start.AddHours(-1));
        atStartMembership.Leave(start, "R1 start boundary");
        var atClockMembership = Membership(team.Id, atClock.Id, start);
        atClockMembership.Leave(Clock, "R1 clock boundary");
        var joinedAtClockMembership = Membership(team.Id, joinedAtClock.Id, Clock);
        var reversedMembership = Membership(team.Id, reversed.Id, Clock);
        reversedMembership.Leave(start, "R1 reversed interval");
        var midIntervalMembership = Membership(team.Id, midInterval.Id, start.AddMinutes(1));
        midIntervalMembership.Leave(Clock.AddMinutes(-1), "R1 interior departure");

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, live, team, atStart, atClock, joinedAtClock, reversed, midInterval,
                atStartMembership, atClockMembership, joinedAtClockMembership, reversedMembership,
                midIntervalMembership);
            await db.SaveChangesAsync();
        }

        await using var read = new ApplicationDbContext(options);
        var result = await new AdminDashboardService(read, new FixedTimeProvider(Clock)).GetAsync(adminId);
        var history = Assert.Single(result.History);

        Assert.Equal(3, history.Participants.Value);
        Assert.Equal(DashboardValueAvailability.Measured, history.Participants.Availability);
    }

    [Fact]
    public async Task UnusableActualDatesRemainUnavailableAndDoNotCreateAnEndedFallback()
    {
        var adminId = Guid.NewGuid();
        var admin = Admin(adminId, "dashboard-r2-admin");
        var missing = Archived(Guid.NewGuid(), "R2 missing", "dashboard-r2-missing", adminId, Clock.AddDays(-20), Clock.AddDays(-18));
        var inverted = Archived(Guid.NewGuid(), "R2 inverted", "dashboard-r2-inverted", adminId, Clock.AddDays(-10), Clock.AddDays(-8));
        var recent = Account.CreateWebsite(Guid.NewGuid(), "r2-recent", "R2-RECENT", Clock.AddDays(-20));

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, missing, inverted, recent);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET actual_started_at = NULL, actual_ended_at = NULL WHERE id = {missing.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE events SET actual_ended_at = actual_started_at - interval '1 microsecond' WHERE id = {inverted.Id}");
        }

        await using var read = new ApplicationDbContext(options);
        var result = await new AdminDashboardService(read, new FixedTimeProvider(Clock)).GetAsync(adminId);

        Assert.Equal(2, result.Statistics.EventsHeld.Value);
        Assert.Equal(DashboardValueAvailability.Unavailable, result.Statistics.UniqueWebsiteParticipants.Availability);
        Assert.Equal(DashboardValueAvailability.Unavailable, result.Statistics.EventParticipations.Availability);
        Assert.Equal(DashboardValueAvailability.Unavailable, result.Statistics.ApprovedSubmissions.Availability);
        Assert.Empty(result.ParticipationChart);
        Assert.Null(result.LatestEndedRecap);
        Assert.False(result.Community.UsedThirtyDayFallback);
        Assert.Equal(DashboardValueAvailability.Unavailable, result.Community.NewWebsiteAccounts.Availability);
        Assert.Null(result.Community.Since);
        Assert.All(result.History, row =>
        {
            Assert.Equal(DashboardValueAvailability.Unavailable, row.Participants.Availability);
            Assert.Equal(DashboardEhbCoverage.Unavailable, row.Ehb.Coverage);
        });
    }

    [Fact]
    public async Task CommunityLoginCountsUseIndependentThirtyDayBoundaryAndRetainDisabledAccounts()
    {
        var adminId = Guid.NewGuid();
        var admin = Admin(adminId, "dashboard-r3-admin");
        var ended = Archived(Guid.NewGuid(), "R3 ended", "dashboard-r3-ended", adminId, Clock.AddDays(-10), Clock.AddDays(-8));
        var inside = Account.CreateWebsite(Guid.NewGuid(), "r3-inside", "R3-INSIDE", Clock.AddDays(-40));
        inside.RecordLogin(Clock.AddDays(-20));
        var exact = Account.CreateWebsite(Guid.NewGuid(), "r3-exact", "R3-EXACT", Clock.AddDays(-40));
        exact.RecordLogin(Clock.AddDays(-30));
        var future = Account.CreateWebsite(Guid.NewGuid(), "r3-future", "R3-FUTURE", Clock.AddDays(-40));
        future.RecordLogin(Clock.AddHours(1));
        var disabled = Account.CreateWebsite(Guid.NewGuid(), "r3-disabled", "R3-DISABLED", Clock.AddDays(-40));
        disabled.Disable(Clock.AddDays(-1), adminId, "R3 fixture");
        disabled.RecordLogin(Clock.AddDays(-20));

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, ended, inside, exact, future, disabled);
            await db.SaveChangesAsync();
        }

        await using var read = new ApplicationDbContext(options);
        var result = await new AdminDashboardService(read, new FixedTimeProvider(Clock)).GetAsync(adminId);

        Assert.False(result.Community.UsedThirtyDayFallback);
        Assert.Equal(2, result.Community.LoggedInWebsiteAccounts.Value);
    }

    [Fact]
    public async Task CardOrdersOverdueBeforeFutureAndUsesStableScheduledTies()
    {
        var adminId = Guid.NewGuid();
        var admin = Admin(adminId, "dashboard-r4-scheduled-admin");
        var tiedLow = Draft(Guid.Parse("00000000-0000-0000-0000-000000000001"), "R4 tied low", "dashboard-r4-tied-low", adminId,
            Clock.AddHours(-2), Clock.AddHours(-1));
        var overdue = Draft(Guid.Parse("00000000-0000-0000-0000-000000000002"), "R4 overdue", "dashboard-r4-overdue", adminId,
            Clock.AddHours(-2), Clock.AddHours(-3));
        var future = Draft(Guid.Parse("00000000-0000-0000-0000-000000000003"), "R4 future", "dashboard-r4-future", adminId,
            Clock.AddHours(1), Clock.AddHours(-4));

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, tiedLow, overdue, future);
            await db.SaveChangesAsync();
        }

        await using var read = new ApplicationDbContext(options);
        var result = await new AdminDashboardService(read, new FixedTimeProvider(Clock)).GetAsync(adminId);

        Assert.Equal(tiedLow.Id, result.CurrentEvent?.EventId);
        Assert.True(result.CurrentEvent?.IsOverdue == true);
    }

    [Fact]
    public async Task CardUnscheduledFallbackUsesStableIdWithoutCreatedAtOrdering()
    {
        var adminId = Guid.NewGuid();
        var admin = Admin(adminId, "dashboard-r4-unscheduled-admin");
        var lowId = Guid.Parse("00000000-0000-0000-0000-000000000010");
        var highId = Guid.Parse("00000000-0000-0000-0000-000000000011");
        var lowIdLater = Draft(lowId, "R4 unscheduled low", "dashboard-r4-unscheduled-low", adminId, null, Clock.AddHours(-1));
        var highIdEarlier = Draft(highId, "R4 unscheduled high", "dashboard-r4-unscheduled-high", adminId, null, Clock.AddHours(-2));

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, lowIdLater, highIdEarlier);
            await db.SaveChangesAsync();
        }

        await using var read = new ApplicationDbContext(options);
        var result = await new AdminDashboardService(read, new FixedTimeProvider(Clock)).GetAsync(adminId);

        Assert.Equal(lowId, result.CurrentEvent?.EventId);
        Assert.False(result.CurrentEvent?.IsOverdue);
    }

    [Fact]
    public async Task ImportedApprovedRowsExcludeReconstructedDataAndCountWeightedManualSubmissionOnce()
    {
        var adminId = Guid.NewGuid();
        var admin = Admin(adminId, "dashboard-r5-submission-admin");
        var ended = Archived(Guid.NewGuid(), "R5 submissions", "dashboard-r5-submissions", adminId, Clock.AddDays(-10), Clock.AddDays(-8));
        var team = new Team(Guid.NewGuid(), ended.Id, "R5 team", "r5-team", "fixture", false, ended.ActualStartedAt);
        var participant = Participant(ended.Id, ended.ActualStartedAt!.Value);
        var membership = Membership(team.Id, participant.Id, ended.ActualStartedAt.Value);
        var characterId = Guid.NewGuid();
        var character = new OsrsCharacter(characterId, "R5 character", "R5 CHARACTER", Clock.AddDays(-11));
        var board = new Board(Guid.NewGuid(), ended.Id, "R5 board", 2, 2);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "R5 manual tile", "", "", 0m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 1, true, false, "R5 manual", true);
        var weightedTile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 1, "R5 weighted tile", "", "", 0m);
        var weightedRequirement = new BoardRequirementSnapshot(Guid.NewGuid(), weightedTile.Id, 1, 1, true, true, "R5 published drop", false, creditedWeight: 2);
        var weightedDrop = new BoardRequirementDropSnapshot(Guid.NewGuid(), weightedRequirement.Id, Guid.NewGuid(), Guid.NewGuid(),
            "R5 boss", "R5 published drop", "1/100", .01m, 3, 1m, creditedWeight: 2);
        var importAt = Clock.AddDays(-9);
        var reconstructed = new Submission(Guid.NewGuid(), ended.Id, team.Id, tile.Id, requirement.Id, null,
            participant.Id, characterId, character.DisplayName, adminId, 3, Clock.AddDays(-10), null, null);
        reconstructed.Approve(3, Clock.AddDays(-9).AddHours(-1));
        var measured = new Submission(Guid.NewGuid(), ended.Id, team.Id, tile.Id, requirement.Id, null,
            participant.Id, characterId, character.DisplayName, adminId, 3, Clock.AddDays(-7), null, null);
        measured.Approve(3, Clock.AddDays(-7));
        var reconstructedContribution = new SubmissionContribution(Guid.NewGuid(), reconstructed.Id, team.Id, requirement.Id, null,
            participant.Id, 3, Clock.AddDays(-9).AddHours(-1));
        var measuredContribution = new SubmissionContribution(Guid.NewGuid(), measured.Id, team.Id, requirement.Id, null,
            participant.Id, 3, Clock.AddDays(-7));
        var weighted = new Submission(Guid.NewGuid(), ended.Id, team.Id, weightedTile.Id, weightedRequirement.Id, weightedDrop.Id,
            participant.Id, characterId, character.DisplayName, adminId, 2, Clock.AddDays(-7).AddHours(1), null, null);
        weighted.Approve(2, Clock.AddDays(-7).AddHours(2));
        var weightedContribution = new SubmissionContribution(Guid.NewGuid(), weighted.Id, team.Id, weightedRequirement.Id,
            weightedDrop.Id, participant.Id, 2, Clock.AddDays(-7).AddHours(2));
        var importAudit = new AuditEntry(Guid.NewGuid(), importAt, adminId, admin.LoginName,
            "historical_import.applied", "Event", ended.Id.ToString("D"), "csv import fixture", ended.Id);

        // This separate AdminCreated roster has no historical-import audit. Its
        // approved manual submission proves an ordinary roster remains measured.
        var ordinary = Archived(Guid.NewGuid(), "R5 ordinary roster", "dashboard-r5-ordinary", adminId,
            Clock.AddDays(-6), Clock.AddDays(-4));
        var ordinaryTeam = new Team(Guid.NewGuid(), ordinary.Id, "R5 ordinary team", "r5-ordinary-team", "fixture", false, ordinary.ActualStartedAt);
        var ordinaryParticipant = Participant(ordinary.Id, ordinary.ActualStartedAt!.Value);
        var ordinaryMembership = Membership(ordinaryTeam.Id, ordinaryParticipant.Id, ordinary.ActualStartedAt.Value);
        var ordinaryCharacter = new OsrsCharacter(Guid.NewGuid(), "R5 ordinary character", "R5 ORDINARY CHARACTER", Clock.AddDays(-7));
        var ordinaryBoard = new Board(Guid.NewGuid(), ordinary.Id, "R5 ordinary board", 1, 1);
        var ordinaryTile = new BoardTile(Guid.NewGuid(), ordinaryBoard.Id, Guid.NewGuid(), 0, 0, "R5 ordinary tile", "", "", 0m);
        var ordinaryRequirement = new BoardRequirementSnapshot(Guid.NewGuid(), ordinaryTile.Id, 1, 1, true, false, "R5 ordinary manual", true);
        var ordinarySubmission = new Submission(Guid.NewGuid(), ordinary.Id, ordinaryTeam.Id, ordinaryTile.Id, ordinaryRequirement.Id, null,
            ordinaryParticipant.Id, ordinaryCharacter.Id, ordinaryCharacter.DisplayName, adminId, 1, Clock.AddDays(-3), null, null);
        ordinarySubmission.Approve(1, Clock.AddDays(-3));
        var ordinaryContribution = new SubmissionContribution(Guid.NewGuid(), ordinarySubmission.Id, ordinaryTeam.Id,
            ordinaryRequirement.Id, null, ordinaryParticipant.Id, 1, Clock.AddDays(-3));

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, ended, team, participant, membership, character, board, tile, requirement,
                weightedTile, weightedRequirement, weightedDrop, reconstructed, measured, weighted,
                reconstructedContribution, measuredContribution, weightedContribution, importAudit,
                ordinary, ordinaryTeam, ordinaryParticipant, ordinaryMembership, ordinaryCharacter,
                ordinaryBoard, ordinaryTile, ordinaryRequirement, ordinarySubmission, ordinaryContribution);
            await db.SaveChangesAsync();
            await BoardApprovalFixture.PublishAsync(db, board, Clock.AddDays(-6), [tile, weightedTile],
                [requirement, weightedRequirement], [weightedDrop]);
            await BoardApprovalFixture.PublishAsync(db, ordinaryBoard, Clock.AddDays(-3), [ordinaryTile], [ordinaryRequirement]);
        }

        await using var read = new ApplicationDbContext(options);
        var result = await new AdminDashboardService(read, new FixedTimeProvider(Clock)).GetAsync(adminId);
        var history = Assert.Single(result.History, value => value.EventId == ended.Id);
        var ordinaryHistory = Assert.Single(result.History, value => value.EventId == ordinary.Id);

        Assert.Equal(2, history.ApprovedSubmissions.Value);
        Assert.Equal(DashboardValueAvailability.Measured, history.ApprovedSubmissions.Availability);
        Assert.Equal(1, ordinaryHistory.ApprovedSubmissions.Value);
        Assert.Equal(DashboardValueAvailability.Measured, ordinaryHistory.ApprovedSubmissions.Availability);
        Assert.Equal(3, result.Statistics.ApprovedSubmissions.Value);
        Assert.Equal(2, weightedContribution.Amount);
    }

    [Fact]
    public async Task OfficialWinnerKeepsSharedFirstPlaceRetainedNamesAndFrozenDenominatorAfterReopen()
    {
        var adminId = Guid.NewGuid();
        var admin = Admin(adminId, "dashboard-r5-winner-admin");
        var ended = Archived(Guid.NewGuid(), "R5 winners", "dashboard-r5-winners", adminId, Clock.AddDays(-10), Clock.AddDays(-8));
        var firstTeam = new Team(Guid.NewGuid(), ended.Id, "Current A", "r5-current-a", "fixture", false, ended.ActualStartedAt);
        var secondTeam = new Team(Guid.NewGuid(), ended.Id, "Current B", "r5-current-b", "fixture", false, ended.ActualStartedAt);
        var firstParticipant = Participant(ended.Id, ended.ActualStartedAt!.Value);
        var secondParticipant = Participant(ended.Id, ended.ActualStartedAt.Value, sequence: 2);
        var firstMembership = Membership(firstTeam.Id, firstParticipant.Id, ended.ActualStartedAt.Value);
        var secondMembership = Membership(secondTeam.Id, secondParticipant.Id, ended.ActualStartedAt.Value);
        var board = new Board(Guid.NewGuid(), ended.Id, "R5 two by two board", 2, 2);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "R5 frozen tile", "", "", 0m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 1, true, false, "R5 frozen manual", true);
        var oldReviewId = Guid.NewGuid();
        var activeReviewId = Guid.NewGuid();
        var oldFinalization = new EventFinalizationSnapshot(Guid.NewGuid(), ended.Id, 1, Clock.AddDays(-6), adminId, oldReviewId);
        oldFinalization.Unfinalize(Clock.AddDays(-5), adminId, "R5 reopen");
        var activeFinalization = new EventFinalizationSnapshot(Guid.NewGuid(), ended.Id, 2, Clock.AddDays(-4), adminId, activeReviewId);
        var oldReview = new EventStateTransition(oldReviewId, ended.Id, EventState.Live, EventState.AwaitingFinalReview,
            adminId, Clock.AddDays(-6), "R5 old review", effectiveAt: Clock.AddDays(-6));
        var activeReview = new EventStateTransition(activeReviewId, ended.Id, EventState.AwaitingFinalReview, EventState.Finalized,
            adminId, Clock.AddDays(-4), "R5 active review", effectiveAt: Clock.AddDays(-4));
        var oldFirst = new OfficialPlacementSnapshot(Guid.NewGuid(), oldFinalization.Id, ended.Id, firstTeam.Id,
            "Old A", 1, true, Clock.AddDays(-6), 1, 1, 0m);
        var oldSecond = new OfficialPlacementSnapshot(Guid.NewGuid(), oldFinalization.Id, ended.Id, secondTeam.Id,
            "Old B", 1, true, Clock.AddDays(-6), 1, 1, 0m);
        var retainedFirst = new OfficialPlacementSnapshot(Guid.NewGuid(), activeFinalization.Id, ended.Id, firstTeam.Id,
            "Retained A", 1, true, Clock.AddDays(-4), 1, 1, 0m);
        var retainedSecond = new OfficialPlacementSnapshot(Guid.NewGuid(), activeFinalization.Id, ended.Id, secondTeam.Id,
            "Retained B", 1, true, Clock.AddDays(-4), 1, 1, 0m);

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, ended, firstTeam, secondTeam, firstParticipant, secondParticipant,
                firstMembership, secondMembership, board, tile, requirement, oldReview, activeReview,
                oldFinalization, activeFinalization, oldFirst, oldSecond, retainedFirst, retainedSecond);
            await db.SaveChangesAsync();
            await BoardApprovalFixture.PublishAsync(db, board, Clock.AddDays(-3), [tile], [requirement]);
        }

        await using (var reopened = new ApplicationDbContext(options))
        {
            await reopened.Database.ExecuteSqlRawAsync(
                "UPDATE events SET state = 'AwaitingFinalReview' WHERE id = {0}", ended.Id);
        }

        await using (var reopenedRead = new ApplicationDbContext(options))
        {
            var reopenedResult = await new AdminDashboardService(reopenedRead, new FixedTimeProvider(Clock)).GetAsync(adminId);
            var reopenedHistory = Assert.Single(reopenedResult.History);
            Assert.Empty(reopenedHistory.Winners);
            Assert.Null(reopenedHistory.WinnerBoard);
        }

        await using (var refinalized = new ApplicationDbContext(options))
        {
            await refinalized.Database.ExecuteSqlRawAsync(
                "UPDATE events SET state = 'Finalized' WHERE id = {0}", ended.Id);
        }

        await using var read = new ApplicationDbContext(options);
        var result = await new AdminDashboardService(read, new FixedTimeProvider(Clock)).GetAsync(adminId);
        var history = Assert.Single(result.History);

        Assert.Equal(2, history.Winners.Count);
        Assert.Contains(history.Winners, value => value.TeamName == "Retained A" && value.Placement == 1);
        Assert.Contains(history.Winners, value => value.TeamName == "Retained B" && value.Placement == 1);
        Assert.DoesNotContain(history.Winners, value => value.TeamName.StartsWith("Old", StringComparison.Ordinal));
        Assert.Equal(1, history.WinnerBoard?.TotalTiles);
        Assert.Equal(1, history.WinnerBoard?.CompletedTiles);
        Assert.Equal(1m, history.WinnerBoard?.CompletionRatio);
    }

    [Fact]
    public async Task ReadUsesOneSnapshotBulkQueryShapePropagatesDatabaseFailureAndDoesNotWrite()
    {
        var adminId = Guid.NewGuid();
        var admin = Admin(adminId, "dashboard-r5-read-admin");
        var firstEvent = Archived(Guid.NewGuid(), "R5 read one", "dashboard-r5-read-one", adminId, Clock.AddDays(-10), Clock.AddDays(-8));
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, firstEvent);
            await db.SaveChangesAsync();
        }

        var counter = new CountingReaderInterceptor();
        var firstClock = new CountingTimeProvider(Clock);
        await using (var firstRead = new ApplicationDbContext(CreateOptions(counter)))
        {
            var firstResult = await new AdminDashboardService(firstRead, firstClock).GetAsync(adminId);
            Assert.NotNull(firstResult);
            Assert.Equal(1, firstClock.Calls);
            Assert.DoesNotContain(firstRead.ChangeTracker.Entries(), entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
        }
        var firstQueryCount = counter.ReaderCount;
        Assert.True(firstQueryCount > 0);
        Assert.All(counter.Commands, command => Assert.Contains("SELECT", command, StringComparison.OrdinalIgnoreCase));

        var secondEvent = Archived(Guid.NewGuid(), "R5 read two", "dashboard-r5-read-two", adminId, Clock.AddDays(-6), Clock.AddDays(-4));
        await using (var db = new ApplicationDbContext(options))
        {
            db.Events.Add(secondEvent);
            await db.SaveChangesAsync();
        }

        counter.Reset();
        var secondClock = new CountingTimeProvider(Clock);
        await using (var secondRead = new ApplicationDbContext(CreateOptions(counter)))
        {
            var secondResult = await new AdminDashboardService(secondRead, secondClock).GetAsync(adminId);
            Assert.Equal(2, secondResult.History.Count);
            Assert.Equal(1, secondClock.Calls);
            Assert.DoesNotContain(secondRead.ChangeTracker.Entries(), entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
        }
        Assert.Equal(firstQueryCount, counter.ReaderCount);

        var failure = new ThrowingReaderInterceptor();
        await using var failedRead = new ApplicationDbContext(CreateOptions(failure));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AdminDashboardService(failedRead, new FixedTimeProvider(Clock)).GetAsync(adminId));
        Assert.Equal("dashboard-remediation-database-failure", exception.Message);
        Assert.DoesNotContain(failedRead.ChangeTracker.Entries(), entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
    }

    [Fact]
    public async Task RepeatableReadSnapshotExcludesAConcurrentCommittedChange()
    {
        var adminId = Guid.NewGuid();
        var admin = Admin(adminId, "dashboard-r5-snapshot-admin");
        var ended = Archived(Guid.NewGuid(), "R5 snapshot", "dashboard-r5-snapshot", adminId, Clock.AddDays(-10), Clock.AddDays(-8));
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, ended);
            await db.SaveChangesAsync();
        }

        var mutation = new SnapshotMutationInterceptor(async () =>
        {
            await using var writer = new ApplicationDbContext(options);
            var account = await writer.Accounts.SingleAsync(value => value.Id == adminId);
            account.RecordLogin(Clock.AddMinutes(-1));
            await writer.SaveChangesAsync();
        });
        await using (var read = new ApplicationDbContext(CreateOptions(mutation)))
        {
            var result = await new AdminDashboardService(read, new FixedTimeProvider(Clock)).GetAsync(adminId);
            Assert.Equal(0, result.Community.LoggedInWebsiteAccounts.Value);
            Assert.DoesNotContain(read.ChangeTracker.Entries(), entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(Clock.AddMinutes(-1), (await verify.Accounts.AsNoTracking().SingleAsync(value => value.Id == adminId)).LastLoginAt);
    }

    private DbContextOptions<ApplicationDbContext> CreateOptions(params DbCommandInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString());
        if (interceptors.Length > 0) builder.AddInterceptors(interceptors);
        return builder.Options;
    }

    private static Account Admin(Guid id, string login)
    {
        var account = Account.CreateWebsite(id, login, login.ToUpperInvariant(), Clock.AddDays(-40));
        account.SetGlobalRole(GlobalRole.Admin);
        return account;
    }

    private static EventParticipant Participant(Guid eventId, DateTimeOffset signedUpAt, long sequence = 1) =>
        new(Guid.NewGuid(), eventId, SignupStatus.Confirmed, sequence, signedUpAt, SignupSource.AdminCreated);

    private static TeamMembership Membership(Guid teamId, Guid participantId, DateTimeOffset joinedAt) =>
        new(Guid.NewGuid(), teamId, participantId, TeamMembershipRole.Participant, joinedAt, null, "Dashboard remediation fixture");

    private static BingoEvent Archived(Guid id, string name, string slug, Guid adminId, DateTimeOffset start, DateTimeOffset end) =>
        BingoEvent.CreateArchivedHistorical(id, name, slug, null, "UTC", start.AddDays(-1), start.AddHours(-12), start, end,
            adminId, start.AddDays(-2), null, 1, 1, 1, 1);

    private static BingoEvent Draft(Guid id, string name, string slug, Guid adminId, DateTimeOffset? startsAt, DateTimeOffset createdAt) =>
        new(id, name, slug, null, "UTC", null, null, startsAt, startsAt?.AddHours(4), startsAt?.AddHours(4), null, adminId, createdAt);

    private static string Fingerprint(EventParticipantCharacter assignment)
    {
        var value = $"{assignment.Id:N}:{assignment.EventParticipantId:N}:{assignment.OsrsCharacterId:N}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class CountingTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public int Calls { get; private set; }
        public override DateTimeOffset GetUtcNow()
        {
            Calls++;
            return value;
        }
    }

    private sealed class CountingReaderInterceptor : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<string> commands = new();
        private int readerCount;
        public int ReaderCount => Volatile.Read(ref readerCount);
        public IReadOnlyCollection<string> Commands => commands.ToArray();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref readerCount);
            commands.Enqueue(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public void Reset()
        {
            while (commands.TryDequeue(out _)) { }
            Interlocked.Exchange(ref readerCount, 0);
        }
    }

    private sealed class ThrowingReaderInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("dashboard-remediation-database-failure");
    }

    private sealed class SnapshotMutationInterceptor(Func<Task> mutation) : DbCommandInterceptor
    {
        private int invoked;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref invoked, 1) == 0) await mutation();
            return result;
        }
    }
}
