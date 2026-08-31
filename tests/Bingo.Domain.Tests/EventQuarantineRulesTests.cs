using Bingo.Domain.Events;

namespace Bingo.Domain.Tests;

public sealed class EventQuarantineRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(EventState.Draft)]
    [InlineData(EventState.SignupOpen)]
    [InlineData(EventState.SignupClosed)]
    [InlineData(EventState.Live)]
    [InlineData(EventState.Cancelled)]
    [InlineData(EventState.Discarded)]
    public void HideRejectsEveryIneligibleLifecycleState(EventState state)
    {
        var item = Event(state);

        Assert.Throws<InvalidOperationException>(() => item.Hide(Guid.NewGuid(), Now, item.Name, "retention test"));
        Assert.False(item.IsHidden);
    }

    [Theory]
    [InlineData(EventState.AwaitingFinalReview)]
    [InlineData(EventState.Finalized)]
    [InlineData(EventState.Archived)]
    public void HideAndRestorePreserveTheLifecycleAndRetainedFacts(EventState state)
    {
        var item = Event(state);
        var stateBefore = item.State;
        var startedBefore = item.ActualStartedAt;
        var endedBefore = item.ActualEndedAt;
        var finalizedBefore = item.FinalizedAt;
        var archivedBefore = item.ArchivedAt;

        item.Hide(Guid.NewGuid(), Now, item.Name, "retention test");

        Assert.True(item.IsHidden);
        Assert.Equal(stateBefore, item.State);
        Assert.Equal(startedBefore, item.ActualStartedAt);
        Assert.Equal(endedBefore, item.ActualEndedAt);
        Assert.Equal(finalizedBefore, item.FinalizedAt);
        Assert.Equal(archivedBefore, item.ArchivedAt);
        Assert.False(item.AcceptsNewSubmissions(Now));
        Assert.Throws<InvalidOperationException>(() => item.EndEvent(Now.AddMinutes(1)));

        item.Restore(item.Name, "restore retention test");

        Assert.False(item.IsHidden);
        Assert.Null(item.HiddenAt);
        Assert.Null(item.HiddenByAccountId);
        Assert.Null(item.HiddenReason);
        Assert.Equal(stateBefore, item.State);
        Assert.Equal(startedBefore, item.ActualStartedAt);
        Assert.Equal(endedBefore, item.ActualEndedAt);
        Assert.Equal(finalizedBefore, item.FinalizedAt);
        Assert.Equal(archivedBefore, item.ArchivedAt);
    }

    [Fact]
    public void HideAndRestoreRequireOrdinalNameConfirmationAndReasons()
    {
        var item = Event(EventState.AwaitingFinalReview);

        Assert.Throws<InvalidOperationException>(() => item.Hide(Guid.NewGuid(), Now, item.Name.ToLowerInvariant(), "reason"));
        Assert.Throws<ArgumentException>(() => item.Hide(Guid.NewGuid(), Now, item.Name, " "));

        item.Hide(Guid.NewGuid(), Now, item.Name, " hidden reason ");
        Assert.Equal("hidden reason", item.HiddenReason);
        Assert.Throws<ArgumentException>(() => item.Restore(item.Name, " "));
        Assert.Throws<InvalidOperationException>(() => item.Restore(item.Name + " ", "reason"));
    }

    private static BingoEvent Event(EventState state)
    {
        var item = new BingoEvent(Guid.NewGuid(), "Quarantine test", $"quarantine-{Guid.NewGuid():N}", "UTC", Guid.NewGuid(), Now);
        item.ConfigureInitialSchedule(Now.AddHours(-5), Now.AddHours(-4), null, Now.AddHours(-3), Now.AddHours(1), 10);

        if (state is EventState.SignupOpen or EventState.SignupClosed or EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived)
            item.OpenSignups(Now.AddHours(-4));
        if (state is EventState.SignupClosed or EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived)
            item.CloseSignups(Now.AddHours(-3));
        if (state is EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived)
            item.StartEvent(Now.AddHours(-2));
        if (state is EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived)
            item.EndEvent(Now.AddHours(-1));
        if (state is EventState.Finalized or EventState.Archived)
            item.FinalizeResults(Now.AddMinutes(-30));
        if (state == EventState.Archived)
            item.Archive(Now.AddMinutes(-15));
        if (state == EventState.Cancelled)
            item.Cancel(Guid.NewGuid(), Now, "cancelled test", protectedHistoryExists: true);
        if (state == EventState.Discarded)
            item.Discard(Guid.NewGuid(), Now, protectedHistoryExists: false);

        return item;
    }
}
