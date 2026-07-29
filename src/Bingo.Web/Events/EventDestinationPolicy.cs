using Bingo.Domain.Events;

namespace Bingo.Web.Events;

/// <summary>One conservative route decision for participant-facing event handoffs.</summary>
public static class EventDestinationPolicy
{
    public static EventDestination Decide(EventRouteState item, bool administrator)
    {
        if (item.State == EventState.Discarded || item.FirstPublicAt is null)
            return EventDestination.Unavailable;

        // The historical signup record remains an Admin operational surface after teams publish.
        if (administrator && item.SignupPublished)
            return EventDestination.SignupTable;

        if (item.ResultsAvailable || item.BoardAvailable)
            return EventDestination.Board;
        if (item.RosterAvailable)
            return EventDestination.Roster;
        if (item.SignupPublished)
            return EventDestination.SignupTable;
        return EventDestination.Unavailable;
    }

    public static bool MayUseSignupTable(EventRouteState item, bool administrator) =>
        item.SignupPublished && (administrator || Decide(item, false) == EventDestination.SignupTable);

    public static EventDestination PublicOverview(EventRouteState item) =>
        !item.RosterAvailable ? EventDestination.Unavailable : item.BoardAvailable ? EventDestination.Board : EventDestination.Roster;

    public static EventRouteState From(BingoEvent item, bool rosterExists = false, bool boardPublished = false) => new(
        item.State, item.FirstPublicAt, item.ActualSignupOpenedAt is not null || item.State is EventState.SignupOpen or EventState.SignupClosed || item.DraftLocked,
        rosterExists || item.TeamRostersPublished || item.DraftResultsPublished,
        item.BoardPublished || boardPublished,
        item.ResultsPublished);
}

public sealed record EventRouteState(EventState State, DateTimeOffset? FirstPublicAt, bool SignupPublished, bool RosterAvailable, bool BoardAvailable, bool ResultsAvailable);
public enum EventDestination { Unavailable, SignupTable, Roster, Board }

/// <summary>
/// A presentation-only phase derived from publication and lifecycle facts. It never authorizes a route or transition.
/// </summary>
public static class EventDisplayPhaseProjection
{
    public static EventDisplayPhase From(EventDisplayFacts facts)
    {
        if (facts.State == EventState.Live) return EventDisplayPhase.Live;
        if (facts.State != EventState.SignupClosed) return EventDisplayPhase.Lifecycle;
        if (!facts.DraftFinalized) return EventDisplayPhase.SignupsClosed;
        if (!facts.BoardPublished) return EventDisplayPhase.DraftFinalized;
        if (facts.StartPostponed) return EventDisplayPhase.StartPostponed;
        return facts.StartReady is true ? EventDisplayPhase.EventReady : EventDisplayPhase.BoardPublished;
    }
}

public sealed record EventDisplayFacts(EventState State, bool DraftFinalized, bool BoardPublished, bool StartPostponed = false, bool? StartReady = null);
public enum EventDisplayPhase { Lifecycle, SignupsClosed, DraftFinalized, BoardPublished, EventReady, StartPostponed, Live }
