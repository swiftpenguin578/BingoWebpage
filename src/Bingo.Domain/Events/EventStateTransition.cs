namespace Bingo.Domain.Events;

public sealed class EventStateTransition
{
    private EventStateTransition() { }

    public EventStateTransition(Guid id, Guid eventId, EventState fromState, EventState toState, Guid actorId, DateTimeOffset performedAt, string? reason)
    {
        Id = id;
        EventId = eventId;
        FromState = fromState;
        ToState = toState;
        PerformedByAccountId = actorId;
        PerformedAt = performedAt.ToUniversalTime();
        Reason = reason;
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public EventState FromState { get; private set; }
    public EventState ToState { get; private set; }
    public Guid PerformedByAccountId { get; private set; }
    public DateTimeOffset PerformedAt { get; private set; }
    public string? Reason { get; private set; }
}
