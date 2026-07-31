using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSlice7LiveAccountAndTeamFocusFoundation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint(
            name: "AK_teams_event_id_id",
            table: "teams",
            columns: new[] { "event_id", "id" });

        migrationBuilder.CreateTable(
            name: "event_participant_character_swaps",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                previous_osrs_character_id = table.Column<Guid>(type: "uuid", nullable: true),
                next_osrs_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                effective_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                recorded_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_event_participant_character_swaps", x => x.id);
                table.CheckConstraint("ck_event_participant_character_swaps_distinct_characters", "previous_osrs_character_id IS NULL OR previous_osrs_character_id <> next_osrs_character_id");
                table.ForeignKey(
                    name: "FK_event_participant_character_swaps_accounts_recorded_by_acco~",
                    column: x => x.recorded_by_account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_event_participant_character_swaps_event_participants_event_~",
                    columns: x => new { x.event_id, x.event_participant_id },
                    principalTable: "event_participants",
                    principalColumns: new[] { "event_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_event_participant_character_swaps_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_event_participant_character_swaps_osrs_characters_next_osrs~",
                    column: x => x.next_osrs_character_id,
                    principalTable: "osrs_characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_event_participant_character_swaps_osrs_characters_previous_~",
                    column: x => x.previous_osrs_character_id,
                    principalTable: "osrs_characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "team_focus_markers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                team_id = table.Column<Guid>(type: "uuid", nullable: false),
                target_kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                board_tile_id = table.Column<Guid>(type: "uuid", nullable: true),
                row_index = table.Column<int>(type: "integer", nullable: true),
                column_index = table.Column<int>(type: "integer", nullable: true),
                focused = table.Column<bool>(type: "boolean", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_by_account_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_team_focus_markers", x => x.id);
                table.UniqueConstraint("AK_team_focus_markers_event_id_team_id", x => new { x.event_id, x.team_id });
                table.CheckConstraint("ck_team_focus_markers_target_shape", "(target_kind = 'Tile' AND board_tile_id IS NOT NULL AND row_index IS NULL AND column_index IS NULL) OR (target_kind = 'Row' AND board_tile_id IS NULL AND row_index >= 0 AND column_index IS NULL) OR (target_kind = 'Column' AND board_tile_id IS NULL AND row_index IS NULL AND column_index >= 0)");
                table.ForeignKey(
                    name: "FK_team_focus_markers_accounts_updated_by_account_id",
                    column: x => x.updated_by_account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_team_focus_markers_board_tiles_board_tile_id",
                    column: x => x.board_tile_id,
                    principalTable: "board_tiles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_team_focus_markers_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_team_focus_markers_teams_event_id_team_id",
                    columns: x => new { x.event_id, x.team_id },
                    principalTable: "teams",
                    principalColumns: new[] { "event_id", "id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_event_participant_character_swaps_event_id_event_participan~",
            table: "event_participant_character_swaps",
            columns: new[] { "event_id", "event_participant_id" });

        migrationBuilder.CreateIndex(
            name: "IX_event_participant_character_swaps_event_participant_id",
            table: "event_participant_character_swaps",
            column: "event_participant_id",
            unique: true,
            filter: "previous_osrs_character_id IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_event_participant_character_swaps_event_participant_id_effe~",
            table: "event_participant_character_swaps",
            columns: new[] { "event_participant_id", "effective_at_utc", "recorded_at_utc", "id" });

        migrationBuilder.CreateIndex(
            name: "IX_event_participant_character_swaps_next_osrs_character_id",
            table: "event_participant_character_swaps",
            column: "next_osrs_character_id");

        migrationBuilder.CreateIndex(
            name: "IX_event_participant_character_swaps_previous_osrs_character_id",
            table: "event_participant_character_swaps",
            column: "previous_osrs_character_id");

        migrationBuilder.CreateIndex(
            name: "IX_event_participant_character_swaps_recorded_by_account_id",
            table: "event_participant_character_swaps",
            column: "recorded_by_account_id");

        migrationBuilder.CreateIndex(
            name: "IX_team_focus_markers_board_tile_id",
            table: "team_focus_markers",
            column: "board_tile_id");

        migrationBuilder.CreateIndex(
            name: "IX_team_focus_markers_team_id_board_tile_id",
            table: "team_focus_markers",
            columns: new[] { "team_id", "board_tile_id" },
            unique: true,
            filter: "target_kind = 'Tile'");

        migrationBuilder.CreateIndex(
            name: "IX_team_focus_markers_team_id_column_index",
            table: "team_focus_markers",
            columns: new[] { "team_id", "column_index" },
            unique: true,
            filter: "target_kind = 'Column'");

        migrationBuilder.CreateIndex(
            name: "IX_team_focus_markers_team_id_row_index",
            table: "team_focus_markers",
            columns: new[] { "team_id", "row_index" },
            unique: true,
            filter: "target_kind = 'Row'");

        migrationBuilder.CreateIndex(
            name: "IX_team_focus_markers_updated_by_account_id",
            table: "team_focus_markers",
            column: "updated_by_account_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "event_participant_character_swaps");

        migrationBuilder.DropTable(
            name: "team_focus_markers");

        migrationBuilder.DropUniqueConstraint(
            name: "AK_teams_event_id_id",
            table: "teams");
    }
}
