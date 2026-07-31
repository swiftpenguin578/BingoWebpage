namespace Bingo.Domain.Signups;

public sealed class EventParticipantCharacterSwap
{
    private EventParticipantCharacterSwap() { }

    public EventParticipantCharacterSwap(
        Guid id,
        Guid eventId,
        Guid eventParticipantId,
        Guid? previousOsrsCharacterId,
        Guid nextOsrsCharacterId,
        DateTimeOffset effectiveAtUtc,
        DateTimeOffset recordedAtUtc,
        Guid? recordedByAccountId,
        string? reason)
    {
        if (previousOsrsCharacterId == nextOsrsCharacterId)
            throw new ArgumentException("A character transition must change the active character.", nameof(nextOsrsCharacterId));
        if (string.IsNullOrWhiteSpace(reason) && recordedByAccountId is null && previousOsrsCharacterId is not null)
            throw new ArgumentException("A non-initial correction requires an actor or reason.", nameof(reason));

        Id = id;
        EventId = eventId;
        EventParticipantId = eventParticipantId;
        PreviousOsrsCharacterId = previousOsrsCharacterId;
        NextOsrsCharacterId = nextOsrsCharacterId;
        EffectiveAtUtc = effectiveAtUtc.ToUniversalTime();
        RecordedAtUtc = recordedAtUtc.ToUniversalTime();
        RecordedByAccountId = recordedByAccountId;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid EventParticipantId { get; private set; }
    public Guid? PreviousOsrsCharacterId { get; private set; }
    public Guid NextOsrsCharacterId { get; private set; }
    public DateTimeOffset EffectiveAtUtc { get; private set; }
    public DateTimeOffset RecordedAtUtc { get; private set; }
    public Guid? RecordedByAccountId { get; private set; }
    public string? Reason { get; private set; }
}
