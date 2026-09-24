using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddManagedWiseOldManCompetitionManagement : Migration
{
    private static readonly string[] CompetitionStatusIndexColumns = ["competition_id", "status"];
    private static readonly string[] OperationFingerprintIndexColumns = ["event_id", "desired_fingerprint", "phase"];
    private static readonly string[] OperationActiveIndexColumns = ["event_id", "operation_type"];
    private static readonly string[] OperationScheduleIndexColumns = ["event_id", "operation_type", "phase", "next_attempt_at"];
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "event_competition_management",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                synchronization_id = table.Column<Guid>(type: "uuid", nullable: false),
                competition_id = table.Column<long>(type: "bigint", nullable: false),
                competition_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                competition_starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                competition_ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                protected_verification_code = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                managed_field_scope = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                last_applied_local_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                last_applied_remote_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                last_acknowledged_roster_json = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: true),
                management_version = table.Column<long>(type: "bigint", nullable: false),
                last_applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_error_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_error_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                last_operation_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                actual_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_event_competition_management", x => x.id);
                table.ForeignKey(
                    name: "FK_event_competition_management_event_competition_synchronizat~",
                    column: x => x.synchronization_id,
                    principalTable: "event_competition_synchronizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_event_competition_management_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "event_competition_management_operations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                management_id = table.Column<Guid>(type: "uuid", nullable: true),
                operation_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                desired_payload_json = table.Column<string>(type: "character varying(200000)", maxLength: 200000, nullable: false),
                desired_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                event_version = table.Column<long>(type: "bigint", nullable: false),
                actor_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                actor_username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                remote_competition_id = table.Column<long>(type: "bigint", nullable: true),
                remote_receipt_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                safe_error_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                safe_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                sending_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_event_competition_management_operations", x => x.id);
                table.ForeignKey(
                    name: "FK_event_competition_management_operations_accounts_actor_acco~",
                    column: x => x.actor_account_id,
                    principalTable: "accounts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_event_competition_management_operations_event_competition_m~",
                    column: x => x.management_id,
                    principalTable: "event_competition_management",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_event_competition_management_operations_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_management_competition_id_status",
            table: "event_competition_management",
            columns: CompetitionStatusIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_management_event_id",
            table: "event_competition_management",
            column: "event_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_management_synchronization_id",
            table: "event_competition_management",
            column: "synchronization_id");

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_management_operations_actor_account_id",
            table: "event_competition_management_operations",
            column: "actor_account_id");

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_management_operations_event_id_desired_fi~",
            table: "event_competition_management_operations",
            columns: OperationFingerprintIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_management_operations_event_id_operation_~",
            table: "event_competition_management_operations",
            columns: OperationActiveIndexColumns,
            unique: true,
            filter: "phase IN ('Pending', 'Claimed', 'Sending', 'Retry', 'Unknown')");

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_management_operations_event_id_operation~1",
            table: "event_competition_management_operations",
            columns: OperationScheduleIndexColumns);

        migrationBuilder.CreateIndex(
            name: "IX_event_competition_management_operations_management_id",
            table: "event_competition_management_operations",
            column: "management_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "event_competition_management_operations");

        migrationBuilder.DropTable(
            name: "event_competition_management");
    }
}
