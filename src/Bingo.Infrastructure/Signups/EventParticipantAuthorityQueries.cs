using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Signups;

public static class EventParticipantAuthorityQueries
{
    public static IQueryable<EventParticipantAuthority> PrimaryCharacters(this ApplicationDbContext db)
    {
        var linkedPrimary = from participant in db.EventParticipants
                            join form in db.SignupForms on participant.EventId equals form.EventId
                            join question in db.SignupQuestions on form.Id equals question.SignupFormId
                            join assignment in db.EventParticipantCharacters on new { EventId = participant.EventId, ParticipantId = participant.Id, SignupQuestionId = (Guid?)question.Id }
                                equals new { EventId = assignment.EventId, ParticipantId = assignment.EventParticipantId, SignupQuestionId = assignment.SignupQuestionId }
                            join character in db.OsrsCharacters on assignment.OsrsCharacterId equals character.Id
                            where participant.Source == SignupSource.Website && question.EventId == participant.EventId &&
                                  question.Active && question.Type == SignupQuestionType.Account &&
                                  question.SystemField == SignupSystemField.PrimaryRegularAccount &&
                                  question.AccountAnswerRole == EventCharacterRole.Playing &&
                                  assignment.ReleasedAt == null && assignment.EventRole == EventCharacterRole.Playing &&
                                  !db.SignupQuestions.Any(other =>
                                      other.Id != question.Id && other.SignupFormId == form.Id && other.EventId == participant.EventId &&
                                      other.Active && other.Type == SignupQuestionType.Account &&
                                      other.SystemField == SignupSystemField.PrimaryRegularAccount &&
                                      other.AccountAnswerRole == EventCharacterRole.Playing) &&
                                  !db.EventParticipantCharacters.Any(other =>
                                      other.Id != assignment.Id && other.EventParticipantId == participant.Id &&
                                      other.ReleasedAt == null && other.EventRole == EventCharacterRole.Playing &&
                                      other.SignupQuestionId == question.Id)
                            select new EventParticipantAuthority
                            {
                                ParticipantId = participant.Id,
                                EventId = participant.EventId,
                                OsrsCharacterId = assignment.OsrsCharacterId,
                                Name = character.DisplayName,
                                NormalizedName = character.NormalizedName,
                                Ehb = assignment.EhbSnapshot!.Value,
                                AssignmentId = assignment.Id
                            };

        var compatibilityLinkedPrimary = from participant in db.EventParticipants
                                         join form in db.SignupForms on participant.EventId equals form.EventId
                                         join question in db.SignupQuestions on form.Id equals question.SignupFormId
                                         join assignment in db.EventParticipantCharacters on new { EventId = participant.EventId, ParticipantId = participant.Id, SignupQuestionId = (Guid?)question.Id }
                                             equals new { EventId = assignment.EventId, ParticipantId = assignment.EventParticipantId, SignupQuestionId = assignment.SignupQuestionId }
                                         join character in db.OsrsCharacters on assignment.OsrsCharacterId equals character.Id
                                         where participant.Source != SignupSource.Website &&
                                               question.EventId == participant.EventId && question.Active &&
                                               question.Type == SignupQuestionType.Account &&
                                               question.SystemField == SignupSystemField.PrimaryRegularAccount &&
                                               question.AccountAnswerRole == EventCharacterRole.Playing &&
                                               assignment.ReleasedAt == null && assignment.EventRole == EventCharacterRole.Playing &&
                                               !db.EventParticipantCharacters.Any(other =>
                                                   other.Id != assignment.Id && other.EventParticipantId == participant.Id &&
                                                   other.ReleasedAt == null && other.EventRole == EventCharacterRole.Playing &&
                                                   other.SignupQuestionId == question.Id)
                                         select new EventParticipantAuthority
                                         {
                                             ParticipantId = participant.Id,
                                             EventId = participant.EventId,
                                             OsrsCharacterId = assignment.OsrsCharacterId,
                                             Name = character.DisplayName,
                                             NormalizedName = character.NormalizedName,
                                             Ehb = assignment.EhbSnapshot!.Value,
                                             AssignmentId = assignment.Id
                                         };

        var compatibilityUnlinkedPrimary = from participant in db.EventParticipants
                                           join assignment in db.EventParticipantCharacters on participant.Id equals assignment.EventParticipantId
                                           join character in db.OsrsCharacters on assignment.OsrsCharacterId equals character.Id
                                           where participant.Source != SignupSource.Website && assignment.SignupQuestionId == null &&
                                                 assignment.EventId == participant.EventId && assignment.ReleasedAt == null &&
                                                 assignment.EventRole == EventCharacterRole.Playing &&
                                                 !db.EventParticipantCharacters.Any(linked =>
                                                     linked.EventParticipantId == participant.Id && linked.ReleasedAt == null &&
                                                     linked.EventRole == EventCharacterRole.Playing && linked.SignupQuestionId != null &&
                                                     db.SignupQuestions.Any(question =>
                                                         question.Id == linked.SignupQuestionId && question.EventId == participant.EventId &&
                                                         question.Active && question.Type == SignupQuestionType.Account &&
                                                         question.SystemField == SignupSystemField.PrimaryRegularAccount &&
                                                         question.AccountAnswerRole == EventCharacterRole.Playing)) &&
                                                 !db.EventParticipantCharacters.Any(other =>
                                                     other.Id != assignment.Id && other.EventParticipantId == participant.Id &&
                                                     other.SignupQuestionId == null && other.ReleasedAt == null &&
                                                     other.EventRole == EventCharacterRole.Playing &&
                                                     other.RegistrationOrder < assignment.RegistrationOrder)
                                           select new EventParticipantAuthority
                                           {
                                               ParticipantId = participant.Id,
                                               EventId = participant.EventId,
                                               OsrsCharacterId = assignment.OsrsCharacterId,
                                               Name = character.DisplayName,
                                               NormalizedName = character.NormalizedName,
                                               Ehb = assignment.EhbSnapshot!.Value,
                                               AssignmentId = assignment.Id
                                           };

        return linkedPrimary.Concat(compatibilityLinkedPrimary)
            .Concat(compatibilityUnlinkedPrimary);
    }

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
               OsrsCharacterId = assignment.OsrsCharacterId,
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
    public Guid OsrsCharacterId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string NormalizedName { get; init; } = string.Empty;
    public decimal Ehb { get; init; }
    public Guid AssignmentId { get; init; }
}
