namespace Bingo.Domain.Events;

/// <summary>The authoritative Slice 3 lifecycle graph. Substates may narrow these permissions but never broaden them.</summary>
public static class EventStatePolicy
{
    public static bool CanTransition(EventState from, EventState to) => (from, to) switch
    {
        (EventState.Draft, EventState.SignupOpen) => true,
        (EventState.SignupOpen, EventState.SignupClosed) => true,
        (EventState.SignupClosed, EventState.SignupOpen) => true,
        (EventState.SignupClosed, EventState.Live) => true,
        (EventState.Live, EventState.AwaitingFinalReview) => true,
        (EventState.AwaitingFinalReview, EventState.Live) => true,
        (EventState.AwaitingFinalReview, EventState.Finalized) => true,
        (EventState.Finalized, EventState.Archived) => true,
        (EventState.Finalized, EventState.AwaitingFinalReview) => true,
        (EventState.Archived, EventState.AwaitingFinalReview) => true,
        (EventState.Draft or EventState.SignupOpen or EventState.SignupClosed, EventState.Cancelled or EventState.Discarded) => true,
        _ => false
    };

    public static bool IsTerminal(EventState state) => state is EventState.Cancelled or EventState.Discarded;

    public static bool Allows(EventState state, EventCapability capability) => capability switch
    {
        EventCapability.ConfigureIdentityOrSchedule => state is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed,
        EventCapability.ConfigureSignup => state is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed,
        EventCapability.ParticipantSignup => state == EventState.SignupOpen,
        EventCapability.ReopenSignup => state == EventState.SignupClosed,
        EventCapability.StartEvent => state == EventState.SignupClosed,
        EventCapability.ResumeEvent => state == EventState.AwaitingFinalReview,
        EventCapability.LiveSubmission => state == EventState.Live,
        EventCapability.CompetitionSynchronization => state == EventState.Live,
        EventCapability.ReviewEvidence => state is EventState.Live or EventState.AwaitingFinalReview,
        EventCapability.ConfigureEvidenceCodes => state is EventState.Draft or EventState.SignupClosed or EventState.Live,
        EventCapability.Finalize => state == EventState.AwaitingFinalReview,
        EventCapability.Archive => state == EventState.Finalized,
        EventCapability.Unfinalize => state is EventState.Finalized or EventState.Archived,
        EventCapability.CancelOrDiscard => state is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed,
        _ => false
    };
}

public enum EventCapability
{
    ConfigureIdentityOrSchedule,
    ConfigureSignup,
    ParticipantSignup,
    ReopenSignup,
    StartEvent,
    ResumeEvent,
    LiveSubmission,
    CompetitionSynchronization,
    ReviewEvidence,
    ConfigureEvidenceCodes,
    Finalize,
    Archive,
    Unfinalize,
    CancelOrDiscard
}
