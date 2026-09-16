using System.Data;
using System.Text.Json;
using Bingo.Application.Catalogue;
using Bingo.Application.Events;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Bingo.IntegrationTests;

public sealed partial class Slice3ScheduledLifecycleIntegrationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task StatsPass2StartUsesActualHourAndCommitsAllUsableCataloguePrices(bool scheduled, bool crossesHour)
    {
        var clock = new MutableTimeProvider(now.AddMinutes(59));
        var (eventId, items) = await StartPriceFixtureAsync(scheduled);
        var requested = Bingo.Domain.Catalogue.CataloguePricing.LastCompletedHour(clock.GetUtcNow());
        await using var db = new ApplicationDbContext(options);
        var api = new StartPriceApi(hour =>
        {
            Assert.Null(db.Database.CurrentTransaction);
            Assert.Equal(requested, hour);
            if (crossesHour) clock.Set(now.AddHours(1));
            return Task.FromResult<ApiHourlyPrices?>(new(hour, new Dictionary<int, long?> { [1] = 15, [2] = 0, [3] = null, [5] = 500 }));
        });
        var service = PriceLifecycle(db, clock, api);
        if (scheduled) await service.ProcessDueAsync();
        else Assert.True((await service.StartNowAsync(eventId, await VersionAsync(eventId), true, "Early start", new(Guid.NewGuid(), "admin"))).Succeeded);
        await using var verify = new ApplicationDbContext(options);
        var saved = await verify.Events.SingleAsync(x => x.Id == eventId);
        var prices = await verify.EventItemPrices.Where(x => x.EventId == eventId).ToDictionaryAsync(x => x.ItemId);
        Assert.Equal(clock.GetUtcNow(), saved.ActualStartedAt);
        Assert.Equal(saved.ActualStartedAt, saved.ItemPricesCapturedAt);
        Assert.Equal(crossesHour ? 4 : 5, prices.Count);
        Assert.All(prices.Values, x => Assert.Equal(CataloguePricing.LastCompletedHour(saved.ActualStartedAt!.Value), x.SelectedHour));
        Assert.Equal(crossesHour ? 10 : 15, prices[items[0].Id].ValueGp);
        Assert.Equal(crossesHour ? EventPriceFallbackReason.PreparedHourChanged : null, prices[items[0].Id].FallbackReason);
        Assert.Equal(0, prices[items[1].Id].ValueGp);
        Assert.Equal(30, prices[items[2].Id].ValueGp);
        Assert.Equal(0, prices[items[3].Id].ValueGp); // inactive, unmapped zero is still a usable catalogue identity
        Assert.False(prices.ContainsKey(items[5].Id)); // unused and unavailable stays absent, never zero
        Assert.Single(await verify.EventStateTransitions.Where(x => x.EventId == eventId && x.ToState == EventState.Live).ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == eventId && (x.Action == "event.started" || x.Action == "event.started_automatically")).ToListAsync());
        if (scheduled) Assert.True((await verify.ScheduledEventStartAttempts.SingleAsync(x => x.EventId == eventId)).Started);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatsPass2ProviderOutageDoesNotBlockManualOrScheduledStart(bool scheduled)
    {
        var (eventId, items) = await StartPriceFixtureAsync(scheduled);
        await using var db = new ApplicationDbContext(options);
        var clock = new MutableTimeProvider(now);
        var service = PriceLifecycle(db, clock, new StartPriceApi(_ => Task.FromResult<ApiHourlyPrices?>(null)));
        if (scheduled) await service.ProcessDueAsync();
        else Assert.True((await service.StartNowAsync(eventId, await VersionAsync(eventId), true, "Early start", new(Guid.NewGuid(), "admin"))).Succeeded);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(EventState.Live, (await verify.Events.SingleAsync(x => x.Id == eventId)).State);
        Assert.Equal(4, await verify.EventItemPrices.CountAsync(x => x.EventId == eventId));
        var price = await verify.EventItemPrices.SingleAsync(x => x.EventId == eventId && x.ItemId == items[0].Id);
        Assert.Equal(10, price.ValueGp); Assert.Equal(EventPriceFallbackReason.ProviderUnavailable, price.FallbackReason);
        Assert.Equal(now.AddDays(-1), price.PriceObservedAt);
    }

    [Fact]
    public async Task StatsPass2ConcurrentAndRepeatedStartsHaveOnePriceSetAndOneTransition()
    {
        var (eventId, _) = await StartPriceFixtureAsync(false);
        var version = await VersionAsync(eventId);
        var entered = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new StartPriceApi(async hour =>
        {
            if (Interlocked.Increment(ref entered) == 2) release.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return new(hour, new Dictionary<int, long?> { [1] = 15 });
        });
        async Task<EventStartResult> Start()
        {
            await using var db = new ApplicationDbContext(options);
            return await PriceLifecycle(db, new MutableTimeProvider(now), api).StartNowAsync(eventId, version, true, "Race", new(Guid.NewGuid(), "admin"));
        }
        var results = await Task.WhenAll(Start(), Start());
        Assert.Single(results, x => x.Succeeded);
        Assert.False((await Start()).Succeeded);
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal(4, await verify.EventItemPrices.CountAsync(x => x.EventId == eventId));
        Assert.Single(await verify.EventStateTransitions.Where(x => x.EventId == eventId && x.ToState == EventState.Live).ToListAsync());
        Assert.Single(await verify.AuditEntries.Where(x => x.EventId == eventId && x.Action == "event.started").ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatsPass2SnapshotFailureRollsBackStartAndRetryCanCommit(bool scheduled)
    {
        var (eventId, _) = await StartPriceFixtureAsync(scheduled);
        var failingOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new FailFirstPriceSave()).Options;
        await using var db = new ApplicationDbContext(failingOptions);
        var service = PriceLifecycle(db, new MutableTimeProvider(now), new StartPriceApi(_ => Task.FromResult<ApiHourlyPrices?>(null)));
        var version = await VersionAsync(eventId);
        if (scheduled) await service.ProcessDueAsync();
        else Assert.False((await service.StartNowAsync(eventId, version, true, "Rollback", new(Guid.NewGuid(), "admin"))).Succeeded);
        await using (var verify = new ApplicationDbContext(options))
        {
            var e = await verify.Events.SingleAsync(x => x.Id == eventId);
            Assert.Equal(EventState.SignupClosed, e.State); Assert.Null(e.ActualStartedAt); Assert.Null(e.ItemPricesCapturedAt);
            Assert.Empty(await verify.EventItemPrices.ToListAsync());
            Assert.Empty(await verify.EventStateTransitions.Where(x => x.EventId == eventId).ToListAsync());
            Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == eventId).ToListAsync());
            Assert.Empty(await verify.ScheduledEventStartAttempts.Where(x => x.EventId == eventId).ToListAsync());
            Assert.Empty(await verify.EventParticipantCharacterSwaps.Where(x => x.EventId == eventId).ToListAsync());
        }
        if (scheduled) await service.ProcessDueAsync();
        else Assert.True((await service.StartNowAsync(eventId, version, true, "Retry", new(Guid.NewGuid(), "admin"))).Succeeded);
        await using var final = new ApplicationDbContext(options);
        Assert.Equal(EventState.Live, (await final.Events.SingleAsync(x => x.Id == eventId)).State);
        Assert.Equal(4, await final.EventItemPrices.CountAsync());
    }

    [Fact]
    public async Task StatsPass2CatalogueEditsAndResumePreserveEveryFrozenField()
    {
        var (eventId, items) = await StartPriceFixtureAsync(false);
        var clock = new MutableTimeProvider(now);
        await using var db = new ApplicationDbContext(options);
        var service = PriceLifecycle(db, clock, new StartPriceApi(_ => Task.FromResult<ApiHourlyPrices?>(null)));
        var actor = new LifecycleActor(Guid.NewGuid(), "admin");
        Assert.True((await service.StartNowAsync(eventId, await VersionAsync(eventId), true, "Early", actor)).Succeeded);
        var before = JsonSerializer.Serialize(await db.EventItemPrices.AsNoTracking().OrderBy(x => x.ItemId).ToListAsync());
        await using (var edit = new ApplicationDbContext(options))
        {
            var item = await edit.CatalogueItems.SingleAsync(x => x.Id == items[0].Id);
            item.SetPrice(999, CataloguePriceSource.Manual, now.AddDays(1)); item.ConfigureApi("88");
            await edit.SaveChangesAsync();
        }
        Assert.True((await service.EndNowAsync(eventId, await VersionAsync(eventId), true, "Premature", actor)).Succeeded);
        clock.Set(now.AddHours(1));
        Assert.True((await service.ResumePrematureEndAsync(eventId, await VersionAsync(eventId), true, "Resume", null, actor)).Succeeded);
        Assert.Equal(before, JsonSerializer.Serialize(await db.EventItemPrices.AsNoTracking().OrderBy(x => x.ItemId).ToListAsync()));
    }

    [Fact]
    public async Task StatsPass2AdditiveMigrationDoesNotInventHistoricalPricesAndEnforcesImmutableRows()
    {
        await using var db = new ApplicationDbContext(options);
        var archived = BingoEvent.CreateArchivedHistorical(Guid.NewGuid(), "Excluded reconstructed fixture", "excluded-price-fixture", null, "UTC", null, null,
            now.AddDays(-7), now.AddDays(-1), Guid.NewGuid(), now, null, 1, 1, 1, 1);
        var item = new CatalogueItem(Guid.NewGuid(), "Retained price", "RETAINED PRICE"); item.SetPrice(0, CataloguePriceSource.Manual, now);
        db.AddRange(archived, item); await db.SaveChangesAsync();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260915142649_AddCatalogueApiMappingAndPrices");
        await RetainedCatalogueMigrationTestSupport.PrepareAsync(db);
        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();
        Assert.Empty(await db.EventItemPrices.ToListAsync());
        var historical = await db.Events.SingleAsync(x => x.Id == archived.Id);
        Assert.Null(historical.ItemPricesCapturedAt); Assert.Equal(EventState.Archived, historical.State);
        await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable))
        {
            await new EventItemPriceService(db, new MutableTimeProvider(now)).IntroduceAsync(historical, [item.Id], default);
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }
        Assert.Empty(await db.EventItemPrices.ToListAsync());
        Assert.Equal(0, (await db.CatalogueItems.SingleAsync(x => x.Id == item.Id)).CatalogueValueGp);
        // Use a controlled supported event for insert/constraint checks, not the excluded event.
        var e = new BingoEvent(Guid.NewGuid(), "Constraint fixture", "constraint-fixture", "UTC", Guid.NewGuid(), now);
        db.Add(e); await db.SaveChangesAsync();
        var price = EventItemPrice.Introduce(e.Id, item, now.AddHours(-1), now);
        db.Add(price); await db.SaveChangesAsync();
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO event_item_prices SELECT * FROM event_item_prices WHERE event_id = {e.Id}"));
        Assert.Equal("23505", duplicate.SqlState);
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE event_item_prices SET value_gp = 100 WHERE event_id = {e.Id}"));
        Assert.Equal("23514", immutable.SqlState);
        db.Entry(price).Property(x => x.ValueGp).CurrentValue = 100;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Equal(0, (await db.EventItemPrices.SingleAsync()).ValueGp);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatsPass2RejectedStartCandidateUsesTrustedFallbackAndFlagsRollbackAtomically(bool scheduled)
    {
        var (eventId, items) = await StartPriceFixtureAsync(scheduled);
        var failOptions = new DbContextOptionsBuilder<ApplicationDbContext>(options).AddInterceptors(new FailFirstPriceSave()).Options;
        await using var db = new ApplicationDbContext(failOptions);
        var service = PriceLifecycle(db, new MutableTimeProvider(now), new StartPriceApi(hour =>
            Task.FromResult<ApiHourlyPrices?>(new(hour, new Dictionary<int, long?> { [1] = 21, [2] = 1 }))));
        if (scheduled) await service.ProcessDueAsync();
        else Assert.False((await service.StartNowAsync(eventId, await VersionAsync(eventId), true, "Reject rollback", new(Guid.NewGuid(), "admin"))).Succeeded);
        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Null((await verify.CatalogueItems.SingleAsync(x => x.Id == items[0].Id)).RejectedPriceGp);
            Assert.Empty(await verify.AuditEntries.Where(x => x.EventId == eventId).ToListAsync());
        }
        if (scheduled) await service.ProcessDueAsync();
        else Assert.True((await service.StartNowAsync(eventId, await VersionAsync(eventId), true, "Reject retry", new(Guid.NewGuid(), "admin"))).Succeeded);
        await using (var verify = new ApplicationDbContext(options))
        {
            var snapshot = await verify.EventItemPrices.SingleAsync(x => x.EventId == eventId && x.ItemId == items[0].Id);
            Assert.Equal(10, snapshot.ValueGp); Assert.Equal(EventPriceFallbackReason.PriceMoveRejected, snapshot.FallbackReason);
            var catalogue = await verify.CatalogueItems.SingleAsync(x => x.Id == items[0].Id);
            Assert.Equal(10, catalogue.CatalogueValueGp); Assert.Equal(CataloguePriceSource.Manual, catalogue.PriceSource); Assert.Equal(21, catalogue.RejectedPriceGp);
            Assert.Equal(0, (await verify.EventItemPrices.SingleAsync(x => x.EventId == eventId && x.ItemId == items[1].Id)).ValueGp);
            Assert.Equal(2, await verify.AuditEntries.CountAsync(x => x.EventId == eventId && x.Action == "catalogue.event_start_price_checked"));
        }
    }

    private async Task<(Guid EventId, CatalogueItem[] Items)> StartPriceFixtureAsync(bool scheduled)
    {
        var eventId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(options);
        var e = ReadyDraft(db, eventId, $"start-price-{eventId:N}", now.AddDays(-2), scheduled ? now.AddHours(-3) : now.AddHours(2), now.AddDays(2));
        e.OpenSignups(now.AddDays(-2)); e.CloseSignups(now.AddDays(-1));
        db.Add(e); await db.SaveChangesAsync(); await AddReadyBoardAndDraftAsync(db, eventId);
        var items = Enumerable.Range(1, 6).Select(i => new CatalogueItem(Guid.NewGuid(), $"Price {i}", $"PRICE {i}")).ToArray();
        for (var i = 0; i < items.Length; i++)
        {
            if (i != 3) items[i].ConfigureApi((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (i < 4) items[i].SetPrice(i is 1 or 3 ? 0 : (i + 1) * 10, CataloguePriceSource.Manual, now.AddDays(-1));
        }
        items[3].SetActive(false);
        db.AddRange(items); await db.SaveChangesAsync();
        return (eventId, items);
    }

    private EventLifecycleService PriceLifecycle(ApplicationDbContext db, TimeProvider clock, ICatalogueApiClient api) =>
        new(db, new EventSignupLifecycleService(db, new EventReadinessEvaluator(db, configuration), clock), clock, new EventItemPriceService(db, clock, api));

    private sealed class StartPriceApi(Func<DateTimeOffset, Task<ApiHourlyPrices?>> response) : ICatalogueApiClient
    {
        public Task<CatalogueApiResult<IReadOnlyList<ApiItem>>> GetItemsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<CatalogueApiResult<IReadOnlySet<string>>> GetBossMetricsAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<CatalogueApiResult<ApiHourlyPrices>> GetHourlyPricesAsync(CancellationToken ct) => throw new NotSupportedException();
        public async Task<CatalogueApiResult<ApiHourlyPrices>> GetHourlyPricesAsync(DateTimeOffset hour, CancellationToken ct) => new(await response(hour));
    }

    private sealed class FailFirstPriceSave : SaveChangesInterceptor
    {
        private bool failed;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!failed && eventData.Context!.ChangeTracker.Entries<EventItemPrice>().Any(x => x.State == EntityState.Added))
            { failed = true; throw new DbUpdateException("Controlled snapshot write failure"); }
            return ValueTask.FromResult(result);
        }
    }
}
