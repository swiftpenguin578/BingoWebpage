using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Bingo.Infrastructure.Persistence.Migrations;

    /// <inheritdoc />
    public partial class AddTeamsAndDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "draft_picks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pick_number = table.Column<int>(type: "integer", nullable: false),
                    round_number = table.Column<int>(type: "integer", nullable: false),
                    picked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    undone_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_draft_picks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "draft_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_team_size = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finalized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_draft_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "team_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    assigned_by_draft_pick_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignment_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_memberships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    formation_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    affiliation_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    included_in_draft = table.Column<bool>(type: "boolean", nullable: false),
                    draft_position = table.Column<int>(type: "integer", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    finalized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teams", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_draft_picks_draft_session_id_pick_number",
                table: "draft_picks",
                columns: new[] { "draft_session_id", "pick_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_draft_picks_event_participant_id",
                table: "draft_picks",
                column: "event_participant_id",
                unique: true,
                filter: "undone_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_draft_sessions_event_id",
                table: "draft_sessions",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_memberships_event_participant_id",
                table: "team_memberships",
                column: "event_participant_id",
                unique: true,
                filter: "left_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_team_memberships_team_id_event_participant_id",
                table: "team_memberships",
                columns: new[] { "team_id", "event_participant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_teams_event_id_slug",
                table: "teams",
                columns: new[] { "event_id", "slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "draft_picks");

            migrationBuilder.DropTable(
                name: "draft_sessions");

            migrationBuilder.DropTable(
                name: "team_memberships");

            migrationBuilder.DropTable(
                name: "teams");
        }
}
