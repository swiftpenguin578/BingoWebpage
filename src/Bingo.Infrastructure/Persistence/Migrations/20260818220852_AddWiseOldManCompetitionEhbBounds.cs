using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWiseOldManCompetitionEhbBounds : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "end_ehb",
            table: "event_competition_character_activity",
            type: "numeric(14,4)",
            precision: 14,
            scale: 4,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "start_ehb",
            table: "event_competition_character_activity",
            type: "numeric(14,4)",
            precision: 14,
            scale: 4,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "end_ehb",
            table: "event_competition_character_activity");

        migrationBuilder.DropColumn(
            name: "start_ehb",
            table: "event_competition_character_activity");
    }
}
