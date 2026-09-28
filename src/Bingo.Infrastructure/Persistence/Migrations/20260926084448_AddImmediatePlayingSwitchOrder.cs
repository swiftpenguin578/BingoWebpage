using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddImmediatePlayingSwitchOrder : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "sequence",
            table: "event_participant_character_swaps",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        // Preserve the exact legacy effective/recorded/UUID ordering, including
        // retained future switches. Do not rewrite attribution or timestamps.
        migrationBuilder.Sql("""
                WITH ordered AS (
                    SELECT id, row_number() OVER (
                        PARTITION BY event_participant_id
                        ORDER BY effective_at_utc, recorded_at_utc, id) AS position
                    FROM event_participant_character_swaps
                )
                UPDATE event_participant_character_swaps AS transition
                SET sequence = ordered.position
                FROM ordered WHERE transition.id = ordered.id;
                """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "sequence",
            table: "event_participant_character_swaps");
    }
}
