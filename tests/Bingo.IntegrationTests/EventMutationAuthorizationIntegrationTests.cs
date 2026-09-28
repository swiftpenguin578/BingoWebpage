using System.Data;
using Bingo.Application.Boards;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

/// <summary>
/// VER-01 boundary proof for the human event lifecycle services.  Each
/// unauthorized call uses a real PostgreSQL context and is followed by a
/// fresh-context snapshot so a successful-looking result cannot hide a
/// mutation or audit row.
/// </summary>
public sealed class EventMutationAuthorizationIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ver01_event_authorization")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();

    private readonly DateTimeOffset now = new(2026, 7, 27, 20, 0, 0, TimeSpan.Zero);
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task HumanLifecycleServicesRejectMissingDisabledAndUserActorsBeforeMutationAndAllowAdmin()
    {
        var admin = Account.CreateWebsite(Guid.NewGuid(), "VER-01 lifecycle admin", "VER-01 LIFECYCLE ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var user = Account.CreateWebsite(Guid.NewGuid(), "VER-01 lifecycle user", "VER-01 LIFECYCLE USER", now);
        var disabled = Account.CreateWebsite(Guid.NewGuid(), "VER-01 lifecycle disabled", "VER-01 LIFECYCLE DISABLED", now);
        disabled.SetGlobalRole(GlobalRole.Admin);
        disabled.Disable(now, admin.Id, "VER-01 authorization proof");

        var signupEvent = Draft(Guid.NewGuid(), "ver01-auth-signup", admin.Id);
        signupEvent.ConfigureSchedule(now.AddHours(-3), now.AddHours(2), null, now.AddHours(3), now.AddHours(4), 20);
        signupEvent.OpenSignups(now.AddHours(-3));

        var lifecycleEvent = Draft(Guid.NewGuid(), "ver01-auth-lifecycle", admin.Id);
        lifecycleEvent.ConfigureSchedule(now.AddHours(-3), now.AddHours(-2), null, now.AddHours(-1), now.AddHours(2), 20);

        var destructiveEvent = Draft(Guid.NewGuid(), "ver01-auth-destructive", admin.Id);
        var protectedParticipant = new EventParticipant(Guid.NewGuid(), destructiveEvent.Id, SignupStatus.Confirmed, 1, now, SignupSource.AdminCreated, null);

        var publicationEventId = Guid.NewGuid();
        var publicationTeamId = Guid.NewGuid();
        var publicationEvent = AwaitingReview(publicationEventId, admin.Id);
        var publicationTransition = new EventStateTransition(Guid.NewGuid(), publicationEventId, EventState.Live, EventState.AwaitingFinalReview, admin.Id, now.AddHours(-2), "Ended", effectiveAt: now.AddHours(-2));
        var publicationTeam = new Team(publicationTeamId, publicationEventId, "Winners", "winners", TeamFormationType.Drafted, null, true, now);

        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, user, disabled, signupEvent, lifecycleEvent, destructiveEvent, protectedParticipant, publicationEvent, publicationTransition, publicationTeam);
            await setup.SaveChangesAsync();
        }

        var unauthorized = new[]
        {
            new LifecycleActor(Guid.NewGuid(), "ver01-missing"),
            new LifecycleActor(user.Id, user.LoginName),
            new LifecycleActor(disabled.Id, disabled.LoginName)
        };

        var signupSnapshot = await SnapshotAsync(signupEvent.Id);
        foreach (var actor in unauthorized)
        {
            await using var db = new ApplicationDbContext(options);
            var result = await new EventSignupLifecycleService(db, new NoReadiness(), new FixedClock(now))
                .CloseAsync(signupEvent.Id, signupSnapshot.Version, true, actor);
            Assert.False(result.Succeeded);
            Assert.Contains("active website administrator", result.Error, StringComparison.Ordinal);
            await AssertSnapshotAsync(signupEvent.Id, signupSnapshot);
        }

        await using (var signupDb = new ApplicationDbContext(options))
        {
            var result = await new EventSignupLifecycleService(signupDb, new NoReadiness(), new FixedClock(now))
                .CloseAsync(signupEvent.Id, signupSnapshot.Version, true, new LifecycleActor(admin.Id, admin.LoginName));
            Assert.True(result.Succeeded, result.Error);
        }
        Assert.Equal(EventState.SignupClosed, (await SnapshotAsync(signupEvent.Id)).State);

        var lifecycleSnapshot = await SnapshotAsync(lifecycleEvent.Id);
        foreach (var actor in unauthorized)
        {
            await using var db = new ApplicationDbContext(options);
            var result = await new EventLifecycleService(db, new NoSignupLifecycle(), new FixedClock(now))
                .EndNowAsync(lifecycleEvent.Id, lifecycleSnapshot.Version, true, "VER-01 manual end", actor);
            Assert.False(result.Succeeded);
            Assert.Contains("active website administrator", result.Error, StringComparison.Ordinal);
            await AssertSnapshotAsync(lifecycleEvent.Id, lifecycleSnapshot);
        }

        var destructiveSnapshot = await SnapshotAsync(destructiveEvent.Id);
        foreach (var actor in unauthorized)
        {
            await using var db = new ApplicationDbContext(options);
            var result = await new EventDestructiveLifecycleService(db, new FixedClock(now))
                .CancelAsync(destructiveEvent.Id, destructiveSnapshot.Version, true, "VER-01 cancellation", actor);
            Assert.False(result.Succeeded);
            Assert.Contains("active website administrator", result.Error, StringComparison.Ordinal);
            await AssertSnapshotAsync(destructiveEvent.Id, destructiveSnapshot);
        }

        await using (var destructiveDb = new ApplicationDbContext(options))
        {
            var result = await new EventDestructiveLifecycleService(destructiveDb, new FixedClock(now))
                .CancelAsync(destructiveEvent.Id, destructiveSnapshot.Version, true, "VER-01 cancellation", new LifecycleActor(admin.Id, admin.LoginName));
            Assert.True(result.Succeeded, result.Error);
        }
        Assert.Equal(EventState.Cancelled, (await SnapshotAsync(destructiveEvent.Id)).State);

        var publicationSnapshot = await SnapshotAsync(publicationEventId);
        foreach (var actor in unauthorized)
        {
            await using var db = new ApplicationDbContext(options);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new EventFinalizationService(db, new ReadyBoard(publicationTeamId), new FixedClock(now))
                    .FinalizeAsync(publicationEventId, actor, publicationSnapshot.Version));
            Assert.Contains("active website administrator", error.Message, StringComparison.Ordinal);
            await AssertSnapshotAsync(publicationEventId, publicationSnapshot);
        }

        var adminActor = new LifecycleActor(admin.Id, admin.LoginName);
        await using (var publicationDb = new ApplicationDbContext(options))
        {
            await new EventFinalizationService(publicationDb, new ReadyBoard(publicationTeamId), new FixedClock(now))
                .FinalizeAsync(publicationEventId, adminActor, publicationSnapshot.Version);
        }
        var publishedSnapshot = await SnapshotAsync(publicationEventId);
        Assert.Equal(EventState.Archived, publishedSnapshot.State);

        foreach (var actor in unauthorized)
        {
            await using var db = new ApplicationDbContext(options);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new EventFinalizationService(db, new ReadyBoard(publicationTeamId), new FixedClock(now))
                    .UnfinalizeAsync(publicationEventId, "VER-01 unauthorized", true, actor, publishedSnapshot.Version));
            Assert.Contains("active website administrator", error.Message, StringComparison.Ordinal);
            await AssertSnapshotAsync(publicationEventId, publishedSnapshot);
        }

        await using (var reopenDb = new ApplicationDbContext(options))
        {
            await new EventFinalizationService(reopenDb, new ReadyBoard(publicationTeamId), new FixedClock(now))
                .UnfinalizeAsync(publicationEventId, "VER-01 authorized reopen", true, adminActor, publishedSnapshot.Version);
        }
        Assert.Equal(EventState.AwaitingFinalReview, (await SnapshotAsync(publicationEventId)).State);

        await using (var lifecycleSetup = new ApplicationDbContext(options))
        {
            var item = await lifecycleSetup.Events.SingleAsync(value => value.Id == lifecycleEvent.Id);
            item.OpenSignups(now.AddHours(-3));
            item.CloseSignups(now.AddHours(-2));
            item.StartEvent(now.AddHours(-1));
            await lifecycleSetup.SaveChangesAsync();
        }

        var liveLifecycleSnapshot = await SnapshotAsync(lifecycleEvent.Id);
        await using (var lifecycleDb = new ApplicationDbContext(options))
        {
            var result = await new EventLifecycleService(lifecycleDb, new NoSignupLifecycle(), new FixedClock(now))
                .EndNowAsync(lifecycleEvent.Id, liveLifecycleSnapshot.Version, true, "VER-01 manual end", adminActor);
            Assert.True(result.Succeeded, result.Error);
        }
        Assert.Equal(EventState.AwaitingFinalReview, (await SnapshotAsync(lifecycleEvent.Id)).State);
    }

    [Fact]
    public async Task RevocationCommittedWhileLifecycleMutationWaitsOnAccountLockIsRejected()
    {
        var admin = Account.CreateWebsite(Guid.NewGuid(), "VER-01 revocation admin", "VER-01 REVOCATION ADMIN", now);
        admin.SetGlobalRole(GlobalRole.Admin);
        var eventItem = Draft(Guid.NewGuid(), "ver01-auth-revocation", admin.Id);
        eventItem.ConfigureSchedule(now.AddHours(-3), now.AddHours(-2), null, now.AddHours(-1), now.AddHours(1), 20);
        eventItem.OpenSignups(now.AddHours(-3));
        eventItem.CloseSignups(now.AddHours(-2));
        eventItem.StartEvent(now.AddHours(-1));

        await using (var setup = new ApplicationDbContext(options))
        {
            setup.AddRange(admin, eventItem);
            await setup.SaveChangesAsync();
        }

        await using var blocker = new ApplicationDbContext(options);
        await using var blockerTransaction = await blocker.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM accounts WHERE id = {admin.Id} FOR UPDATE");

        await using var mutator = new ApplicationDbContext(options);
        await mutator.Database.OpenConnectionAsync();
        var mutatorProcessId = ((NpgsqlConnection)mutator.Database.GetDbConnection()).ProcessID;
        var pending = new EventLifecycleService(mutator, new NoSignupLifecycle(), new FixedClock(now))
            .EndNowAsync(eventItem.Id, eventItem.Version, true, "VER-01 revocation race", new LifecycleActor(admin.Id, admin.LoginName));

        var waiting = false;
        for (var attempt = 0; attempt < 200 && !waiting; attempt++)
        {
            await using var probe = new ApplicationDbContext(options);
            waiting = await probe.Database.SqlQueryRaw<bool>(
                "SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = {0} AND NOT granted) AS \"Value\"",
                mutatorProcessId).SingleAsync();
            if (!waiting) await Task.Delay(10);
        }

        if (!waiting)
        {
            await blockerTransaction.RollbackAsync();
            await pending;
            Assert.Fail("The lifecycle mutation did not reach the account-row lock boundary.");
        }

        var lockedAdmin = await blocker.Accounts.SingleAsync(account => account.Id == admin.Id);
        lockedAdmin.Disable(now, admin.Id, "VER-01 concurrent revocation");
        await blocker.SaveChangesAsync();
        await blockerTransaction.CommitAsync();

        var result = await pending;
        Assert.False(result.Succeeded);
        Assert.True(result.Error?.Contains("active website administrator", StringComparison.Ordinal) == true
            || result.Error?.Contains("transient", StringComparison.OrdinalIgnoreCase) == true,
            $"Unexpected revocation-race rejection: {result.Error}");
        await using (var retry = new ApplicationDbContext(options))
        {
            var retryResult = await new EventLifecycleService(retry, new NoSignupLifecycle(), new FixedClock(now))
                .EndNowAsync(eventItem.Id, eventItem.Version, true, "VER-01 revocation retry", new LifecycleActor(admin.Id, admin.LoginName));
            Assert.False(retryResult.Succeeded);
            Assert.Contains("active website administrator", retryResult.Error, StringComparison.Ordinal);
        }
        var snapshot = await SnapshotAsync(eventItem.Id);
        Assert.Equal(EventState.Live, snapshot.State);
        Assert.Equal(0, snapshot.AuditCount);
        Assert.Equal(0, snapshot.TransitionCount);
    }

    private async Task<EventSnapshot> SnapshotAsync(Guid eventId)
    {
        await using var db = new ApplicationDbContext(options);
        var item = await db.Events.SingleAsync(value => value.Id == eventId);
        return new(item.State, item.Version,
            await db.AuditEntries.CountAsync(value => value.EventId == eventId),
            await db.EventStateTransitions.CountAsync(value => value.EventId == eventId));
    }

    private async Task AssertSnapshotAsync(Guid eventId, EventSnapshot expected)
    {
        var actual = await SnapshotAsync(eventId);
        Assert.Equal(expected, actual);
    }

    private BingoEvent Draft(Guid id, string slug, Guid actorId) => new(id, slug, slug, "UTC", actorId, now);

    private BingoEvent AwaitingReview(Guid eventId, Guid actorId)
    {
        var item = Draft(eventId, "ver01-auth-publication", actorId);
        item.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddHours(-3), now.AddHours(-2), 20);
        item.OpenSignups(now.AddDays(-2));
        item.CloseSignups(now.AddDays(-1));
        item.StartEvent(now.AddHours(-3));
        item.EndEvent(now.AddHours(-2));
        return item;
    }

    private sealed record EventSnapshot(EventState State, long Version, int AuditCount, int TransitionCount);

    private sealed class NoReadiness : IEventReadinessEvaluator
    {
        public Task<SignupReadiness?> GetSignupReadinessAsync(Guid eventId, SignupOpeningMode mode, DateTimeOffset now, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SignupReadiness?> GetSignupReadinessAsync(Guid eventId, SignupOpeningMode mode, DateTimeOffset now, EventScheduleValues proposedValues, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NoSignupLifecycle : IEventSignupLifecycleService
    {
        public Task<SignupLifecycleResult> SaveScheduleAsync(Guid eventId, long version, EventScheduleValues values, bool confirmChanges, LifecycleActor actor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SignupLifecycleResult> SaveScheduleAsync(Guid eventId, long version, EventScheduleValues values, bool confirmChanges, LifecycleActor actor, string? reason, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SignupLifecycleResult> OpenAsync(Guid eventId, long version, IReadOnlyCollection<string> acknowledgedWarningCodes, bool acceptProposedClose, LifecycleActor actor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SignupLifecycleResult> CloseAsync(Guid eventId, long version, bool confirmed, LifecycleActor actor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<SignupLifecycleResult> ReopenAsync(Guid eventId, long version, IReadOnlyCollection<string> acknowledgedWarningCodes, bool acceptProposedClose, LifecycleActor actor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ProcessDueSignupAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class ReadyBoard(Guid teamId) : IPublicBoardService
    {
        public Task<PublicEventBoard?> GetEventBoardAsync(string eventSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult<PublicEventBoard?>(new PublicEventBoard(
                Guid.Empty, "VER-01 authorization publication", eventSlug, EventState.AwaitingFinalReview, 1, 1, 0,
                [new PublicTeamBoard(teamId, "Winners", "winners", null, null, 1, false,
                    new([], 0, [], [], false, null, 0, []), [])], [], []));

        public Task<PublicTileDetails?> GetTileAsync(string eventSlug, string teamSlug, Guid tileId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PublicTileDetails?>(null);
    }

    private sealed class FixedClock(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
