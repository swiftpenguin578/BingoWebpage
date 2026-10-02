namespace Bingo.Domain.Events;

/// <summary>Immutable identity and committed result of one actor's event creation.</summary>
public sealed class EventCreationOperation
{
    private EventCreationOperation() { }

    public EventCreationOperation(Guid actorAccountId, Guid requestId, string name, string timezone, Guid eventId)
    {
        ActorAccountId = actorAccountId;
        RequestId = requestId;
        Name = name;
        Timezone = timezone;
        EventId = eventId;
    }

    public Guid ActorAccountId { get; private set; }
    public Guid RequestId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Timezone { get; private set; } = string.Empty;
    public Guid EventId { get; private set; }
}
