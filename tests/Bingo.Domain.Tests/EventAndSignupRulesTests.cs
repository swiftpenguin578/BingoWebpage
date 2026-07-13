using Bingo.Domain.Events;
using Bingo.Domain.Access;
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
    public void ManuallyOpeningSignupsOverridesFutureScheduledOpening()
    {
        var item = CreateEvent(50);
        item.OpenSignups(Now.AddHours(-1));

        Assert.True(item.AcceptsSignups(Now.AddHours(-1)));
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
