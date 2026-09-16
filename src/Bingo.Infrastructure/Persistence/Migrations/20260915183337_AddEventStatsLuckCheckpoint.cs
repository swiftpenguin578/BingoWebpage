using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;
/// <inheritdoc />
public partial class AddEventStatsLuckCheckpoint : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "stats_evidence_revision",
            table: "events",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.CreateTable(
            name: "event_stats_luck_checkpoints",
            columns: table => new
            {
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                schema_version = table.Column<int>(type: "integer", nullable: false),
                evidence_revision = table.Column<long>(type: "bigint", nullable: false),
                competition_id = table.Column<long>(type: "bigint", nullable: false),
                generation = table.Column<int>(type: "integer", nullable: false),
                activity_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                assignment_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                source_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                lifecycle_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                calculated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                fetched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                upstream_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                payload = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_event_stats_luck_checkpoints", x => x.event_id);
                table.CheckConstraint("ck_stats_checkpoint_payload", "schema_version = 1 AND evidence_revision >= 0 AND octet_length(payload::text) <= 8388608 AND jsonb_typeof(payload) = 'object'");
                table.ForeignKey(
                    name: "FK_event_stats_luck_checkpoints_events_event_id",
                    column: x => x.event_id,
                    principalTable: "events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "event_stats_luck_checkpoints");

        migrationBuilder.DropColumn(
            name: "stats_evidence_revision",
            table: "events");
    }
}
