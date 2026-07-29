#pragma warning disable IDE0161, CA1861

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlice3LifecyclePersistenceFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "submission_cutoff_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "signup_opens_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "signup_closes_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<int>(
                name: "participant_cap",
                table: "events",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "event_starts_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "event_ends_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "events",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "actual_ended_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "actual_started_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "banner_asset_id",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason",
                table: "events",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelled_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by_account_id",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "discarded_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "discarded_by_account_id",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "draft_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "first_public_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "events",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            // Existing public lifecycle records predate first_public_at. Their retained signup opening is
            // the only available public-exposure boundary; the deterministic fallback preserves a value
            // for historical records with incomplete legacy schedules without inventing Draft history.
            migrationBuilder.Sql("""
                UPDATE events
                SET first_public_at = COALESCE(signup_opens_at, event_starts_at, created_at)
                WHERE state IN ('SignupOpen', 'SignupClosed', 'Live', 'AwaitingFinalReview', 'Finalized', 'Archived');
                """);

            // Before Slice 3 there was no separate actual timestamp. Retained completed lifecycle rows
            // used the configured instants as their authoritative effective instants, so preserve that fact.
            migrationBuilder.Sql("""
                UPDATE events
                SET actual_started_at = event_starts_at
                WHERE state IN ('Live', 'AwaitingFinalReview', 'Finalized', 'Archived')
                  AND event_starts_at IS NOT NULL;
                UPDATE events
                SET actual_ended_at = event_ends_at
                WHERE state IN ('AwaitingFinalReview', 'Finalized', 'Archived')
                  AND event_ends_at IS NOT NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "performed_by_account_id",
                table: "event_state_transitions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "scheduled",
                table: "event_state_transitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "scheduled_event_start_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_for = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started = table.Column<bool>(type: "boolean", nullable: false),
                    blocker_codes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_event_start_attempts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_event_start_attempts_event_id_resolved_at",
                table: "scheduled_event_start_attempts",
                columns: new[] { "event_id", "resolved_at" });

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_event_start_attempts_event_id_scheduled_for",
                table: "scheduled_event_start_attempts",
                columns: new[] { "event_id", "scheduled_for" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scheduled_event_start_attempts");

            migrationBuilder.DropColumn(
                name: "actual_ended_at",
                table: "events");

            migrationBuilder.DropColumn(
                name: "actual_started_at",
                table: "events");

            migrationBuilder.DropColumn(
                name: "banner_asset_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "cancellation_reason",
                table: "events");

            migrationBuilder.DropColumn(
                name: "cancelled_at",
                table: "events");

            migrationBuilder.DropColumn(
                name: "cancelled_by_account_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "discarded_at",
                table: "events");

            migrationBuilder.DropColumn(
                name: "discarded_by_account_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "draft_at",
                table: "events");

            migrationBuilder.DropColumn(
                name: "first_public_at",
                table: "events");

            migrationBuilder.DropColumn(
                name: "version",
                table: "events");

            migrationBuilder.DropColumn(
                name: "scheduled",
                table: "event_state_transitions");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "submission_cutoff_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "signup_opens_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "signup_closes_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "participant_cap",
                table: "events",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "event_starts_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "event_ends_at",
                table: "events",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "events",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "performed_by_account_id",
                table: "event_state_transitions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
