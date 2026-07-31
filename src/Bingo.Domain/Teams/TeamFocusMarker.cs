namespace Bingo.Domain.Teams;

public sealed class TeamFocusMarker
{
    private TeamFocusMarker() { }

    public TeamFocusMarker(
        Guid id,
        Guid eventId,
        Guid teamId,
        TeamFocusTargetKind targetKind,
        Guid? boardTileId,
        int? rowIndex,
        int? columnIndex,
        bool focused,
        DateTimeOffset updatedAt,
        Guid? updatedByAccountId)
    {
        ValidateTarget(targetKind, boardTileId, rowIndex, columnIndex);
        Id = id;
        EventId = eventId;
        TeamId = teamId;
        TargetKind = targetKind;
        BoardTileId = boardTileId;
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        Focused = focused;
        Version = 1;
        UpdatedAt = updatedAt.ToUniversalTime();
        UpdatedByAccountId = updatedByAccountId;
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid TeamId { get; private set; }
    public TeamFocusTargetKind TargetKind { get; private set; }
    public Guid? BoardTileId { get; private set; }
    public int? RowIndex { get; private set; }
    public int? ColumnIndex { get; private set; }
    public bool Focused { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid? UpdatedByAccountId { get; private set; }

    public void SetFocused(bool focused, DateTimeOffset updatedAt, Guid? updatedByAccountId)
    {
        Focused = focused;
        UpdatedAt = updatedAt.ToUniversalTime();
        UpdatedByAccountId = updatedByAccountId;
        Version++;
    }

    private static void ValidateTarget(
        TeamFocusTargetKind targetKind,
        Guid? boardTileId,
        int? rowIndex,
        int? columnIndex)
    {
        switch (targetKind)
        {
            case TeamFocusTargetKind.Tile when boardTileId is not null && rowIndex is null && columnIndex is null:
            case TeamFocusTargetKind.Row when boardTileId is null && rowIndex is >= 0 && columnIndex is null:
            case TeamFocusTargetKind.Column when boardTileId is null && rowIndex is null && columnIndex is >= 0:
                return;
            default:
                throw new ArgumentException("A team focus marker must have exactly one valid target identity.");
        }
    }
}
