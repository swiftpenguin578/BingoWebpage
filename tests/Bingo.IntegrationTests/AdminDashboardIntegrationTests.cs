using System.Data;
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
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class AdminDashboardIntegrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Clock = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task DashboardCountsMembershipIntervalsAndDisabledWebsiteIdentityInOneSnapshot()
    {
        var adminId = Guid.NewGuid();
        var disabledId = Guid.NewGuid();
        var emergencyId = Guid.NewGuid();
        var first = BingoEvent.CreateArchivedHistorical(Guid.NewGuid(), "First event", "dashboard-first", null, "UTC",
            Clock.AddDays(-20), Clock.AddDays(-18), Clock.AddDays(-17), Clock.AddDays(-15), adminId,
            Clock.AddDays(-21), null, 1, 2, 1, 1);
        var live = new BingoEvent(Guid.NewGuid(), "Live event", "dashboard-live", null, "UTC",
            Clock.AddDays(-10), Clock.AddDays(-8), Clock.AddDays(-7), Clock.AddDays(5), Clock.AddDays(6), 10, adminId, Clock.AddDays(-11));
        live.OpenSignups(Clock.AddDays(-10));
        live.CloseSignups(Clock.AddDays(-8));
        live.SetDraftLocked(true, Clock.AddDays(-7));
        live.StartEvent(Clock.AddDays(-7));
        var hidden = BingoEvent.CreateArchivedHistorical(Guid.NewGuid(), "Hidden event", "dashboard-hidden", null, "UTC",
            Clock.AddDays(-30), Clock.AddDays(-28), Clock.AddDays(-27), Clock.AddDays(-25), adminId,
            Clock.AddDays(-31), null, 1, 1, 1, 1);
        hidden.Hide(adminId, Clock.AddDays(-24), "Hidden event", "Quarantine fixture");

        var admin = Account.CreateWebsite(adminId, "dashboard-admin", "DASHBOARD-ADMIN", Clock.AddDays(-40));
        admin.SetGlobalRole(GlobalRole.Admin);
        var disabled = Account.CreateWebsite(disabledId, "historical-player", "HISTORICAL-PLAYER", Clock.AddDays(-40));
        disabled.SetGlobalRole(GlobalRole.User);
        disabled.Disable(Clock.AddDays(-2), adminId, "Fixture");
        var emergency = Account.CreateEmergency(emergencyId, "emergency", "EMERGENCY", Clock.AddDays(-40));

        var firstTeam = new Team(Guid.NewGuid(), first.Id, "First team", "first-team", "fixture", false, first.ActualStartedAt);
        var liveTeam = new Team(Guid.NewGuid(), live.Id, "Live team", "live-team", "fixture", false, live.ActualStartedAt);
        liveTeam.SetActive(false); // Dashboard population must not use Team.Active.
        var firstParticipant = new EventParticipant(Guid.NewGuid(), first.Id, SignupStatus.Confirmed, 1, first.ActualStartedAt!.Value, SignupSource.Website);
        firstParticipant.AssignOwner(disabled);
        var liveParticipant = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Confirmed, 1, live.ActualStartedAt!.Value, SignupSource.Website);
        liveParticipant.AssignOwner(disabled);
        var withdrawnParticipant = new EventParticipant(Guid.NewGuid(), live.Id, SignupStatus.Withdrawn, 2, live.ActualStartedAt!.Value, SignupSource.AdminCreated);
        var firstMembership = new TeamMembership(Guid.NewGuid(), firstTeam.Id, firstParticipant.Id, TeamMembershipRole.Participant,
            first.ActualStartedAt.Value, null, "Fixture");
        var liveMembershipBeforeMove = new TeamMembership(Guid.NewGuid(), liveTeam.Id, liveParticipant.Id, TeamMembershipRole.Participant,
            live.ActualStartedAt.Value, null, "Fixture");
        liveMembershipBeforeMove.Leave(Clock.AddDays(-4), "Moved");
        var liveMembershipAfterMove = new TeamMembership(Guid.NewGuid(), liveTeam.Id, liveParticipant.Id, TeamMembershipRole.Participant,
            Clock.AddDays(-4), null, "Fixture");
        var withdrawnMembership = new TeamMembership(Guid.NewGuid(), liveTeam.Id, withdrawnParticipant.Id, TeamMembershipRole.Participant,
            live.ActualStartedAt.Value, null, "Fixture");
        withdrawnMembership.Leave(live.ActualEndedAt ?? Clock, "Ended at boundary");

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, disabled, emergency, first, live, hidden, firstTeam, liveTeam,
                firstParticipant, liveParticipant, withdrawnParticipant, firstMembership,
                liveMembershipBeforeMove, liveMembershipAfterMove, withdrawnMembership);
            await db.SaveChangesAsync();
        }

        await using var read = new ApplicationDbContext(options);
        var service = new AdminDashboardService(read, new FixedTimeProvider(Clock));
        var result = await service.GetAsync(adminId);

        Assert.Equal(2, result.Statistics.EventsHeld.Value);
        Assert.Equal(DashboardValueAvailability.Measured, result.Statistics.UniqueWebsiteParticipants.Availability);
        Assert.Equal(1, result.Statistics.UniqueWebsiteParticipants.Value);
        Assert.Equal(3, result.Statistics.EventParticipations.Value);
        Assert.Equal(2, result.ParticipationChart.Count);
        var livePoint = Assert.Single(result.ParticipationChart, value => value.EventId == live.Id);
        Assert.Equal(1, livePoint.ReturningWebsiteParticipants.Value);
        Assert.Equal(0, livePoint.NewWebsiteParticipants.Value);
        Assert.Equal(EventState.Live, result.CurrentEvent?.State);
        Assert.Equal(live.Id, result.CurrentEvent?.EventId);
        Assert.Equal(2, result.Community.WebsiteAccounts.Value); // emergency credentials are excluded.
        Assert.Null(result.History.SingleOrDefault(value => value.EventId == hidden.Id));
        Assert.Equal("/Admin/Events/Manage/" + live.Id.ToString("D"), result.History.Single(value => value.EventId == live.Id).OverviewPath);
    }

    [Fact]
    public async Task DashboardRejectsDisabledOrNonAdminActorWithoutReturningZeros()
    {
        var disabledId = Guid.NewGuid();
        var account = Account.CreateWebsite(disabledId, "disabled-admin", "DISABLED-ADMIN", Clock.AddDays(-1));
        account.SetGlobalRole(GlobalRole.Admin);
        account.Disable(Clock, disabledId, "Fixture");
        await using (var db = new ApplicationDbContext(options))
        {
            db.Accounts.Add(account);
            await db.SaveChangesAsync();
        }

        await using var read = new ApplicationDbContext(options);
        var service = new AdminDashboardService(read, new FixedTimeProvider(Clock));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(disabledId));
        Assert.DoesNotContain(read.ChangeTracker.Entries(), entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
    }

    [Fact]
    public async Task DashboardUsesPublishedApprovalWinnerSnapshotAndCompatibleBulkEhb()
    {
        var adminId = Guid.NewGuid();
        var ended = BingoEvent.CreateArchivedHistorical(Guid.NewGuid(), "Finalized event", "dashboard-finalized", null, "UTC",
            Clock.AddDays(-10), Clock.AddDays(-9), Clock.AddDays(-8), Clock.AddDays(-7), adminId,
            Clock.AddDays(-11), null, 1, 1, 1, 1);
        var admin = Account.CreateWebsite(adminId, "dashboard-b2-admin", "DASHBOARD-B2-ADMIN", Clock.AddDays(-40));
        admin.SetGlobalRole(GlobalRole.Admin);
        var team = new Team(Guid.NewGuid(), ended.Id, "Retained winner", "retained-winner", "fixture", false, ended.ActualStartedAt);
        var participant = new EventParticipant(Guid.NewGuid(), ended.Id, SignupStatus.Confirmed, 1,
            ended.ActualStartedAt!.Value, SignupSource.AdminCreated);
        var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant,
            ended.ActualStartedAt.Value, null, "Fixture");
        var osrsCharacterId = Guid.NewGuid();
        var osrsCharacter = new OsrsCharacter(osrsCharacterId, "Fixture character", "FIXTURE CHARACTER", Clock.AddDays(-11));
        var character = new EventParticipantCharacter(Guid.NewGuid(), ended.Id, participant.Id, osrsCharacterId, 0,
            ended.ActualStartedAt.Value, adminId, null, EventCharacterRole.Playing, 10m, EhbSource.WiseOldMan,
            Clock.AddDays(-7));
        var board = new Board(Guid.NewGuid(), ended.Id, "Approved board", 1, 1);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Manual tile", "", "", 0m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 1, true, false, "Manual objective", true);

        var validSubmission = new Submission(Guid.NewGuid(), ended.Id, team.Id, tile.Id, requirement.Id, null,
            participant.Id, character.OsrsCharacterId, "Fixture character", adminId, 1, Clock.AddDays(-6), null, null);
        validSubmission.Approve(1, Clock.AddDays(-6));
        var reversedSubmission = new Submission(Guid.NewGuid(), ended.Id, team.Id, tile.Id, requirement.Id, null,
            participant.Id, character.OsrsCharacterId, "Fixture character", adminId, 1, Clock.AddDays(-5), null, null);
        reversedSubmission.Approve(1, Clock.AddDays(-5));
        reversedSubmission.Reverse("Fixture correction", Clock.AddDays(-4));
        var validContribution = new SubmissionContribution(Guid.NewGuid(), validSubmission.Id, team.Id, requirement.Id, null,
            participant.Id, 1, Clock.AddDays(-6));

        var oldReviewCycleId = Guid.NewGuid();
        var activeReviewCycleId = Guid.NewGuid();
        var oldFinalization = new EventFinalizationSnapshot(Guid.NewGuid(), ended.Id, 1, Clock.AddDays(-6), adminId,
            oldReviewCycleId);
        oldFinalization.Unfinalize(Clock.AddDays(-5), adminId, "Fixture correction");
        var activeFinalization = new EventFinalizationSnapshot(Guid.NewGuid(), ended.Id, 2, Clock.AddDays(-4), adminId,
            activeReviewCycleId);
        var oldReviewCycle = new EventStateTransition(oldReviewCycleId, ended.Id, EventState.Live,
            EventState.AwaitingFinalReview, adminId, Clock.AddDays(-7), "Fixture ended", effectiveAt: Clock.AddDays(-7));
        var activeReviewCycle = new EventStateTransition(activeReviewCycleId, ended.Id, EventState.AwaitingFinalReview,
            EventState.Finalized, adminId, Clock.AddDays(-4), "Fixture finalized", effectiveAt: Clock.AddDays(-4));
        var oldPlacement = new OfficialPlacementSnapshot(Guid.NewGuid(), oldFinalization.Id, ended.Id, team.Id,
            "Old team name", 1, true, Clock.AddDays(-6), 1, 1, 0m);
        var retainedPlacement = new OfficialPlacementSnapshot(Guid.NewGuid(), activeFinalization.Id, ended.Id, team.Id,
            "Retained winner", 1, true, Clock.AddDays(-4), 1, 1, 0m);
        var synchronization = new EventCompetitionSynchronization(Guid.NewGuid(), ended.Id, 1, 99,
            "Fixture competition", ended.EventStartsAt, ended.EventEndsAt, Fingerprint(character), Clock);
        synchronization.MarkSuccess(Clock.AddDays(-1), Clock.AddDays(-1), true, "[]", null);
        var activity = new EventCompetitionCharacterActivity(Guid.NewGuid(), ended.Id, synchronization.Generation, 99,
            character.OsrsCharacterId, 4m, Clock.AddDays(-1), Clock.AddDays(-1), synchronization.AssignmentFingerprint,
            6m, 10m);
        var importAudit = new AuditEntry(Guid.NewGuid(), Clock.AddDays(-12), adminId, admin.LoginName,
            "unrelated.import", "Event", ended.Id.ToString("D"), "not a historical import", ended.Id);

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, ended, team, participant, membership, osrsCharacter, character, board, tile, requirement,
                validSubmission, reversedSubmission, validContribution,
                oldReviewCycle, activeReviewCycle, oldFinalization, activeFinalization, oldPlacement,
                retainedPlacement, synchronization, activity, importAudit);
            await db.SaveChangesAsync();
            await BoardApprovalFixture.PublishAsync(db, board, Clock.AddDays(-3), [tile], [requirement]);
        }

        await using var read = new ApplicationDbContext(options);
        var service = new AdminDashboardService(read, new FixedTimeProvider(Clock));
        var result = await service.GetAsync(adminId);
        var history = Assert.Single(result.History);

        Assert.Equal(1, history.ApprovedSubmissions.Value);
        Assert.Equal(DashboardValueAvailability.Measured, history.ApprovedSubmissions.Availability);
        var winner = Assert.Single(history.Winners);
        Assert.Equal("Retained winner", winner.TeamName);
        Assert.Null(history.Winners.SingleOrDefault(value => value.TeamName == "Old team name"));
        Assert.Equal(1, history.WinnerBoard?.CompletedTiles);
        Assert.Equal(1, history.WinnerBoard?.TotalTiles);
        Assert.Equal(1m, history.WinnerBoard?.CompletionRatio);
        Assert.Equal(DashboardEhbCoverage.Complete, history.Ehb.Coverage);
        Assert.Equal(4m, history.Ehb.Gain);
    }

    [Fact]
    public async Task DashboardDistinguishesImportedUnlinkedNormalZeroAndEqualStartCohorts()
    {
        var adminId = Guid.NewGuid();
        var admin = Account.CreateWebsite(adminId, "dashboard-b2-cohort-admin", "DASHBOARD-B2-COHORT-ADMIN", Clock.AddDays(-40));
        admin.SetGlobalRole(GlobalRole.Admin);
        var disabledId = Guid.NewGuid();
        var disabled = Account.CreateWebsite(disabledId, "disabled-cohort-player", "DISABLED-COHORT-PLAYER", Clock.AddDays(-40));
        disabled.Disable(Clock.AddDays(-2), adminId, "Fixture");
        disabled.RecordLogin(Clock.AddDays(-1));
        var afterEnd = Account.CreateWebsite(Guid.NewGuid(), "created-after-end", "CREATED-AFTER-END", Clock.AddDays(-1));
        afterEnd.RecordLogin(Clock.AddDays(-1));
        var atEnd = Account.CreateWebsite(Guid.NewGuid(), "created-at-end", "CREATED-AT-END", Clock.AddDays(-8).AddTicks(1));
        atEnd.RecordLogin(Clock.AddDays(-8).AddTicks(1));
        var loginWindowOnly = Account.CreateWebsite(Guid.NewGuid(), "login-window-only", "LOGIN-WINDOW-ONLY", Clock.AddDays(-40));
        loginWindowOnly.RecordLogin(Clock.AddDays(-20));
        var future = Account.CreateWebsite(Guid.NewGuid(), "future-account", "FUTURE-ACCOUNT", Clock.AddHours(1));
        future.RecordLogin(Clock.AddHours(1));
        var emergency = Account.CreateEmergency(Guid.NewGuid(), "cohort-emergency", "COHORT-EMERGENCY", Clock.AddDays(-1));

        var imported = BingoEvent.CreateArchivedHistorical(Guid.NewGuid(), "Imported history", "dashboard-imported", null, "UTC",
            Clock.AddDays(-21), Clock.AddDays(-20), Clock.AddDays(-20), Clock.AddDays(-18), adminId,
            Clock.AddDays(-22), null, 1, 1, 1, 1);
        var normal = BingoEvent.CreateArchivedHistorical(Guid.NewGuid(), "Normal zero", "dashboard-normal-zero", null, "UTC",
            Clock.AddDays(-17), Clock.AddDays(-16), Clock.AddDays(-16), Clock.AddDays(-14), adminId,
            Clock.AddDays(-18), null, 1, 1, 1, 1);
        var cohortStart = Clock.AddDays(-10);
        var cohortA = BingoEvent.CreateArchivedHistorical(Guid.NewGuid(), "Cohort A", "dashboard-cohort-a", null, "UTC",
            Clock.AddDays(-11), Clock.AddDays(-10), cohortStart, Clock.AddDays(-8), adminId,
            Clock.AddDays(-12), null, 1, 1, 1, 1);
        var cohortB = BingoEvent.CreateArchivedHistorical(Guid.NewGuid(), "Cohort B", "dashboard-cohort-b", null, "UTC",
            Clock.AddDays(-11), Clock.AddDays(-10), cohortStart, Clock.AddDays(-8), adminId,
            Clock.AddDays(-12), null, 1, 1, 1, 1);
        var cancelled = new BingoEvent(Guid.NewGuid(), "Cancelled", "dashboard-cancelled", "UTC", adminId, Clock.AddDays(-2));
        cancelled.Cancel(adminId, Clock.AddDays(-1), "Fixture", protectedHistoryExists: true);
        var discarded = new BingoEvent(Guid.NewGuid(), "Discarded", "dashboard-discarded", "UTC", adminId, Clock.AddDays(-2));
        discarded.Discard(adminId, Clock.AddDays(-1), protectedHistoryExists: false);

        var importedTeam = new Team(Guid.NewGuid(), imported.Id, "Imported team", "imported-team", "fixture", false, imported.ActualStartedAt);
        var normalTeam = new Team(Guid.NewGuid(), normal.Id, "Normal team", "normal-team", "fixture", false, normal.ActualStartedAt);
        var cohortTeamA = new Team(Guid.NewGuid(), cohortA.Id, "Cohort A team", "cohort-a-team", "fixture", false, cohortA.ActualStartedAt);
        var cohortTeamB = new Team(Guid.NewGuid(), cohortB.Id, "Cohort B team", "cohort-b-team", "fixture", false, cohortB.ActualStartedAt);
        var importedParticipant = new EventParticipant(Guid.NewGuid(), imported.Id, SignupStatus.Confirmed, 1, imported.ActualStartedAt!.Value, SignupSource.AdminCreated);
        var normalParticipant = new EventParticipant(Guid.NewGuid(), normal.Id, SignupStatus.Confirmed, 1, normal.ActualStartedAt!.Value, SignupSource.AdminCreated);
        var cohortParticipantA = new EventParticipant(Guid.NewGuid(), cohortA.Id, SignupStatus.Confirmed, 1, cohortA.ActualStartedAt!.Value, SignupSource.Website);
        cohortParticipantA.AssignOwner(disabled);
        var cohortParticipantB = new EventParticipant(Guid.NewGuid(), cohortB.Id, SignupStatus.Confirmed, 1, cohortB.ActualStartedAt!.Value, SignupSource.Website);
        cohortParticipantB.AssignOwner(disabled);
        var memberships = new[]
        {
            new TeamMembership(Guid.NewGuid(), importedTeam.Id, importedParticipant.Id, TeamMembershipRole.Participant, imported.ActualStartedAt.Value, null, "Fixture"),
            new TeamMembership(Guid.NewGuid(), normalTeam.Id, normalParticipant.Id, TeamMembershipRole.Participant, normal.ActualStartedAt.Value, null, "Fixture"),
            new TeamMembership(Guid.NewGuid(), cohortTeamA.Id, cohortParticipantA.Id, TeamMembershipRole.Participant, cohortA.ActualStartedAt.Value, null, "Fixture"),
            new TeamMembership(Guid.NewGuid(), cohortTeamB.Id, cohortParticipantB.Id, TeamMembershipRole.Participant, cohortB.ActualStartedAt.Value, null, "Fixture")
        };
        var importAudit = new AuditEntry(Guid.NewGuid(), Clock.AddDays(-19), adminId, admin.LoginName,
            "historical_import.applied", "Event", imported.Id.ToString("D"), "fixture import", imported.Id);

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, disabled, afterEnd, atEnd, loginWindowOnly, future, emergency,
                imported, normal, cohortA, cohortB, cancelled, discarded,
                importedTeam, normalTeam, cohortTeamA, cohortTeamB,
                importedParticipant, normalParticipant, cohortParticipantA, cohortParticipantB);
            db.AddRange(memberships);
            db.AuditEntries.Add(importAudit);
            await db.SaveChangesAsync();
            var persistedAtEnd = await db.Accounts.AsNoTracking().Where(value => value.Id == atEnd.Id)
                .Select(value => value.CreatedAt).SingleAsync();
            Assert.Equal(Clock.AddDays(-8), persistedAtEnd);
        }

        await using var read = new ApplicationDbContext(options);
        var service = new AdminDashboardService(read, new FixedTimeProvider(Clock));
        var result = await service.GetAsync(adminId);

        Assert.Equal(4, result.Statistics.EventsHeld.Value);
        Assert.Equal(4, result.ParticipationChart.Count);
        Assert.Equal(4, result.Statistics.EventParticipations.Value);
        Assert.Equal(1, result.Statistics.UniqueWebsiteParticipants.Value);
        Assert.Equal(DashboardValueAvailability.Unavailable,
            result.History.Single(value => value.EventId == imported.Id).UniqueWebsiteParticipants.Availability);
        Assert.Equal(0, result.History.Single(value => value.EventId == normal.Id).UniqueWebsiteParticipants.Value);
        Assert.Equal(DashboardValueAvailability.Measured,
            result.History.Single(value => value.EventId == normal.Id).UniqueWebsiteParticipants.Availability);
        Assert.All(result.ParticipationChart.Where(value => value.EventId is var id && (id == cohortA.Id || id == cohortB.Id)), point =>
        {
            Assert.Equal(1, point.NewWebsiteParticipants.Value);
            Assert.Equal(0, point.ReturningWebsiteParticipants.Value);
        });
        Assert.Equal(6, result.Community.WebsiteAccounts.Value);
        Assert.Equal(1, result.Community.NewWebsiteAccounts.Value);
        Assert.Equal(4, result.Community.LoggedInWebsiteAccounts.Value);
        Assert.False(result.Community.UsedThirtyDayFallback);
        Assert.Null(result.History.SingleOrDefault(value => value.EventId == cancelled.Id));
        Assert.Null(result.History.SingleOrDefault(value => value.EventId == discarded.Id));
    }

    [Fact]
    public async Task DashboardSelectsOverduePreparationAndHonorsReadClockBoundaries()
    {
        var adminId = Guid.NewGuid();
        var admin = Account.CreateWebsite(adminId, "dashboard-b2-card-admin", "DASHBOARD-B2-CARD-ADMIN", Clock.AddDays(-2));
        admin.SetGlobalRole(GlobalRole.Admin);
        var overdue = new BingoEvent(Guid.NewGuid(), "Overdue setup", "dashboard-overdue", null, "UTC",
            Clock.AddDays(-3), Clock.AddDays(-2), Clock.AddHours(-1), Clock.AddHours(1), Clock.AddHours(1), null, adminId, Clock.AddDays(-3));
        var confirmed = new EventParticipant(Guid.NewGuid(), overdue.Id, SignupStatus.Confirmed, 1, Clock.AddDays(-1), SignupSource.AdminCreated);
        var waiting = new EventParticipant(Guid.NewGuid(), overdue.Id, SignupStatus.WaitingList, 2, Clock.AddDays(-1), SignupSource.AdminCreated);

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, overdue, confirmed, waiting);
            await db.SaveChangesAsync();
        }

        await using var read = new ApplicationDbContext(options);
        var service = new AdminDashboardService(read, new FixedTimeProvider(Clock));
        var result = await service.GetAsync(adminId);
        Assert.NotNull(result.CurrentEvent);
        var card = result.CurrentEvent!;
        Assert.Equal(overdue.Id, card.EventId);
        Assert.True(card.IsOverdue);
        Assert.Null(card.Capacity);
        Assert.Equal(1, card.ConfirmedParticipants);
        Assert.Equal(1, card.WaitingParticipants);
    }

    [Fact]
    public async Task DashboardClassifiesCompatiblePartialMissingZeroAndIncompatibleEhbCoverage()
    {
        var adminId = Guid.NewGuid();
        var admin = Account.CreateWebsite(adminId, "dashboard-b2-ehb-admin", "DASHBOARD-B2-EHB-ADMIN", Clock.AddDays(-40));
        admin.SetGlobalRole(GlobalRole.Admin);
        var events = new List<BingoEvent>();
        var teams = new List<Team>();
        var participants = new List<EventParticipant>();
        var memberships = new List<TeamMembership>();
        var osrsCharacters = new List<OsrsCharacter>();
        var assignments = new List<EventParticipantCharacter>();
        var synchronizations = new List<EventCompetitionSynchronization>();
        var activities = new List<EventCompetitionCharacterActivity>();
        var importAudits = new List<AuditEntry>();
        var eventIds = new Guid[4];

        for (var index = 0; index < 4; index++)
        {
            var start = Clock.AddDays(-30 + index * 5);
            var ended = BingoEvent.CreateArchivedHistorical(Guid.NewGuid(), $"EHB {index}", $"dashboard-ehb-{index}", null, "UTC",
                start.AddDays(-1), start.AddHours(-12), start, start.AddDays(2), adminId,
                start.AddDays(-2), null, 1, index == 1 ? 2 : 1, 1, 1);
            var team = new Team(Guid.NewGuid(), ended.Id, $"EHB team {index}", $"ehb-team-{index}", "fixture", false, start);
            var participant = new EventParticipant(Guid.NewGuid(), ended.Id, SignupStatus.Confirmed, 1, start, SignupSource.AdminCreated);
            var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, start, null, "Fixture");
            events.Add(ended);
            teams.Add(team);
            participants.Add(participant);
            memberships.Add(membership);
            eventIds[index] = ended.Id;

            var assignmentCount = index == 1 ? 2 : 1;
            for (var characterIndex = 0; characterIndex < assignmentCount; characterIndex++)
            {
                var osrsId = Guid.NewGuid();
                osrsCharacters.Add(new OsrsCharacter(osrsId, $"EHB character {index}-{characterIndex}",
                    $"EHB CHARACTER {index}-{characterIndex}", start.AddDays(-1)));
                assignments.Add(new EventParticipantCharacter(Guid.NewGuid(), ended.Id, participant.Id, osrsId,
                    characterIndex, start, adminId, null, EventCharacterRole.Playing, 0m, EhbSource.Manual, null));
            }

            var eventAssignments = assignments.Where(value => value.EventId == ended.Id).ToArray();
            var expectedFingerprint = Fingerprint(eventAssignments);
            var syncFingerprint = index == 3 ? "incompatible-fingerprint" : expectedFingerprint;
            var synchronization = new EventCompetitionSynchronization(Guid.NewGuid(), ended.Id, 1, 500 + index,
                $"EHB competition {index}", ended.EventStartsAt, ended.EventEndsAt, syncFingerprint, Clock);
            synchronization.MarkSuccess(Clock.AddDays(-1), Clock.AddDays(-1), index != 1, index == 1 ? "[\"missing\"]" : "[]", null);
            synchronizations.Add(synchronization);

            if (index is 0 or 1 or 3)
            {
                var matchedCount = index == 1 ? 1 : 1;
                foreach (var assignment in eventAssignments.Take(matchedCount))
                {
                    activities.Add(new EventCompetitionCharacterActivity(Guid.NewGuid(), ended.Id, synchronization.Generation,
                        synchronization.CompetitionId!.Value, assignment.OsrsCharacterId, index == 0 ? 0m : 2m,
                        Clock.AddDays(-1), Clock.AddDays(-1), syncFingerprint, 0m, index == 0 ? 0m : 2m));
                }
            }

            if (index == 0)
                importAudits.Add(new AuditEntry(Guid.NewGuid(), start.AddHours(-1), adminId, admin.LoginName,
                    "historical_import.applied", "Event", ended.Id.ToString("D"), "fixture import", ended.Id));
        }

        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin);
            db.AddRange(events);
            db.AddRange(teams);
            db.AddRange(participants);
            db.AddRange(memberships);
            db.AddRange(osrsCharacters);
            db.AddRange(assignments);
            db.AddRange(synchronizations);
            db.AddRange(activities);
            db.AddRange(importAudits);
            await db.SaveChangesAsync();
        }

        await using var read = new ApplicationDbContext(options);
        var result = await new AdminDashboardService(read, new FixedTimeProvider(Clock)).GetAsync(adminId);

        var zero = result.History.Single(value => value.EventId == eventIds[0]).Ehb;
        Assert.Equal(DashboardEhbCoverage.MeasuredZero, zero.Coverage);
        Assert.Equal(0m, zero.Gain);
        Assert.Equal(1, zero.ExpectedAccounts);
        Assert.Equal(1, zero.MatchedAccounts);
        var partial = result.History.Single(value => value.EventId == eventIds[1]).Ehb;
        Assert.Equal(DashboardEhbCoverage.Partial, partial.Coverage);
        Assert.Equal(2m, partial.Gain);
        Assert.Equal(2, partial.ExpectedAccounts);
        Assert.Equal(1, partial.MatchedAccounts);
        var missing = result.History.Single(value => value.EventId == eventIds[2]).Ehb;
        Assert.Equal(DashboardEhbCoverage.Unavailable, missing.Coverage);
        Assert.Null(missing.Gain);
        Assert.Equal(1, missing.ExpectedAccounts);
        Assert.Equal(0, missing.MatchedAccounts);
        var incompatible = result.History.Single(value => value.EventId == eventIds[3]).Ehb;
        Assert.Equal(DashboardEhbCoverage.Unavailable, incompatible.Coverage);
        Assert.Null(incompatible.Gain);
        Assert.Equal(1, incompatible.ExpectedAccounts);
        Assert.Equal(0, incompatible.MatchedAccounts);
    }

    [Fact]
    public async Task DashboardRejectsWeakCallerTransactionAndCancellationWithoutWrites()
    {
        var adminId = Guid.NewGuid();
        var admin = Account.CreateWebsite(adminId, "dashboard-b2-transaction-admin", "DASHBOARD-B2-TRANSACTION-ADMIN", Clock.AddDays(-2));
        admin.SetGlobalRole(GlobalRole.Admin);
        await using (var db = new ApplicationDbContext(options))
        {
            db.Accounts.Add(admin);
            await db.SaveChangesAsync();
        }

        await using var read = new ApplicationDbContext(options);
        var service = new AdminDashboardService(read, new FixedTimeProvider(Clock));
        await using (var weakTransaction = await read.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAsync(adminId));
            Assert.DoesNotContain(read.ChangeTracker.Entries(), entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
        }

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(adminId, cancellation.Token));
        Assert.DoesNotContain(read.ChangeTracker.Entries(), entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
    }

    private static string Fingerprint(EventParticipantCharacter assignment) => Fingerprint([assignment]);

    private static string Fingerprint(IEnumerable<EventParticipantCharacter> assignments)
    {
        var value = string.Join('|', assignments.OrderBy(row => row.Id)
            .Select(row => $"{row.Id:N}:{row.EventParticipantId:N}:{row.OsrsCharacterId:N}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
