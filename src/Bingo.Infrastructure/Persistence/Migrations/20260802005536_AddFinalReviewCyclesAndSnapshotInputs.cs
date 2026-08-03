#pragma warning disable IDE0161, CA1861
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinalReviewCyclesAndSnapshotInputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_team_completion_corrections_event_id_team_id",
                table: "team_completion_corrections");

            migrationBuilder.DropIndex(
                name: "IX_final_review_resolutions_event_id_blocker_key",
                table: "final_review_resolutions");

            migrationBuilder.AddColumn<Guid>(
                name: "review_cycle_id",
                table: "team_completion_corrections",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<string>(
                name: "reason",
                table: "final_review_resolutions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "final_review_resolutions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "review_cycle_id",
                table: "final_review_resolutions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "team_id",
                table: "final_review_resolutions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "calculation_inputs_json",
                table: "event_finalizations",
                type: "character varying(30000)",
                maxLength: 30000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "calculation_results_json",
                table: "event_finalizations",
                type: "character varying(30000)",
                maxLength: 30000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "consumed_resolution_ids_json",
                table: "event_finalizations",
                type: "character varying(12000)",
                maxLength: 12000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "review_cycle_id",
                table: "event_finalizations",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE affected_ids text;
                BEGIN
                    SELECT string_agg(c.event_id::text, ', ' ORDER BY c.event_id) INTO affected_ids
                    FROM team_completion_corrections c
                    WHERE (SELECT count(*) FROM event_state_transitions t WHERE t.event_id = c.event_id AND t.to_state = 'AwaitingFinalReview') <> 1;
                    IF affected_ids IS NOT NULL THEN
                        RAISE EXCEPTION 'Slice 9 final-review migration refused: completion corrections have zero or multiple review-cycle transitions for event IDs: %. Inspect those event_state_transitions/team_completion_corrections rows, correct retained history, then retry.', affected_ids;
                    END IF;
                    SELECT string_agg(r.event_id::text, ', ' ORDER BY r.event_id) INTO affected_ids
                    FROM final_review_resolutions r
                    WHERE (SELECT count(*) FROM event_state_transitions t WHERE t.event_id = r.event_id AND t.to_state = 'AwaitingFinalReview') <> 1;
                    IF affected_ids IS NOT NULL THEN
                        RAISE EXCEPTION 'Slice 9 final-review migration refused: resolutions have zero or multiple review-cycle transitions for event IDs: %. Inspect those event_state_transitions/final_review_resolutions rows, correct retained history, then retry.', affected_ids;
                    END IF;
                    SELECT string_agg(format('event_id=%s snapshot_id=%s', f.event_id, f.id), ', ' ORDER BY f.event_id, f.id) INTO affected_ids
                    FROM event_finalizations f
                    WHERE (SELECT count(*) FROM event_state_transitions t WHERE t.event_id = f.event_id AND t.to_state = 'AwaitingFinalReview') <> 1;
                    IF affected_ids IS NOT NULL THEN
                        RAISE EXCEPTION 'Slice 9 final-review migration refused: official snapshots have zero or multiple review-cycle transitions for %. Inspect those event_state_transitions/event_finalizations rows, correct retained history, then retry.', affected_ids;
                    END IF;
                    UPDATE team_completion_corrections c
                    SET review_cycle_id = (SELECT t.id FROM event_state_transitions t WHERE t.event_id = c.event_id AND t.to_state = 'AwaitingFinalReview');
                    UPDATE final_review_resolutions r
                    SET review_cycle_id = (SELECT t.id FROM event_state_transitions t WHERE t.event_id = r.event_id AND t.to_state = 'AwaitingFinalReview'), kind = 'ExceptionalOverride'
                    WHERE kind = '';
                    UPDATE event_finalizations f
                    SET review_cycle_id = (SELECT t.id FROM event_state_transitions t WHERE t.event_id = f.event_id AND t.to_state = 'AwaitingFinalReview');
                    IF EXISTS (
                        SELECT 1 FROM final_review_resolutions
                        GROUP BY review_cycle_id, blocker_key HAVING count(*) > 1
                    ) THEN
                        SELECT string_agg(review_cycle_id::text || ':' || blocker_key, ', ' ORDER BY review_cycle_id, blocker_key)
                        INTO affected_ids
                        FROM final_review_resolutions
                        GROUP BY review_cycle_id, blocker_key HAVING count(*) > 1;
                        RAISE EXCEPTION 'Slice 9 final-review migration refused: duplicate resolutions apply to one retained review cycle and blocker (%). Inspect final_review_resolutions, retain one authoritative row, then retry.', affected_ids;
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_team_completion_corrections_review_cycle_id_team_id",
                table: "team_completion_corrections",
                columns: new[] { "review_cycle_id", "team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_final_review_resolutions_review_cycle_id_blocker_key",
                table: "final_review_resolutions",
                columns: new[] { "review_cycle_id", "blocker_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_team_completion_corrections_review_cycle_id_team_id",
                table: "team_completion_corrections");

            migrationBuilder.DropIndex(
                name: "IX_final_review_resolutions_review_cycle_id_blocker_key",
                table: "final_review_resolutions");

            migrationBuilder.DropColumn(
                name: "review_cycle_id",
                table: "team_completion_corrections");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "final_review_resolutions");

            migrationBuilder.DropColumn(
                name: "review_cycle_id",
                table: "final_review_resolutions");

            migrationBuilder.DropColumn(
                name: "team_id",
                table: "final_review_resolutions");

            migrationBuilder.DropColumn(
                name: "calculation_inputs_json",
                table: "event_finalizations");

            migrationBuilder.DropColumn(
                name: "calculation_results_json",
                table: "event_finalizations");

            migrationBuilder.DropColumn(
                name: "consumed_resolution_ids_json",
                table: "event_finalizations");

            migrationBuilder.DropColumn(
                name: "review_cycle_id",
                table: "event_finalizations");

            migrationBuilder.AlterColumn<string>(
                name: "reason",
                table: "final_review_resolutions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_completion_corrections_event_id_team_id",
                table: "team_completion_corrections",
                columns: new[] { "event_id", "team_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_final_review_resolutions_event_id_blocker_key",
                table: "final_review_resolutions",
                columns: new[] { "event_id", "blocker_key" });
        }
    }
}
