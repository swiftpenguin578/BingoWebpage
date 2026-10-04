using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed partial class Au12PlacementRuleIntegrationTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;
    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }
    public Task DisposeAsync() => database.DisposeAsync().AsTask();
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }

    [Fact]
    public async Task CreationPersistsNewRuleReplayRetainsItAndHistoricalFactoryIsLegacy()
    {
        await using var db = new ApplicationDbContext(options);
        var admin = Account.CreateWebsite(Guid.NewGuid(), "au12-admin", "AU12-ADMIN", Now);
        admin.SetGlobalRole(GlobalRole.Admin);
        db.Add(admin); await db.SaveChangesAsync();
        var actor = new LifecycleActor(admin.Id, "au12-admin");
        var request = Guid.NewGuid();
        var service = new EventCreationService(db, new Clock());
        var result = await service.CreateAsync(request, "New AU12", "UTC", actor);
        Assert.Equal(EventCreationOutcome.Completed, result.Outcome);
        Assert.Equal(result, await service.CreateAsync(request, "New AU12", "UTC", actor));
        db.ChangeTracker.Clear();
        Assert.Equal(PlacementRule.CreditedEhbThenScoreTime, (await db.Events.SingleAsync()).PlacementRule);
        var historical = BingoEvent.CreateArchivedHistorical(Guid.NewGuid(), "Historical", "historical", null, "UTC", null, null,
            Now.AddDays(-4), Now.AddDays(-2), admin.Id, Now, null, 2, 1, 2, 2);
        db.Add(historical); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal(PlacementRule.LegacyScoreTimeThenEhb, (await db.Events.SingleAsync(x => x.Id == historical.Id)).PlacementRule);
        var created = await db.Events.SingleAsync(x => x.Id == result.EventId);
        db.Entry(created).Property(x => x.PlacementRule).CurrentValue = PlacementRule.LegacyScoreTimeThenEhb;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(PlacementRule.LegacyScoreTimeThenEhb, false)]
    [InlineData(PlacementRule.CreditedEhbThenScoreTime, false)]
    [InlineData(PlacementRule.LegacyScoreTimeThenEhb, true)]
    [InlineData(PlacementRule.CreditedEhbThenScoreTime, true)]
    public async Task ProvisionalAndOfficialUsePersistedRuleAndPostgresPrecision(PlacementRule rule, bool exactTie)
    {
        var fixture = await SeedAsync(rule, exactTie);
        await using var db = new ApplicationDbContext(options);
        var publicBoards = new PublicBoardService(db, new Clock());
        var board = await publicBoards.GetEventBoardAsync("au12-event");
        Assert.NotNull(board);
        var ordered = board.Teams.OrderBy(x => x.Rank).ToList();
        var expected = exactTie || rule == PlacementRule.LegacyScoreTimeThenEhb ? "Team A" : "Team B";
        Assert.Equal(expected, ordered[0].TeamName);
        Assert.Equal<int>(exactTie ? [1, 1] : [1, 2], ordered.Select(x => x.Rank));
        Assert.Equal(Now.AddHours(-3).AddTicks(10), board.Teams.Single(x => x.TeamName == "Team A").Progress.CurrentScoreReachedAt);
        var service = new EventFinalizationService(db, publicBoards, new Clock());
        var readiness = await service.GetReadinessAsync(fixture.EventId);
        Assert.True(readiness!.CanFinalize, string.Join(";", readiness.Blockers.Select(x => x.Description)));
        Assert.Equal(rule, readiness.PlacementRule);
        Assert.Equal(board.Teams.Select(x => (x.TeamId, x.Rank)), readiness.Placements.Select(x => (x.TeamId, x.Placement)));
        var result = await service.FinalizeAsync(fixture.EventId, new LifecycleActor(fixture.AdminId, "admin"), readiness.EventVersion);
        Assert.True(result.Published);
        db.ChangeTracker.Clear();
        var official = await db.OfficialPlacements.OrderBy(x => x.Placement).ThenBy(x => x.TeamName).ToListAsync();
        Assert.Equal(ordered.Select(x => (x.TeamName, x.Rank, x.Progress.EhbTiebreak, x.Progress.CurrentScoreReachedAt)),
            official.Select(x => (x.TeamName, x.Placement, x.EhbTiebreak, x.CurrentScoreReachedAt)));
        var snapshot = await db.EventFinalizations.SingleAsync();
        Assert.Contains(rule.ToString(), snapshot.CalculationInputsJson);
        var inputs = snapshot.CalculationInputsJson;
        await service.FinalizeAsync(fixture.EventId, new LifecycleActor(fixture.AdminId, "admin"), readiness.EventVersion);
        Assert.Equal(inputs, (await db.EventFinalizations.SingleAsync()).CalculationInputsJson);
    }

    [Theory]
    [InlineData(PlacementRule.CreditedEhbThenScoreTime, false)]
    [InlineData(PlacementRule.CreditedEhbThenScoreTime, true)]
    [InlineData(PlacementRule.LegacyScoreTimeThenEhb, false)]
    [InlineData(PlacementRule.LegacyScoreTimeThenEhb, true)]
    public async Task FractionalCreditPublicAndOfficialRanksAgree(PlacementRule rule, bool sameTime)
    {
        var fixture = await SeedAsync(rule, sameTime, fractionalCredit: true);
        await using var db = new ApplicationDbContext(options);
        var publicBoards = new PublicBoardService(db, new Clock());
        var board = (await publicBoards.GetEventBoardAsync("au12-event"))!;
        var a = board.Teams.Single(x => x.TeamName == "Team A");
        var b = board.Teams.Single(x => x.TeamName == "Team B");
        Assert.True(a.Progress.EhbTiebreak < b.Progress.EhbTiebreak);
        Assert.Equal(decimal.Round(a.Progress.EhbTiebreak, 4), decimal.Round(b.Progress.EhbTiebreak, 4));
        var shared = sameTime && rule == PlacementRule.CreditedEhbThenScoreTime;
        Assert.Equal(sameTime && !shared ? 2 : 1, a.Rank);
        Assert.Equal(sameTime ? 1 : 2, b.Rank);
        Assert.Equal(Now.AddHours(-3).AddTicks(10), a.Progress.CurrentScoreReachedAt);
        var service = new EventFinalizationService(db, publicBoards, new Clock());
        var readiness = (await service.GetReadinessAsync(fixture.EventId))!;
        Assert.True(readiness.CanFinalize);
        Assert.Equal(board.Teams.Select(x => (x.TeamId, x.Rank)), readiness.Placements.Select(x => (x.TeamId, x.Placement)));
        Assert.True((await service.FinalizeAsync(fixture.EventId, new LifecycleActor(fixture.AdminId, "admin"), readiness.EventVersion)).Published);
        db.ChangeTracker.Clear();
        var official = await db.OfficialPlacements.OrderBy(x => x.TeamName).ToListAsync();
        Assert.Equal(board.Teams.OrderBy(x => x.TeamName).Select(x => (x.TeamName, x.Rank, decimal.Round(x.Progress.EhbTiebreak, 4))),
            official.Select(x => (x.TeamName, x.Placement, x.EhbTiebreak)));
    }

    [Fact]
    public async Task PopulatedMigrationBackfillsAllExistingEventsAndDownPreservesHistory()
    {
        var fixture = await SeedAsync(PlacementRule.CreditedEhbThenScoreTime, false);
        await using (var finalize = new ApplicationDbContext(options))
        {
            var service = new EventFinalizationService(finalize, new PublicBoardService(finalize, new Clock()), new Clock());
            var readiness = await service.GetReadinessAsync(fixture.EventId);
            await service.FinalizeAsync(fixture.EventId, new LifecycleActor(fixture.AdminId, "admin"), readiness!.EventVersion);
            finalize.Add(new BingoEvent(Guid.NewGuid(), "Existing draft", "existing-draft", "UTC", fixture.AdminId, Now));
            await finalize.SaveChangesAsync();
        }
        await using var db = new ApplicationDbContext(options);
        var inputs = (await db.EventFinalizations.SingleAsync()).CalculationInputsJson;
        var ranks = await db.OfficialPlacements.OrderBy(x => x.TeamName).Select(x => x.Placement).ToListAsync();
        await db.GetService<IMigrator>().MigrateAsync("20261003184632_AllowCancelledDraftRestart");
        Assert.Equal(2, await db.Database.SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM events").SingleAsync());
        await db.Database.MigrateAsync();
        Assert.All(await db.Events.AsNoTracking().ToListAsync(), x => Assert.Equal(PlacementRule.LegacyScoreTimeThenEhb, x.PlacementRule));
        Assert.Equal(inputs, (await db.EventFinalizations.SingleAsync()).CalculationInputsJson);
        Assert.Equal(ranks, await db.OfficialPlacements.OrderBy(x => x.TeamName).Select(x => x.Placement).ToListAsync());
        var defaultCount = await db.Database.SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM information_schema.columns WHERE table_name='events' AND column_name='placement_rule' AND column_default IS NOT NULL").SingleAsync();
        Assert.Equal(0, defaultCount);
    }

    private async Task<(Guid EventId, Guid AdminId)> SeedAsync(PlacementRule rule, bool exactTie, bool fractionalCredit = false)
    {
        await using var db = new ApplicationDbContext(options);
        var admin = Account.CreateWebsite(Guid.NewGuid(), "au12-admin", "AU12-ADMIN", Now.AddDays(-10));
        admin.SetGlobalRole(GlobalRole.Admin);
        var ev = new BingoEvent(Guid.NewGuid(), "AU12 event", "au12-event", "UTC", admin.Id, Now.AddDays(-10), rule);
        ev.ConfigureSchedule(Now.AddDays(-8), Now.AddDays(-5), null, Now.AddHours(-4), Now.AddHours(-1), 20);
        ev.OpenSignups(Now.AddDays(-8)); ev.CloseSignups(Now.AddDays(-5)); ev.SetDraftRosterPublication(true);
        ev.StartEvent(Now.AddHours(-4)); ev.MarkFirstPublic(Now.AddDays(-8));
        var board = new Board(Guid.NewGuid(), ev.Id, "AU12 board", 2, 2);
        var tiles = Enumerable.Range(0, 4).Select(i => new BoardTile(Guid.NewGuid(), board.Id, Guid.NewGuid(), i / 2, i % 2, "Tile " + i, "", "", fractionalCredit ? i == 0 ? 1m : 10m : 4m)).ToList();
        var requirements = tiles.Select((tile, i) => new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, 0, i == 0 ? 1 : fractionalCredit ? 3 : 4, true, false, "Objective", true)).ToList();
        board.SetTotalEhb(fractionalCredit ? 31m : 16m);
        var draft = new DraftSession(Guid.NewGuid(), ev.Id, 1); draft.FinalizeDirect(Now.AddDays(-2));
        var publication = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, 1, Now.AddDays(-2), admin.Id, DraftPublicationMethod.DirectRoster);
        db.AddRange(admin, ev, board, draft, publication); db.AddRange(tiles); db.AddRange(requirements);
        for (var index = 0; index < 2; index++)
        {
            var team = new Team(Guid.NewGuid(), ev.Id, index == 0 ? "Team A" : "Team B", "team-" + index, TeamFormationType.Drafted, null, true); team.Finalize(Now.AddDays(-2));
            var participant = new EventParticipant(Guid.NewGuid(), ev.Id, SignupStatus.Confirmed, index + 1, Now.AddDays(-6), SignupSource.AdminCreated);
            var character = new OsrsCharacter(Guid.NewGuid(), "AU12 Player " + index, "AU12 PLAYER " + index, Now.AddDays(-6));
            db.AddRange(team, participant, character,
                new EventParticipantCharacter(Guid.NewGuid(), ev.Id, participant.Id, character.Id, 0, Now.AddDays(-6), admin.Id, null, EventCharacterRole.Playing, 10m, EhbSource.Manual, null),
                new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, TeamMembershipRole.Participant, Now.AddDays(-4), null, "AU12 fixture"),
                new DraftPublicationRoster(Guid.NewGuid(), publication.Id, team.Id, participant.Id, TeamMembershipRole.Participant, null, character.DisplayName));
            foreach (var objective in fractionalCredit && index == 0 ? new[] { 0, 1, 2 } : new[] { 0, 1 })
            {
                var amount = objective == 0 ? 1 : fractionalCredit ? index == 0 ? 1 : 2 : exactTie ? 2 : index == 0 ? 1 : 3;
                var at = Now.AddHours(-3).AddSeconds(exactTie ? 0 : index).AddTicks(index == 0 ? 11 : 19);
                var submission = new Submission(Guid.NewGuid(), ev.Id, team.Id, tiles[objective].Id, requirements[objective].Id, null, participant.Id, character.Id, character.DisplayName, admin.Id, amount, at, null, null);
                submission.Approve(amount, Now.AddHours(-2));
                db.AddRange(submission, new SubmissionContribution(Guid.NewGuid(), submission.Id, team.Id, requirements[objective].Id, null, participant.Id, amount, Now.AddHours(-2)));
            }
        }
        await BoardApprovalFixture.PublishAsync(db, board, Now.AddDays(-2), tiles, requirements);
        ev.EndEvent(Now.AddHours(-1));
        db.Add(new EventStateTransition(Guid.NewGuid(), ev.Id, EventState.Live, EventState.AwaitingFinalReview, admin.Id, Now.AddHours(-1), "AU12 fixture", effectiveAt: Now.AddHours(-1)));
        await db.SaveChangesAsync();
        return (ev.Id, admin.Id);
    }
}
