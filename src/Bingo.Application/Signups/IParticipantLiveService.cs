using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;

namespace Bingo.Application.Signups;

public interface IParticipantLiveService
{
    Task<ParticipantLiveContext?> GetContextAsync(
        Guid eventId,
        Guid participantId,
        Guid viewerAccountId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParticipantLiveContext>> GetTeamContextsAsync(
        Guid eventId,
        Guid teamId,
        Guid viewerAccountId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The playing account shown in the public header: the signed-in website account's participant on a team
    /// of a Live event (the preferred event when it qualifies, else the most recently started Live event),
    /// named by the account submissions are credited to now. Null when there is none.
    /// </summary>
    Task<PlayingAccountHeader?> GetPlayingAccountHeaderAsync(
        Guid viewerAccountId,
        string? preferredEventSlug,
        CancellationToken cancellationToken = default);

    Task<ParticipantCharacterSwapResult> SwapAsync(
        ParticipantCharacterSwapRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ParticipantLiveContext(
    Guid EventId,
    string EventSlug,
    string EventName,
    EventState EventState,
    DateTimeOffset? EventEndsAt,
    Guid ParticipantId,
    Guid TeamId,
    string TeamName,
    string TeamSlug,
    TeamMembershipRole TeamRole,
    string? PlannedCharacterName,
    string? ActiveCharacterName,
    DateTimeOffset? ActiveSinceUtc,
    IReadOnlyList<ParticipantPlayingCharacter> PlayingCharacters,
    bool CanSwap);

public sealed record ParticipantPlayingCharacter(Guid CharacterId, string Name, bool IsActive);

public sealed record ParticipantCharacterSwapRequest(
    Guid EventId,
    Guid ParticipantId,
    Guid ExpectedCurrentCharacterId,
    Guid NextCharacterId,
    Guid ActorAccountId,
    string ActorName);

public sealed record ParticipantCharacterSwapResult(
    bool Succeeded,
    string? Error = null,
    DateTimeOffset? EffectiveAtUtc = null,
    string? CharacterName = null);

public sealed record PlayingAccountHeader(
    Guid EventId,
    Guid ParticipantId,
    string CreditedCharacterName,
    Guid CreditedCharacterId,
    IReadOnlyList<ParticipantPlayingCharacter> SwitchTargets);
