using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDraftPublicationMethod : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "publication_method",
            table: "draft_publication_cycles",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "HistoricalUnknown");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "publication_method",
            table: "draft_publication_cycles");
    }
}
