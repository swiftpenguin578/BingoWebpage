using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Evidence;

public sealed class EvidenceAuthority(ApplicationDbContext db) : IEvidenceAuthority
{
    public async Task<EvidenceActorScope> ResolveActorAsync(Guid actorAccountId, Guid? eventId, Guid? teamId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorAccountId && x.Active && x.AccountType == AccountType.WebsiteAccount, cancellationToken)
            ?? throw new InvalidOperationException("The submitting account is not active.");
        if (eventId is Guid visibleEventId && !await db.Events.AsNoTracking().AnyAsync(x => x.Id == visibleEventId && x.HiddenAt == null, cancellationToken))
            throw new InvalidOperationException("The event was not found.");
        var memberships = from participant in db.EventParticipants.AsNoTracking()
                          join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
                          join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
                          join bingoEvent in db.Events.AsNoTracking() on participant.EventId equals bingoEvent.Id
                          where participant.AccountId == actorAccountId && team.Active && membership.LeftAt == null && bingoEvent.HiddenAt == null && participant.EventId == team.EventId
                          select new { participant.EventId, TeamId = team.Id, ParticipantId = participant.Id, membership.Role };
        if (eventId is Guid selectedEvent) memberships = memberships.Where(x => x.EventId == selectedEvent);
        if (teamId is Guid selectedTeam) memberships = memberships.Where(x => x.TeamId == selectedTeam);
        var rows = await memberships.ToListAsync(cancellationToken);
        if (rows.Count == 1)
        {
            var row = rows[0];
            return new(row.Role is TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain ? EvidenceActorKind.Captain : EvidenceActorKind.Participant,
                actorAccountId, row.EventId, row.TeamId, row.ParticipantId);
        }

        if (account.GlobalRole is GlobalRole.Admin or GlobalRole.SuperAdmin)
            return new(EvidenceActorKind.Administrator, actorAccountId, eventId ?? Guid.Empty, teamId ?? Guid.Empty, Guid.Empty);

