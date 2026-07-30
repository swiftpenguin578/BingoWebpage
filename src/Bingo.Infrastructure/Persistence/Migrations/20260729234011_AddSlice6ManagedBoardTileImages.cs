using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSlice6ManagedBoardTileImages : Migration
{
    private static readonly string[] ImageAssetIndexColumns = ["board_tile_id", "replaced_at"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "active_image_asset_id",
            table: "board_tiles",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "board_tile_image_assets",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                board_tile_id = table.Column<Guid>(type: "uuid", nullable: false),
                storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                original_filename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                media_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                byte_size = table.Column<long>(type: "bigint", nullable: false),
                width = table.Column<int>(type: "integer", nullable: false),
                height = table.Column<int>(type: "integer", nullable: false),
                checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                uploaded_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                replaced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_board_tile_image_assets", x => x.id);
                table.ForeignKey(
                    name: "FK_board_tile_image_assets_board_tiles_board_tile_id",
                    column: x => x.board_tile_id,
                    principalTable: "board_tiles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_board_tiles_active_image_asset_id",
            table: "board_tiles",
            column: "active_image_asset_id");

        migrationBuilder.CreateIndex(
            name: "IX_board_tile_image_assets_board_tile_id_replaced_at",
            table: "board_tile_image_assets",
            columns: ImageAssetIndexColumns);

        migrationBuilder.AddForeignKey(
            name: "FK_board_tiles_board_tile_image_assets_active_image_asset_id",
            table: "board_tiles",
            column: "active_image_asset_id",
            principalTable: "board_tile_image_assets",
            principalColumn: "id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_board_tiles_board_tile_image_assets_active_image_asset_id",
            table: "board_tiles");

        migrationBuilder.DropTable(
            name: "board_tile_image_assets");

        migrationBuilder.DropIndex(
            name: "IX_board_tiles_active_image_asset_id",
            table: "board_tiles");

        migrationBuilder.DropColumn(
            name: "active_image_asset_id",
            table: "board_tiles");
    }
}
