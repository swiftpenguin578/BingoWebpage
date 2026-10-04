using Bingo.Domain.Catalogue;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class ProductionCataloguePreflightIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_catalogue_preflight")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();

    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .Options;
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task ActiveCataloguePassesWithDatabaseNamesIndependentOfBundledSnapshot()
    {
        await ResetDatabaseAsync();
        await SeedCatalogueAsync(includeActivity: true, includeItem: true, includeDrop: true);

        await using var db = new ApplicationDbContext(options);
        await CreatePreflight(db).ValidateCatalogueAsync();
    }

    [Fact]
    public async Task MissingCatalogueCategoryFailsClosed()
    {
        foreach (var missing in new[] { "activity", "item", "drop" })
        {
            await ResetDatabaseAsync();
            await SeedCatalogueAsync(
                includeActivity: missing != "activity",
                includeItem: missing != "item",
                includeDrop: false);

            await using var db = new ApplicationDbContext(options);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePreflight(db).ValidateCatalogueAsync());
            Assert.Contains("restore the production database backup", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task InactiveOnlyCatalogueCategoryFailsClosed()
    {
        foreach (var inactive in new[] { "activity", "item", "drop" })
        {
            await ResetDatabaseAsync();
            var ids = await SeedCatalogueAsync(includeActivity: true, includeItem: true, includeDrop: true);
            await using (var deactivate = new ApplicationDbContext(options))
            {
                switch (inactive)
                {
                    case "activity":
                        (await deactivate.BossActivities.SingleAsync(value => value.Id == ids.ActivityId)).SetActive(false);
                        break;
                    case "item":
                        (await deactivate.CatalogueItems.SingleAsync(value => value.Id == ids.ItemId)).SetActive(false);
                        break;
                    default:
                        (await deactivate.SourceDrops.SingleAsync(value => value.Id == ids.DropId)).SetActive(false);
                        break;
                }

                await deactivate.SaveChangesAsync();
            }

            await using var db = new ApplicationDbContext(options);
            var exception = await Record.ExceptionAsync(() => CreatePreflight(db).ValidateCatalogueAsync());
            Assert.True(exception is InvalidOperationException, $"Expected inactive-only {inactive} catalogue to fail, got {exception?.GetType().Name ?? "no exception"}.");
            Assert.Contains("restore the production database backup", exception!.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    private async Task ResetDatabaseAsync()
    {
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("UPDATE boss_activities SET active = FALSE; UPDATE catalogue_items SET active = FALSE; UPDATE source_drops SET active = FALSE;");
    }

    private async Task<CatalogueIds> SeedCatalogueAsync(bool includeActivity, bool includeItem, bool includeDrop)
    {
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var activity = new BossActivity(Guid.NewGuid(), "Database-only renamed activity", "database-only-renamed-activity", "Database-only category", 2m, now);
        var item = new CatalogueItem(Guid.NewGuid(), "Database-only renamed item", "DATABASE-ONLY RENAMED ITEM");
        var drop = new SourceDrop(Guid.NewGuid(), activity.Id, item.Id, "1/17", 1m / 17m, 17m, now);
        await using var db = new ApplicationDbContext(options);
        if (includeActivity) db.BossActivities.Add(activity);
        if (includeItem) db.CatalogueItems.Add(item);
        if (includeDrop) db.SourceDrops.Add(drop);
        await db.SaveChangesAsync();
        return new CatalogueIds(activity.Id, item.Id, drop.Id);
    }

    private static ProductionPreflight CreatePreflight(ApplicationDbContext db) =>
        new(db, new ConfigurationBuilder().Build(), new ServiceCollection().BuildServiceProvider());

    private sealed record CatalogueIds(Guid ActivityId, Guid ItemId, Guid DropId);
}
