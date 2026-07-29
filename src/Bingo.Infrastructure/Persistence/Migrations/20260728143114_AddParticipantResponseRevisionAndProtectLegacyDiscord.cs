using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddParticipantResponseRevisionAndProtectLegacyDiscord : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "response_version",
            table: "event_participants",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        // Retained historical answers stay available to administrators but not public tables.
        migrationBuilder.Sql("UPDATE signup_questions SET public_on_signup_board = FALSE WHERE system_field = 'LegacyDiscordIdentity';");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "response_version",
            table: "event_participants");
    }
}
