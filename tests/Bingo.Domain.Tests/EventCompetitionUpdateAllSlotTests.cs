using Bingo.Domain.Integrations.WiseOldMan;

namespace Bingo.Domain.Tests;

public sealed class EventCompetitionUpdateAllSlotTests
{
    [Fact]
    public void FourHourSlotsUseTheLiveAnchorAndLeadTheirPairedFetch()
    {
        var anchor = new DateTimeOffset(2026, 9, 22, 18, 0, 0, TimeSpan.Zero);

        Assert.Equal(anchor.AddHours(4), EventCompetitionUpdateAllSchedule.PairedFetchAt(anchor, 1));
        Assert.Equal(anchor.AddHours(3).AddMinutes(45), EventCompetitionUpdateAllSchedule.ScheduledAt(anchor, 1));
        Assert.Equal(anchor.AddHours(7).AddMinutes(45), EventCompetitionUpdateAllSchedule.ScheduledAt(anchor, 2));
        Assert.Equal(1, EventCompetitionUpdateAllSchedule.CurrentSequence(anchor, anchor.AddHours(3).AddMinutes(45)));
        Assert.Equal(2, EventCompetitionUpdateAllSchedule.CurrentSequence(anchor, anchor.AddHours(8).AddSeconds(1)));
    }

    [Fact]
    public void MidnightAndDstBoundariesRemainUtcAnchored()
    {
        var midnightAnchor = new DateTimeOffset(2026, 9, 22, 22, 30, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 9, 23, 2, 15, 0, TimeSpan.Zero), EventCompetitionUpdateAllSchedule.ScheduledAt(midnightAnchor, 1));
        Assert.Equal(new DateTimeOffset(2026, 9, 23, 2, 30, 0, TimeSpan.Zero), EventCompetitionUpdateAllSchedule.PairedFetchAt(midnightAnchor, 1));

        // Europe/Copenhagen leaves daylight time between the anchor and the
        // paired fetch. The schedule is elapsed UTC time, not a wall-clock
        // calculation that repeats or skips an hour.
        var dstAnchor = new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.FromHours(2));
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 3, 15, 0, TimeSpan.Zero), EventCompetitionUpdateAllSchedule.ScheduledAt(dstAnchor, 1));
        Assert.Equal(TimeSpan.Zero, EventCompetitionUpdateAllSchedule.PairedFetchAt(dstAnchor, 1).Offset);
    }

    [Fact]
    public void EndBoundaryIsStrictAndShortEventsHaveNoEligiblePair()
    {
        var anchor = new DateTimeOffset(2026, 9, 22, 18, 0, 0, TimeSpan.Zero);

        Assert.False(EventCompetitionUpdateAllSchedule.IsInterior(anchor, anchor.AddHours(4), 1));
        Assert.True(EventCompetitionUpdateAllSchedule.IsInterior(anchor, anchor.AddHours(4).AddSeconds(1), 1));
        Assert.False(EventCompetitionUpdateAllSchedule.IsInterior(anchor, anchor.AddHours(3).AddMinutes(50), 1));
    }

    [Fact]
    public void AcknowledgementFailureAndAmbiguousOutcomeAreTerminalReceipts()
    {
        var anchor = new DateTimeOffset(2026, 9, 22, 18, 0, 0, TimeSpan.Zero);
        var now = anchor.AddHours(3);
        var slot = new EventCompetitionUpdateAllSlot(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 42,
            anchor, anchor.AddHours(4), anchor.AddHours(3).AddMinutes(45), 1, 1, now);

        slot.Claim(now.AddMinutes(45));
        slot.Acknowledge("Acknowledged", "accepted", now.AddMinutes(45).AddSeconds(1));

        Assert.Equal(EventCompetitionUpdateAllSlotStatus.Acknowledged, slot.Status);
        Assert.Equal(1, slot.AttemptCount);
        Assert.Throws<InvalidOperationException>(() => slot.Claim(now.AddHours(4)));
    }
}
