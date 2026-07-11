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
    public void ManuallyOpeningSignupsOverridesFutureScheduledOpening()
    {
        var item = CreateEvent(50);
        item.OpenSignups(Now.AddHours(-1));

        Assert.True(item.AcceptsSignups(Now.AddHours(-1)));
    }

    private static BingoEvent CreateEvent(int cap) => new(Guid.NewGuid(), "Test", "test", "Test", "Europe/Copenhagen", Now, Now.AddDays(1), Now.AddDays(2), Now.AddDays(3), Now.AddDays(4), cap, Guid.NewGuid(), Now);
}
