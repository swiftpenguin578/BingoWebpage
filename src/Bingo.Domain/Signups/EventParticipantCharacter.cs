namespace Bingo.Domain.Signups;

public sealed class EventParticipantCharacter
{
    private EventParticipantCharacter() { }

    public EventParticipantCharacter(
        Guid id,
        Guid eventId,
        Guid eventParticipantId,
        Guid osrsCharacterId,
        int registrationOrder,
        DateTimeOffset registeredAt,
        Guid? registeredByAccountId,
        Guid? signupQuestionId,
        EventCharacterRole eventRole,
        decimal? ehbSnapshot,
        EhbSource? ehbSource,
        DateTimeOffset? ehbFetchedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(registrationOrder);
        ValidateEhb(eventRole, ehbSnapshot, ehbSource, ehbFetchedAt);
        Id = id;
        EventId = eventId;
        EventParticipantId = eventParticipantId;
        OsrsCharacterId = osrsCharacterId;
        RegistrationOrder = registrationOrder;
        RegisteredAt = registeredAt.ToUniversalTime();
        RegisteredByAccountId = registeredByAccountId;
        SignupQuestionId = signupQuestionId;
        EventRole = eventRole;
        EhbSnapshot = ehbSnapshot;
        EhbSource = ehbSource;
        EhbFetchedAt = ehbFetchedAt?.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid EventParticipantId { get; private set; }
    public Guid OsrsCharacterId { get; private set; }
    public int RegistrationOrder { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }
    public Guid? RegisteredByAccountId { get; private set; }
    public Guid? SignupQuestionId { get; private set; }
    public EventCharacterRole EventRole { get; private set; }
    public decimal? EhbSnapshot { get; private set; }
    public EhbSource? EhbSource { get; private set; }
    public DateTimeOffset? EhbFetchedAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public Guid? ReleasedByAccountId { get; private set; }
    public int Version { get; private set; }

    public void Release(Guid actorAccountId, DateTimeOffset now)
    {
        if (ReleasedAt is not null) return;
        ReleasedAt = now.ToUniversalTime();
        ReleasedByAccountId = actorAccountId;
    }

    public void AdvanceVersion() => Version++;

    private static void ValidateEhb(
        EventCharacterRole role,
        decimal? ehb,
        EhbSource? source,
        DateTimeOffset? fetchedAt)
    {
        if (role == EventCharacterRole.Playing)
        {
            if (ehb is null || ehb < 0 || source is null)
                throw new ArgumentException("Playing assignments require a non-negative EHB snapshot and source.");
            if ((source == global::Bingo.Domain.Signups.EhbSource.WiseOldMan) != (fetchedAt is not null))
                throw new ArgumentException("Wise Old Man EHB requires fetch metadata, and other sources cannot have it.");
            return;
        }

        if (ehb is not null || source is not null || fetchedAt is not null)
            throw new ArgumentException("Informational assignments cannot contain EHB metadata.");
    }
}
