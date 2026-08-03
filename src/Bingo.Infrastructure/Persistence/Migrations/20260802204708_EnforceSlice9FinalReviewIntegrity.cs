#pragma warning disable IDE0161, CA1861
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSlice9FinalReviewIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE affected_ids text;
                BEGIN
                    SELECT string_agg(format('event_id=%s finalization_id=%s review_cycle_id=%s', f.event_id, f.id, coalesce(f.review_cycle_id::text, 'NULL')), ', ' ORDER BY f.event_id, f.id)
                    INTO affected_ids
                    FROM event_finalizations f
                    WHERE f.review_cycle_id IS NULL OR f.review_cycle_id = '00000000-0000-0000-0000-000000000000'::uuid
                       OR NOT EXISTS (SELECT 1 FROM event_state_transitions t WHERE t.event_id = f.event_id AND t.id = f.review_cycle_id);
                    IF affected_ids IS NOT NULL THEN
                        RAISE EXCEPTION 'Slice 9 final-review integrity refused: invalid event_finalizations cycle references (%). Correct the listed IDs, then retry.', affected_ids;
                    END IF;
                    SELECT string_agg(format('event_id=%s correction_id=%s review_cycle_id=%s', c.event_id, c.id, c.review_cycle_id), ', ' ORDER BY c.event_id, c.id)
                    INTO affected_ids
                    FROM team_completion_corrections c
                    WHERE c.review_cycle_id IS NULL OR c.review_cycle_id = '00000000-0000-0000-0000-000000000000'::uuid
                       OR NOT EXISTS (SELECT 1 FROM event_state_transitions t WHERE t.event_id = c.event_id AND t.id = c.review_cycle_id)
                       OR NOT EXISTS (SELECT 1 FROM teams tm WHERE tm.event_id = c.event_id AND tm.id = c.team_id);
                    IF affected_ids IS NOT NULL THEN
                        RAISE EXCEPTION 'Slice 9 final-review integrity refused: invalid team_completion_corrections references (%). Correct the listed IDs, then retry.', affected_ids;
                    END IF;
                    SELECT string_agg(format('event_id=%s resolution_id=%s review_cycle_id=%s team_id=%s', r.event_id, r.id, r.review_cycle_id, coalesce(r.team_id::text, 'NULL')), ', ' ORDER BY r.event_id, r.id)
                    INTO affected_ids
                    FROM final_review_resolutions r
                    WHERE r.review_cycle_id IS NULL OR r.review_cycle_id = '00000000-0000-0000-0000-000000000000'::uuid
                       OR NOT EXISTS (SELECT 1 FROM event_state_transitions t WHERE t.event_id = r.event_id AND t.id = r.review_cycle_id)
                       OR (r.team_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM teams tm WHERE tm.event_id = r.event_id AND tm.id = r.team_id));
                    IF affected_ids IS NOT NULL THEN
                        RAISE EXCEPTION 'Slice 9 final-review integrity refused: invalid final_review_resolutions references (%). Correct the listed IDs, then retry.', affected_ids;
                    END IF;
                    SELECT string_agg(format('event_id=%s placement_id=%s finalization_id=%s', p.event_id, p.id, p.finalization_id), ', ' ORDER BY p.event_id, p.id)
                    INTO affected_ids
                    FROM official_placements p
                    WHERE NOT EXISTS (SELECT 1 FROM event_finalizations f WHERE f.event_id = p.event_id AND f.id = p.finalization_id);
                    IF affected_ids IS NOT NULL THEN
                        RAISE EXCEPTION 'Slice 9 final-review integrity refused: invalid official_placements references (%). Correct the listed IDs, then retry.', affected_ids;
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "review_cycle_id",
                table: "event_finalizations",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_event_state_transitions_event_id_id",
                table: "event_state_transitions",
                columns: new[] { "event_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_event_finalizations_event_id_id",
                table: "event_finalizations",
                columns: new[] { "event_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_team_completion_corrections_event_id_review_cycle_id",
                table: "team_completion_corrections",
                columns: new[] { "event_id", "review_cycle_id" });

            migrationBuilder.CreateIndex(
                name: "IX_team_completion_corrections_event_id_team_id",
                table: "team_completion_corrections",
                columns: new[] { "event_id", "team_id" });

            migrationBuilder.CreateIndex(
                name: "IX_official_placements_event_id_finalization_id",
                table: "official_placements",
                columns: new[] { "event_id", "finalization_id" });

            migrationBuilder.CreateIndex(
                name: "IX_final_review_resolutions_event_id_review_cycle_id",
                table: "final_review_resolutions",
                columns: new[] { "event_id", "review_cycle_id" });

            migrationBuilder.CreateIndex(
                name: "IX_final_review_resolutions_event_id_team_id",
                table: "final_review_resolutions",
                columns: new[] { "event_id", "team_id" });

            migrationBuilder.CreateIndex(
                name: "IX_event_finalizations_event_id_review_cycle_id",
                table: "event_finalizations",
                columns: new[] { "event_id", "review_cycle_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_event_finalizations_event_state_transitions_event_id_review~",
                table: "event_finalizations",
                columns: new[] { "event_id", "review_cycle_id" },
                principalTable: "event_state_transitions",
                principalColumns: new[] { "event_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_final_review_resolutions_event_state_transitions_event_id_r~",
                table: "final_review_resolutions",
                columns: new[] { "event_id", "review_cycle_id" },
                principalTable: "event_state_transitions",
                principalColumns: new[] { "event_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_final_review_resolutions_teams_event_id_team_id",
                table: "final_review_resolutions",
                columns: new[] { "event_id", "team_id" },
                principalTable: "teams",
                principalColumns: new[] { "event_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_official_placements_event_finalizations_event_id_finalizati~",
                table: "official_placements",
                columns: new[] { "event_id", "finalization_id" },
                principalTable: "event_finalizations",
                principalColumns: new[] { "event_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_team_completion_corrections_event_state_transitions_event_i~",
                table: "team_completion_corrections",
                columns: new[] { "event_id", "review_cycle_id" },
                principalTable: "event_state_transitions",
                principalColumns: new[] { "event_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_team_completion_corrections_teams_event_id_team_id",
                table: "team_completion_corrections",
                columns: new[] { "event_id", "team_id" },
                principalTable: "teams",
                principalColumns: new[] { "event_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_event_finalizations_event_state_transitions_event_id_review~",
                table: "event_finalizations");

            migrationBuilder.DropForeignKey(
                name: "FK_final_review_resolutions_event_state_transitions_event_id_r~",
                table: "final_review_resolutions");

            migrationBuilder.DropForeignKey(
                name: "FK_final_review_resolutions_teams_event_id_team_id",
                table: "final_review_resolutions");

            migrationBuilder.DropForeignKey(
                name: "FK_official_placements_event_finalizations_event_id_finalizati~",
                table: "official_placements");

            migrationBuilder.DropForeignKey(
                name: "FK_team_completion_corrections_event_state_transitions_event_i~",
                table: "team_completion_corrections");

            migrationBuilder.DropForeignKey(
                name: "FK_team_completion_corrections_teams_event_id_team_id",
                table: "team_completion_corrections");

            migrationBuilder.DropIndex(
                name: "IX_team_completion_corrections_event_id_review_cycle_id",
                table: "team_completion_corrections");

            migrationBuilder.DropIndex(
                name: "IX_team_completion_corrections_event_id_team_id",
                table: "team_completion_corrections");

            migrationBuilder.DropIndex(
                name: "IX_official_placements_event_id_finalization_id",
                table: "official_placements");

            migrationBuilder.DropIndex(
                name: "IX_final_review_resolutions_event_id_review_cycle_id",
                table: "final_review_resolutions");

            migrationBuilder.DropIndex(
                name: "IX_final_review_resolutions_event_id_team_id",
                table: "final_review_resolutions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_event_state_transitions_event_id_id",
                table: "event_state_transitions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_event_finalizations_event_id_id",
                table: "event_finalizations");

            migrationBuilder.DropIndex(
                name: "IX_event_finalizations_event_id_review_cycle_id",
                table: "event_finalizations");

            migrationBuilder.AlterColumn<Guid>(
                name: "review_cycle_id",
                table: "event_finalizations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
