using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCompetitionEndUpdateState : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "end_update_error_code",
            table: "event_competition_synchronizations",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "end_update_requested_at",
            table: "event_competition_synchronizations",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "end_update_status",
            table: "event_competition_synchronizations",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "NotRequired");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "end_update_target_at",
            table: "event_competition_synchronizations",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "end_update_error_code",
            table: "event_competition_synchronizations");

        migrationBuilder.DropColumn(
            name: "end_update_requested_at",
            table: "event_competition_synchronizations");

        migrationBuilder.DropColumn(
            name: "end_update_status",
            table: "event_competition_synchronizations");

        migrationBuilder.DropColumn(
            name: "end_update_target_at",
            table: "event_competition_synchronizations");
    }
}
