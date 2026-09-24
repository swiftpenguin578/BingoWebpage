#pragma warning disable CA1861
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTileCompletionFactsAndCurrentScoreReachedAt : Migration
{
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "current_score_reached_at",
                table: "official_placements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_board_approval_tile_snapshots_approval_snapshot_id_board_ti~",
                table: "board_approval_tile_snapshots",
                columns: new[] { "approval_snapshot_id", "board_tile_id" });

            migrationBuilder.CreateTable(
                name: "tile_completion_facts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    board_tile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approval_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_complete = table.Column<bool>(type: "boolean", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    qualifying_contributions_json = table.Column<string>(type: "jsonb", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tile_completion_facts", x => x.id);
                    table.CheckConstraint("ck_tile_completion_facts_incomplete", "is_complete OR (completed_at IS NULL AND qualifying_contributions_json = '[]'::jsonb)");
                    table.ForeignKey(
                        name: "FK_tile_completion_facts_board_approval_tile_snapshots_approva~",
                        columns: x => new { x.approval_snapshot_id, x.board_tile_id },
                        principalTable: "board_approval_tile_snapshots",
                        principalColumns: new[] { "approval_snapshot_id", "board_tile_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tile_completion_facts_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tile_completion_facts_teams_event_id_team_id",
                        columns: x => new { x.event_id, x.team_id },
                        principalTable: "teams",
                        principalColumns: new[] { "event_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tile_completion_facts_approval_snapshot_id_board_tile_id",
                table: "tile_completion_facts",
                columns: new[] { "approval_snapshot_id", "board_tile_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tile_completion_facts_event_id_approval_snapshot_id_team_id",
                table: "tile_completion_facts",
                columns: new[] { "event_id", "approval_snapshot_id", "team_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tile_completion_facts_event_id_team_id_board_tile_id_approv~",
                table: "tile_completion_facts",
                columns: new[] { "event_id", "team_id", "board_tile_id", "approval_snapshot_id" },
                unique: true);

            migrationBuilder.Sql("""
                WITH active_generation AS (
                    SELECT b.event_id, b.active_approval_snapshot_id AS approval_snapshot_id
                    FROM boards b
                    WHERE b.state = 'Published' AND b.active_approval_snapshot_id IS NOT NULL
                ),
                team_tiles AS (
                    SELECT g.event_id, t.id AS team_id, tile.board_tile_id, g.approval_snapshot_id,
                           tile.id AS approval_tile_snapshot_id, requirement_count.expected_requirement_count
                    FROM active_generation g
                    JOIN teams t ON t.event_id = g.event_id AND t.active AND t.finalized_at IS NOT NULL
                    JOIN board_approval_tile_snapshots tile ON tile.approval_snapshot_id = g.approval_snapshot_id
                    CROSS JOIN LATERAL (
                        SELECT COUNT(*) AS expected_requirement_count
                        FROM board_approval_requirement_snapshots requirement
                        WHERE requirement.approval_tile_snapshot_id = tile.id
                    ) requirement_count
                ),
                team_requirements AS (
                    SELECT DISTINCT tt.event_id, tt.team_id, tt.board_tile_id, tt.approval_snapshot_id,
                           tt.approval_tile_snapshot_id, tt.expected_requirement_count,
                           requirement.id AS approval_requirement_snapshot_id,
                           requirement.board_requirement_snapshot_id AS requirement_id,
                           requirement.position, requirement.target_contribution AS target,
                           requirement.duplicates_allowed, requirement.manual_objective
                    FROM team_tiles tt
                    JOIN board_approval_requirement_snapshots requirement ON requirement.approval_tile_snapshot_id = tt.approval_tile_snapshot_id
                ),
                contributions AS (
                    SELECT tr.event_id, tr.team_id, tr.board_tile_id, tr.approval_snapshot_id, tr.requirement_id,
                           tr.target, tr.duplicates_allowed, tr.manual_objective,
                           contribution.id AS contribution_id, submission.id AS submission_id,
                           submission.submitted_at, contribution.amount,
                           contribution.drop_snapshot_id,
                           working_drop.source_drop_id,
                           working_drop.item_id_snapshot,
                           CASE WHEN tr.duplicates_allowed THEN approved_drop.source_drop_id ELSE approved_drop.item_id_snapshot END AS cap_key,
                           CASE WHEN tr.duplicates_allowed THEN COALESCE(approved_drop.maximum_contribution, 2147483647)
                                ELSE COALESCE(approved_drop.maximum_contribution, 1) END AS cap_limit,
                           approved_drop.id AS approved_drop_id
                    FROM team_requirements tr
                    JOIN submission_contributions contribution ON contribution.team_id = tr.team_id AND contribution.requirement_id = tr.requirement_id
                    JOIN submissions submission ON submission.id = contribution.submission_id AND submission.event_id = tr.event_id AND submission.status = 'Approved'
                    LEFT JOIN board_requirement_drop_snapshots working_drop ON working_drop.id = contribution.drop_snapshot_id AND working_drop.requirement_id = contribution.requirement_id
                    LEFT JOIN board_approval_requirement_drop_snapshots approved_drop
                      ON approved_drop.approval_requirement_snapshot_id = tr.approval_requirement_snapshot_id
                     AND approved_drop.source_drop_id = working_drop.source_drop_id
                     AND approved_drop.item_id_snapshot = working_drop.item_id_snapshot
                    WHERE contribution.reversed_at IS NULL
                ),
                requirement_totals AS (
                    SELECT tr.event_id, tr.team_id, tr.board_tile_id, tr.approval_snapshot_id, tr.requirement_id,
                           COALESCE(SUM(c.amount), 0) AS effective_total,
                           COUNT(c.contribution_id) FILTER (WHERE
                               (tr.manual_objective AND c.drop_snapshot_id IS NOT NULL) OR
                               (NOT tr.manual_objective AND (c.drop_snapshot_id IS NULL OR c.approved_drop_id IS NULL))) AS mapping_errors
                    FROM team_requirements tr
                    LEFT JOIN contributions c ON c.event_id = tr.event_id AND c.team_id = tr.team_id AND c.requirement_id = tr.requirement_id
                    GROUP BY tr.event_id, tr.team_id, tr.board_tile_id, tr.approval_snapshot_id, tr.requirement_id
                ),
                cap_usage AS (
                    SELECT event_id, team_id, requirement_id, cap_key,
                           COUNT(DISTINCT cap_limit) AS cap_variants,
                           MAX(cap_limit) AS cap_limit,
                           SUM(amount) AS used
                    FROM contributions
                    WHERE cap_key IS NOT NULL
                    GROUP BY event_id, team_id, requirement_id, cap_key
                ),
                cap_violations AS (
                    SELECT event_id, team_id, requirement_id,
                           COUNT(*) FILTER (WHERE cap_variants <> 1 OR used > cap_limit) AS violations
                    FROM cap_usage
                    GROUP BY event_id, team_id, requirement_id
                ),
                ordered_contributions AS (
                    SELECT c.*,
                           SUM(c.amount) OVER (PARTITION BY c.event_id, c.team_id, c.requirement_id
                                               ORDER BY c.submitted_at, c.contribution_id ROWS UNBOUNDED PRECEDING) AS running_amount
                    FROM contributions c
                ),
                threshold_crossings AS (
                    SELECT event_id, team_id, board_tile_id, approval_snapshot_id, requirement_id,
                           MIN(submitted_at) FILTER (WHERE running_amount >= target) AS completed_at,
                           jsonb_agg(jsonb_build_object(
                               'RequirementId', requirement_id,
                               'ContributionId', contribution_id,
                               'SubmissionId', submission_id,
                               'SubmittedAt', submitted_at,
                               'EffectiveAmount', amount)
                               ORDER BY submitted_at, contribution_id)
                               FILTER (WHERE running_amount - amount < target) AS qualifying_contributions
                    FROM ordered_contributions
                    GROUP BY event_id, team_id, board_tile_id, approval_snapshot_id, requirement_id
                ),
                requirement_state AS (
                    SELECT tr.*,
                           (totals.mapping_errors = 0 AND totals.effective_total <= tr.target AND COALESCE(caps.violations, 0) = 0) AS derivable,
                           crossings.completed_at,
                           crossings.qualifying_contributions
                    FROM team_requirements tr
                    JOIN requirement_totals totals USING (event_id, team_id, board_tile_id, approval_snapshot_id, requirement_id)
                    LEFT JOIN cap_violations caps USING (event_id, team_id, requirement_id)
                    LEFT JOIN threshold_crossings crossings USING (event_id, team_id, board_tile_id, approval_snapshot_id, requirement_id)
                ),
                tile_state AS (
                    SELECT tt.event_id, tt.team_id, tt.board_tile_id, tt.approval_snapshot_id,
                           (tt.expected_requirement_count > 0 AND COUNT(rs.requirement_id) = tt.expected_requirement_count AND BOOL_AND(rs.derivable)) AS derivable,
                           (tt.expected_requirement_count > 0 AND COUNT(rs.requirement_id) = tt.expected_requirement_count AND BOOL_AND(rs.derivable AND rs.completed_at IS NOT NULL)) AS is_complete,
                           CASE WHEN tt.expected_requirement_count > 0 AND COUNT(rs.requirement_id) = tt.expected_requirement_count AND BOOL_AND(rs.derivable AND rs.completed_at IS NOT NULL)
                                THEN MAX(rs.completed_at) END AS completed_at,
                           CASE WHEN tt.expected_requirement_count > 0 AND COUNT(rs.requirement_id) = tt.expected_requirement_count AND BOOL_AND(rs.derivable AND rs.completed_at IS NOT NULL)
                                THEN jsonb_object_agg(rs.requirement_id::text, rs.qualifying_contributions ORDER BY rs.position)
                                ELSE '[]'::jsonb END AS qualifying_contributions
                    FROM team_tiles tt
                    LEFT JOIN requirement_state rs ON rs.event_id = tt.event_id AND rs.team_id = tt.team_id AND
                        rs.board_tile_id = tt.board_tile_id AND rs.approval_snapshot_id = tt.approval_snapshot_id
                    GROUP BY tt.event_id, tt.team_id, tt.board_tile_id, tt.approval_snapshot_id, tt.expected_requirement_count
                ),
                team_state AS (
                    SELECT event_id, team_id, approval_snapshot_id,
                           COUNT(*) AS tile_count,
                           BOOL_AND(derivable) AS derivable
                    FROM tile_state
                    GROUP BY event_id, team_id, approval_snapshot_id
                )
                INSERT INTO tile_completion_facts
                    (id, event_id, team_id, board_tile_id, approval_snapshot_id, is_complete, completed_at, qualifying_contributions_json, recorded_at)
                SELECT gen_random_uuid(), tile.event_id, tile.team_id, tile.board_tile_id, tile.approval_snapshot_id,
                       tile.is_complete, tile.completed_at, tile.qualifying_contributions, CURRENT_TIMESTAMP
                FROM tile_state tile
                JOIN team_state team USING (event_id, team_id, approval_snapshot_id)
                WHERE team.derivable AND team.tile_count = (
                    SELECT COUNT(*) FROM team_tiles expected
                    WHERE expected.event_id = tile.event_id AND expected.team_id = tile.team_id AND expected.approval_snapshot_id = tile.approval_snapshot_id
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tile_completion_facts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_board_approval_tile_snapshots_approval_snapshot_id_board_ti~",
                table: "board_approval_tile_snapshots");

            migrationBuilder.DropColumn(
                name: "current_score_reached_at",
                table: "official_placements");
        }
}
