using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class CacheEventCompetitionBossActivity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "last_metric_attempt_at",
            table: "event_competition_synchronizations",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "latest_metrics_complete",
            table: "event_competition_synchronizations",
            type: "boolean",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "metric_activity_batch_id",
            table: "event_competition_synchronizations",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "source_request_fingerprint",
            table: "event_competition_synchronizations",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "event_competition_character_metric_activity",
            columns: table => new
            {
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                generation = table.Column<int>(type: "integer", nullable: false),
                osrs_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                metric = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                competition_id = table.Column<long>(type: "bigint", nullable: false),
                start = table.Column<decimal>(type: "numeric(20,0)", precision: 20, scale: 0, nullable: true),
                end = table.Column<decimal>(type: "numeric(20,0)", precision: 20, scale: 0, nullable: true),
                gained = table.Column<decimal>(type: "numeric(20,0)", precision: 20, scale: 0, nullable: true),
                coverage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                last_issue = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                upstream_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                activity_batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                source_request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                assignment_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_event_competition_character_metric_activity", x => new { x.event_id, x.generation, x.osrs_character_id, x.metric });
                table.CheckConstraint("ck_metric_activity_origin", "(fetched_at IS NULL AND start IS NULL AND \"end\" IS NULL AND gained IS NULL AND activity_batch_id IS NOT NULL AND source_request_fingerprint IS NOT NULL AND coverage = 'Missing') OR (fetched_at IS NOT NULL AND start IS NOT NULL AND \"end\" IS NOT NULL AND gained IS NOT NULL AND activity_batch_id IS NOT NULL AND source_request_fingerprint IS NOT NULL AND coverage IN ('Ranked','EstimatedBaseline','ZeroRecorded'))");
                table.CheckConstraint("ck_metric_activity_values", "start >= -1 AND \"end\" >= -1 AND gained >= 0 AND trunc(start) = start AND trunc(\"end\") = \"end\" AND trunc(gained) = gained");
                table.ForeignKey(
                    name: "FK_event_competition_character_metric_activity_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_event_competition_character_metric_activity_osrs_characters~",
                    column: x => x.osrs_character_id,
                    principalTable: "osrs_characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "event_luck_outcome_bases",
            columns: table => new
            {
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                source_drop_id = table.Column<Guid>(type: "uuid", nullable: false),
                item_id_snapshot = table.Column<Guid>(type: "uuid", nullable: false),
                boss_activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                first_approval_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                first_approval_drop_snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                first_approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                numeric_probability = table.Column<decimal>(type: "numeric(18,12)", precision: 18, scale: 12, nullable: true),
                rolls_per_completion = table.Column<int>(type: "integer", nullable: true),
                probability_scope = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                conditional_on_parent = table.Column<bool>(type: "boolean", nullable: true),
                parent_probability = table.Column<decimal>(type: "numeric(18,12)", precision: 18, scale: 12, nullable: true),
                assumed_participants = table.Column<int>(type: "integer", nullable: true),
                roll_group = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                rate_condition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                metric = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                metric_bound_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                mapping_validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                mapping_catalogue_version = table.Column<long>(type: "bigint", nullable: true),
                source_revision = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_event_luck_outcome_bases", x => new { x.event_id, x.source_drop_id, x.item_id_snapshot });
                table.CheckConstraint("ck_luck_basis_binding", "(metric IS NULL AND metric_bound_at IS NULL AND mapping_validated_at IS NULL AND mapping_catalogue_version IS NULL AND source_revision = 1) OR (status = 'Retained' AND metric IS NOT NULL AND metric_bound_at IS NOT NULL AND mapping_validated_at IS NOT NULL AND mapping_catalogue_version IS NOT NULL AND source_revision = 2)");
                table.CheckConstraint("ck_luck_basis_mechanics", "status <> 'Retained' OR (boss_activity_id IS NOT NULL AND first_approval_snapshot_id IS NOT NULL AND first_approval_drop_snapshot_id IS NOT NULL AND first_approved_at IS NOT NULL AND numeric_probability IS NOT NULL AND rolls_per_completion IS NOT NULL AND numeric_probability > 0 AND numeric_probability <= 1 AND rolls_per_completion >= 1)");
                table.ForeignKey(
                    name: "FK_event_luck_outcome_bases_board_approval_requirement_drop_sn~",
                    column: x => x.first_approval_drop_snapshot_id,
                    principalTable: "board_approval_requirement_drop_snapshots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_event_luck_outcome_bases_board_approval_snapshots_first_app~",
                    column: x => x.first_approval_snapshot_id,
                    principalTable: "board_approval_snapshots",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_event_luck_outcome_bases_boss_activities_boss_activity_id",
                    column: x => x.boss_activity_id,
                    principalTable: "boss_activities",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_event_luck_outcome_bases_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_character_metric_activity_osrs_character_~",
            table: "event_competition_character_metric_activity",
            column: "osrs_character_id");

        migrationBuilder.CreateIndex(
            name: "IX_event_luck_outcome_bases_boss_activity_id",
            table: "event_luck_outcome_bases",
            column: "boss_activity_id");

        migrationBuilder.CreateIndex(
            name: "IX_event_luck_outcome_bases_first_approval_drop_snapshot_id",
            table: "event_luck_outcome_bases",
            column: "first_approval_drop_snapshot_id");

        migrationBuilder.CreateIndex(
            name: "IX_event_luck_outcome_bases_first_approval_snapshot_id",
            table: "event_luck_outcome_bases",
            column: "first_approval_snapshot_id");

        // Only the first validated metric binding may update a retained basis. Mechanics and
        // earliest approval identity are immutable even to bulk/raw SQL writers.
        migrationBuilder.Sql("""
            CREATE FUNCTION protect_event_luck_basis() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF (to_jsonb(NEW) - ARRAY['metric','metric_bound_at','mapping_validated_at','mapping_catalogue_version','source_revision'])
                    IS DISTINCT FROM (to_jsonb(OLD) - ARRAY['metric','metric_bound_at','mapping_validated_at','mapping_catalogue_version','source_revision'])
                    OR OLD.metric IS NOT NULL OR NEW.metric IS NULL OR NEW.source_revision <> OLD.source_revision + 1 THEN
                    RAISE EXCEPTION 'Retained event Luck mechanics and metric bindings are immutable';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER event_luck_basis_immutable BEFORE UPDATE ON event_luck_outcome_bases
                FOR EACH ROW EXECUTE FUNCTION protect_event_luck_basis();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS event_luck_basis_immutable ON event_luck_outcome_bases; DROP FUNCTION IF EXISTS protect_event_luck_basis();");
        migrationBuilder.DropTable(
            name: "event_competition_character_metric_activity");

        migrationBuilder.DropTable(
            name: "event_luck_outcome_bases");

        migrationBuilder.DropColumn(
            name: "last_metric_attempt_at",
            table: "event_competition_synchronizations");

        migrationBuilder.DropColumn(
            name: "latest_metrics_complete",
            table: "event_competition_synchronizations");

        migrationBuilder.DropColumn(
            name: "metric_activity_batch_id",
            table: "event_competition_synchronizations");

        migrationBuilder.DropColumn(
            name: "source_request_fingerprint",
            table: "event_competition_synchronizations");
    }
}
