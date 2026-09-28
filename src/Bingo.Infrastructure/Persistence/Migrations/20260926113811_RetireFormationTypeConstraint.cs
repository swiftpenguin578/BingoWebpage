using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RetireFormationTypeConstraint : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_teams_formation_draft",
            table: "teams");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddCheckConstraint(
            name: "ck_teams_formation_draft",
            table: "teams",
            sql: "(formation_type = 'Drafted' AND included_in_draft) OR (formation_type = 'Preformed' AND NOT included_in_draft)");
    }
}
