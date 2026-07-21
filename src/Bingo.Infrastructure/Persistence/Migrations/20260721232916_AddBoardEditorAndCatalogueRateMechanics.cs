using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddBoardEditorAndCatalogueRateMechanics : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "image_url",
            table: "tile_templates",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "credited_weight",
            table: "tile_template_requirements",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "credited_weight",
            table: "template_requirement_drops",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "assumed_participants",
            table: "source_drops",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<bool>(
            name: "conditional_on_parent",
            table: "source_drops",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<decimal>(
            name: "parent_probability",
            table: "source_drops",
            type: "numeric(18,12)",
            precision: 18,
            scale: 12,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "probability_scope",
            table: "source_drops",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Participant");

        migrationBuilder.AddColumn<string>(
            name: "roll_group",
            table: "source_drops",
            type: "character varying(120)",
            maxLength: 120,
            nullable: false,
            defaultValue: "default");

        migrationBuilder.AddColumn<int>(
            name: "rolls_per_completion",
            table: "source_drops",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<string>(
            name: "image_url_snapshot",
            table: "board_tiles",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "credited_weight",
            table: "board_requirement_snapshots",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "credited_weight",
            table: "board_requirement_drop_snapshots",
            type: "integer",
            nullable: false,
            defaultValue: 1);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "image_url",
            table: "tile_templates");

        migrationBuilder.DropColumn(
            name: "credited_weight",
            table: "tile_template_requirements");

        migrationBuilder.DropColumn(
            name: "credited_weight",
            table: "template_requirement_drops");

        migrationBuilder.DropColumn(
            name: "assumed_participants",
            table: "source_drops");

        migrationBuilder.DropColumn(
            name: "conditional_on_parent",
            table: "source_drops");

        migrationBuilder.DropColumn(
            name: "parent_probability",
            table: "source_drops");

        migrationBuilder.DropColumn(
            name: "probability_scope",
            table: "source_drops");

        migrationBuilder.DropColumn(
            name: "roll_group",
            table: "source_drops");

        migrationBuilder.DropColumn(
            name: "rolls_per_completion",
            table: "source_drops");

        migrationBuilder.DropColumn(
            name: "image_url_snapshot",
            table: "board_tiles");

        migrationBuilder.DropColumn(
            name: "credited_weight",
            table: "board_requirement_snapshots");

        migrationBuilder.DropColumn(
            name: "credited_weight",
            table: "board_requirement_drop_snapshots");
    }
}
