using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bingo.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class NormalizeCompletedTileFocusMarkers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
                UPDATE team_focus_markers AS marker
                SET focused = FALSE,
                    version = marker.version + 1,
                    updated_at = CURRENT_TIMESTAMP,
                    updated_by_account_id = NULL
                WHERE marker.target_kind = 'Tile'
                  AND marker.focused
                  AND EXISTS (
                      SELECT 1
                      FROM board_requirement_snapshots AS requirement
                      WHERE requirement.board_tile_id = marker.board_tile_id
                  )
                  AND NOT EXISTS (
                      SELECT 1
                      FROM board_requirement_snapshots AS requirement
                      WHERE requirement.board_tile_id = marker.board_tile_id
                        AND COALESCE((
                            SELECT SUM(contribution.amount)
                            FROM submission_contributions AS contribution
                            WHERE contribution.team_id = marker.team_id
                              AND contribution.requirement_id = requirement.id
                              AND contribution.reversed_at IS NULL
                        ), 0) < requirement.target_contribution
                  );
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
