using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;
/// <inheritdoc />
public partial class SaveStatsGuidanceAndArtwork : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "artwork_height",
            table: "catalogue_items",
            type: "numeric",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "artwork_rotation",
            table: "catalogue_items",
            type: "numeric",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "artwork_scale",
            table: "catalogue_items",
            type: "numeric",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "artwork_width",
            table: "catalogue_items",
            type: "numeric",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "artwork_x",
            table: "catalogue_items",
            type: "numeric",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "artwork_y",
            table: "catalogue_items",
            type: "numeric",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "stats_guidance_hidden",
            table: "accounts",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddCheckConstraint(
            name: "ck_catalogue_item_artwork",
            table: "catalogue_items",
            sql: "((artwork_x IS NULL AND artwork_y IS NULL AND artwork_width IS NULL AND artwork_height IS NULL AND artwork_scale IS NULL AND artwork_rotation IS NULL) OR (artwork_x IS NOT NULL AND artwork_y IS NOT NULL AND artwork_width IS NOT NULL AND artwork_height IS NOT NULL AND artwork_scale IS NOT NULL AND artwork_rotation IS NOT NULL AND artwork_x BETWEEN 0 AND 100 AND artwork_y BETWEEN 0 AND 100 AND artwork_width BETWEEN 5 AND 150 AND artwork_height BETWEEN 5 AND 200 AND artwork_scale BETWEEN 0.5 AND 2.5 AND artwork_rotation BETWEEN -180 AND 180))");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_catalogue_item_artwork",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "artwork_height",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "artwork_rotation",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "artwork_scale",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "artwork_width",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "artwork_x",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "artwork_y",
            table: "catalogue_items");

        migrationBuilder.DropColumn(
            name: "stats_guidance_hidden",
            table: "accounts");
    }
}
