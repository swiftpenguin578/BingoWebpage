using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddEventFinalizationWorkflow : Migration
{
    private static readonly string[] EventVersionColumns = ["event_id", "version"];
    private static readonly string[] EventBlockerColumns = ["event_id", "blocker_key"];
    private static readonly string[] FinalizationTeamColumns = ["finalization_id", "team_id"];
    private static readonly string[] EventTeamColumns = ["event_id", "team_id"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "archived_at",
            table: "events",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "finalized_at",
            table: "events",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "event_finalizations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                version = table.Column<int>(type: "integer", nullable: false),
                finalized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                finalized_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                unfinalized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                unfinalized_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                unfinalize_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_event_finalizations", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "final_review_resolutions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                blocker_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                blocker_description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                resolved_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_final_review_resolutions", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "official_placements",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                finalization_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                team_id = table.Column<Guid>(type: "uuid", nullable: false),
                team_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                placement = table.Column<int>(type: "integer", nullable: false),
                board_complete = table.Column<bool>(type: "boolean", nullable: false),
                board_completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                completed_lines = table.Column<int>(type: "integer", nullable: false),
                completed_tiles = table.Column<int>(type: "integer", nullable: false),
                ehb_tiebreak = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_official_placements", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "team_completion_corrections",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                team_id = table.Column<Guid>(type: "uuid", nullable: false),
                corrected_completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                corrected_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_team_completion_corrections", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_event_finalizations_event_id_version",
            table: "event_finalizations",
            columns: EventVersionColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_final_review_resolutions_event_id_blocker_key",
            table: "final_review_resolutions",
            columns: EventBlockerColumns);

        migrationBuilder.CreateIndex(
            name: "IX_official_placements_finalization_id_team_id",
            table: "official_placements",
            columns: FinalizationTeamColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_team_completion_corrections_event_id_team_id",
            table: "team_completion_corrections",
            columns: EventTeamColumns,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "event_finalizations");

        migrationBuilder.DropTable(
            name: "final_review_resolutions");

        migrationBuilder.DropTable(
            name: "official_placements");

        migrationBuilder.DropTable(
            name: "team_completion_corrections");

        migrationBuilder.DropColumn(
            name: "archived_at",
            table: "events");

        migrationBuilder.DropColumn(
            name: "finalized_at",
            table: "events");
    }
}
