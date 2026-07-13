using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddBoardSnapshots : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "board_requirement_boss_snapshots",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                requirement_id = table.Column<Guid>(type: "uuid", nullable: false),
                boss_activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                boss_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                efficient_rate = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_requirement_boss_snapshots", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "board_requirement_drop_snapshots",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                requirement_id = table.Column<Guid>(type: "uuid", nullable: false),
                source_drop_id = table.Column<Guid>(type: "uuid", nullable: false),
                boss_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                item_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                display_rate = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                numeric_probability = table.Column<decimal>(type: "numeric(18,12)", precision: 18, scale: 12, nullable: true),
                maximum_contribution = table.Column<int>(type: "integer", nullable: true),
                ehb_per_contribution = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_requirement_drop_snapshots", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "board_requirement_snapshots",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                board_tile_id = table.Column<Guid>(type: "uuid", nullable: false),
                position = table.Column<int>(type: "integer", nullable: false),
                target_contribution = table.Column<int>(type: "integer", nullable: false),
                duplicates_allowed = table.Column<bool>(type: "boolean", nullable: false),
                allow_higher_weightings = table.Column<bool>(type: "boolean", nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                manual_objective = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_requirement_snapshots", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "board_tiles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                board_id = table.Column<Guid>(type: "uuid", nullable: false),
                tile_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                row_index = table.Column<int>(type: "integer", nullable: false),
                column_index = table.Column<int>(type: "integer", nullable: false),
                name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description_snapshot = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                evidence_instructions_snapshot = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                estimated_ehb_snapshot = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_tiles", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_board_tiles_board_id_row_index_column_index",
            table: "board_tiles",
            columns: ["board_id", "row_index", "column_index"],
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "board_requirement_boss_snapshots");

        migrationBuilder.DropTable(
            name: "board_requirement_drop_snapshots");

        migrationBuilder.DropTable(
            name: "board_requirement_snapshots");

        migrationBuilder.DropTable(
            name: "board_tiles");
    }
}
