using System.Reflection;
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
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class U2DashboardIntegrationTests(PostgreSqlTestFixture fixture) : IAsyncLifetime, IClassFixture<PostgreSqlTestFixture>
{
    private static readonly DateTimeOffset Clock = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlTestDatabase database = fixture.CreateDatabase(new PostgreSqlBuilder("postgres:17-alpine"));
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
    }

    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task RemovedAndNeverPlacedConfirmedAssignmentsUseTheActualWomFingerprintAndRemainStrict()
    {
        var admin = Admin();
        var ended = Archived(Guid.NewGuid(), "Removed roster", admin.Id, Clock.AddDays(-4), Clock.AddDays(-2));
        var team = new Team(Guid.NewGuid(), ended.Id, "Retained team", "retained", "fixture", false, ended.ActualStartedAt);
        var removed = Participant(ended.Id, 1);
        var unplaced = Participant(ended.Id, 2);
        var member = new TeamMembership(Guid.NewGuid(), team.Id, removed.Id, TeamMembershipRole.Participant,
            Clock.AddDays(-6), null, "Fixture");
        member.Leave(Clock.AddDays(-5), "Removed before Live; participant remains Confirmed");
        var characters = new[]
        {
            new OsrsCharacter(Guid.NewGuid(), "Removed", "REMOVED", Clock.AddDays(-7)),
            new OsrsCharacter(Guid.NewGuid(), "Unplaced", "UNPLACED", Clock.AddDays(-7))
        };
        var assignments = new[]
        {
            Assignment(ended.Id, removed.Id, characters[0].Id, admin.Id),
            Assignment(ended.Id, unplaced.Id, characters[1].Id, admin.Id)
        };
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, ended, team, removed, unplaced, member);
            db.AddRange(characters);
            db.AddRange(assignments);
            await db.SaveChangesAsync();
            // Invoke the real producer, rather than duplicate its selection rule.
            var method = typeof(EventCompetitionSynchronizationService).GetMethod("StatsAssignmentFingerprintAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            var fingerprint = await (Task<string>)method.Invoke(null, [db, ended.Id, CancellationToken.None])!;
            var sync = new EventCompetitionSynchronization(Guid.NewGuid(), ended.Id, 1, 101, "Fixture",
                ended.EventStartsAt, ended.EventEndsAt, fingerprint, Clock);
            sync.MarkSuccess(Clock, Clock, true, "[]", null);
            db.Add(sync);
            foreach (var assignment in assignments)
                db.Add(new EventCompetitionCharacterActivity(Guid.NewGuid(), ended.Id, 1, 101, assignment.OsrsCharacterId,
                    3m, Clock, Clock, fingerprint, 0m, 3m));
            await db.SaveChangesAsync();
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var result = await Read(db, admin.Id);
            var history = Assert.Single(result.History);
            Assert.Equal(0, history.Participants.Value);
            Assert.Equal(DashboardEhbCoverage.Complete, history.Ehb.Coverage);
            Assert.Equal(2, history.Ehb.ExpectedAccounts);
            Assert.Equal(6m, history.Ehb.Gain);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE event_competition_synchronizations SET assignment_fingerprint = 'stale' WHERE event_id = {ended.Id}");
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(DashboardEhbCoverage.Unavailable, Assert.Single((await Read(verify, admin.Id)).History).Ehb.Coverage);
    }

    [Fact]
    public async Task MixedHistoryHeadlineExcludesReconstructionPendingAndRejectedWhileImportOnlyIsUnknown()
    {
        var admin = Admin();
        var imported = Archived(Guid.NewGuid(), "Imported", admin.Id, Clock.AddDays(-8), Clock.AddDays(-6));
        var platform = Archived(Guid.NewGuid(), "Platform", admin.Id, Clock.AddDays(-4), Clock.AddDays(-2));
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, imported);
            db.Add(new AuditEntry(Guid.NewGuid(), Clock.AddDays(-5), admin.Id, admin.LoginName,
                "historical_import.applied", "event", imported.Id.ToString("D"), "Controlled import fixture", imported.Id));
            await SeedBoard(db, imported, admin, reconstructed: true);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var result = await Read(db, admin.Id);
            Assert.False(result.Statistics.ApprovedSubmissions.IsAvailable);
            Assert.True(Assert.Single(result.History).IsHistoricalImport);
        }
        await using (var db = new ApplicationDbContext(options))
        {
            db.Add(platform);
            await SeedBoard(db, platform, admin, reconstructed: false);
        }
        await using var read = new ApplicationDbContext(options);
        var mixed = await Read(read, admin.Id);
        Assert.True(mixed.Statistics.ApprovedSubmissions.IsAvailable);
        Assert.Equal(1, mixed.Statistics.ApprovedSubmissions.Value);
        Assert.Equal(1, mixed.Statistics.LatestNewApprovedSubmissions.Value);
        Assert.Equal(platform.Id, mixed.Statistics.LatestContributionEventId);
        Assert.False(mixed.History.Single(row => row.EventId == imported.Id).ApprovedSubmissions.IsAvailable);
    }

    [Fact]
    public async Task RecapSelectsLatestActualEndWhileLiveLatestContributionUsesChartTieOrderAndTrackingMetadata()
    {
        var admin = Admin();
        var old = Archived(Guid.NewGuid(), "Old ended", admin.Id, Clock.AddDays(-10), Clock.AddDays(-8));
        var review = Live(Guid.NewGuid(), "Latest ended", admin.Id, Clock.AddDays(-6));
        review.EndEvent(Clock.AddDays(-1));
        var lower = Live(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Tie low", admin.Id, Clock.AddHours(-2));
        var higher = Live(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Tie high", admin.Id, Clock.AddHours(-2));
        // Read-model tie fixture: no lifecycle mutation/provider call under test.
        var team = new Team(Guid.NewGuid(), review.Id, "Review team", "review-team", "fixture", false, review.ActualStartedAt);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, old, review, lower, higher, team);
            await db.SaveChangesAsync();
        }
        await using var read = new ApplicationDbContext(options);
        var result = await Read(read, admin.Id);
        var recap = Assert.IsType<DashboardRecap>(result.LatestEndedRecap);
        Assert.Equal(review.Id, recap.EventId);
        Assert.Equal(EventState.AwaitingFinalReview, recap.State);
        Assert.True(recap.Provisional);
        Assert.Equal(1, recap.TeamCount);
        Assert.Equal(1, result.History.Single(row => row.EventId == review.Id).TeamCount);
        Assert.Equal(1, result.Chart.Single(row => row.EventId == review.Id).TeamCount);
        Assert.Equal(higher.Id, result.Chart[^1].EventId);
        Assert.Equal(higher.Id, result.Statistics.LatestContributionEventId);
        Assert.Equal("Tie high", result.Statistics.LatestContributionEventName);
        Assert.True(result.Statistics.LatestContributionProvisional);
        Assert.True(result.Statistics.Provisional);
        Assert.Equal(3, result.Statistics.ProvisionalEvents);
        Assert.True(result.Chart[0].TrackingStarts);
        Assert.False(result.Chart[0].ReturningWebsiteParticipants.IsAvailable);
        Assert.All(result.Chart.Skip(1), point => Assert.False(point.TrackingStarts));
        Assert.False(result.History.Single(row => row.EventId == old.Id).Provisional);
    }

    [Fact]
    public async Task RemovedInactiveTeamDoesNotCountInRecapHistoryOrChart()
    {
        var admin = Admin();
        var item = Archived(Guid.NewGuid(), "Five teams competed", admin.Id, Clock.AddDays(-4), Clock.AddDays(-2));
        var teams = Enumerable.Range(1, 6).Select(index => new Team(Guid.NewGuid(), item.Id,
            $"Team {index}", $"team-{index}", "fixture", false, item.ActualStartedAt)).ToArray();
        teams[^1].SetActive(false);
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, item);
            db.AddRange(teams);
            await db.SaveChangesAsync();
        }
        await using var read = new ApplicationDbContext(options);
        Assert.Equal(6, await read.Teams.CountAsync(team => team.EventId == item.Id));
        Assert.Equal(5, await read.Teams.CountAsync(team => team.EventId == item.Id && team.Active));
        var result = await Read(read, admin.Id);
        Assert.Equal(5, Assert.IsType<DashboardRecap>(result.LatestEndedRecap).TeamCount);
        Assert.Equal(5, Assert.Single(result.History).TeamCount);
        Assert.Equal(5, Assert.Single(result.Chart).TeamCount);
    }

    [Theory]
    [InlineData(EventState.Draft, DashboardNextDateKind.SignupsOpen)]
    [InlineData(EventState.SignupOpen, DashboardNextDateKind.SignupsClose)]
    [InlineData(EventState.SignupClosed, DashboardNextDateKind.EventStarts)]
    [InlineData(EventState.Live, DashboardNextDateKind.EventEnds)]
    public async Task CardExposesPhaseRelevantDate(EventState state, DashboardNextDateKind kind)
    {
        var admin = Admin();
        var item = new BingoEvent(Guid.NewGuid(), "Card", "u2-card", null, "Europe/Copenhagen",
            Clock.AddHours(1), Clock.AddHours(2), Clock.AddHours(3), Clock.AddHours(4), Clock.AddHours(5), null, admin.Id, Clock.AddDays(-1));
        if (state != EventState.Draft) item.OpenSignups(Clock.AddDays(-1));
        if (state is EventState.SignupClosed or EventState.Live) item.CloseSignups(Clock.AddHours(-2));
        if (state == EventState.Live)
        {
            item.SetDraftLocked(true, Clock.AddHours(-2));
            item.StartEvent(Clock.AddHours(-1));
        }
        await using (var db = new ApplicationDbContext(options))
        {
            db.AddRange(admin, item);
            await db.SaveChangesAsync();
        }
        await using var read = new ApplicationDbContext(options);
        var card = Assert.IsType<DashboardEventCard>((await Read(read, admin.Id)).CurrentEvent);
        Assert.Equal(kind, card.NextDateKind);
        Assert.Equal(Clock.AddHours((int)kind switch { 1 => 1, 2 => 2, 0 => 3, _ => 4 }), card.NextDate);
        Assert.Equal("Europe/Copenhagen", card.Timezone);
        Assert.Null(card.Capacity);
    }

    private static async Task SeedBoard(ApplicationDbContext db, BingoEvent item, Account admin, bool reconstructed)
    {
        var team = new Team(Guid.NewGuid(), item.Id, "Fixture team", "fixture-team", "fixture", false, item.ActualStartedAt);
        var person = Participant(item.Id, 1);
        var rsn = reconstructed ? "Import RSN" : "Platform RSN";
        var character = new OsrsCharacter(Guid.NewGuid(), rsn, rsn.ToUpperInvariant(), Clock.AddDays(-20));
        var board = new Board(Guid.NewGuid(), item.Id, "Fixture board", 1, 1);
        var tile = new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), 0, 0, "Manual", "", "", 0m);
        var requirement = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 1, 1, true, false, "Manual", true);
        var at = reconstructed ? item.ActualStartedAt!.Value : Clock.AddDays(-1);
        var approved = new Submission(Guid.NewGuid(), item.Id, team.Id, tile.Id, requirement.Id, null,
            person.Id, character.Id, character.DisplayName, admin.Id, 1, at, null, null);
        approved.Approve(1, at);
        var pending = new Submission(Guid.NewGuid(), item.Id, team.Id, tile.Id, requirement.Id, null,
            person.Id, character.Id, character.DisplayName, admin.Id, 1, at, null, null);
        var rejected = new Submission(Guid.NewGuid(), item.Id, team.Id, tile.Id, requirement.Id, null,
            person.Id, character.Id, character.DisplayName, admin.Id, 1, at, null, null);
        rejected.Reject("Fixture rejection", at);
        db.AddRange(team, person, character, board, tile, requirement, approved, pending, rejected,
            new SubmissionContribution(Guid.NewGuid(), approved.Id, team.Id, requirement.Id, null, person.Id, 1, at));
        await db.SaveChangesAsync();
        await BoardApprovalFixture.PublishAsync(db, board, at, [tile], [requirement]);
    }

    private static Account Admin()
    {
        var admin = Account.CreateWebsite(Guid.NewGuid(), "u2-admin", "U2-ADMIN", Clock.AddDays(-30));
        admin.SetGlobalRole(GlobalRole.Admin);
        return admin;
    }

    private static BingoEvent Archived(Guid id, string name, Guid admin, DateTimeOffset start, DateTimeOffset end) =>
        BingoEvent.CreateArchivedHistorical(id, name, "u2-" + id.ToString("N"), null, "UTC",
            start.AddDays(-2), start.AddDays(-1), start, end, admin, start.AddDays(-3), null, 1, 1, 1, 1);

    private static BingoEvent Live(Guid id, string name, Guid admin, DateTimeOffset start)
    {
        var item = new BingoEvent(id, name, "u2-" + id.ToString("N"), null, "UTC", start.AddDays(-2),
            start.AddDays(-1), start, Clock.AddDays(1), Clock.AddDays(2), null, admin, start.AddDays(-3));
        item.OpenSignups(start.AddDays(-2));
        item.CloseSignups(start.AddDays(-1));
        item.SetDraftLocked(true, start.AddDays(-1));
        item.StartEvent(start);
        return item;
    }

    private static EventParticipant Participant(Guid id, long sequence) =>
        new(Guid.NewGuid(), id, SignupStatus.Confirmed, sequence, Clock.AddDays(-20), SignupSource.AdminCreated);

    private static EventParticipantCharacter Assignment(Guid id, Guid person, Guid character, Guid admin) =>
        new(Guid.NewGuid(), id, person, character, 0, Clock.AddDays(-7), admin, null, EventCharacterRole.Playing, 0m, EhbSource.Manual, null);

    private static Task<AdminDashboardResult> Read(ApplicationDbContext db, Guid admin) =>
        new AdminDashboardService(db, new FixedTimeProvider()).GetAsync(admin);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Clock;
    }
}