        throw new InvalidOperationException(rows.Count == 0 ? "The account is not an active event participant." : "Choose one event and team before submitting evidence.");
    }

    public async Task<EvidenceActorScope> AuthorizeAsync(Guid actorAccountId, Guid eventId, Guid teamId, Guid creditedParticipantId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveActorAsync(actorAccountId, eventId, teamId, now, cancellationToken);
        if (scope.Kind == EvidenceActorKind.Administrator)
            throw new InvalidOperationException("Administrators have no submission authority without a current participant or team-leader role.");
        if (scope.Kind == EvidenceActorKind.Participant && scope.CreditedParticipantId != creditedParticipantId)
            throw new InvalidOperationException("Participants may submit evidence only for themselves.");
        if (scope.Kind is EvidenceActorKind.Captain &&
            !await db.TeamMemberships.AsNoTracking().AnyAsync(x => x.TeamId == teamId && x.EventParticipantId == creditedParticipantId && x.LeftAt == null, cancellationToken))
            throw new InvalidOperationException("Choose a current member of your team.");
        return scope with { CreditedParticipantId = creditedParticipantId };
    }

    public async Task<EvidenceActorScope> AuthorizeOwnerAsync(Guid actorAccountId, Guid eventId, Guid teamId, Guid creditedParticipantId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveActorAsync(actorAccountId, eventId, teamId, now, cancellationToken);
        if (scope.Kind is EvidenceActorKind.Participant or EvidenceActorKind.Captain && scope.CreditedParticipantId == creditedParticipantId)
            return scope with { CreditedParticipantId = creditedParticipantId };
        throw new InvalidOperationException("Only the credited participant may mutate this submission.");
    }

    public async Task<bool> CanViewPrivateEvidenceAsync(Guid actorAccountId, Guid eventId, Guid teamId, Guid creditedParticipantId, DateTimeOffset now, Guid? submissionId = null, CancellationToken cancellationToken = default)
    {
        if (!await db.Accounts.AsNoTracking().AnyAsync(x => x.Id == actorAccountId && x.Active && x.AccountType == AccountType.WebsiteAccount, cancellationToken)) return false;
        try
        {
            _ = await ResolveActorAsync(actorAccountId, eventId, teamId, now, cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            if (submissionId is not Guid retainedSubmissionId) return false;
            return await (from submission in db.Submissions.AsNoTracking()
                          join bingoEvent in db.Events.AsNoTracking() on submission.EventId equals bingoEvent.Id
                          join participant in db.EventParticipants.AsNoTracking() on submission.CreditedParticipantId equals participant.Id
                          join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
                          where submission.Id == retainedSubmissionId && submission.EventId == eventId && submission.TeamId == teamId &&
                                submission.CreditedParticipantId == creditedParticipantId && bingoEvent.HiddenAt == null &&
                                bingoEvent.State == EventState.Archived && participant.EventId == eventId && participant.AccountId == actorAccountId &&
                                membership.TeamId == teamId &&
                                (submission.Status == SubmissionStatus.Rejected || submission.Status == SubmissionStatus.Withdrawn)
                          select submission.Id).AnyAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<EvidenceCandidate>> GetCurrentTeamCandidatesAsync(EvidenceActorScope scope, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (scope.Kind is EvidenceActorKind.EmergencyCaptain or EvidenceActorKind.Administrator)
            throw new InvalidOperationException("This account is not available.");
        // Name each candidate by the Playing account ResolveCreditedCharacterAsync
        // would credit now: the active account after a switch, else the primary.
        var at = ParticipantAttributionLock.AtDatabasePrecision(now.ToUniversalTime());
        var active = db.ActiveCharactersAt(at);
        var candidates = from participant in db.EventParticipants.AsNoTracking()
                         join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
                         join character in db.PrimaryCharacters().AsNoTracking() on participant.Id equals character.ParticipantId
                         where participant.EventId == scope.EventId && membership.TeamId == scope.TeamId &&
                               membership.LeftAt == null
                         select new
                         {
                             participant.Id,
                             Name = (from current in active
                                     join currentCharacter in db.OsrsCharacters on current.OsrsCharacterId equals currentCharacter.Id
                                     where current.ParticipantId == participant.Id
                                     select currentCharacter.DisplayName).FirstOrDefault() ?? character.Name
                         };
        if (scope.Kind == EvidenceActorKind.Participant) candidates = candidates.Where(x => x.Id == scope.CreditedParticipantId);
        return (await candidates.OrderBy(x => x.Name).ToListAsync(cancellationToken)).Select(x => new EvidenceCandidate(x.Id, x.Name)).ToList();
    }

    public async Task<CreditedCharacterSnapshot> ResolveCreditedCharacterAsync(Guid eventId, Guid participantId, DateTimeOffset submittedAt, CancellationToken cancellationToken = default)
    {
        var at = submittedAt.ToUniversalTime();
        var transition = await db.ActiveCharacterAtAsync(eventId, participantId, at, cancellationToken);
        var characterId = transition?.OsrsCharacterId;
        if (characterId is null)
        {
            if (await db.EventParticipantCharacterSwaps.AsNoTracking().AnyAsync(x => x.EventId == eventId && x.EventParticipantId == participantId, cancellationToken))
                throw new InvalidOperationException("The credited participant has no active Playing account at the evidence time.");
            var fallback = await (from assignment in db.EventParticipantCharacters.AsNoTracking()
                                  join participant in db.EventParticipants.AsNoTracking() on assignment.EventParticipantId equals participant.Id
                                  where assignment.EventId == eventId && assignment.EventParticipantId == participantId && participant.EventId == eventId &&
                                        assignment.EventRole == Bingo.Domain.Signups.EventCharacterRole.Playing && assignment.ReleasedAt == null
                                  select assignment.OsrsCharacterId).ToListAsync(cancellationToken);
            if (fallback.Count != 1)
                throw new InvalidOperationException($"The credited character for retained participant {participantId} is missing or ambiguous.");
            characterId = fallback[0];
        }

        var character = await db.OsrsCharacters.AsNoTracking().SingleOrDefaultAsync(x => x.Id == characterId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"The credited character {characterId} for participant {participantId} does not exist.");
        return new(character.Id, character.DisplayName);
    }

}
