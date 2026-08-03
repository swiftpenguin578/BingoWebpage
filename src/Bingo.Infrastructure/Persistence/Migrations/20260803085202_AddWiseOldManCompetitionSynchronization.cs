using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWiseOldManCompetitionSynchronization : Migration
{
    private static readonly string[] ActivityGenerationColumns = ["event_id", "generation"];
    private static readonly string[] ActivityCharacterColumns = ["event_id", "generation", "osrs_character_id"];
    private static readonly string[] SynchronizationDueColumns = ["event_id", "competition_id", "normal_due_at", "retry_due_at"];
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "event_competition_character_activity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                generation = table.Column<int>(type: "integer", nullable: false),
                competition_id = table.Column<long>(type: "bigint", nullable: false),
                osrs_character_id = table.Column<Guid>(type: "uuid", nullable: false),
                gained_ehb = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: false),
                fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                upstream_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                assignment_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_event_competition_character_activity", x => x.id);
                table.ForeignKey(
                    name: "FK_event_competition_character_activity_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_event_competition_character_activity_osrs_characters_osrs_c~",
                    column: x => x.osrs_character_id,
                    principalTable: "osrs_characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "event_competition_synchronizations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                generation = table.Column<int>(type: "integer", nullable: false),
                competition_id = table.Column<long>(type: "bigint", nullable: true),
                competition_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                competition_starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                competition_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_successful_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_upstream_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                assignment_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                latest_complete = table.Column<bool>(type: "boolean", nullable: true),
                missing_accounts_json = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                last_error_kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                cycle_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                normal_due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                retry_due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                retry_count = table.Column<int>(type: "integer", nullable: false),
                lease_owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                lease_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_event_competition_synchronizations", x => x.id);
                table.ForeignKey(
                    name: "FK_event_competition_synchronizations_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_character_activity_event_id_generation",
            table: "event_competition_character_activity",
            columns: ActivityGenerationColumns);

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_character_activity_event_id_generation_os~",
            table: "event_competition_character_activity",
            columns: ActivityCharacterColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_character_activity_osrs_character_id",
            table: "event_competition_character_activity",
            column: "osrs_character_id");

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_synchronizations_event_id_competition_id_~",
            table: "event_competition_synchronizations",
            columns: SynchronizationDueColumns);

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_synchronizations_event_id",
            table: "event_competition_synchronizations",
            column: "event_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "event_competition_character_activity");

        migrationBuilder.DropTable(
            name: "event_competition_synchronizations");
    }
}
