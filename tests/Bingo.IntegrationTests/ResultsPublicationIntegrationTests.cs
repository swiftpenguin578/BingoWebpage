using Bingo.Application.Boards;
using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

/// <summary>
/// VER-01 proof that official publication is one PostgreSQL transaction.
/// The test intentionally exercises the real service and database rather than
/// asserting the state machine in isolation.
/// </summary>
public sealed class ResultsPublicationIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ver01_results_publication")
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
    public async Task PublicationArchivesAtomicallyAndIsFailureSafeAndIdempotentAcrossConcurrentRetries()
    {
        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var actor = new LifecycleActor(actorId, "ver01-publication-admin");
        var publicationAdmin = Account.CreateWebsite(actorId, "VER-01 Publication Admin", "VER-01 PUBLICATION ADMIN", now);
        publicationAdmin.SetGlobalRole(GlobalRole.Admin);
        var ordinaryUser = Account.CreateWebsite(Guid.NewGuid(), "VER-01 Publication User", "VER-01 PUBLICATION USER", now);
        var disabledAdmin = Account.CreateWebsite(Guid.NewGuid(), "VER-01 Disabled Admin", "VER-01 DISABLED ADMIN", now);
        disabledAdmin.SetGlobalRole(GlobalRole.Admin);
        disabledAdmin.Disable(now, publicationAdmin.Id, "VER-01 authorization proof");

        long expectedVersion;
        await using (var setup = new ApplicationDbContext(options))
        {
            var item = AwaitingReview(eventId, actorId);
            setup.AddRange(
                publicationAdmin,
                ordinaryUser,
                disabledAdmin,
                item,
                new Team(teamId, eventId, "Winners", "winners", TeamFormationType.Drafted, null, true, now));
            setup.EventStateTransitions.Add(new EventStateTransition(
                Guid.NewGuid(), eventId, EventState.Live, EventState.AwaitingFinalReview, actorId,
                now.AddHours(-2), "Event ended", effectiveAt: now.AddHours(-2)));
            await setup.SaveChangesAsync();
            expectedVersion = item.Version;
        }

        foreach (var unauthorizedActor in new[]
        {
            new LifecycleActor(Guid.NewGuid(), "ver01-publication-missing"),
            new LifecycleActor(ordinaryUser.Id, ordinaryUser.LoginName),
            new LifecycleActor(disabledAdmin.Id, disabledAdmin.LoginName)
        })
        {
            await using var authorizationDb = new ApplicationDbContext(options);
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new EventFinalizationService(authorizationDb, new ReadyBoard(teamId), new FixedClock(now))
                    .FinalizeAsync(eventId, unauthorizedActor, expectedVersion));
            Assert.Contains("active website administrator", failure.Message, StringComparison.Ordinal);
            await AssertAwaitingReviewWithoutPublicationAsync(eventId, expectedVersion);
        }

        // An audit write failure must roll back the event, snapshot, placement,
        // transition, and audit rows together.
        var failingOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options)
            .AddInterceptors(new ThrowOnResultsPublicationAudit())
            .Options;
        await using (var failing = new ApplicationDbContext(failingOptions))
        {
            var service = new EventFinalizationService(failing, new ReadyBoard(teamId), new FixedClock(now));
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.FinalizeAsync(eventId, actor, expectedVersion));
            Assert.Equal("Simulated results-publication audit failure.", failure.Message);
        }

        await AssertAwaitingReviewWithoutPublicationAsync(eventId, expectedVersion);

        // The advisory lock serializes the two real PostgreSQL transactions.
        // PostgreSQL's Serializable isolation may abort the loser with a
        // stale-session error; a fresh retry then observes the committed
        // Archived snapshot and returns through the idempotent path.
        var results = await Task.WhenAll(
            FinalizeInNewContextAsync(eventId, actor, teamId, expectedVersion),
            FinalizeInNewContextAsync(eventId, actor, teamId, expectedVersion));
        Assert.Equal(1, results.Count(result => result is null));
        var concurrencyFailure = Assert.Single(results, result => result is not null);
        Assert.Contains("changed in another session", concurrencyFailure!.Message, StringComparison.Ordinal);

        Assert.Null(await FinalizeInNewContextAsync(eventId, actor, teamId, expectedVersion));

        await using var verify = new ApplicationDbContext(options);
        var publishedItem = await verify.Events.SingleAsync(value => value.Id == eventId);
        Assert.Equal(EventState.Archived, publishedItem.State);
        Assert.True(publishedItem.ResultsPublished);
        Assert.Equal(publishedItem.FinalizedAt, publishedItem.ArchivedAt);
        Assert.Equal(expectedVersion + 1, publishedItem.Version);

        var finalization = Assert.Single(await verify.EventFinalizations.Where(value => value.EventId == eventId).ToListAsync());
        var placement = Assert.Single(await verify.OfficialPlacements.Where(value => value.EventId == eventId).ToListAsync());
        Assert.Equal(finalization.Id, placement.FinalizationId);
        Assert.Equal(teamId, placement.TeamId);
        Assert.Single(await verify.EventStateTransitions.Where(value => value.EventId == eventId && value.ToState == EventState.Archived).ToListAsync());
        var publicationAudit = Assert.Single(await verify.AuditEntries.Where(value => value.EventId == eventId && value.Action == "event.results_published").ToListAsync());
        Assert.Equal(publicationAdmin.LoginName, publicationAudit.ActorUsername);
        Assert.Empty(await verify.AuditEntries.Where(value => value.EventId == eventId && value.Action == "event.finalized").ToListAsync());

        var publishedVersion = publishedItem.Version;
        foreach (var unauthorizedActor in new[]
        {
            new LifecycleActor(Guid.NewGuid(), "ver01-unfinalize-missing"),
            new LifecycleActor(ordinaryUser.Id, ordinaryUser.LoginName),
            new LifecycleActor(disabledAdmin.Id, disabledAdmin.LoginName)
        })
        {
            await using var authorizationDb = new ApplicationDbContext(options);
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new EventFinalizationService(authorizationDb, new ReadyBoard(teamId), new FixedClock(now))
                    .UnfinalizeAsync(eventId, "unauthorized", true, unauthorizedActor, publishedVersion));
            Assert.Contains("active website administrator", failure.Message, StringComparison.Ordinal);
            await AssertArchivedWithoutUnfinalizationAsync(eventId, publishedVersion);
        }

        await using (var reopen = new ApplicationDbContext(options))
        {
            await new EventFinalizationService(reopen, new ReadyBoard(teamId), new FixedClock(now))
                .UnfinalizeAsync(eventId, "VER-01 authorization proof", true, actor, publishedVersion);
        }

        await using var reopened = new ApplicationDbContext(options);
        var reopenedItem = await reopened.Events.SingleAsync(value => value.Id == eventId);
        Assert.Equal(EventState.AwaitingFinalReview, reopenedItem.State);
        Assert.Equal(publishedVersion + 1, reopenedItem.Version);
        Assert.NotNull((await reopened.EventFinalizations.SingleAsync(value => value.EventId == eventId)).UnfinalizedAt);
        Assert.Single(await reopened.AuditEntries.Where(value => value.EventId == eventId && value.Action == "event.unfinalized").ToListAsync());
    }

    private async Task<Exception?> FinalizeInNewContextAsync(Guid eventId, LifecycleActor actor, Guid teamId, long expectedVersion)
    {
        await using var db = new ApplicationDbContext(options);
        try
        {
            await new EventFinalizationService(db, new ReadyBoard(teamId), new FixedClock(now))
                .FinalizeAsync(eventId, actor, expectedVersion);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private async Task AssertAwaitingReviewWithoutPublicationAsync(Guid eventId, long expectedVersion)
    {
        await using var verify = new ApplicationDbContext(options);
        var item = await verify.Events.SingleAsync(value => value.Id == eventId);
        Assert.Equal(EventState.AwaitingFinalReview, item.State);
        Assert.Equal(expectedVersion, item.Version);
        Assert.False(item.ResultsPublished);
        Assert.Empty(await verify.EventFinalizations.Where(value => value.EventId == eventId).ToListAsync());
        Assert.Empty(await verify.OfficialPlacements.Where(value => value.EventId == eventId).ToListAsync());
        Assert.Empty(await verify.EventStateTransitions.Where(value => value.EventId == eventId && value.ToState == EventState.Archived).ToListAsync());
        Assert.Empty(await verify.AuditEntries.Where(value => value.EventId == eventId && value.Action == "event.results_published").ToListAsync());
    }

    private async Task AssertArchivedWithoutUnfinalizationAsync(Guid eventId, long expectedVersion)
    {
        await using var verify = new ApplicationDbContext(options);
        var item = await verify.Events.SingleAsync(value => value.Id == eventId);
        Assert.Equal(EventState.Archived, item.State);
        Assert.Equal(expectedVersion, item.Version);
        Assert.Empty(await verify.AuditEntries.Where(value => value.EventId == eventId && value.Action == "event.unfinalized").ToListAsync());
        Assert.Null((await verify.EventFinalizations.SingleAsync(value => value.EventId == eventId)).UnfinalizedAt);
    }

    private BingoEvent AwaitingReview(Guid eventId, Guid actorId)
    {
        var item = new BingoEvent(eventId, "VER-01 publication", $"ver01-publication-{eventId:N}", "UTC", actorId, now.AddDays(-2));
        item.ConfigureSchedule(now.AddDays(-2), now.AddDays(-1), null, now.AddHours(-3), now.AddHours(-2), 20);
        item.OpenSignups(now.AddDays(-2));
        item.CloseSignups(now.AddDays(-1));
        item.StartEvent(now.AddHours(-3));
        item.EndEvent(now.AddHours(-2));
        return item;
    }

    private sealed class ReadyBoard(Guid teamId) : IPublicBoardService
    {
        public Task<PublicEventBoard?> GetEventBoardAsync(string eventSlug, CancellationToken cancellationToken = default) =>
            Task.FromResult<PublicEventBoard?>(new PublicEventBoard(
                Guid.Empty, "VER-01 publication", eventSlug, EventState.AwaitingFinalReview, 1, 1, 0,
                [new PublicTeamBoard(teamId, "Winners", "winners", null, null, 1, false,
                    new([], 0, [], [], false, null, 0, []), [])], [], []));

        public Task<PublicTileDetails?> GetTileAsync(string eventSlug, string teamSlug, Guid tileId, CancellationToken cancellationToken = default) =>
            Task.FromResult<PublicTileDetails?>(null);
    }

    private sealed class ThrowOnResultsPublicationAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            eventData.Context!.ChangeTracker.Entries<AuditEntry>().Any(entry =>
                entry.State == EntityState.Added && entry.Entity.Action == "event.results_published")
                ? ValueTask.FromException<InterceptionResult<int>>(new InvalidOperationException("Simulated results-publication audit failure."))
                : ValueTask.FromResult(result);
    }

    private sealed class FixedClock(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
