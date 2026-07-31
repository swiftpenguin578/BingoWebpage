using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bingo.Infrastructure.Persistence.Configurations;

public sealed class TeamFocusMarkerConfiguration : IEntityTypeConfiguration<TeamFocusMarker>
{
    public void Configure(EntityTypeBuilder<TeamFocusMarker> builder)
    {
        builder.ToTable("team_focus_markers", table =>
        {
            table.HasCheckConstraint(
                "ck_team_focus_markers_target_shape",
                "(target_kind = 'Tile' AND board_tile_id IS NOT NULL AND row_index IS NULL AND column_index IS NULL) OR " +
                "(target_kind = 'Row' AND board_tile_id IS NULL AND row_index >= 0 AND column_index IS NULL) OR " +
                "(target_kind = 'Column' AND board_tile_id IS NULL AND row_index IS NULL AND column_index >= 0)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EventId).HasColumnName("event_id");
        builder.Property(x => x.TeamId).HasColumnName("team_id");
        builder.Property(x => x.TargetKind).HasColumnName("target_kind").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.BoardTileId).HasColumnName("board_tile_id");
        builder.Property(x => x.RowIndex).HasColumnName("row_index");
        builder.Property(x => x.ColumnIndex).HasColumnName("column_index");
        builder.Property(x => x.Focused).HasColumnName("focused");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedByAccountId).HasColumnName("updated_by_account_id");
        builder.HasIndex(x => new { x.TeamId, x.BoardTileId }).IsUnique().HasFilter("target_kind = 'Tile'");
        builder.HasIndex(x => new { x.TeamId, x.RowIndex }).IsUnique().HasFilter("target_kind = 'Row'");
        builder.HasIndex(x => new { x.TeamId, x.ColumnIndex }).IsUnique().HasFilter("target_kind = 'Column'");
        builder.HasOne<BingoEvent>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Team>().WithMany()
            .HasForeignKey(x => new { x.EventId, x.TeamId })
            .HasPrincipalKey(x => new { x.EventId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BoardTile>().WithMany().HasForeignKey(x => x.BoardTileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.UpdatedByAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
