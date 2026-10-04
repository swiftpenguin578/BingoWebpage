using Bingo.Domain.Events;

namespace Bingo.Domain.Tests;

public sealed class ResultsPublicationRulesTests
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 9, 26, 12, 34, 56, TimeSpan.Zero).AddMicroseconds(123456);

    [Fact]
    public void PublishOfficialResultsArchivesAndSetsOnePublicationTimestamp()
    {
        var actorId = Guid.NewGuid();
        var item = EndedEvent(actorId);

        item.PublishOfficialResults(Now);

        Assert.Equal(EventState.Archived, item.State);
        Assert.True(item.ResultsPublished);
        Assert.Equal(Now, item.FinalizedAt);
        Assert.Equal(Now, item.ArchivedAt);
    }

    [Fact]
    public void NewFinalReviewLifecycleOnlyAllowsArchivedAsThePublicationDestination()
    {
        Assert.True(EventStatePolicy.CanTransition(EventState.AwaitingFinalReview, EventState.Archived));
        Assert.False(EventStatePolicy.CanTransition(EventState.AwaitingFinalReview, EventState.Finalized));
    }

    [Fact]
    public void PublishOfficialResultsRejectsASecondPublicationOrLegacyFinalizedState()
    {
        var item = EndedEvent(Guid.NewGuid());
        item.PublishOfficialResults(Now);

        Assert.Throws<InvalidOperationException>(() => item.PublishOfficialResults(Now.AddMinutes(1)));

        var legacy = EndedEvent(Guid.NewGuid());
        legacy.FinalizeResults(Now);
        Assert.Equal(EventState.Finalized, legacy.State);
        Assert.Throws<InvalidOperationException>(() => legacy.PublishOfficialResults(Now));
    }

    private static BingoEvent EndedEvent(Guid actorId)
    {
        var item = new BingoEvent(Guid.NewGuid(), "Publication rules", $"publication-rules-{Guid.NewGuid():N}", "UTC", actorId, Now.AddDays(-2), Bingo.Domain.Events.PlacementRule.LegacyScoreTimeThenEhb);
        item.ConfigureSchedule(Now.AddDays(-2), Now.AddDays(-1), null, Now.AddHours(-3), Now.AddHours(-2), 20);
        item.OpenSignups(Now.AddDays(-2));
        item.CloseSignups(Now.AddDays(-1));
        item.StartEvent(Now.AddHours(-3));
        item.EndEvent(Now.AddHours(-2));
        return item;
    }
}
