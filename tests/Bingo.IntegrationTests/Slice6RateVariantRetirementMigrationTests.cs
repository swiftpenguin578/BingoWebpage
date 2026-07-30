using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class Slice6RateVariantRetirementMigrationTests : IAsyncLifetime
{
    private const string PreCorrectionMigration = "20260729234011_AddSlice6ManagedBoardTileImages";
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_slice6_rate_variant_retirement")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();
    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.GetConnectionString()).Options;
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task RetiredVariantStorageDoesNotMergeOrDeleteRetainedSourceDrops()
    {
        var bossId = Guid.NewGuid();
        var nidItemId = Guid.NewGuid();
        var destroyItemId = Guid.NewGuid();
        var nidDropId = Guid.NewGuid();
        var destroyDropId = Guid.NewGuid();
        var legacyItemId = Guid.NewGuid();
        var legacyDropId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow.AddDays(-1);

        await using (var retained = new ApplicationDbContext(options))
        {
            await retained.GetService<IMigrator>().MigrateAsync(PreCorrectionMigration);
            await retained.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO boss_activities (id, name, slug, category, efficient_completions_per_hour, data_updated_at, active, version)
                VALUES ({bossId}, {"Araxxor"}, {"retained-araxxor"}, {"Boss"}, 10, {now}, TRUE, 1);
                INSERT INTO catalogue_items (id, name, normalized_name, active, version)
                VALUES ({nidItemId}, {"Nid"}, {"NID"}, TRUE, 1),
                       ({destroyItemId}, {"Nid (Destroy)"}, {"NID (DESTROY)"}, TRUE, 1);
                INSERT INTO catalogue_items (id, name, normalized_name, active, version)
                VALUES ({legacyItemId}, {"Legacy single rate"}, {"LEGACY SINGLE RATE"}, TRUE, 1);
                INSERT INTO source_drops (id, boss_activity_id, item_id, display_rate, numeric_probability, default_ehb_estimate, data_updated_at, active, version)
                VALUES ({nidDropId}, {bossId}, {nidItemId}, {"1/100"}, .01, 10, {now}, TRUE, 1),
                       ({destroyDropId}, {bossId}, {destroyItemId}, {"1/50"}, .02, 5, {now}, TRUE, 1),
                       ({legacyDropId}, {bossId}, {legacyItemId}, {"Not supplied"}, NULL, NULL, {now}, TRUE, 1);
                INSERT INTO source_drop_rate_variants (id, source_drop_id, position, label, display_rate, numeric_probability, active, version)
                VALUES ({variantId}, {nidDropId}, 1, {"Default"}, {"1/100"}, .01, TRUE, 1),
                       ({Guid.NewGuid()}, {legacyDropId}, 1, {"Default"}, {"1/25"}, .04, TRUE, 1);
                """);

            await retained.GetService<IMigrator>().MigrateAsync();
        }

        await using var migrated = new ApplicationDbContext(options);
        var drops = await migrated.SourceDrops.Where(drop => drop.BossActivityId == bossId).OrderBy(drop => drop.DisplayRate).ToListAsync();
        Assert.Contains(drops, drop => (drop.Id, drop.NumericProbability) == (nidDropId, .01m));
        Assert.Contains(drops, drop => (drop.Id, drop.NumericProbability) == (destroyDropId, .02m));
        Assert.Contains(drops, drop => (drop.Id, drop.NumericProbability) == (legacyDropId, .04m));
        Assert.False(await migrated.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = {"source_drop_rate_variants"}) AS \"Value\"").SingleAsync());
        Assert.False(await migrated.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = {"board_approval_rate_variant_snapshots"}) AS \"Value\"").SingleAsync());
    }

    [Fact]
    public async Task RetiredVariantSnapshotTransfersItsExactFrozenValuesToTheSurvivingApprovalDrop()
    {
        var approvalDropId = Guid.NewGuid();

        await using (var retained = new ApplicationDbContext(options))
        {
            await retained.GetService<IMigrator>().MigrateAsync(PreCorrectionMigration);
            await SeedApprovalVariantSnapshotAsync(retained, approvalDropId, [("1/37", .027027027027m, "Challenge mode")]);

            await retained.GetService<IMigrator>().MigrateAsync();
        }

        await using var migrated = new ApplicationDbContext(options);
        var preserved = await migrated.BoardApprovalRequirementDropSnapshots.SingleAsync(drop => drop.Id == approvalDropId);
        Assert.Equal("1/37", preserved.DisplayRate);
        Assert.Equal(.027027027027m, preserved.NumericProbability);
        Assert.Equal("Challenge mode", preserved.RateCondition);
    }

    [Fact]
    public async Task RetiredVariantSnapshotsWithMultipleFrozenRowsRemainSeparateFrozenDropSnapshots()
    {
        var approvalDropId = Guid.NewGuid();

        await using (var retained = new ApplicationDbContext(options))
        {
            await retained.GetService<IMigrator>().MigrateAsync(PreCorrectionMigration);
            await SeedApprovalVariantSnapshotAsync(
                retained,
                approvalDropId,
                [("1/50", .02m, "Normal"), ("1/25", .04m, "Challenge mode")]);

            await retained.GetService<IMigrator>().MigrateAsync();
        }

        await using var migrated = new ApplicationDbContext(options);
        var approvalRequirementId = await migrated.BoardApprovalRequirementDropSnapshots
            .Where(value => value.Id == approvalDropId)
            .Select(value => value.ApprovalRequirementSnapshotId)
            .SingleAsync();
        var preserved = await migrated.BoardApprovalRequirementDropSnapshots
            .Where(drop => drop.ApprovalRequirementSnapshotId == approvalRequirementId)
            .OrderBy(drop => drop.NumericProbability)
            .ToListAsync();

        Assert.Collection(
            preserved,
            drop =>
            {
                Assert.Equal("1/50", drop.DisplayRate);
                Assert.Equal(.02m, drop.NumericProbability);
                Assert.Equal("Normal", drop.RateCondition);
            },
            drop =>
            {
                Assert.Equal("1/25", drop.DisplayRate);
                Assert.Equal(.04m, drop.NumericProbability);
                Assert.Equal("Challenge mode", drop.RateCondition);
            });
        Assert.False(await migrated.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = {"board_approval_rate_variant_snapshots"}) AS \"Value\"").SingleAsync());
    }

    private static async Task SeedApprovalVariantSnapshotAsync(
        ApplicationDbContext database,
        Guid approvalDropId,
        IReadOnlyList<(string DisplayRate, decimal Probability, string Condition)> variants)
    {
        var boardId = Guid.NewGuid();
        var approvalId = Guid.NewGuid();
        var approvalTileId = Guid.NewGuid();
        var approvalRequirementId = Guid.NewGuid();
        var sourceDropId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow.AddDays(-1);

        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO boards (id, event_id, name, rows, columns, state, total_ehb_estimate, calculation_version, version)
            VALUES ({boardId}, {Guid.NewGuid()}, {"Approved board"}, 1, 1, {"Published"}, 1, 1, 1);
            INSERT INTO board_approval_snapshots (id, board_id, version, approved_at, name, rows, columns, total_ehb_estimate, calculation_version, board_version, lifecycle_state)
            VALUES ({approvalId}, {boardId}, 1, {now}, {"Approved board"}, 1, 1, 1, 1, 1, {"Published"});
            INSERT INTO board_approval_tile_snapshots (id, approval_snapshot_id, board_tile_id, tile_template_id, row_index, column_index, name, description, evidence_instructions, estimated_ehb)
            VALUES ({approvalTileId}, {approvalId}, {Guid.NewGuid()}, {Guid.NewGuid()}, 0, 0, {"Tile"}, {""}, {""}, 1);
            INSERT INTO board_approval_requirement_snapshots (id, approval_tile_snapshot_id, board_requirement_snapshot_id, position, target_contribution, duplicates_allowed, allow_higher_weightings, credited_weight, description, manual_objective)
            VALUES ({approvalRequirementId}, {approvalTileId}, {Guid.NewGuid()}, 0, 1, FALSE, FALSE, 1, {""}, FALSE);
            INSERT INTO board_approval_requirement_drop_snapshots
                (id, approval_requirement_snapshot_id, source_drop_id, boss_name, item_name, display_rate,
                 numeric_probability, probability_scope, conditional_on_parent, assumed_participants,
                 rolls_per_completion, roll_group, maximum_contribution, credited_weight, catalogue_version)
            VALUES ({approvalDropId}, {approvalRequirementId}, {sourceDropId}, {"Boss"}, {"Item"}, {"Unresolved source rate"},
                    NULL, {"Participant"}, FALSE, 1, 1, {"default"}, NULL, 1, 1);
            """);

        for (var position = 0; position < variants.Count; position++)
        {
            var (displayRate, probability, condition) = variants[position];
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO board_approval_rate_variant_snapshots
                    (id, approval_requirement_drop_snapshot_id, rate_variant_id, position, label, display_rate,
                     numeric_probability, condition, catalogue_version)
                VALUES ({Guid.NewGuid()}, {approvalDropId}, {Guid.NewGuid()}, {position},
                        {"Selected"}, {displayRate}, {probability}, {condition}, 1);
                """);
        }
    }
}
