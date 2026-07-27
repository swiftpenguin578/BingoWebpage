#pragma warning disable IDE0161, CA1861

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlice3GuidedCreationAndIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_banner_assets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_event_banner_assets", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_events_banner_asset_id",
                table: "events",
                column: "banner_asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_event_banner_assets_event_id_replaced_at",
                table: "event_banner_assets",
                columns: new[] { "event_id", "replaced_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_events_event_banner_assets_banner_asset_id",
                table: "events",
                column: "banner_asset_id",
                principalTable: "event_banner_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_events_event_banner_assets_banner_asset_id",
                table: "events");

            migrationBuilder.DropTable(
                name: "event_banner_assets");

            migrationBuilder.DropIndex(
                name: "IX_events_banner_asset_id",
                table: "events");
        }
    }
}
