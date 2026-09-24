using Bingo.Domain.Integrations.WiseOldMan;

namespace Bingo.Domain.Tests;

public sealed class EventCompetitionSynchronizationTests
{
    [Fact]
    public void MetadataOnlyTitleUpdateRetainsGenerationAndRefreshCadence()
    {
        var now = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
        var state = new EventCompetitionSynchronization(
            Guid.NewGuid(), Guid.NewGuid(), 3, 42, "Old title", now.AddDays(1), now.AddDays(2), "assignments", now);
        state.BeginNormalAttempt(now, now);
        var generation = state.Generation;
        var dueAt = state.NormalDueAt;

        state.UpdateMetadata(42, "New title", now.AddDays(1), now.AddDays(2), now.AddMinutes(5));

        Assert.Equal(generation, state.Generation);
        Assert.Equal(dueAt, state.NormalDueAt);
        Assert.Equal("New title", state.CompetitionTitle);
        Assert.Equal("assignments", state.AssignmentFingerprint);
    }

    [Fact]
    public void FixedHourlySlotsStayAnchoredWhenARefreshIsLateAndRetriesSeparately()
    {
        var anchor = new DateTimeOffset(2026, 9, 22, 18, 0, 0, TimeSpan.Zero);
        var state = new EventCompetitionSynchronization(
            Guid.NewGuid(), Guid.NewGuid(), 1, 42, "Competition", anchor, anchor.AddDays(1), "assignments", anchor);

        state.ReconcileNormalSlot(anchor, anchor.AddMinutes(59));
        Assert.Equal(anchor.AddHours(1), state.NormalDueAt);

        var late = anchor.AddHours(2).AddMinutes(3);
        state.ReconcileNormalSlot(anchor, late);
        Assert.Equal(anchor.AddHours(2), state.NormalDueAt);
        state.MarkFailure(late, "Unavailable", "temporary", late.AddMinutes(1), anchor);

        Assert.Equal(anchor.AddHours(3), state.NormalDueAt);
        Assert.Equal(late.AddMinutes(1), state.RetryDueAt);

        state.MarkSuccess(late.AddMinutes(2), late.AddMinutes(1), true, "[]", null, anchor);
        Assert.Equal(anchor.AddHours(3), state.NormalDueAt);
        Assert.Null(state.RetryDueAt);
    }

    [Fact]
    public void FixedSlotsAreCalculatedInUtcAcrossAnOffsetAndDoNotFabricateAnAnchor()
    {
        var anchor = new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.FromHours(2));
        var first = EventCompetitionSynchronization.FirstNormalSlot(anchor);
        Assert.Equal(TimeSpan.Zero, first.Offset);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero), first);

        var state = new EventCompetitionSynchronization(
            Guid.NewGuid(), Guid.NewGuid(), 1, 42, "Competition", anchor, anchor.AddDays(1), "assignments", anchor);
        state.MakeNormalRefreshDue(anchor.AddHours(2));
        state.ReconcileNormalSlot(null, anchor.AddHours(4));
        Assert.Null(state.NormalDueAt);
    }

    [Fact]
    public void ExistingRollingDueSkipsAConsumedCurrentFixedSlot()
    {
        var anchor = new DateTimeOffset(2026, 9, 22, 18, 0, 0, TimeSpan.Zero);
        var state = new EventCompetitionSynchronization(
            Guid.NewGuid(), Guid.NewGuid(), 1, 42, "Competition", anchor, anchor.AddDays(1), "assignments", anchor);
        var consumedAt = anchor.AddHours(1).AddMinutes(3);
        state.MarkSuccess(consumedAt, consumedAt, true, "[]", null);
        state.MakeNormalRefreshDue(anchor.AddHours(3).AddMinutes(3));

        state.ReconcileNormalSlot(anchor, anchor.AddHours(1).AddMinutes(30));

        Assert.Equal(anchor.AddHours(2), state.NormalDueAt);
    }

    [Fact]
    public void MissingAnchorKeepsNormalDueUnscheduledAfterSuccessOrFailure()
    {
        var now = new DateTimeOffset(2026, 9, 22, 20, 0, 0, TimeSpan.Zero);
        var state = new EventCompetitionSynchronization(
            Guid.NewGuid(), Guid.NewGuid(), 1, 42, "Competition", now, now.AddDays(1), "assignments", now);
        state.MakeNormalRefreshDue(now);

        state.MarkSuccess(now, now, true, "[]", null);
        Assert.Null(state.NormalDueAt);

        state.MarkFailure(now.AddMinutes(1), "Unavailable", "temporary", now.AddMinutes(2));
        Assert.Null(state.NormalDueAt);
    }

    [Fact]
    public void UnknownOperationCarriesAReadOnlyReconciliationDeadline()
    {
        var now = new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
        var operation = new EventCompetitionManagementOperation(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), EventCompetitionManagementOperationType.Update,
            "{}", "fingerprint", 4, now);
        operation.Claim(now);
        operation.MarkUnknown("UnknownOutcome", "The outcome is unknown.", now, now.AddMinutes(1));

        Assert.Equal(EventCompetitionManagementOperationPhase.Unknown, operation.Phase);
        Assert.Equal(now.AddMinutes(1), operation.NextAttemptAt);
    }
}
