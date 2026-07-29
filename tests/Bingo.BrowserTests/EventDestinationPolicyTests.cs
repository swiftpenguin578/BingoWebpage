using Bingo.Domain.Events;
using Bingo.Web.Events;

namespace Bingo.BrowserTests;

public sealed class EventDestinationPolicyTests
{
    [Theory]
    [InlineData(EventState.SignupOpen, false, false, false, EventDestination.SignupTable)]
    [InlineData(EventState.SignupClosed, false, false, false, EventDestination.SignupTable)]
    [InlineData(EventState.Draft, false, false, false, EventDestination.SignupTable)]
    [InlineData(EventState.Live, true, true, false, EventDestination.Board)]
    [InlineData(EventState.AwaitingFinalReview, true, true, false, EventDestination.Board)]
    [InlineData(EventState.Finalized, true, true, true, EventDestination.Board)]
    [InlineData(EventState.Archived, true, true, true, EventDestination.Board)]
    public void NormalVisitorsFollowTheBestPublishedSurface(EventState state, bool roster, bool board, bool results, EventDestination expected)
    {
        var route = new EventRouteState(state, DateTimeOffset.UtcNow, true, roster, board, results);
        Assert.Equal(expected, EventDestinationPolicy.Decide(route, false));
    }

    [Fact]
    public void AdminRetainsTheHistoricalTableAfterRosterPublication()
    {
        var route = new EventRouteState(EventState.Live, DateTimeOffset.UtcNow, true, true, true, false);
        Assert.Equal(EventDestination.Board, EventDestinationPolicy.Decide(route, false));
        Assert.Equal(EventDestination.SignupTable, EventDestinationPolicy.Decide(route, true));
        Assert.True(EventDestinationPolicy.MayUseSignupTable(route, true));
        Assert.False(EventDestinationPolicy.MayUseSignupTable(route, false));
    }

    [Fact]
    public void PrivateOrInconsistentEventsFailClosed()
    {
        var route = new EventRouteState(EventState.Draft, null, false, false, false, false);
        Assert.Equal(EventDestination.Unavailable, EventDestinationPolicy.Decide(route, false));
        Assert.False(EventDestinationPolicy.MayUseSignupTable(route, false));
    }

    [Fact]
    public void PublicOverviewUsesRosterUntilTheBoardIsPublished()
    {
        var rosterOnly = new EventRouteState(EventState.SignupClosed, DateTimeOffset.UtcNow, true, true, false, false);
        var boardPublished = rosterOnly with { BoardAvailable = true };
        Assert.Equal(EventDestination.Roster, EventDestinationPolicy.PublicOverview(rosterOnly));
        Assert.Equal(EventDestination.Board, EventDestinationPolicy.PublicOverview(boardPublished));
    }

    [Fact]
    public void PublicOverviewUsesTheBoardWhenNoRosterPublicationExists()
    {
        var boardOnly = new EventRouteState(EventState.SignupClosed, DateTimeOffset.UtcNow, true, false, true, false);

        Assert.Equal(EventDestination.Board, EventDestinationPolicy.PublicOverview(boardOnly));
    }

    [Theory]
    [InlineData(EventState.SignupClosed, false, false, false, null, EventDisplayPhase.SignupsClosed)]
    [InlineData(EventState.SignupClosed, true, false, false, null, EventDisplayPhase.DraftFinalized)]
    [InlineData(EventState.SignupClosed, true, true, false, false, EventDisplayPhase.BoardPublished)]
    [InlineData(EventState.SignupClosed, true, true, false, true, EventDisplayPhase.EventReady)]
    [InlineData(EventState.SignupClosed, true, true, true, false, EventDisplayPhase.StartPostponed)]
    [InlineData(EventState.Live, true, true, false, true, EventDisplayPhase.Live)]
    public void DisplayPhaseUsesPublicationFactsAndTheAuthoritativeReadinessResult(EventState state, bool draftFinalized, bool boardPublished, bool startPostponed, bool? startReady, EventDisplayPhase expected)
    {
        Assert.Equal(expected, EventDisplayPhaseProjection.From(new(state, draftFinalized, boardPublished, startPostponed, startReady)));
    }
}
