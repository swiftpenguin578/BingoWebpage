#pragma warning disable IDE0161, CA1861
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmergencyLifecycleAndPersonalNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "cutoff_disabled",
                table: "account_event_accesses",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Slice 1 replaces the old per-credential expiry with a durable event-cutoff
            // state transition. Otherwise a post-cutoff administrative re-enable would
            // authenticate as disabled before the lifecycle state could be consulted.
            migrationBuilder.Sql("UPDATE account_event_accesses SET expires_at = NULL;");

            migrationBuilder.CreateTable(
                name: "personal_notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Route = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personal_notifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_personal_notifications_RecipientAccountId_ReadAt_CreatedAt",
                table: "personal_notifications",
                columns: new[] { "RecipientAccountId", "ReadAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "personal_notifications");

            migrationBuilder.DropColumn(
                name: "cutoff_disabled",
                table: "account_event_accesses");
        }
    }
}
