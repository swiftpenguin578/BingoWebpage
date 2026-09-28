#pragma warning disable IDE0161, CA1861
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddBoardEstimateFreshness : Migration
{
    private static readonly string[] EstimateFreshnessIndexColumns = ["board_id", "estimate_needs_verification"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
                name: "estimate_calculated_at",
                table: "board_tiles",
                type: "timestamp with time zone",
                nullable: true);

        migrationBuilder.AddColumn<string>(
                name: "estimate_catalogue_fingerprint",
                table: "board_tiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

        migrationBuilder.AddColumn<bool>(
                name: "estimate_needs_verification",
                table: "board_tiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

        migrationBuilder.CreateIndex(
                name: "IX_board_tiles_board_id_estimate_needs_verification",
                table: "board_tiles",
                columns: EstimateFreshnessIndexColumns);

        // Existing unfinished working copies must be checked against the
        // current catalogue on their next Board read. Approved/private and
        // public snapshots are intentionally left frozen and untouched.
        migrationBuilder.Sql("""
                UPDATE board_tiles AS tile
                SET estimate_needs_verification = TRUE
                FROM boards AS board
                WHERE tile.board_id = board.id
                  AND (board.state = 'Draft'
                       OR (board.state = 'Published' AND board.published_correction_in_progress = TRUE));
                """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
                name: "IX_board_tiles_board_id_estimate_needs_verification",
                table: "board_tiles");

        migrationBuilder.DropColumn(
                name: "estimate_calculated_at",
                table: "board_tiles");

        migrationBuilder.DropColumn(
                name: "estimate_catalogue_fingerprint",
                table: "board_tiles");

        migrationBuilder.DropColumn(
                name: "estimate_needs_verification",
                table: "board_tiles");
    }
}
