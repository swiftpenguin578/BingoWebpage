using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Evidence;

public sealed class EvidenceAuthority(ApplicationDbContext db) : IEvidenceAuthority
{
    public async Task<EvidenceActorScope> ResolveActorAsync(Guid actorAccountId, Guid? eventId, Guid? teamId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorAccountId && x.Active, cancellationToken)
            ?? throw new InvalidOperationException("The submitting account is not active.");
        if (account.AccountType == AccountType.WebsiteAccount && account.GlobalRole is GlobalRole.Admin or GlobalRole.SuperAdmin)
            return new(EvidenceActorKind.Administrator, actorAccountId, eventId ?? Guid.Empty, teamId ?? Guid.Empty, Guid.Empty);

        if (account.AccountType == AccountType.EmergencyCaptain)
        {
            if (eventId is not Guid emergencyEventId || teamId is not Guid emergencyTeamId)
                throw new InvalidOperationException("Emergency evidence access requires an event and team scope.");
            var access = await db.AccountEventAccesses.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == actorAccountId && x.EventId == emergencyEventId && x.TeamId == emergencyTeamId, cancellationToken);
            if (access?.GetAccessMode(now) == AccountAccessMode.Full)
                return new(EvidenceActorKind.EmergencyCaptain, actorAccountId, emergencyEventId, emergencyTeamId, Guid.Empty);
            throw new InvalidOperationException("This emergency evidence access is disabled, expired, or outside its team scope.");
        }

        var memberships = from participant in db.EventParticipants.AsNoTracking()
                          join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
                          join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
                          where participant.AccountId == actorAccountId && team.Active && membership.LeftAt == null && participant.EventId == team.EventId
                          select new { participant.EventId, TeamId = team.Id, ParticipantId = participant.Id, membership.Role };
        if (eventId is Guid selectedEvent) memberships = memberships.Where(x => x.EventId == selectedEvent);
        if (teamId is Guid selectedTeam) memberships = memberships.Where(x => x.TeamId == selectedTeam);
        var rows = await memberships.ToListAsync(cancellationToken);
        if (rows.Count != 1) throw new InvalidOperationException(rows.Count == 0 ? "The account is not an active event participant." : "Choose one event and team before submitting evidence.");
        var row = rows[0];
        return new(row.Role is TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain ? EvidenceActorKind.Captain : EvidenceActorKind.Participant,
            actorAccountId, row.EventId, row.TeamId, row.ParticipantId);
    }

    public async Task<EvidenceActorScope> AuthorizeAsync(Guid actorAccountId, Guid eventId, Guid teamId, Guid creditedParticipantId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveActorAsync(actorAccountId, eventId, teamId, now, cancellationToken);
        if (scope.Kind == EvidenceActorKind.Administrator) return scope with { CreditedParticipantId = creditedParticipantId };
        if (scope.Kind == EvidenceActorKind.Participant && scope.CreditedParticipantId != creditedParticipantId)
            throw new InvalidOperationException("Participants may submit evidence only for themselves.");
        if (scope.Kind is EvidenceActorKind.Captain or EvidenceActorKind.EmergencyCaptain &&
            !await IsEligibleTeamCreditAsync(teamId, creditedParticipantId, now, cancellationToken))
            throw new InvalidOperationException("The credited player is not eligible for this team at the evidence time.");
        return scope with { CreditedParticipantId = creditedParticipantId };
    }

    public async Task<bool> CanViewPrivateEvidenceAsync(Guid actorAccountId, Guid eventId, Guid teamId, Guid creditedParticipantId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await AuthorizeAsync(actorAccountId, eventId, teamId, creditedParticipantId, now, cancellationToken);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<EvidenceCandidate>> GetCurrentTeamCandidatesAsync(EvidenceActorScope scope, CancellationToken cancellationToken = default)
    {
        var candidates = from participant in db.EventParticipants.AsNoTracking()
                         join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
                         join character in db.PrimaryCharacters().AsNoTracking() on participant.Id equals character.ParticipantId
                         where participant.EventId == scope.EventId && membership.TeamId == scope.TeamId &&
                               (membership.LeftAt == null || (participant.SignupStatus == Bingo.Domain.Signups.SignupStatus.Withdrawn && participant.WithdrawnAt >= DateTimeOffset.UtcNow))
                         select new { participant.Id, character.Name };
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

    private async Task<bool> IsEligibleTeamCreditAsync(Guid teamId, Guid participantId, DateTimeOffset submittedAt, CancellationToken cancellationToken)
    {
        if (await db.TeamMemberships.AsNoTracking().AnyAsync(x => x.TeamId == teamId && x.EventParticipantId == participantId && x.LeftAt == null, cancellationToken))
            return true;

        return await (from participant in db.EventParticipants.AsNoTracking()
                      join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
                      where membership.TeamId == teamId && membership.EventParticipantId == participantId && membership.LeftAt != null &&
                            participant.SignupStatus == Bingo.Domain.Signups.SignupStatus.Withdrawn && participant.WithdrawnAt != null &&
                            submittedAt.ToUniversalTime() <= participant.WithdrawnAt.Value
                      select participant.Id).AnyAsync(cancellationToken);
    }
}
