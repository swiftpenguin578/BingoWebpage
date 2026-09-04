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
using Microsoft.Extensions.Configuration;
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
        Assert.Equal(51.8596m, await verify.EventCompetitionCharacterActivities.Where(x => x.EventId == eventItem.Id).Select(x => x.GainedEhb).SingleAsync());
        Assert.Equal(100m, await verify.EventCompetitionCharacterActivities.Where(x => x.EventId == eventItem.Id).Select(x => x.StartEhb).SingleAsync());
        Assert.Equal(151.8596m, await verify.EventCompetitionCharacterActivities.Where(x => x.EventId == eventItem.Id).Select(x => x.EndEhb).SingleAsync());
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
            var expectedScheduledDue = now.AddHours(2);
            Assert.Equal(expectedScheduledDue.AddTicks(-(expectedScheduledDue.Ticks % TimeSpan.TicksPerMicrosecond)), scheduled.NormalDueAt);
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
            Assert.Equal(firstNormalDue.AddTicks(-(firstNormalDue.Ticks % TimeSpan.TicksPerMicrosecond)), (await new EventCompetitionSynchronizationService(afterFirstFetch, fake, new FixedStatus(), clock).GetAsync(eventItem.Id))!.NormalDueAt);
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
            Assert.Equal(firstNormalDue.AddTicks(-(firstNormalDue.Ticks % TimeSpan.TicksPerMicrosecond)), (await verifyResume.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == eventItem.Id)).NormalDueAt);
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
        var expectedRecoveryDue = clock.GetUtcNow().AddHours(2);
        Assert.Equal(expectedRecoveryDue.AddTicks(-(expectedRecoveryDue.Ticks % TimeSpan.TicksPerMicrosecond)), (await verifyRecovery.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == eventItem.Id)).NormalDueAt);
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
    public async Task LiveCompetitionReplacementInvalidatesOnlyAfterAValidatedSuccess()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var now = clock.GetUtcNow();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "live-replacement-admin", "LIVE-REPLACEMENT-ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var eventItem = new BingoEvent(Guid.NewGuid(), "Live replacement", $"live-replacement-{Guid.NewGuid():N}", "", "UTC",
            now.AddHours(-4), now.AddHours(-3), now.AddHours(-2), now.AddHours(2), now.AddHours(2), 20, admin.Id, now);
        eventItem.OpenSignups(now.AddHours(-4));
        eventItem.CloseSignups(now.AddHours(-3));
        eventItem.StartEvent(now.AddHours(-2));
        var prior = new EventCompetitionSynchronization(Guid.NewGuid(), eventItem.Id, 1, 501, "Prior competition",
            eventItem.EventStartsAt, eventItem.EventEndsAt, "prior-fingerprint", now);
        prior.MarkSuccess(now, now, true, "[]", null);
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, eventItem, prior);
            await setup.SaveChangesAsync();
        }

        var replacement = new WiseOldManCompetition(502, "Replacement competition", eventItem.EventStartsAt!.Value, eventItem.EventEndsAt!.Value, now, []);
        var mismatch = new WiseOldManCompetition(503, "Mismatched competition", eventItem.EventStartsAt.Value.AddMinutes(6), eventItem.EventEndsAt.Value, now, []);
        var fake = new FakeCompetitionClient([
            new(WiseOldManCompetitionStatus.Success, replacement),
            new(WiseOldManCompetitionStatus.Success, replacement),
            new(WiseOldManCompetitionStatus.Success, mismatch),
            new(WiseOldManCompetitionStatus.Success, replacement)
        ]);
        var actor = new LifecycleActor(admin.Id, admin.LoginName);

        await using (var configureDb = new ApplicationDbContext(options))
        {
            var configured = await new EventCompetitionSynchronizationService(configureDb, fake, new FixedStatus(), clock)
                .ConfigureAsync(eventItem.Id, eventItem.Version, replacement.Id, false, actor);
            Assert.True(configured.Succeeded, configured.Error);
            var invalidated = await configureDb.EventCompetitionSynchronizations.SingleAsync(x => x.EventId == eventItem.Id);
            Assert.Equal(replacement.Id, invalidated.CompetitionId);
            Assert.Null(invalidated.LatestComplete);
        }

        await using (var refreshDb = new ApplicationDbContext(options))
        {
            var refreshed = await new EventCompetitionSynchronizationService(refreshDb, fake, new FixedStatus(), clock).RefreshAsync(eventItem.Id, actor);
            Assert.True(refreshed.Succeeded, refreshed.Message);
        }

        DateTimeOffset? successfulAt;
        DateTimeOffset originalStart;
        DateTimeOffset originalEnd;
        long currentVersion;
        await using (var baselineDb = new ApplicationDbContext(options))
        {
            var savedEvent = await baselineDb.Events.AsNoTracking().SingleAsync(x => x.Id == eventItem.Id);
            var savedState = await baselineDb.EventCompetitionSynchronizations.AsNoTracking().SingleAsync(x => x.EventId == eventItem.Id);
            currentVersion = savedEvent.Version;
            originalStart = savedEvent.EventStartsAt!.Value;
            originalEnd = savedEvent.EventEndsAt!.Value;
            successfulAt = savedState.LastSuccessfulAt;
            Assert.Equal(502, savedState.CompetitionId);
            Assert.Equal(2, savedState.Generation);
            Assert.Equal(true, savedState.LatestComplete);
        }

        await using (var mismatchDb = new ApplicationDbContext(options))
        {
            var failed = await new EventCompetitionSynchronizationService(mismatchDb, fake, new FixedStatus(), clock)
                .ConfigureAsync(eventItem.Id, currentVersion, mismatch.Id, false, actor);
            Assert.False(failed.Succeeded);
            Assert.Contains("within five minutes", failed.Error, StringComparison.OrdinalIgnoreCase);
        }

        await using (var clearDb = new ApplicationDbContext(options))
        {
            var failed = await new EventCompetitionSynchronizationService(clearDb, fake, new FixedStatus(), clock)
                .ConfigureAsync(eventItem.Id, currentVersion, null, false, actor);
            Assert.False(failed.Succeeded);
            Assert.Contains("replace", failed.Error, StringComparison.OrdinalIgnoreCase);
        }

        await using (var scheduleDb = new ApplicationDbContext(options))
        {
            var failed = await new EventCompetitionSynchronizationService(scheduleDb, fake, new FixedStatus(), clock)
                .ConfigureAsync(eventItem.Id, currentVersion, replacement.Id, true, actor);
            Assert.False(failed.Succeeded);
            Assert.Contains("schedule", failed.Error, StringComparison.OrdinalIgnoreCase);
        }

        await using var verify = new ApplicationDbContext(options);
        var persistedEvent = await verify.Events.AsNoTracking().SingleAsync(x => x.Id == eventItem.Id);
        var persistedState = await verify.EventCompetitionSynchronizations.AsNoTracking().SingleAsync(x => x.EventId == eventItem.Id);
        Assert.Equal(originalStart, persistedEvent.EventStartsAt);
        Assert.Equal(originalEnd, persistedEvent.EventEndsAt);
        Assert.Equal(currentVersion, persistedEvent.Version);
        Assert.Equal(502, persistedState.CompetitionId);
        Assert.Equal(2, persistedState.Generation);
        Assert.Equal(true, persistedState.LatestComplete);
        Assert.Equal(successfulAt, persistedState.LastSuccessfulAt);
        Assert.Equal(1, await verify.AuditEntries.CountAsync(x => x.EventId == eventItem.Id && x.Action == "event.competition_changed"));
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
            var expectedAnchor = now.AddHours(2);
            Assert.Equal(expectedAnchor.AddTicks(-(expectedAnchor.Ticks % TimeSpan.TicksPerMicrosecond)), view.NormalDueAt);
            Assert.Equal(5, fake.Calls);
            clock.Advance(TimeSpan.FromHours(2));
            await service.ProcessDueAsync();
            view = await service.GetAsync(eventItem.Id);
            Assert.Equal(1, view!.RetryCount);
            var expectedRetryAnchor = clock.GetUtcNow().AddHours(2);
            Assert.Equal(expectedRetryAnchor.AddTicks(-(expectedRetryAnchor.Ticks % TimeSpan.TicksPerMicrosecond)), view.NormalDueAt);
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

    [Theory]
    [InlineData(DraftState.Running)]
    [InlineData(DraftState.Paused)]
    [InlineData(DraftState.Finalized)]
    public async Task WiseOldManScheduleSynchronizationRejectsLockedDraftStatesWithoutResidue(DraftState draftState)
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var now = clock.GetUtcNow();
        var admin = Account.CreateWebsite(Guid.NewGuid(), "locked-schedule-admin", "LOCKED-SCHEDULE-ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var eventItem = new BingoEvent(Guid.NewGuid(), "Locked schedule", $"locked-schedule-{Guid.NewGuid():N}", "", "UTC",
            now.AddHours(-2), now.AddHours(-1), null, now.AddDays(1), now.AddDays(2), 20, admin.Id, now);
        eventItem.OpenSignups(now.AddHours(-2));
        eventItem.CloseSignups(now.AddHours(-1));
        eventItem.SetDraftLocked(true);
        var draft = new DraftSession(Guid.NewGuid(), eventItem.Id, 2);
        draft.Start(now.AddMinutes(-30));
        if (draftState == DraftState.Paused) draft.Pause();
        if (draftState == DraftState.Finalized) draft.Finalize(now.AddMinutes(-15));
        var originalStart = eventItem.EventStartsAt;
        var originalEnd = eventItem.EventEndsAt;
        var originalCutoff = eventItem.SubmissionCutoffAt;
        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, eventItem, draft);
            await setup.SaveChangesAsync();
        }

        var competition = new WiseOldManCompetition(98, "Locked schedule competition", now.AddHours(3), now.AddHours(4), now, []);
        var fake = new FakeCompetitionClient([new(WiseOldManCompetitionStatus.Success, competition)]);
        await using (var db = new ApplicationDbContext(options))
        {
            var result = await new EventCompetitionSynchronizationService(db, fake, new FixedStatus(), clock)
                .ConfigureAsync(eventItem.Id, eventItem.Version, competition.Id, true, new(admin.Id, admin.LoginName));
            Assert.False(result.Succeeded);
            Assert.Contains("draft has started", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        await using var verify = new ApplicationDbContext(options);
        var persisted = await verify.Events.AsNoTracking().SingleAsync(x => x.Id == eventItem.Id);
        Assert.Equal(originalStart, persisted.EventStartsAt);
        Assert.Equal(originalEnd, persisted.EventEndsAt);
        Assert.Equal(originalCutoff, persisted.SubmissionCutoffAt);
        Assert.Equal(1, persisted.Version);
        Assert.Equal(draftState, await verify.DraftSessions.Where(x => x.EventId == eventItem.Id).Select(x => x.State).SingleAsync());
        Assert.Empty(await verify.EventCompetitionSynchronizations.Where(x => x.EventId == eventItem.Id).ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == eventItem.Id).ToListAsync());
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public async Task ScheduleEditRejectsAMismatchWithTheLinkedCompetition()
    {
        var now = DateTimeOffset.UtcNow;
        var actor = new LifecycleActor(Guid.NewGuid(), "schedule-admin");
        var item = new BingoEvent(Guid.NewGuid(), "Linked schedule", $"linked-schedule-{Guid.NewGuid():N}", "UTC", actor.Id, now);
        item.ConfigureSchedule(now.AddHours(1), now.AddHours(2), null, now.AddDays(1), now.AddDays(2), 20);
        var state = new EventCompetitionSynchronization(Guid.NewGuid(), item.Id, 1, 99, "Linked competition", item.EventStartsAt, item.EventEndsAt, "", now);
        await using var db = new ApplicationDbContext(options);
        db.AddRange(item, state);
        await db.SaveChangesAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var service = new EventSignupLifecycleService(db, new EventReadinessEvaluator(db, configuration), new TestClock(now));
        var values = new EventScheduleValues(item.SignupOpensAt, item.SignupClosesAt, item.DraftAt, item.EventStartsAt, item.EventEndsAt!.Value.AddMinutes(10), item.ParticipantCap, false);

        var result = await service.SaveScheduleAsync(item.Id, item.Version, values, false, actor);

        Assert.False(result.Succeeded);
        Assert.Contains("within five minutes", result.Error);
        var expectedEnd = now.AddDays(2);
        Assert.Equal(expectedEnd.AddTicks(-(expectedEnd.Ticks % TimeSpan.TicksPerMicrosecond)), (await db.Events.AsNoTracking().SingleAsync(x => x.Id == item.Id)).EventEndsAt);
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
