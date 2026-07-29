using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddEventBannerCleanupOutbox : Migration
{
    private static readonly string[] EventCleanupIdentityColumns = ["event_id", "storage_key"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "event_banner_cleanups",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_failure = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                attempt_count = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_event_banner_cleanups", x => x.id);
                table.ForeignKey(
                    name: "FK_event_banner_cleanups_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_event_banner_cleanups_event_id_storage_key",
            table: "event_banner_cleanups",
            columns: EventCleanupIdentityColumns,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "event_banner_cleanups");
    }
}
