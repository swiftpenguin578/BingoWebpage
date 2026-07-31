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
}

public sealed class ActiveEventCharacter
{
    public Guid EventId { get; init; }
    public Guid ParticipantId { get; init; }
    public Guid OsrsCharacterId { get; init; }
    public Guid TransitionId { get; init; }
    public DateTimeOffset EffectiveAtUtc { get; init; }
}
