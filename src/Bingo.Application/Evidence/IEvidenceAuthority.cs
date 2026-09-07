namespace Bingo.Application.Evidence;

public interface IEvidenceAuthority
{
    Task<EvidenceActorScope> ResolveActorAsync(Guid actorAccountId, Guid? eventId, Guid? teamId, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<EvidenceActorScope> AuthorizeAsync(Guid actorAccountId, Guid eventId, Guid teamId, Guid creditedParticipantId, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<EvidenceActorScope> AuthorizeOwnerAsync(Guid actorAccountId, Guid eventId, Guid teamId, Guid creditedParticipantId, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<bool> CanViewPrivateEvidenceAsync(Guid actorAccountId, Guid eventId, Guid teamId, Guid creditedParticipantId, DateTimeOffset now, Guid? submissionId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvidenceCandidate>> GetCurrentTeamCandidatesAsync(EvidenceActorScope scope, CancellationToken cancellationToken = default);
    Task<CreditedCharacterSnapshot> ResolveCreditedCharacterAsync(Guid eventId, Guid participantId, DateTimeOffset submittedAt, CancellationToken cancellationToken = default);
}

public sealed record EvidenceActorScope(EvidenceActorKind Kind, Guid ActorAccountId, Guid EventId, Guid TeamId, Guid CreditedParticipantId);

public sealed record EvidenceCandidate(Guid ParticipantId, string CharacterName);

public enum EvidenceActorKind
{
    Participant = 1,
    Captain = 2,
    EmergencyCaptain = 3,
    Administrator = 4
}

public sealed record CreditedCharacterSnapshot(Guid OsrsCharacterId, string Name);
