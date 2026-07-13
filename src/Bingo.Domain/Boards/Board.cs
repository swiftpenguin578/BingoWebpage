namespace Bingo.Domain.Boards;

public sealed class Board
{
    private Board() { }
    public Board(Guid id, Guid eventId, string name, int rows, int columns) { ValidateDimensions(rows, columns); Id = id; EventId = eventId; Name = name; Rows = rows; Columns = columns; State = BoardState.Draft; CalculationVersion = 1; Version = 1; EditControlVersion = 1; }
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string Name { get; private set; } = string.Empty; public int Rows { get; private set; }
    public int Columns { get; private set; }
    public BoardState State { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public decimal TotalEhbEstimate { get; private set; }
    public int CalculationVersion { get; private set; }
    public long Version { get; private set; }
    public Guid? EditorAccountId { get; private set; }
    public DateTimeOffset? EditorLeaseExpiresAt { get; private set; }
    public long EditControlVersion { get; private set; }
    public void Resize(int rows, int columns, int placedTileCount) { EnsureDraft(); ValidateDimensions(rows, columns); if (placedTileCount > rows * columns) throw new InvalidOperationException($"Remove {placedTileCount - rows * columns} tile(s) before shrinking the board."); Rows = rows; Columns = columns; }
    public void SetTotalEhb(decimal total) => TotalEhbEstimate = total;
    public void MarkChanged() => Version++;
    public bool HasActiveEditor(DateTimeOffset now) => EditorAccountId is not null && EditorLeaseExpiresAt > now.ToUniversalTime();
    public Guid? AcquireEditing(Guid accountId, DateTimeOffset now, TimeSpan leaseDuration, bool force = false)
    {
        EnsureDraft();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        var at = now.ToUniversalTime();
        var previous = HasActiveEditor(at) ? EditorAccountId : null;
        if (previous is not null && previous != accountId && !force) throw new InvalidOperationException("Another administrator is currently editing this board.");
        EditorAccountId = accountId;
        EditorLeaseExpiresAt = at.Add(leaseDuration);
        EditControlVersion++;
        return previous;
    }
    public void RenewEditing(Guid accountId, DateTimeOffset now, TimeSpan leaseDuration)
    {
        RequireEditing(accountId, now);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        EditorLeaseExpiresAt = now.ToUniversalTime().Add(leaseDuration);
    }
    public void RequireEditing(Guid accountId, DateTimeOffset now)
    {
        if (!HasActiveEditor(now) || EditorAccountId != accountId) throw new InvalidOperationException("You no longer have editing control of this board. The latest board has been loaded.");
    }
    public void ReleaseEditing(Guid accountId, DateTimeOffset now)
    {
        RequireEditing(accountId, now);
        EditorAccountId = null;
        EditorLeaseExpiresAt = null;
        EditControlVersion++;
    }
    public void Publish(DateTimeOffset now) { EnsureDraft(); State = BoardState.Published; PublishedAt = now.ToUniversalTime(); }
    private void EnsureDraft() { if (State != BoardState.Draft) throw new InvalidOperationException("Published boards cannot be edited normally."); }
    private static void ValidateDimensions(int rows, int columns) { ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1); ArgumentOutOfRangeException.ThrowIfGreaterThan(rows, 20); ArgumentOutOfRangeException.ThrowIfLessThan(columns, 1); ArgumentOutOfRangeException.ThrowIfGreaterThan(columns, 20); }
}
