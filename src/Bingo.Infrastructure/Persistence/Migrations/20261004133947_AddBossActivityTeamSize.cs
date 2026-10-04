using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddBossActivityTeamSize : Migration
{
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM source_drops
                        WHERE assumed_participants <> 1
                           OR probability_scope <> 'Participant'
                    ) THEN
                        RAISE EXCEPTION 'Cannot add activity team size while source drops contain non-default participant context. Resolve the source-drop context and retry the migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<int>(
                name: "team_size",
                table: "boss_activities",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "ck_boss_activity_team_size",
                table: "boss_activities",
                sql: "team_size >= 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_boss_activity_team_size",
                table: "boss_activities");

            migrationBuilder.DropColumn(
                name: "team_size",
                table: "boss_activities");
        }
}
