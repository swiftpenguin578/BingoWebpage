using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddWiseOldManConnectionProvenance : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "provenance",
            table: "event_competition_synchronizations",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "Unknown");

        migrationBuilder.AddColumn<string>(
            name: "credential_status",
            table: "event_competition_management",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "Unavailable");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "credential_updated_at",
            table: "event_competition_management",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "credential_validated_at",
            table: "event_competition_management",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "provenance",
            table: "event_competition_management",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "Unknown");

        migrationBuilder.AddColumn<string>(
            name: "write_capability",
            table: "event_competition_management",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "Unknown");

        // Only a durable successful Create receipt proves that a
        // competition was website-created. ID-only links without a
        // management record are external/read-only. Anything else stays
        // Unknown and therefore never gains remote-delete authority.
        migrationBuilder.Sql("""
                UPDATE event_competition_synchronizations AS s
                SET provenance = 'WebsiteCreated'
                WHERE EXISTS (
                    SELECT 1
                    FROM event_competition_management_operations AS o
                    WHERE o.event_id = s.event_id
                      AND o.operation_type = 'Create'
                      AND o.phase = 'Succeeded'
                      AND o.remote_competition_id = s.competition_id
                      AND o.remote_receipt_reference IS NOT NULL
                );

                UPDATE event_competition_synchronizations AS s
                SET provenance = 'External'
                WHERE s.competition_id IS NOT NULL
                  AND s.provenance = 'Unknown'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM event_competition_management AS m
                      WHERE m.synchronization_id = s.id
                  );

                UPDATE event_competition_management AS m
                SET provenance = CASE
                        WHEN s.provenance = 'WebsiteCreated' THEN 'WebsiteCreated'
                        WHEN s.provenance = 'External' THEN 'External'
                        ELSE 'Unknown'
                    END,
                    write_capability = CASE
                        WHEN NULLIF(m.protected_verification_code, '') IS NOT NULL
                             AND s.provenance IN ('WebsiteCreated', 'External') THEN 'Writable'
                        ELSE 'ReadOnly'
                    END,
                    credential_status = CASE
                        WHEN NULLIF(m.protected_verification_code, '') IS NULL THEN 'Unavailable'
                        WHEN s.provenance = 'WebsiteCreated' THEN 'Valid'
                        WHEN s.provenance = 'External' THEN 'Unverified'
                        ELSE 'Unavailable'
                    END,
                    credential_updated_at = CASE
                        WHEN NULLIF(m.protected_verification_code, '') IS NOT NULL THEN m.updated_at
                        ELSE NULL
                    END,
                    credential_validated_at = CASE
                        WHEN s.provenance = 'WebsiteCreated'
                             AND NULLIF(m.protected_verification_code, '') IS NOT NULL THEN m.updated_at
                        ELSE NULL
                    END
                FROM event_competition_synchronizations AS s
                WHERE s.id = m.synchronization_id;
                """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "provenance",
            table: "event_competition_synchronizations");

        migrationBuilder.DropColumn(
            name: "credential_status",
            table: "event_competition_management");

        migrationBuilder.DropColumn(
            name: "credential_updated_at",
            table: "event_competition_management");

        migrationBuilder.DropColumn(
            name: "credential_validated_at",
            table: "event_competition_management");

        migrationBuilder.DropColumn(
            name: "provenance",
            table: "event_competition_management");

        migrationBuilder.DropColumn(
            name: "write_capability",
            table: "event_competition_management");
    }
}
