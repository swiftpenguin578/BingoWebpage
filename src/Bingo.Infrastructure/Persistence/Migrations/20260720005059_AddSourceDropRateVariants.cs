using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSourceDropRateVariants : Migration
{
    private static readonly string[] SourceDropPositionColumns = ["source_drop_id", "position"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
                name: "source_drop_rate_variants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_drop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    display_rate = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    numeric_probability = table.Column<decimal>(type: "numeric(18,12)", precision: 18, scale: 12, nullable: true),
                    condition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_drop_rate_variants", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_source_drop_rate_variants_source_drop_id_position",
                table: "source_drop_rate_variants",
                columns: SourceDropPositionColumns,
                unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "source_drop_rate_variants");
    }
}
