using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice3ReviewRemediationMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("slice3_review_migration").WithUsername("bingo").WithPassword("bingo_test_password").Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task CleanupOutboxMigrationAppliesToCleanPostgreSql()
    {
        await using var db = new ApplicationDbContext(options);
        await db.Database.MigrateAsync();
        Assert.Contains("20260727192755_AddEventBannerCleanupOutbox", await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.EventBannerCleanups.ToListAsync());
    }

    [Fact]
    public async Task CleanupOutboxMigrationPreservesRepresentativeRetainedEvent()
    {
        var eventId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 7, 27, 19, 0, 0, TimeSpan.Zero);
        await using (var retained = new ApplicationDbContext(options))
        {
            await retained.Database.MigrateAsync("20260727134959_AddScheduledLifecycleExecution");
            await retained.Database.ExecuteSqlRawAsync("ALTER TABLE events ALTER COLUMN allow_private_signup_editing SET DEFAULT FALSE;");
            await retained.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO events (id, name, slug, description, timezone, state, signup_opens_at, signup_closes_at, event_starts_at, event_ends_at, submission_cutoff_at, participant_cap, waiting_list_enabled, allow_private_signup_editing, require_signup_code, participant_list_published, draft_results_published, team_rosters_published, board_published, results_published, draft_locked, created_by_account_id, created_at)
                VALUES ({eventId}, {"retained-review-event"}, {"retained-review-event"}, {""}, {"UTC"}, {"Draft"}, {createdAt}, {createdAt}, {createdAt}, {createdAt.AddDays(1)}, {createdAt.AddDays(1)}, {19}, {true}, {false}, {false}, {false}, {false}, {false}, {false}, {false}, {false}, {Guid.NewGuid()}, {createdAt});
                """);
            await retained.Database.MigrateAsync();
        }
        await using var verify = new ApplicationDbContext(options);
        Assert.Equal("retained-review-event", (await verify.Events.SingleAsync(value => value.Id == eventId)).Slug);
        Assert.Empty(await verify.EventBannerCleanups.ToListAsync());
    }
}
