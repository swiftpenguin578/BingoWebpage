using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;
/// <inheritdoc />
public partial class RetainApprovalArtworkAfterTileRemoval : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_board_tile_image_assets_board_tiles_board_tile_id",
            table: "board_tile_image_assets");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddForeignKey(
            name: "FK_board_tile_image_assets_board_tiles_board_tile_id",
            table: "board_tile_image_assets",
            column: "board_tile_id",
            principalTable: "board_tiles",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }
}
