using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace Bingo.IntegrationTests;

public sealed class BoardEstimateFreshnessMigrationTests : IAsyncLifetime
{
    private const string BeforeFreshness = "20260926201553_AddWiseOldManConnectionProvenance";
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bingo_board_estimate_freshness")
        .WithUsername("bingo")
        .WithPassword("bingo_test_password")
        .Build();

    private DbContextOptions<ApplicationDbContext> options = null!;

    public async Task InitializeAsync()
    {
        await PostgreSqlReadiness.StartAsync(database);
        options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(database.GetConnectionString())
            .Options;
    }

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task BackfillMarksOnlyUnfinishedBoardsAndPreservesExistingEstimates()
    {
        var draftBoardId = Guid.NewGuid();
        var correctionBoardId = Guid.NewGuid();
        var publishedBoardId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var draftTileId = Guid.NewGuid();
        var correctionTileId = Guid.NewGuid();
        var publishedTileId = Guid.NewGuid();

        await using (var before = new ApplicationDbContext(options))
        {
            await before.Database.GetService<IMigrator>().MigrateAsync(BeforeFreshness);
            await before.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO boards
                    (id, event_id, name, rows, columns, state, published_at,
                     total_ehb_estimate, calculation_version, version,
                     active_approval_snapshot_id, published_correction_in_progress,
                     editor_account_id, editor_lease_expires_at, edit_control_version)
                VALUES
                    ({draftBoardId}, {eventId}, {"Draft board"}, 1, 1, {"Draft"}, NULL,
                     {12.3456m}, 1, 4, NULL, FALSE, NULL, NULL, 2),
                    ({correctionBoardId}, {Guid.NewGuid()}, {"Correction board"}, 1, 1, {"Published"}, {DateTimeOffset.UtcNow},
                     {23.4567m}, 1, 8, NULL, TRUE, NULL, NULL, 3),
                    ({publishedBoardId}, {Guid.NewGuid()}, {"Published board"}, 1, 1, {"Published"}, {DateTimeOffset.UtcNow},
                     {34.5678m}, 1, 9, NULL, FALSE, NULL, NULL, 4);

                INSERT INTO board_tiles
                    (id, board_id, tile_template_id, row_index, column_index,
                     name_snapshot, description_snapshot, evidence_instructions_snapshot,
                     estimated_ehb_snapshot, image_url_snapshot, description_is_automatic)
                VALUES
                    ({draftTileId}, {draftBoardId}, {Guid.NewGuid()}, 0, 0,
                     {"Draft tile"}, {"draft"}, {"draft evidence"}, {12.3456m}, NULL, FALSE),
                    ({correctionTileId}, {correctionBoardId}, {Guid.NewGuid()}, 0, 0,
                     {"Correction tile"}, {"correction"}, {"correction evidence"}, {23.4567m}, NULL, FALSE),
                    ({publishedTileId}, {publishedBoardId}, {Guid.NewGuid()}, 0, 0,
                     {"Published tile"}, {"published"}, {"published evidence"}, {34.5678m}, NULL, FALSE);
                """);
        }

        await using (var migrated = new ApplicationDbContext(options))
        {
            await migrated.Database.MigrateAsync();
        }

        await using var verify = new ApplicationDbContext(options);
        var tiles = await verify.Database.SqlQuery<TileFreshness>($"""
            SELECT board_id AS "BoardId",
                   estimated_ehb_snapshot AS "EstimatedEhb",
                   estimate_catalogue_fingerprint AS "Fingerprint",
                   estimate_calculated_at AS "CalculatedAt",
                   estimate_needs_verification AS "NeedsVerification"
            FROM board_tiles
            WHERE id IN ({draftTileId}, {correctionTileId}, {publishedTileId})
            ORDER BY board_id
            """).ToListAsync();

        Assert.Equal(3, tiles.Count);
        Assert.Equal(12.3456m, tiles.Single(tile => tile.BoardId == draftBoardId).EstimatedEhb);
        Assert.Equal(23.4567m, tiles.Single(tile => tile.BoardId == correctionBoardId).EstimatedEhb);
        Assert.Equal(34.5678m, tiles.Single(tile => tile.BoardId == publishedBoardId).EstimatedEhb);

        Assert.True(tiles.Single(tile => tile.BoardId == draftBoardId).NeedsVerification);
        Assert.True(tiles.Single(tile => tile.BoardId == correctionBoardId).NeedsVerification);
        Assert.False(tiles.Single(tile => tile.BoardId == publishedBoardId).NeedsVerification);
        Assert.All(tiles, tile =>
        {
            Assert.Null(tile.Fingerprint);
            Assert.Null(tile.CalculatedAt);
        });
    }

    private sealed class TileFreshness
    {
        public Guid BoardId { get; set; }
        public decimal EstimatedEhb { get; set; }
        public string? Fingerprint { get; set; }
        public DateTimeOffset? CalculatedAt { get; set; }
        public bool NeedsVerification { get; set; }
    }
}
