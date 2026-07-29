using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;

namespace Bingo.Domain.Tests;

public sealed class EventAndSignupRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PrivateParticipantCapCanChangeButPublicCapCannotDecrease()
    {
        var item = CreateEvent(50);
        item.IncreaseParticipantCap(60);
        Assert.Equal(60, item.ParticipantCap);
        item.IncreaseParticipantCap(59);
        Assert.Equal(59, item.ParticipantCap);
        item.MarkFirstPublic(Now);
        item.IncreaseParticipantCap(60);
        Assert.Throws<InvalidOperationException>(() => item.IncreaseParticipantCap(59));
    }

    [Fact]
    public void EditingParticipantDoesNotChangeQueueSequenceOrSignupTime()
    {
        var participant = new EventParticipant(Guid.NewGuid(), Guid.NewGuid(), SignupStatus.WaitingList, 42, Now, SignupSource.Website);
        participant.SetCaptainVolunteer(false);
        Assert.Equal(42, participant.SignupSequence);
        Assert.Equal(Now, participant.SignedUpAt);
    }

    [Fact]
    public void ManuallyOpeningSignupsRecordsTheActualOpeningWithoutRewritingTheSchedule()
    {
        var item = CreateEvent(50);
        var openedAt = Now.AddMinutes(11);
        item.OpenSignups(openedAt);

        Assert.True(item.AcceptsSignups(openedAt));
        Assert.Equal(openedAt, item.ActualSignupOpenedAt);
        Assert.Equal(Now, item.SignupOpensAt);
        Assert.Equal(Now.AddDays(1), item.SignupClosesAt);
    }

    [Fact]
    public void ClosedSignupsCannotBeScheduledToOpenInThePast()
    {
        var item = CreateEvent(50);
        item.OpenSignups();
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
        item.OpenSignups();
        item.CloseSignups();
        item.StartEvent(Now.AddDays(2));

        Assert.Throws<InvalidOperationException>(() => item.OpenSignups(Now.AddDays(2)));
        Assert.Throws<InvalidOperationException>(item.CloseSignups);
        Assert.Throws<InvalidOperationException>(() => item.ChangeSignupClosing(Now.AddDays(5), Now));
    }

    [Fact]
    public void EventCannotBeStartedTwiceOrRestartedFromFinalReview()
    {
        var item = CreateEvent(50);
        item.OpenSignups();
        item.CloseSignups();
        item.StartEvent(Now.AddDays(2));

        Assert.Throws<InvalidOperationException>(() => item.StartEvent(Now.AddDays(2)));
        item.EndEvent();
        Assert.Throws<InvalidOperationException>(() => item.StartEvent(Now.AddDays(3)));
    }

    [Fact]
    public void ADirectDomainReopenDoesNotSilentlyReplaceAnExpiredClosingTime()
    {
        var item = CreateEvent(50);
        var reopenedAt = Now.AddDays(1);

        item.OpenSignups(reopenedAt);

        Assert.False(item.AcceptsSignups(reopenedAt));
        Assert.Equal(Now, item.SignupOpensAt);
        Assert.Equal(Now.AddDays(1), item.SignupClosesAt);
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

        Assert.Equal(EventState.SignupOpen, item.State);
        Assert.True(item.AcceptsSignups(Now.AddHours(1)));
        Assert.False(item.OpenSignupsIfScheduled(Now.AddHours(2)));
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
        item.OpenSignups();
        item.CloseSignups();
        item.StartEvent(Now.AddDays(2));
        Assert.Throws<InvalidOperationException>(() => item.FinalizeResults(Now.AddDays(3)));
        item.EndEvent();
        item.FinalizeResults(Now.AddDays(3));
        Assert.True(item.ResultsPublished);
        Assert.Equal(EventState.Finalized, item.State);

        item.Archive(Now.AddDays(4));
        Assert.Equal(EventState.Archived, item.State);
        item.Unfinalize("Correct an official result");

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

    private static BingoEvent CreateEvent(int cap) => new(Guid.NewGuid(), "Test", "test", "Test", "Europe/Copenhagen", Now, Now.AddDays(1), Now.AddDays(2), Now.AddDays(3), Now.AddDays(4), cap, Guid.NewGuid(), Now);
}
