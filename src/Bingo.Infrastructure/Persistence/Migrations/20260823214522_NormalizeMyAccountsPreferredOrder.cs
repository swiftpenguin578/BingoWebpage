using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class NormalizeMyAccountsPreferredOrder : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE account_osrs_characters
            SET preferred = FALSE
            WHERE active;

            WITH ranked AS (
                SELECT "Id" AS id,
                       (ROW_NUMBER() OVER (
                           PARTITION BY "AccountId"
                           ORDER BY sort_order, linked_at, "Id") - 1)::integer AS new_position
                FROM account_osrs_characters
                WHERE active)
            UPDATE account_osrs_characters link
            SET sort_order = ranked.new_position,
                preferred = ranked.new_position = 0
            FROM ranked
            WHERE link."Id" = ranked.id;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The canonical order/preferred state cannot be safely reconstructed.
    }
}
