using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;

namespace Bingo.Domain.Tests;

public sealed class EventAndSignupRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ParticipantCapCanIncreaseButCannotDecrease()
    {
        var item = CreateEvent(50);
        item.IncreaseParticipantCap(60);
        Assert.Equal(60, item.ParticipantCap);
        Assert.Throws<InvalidOperationException>(() => item.IncreaseParticipantCap(59));
    }

    [Fact]
    public void EditingParticipantDoesNotChangeQueueSequenceOrSignupTime()
    {
        var participant = new EventParticipant(Guid.NewGuid(), Guid.NewGuid(), "Old", "OLD", 100, SignupStatus.WaitingList, 42, Now, SignupSource.Website, "hash");
        participant.UpdatePublicDetails("New", "NEW", 200, null, null, null, false);
        Assert.Equal(42, participant.SignupSequence);
        Assert.Equal(Now, participant.SignedUpAt);
    }

    [Fact]
    public void ReplacingParticipantEditTokenInvalidatesThePreviousHash()
    {
        var participant = new EventParticipant(Guid.NewGuid(), Guid.NewGuid(), "Player", "PLAYER", 100, SignupStatus.Confirmed, 1, Now, SignupSource.Website, "old-hash");

        participant.ReplacePrivateEditToken("new-hash");

        Assert.Equal("new-hash", participant.PrivateEditTokenHash);
        Assert.Throws<ArgumentException>(() => participant.ReplacePrivateEditToken(" "));
    }

    [Fact]
    public void ManuallyOpeningSignupsCreatesANewThreeMonthWindow()
    {
        var item = CreateEvent(50);
        var openedAt = Now.AddMinutes(11);
        item.OpenSignups(openedAt);

        Assert.True(item.AcceptsSignups(openedAt));
        Assert.Equal(openedAt.AddSeconds(-openedAt.Second), item.SignupOpensAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 12, 30, 0, TimeSpan.Zero), item.SignupClosesAt);
    }

    [Fact]
    public void ClosedSignupsCannotBeScheduledToOpenInThePast()
    {
        var item = CreateEvent(50);
        item.CloseSignups();

        Assert.Throws<InvalidOperationException>(() =>
            item.ChangeSignupWindow(Now.AddMinutes(-30), Now.AddDays(1), Now));
    }

    [Fact]
    public void ScheduledSignupTransitionsOnlyApplyWhenDue()
    {
        var item = CreateEvent(50);

        Assert.False(item.OpenSignupsIfScheduled(Now.AddMinutes(-1)));
        Assert.True(item.OpenSignupsIfScheduled(Now));
        Assert.False(item.CloseSignupsIfScheduled(Now.AddHours(1)));
        Assert.True(item.CloseSignupsIfScheduled(Now.AddDays(1)));
    }

    [Fact]
    public void SignupControlsCannotMoveAStartedEventBackwards()
    {
        var item = CreateEvent(50);
        item.StartEvent(Now.AddDays(2));

        Assert.Throws<InvalidOperationException>(() => item.OpenSignups(Now.AddDays(2)));
        Assert.Throws<InvalidOperationException>(item.CloseSignups);
        Assert.Throws<InvalidOperationException>(() => item.ChangeSignupClosing(Now.AddDays(5), Now));
    }

    [Fact]
    public void EventCannotBeStartedTwiceOrRestartedFromFinalReview()
    {
        var item = CreateEvent(50);
        item.StartEvent(Now.AddDays(2));

        Assert.Throws<InvalidOperationException>(() => item.StartEvent(Now.AddDays(2)));
        item.EndEvent();
        Assert.Throws<InvalidOperationException>(() => item.StartEvent(Now.AddDays(3)));
    }

    [Fact]
    public void SignupsCanBeManuallyReopenedAfterTheirOriginalClosingTime()
    {
        var item = CreateEvent(50);
        var reopenedAt = Now.AddDays(1);

        item.OpenSignups(reopenedAt);

        Assert.True(item.AcceptsSignups(reopenedAt));
        Assert.Equal(reopenedAt, item.SignupOpensAt);
        Assert.Equal(reopenedAt.AddMonths(3), item.SignupClosesAt);
    }

    [Fact]
    public void SignupClosingCanMoveEarlierOrLaterWhileRemainingInTheFuture()
    {
        var item = CreateEvent(50);

        item.ChangeSignupClosing(Now.AddHours(12), Now);
        Assert.Equal(Now.AddHours(12), item.SignupClosesAt);

        item.ChangeSignupClosing(Now.AddDays(2), Now);
        Assert.Equal(Now.AddDays(2), item.SignupClosesAt);
    }

    [Fact]
    public void SignupClosingCannotMoveIntoThePast()
    {
        var item = CreateEvent(50);

        Assert.Throws<InvalidOperationException>(() => item.ChangeSignupClosing(Now.AddMinutes(-30), Now));
    }

    [Fact]
    public void FutureSignupWindowWaitsForItsScheduledOpening()
    {
        var item = CreateEvent(50);
        item.OpenSignups(Now);

        item.ChangeSignupWindow(Now.AddHours(2), Now.AddHours(6), Now);

        Assert.Equal(EventState.Draft, item.State);
        Assert.False(item.AcceptsSignups(Now.AddHours(1)));
        Assert.True(item.OpenSignupsIfScheduled(Now.AddHours(2)));
    }

    [Fact]
    public void SignupWindowRequiresOpeningBeforeClosing()
    {
        var item = CreateEvent(50);

        Assert.Throws<InvalidOperationException>(() => item.ChangeSignupWindow(Now.AddHours(4), Now.AddHours(3), Now));
    }

    [Fact]
    public void FinalizeArchiveAndUnfinalizeFollowTheOfficialResultsLifecycle()
    {
        var item = CreateEvent(50);
        item.StartEvent(Now.AddDays(2));
        Assert.Throws<InvalidOperationException>(() => item.FinalizeResults(Now.AddDays(3)));
        item.EndEvent();
        item.FinalizeResults(Now.AddDays(3));
        Assert.True(item.ResultsPublished);
        Assert.Equal(EventState.Finalized, item.State);

        item.Archive(Now.AddDays(4));
        Assert.Equal(EventState.Archived, item.State);
        item.Unfinalize();

        Assert.Equal(EventState.AwaitingFinalReview, item.State);
        Assert.False(item.ResultsPublished);
    }

    [Fact]
    public void UnfinalizingSnapshotPreservesItsOfficialPlacementsAsHistory()
    {
        var snapshot = new EventFinalizationSnapshot(Guid.NewGuid(), Guid.NewGuid(), 1, Now, Guid.NewGuid());
        snapshot.Unfinalize(Now.AddHours(1), Guid.NewGuid(), "Correct an approval");
        Assert.False(snapshot.Active);
        Assert.Equal("Correct an approval", snapshot.UnfinalizeReason);
        Assert.Equal(Now, snapshot.FinalizedAt);
    }

    [Fact]
    public void CaptainExpiryCanBeRescheduledFromFinalizationTime()
    {
        var account = new Account(Guid.NewGuid(), "Captain", "CAPTAIN", AccountRole.Captain, Now);
        account.ScopeCaptain(Guid.NewGuid(), Guid.NewGuid(), Now, Now.AddDays(3), Now.AddDays(4));

        account.ScheduleExpiry(Now.AddDays(3).AddHours(24));

        Assert.Equal(Now.AddDays(4), account.ExpiresAt);
        Assert.Equal(AccountAccessMode.CorrectionOnly, account.GetAccessMode(Now.AddDays(3).AddHours(1)));
        Assert.Equal(AccountAccessMode.Disabled, account.GetAccessMode(Now.AddDays(4)));
    }

    private static BingoEvent CreateEvent(int cap) => new(Guid.NewGuid(), "Test", "test", "Test", "Europe/Copenhagen", Now, Now.AddDays(1), Now.AddDays(2), Now.AddDays(3), Now.AddDays(4), cap, Guid.NewGuid(), Now);
}
