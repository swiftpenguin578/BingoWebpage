using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

public partial class ClearLegacyDropTileEhbOverrides : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            DECLARE cleared_count integer;
            BEGIN
                UPDATE tile_templates SET manual_ehb_override = NULL
                WHERE objective_type = 'DropRequirements' AND manual_ehb_override IS NOT NULL;
                GET DIAGNOSTICS cleared_count = ROW_COUNT;
                RAISE NOTICE 'AU11 legacy drop-tile overrides cleared: %', cleared_count;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately non-restorable data cleanup, approved in remediation D1.
        // Forgotten overrides must not be reactivated by a rollback.
    }
}
