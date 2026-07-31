using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RemoveTeamFocusEventTeamAlternateKey : Migration
{
    private static readonly string[] EventAndTeamColumns = ["event_id", "team_id"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropUniqueConstraint(
            name: "AK_team_focus_markers_event_id_team_id",
            table: "team_focus_markers");

        migrationBuilder.CreateIndex(
            name: "IX_team_focus_markers_event_id_team_id",
            table: "team_focus_markers",
            columns: EventAndTeamColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_team_focus_markers_event_id_team_id",
            table: "team_focus_markers");

        migrationBuilder.AddUniqueConstraint(
            name: "AK_team_focus_markers_event_id_team_id",
            table: "team_focus_markers",
            columns: EventAndTeamColumns);
    }
}
