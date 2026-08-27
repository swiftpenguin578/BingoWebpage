using Bingo.Domain.Teams;
using Bingo.Web.Pages.Captain;

namespace Bingo.IntegrationTests;

public sealed class CaptainFocusInputTranslationTests
{
    [Fact]
    public void FocusTargetTranslationKeepsTargetIdentityWithItsSelectedType()
    {
        var tileId = Guid.NewGuid();

        Assert.True(IndexModel.TryTranslateFocusTarget("Tile", $"{tileId}|0", out var tileKind, out var translatedTileId, out var tileRow, out var tileColumn, out var tileVersion));
        Assert.Equal(TeamFocusTargetKind.Tile, tileKind);
        Assert.Equal(tileId, translatedTileId);
        Assert.Null(tileRow);
        Assert.Null(tileColumn);
        Assert.Equal(0, tileVersion);

        Assert.True(IndexModel.TryTranslateFocusTarget("Row", "2|7", out var rowKind, out var rowTileId, out var translatedRow, out var rowColumn, out var rowVersion));
        Assert.Equal(TeamFocusTargetKind.Row, rowKind);
        Assert.Null(rowTileId);
        Assert.Equal(2, translatedRow);
        Assert.Null(rowColumn);
        Assert.Equal(7, rowVersion);

        Assert.True(IndexModel.TryTranslateFocusTarget("Column", "3|4", out var columnKind, out var columnTileId, out var columnRow, out var translatedColumn, out var columnVersion));
        Assert.Equal(TeamFocusTargetKind.Column, columnKind);
        Assert.Null(columnTileId);
        Assert.Null(columnRow);
        Assert.Equal(3, translatedColumn);
        Assert.Equal(4, columnVersion);

        Assert.False(IndexModel.TryTranslateFocusTarget("Tile", "2|0", out _, out _, out _, out _, out _));
        Assert.False(IndexModel.TryTranslateFocusTarget("Row", $"{tileId}|7", out _, out _, out _, out _, out _));
        Assert.False(IndexModel.TryTranslateFocusTarget("Column", "3|", out _, out _, out _, out _, out _));
        Assert.False(IndexModel.TryTranslateFocusTarget("Column", "3|-1", out _, out _, out _, out _, out _));
        Assert.False(IndexModel.TryTranslateFocusTarget("Tile", $"{tileId}|1|extra", out _, out _, out _, out _, out _));
        Assert.False(IndexModel.TryTranslateFocusTarget("0", $"{tileId}|0", out _, out _, out _, out _, out _));
    }
}
