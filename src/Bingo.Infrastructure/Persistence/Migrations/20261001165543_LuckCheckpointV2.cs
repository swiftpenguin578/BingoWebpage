using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class LuckCheckpointV2 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_stats_checkpoint_payload",
            table: "event_stats_luck_checkpoints");

        migrationBuilder.AddColumn<string>(
            name: "algorithm_version",
            table: "event_stats_luck_checkpoints",
            type: "character varying(80)",
            maxLength: 80,
            nullable: false,
            defaultValue: "legacy-signed-v1");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "converted_at",
            table: "event_stats_luck_checkpoints",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "converted_from_schema_version",
            table: "event_stats_luck_checkpoints",
            type: "integer",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_stats_checkpoint_payload",
            table: "event_stats_luck_checkpoints",
            sql: "schema_version IN (1, 2) AND evidence_revision >= 0 AND octet_length(payload::text) <= 8388608 AND jsonb_typeof(payload) = 'object'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Downgrade is deliberately fail-closed: v2 rows carry algorithm and
        // conversion provenance that the legacy schema cannot represent. An
        // operator must export/resolve those rows before retrying the downgrade.
        migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM event_stats_luck_checkpoints WHERE schema_version = 2) THEN
                        RAISE EXCEPTION 'LuckCheckpointV2 downgrade blocked: schema_version=2 rows exist; export or resolve retained v2 history before retrying.';
                    END IF;
                END
                $$;
                """);

        migrationBuilder.DropCheckConstraint(
            name: "ck_stats_checkpoint_payload",
            table: "event_stats_luck_checkpoints");

        migrationBuilder.DropColumn(
            name: "algorithm_version",
            table: "event_stats_luck_checkpoints");

        migrationBuilder.DropColumn(
            name: "converted_at",
            table: "event_stats_luck_checkpoints");

        migrationBuilder.DropColumn(
            name: "converted_from_schema_version",
            table: "event_stats_luck_checkpoints");

        migrationBuilder.AddCheckConstraint(
            name: "ck_stats_checkpoint_payload",
            table: "event_stats_luck_checkpoints",
            sql: "schema_version = 1 AND evidence_revision >= 0 AND octet_length(payload::text) <= 8388608 AND jsonb_typeof(payload) = 'object'");
    }
}
