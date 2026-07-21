using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;

namespace Bingo.Domain.Tests;

public sealed class BoardRulesTests
{
    [Fact]
    public void RequirementWeightDefaultsToOneAndRequiresExplicitEnablement()
    {
        var disabled = new BoardRequirementSnapshot(Guid.NewGuid(), Guid.NewGuid(), 1, 5, true, false, "Drops", false, 4);
        var enabled = new BoardRequirementSnapshot(Guid.NewGuid(), Guid.NewGuid(), 1, 5, true, true, "Drops", false, 4);

        Assert.Equal(1, disabled.CreditedWeight);
        Assert.Equal(4, enabled.CreditedWeight);
    }

    [Fact]
    public void ShrinkingIsBlockedWhenPlacedTilesDoNotFit()
    {
        var board = new Board(Guid.NewGuid(), Guid.NewGuid(), "Board", 5, 5);
        var exception = Assert.Throws<InvalidOperationException>(() => board.Resize(2, 2, 5));
        Assert.Contains("Remove 1 tile", exception.Message);
    }

    [Fact]
    public void ShrinkingIsAllowedWhenAllPlacedTilesFit()
    {
        var board = new Board(Guid.NewGuid(), Guid.NewGuid(), "Board", 5, 5);
        board.Resize(2, 3, 6);
        Assert.Equal(2, board.Rows);
        Assert.Equal(3, board.Columns);
    }

    [Fact]
    public void PublishedBoardCannotBeResizedOrPublishedAgain()
    {
        var board = new Board(Guid.NewGuid(), Guid.NewGuid(), "Board", 5, 5);
        board.Publish(DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => board.Resize(6, 6, 25));
        Assert.Throws<InvalidOperationException>(() => board.Publish(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RowAndColumnEhbFollowTilesAfterSwap()
    {
        var boardId = Guid.NewGuid();
        var first = new BoardTile(Guid.NewGuid(), boardId, Guid.NewGuid(), 0, 0, "First", "", "", 10);
        var second = new BoardTile(Guid.NewGuid(), boardId, Guid.NewGuid(), 1, 1, "Second", "", "", 25);
        first.Move(1, 1); second.Move(0, 0);
        Assert.Equal(25, new[] { first, second }.Where(x => x.RowIndex == 0).Sum(x => x.EstimatedEhbSnapshot));
        Assert.Equal(10, new[] { first, second }.Where(x => x.ColumnIndex == 1).Sum(x => x.EstimatedEhbSnapshot));
    }

    [Fact]
    public void BoardSnapshotIsUnaffectedByLaterCatalogueChanges()
    {
        var now = DateTimeOffset.UtcNow; var boss = new BossActivity(Guid.NewGuid(), "Mole", "mole", "Boss", 100, now);
        var snapshot = new BoardRequirementBossSnapshot(Guid.NewGuid(), Guid.NewGuid(), boss.Id, boss.Name, boss.EfficientCompletionsPerHour);
        boss.Update("Giant Mole", "Boss", 120, null, "manual", null, now.AddDays(1));
        Assert.Equal("Mole", snapshot.BossName); Assert.Equal(100, snapshot.EfficientRate);
    }

    [Fact]
    public void BoardAggregateVersionAdvancesForEachClaimedEdit()
    {
        var board = new Board(Guid.NewGuid(), Guid.NewGuid(), "Board", 5, 5);
        var original = board.Version;

        board.MarkChanged();
        board.MarkChanged();

        Assert.Equal(original + 2, board.Version);
    }

    [Fact]
    public void BoardEditingIsExclusiveButCanBeExplicitlyTransferred()
    {
        var now = DateTimeOffset.UtcNow;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var board = new Board(Guid.NewGuid(), Guid.NewGuid(), "Board", 5, 5);

        board.AcquireEditing(first, now, TimeSpan.FromMinutes(5));
        Assert.Throws<InvalidOperationException>(() => board.AcquireEditing(second, now.AddMinutes(1), TimeSpan.FromMinutes(5)));

        var previous = board.AcquireEditing(second, now.AddMinutes(1), TimeSpan.FromMinutes(5), force: true);
        Assert.Equal(first, previous);
        Assert.Equal(second, board.EditorAccountId);
        Assert.Throws<InvalidOperationException>(() => board.RequireEditing(first, now.AddMinutes(2)));
    }

    [Fact]
    public void BoardEditingCanBeReleasedOrRecoveredAfterExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var board = new Board(Guid.NewGuid(), Guid.NewGuid(), "Board", 5, 5);

        board.AcquireEditing(first, now, TimeSpan.FromMinutes(1));
        board.AcquireEditing(second, now.AddMinutes(2), TimeSpan.FromMinutes(5));
        board.ReleaseEditing(second, now.AddMinutes(3));

        Assert.Null(board.EditorAccountId);
        Assert.False(board.HasActiveEditor(now.AddMinutes(3)));
    }

    [Fact]
    public void LeaseHeartbeatDoesNotInvalidateOpenEditingForms()
    {
        var now = DateTimeOffset.UtcNow;
        var editor = Guid.NewGuid();
        var board = new Board(Guid.NewGuid(), Guid.NewGuid(), "Board", 5, 5);
        board.AcquireEditing(editor, now, TimeSpan.FromMinutes(5));
        var controlVersion = board.EditControlVersion;

        board.RenewEditing(editor, now.AddMinutes(2), TimeSpan.FromMinutes(5));

        Assert.Equal(controlVersion, board.EditControlVersion);
    }
}
