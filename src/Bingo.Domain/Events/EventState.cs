namespace Bingo.Domain.Events;

public enum EventState
{
    Draft = 1,
    SignupOpen = 2,
    SignupClosed = 3,
    Live = 4,
    AwaitingFinalReview = 5,
    Finalized = 6,
    Archived = 7,
    Cancelled = 8,
    Discarded = 9
}
