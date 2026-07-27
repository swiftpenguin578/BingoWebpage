using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddScheduledLifecycleExecution : Migration
{
    private static readonly string[] OpeningAttemptResolutionColumns = ["event_id", "resolved_at"];
    private static readonly string[] OpeningAttemptIdentityColumns = ["event_id", "scheduled_for"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "scheduled_signup_opening_enabled",
            table: "events",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "scheduled_signup_warning_codes",
            table: "events",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateTable(
            name: "scheduled_signup_opening_attempts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                scheduled_for = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                opened = table.Column<bool>(type: "boolean", nullable: false),
                blocker_codes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                blocker_details = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_scheduled_signup_opening_attempts", x => x.id);
                table.ForeignKey(
                    name: "FK_scheduled_signup_opening_attempts_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_scheduled_signup_opening_attempts_event_id_resolved_at",
            table: "scheduled_signup_opening_attempts",
            columns: OpeningAttemptResolutionColumns);

        migrationBuilder.CreateIndex(
            name: "IX_scheduled_signup_opening_attempts_event_id_scheduled_for",
            table: "scheduled_signup_opening_attempts",
            columns: OpeningAttemptIdentityColumns,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "scheduled_signup_opening_attempts");

        migrationBuilder.DropColumn(
            name: "scheduled_signup_opening_enabled",
            table: "events");

        migrationBuilder.DropColumn(
            name: "scheduled_signup_warning_codes",
            table: "events");
    }
}
