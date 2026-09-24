using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

public partial class AddWiseOldManUpdateAllSlots : Migration
{
    private static readonly string[] SlotIdentityColumns = ["competition_id", "paired_fetch_at"];
    private static readonly string[] SlotScheduleColumns = ["event_id", "scheduled_at", "status"];
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "event_competition_update_all_slots",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                management_id = table.Column<Guid>(type: "uuid", nullable: false),
                synchronization_id = table.Column<Guid>(type: "uuid", nullable: false),
                competition_id = table.Column<long>(type: "bigint", nullable: false),
                actual_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                paired_fetch_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                management_version = table.Column<long>(type: "bigint", nullable: false),
                synchronization_generation = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                outcome_code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                outcome = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_event_competition_update_all_slots", x => x.id);
                table.ForeignKey(
                    name: "FK_event_competition_update_all_slots_event_competition_manage~",
                    column: x => x.management_id,
                    principalTable: "event_competition_management",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_event_competition_update_all_slots_event_competition_synchr~",
                    column: x => x.synchronization_id,
                    principalTable: "event_competition_synchronizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_event_competition_update_all_slots_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_update_all_slots_competition_id_paired_fe~",
            table: "event_competition_update_all_slots",
            columns: SlotIdentityColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_update_all_slots_event_id_scheduled_at_st~",
            table: "event_competition_update_all_slots",
            columns: SlotScheduleColumns);

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_update_all_slots_management_id",
            table: "event_competition_update_all_slots",
            column: "management_id");

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_update_all_slots_synchronization_id",
            table: "event_competition_update_all_slots",
            column: "synchronization_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "event_competition_update_all_slots");
    }
}
