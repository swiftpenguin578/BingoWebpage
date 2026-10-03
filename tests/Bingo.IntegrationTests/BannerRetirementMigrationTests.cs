using Bingo.Domain.Access;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class BannerRetirementMigrationTests : IAsyncLifetime
{
    private const string BeforeRetirement = "20260926215725_AddBoardEstimateFreshness";
    private const string Retirement = "20260926233834_RetireEventBanners";
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_banner_retirement")
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
    public async Task EmptyDatabaseRetiresSchemaAndCanBeRehearsedDownAndUp()
    {
        await using (var db = new ApplicationDbContext(options))
            await db.Database.MigrateAsync();

        await using (var verify = new ApplicationDbContext(options))
        {
            Assert.Contains(Retirement, await verify.Database.GetAppliedMigrationsAsync());
            Assert.False(await RelationExistsAsync(verify, "event_banner_assets"));
            Assert.False(await RelationExistsAsync(verify, "event_banner_cleanups"));
            Assert.False(await RelationExistsAsync(verify, "event_banner_retirement_keys"));
            Assert.False(await ColumnExistsAsync(verify, "events", "banner_asset_id"));
            Assert.True(await RelationExistsAsync(verify, "evidence_assets"));
            Assert.True(await RelationExistsAsync(verify, "team_image_assets"));
            Assert.True(await RelationExistsAsync(verify, "board_tile_image_assets"));
            Assert.True(await RelationExistsAsync(verify, "catalogue_items"));
        }

        await using (var down = new ApplicationDbContext(options))
            await down.Database.GetService<IMigrator>().MigrateAsync(BeforeRetirement);

        await using (var restored = new ApplicationDbContext(options))
        {
            Assert.True(await RelationExistsAsync(restored, "event_banner_assets"));
            Assert.True(await RelationExistsAsync(restored, "event_banner_cleanups"));
            Assert.True(await ColumnExistsAsync(restored, "events", "banner_asset_id"));
            Assert.False(await RelationExistsAsync(restored, "event_banner_retirement_keys"));
        }

        await using (var upAgain = new ApplicationDbContext(options))
            await upAgain.Database.MigrateAsync();

        await using var final = new ApplicationDbContext(options);
        Assert.Contains(Retirement, await final.Database.GetAppliedMigrationsAsync());
        Assert.False(await RelationExistsAsync(final, "event_banner_assets"));
        Assert.False(await RelationExistsAsync(final, "event_banner_cleanups"));
        Assert.False(await ColumnExistsAsync(final, "events", "banner_asset_id"));
    }

    [Fact]
    public async Task ManuallyCleanedSingleAssetRetiresSchemaWithoutPendingLedger()
    {
        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero).AddTicks(1_234_560);
        var storageKey = $"{eventId:N}/banner/manual-cleanup.png";

        await using (var before = new ApplicationDbContext(options))
        {
            await before.Database.GetService<IMigrator>().MigrateAsync(BeforeRetirement);
            var actor = Account.CreateWebsite(actorId, "manual-banner-fixture", "MANUAL-BANNER-FIXTURE", now);
            var item = new BingoEvent(eventId, "Manual banner fixture", "manual-banner-fixture", "UTC", actorId, now);
            before.AddRange(actor, item);
            await before.SaveChangesAsync();

            await using var fixtureTransaction = await before.Database.BeginTransactionAsync();
            await before.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO event_banner_assets
                    (id, event_id, storage_key, original_filename, media_type, byte_size, width, height,
                     checksum, uploaded_by_account_id, uploaded_at, replaced_at)
                VALUES
                    ({assetId}, {eventId}, {storageKey}, {"manual-cleanup.png"}, {"image/png"}, 10, 1, 1,
                     {"manual-cleanup"}, {actorId}, {now}, NULL);

                UPDATE events
                SET banner_asset_id = {assetId}, version = version + 1
                WHERE id = {eventId};
                """);
            await fixtureTransaction.CommitAsync();
        }

        await using (var cleanup = new ApplicationDbContext(options))
        {
            await using var cleanupTransaction = await cleanup.Database.BeginTransactionAsync();
            Assert.Equal(1, await cleanup.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE events
                SET banner_asset_id = NULL, version = version + 1
                WHERE id = {eventId} AND banner_asset_id = {assetId};
                """));
            Assert.Equal(1, await cleanup.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM event_banner_assets
                WHERE id = {assetId};
                """));
            await cleanupTransaction.CommitAsync();

            Assert.Equal(0, await cleanup.Database.SqlQuery<long>($"SELECT COUNT(*)::bigint AS \"Value\" FROM event_banner_assets").SingleAsync());
            Assert.Equal(0, await cleanup.Database.SqlQuery<long>($"SELECT COUNT(*)::bigint AS \"Value\" FROM event_banner_cleanups").SingleAsync());
            Assert.Equal(0, await cleanup.Database.SqlQuery<long>($"SELECT COUNT(*)::bigint AS \"Value\" FROM events WHERE banner_asset_id IS NOT NULL").SingleAsync());
        }

        await using (var migrated = new ApplicationDbContext(options))
            await migrated.Database.MigrateAsync();

        await using var final = new ApplicationDbContext(options);
        Assert.Contains(Retirement, await final.Database.GetAppliedMigrationsAsync());
        Assert.False(await RelationExistsAsync(final, "event_banner_assets"));
        Assert.False(await RelationExistsAsync(final, "event_banner_cleanups"));
        Assert.False(await RelationExistsAsync(final, "event_banner_retirement_keys"));
        Assert.False(await ColumnExistsAsync(final, "events", "banner_asset_id"));
    }

    [Fact]
    public async Task PopulatedDatabaseKeepsExactKeysUntilFailedCleanupRecoversAndPreservesSharedObjects()
    {
        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);
        var replacedKey = $"{eventId:N}/banner/replaced.png";
        var currentKey = $"{eventId:N}/banner/current.png";
        var pendingKey = $"{eventId:N}/banner/pending.png";
        var sharedKey = $"{eventId:N}/banner/shared.png";
        var emptyKey = string.Empty;
        var whitespaceKey = "   ";
        var expectedKeys = new[] { replacedKey, currentKey, pendingKey, sharedKey, emptyKey, whitespaceKey };

        await using (var before = new ApplicationDbContext(options))
        {
            await before.Database.GetService<IMigrator>().MigrateAsync(BeforeRetirement);
            var actor = Account.CreateWebsite(actorId, "banner-fixture", "BANNER-FIXTURE", now);
            var item = new BingoEvent(eventId, "Banner fixture", "banner-fixture", "UTC", actorId, now);
            before.AddRange(actor, item);
            before.CatalogueItems.Add(new CatalogueItem(Guid.NewGuid(), "Shared catalogue object", "shared-catalogue-object"));
            await before.SaveChangesAsync();
            await before.Database.ExecuteSqlInterpolatedAsync($"UPDATE catalogue_items SET image_url = {sharedKey} WHERE normalized_name = {"shared-catalogue-object"};");

            await using var fixtureTransaction = await before.Database.BeginTransactionAsync();
            await before.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica;");
            await before.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO event_banner_assets
                    (id, event_id, storage_key, original_filename, media_type, byte_size, width, height,
                     checksum, uploaded_by_account_id, uploaded_at, replaced_at)
                VALUES
                    ({Guid.NewGuid()}, {eventId}, {replacedKey}, {"replaced.png"}, {"image/png"}, 10, 1, 1, {"replaced"}, {actorId}, {now.AddHours(-2)}, {now.AddHours(-1)}),
                    ({Guid.NewGuid()}, {eventId}, {currentKey}, {"current.png"}, {"image/png"}, 11, 1, 1, {"current"}, {actorId}, {now.AddMinutes(-30)}, NULL),
                    ({Guid.NewGuid()}, {eventId}, {sharedKey}, {"shared.png"}, {"image/png"}, 12, 1, 1, {"shared"}, {actorId}, {now.AddMinutes(-20)}, NULL),
                    ({Guid.NewGuid()}, {eventId}, {emptyKey}, {"empty.png"}, {"image/png"}, 13, 1, 1, {"empty"}, {actorId}, {now.AddMinutes(-15)}, NULL),
                    ({Guid.NewGuid()}, {eventId}, {whitespaceKey}, {"whitespace.png"}, {"image/png"}, 14, 1, 1, {"whitespace"}, {actorId}, {now.AddMinutes(-14)}, NULL);

                INSERT INTO event_banner_cleanups
                    (id, event_id, storage_key, queued_at, last_attempted_at, last_failure, attempt_count)
                VALUES
                    ({Guid.NewGuid()}, {eventId}, {pendingKey}, {now.AddMinutes(-10)}, NULL, NULL, 0);

                -- These rows intentionally reuse the shared banner key. They
                -- are evidence/team/tile references and must be retained by
                -- the operator as shared-retained, never deleted as banners.
                INSERT INTO evidence_assets
                    (id, submission_id, storage_key, original_filename, media_type, byte_size,
                     pixel_width, pixel_height, checksum, uploaded_at, uploaded_by_account_id, role, active)
                VALUES
                    ({Guid.NewGuid()}, {Guid.NewGuid()}, {sharedKey}, {"shared-proof.png"}, {"image/png"}, 12,
                     1, 1, {"shared"}, {now}, {actorId}, {"OriginalEvidence"}, TRUE);

                INSERT INTO team_image_assets
                    (id, event_id, team_id, storage_key, original_filename, media_type, byte_size,
                     width, height, checksum, uploaded_by_account_id, uploaded_at, replaced_at)
                VALUES
                    ({Guid.NewGuid()}, {eventId}, {Guid.NewGuid()}, {sharedKey}, {"shared-team.png"}, {"image/png"}, 12,
                     1, 1, {"shared"}, {actorId}, {now}, NULL);

                INSERT INTO board_tile_image_assets
                    (id, event_id, board_tile_id, storage_key, original_filename, media_type, byte_size,
                     width, height, checksum, uploaded_by_account_id, uploaded_at, replaced_at)
                VALUES
                    ({Guid.NewGuid()}, {eventId}, {Guid.NewGuid()}, {sharedKey}, {"shared-tile.png"}, {"image/png"}, 12,
                     1, 1, {"shared"}, {actorId}, {now}, NULL);
                """);
            await fixtureTransaction.CommitAsync();
        }

        await using (var firstAttempt = new ApplicationDbContext(options))
            await Assert.ThrowsAnyAsync<Exception>(() => firstAttempt.Database.MigrateAsync());

        await using (var pending = new ApplicationDbContext(options))
        {
            Assert.DoesNotContain(Retirement, await pending.Database.GetAppliedMigrationsAsync());
            Assert.True(await RelationExistsAsync(pending, "event_banner_assets"));
            Assert.True(await RelationExistsAsync(pending, "event_banner_cleanups"));
            Assert.True(await ColumnExistsAsync(pending, "events", "banner_asset_id"));
            Assert.Equal(expectedKeys.Order(), await LedgerKeysAsync(pending));
            foreach (var key in expectedKeys)
                Assert.Equal("pending", await LedgerStatusAsync(pending, key));

            // A failed object-store attempt is recoverable in the ledger. The
            // migration must not rediscover or erase this exact reference.
            await pending.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE event_banner_retirement_keys
                SET attempt_count = 1,
                    last_attempted_at = {now},
                    last_failure = {"controlled object-store failure"}
                WHERE storage_key = {pendingKey};
                """);
        }

        await using (var retry = new ApplicationDbContext(options))
            await Assert.ThrowsAnyAsync<Exception>(() => retry.Database.MigrateAsync());

        await using (var failed = new ApplicationDbContext(options))
        {
            Assert.Equal("pending", await LedgerStatusAsync(failed, pendingKey));
            Assert.Equal(1, await LedgerAttemptCountAsync(failed, pendingKey));
            Assert.Equal("controlled object-store failure", await LedgerFailureAsync(failed, pendingKey));
        }

        var completedAt = now.AddMinutes(5);
        await using (var partiallyRecovered = new ApplicationDbContext(options))
        {
            await MarkLedgerAsync(partiallyRecovered, replacedKey, "missing", completedAt);
            await MarkLedgerAsync(partiallyRecovered, currentKey, "deleted", completedAt);
            await MarkLedgerAsync(partiallyRecovered, pendingKey, "deleted", completedAt);
            // The shared object is present in evidence, team, tile and
            // catalogue references, so the safe outcome is retained.
            await MarkLedgerAsync(partiallyRecovered, sharedKey, "shared-retained", completedAt);
        }

        // Exact empty and whitespace keys remain pending, so schema removal
        // must still be blocked until those values receive a terminal
        // cleanup outcome too.
        await using (var blockedByUnresolvedExactKeys = new ApplicationDbContext(options))
            await Assert.ThrowsAnyAsync<Exception>(() => blockedByUnresolvedExactKeys.Database.MigrateAsync());

        await using (var recovered = new ApplicationDbContext(options))
        {
            await MarkLedgerAsync(recovered, emptyKey, "missing", completedAt);
            await MarkLedgerAsync(recovered, whitespaceKey, "shared-retained", completedAt);
            await recovered.Database.MigrateAsync();
        }

        await using (var final = new ApplicationDbContext(options))
        {
            Assert.Contains(Retirement, await final.Database.GetAppliedMigrationsAsync());
            Assert.False(await RelationExistsAsync(final, "event_banner_assets"));
            Assert.False(await RelationExistsAsync(final, "event_banner_cleanups"));
            Assert.False(await RelationExistsAsync(final, "event_banner_retirement_keys"));
            Assert.False(await ColumnExistsAsync(final, "events", "banner_asset_id"));

            Assert.Equal(1, await final.Database.SqlQuery<long>($"SELECT COUNT(*)::bigint AS \"Value\" FROM evidence_assets WHERE storage_key = {sharedKey}").SingleAsync());
            Assert.Equal(1, await final.Database.SqlQuery<long>($"SELECT COUNT(*)::bigint AS \"Value\" FROM team_image_assets WHERE storage_key = {sharedKey}").SingleAsync());
            Assert.Equal(1, await final.Database.SqlQuery<long>($"SELECT COUNT(*)::bigint AS \"Value\" FROM board_tile_image_assets WHERE storage_key = {sharedKey}").SingleAsync());
            Assert.Equal(1, await final.Database.SqlQuery<long>($"SELECT COUNT(*)::bigint AS \"Value\" FROM catalogue_items WHERE image_url = {sharedKey}").SingleAsync());
        }
    }

    private static async Task<bool> RelationExistsAsync(ApplicationDbContext db, string table) =>
        await db.Database.SqlQuery<bool>($"SELECT to_regclass({"public." + table}) IS NOT NULL AS \"Value\"").SingleAsync();

    private static async Task<bool> ColumnExistsAsync(ApplicationDbContext db, string table, string column) =>
        await db.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = {table} AND column_name = {column}) AS \"Value\"").SingleAsync();

    private static Task<List<string>> LedgerKeysAsync(ApplicationDbContext db) =>
        db.Database.SqlQueryRaw<string>("SELECT storage_key AS \"Value\" FROM event_banner_retirement_keys ORDER BY storage_key").ToListAsync();

    private static Task<string> LedgerStatusAsync(ApplicationDbContext db, string key) =>
        db.Database.SqlQuery<string>($"SELECT cleanup_status AS \"Value\" FROM event_banner_retirement_keys WHERE storage_key = {key}").SingleAsync();

    private static Task<int> LedgerAttemptCountAsync(ApplicationDbContext db, string key) =>
        db.Database.SqlQuery<int>($"SELECT attempt_count AS \"Value\" FROM event_banner_retirement_keys WHERE storage_key = {key}").SingleAsync();

    private static Task<string?> LedgerFailureAsync(ApplicationDbContext db, string key) =>
        db.Database.SqlQuery<string?>($"SELECT last_failure AS \"Value\" FROM event_banner_retirement_keys WHERE storage_key = {key}").SingleAsync();

    private static Task<int> MarkLedgerAsync(ApplicationDbContext db, string key, string status, DateTimeOffset completedAt) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE event_banner_retirement_keys
            SET cleanup_status = {status}, completed_at = {completedAt}, last_failure = NULL
            WHERE storage_key = {key};
            """);
}
