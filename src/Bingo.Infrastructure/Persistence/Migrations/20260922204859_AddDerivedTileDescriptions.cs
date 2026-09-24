using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDerivedTileDescriptions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "description_is_automatic",
            table: "tile_templates",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "description_is_automatic",
            table: "board_tiles",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AlterColumn<string>(
            name: "description",
            table: "board_approval_tile_snapshots",
            type: "character varying(16000)",
            maxLength: 16000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(4000)",
            oldMaxLength: 4000);

        migrationBuilder.AddColumn<bool>(
            name: "description_is_automatic",
            table: "board_approval_tile_snapshots",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("""
            UPDATE tile_templates
            SET description_is_automatic = TRUE
            WHERE description ~ '^[[:space:]]*$';

            UPDATE board_tiles
            SET description_is_automatic = TRUE
            WHERE description_snapshot ~ '^[[:space:]]*$';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "description_is_automatic",
            table: "tile_templates");

        migrationBuilder.DropColumn(
            name: "description_is_automatic",
            table: "board_tiles");

        migrationBuilder.DropColumn(
            name: "description_is_automatic",
            table: "board_approval_tile_snapshots");

        migrationBuilder.AlterColumn<string>(
            name: "description",
            table: "board_approval_tile_snapshots",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(16000)",
            oldMaxLength: 16000);
    }
}
