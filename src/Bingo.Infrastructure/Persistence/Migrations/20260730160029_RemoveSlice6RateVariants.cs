using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RemoveSlice6RateVariants : Migration
{
    private static readonly string[] RateVariantSnapshotIndexColumns = ["approval_requirement_drop_snapshot_id", "rate_variant_id"];
    private static readonly string[] SourceDropRateVariantIndexColumns = ["source_drop_id", "position"];
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Approval snapshots are immutable public/history records. A legacy rate
        // variant is therefore retained as its own frozen drop snapshot. The first
        // variant reuses the existing snapshot row and every later variant gets a
        // deterministic sibling row. New approval snapshots still write one row per
        // source drop; this non-unique historical index exists solely for retained
        // multi-rate approval history.
        migrationBuilder.DropIndex(
            name: "IX_board_approval_requirement_drop_snapshots_approval_requirem~",
            table: "board_approval_requirement_drop_snapshots");

        migrationBuilder.CreateIndex(
            name: "IX_board_approval_requirement_drop_snapshots_approval_requirem~",
            table: "board_approval_requirement_drop_snapshots",
            columns: ["approval_requirement_snapshot_id", "source_drop_id"]);

        migrationBuilder.Sql("""
            WITH ranked_variants AS (
                SELECT variant.*,
                       row_number() OVER (
                           PARTITION BY variant.approval_requirement_drop_snapshot_id
                           ORDER BY variant.position, variant.id) AS ordinal
                FROM board_approval_rate_variant_snapshots variant
            )
            UPDATE board_approval_requirement_drop_snapshots drop_snapshot
            SET display_rate = variant.display_rate,
                numeric_probability = variant.numeric_probability,
                rate_condition = variant.condition
            FROM ranked_variants variant
            WHERE variant.approval_requirement_drop_snapshot_id = drop_snapshot.id
              AND variant.ordinal = 1;
            """);

        migrationBuilder.Sql("""
            WITH ranked_variants AS (
                SELECT variant.*,
                       row_number() OVER (
                           PARTITION BY variant.approval_requirement_drop_snapshot_id
                           ORDER BY variant.position, variant.id) AS ordinal
                FROM board_approval_rate_variant_snapshots variant
            )
            INSERT INTO board_approval_requirement_drop_snapshots
                (id, approval_requirement_snapshot_id, source_drop_id, boss_name, item_name, display_rate,
                 numeric_probability, probability_scope, conditional_on_parent, parent_probability,
                 assumed_participants, rolls_per_completion, roll_group, rate_condition,
                 maximum_contribution, ehb_per_contribution, credited_weight, catalogue_version)
            SELECT md5(drop_snapshot.id::text || ':retired-rate-variant:' || variant.id::text)::uuid,
                   drop_snapshot.approval_requirement_snapshot_id, drop_snapshot.source_drop_id,
                   drop_snapshot.boss_name, drop_snapshot.item_name, variant.display_rate,
                   variant.numeric_probability, drop_snapshot.probability_scope,
                   drop_snapshot.conditional_on_parent, drop_snapshot.parent_probability,
                   drop_snapshot.assumed_participants, drop_snapshot.rolls_per_completion,
                   drop_snapshot.roll_group, variant.condition, drop_snapshot.maximum_contribution,
                   drop_snapshot.ehb_per_contribution, drop_snapshot.credited_weight,
                   drop_snapshot.catalogue_version
            FROM board_approval_requirement_drop_snapshots drop_snapshot
            JOIN ranked_variants variant
              ON variant.approval_requirement_drop_snapshot_id = drop_snapshot.id
            WHERE variant.ordinal > 1;
            """);

        // A legacy source with exactly one active rate has an unambiguous
        // authoritative source rate. Preserve it before retiring the variant
        // table; multi-rate sources remain intentionally incomplete so an
        // administrator can correct the source drop without a guessed merge.
        migrationBuilder.Sql("""
            UPDATE source_drops source_drop
            SET display_rate = legacy.display_rate,
                numeric_probability = legacy.numeric_probability,
                rate_condition_note = COALESCE(source_drop.rate_condition_note, legacy.condition),
                default_ehb_estimate = CASE
                    WHEN boss.efficient_completions_per_hour > 0
                     AND legacy.numeric_probability > 0
                    THEN 1 / (boss.efficient_completions_per_hour * legacy.numeric_probability * source_drop.rolls_per_completion)
                    ELSE source_drop.default_ehb_estimate
                END
            FROM (
                SELECT source_drop_id,
                       (array_agg(display_rate ORDER BY position, id))[1] AS display_rate,
                       (array_agg(numeric_probability ORDER BY position, id))[1] AS numeric_probability,
                       (array_agg(condition ORDER BY position, id))[1] AS condition
                FROM source_drop_rate_variants
                WHERE active
                GROUP BY source_drop_id
                HAVING COUNT(*) = 1
            ) legacy,
                 boss_activities boss
            WHERE source_drop.id = legacy.source_drop_id
              AND boss.id = source_drop.boss_activity_id
              AND source_drop.numeric_probability IS NULL;
            """);

        migrationBuilder.DropTable(
            name: "board_approval_rate_variant_snapshots");

        migrationBuilder.DropTable(
            name: "source_drop_rate_variants");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "board_approval_rate_variant_snapshots",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                approval_requirement_drop_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                catalogue_version = table.Column<long>(type: "bigint", nullable: false),
                condition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                display_rate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                numeric_probability = table.Column<decimal>(type: "numeric(18,12)", precision: 18, scale: 12, nullable: true),
                position = table.Column<int>(type: "integer", nullable: false),
                rate_variant_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_approval_rate_variant_snapshots", x => x.id);
                table.ForeignKey(
                    name: "FK_board_approval_rate_variant_snapshots_board_approval_requir~",
                    column: x => x.approval_requirement_drop_snapshot_id,
                    principalTable: "board_approval_requirement_drop_snapshots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "source_drop_rate_variants",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                condition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                display_rate = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                numeric_probability = table.Column<decimal>(type: "numeric(18,12)", precision: 18, scale: 12, nullable: true),
                position = table.Column<int>(type: "integer", nullable: false),
                source_drop_id = table.Column<Guid>(type: "uuid", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_source_drop_rate_variants", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_board_approval_rate_variant_snapshots_approval_requirement_~",
            table: "board_approval_rate_variant_snapshots",
            columns: RateVariantSnapshotIndexColumns,
            unique: true);

        // The forward conversion can have retained multiple frozen rates as sibling
        // snapshots. The old schema can represent only one parent drop snapshot, so
        // remove those forward-only siblings before restoring its unique index.
        migrationBuilder.Sql("""
            WITH ranked_drop_snapshots AS (
                SELECT id,
                       row_number() OVER (
                           PARTITION BY approval_requirement_snapshot_id, source_drop_id
                           ORDER BY id) AS ordinal
                FROM board_approval_requirement_drop_snapshots
            )
            DELETE FROM board_approval_requirement_drop_snapshots drop_snapshot
            USING ranked_drop_snapshots ranked
            WHERE drop_snapshot.id = ranked.id
              AND ranked.ordinal > 1;
            """);

        migrationBuilder.DropIndex(
            name: "IX_board_approval_requirement_drop_snapshots_approval_requirem~",
            table: "board_approval_requirement_drop_snapshots");

        migrationBuilder.CreateIndex(
            name: "IX_board_approval_requirement_drop_snapshots_approval_requirem~",
            table: "board_approval_requirement_drop_snapshots",
            columns: ["approval_requirement_snapshot_id", "source_drop_id"],
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_source_drop_rate_variants_source_drop_id_position",
            table: "source_drop_rate_variants",
            columns: SourceDropRateVariantIndexColumns,
            unique: true);
    }
}
