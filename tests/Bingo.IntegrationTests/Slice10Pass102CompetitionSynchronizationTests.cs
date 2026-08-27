using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice10Pass102CompetitionSynchronizationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice10_pass102")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
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
    public async Task OneCompetitionResponseAggregatesCurrentPlayingOnlyAndCachesAnIncompleteGeneration()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var now = clock.GetUtcNow();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "competition-admin", "COMPETITION-ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var eventItem = new BingoEvent(Guid.NewGuid(), "Competition workflow", $"competition-{Guid.NewGuid():N}", "", "UTC",
            now.AddHours(-2), now.AddHours(-1), now.AddHours(-1), now.AddHours(1), now.AddHours(1), 20, admin.Id, now);
        eventItem.OpenSignups(now.AddHours(-2));
        eventItem.CloseSignups(now.AddHours(-1));
        eventItem.StartEvent(now);
        var participant = new EventParticipant(Guid.NewGuid(), eventItem.Id, SignupStatus.Confirmed, 1, now.AddHours(-2), SignupSource.Website);
        var mathias = new OsrsCharacter(Guid.NewGuid(), "Mathias_Jr", "MATHIAS_JR", now);
        var bob = new OsrsCharacter(Guid.NewGuid(), "Bob", "BOB", now);
        var alt = new OsrsCharacter(Guid.NewGuid(), "Alt", "ALT", now);
        var released = new OsrsCharacter(Guid.NewGuid(), "Released", "RELEASED", now);
        var assignments = new List<EventParticipantCharacter>
        {
            new(Guid.NewGuid(), eventItem.Id, participant.Id, mathias.Id, 0, now, null, null, EventCharacterRole.Playing, 0, EhbSource.Manual, null),
            new(Guid.NewGuid(), eventItem.Id, participant.Id, bob.Id, 1, now, null, null, EventCharacterRole.Playing, 0, EhbSource.Manual, null),
            new(Guid.NewGuid(), eventItem.Id, participant.Id, alt.Id, 2, now, null, null, EventCharacterRole.Informational, null, null, null),
            new(Guid.NewGuid(), eventItem.Id, participant.Id, released.Id, 3, now, null, null, EventCharacterRole.Playing, 0, EhbSource.Manual, null)
        };
        assignments[^1].Release(admin.Id, now);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, eventItem, participant, mathias, bob, alt, released);
            setup.AddRange(assignments);
            await setup.SaveChangesAsync();
        }

        var competition = new WiseOldManCompetition(42, "Test competition", eventItem.EventStartsAt!.Value, eventItem.EventEndsAt!.Value, now,
            [new("mathias jr", "REGULAR", 51.85956m, 100m, 151.85956m), new("Alt", "REGULAR", 99m)]);
        var fake = new FakeCompetitionClient([new(WiseOldManCompetitionStatus.Success, competition), new(WiseOldManCompetitionStatus.Success, competition)]);
        await using (var db = new ApplicationDbContext(options))
        {
            var service = new EventCompetitionSynchronizationService(db, fake, new FixedStatus(), clock);
            var configured = await service.ConfigureAsync(eventItem.Id, eventItem.Version, 42, false, new(admin.Id, admin.LoginName));
            Assert.True(configured.Succeeded, configured.Error);
            var refreshed = await service.RefreshAsync(eventItem.Id, new(admin.Id, admin.LoginName));
            Assert.True(refreshed.Succeeded, refreshed.Message);
            var view = await service.GetAsync(eventItem.Id);
            Assert.False(view!.Complete);
            Assert.Equal(["Bob"], view.MissingAccounts);
        }

        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(1, await verify.EventCompetitionCharacterActivities.CountAsync(x => x.EventId == eventItem.Id));
        Assert.Equal(51.85956m, await verify.EventCompetitionCharacterActivities.Where(x => x.EventId == eventItem.Id).Select(x => x.GainedEhb).SingleAsync());
        Assert.Equal(100m, await verify.EventCompetitionCharacterActivities.Where(x => x.EventId == eventItem.Id).Select(x => x.StartEhb).SingleAsync());
        Assert.Equal(151.85956m, await verify.EventCompetitionCharacterActivities.Where(x => x.EventId == eventItem.Id).Select(x => x.EndEhb).SingleAsync());
        Assert.Equal("Mathias_Jr", await verify.OsrsCharacters.Where(x => x.Id == mathias.Id).Select(x => x.DisplayName).SingleAsync());
        Assert.DoesNotContain(await verify.EventCompetitionCharacterActivities.Where(x => x.EventId == eventItem.Id).Select(x => x.OsrsCharacterId).ToListAsync(), id => id == alt.Id || id == released.Id);
        Assert.Equal(2, fake.Calls);
    }

    [Fact]
    public async Task ManualRefreshDistinguishesSuccessFromPerformedUpstreamFailures()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var now = clock.GetUtcNow();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "refresh-feedback-admin", "REFRESH-FEEDBACK-ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var eventItem = new BingoEvent(Guid.NewGuid(), "Refresh feedback", $"refresh-feedback-{Guid.NewGuid():N}", "", "UTC",
            now.AddHours(-2), now.AddHours(-1), now.AddHours(-1), now.AddHours(1), now.AddHours(1), 20, admin.Id, now);
        eventItem.OpenSignups(now.AddHours(-2)); eventItem.CloseSignups(now.AddHours(-1)); eventItem.StartEvent(now);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, eventItem);
            await setup.SaveChangesAsync();
        }

        var competition = new WiseOldManCompetition(48, "Refresh feedback competition", eventItem.EventStartsAt!.Value, eventItem.EventEndsAt!.Value, now, []);
        var fake = new FakeCompetitionClient([
            new(WiseOldManCompetitionStatus.Success, competition),
            new(WiseOldManCompetitionStatus.Success, competition),
            new(WiseOldManCompetitionStatus.RateLimited, RetryAt: now),
            new(WiseOldManCompetitionStatus.Unavailable, RetryAt: now),
            new(WiseOldManCompetitionStatus.NotFound),
            new(WiseOldManCompetitionStatus.Invalid)
        ]);
        await using var db = new ApplicationDbContext(options);
        var service = new EventCompetitionSynchronizationService(db, fake, new FixedStatus(), clock);
        var actor = new LifecycleActor(admin.Id, admin.LoginName);
        Assert.True((await service.ConfigureAsync(eventItem.Id, eventItem.Version, 48, false, actor)).Succeeded);

        var success = await service.RefreshAsync(eventItem.Id, actor);
        Assert.True(success.Succeeded);
        Assert.False(success.Skipped);

        foreach (var expected in new[] { "RateLimited", "Unavailable", "NotFound", "Invalid" })
        {
            clock.Advance(TimeSpan.FromHours(2));
            var failure = await service.RefreshAsync(eventItem.Id, actor);
            Assert.False(failure.Succeeded);
            Assert.False(failure.Skipped);
            Assert.Equal(expected, failure.ErrorKind);
            Assert.Equal(expected is "RateLimited" or "Unavailable", failure.RetryAt is not null);
        }
    }

    [Fact]
    public async Task InitialLiveTransitionDefersAutomaticFetchAndReturnToLiveKeepsItsSchedule()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var now = clock.GetUtcNow();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "initial-live-admin", "INITIAL-LIVE-ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var eventItem = new BingoEvent(Guid.NewGuid(), "Initial Live scheduling", "initial-live-scheduling", "Initial Live scheduling test event.", "UTC",
            now.AddHours(-2), now.AddHours(-1), now.AddHours(-1), now.AddHours(4), now.AddHours(4), 20, admin.Id, now);
        eventItem.OpenSignups(now.AddHours(-2));
        eventItem.CloseSignups(now.AddHours(-1));
        var board = new Board(Guid.NewGuid(), eventItem.Id, "Initial Live board", 1, 1);
        var draft = new DraftSession(Guid.NewGuid(), eventItem.Id, 1);
        draft.Start(now.AddHours(-2));
        draft.Finalize(now.AddHours(-1));
        var state = new EventCompetitionSynchronization(Guid.NewGuid(), eventItem.Id, 1, 49, "Initial Live competition", eventItem.EventStartsAt, eventItem.EventEndsAt, "", now);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, eventItem, board, draft, state);
            await BoardApprovalFixture.PublishAsync(setup, board, now);
            await setup.SaveChangesAsync();
        }

        var actor = new LifecycleActor(admin.Id, admin.LoginName);
        var competition = new WiseOldManCompetition(49, "Initial Live competition", eventItem.EventStartsAt!.Value, eventItem.EventEndsAt!.Value, now, []);
        var fake = new FakeCompetitionClient([
            new(WiseOldManCompetitionStatus.Success, competition),
            new(WiseOldManCompetitionStatus.Success, competition)
        ]);
        await using (var lifecycle = new ApplicationDbContext(options))
        {
            var started = await new EventLifecycleService(lifecycle, null!, clock).StartNowAsync(eventItem.Id, eventItem.Version, true, null, actor);
            Assert.True(started.Succeeded, started.Error);
        }

        await using (var verifyStart = new ApplicationDbContext(options))
        {
            var scheduled = await verifyStart.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == eventItem.Id);
            Assert.Equal(now.AddHours(2), scheduled.NormalDueAt);
        }
        await using (var beforeDue = new ApplicationDbContext(options))
        {
            await new EventCompetitionSynchronizationService(beforeDue, fake, new FixedStatus(), clock).ProcessDueAsync();
        }
        Assert.Equal(0, fake.Calls);

        clock.Advance(TimeSpan.FromHours(2));
        await using (var firstDue = new ApplicationDbContext(options))
        {
            await new EventCompetitionSynchronizationService(firstDue, fake, new FixedStatus(), clock).ProcessDueAsync();
        }
        Assert.Equal(1, fake.Calls);
        var firstNormalDue = clock.GetUtcNow().AddHours(2);
        await using (var afterFirstFetch = new ApplicationDbContext(options))
        {
            Assert.Equal(firstNormalDue, (await new EventCompetitionSynchronizationService(afterFirstFetch, fake, new FixedStatus(), clock).GetAsync(eventItem.Id))!.NormalDueAt);
        }

        await using (var end = new ApplicationDbContext(options))
        {
            var version = await end.Events.Where(x => x.Id == eventItem.Id).Select(x => x.Version).SingleAsync();
            var ended = await new EventLifecycleService(end, null!, clock).EndNowAsync(eventItem.Id, version, true, "Pause for scheduling test.", actor);
            Assert.True(ended.Succeeded, ended.Error);
        }
        await using (var resume = new ApplicationDbContext(options))
        {
            var version = await resume.Events.Where(x => x.Id == eventItem.Id).Select(x => x.Version).SingleAsync();
            var resumed = await new EventLifecycleService(resume, null!, clock).ResumePrematureEndAsync(eventItem.Id, version, true, "Resume for scheduling test.", clock.GetUtcNow().AddHours(3), actor);
            Assert.True(resumed.Succeeded, resumed.Error);
        }
        await using (var verifyResume = new ApplicationDbContext(options))
        {
            Assert.Equal(firstNormalDue, (await verifyResume.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == eventItem.Id)).NormalDueAt);
        }

        await using (var makeOverdue = new ApplicationDbContext(options))
        {
            var scheduled = await makeOverdue.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == eventItem.Id);
            scheduled.MarkSuccess(clock.GetUtcNow().AddHours(-3), clock.GetUtcNow().AddHours(-3), true, "[]", null);
            await makeOverdue.SaveChangesAsync();
        }
        await using (var end = new ApplicationDbContext(options))
        {
            var version = await end.Events.Where(x => x.Id == eventItem.Id).Select(x => x.Version).SingleAsync();
            Assert.True((await new EventLifecycleService(end, null!, clock).EndNowAsync(eventItem.Id, version, true, "Pause for overdue test.", actor)).Succeeded);
        }
        await using (var resume = new ApplicationDbContext(options))
        {
            var version = await resume.Events.Where(x => x.Id == eventItem.Id).Select(x => x.Version).SingleAsync();
            Assert.True((await new EventLifecycleService(resume, null!, clock).ResumePrematureEndAsync(eventItem.Id, version, true, "Resume overdue test.", clock.GetUtcNow().AddHours(3), actor)).Succeeded);
        }
        await using (var overdue = new ApplicationDbContext(options))
        {
            await new EventCompetitionSynchronizationService(overdue, fake, new FixedStatus(), clock).ProcessDueAsync();
        }
        Assert.Equal(2, fake.Calls);
        await using var verifyRecovery = new ApplicationDbContext(options);
        Assert.Equal(clock.GetUtcNow().AddHours(2), (await verifyRecovery.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == eventItem.Id)).NormalDueAt);
    }

    [Fact]
    public async Task StaleConfigurationPostLeavesOneStateAndOneAudit()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var now = clock.GetUtcNow();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "configuration-admin", "CONFIGURATION-ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var eventItem = new BingoEvent(Guid.NewGuid(), "Configuration workflow", $"configuration-{Guid.NewGuid():N}", "", "UTC",
            now.AddHours(-2), now.AddHours(-1), now.AddHours(-1), now.AddHours(1), now.AddHours(1), 20, admin.Id, now);
        eventItem.OpenSignups(now.AddHours(-2)); eventItem.CloseSignups(now.AddHours(-1));
        await using (var setup = new ApplicationDbContext(options)) { setup.AddRange(admin, eventItem); await setup.SaveChangesAsync(); }
        var firstCompetition = new WiseOldManCompetition(46, "First competition", eventItem.EventStartsAt!.Value, eventItem.EventEndsAt!.Value, now, []);
        var secondCompetition = new WiseOldManCompetition(47, "Second competition", eventItem.EventStartsAt!.Value, eventItem.EventEndsAt!.Value, now, []);
        var fake = new FakeCompetitionClient([
            new(WiseOldManCompetitionStatus.Success, firstCompetition),
            new(WiseOldManCompetitionStatus.Success, secondCompetition)
        ]);
        var actor = new LifecycleActor(admin.Id, admin.LoginName);
        await using (var firstDb = new ApplicationDbContext(options))
        {
            var first = await new EventCompetitionSynchronizationService(firstDb, fake, new FixedStatus(), clock)
                .ConfigureAsync(eventItem.Id, eventItem.Version, 46, false, actor);
            Assert.True(first.Succeeded, first.Error);
        }
        await using (var staleDb = new ApplicationDbContext(options))
        {
            var stale = await new EventCompetitionSynchronizationService(staleDb, fake, new FixedStatus(), clock)
                .ConfigureAsync(eventItem.Id, eventItem.Version, 47, false, actor);
            Assert.False(stale.Succeeded);
        }

        await using var verify = new ApplicationDbContext(options);
        var state = await verify.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == eventItem.Id);
        Assert.Equal(46, state.CompetitionId);
        Assert.Equal(1, await verify.AuditEntries.CountAsync(x => x.EventId == eventItem.Id && x.Action == "event.competition_linked"));
    }

    [Fact]
    public async Task CooldownIsSharedAndAssignmentChangeStartsANewAuthoritativeGeneration()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var now = clock.GetUtcNow();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "generation-admin", "GENERATION-ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var eventItem = new BingoEvent(Guid.NewGuid(), "Generation workflow", $"generation-{Guid.NewGuid():N}", "", "UTC",
            now.AddHours(-2), now.AddHours(-1), now.AddHours(-1), now.AddHours(1), now.AddHours(1), 20, admin.Id, now);
        eventItem.OpenSignups(now.AddHours(-2)); eventItem.CloseSignups(now.AddHours(-1)); eventItem.StartEvent(now);
        var participant = new EventParticipant(Guid.NewGuid(), eventItem.Id, SignupStatus.Confirmed, 1, now.AddHours(-2), SignupSource.Website);
        var alice = new OsrsCharacter(Guid.NewGuid(), "Alice", "ALICE", now);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, eventItem, participant, alice,
                new EventParticipantCharacter(Guid.NewGuid(), eventItem.Id, participant.Id, alice.Id, 0, now, null, null, EventCharacterRole.Playing, 0, EhbSource.Manual, null));
            await setup.SaveChangesAsync();
        }
        var competition = new WiseOldManCompetition(43, "Generation competition", eventItem.EventStartsAt!.Value, eventItem.EventEndsAt!.Value, now,
            [new("Alice", "REGULAR", 1m), new("Bob", "REGULAR", 2m)]);
        var fake = new FakeCompetitionClient([
            new(WiseOldManCompetitionStatus.Success, competition),
            new(WiseOldManCompetitionStatus.Success, competition),
            new(WiseOldManCompetitionStatus.Success, competition)
        ]);
        await using (var db = new ApplicationDbContext(options))
        {
            var service = new EventCompetitionSynchronizationService(db, fake, new FixedStatus(), clock);
            Assert.True((await service.ConfigureAsync(eventItem.Id, eventItem.Version, 43, false, new(admin.Id, admin.LoginName))).Succeeded);
            Assert.True((await service.RefreshAsync(eventItem.Id, new(admin.Id, admin.LoginName))).Succeeded);
            var cached = await service.RefreshAsync(eventItem.Id, new(admin.Id, admin.LoginName));
            Assert.True(cached.Skipped);
        }

        var bob = new OsrsCharacter(Guid.NewGuid(), "Bob", "BOB", now);
        await using (var add = new ApplicationDbContext(options))
        {
            var current = await add.EventParticipants.SingleAsync(x => x.Id == participant.Id);
            add.OsrsCharacters.Add(bob);
            add.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), eventItem.Id, current.Id, bob.Id, 1, now, null, null, EventCharacterRole.Playing, 0, EhbSource.Manual, null));
            await add.SaveChangesAsync();
        }
        await using (var db = new ApplicationDbContext(options))
        {
            var service = new EventCompetitionSynchronizationService(db, fake, new FixedStatus(), clock);
            Assert.True((await service.RefreshAsync(eventItem.Id, new(admin.Id, admin.LoginName))).Succeeded);
            var view = await service.GetAsync(eventItem.Id);
            Assert.Equal(2, view!.Generation);
            Assert.True(view.Complete);
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(1, await verify.EventCompetitionSynchronizations.CountAsync(x => x.EventId == eventItem.Id));
        Assert.Equal(2, await verify.EventCompetitionCharacterActivities.CountAsync(x => x.EventId == eventItem.Id && x.Generation == 2));
        Assert.Equal(3, await verify.EventCompetitionCharacterActivities.CountAsync(x => x.EventId == eventItem.Id));
    }

    [Fact]
    public async Task FourTemporaryFailuresExhaustTheRetryCycleWithoutMovingTheTwoHourAnchor()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var now = clock.GetUtcNow();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "retry-admin", "RETRY-ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var eventItem = new BingoEvent(Guid.NewGuid(), "Retry workflow", $"retry-{Guid.NewGuid():N}", "", "UTC",
            now.AddHours(-2), now.AddHours(-1), now.AddHours(-1), now.AddHours(1), now.AddHours(1), 20, admin.Id, now);
        eventItem.OpenSignups(now.AddHours(-2)); eventItem.CloseSignups(now.AddHours(-1)); eventItem.StartEvent(now);
        await using (var setup = new ApplicationDbContext(options)) { setup.AddRange(admin, eventItem); await setup.SaveChangesAsync(); }
        var competition = new WiseOldManCompetition(44, "Retry competition", eventItem.EventStartsAt!.Value, eventItem.EventEndsAt!.Value, now, []);
        var fake = new FakeCompetitionClient([
            new(WiseOldManCompetitionStatus.Success, competition),
            new(WiseOldManCompetitionStatus.Unavailable, Message: "temporary"),
            new(WiseOldManCompetitionStatus.Unavailable, Message: "temporary"),
            new(WiseOldManCompetitionStatus.Unavailable, Message: "temporary"),
            new(WiseOldManCompetitionStatus.Unavailable, Message: "temporary")
        ]);
        await using (var db = new ApplicationDbContext(options))
        {
            var service = new EventCompetitionSynchronizationService(db, fake, new FixedStatus(), clock);
            Assert.True((await service.ConfigureAsync(eventItem.Id, eventItem.Version, 44, false, new(admin.Id, admin.LoginName))).Succeeded);
            await service.RefreshAsync(eventItem.Id, new(admin.Id, admin.LoginName));
            clock.Advance(TimeSpan.FromMinutes(1)); await service.ProcessDueAsync();
            clock.Advance(TimeSpan.FromMinutes(2)); await service.ProcessDueAsync();
            clock.Advance(TimeSpan.FromMinutes(4)); await service.ProcessDueAsync();
            var view = await service.GetAsync(eventItem.Id);
            Assert.Equal(4, view!.RetryCount);
            Assert.Null(view.RetryDueAt);
            Assert.Equal(now.AddHours(2), view.NormalDueAt);
            Assert.Equal(5, fake.Calls);
            clock.Advance(TimeSpan.FromHours(2));
            await service.ProcessDueAsync();
            view = await service.GetAsync(eventItem.Id);
            Assert.Equal(1, view!.RetryCount);
            Assert.Equal(clock.GetUtcNow().AddHours(2), view.NormalDueAt);
            Assert.Equal(6, fake.Calls);
        }
    }

    [Fact]
    public async Task RetryAfterBeyondNormalAnchorBlocksTheAnchorUntilRetryIsDue()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var now = clock.GetUtcNow();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "retry-after-admin", "RETRY-AFTER-ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var eventItem = new BingoEvent(Guid.NewGuid(), "Retry-After workflow", $"retry-after-{Guid.NewGuid():N}", "", "UTC",
            now.AddHours(-2), now.AddHours(-1), now.AddHours(-1), now.AddHours(1), now.AddHours(1), 20, admin.Id, now);
        eventItem.OpenSignups(now.AddHours(-2)); eventItem.CloseSignups(now.AddHours(-1)); eventItem.StartEvent(now);
        await using (var setup = new ApplicationDbContext(options)) { setup.AddRange(admin, eventItem); await setup.SaveChangesAsync(); }
        var competition = new WiseOldManCompetition(45, "Retry-After competition", eventItem.EventStartsAt!.Value, eventItem.EventEndsAt!.Value, now, []);
        var fake = new FakeCompetitionClient([
            new(WiseOldManCompetitionStatus.Success, competition),
            new(WiseOldManCompetitionStatus.RateLimited, RetryAt: now.AddHours(3), Message: "retry later"),
            new(WiseOldManCompetitionStatus.Success, competition)
        ]);
        await using var db = new ApplicationDbContext(options);
        var service = new EventCompetitionSynchronizationService(db, fake, new FixedStatus(), clock);
        Assert.True((await service.ConfigureAsync(eventItem.Id, eventItem.Version, 45, false, new(admin.Id, admin.LoginName))).Succeeded);
        await service.RefreshAsync(eventItem.Id, new(admin.Id, admin.LoginName));
        clock.Advance(TimeSpan.FromHours(2));
        await service.ProcessDueAsync();
        Assert.Equal(2, fake.Calls);
        clock.Advance(TimeSpan.FromHours(1));
        await service.ProcessDueAsync();
        Assert.Equal(3, fake.Calls);
    }

    private sealed class FakeCompetitionClient(IReadOnlyList<WiseOldManCompetitionResult> results) : IWiseOldManCompetitionClient
    {
        private int index;
        public int Calls => index;
        public Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, CancellationToken cancellationToken = default) => Task.FromResult(results[Math.Min(index++, results.Count - 1)]);
    }

    private sealed class FixedStatus : IWiseOldManStatus
    {
        public WiseOldManRequestStatus GetStatus() => new(20, 17, null, null, null, null, null, null);
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset value = now;
        public override DateTimeOffset GetUtcNow() => value;
        public void Advance(TimeSpan amount) => value += amount;
    }
}
