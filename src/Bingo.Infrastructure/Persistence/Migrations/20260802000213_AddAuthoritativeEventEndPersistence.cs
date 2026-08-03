using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAuthoritativeEventEndPersistence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "submissions_closed_at",
            table: "events",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "effective_at",
            table: "event_state_transitions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql("UPDATE event_state_transitions SET effective_at = performed_at WHERE effective_at IS NULL;");

        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "effective_at",
            table: "event_state_transitions",
            type: "timestamp with time zone",
            nullable: false,
            oldClrType: typeof(DateTimeOffset),
            oldType: "timestamp with time zone",
            oldNullable: true);

        migrationBuilder.Sql("""
                UPDATE events
                SET submissions_closed_at = CASE
                    WHEN reopened_submission_cutoff_at IS NOT NULL
                         AND (submission_cutoff_at IS NULL OR reopened_submission_cutoff_at > submission_cutoff_at)
                        THEN reopened_submission_cutoff_at
                    ELSE submission_cutoff_at
                END
                WHERE state IN ('AwaitingFinalReview', 'Finalized', 'Archived')
                  AND submission_cutoff_at IS NOT NULL
                  AND (CASE
                        WHEN reopened_submission_cutoff_at IS NOT NULL
                             AND (submission_cutoff_at IS NULL OR reopened_submission_cutoff_at > submission_cutoff_at)
                            THEN reopened_submission_cutoff_at
                        ELSE submission_cutoff_at
                       END) <= CURRENT_TIMESTAMP;
                """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "submissions_closed_at",
            table: "events");

        migrationBuilder.DropColumn(
            name: "effective_at",
            table: "event_state_transitions");
    }
}
