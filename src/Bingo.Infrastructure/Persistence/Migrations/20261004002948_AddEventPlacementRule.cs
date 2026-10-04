using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;
/// <inheritdoc />
public partial class AddEventPlacementRule : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "placement_rule",
            table: "events",
            type: "integer",
            nullable: false,
            defaultValue: 0);
        // Every retained row is legacy. Future inserts must state their rule explicitly.
        migrationBuilder.Sql("ALTER TABLE events ALTER COLUMN placement_rule DROP DEFAULT");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "placement_rule",
            table: "events");
    }
}
