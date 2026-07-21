using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Catalogue;
using Bingo.Web.Pages.Admin.Events;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class PostgreSqlConnectivityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_tests")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();

    public Task InitializeAsync() => _database.StartAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [Fact]
    public async Task ContextCanCreateAndQueryTheFoundationSchema()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_database.GetConnectionString())
            .Options;

        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.SystemMetadata.Add(new SystemMetadata
        {
            Key = "foundation",
            Value = "ready",
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();

        var value = await context.SystemMetadata
            .Where(item => item.Key == "foundation")
            .Select(item => item.Value)
            .SingleAsync();

        Assert.Equal("ready", value);
    }

    [Fact]
    public async Task VersionedCatalogueSnapshotRestoresIntoFreshMigratedDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_database.GetConnectionString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync();

        var path = Path.Combine(AppContext.BaseDirectory, "data", "osrs-catalogue.json");
        var service = new CatalogueSnapshotService(context, TimeProvider.System);
        var result = await service.ApplyAsync(path);

        Assert.Equal(68, result.Bosses);
        Assert.Equal(311, result.Items);
        Assert.Equal(441, result.Drops);
        Assert.Equal(479, result.Variants);
        Assert.Equal(result.Bosses, await context.BossActivities.CountAsync());
        Assert.Equal(result.Items, await context.CatalogueItems.CountAsync());
        Assert.Equal(result.Drops, await context.SourceDrops.CountAsync());
        Assert.Equal(result.Variants, await context.SourceDropRateVariants.CountAsync());
        Assert.Equal(22, await context.SourceDrops.CountAsync(x => x.Active && EF.Functions.ILike(x.DisplayRate, "%+1 variant%")));
    }

    [Fact]
    public async Task BoardDropPickerQueryOrdersBeforeProjectingViewRecords()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_database.GetConnectionString()).Options;
        await using var context = new ApplicationDbContext(options); await context.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow; var boss = new BossActivity(Guid.NewGuid(), "Query Test Boss", $"query-test-{Guid.NewGuid():N}", "Boss", 10, now); var item = new CatalogueItem(Guid.NewGuid(), "Query Test Item", $"QUERY TEST ITEM {Guid.NewGuid():N}");
        var drop = new SourceDrop(Guid.NewGuid(), boss.Id, item.Id, "1/100", 0.01m, 10, now); context.BossActivities.Add(boss); context.CatalogueItems.Add(item); context.SourceDrops.Add(drop); await context.SaveChangesAsync();

        var results = await (from sourceDrop in context.SourceDrops.AsNoTracking()
                             join sourceBoss in context.BossActivities on sourceDrop.BossActivityId equals sourceBoss.Id
                             join sourceItem in context.CatalogueItems on sourceDrop.ItemId equals sourceItem.Id
                             where sourceDrop.Id == drop.Id
                             orderby sourceBoss.Name, sourceItem.Name
                             select new BoardModel.DropView(sourceDrop.Id, sourceBoss.Id, sourceBoss.Name, sourceItem.Name, sourceDrop.DisplayRate))
            .ToListAsync();

        var result = Assert.Single(results); Assert.Equal("Query Test Boss", result.BossName); Assert.Equal("Query Test Item", result.ItemName);
    }

    [Fact]
    public async Task UndoneDraftPickNumberCanBeReusedRepeatedly()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_database.GetConnectionString()).Options;
        await using var context = new ApplicationDbContext(options); await context.Database.EnsureCreatedAsync();
        var draftId = Guid.NewGuid(); var teamId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var pick = new DraftPick(Guid.NewGuid(), draftId, teamId, Guid.NewGuid(), 4, 2, now.AddMinutes(attempt));
            context.DraftPicks.Add(pick); await context.SaveChangesAsync();
            pick.Undo(now.AddMinutes(attempt).AddSeconds(1)); await context.SaveChangesAsync();
        }

        var replacement = new DraftPick(Guid.NewGuid(), draftId, teamId, Guid.NewGuid(), 4, 2, now.AddHours(1));
        context.DraftPicks.Add(replacement); await context.SaveChangesAsync();
        Assert.Equal(5, await context.DraftPicks.CountAsync(x => x.DraftSessionId == draftId && x.PickNumber == 4));
        Assert.Equal(1, await context.DraftPicks.CountAsync(x => x.DraftSessionId == draftId && x.PickNumber == 4 && x.UndoneAt == null));
    }

    [Fact]
    public async Task StaleBoardAggregateEditIsRejected()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_database.GetConnectionString()).Options;
        var boardId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Boards.Add(new Board(boardId, Guid.NewGuid(), "Concurrent board", 5, 5));
            await setup.SaveChangesAsync();
        }

        await using var firstContext = new ApplicationDbContext(options);
        await using var staleContext = new ApplicationDbContext(options);
        var first = await firstContext.Boards.SingleAsync(x => x.Id == boardId);
        var stale = await staleContext.Boards.SingleAsync(x => x.Id == boardId);
        first.MarkChanged();
        stale.MarkChanged();

        await firstContext.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleContext.SaveChangesAsync());
    }

    [Fact]
    public async Task SimultaneousDraftControlClaimsCannotBothWin()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_database.GetConnectionString()).Options;
        var draftId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.DraftSessions.Add(new DraftSession(draftId, Guid.NewGuid(), 14));
            await setup.SaveChangesAsync();
        }

        await using var firstContext = new ApplicationDbContext(options);
        await using var secondContext = new ApplicationDbContext(options);
        var first = await firstContext.DraftSessions.SingleAsync(x => x.Id == draftId);
        var second = await secondContext.DraftSessions.SingleAsync(x => x.Id == draftId);
        var now = DateTimeOffset.UtcNow;
        first.AcquireControl(Guid.NewGuid(), now, TimeSpan.FromMinutes(5));
        second.AcquireControl(Guid.NewGuid(), now, TimeSpan.FromMinutes(5));

        await firstContext.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task SimultaneousBoardEditingClaimsCannotBothWin()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_database.GetConnectionString()).Options;
        var boardId = Guid.NewGuid();
        await using (var setup = new ApplicationDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Boards.Add(new Board(boardId, Guid.NewGuid(), "Editing lease board", 5, 5));
            await setup.SaveChangesAsync();
        }

        await using var firstContext = new ApplicationDbContext(options);
        await using var secondContext = new ApplicationDbContext(options);
        var first = await firstContext.Boards.SingleAsync(x => x.Id == boardId);
        var second = await secondContext.Boards.SingleAsync(x => x.Id == boardId);
        var now = DateTimeOffset.UtcNow;
        first.AcquireEditing(Guid.NewGuid(), now, TimeSpan.FromMinutes(5));
        second.AcquireEditing(Guid.NewGuid(), now, TimeSpan.FromMinutes(5));

        await firstContext.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task OfficialResultSnapshotRemainsAfterItBecomesHistorical()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_database.GetConnectionString()).Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
        var eventId = Guid.NewGuid();
        var finalization = new EventFinalizationSnapshot(Guid.NewGuid(), eventId, 1, now, Guid.NewGuid());
        var placement = new OfficialPlacementSnapshot(
            Guid.NewGuid(), finalization.Id, eventId, Guid.NewGuid(), "Snapshot Team",
            1, true, now.AddHours(-1), 10, 25, 42.5m);
        context.EventFinalizations.Add(finalization);
        context.OfficialPlacements.Add(placement);
        await context.SaveChangesAsync();

        finalization.Unfinalize(now.AddMinutes(1), Guid.NewGuid(), "Correct the final completion time");
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var historical = await context.EventFinalizations.SingleAsync(x => x.Id == finalization.Id);
        var savedPlacement = await context.OfficialPlacements.SingleAsync(x => x.FinalizationId == finalization.Id);
        Assert.False(historical.Active);
        Assert.Equal("Correct the final completion time", historical.UnfinalizeReason);
        Assert.Equal("Snapshot Team", savedPlacement.TeamName);
        Assert.Equal(1, savedPlacement.Placement);
    }
}
