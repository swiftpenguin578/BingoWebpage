using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Signups;

public static class EventParticipantActiveCharacterQueries
{
    public static IQueryable<ActiveEventCharacter> ActiveCharactersAt(
        this ApplicationDbContext db,
        DateTimeOffset instantUtc)
    {
        var instant = instantUtc.ToUniversalTime();
        return from participant in db.EventParticipants
               from transition in db.EventParticipantCharacterSwaps
                   .Where(candidate => candidate.EventParticipantId == participant.Id && candidate.EffectiveAtUtc <= instant)
                   .OrderByDescending(candidate => candidate.EffectiveAtUtc)
                   .ThenByDescending(candidate => candidate.Sequence)
                   .ThenByDescending(candidate => candidate.RecordedAtUtc)
                   .ThenByDescending(candidate => candidate.Id)
                   .Take(1)
               select new ActiveEventCharacter
               {
                   EventId = participant.EventId,
                   ParticipantId = participant.Id,
                   OsrsCharacterId = transition.NextOsrsCharacterId,
                   TransitionId = transition.Id,
                   EffectiveAtUtc = transition.EffectiveAtUtc
               };
    }

    public static Task<ActiveEventCharacter?> ActiveCharacterAtAsync(
        this ApplicationDbContext db,
        Guid eventId,
        Guid participantId,
        DateTimeOffset instantUtc,
        CancellationToken cancellationToken = default) =>
        db.ActiveCharactersAt(instantUtc)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.EventId == eventId && x.ParticipantId == participantId, cancellationToken);

    /// <summary>
    /// The Playing account a submission by this participant is credited to at the given instant:
    /// the active transition, else (only when no account switch rows exist for the participant)
    /// the single unreleased Playing assignment. Null when neither identifies one account.
    /// Shared by evidence crediting and the participant header so both name the same account.
    /// </summary>
    public static async Task<Guid?> CreditedPlayingCharacterIdAsync(
        this ApplicationDbContext db,
        Guid eventId,
        Guid participantId,
        DateTimeOffset instantUtc,
        CancellationToken cancellationToken = default)
    {
        var transition = await db.ActiveCharacterAtAsync(eventId, participantId, instantUtc, cancellationToken);
        if (transition is not null) return transition.OsrsCharacterId;
        if (await db.EventParticipantCharacterSwaps.AsNoTracking().AnyAsync(x => x.EventId == eventId && x.EventParticipantId == participantId, cancellationToken))
            return null;
        var playing = await (from assignment in db.EventParticipantCharacters.AsNoTracking()
                             join participant in db.EventParticipants.AsNoTracking() on assignment.EventParticipantId equals participant.Id
                             where assignment.EventId == eventId && assignment.EventParticipantId == participantId && participant.EventId == eventId &&
                                   assignment.EventRole == EventCharacterRole.Playing && assignment.ReleasedAt == null
                             select assignment.OsrsCharacterId).ToListAsync(cancellationToken);
        return UnswitchedCreditedCharacterId(false, playing);
    }

    /// <summary>The account credited when no switch transition is active: only a participant with no switch rows and exactly one Playing assignment.</summary>
    public static Guid? UnswitchedCreditedCharacterId(bool hasSwapRows, IReadOnlyList<Guid> playingCharacterIds) =>
        !hasSwapRows && playingCharacterIds.Count == 1 ? playingCharacterIds[0] : null;
}

public sealed class ActiveEventCharacter
{
    public Guid EventId { get; init; }
    public Guid ParticipantId { get; init; }
    public Guid OsrsCharacterId { get; init; }
    public Guid TransitionId { get; init; }
    public DateTimeOffset EffectiveAtUtc { get; init; }
}
