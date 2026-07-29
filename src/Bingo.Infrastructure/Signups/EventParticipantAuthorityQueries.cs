using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Signups;

public static class EventParticipantAuthorityQueries
{
    public static IQueryable<EventParticipantAuthority> PrimaryCharacters(this ApplicationDbContext db)
        => from participant in db.EventParticipants
           join assignment in db.EventParticipantCharacters on participant.Id equals assignment.EventParticipantId
           join character in db.OsrsCharacters on assignment.OsrsCharacterId equals character.Id
           where assignment.ReleasedAt == null &&
                 assignment.EventRole == EventCharacterRole.Playing &&
                 !db.EventParticipantCharacters.Any(other =>
                     other.EventParticipantId == participant.Id &&
                     other.ReleasedAt == null &&
                     other.EventRole == EventCharacterRole.Playing &&
                     other.RegistrationOrder < assignment.RegistrationOrder)
           select new EventParticipantAuthority
           {
               ParticipantId = participant.Id,
               EventId = participant.EventId,
               Name = character.DisplayName,
               NormalizedName = character.NormalizedName,
               Ehb = assignment.EhbSnapshot!.Value,
               AssignmentId = assignment.Id
           };

    public static IQueryable<EventParticipantAuthority> AdminPrimaryCharacters(this ApplicationDbContext db)
        => from participant in db.EventParticipants
           join assignment in db.EventParticipantCharacters on participant.Id equals assignment.EventParticipantId
           join character in db.OsrsCharacters on assignment.OsrsCharacterId equals character.Id
           where assignment.EventRole == EventCharacterRole.Playing &&
                 (((participant.SignupStatus == SignupStatus.Confirmed || participant.SignupStatus == SignupStatus.WaitingList) &&
                   assignment.ReleasedAt == null &&
                   !db.EventParticipantCharacters.Any(other =>
                       other.EventParticipantId == participant.Id &&
                       other.EventRole == EventCharacterRole.Playing &&
                       other.ReleasedAt == null &&
                       other.RegistrationOrder < assignment.RegistrationOrder))
                  ||
                  (participant.SignupStatus == SignupStatus.Withdrawn &&
                   assignment.ReleasedAt != null &&
                   !db.EventParticipantCharacters.Any(other =>
                       other.EventParticipantId == participant.Id &&
                       other.EventRole == EventCharacterRole.Playing &&
                       other.ReleasedAt != null &&
                       (other.ReleasedAt > assignment.ReleasedAt ||
                        (other.ReleasedAt == assignment.ReleasedAt &&
                         (other.RegistrationOrder > assignment.RegistrationOrder ||
                          (other.RegistrationOrder == assignment.RegistrationOrder &&
                           other.Id.CompareTo(assignment.Id) > 0)))))))
           select new EventParticipantAuthority
           {
               ParticipantId = participant.Id,
               EventId = participant.EventId,
               Name = character.DisplayName,
               NormalizedName = character.NormalizedName,
               Ehb = assignment.EhbSnapshot!.Value,
               AssignmentId = assignment.Id
           };
}

public sealed class EventParticipantAuthority
{
    public Guid ParticipantId { get; init; }
    public Guid EventId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string NormalizedName { get; init; } = string.Empty;
    public decimal Ehb { get; init; }
    public Guid AssignmentId { get; init; }
}
