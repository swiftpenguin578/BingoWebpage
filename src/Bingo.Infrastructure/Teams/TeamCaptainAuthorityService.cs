using System.Data;
using System.Text.Json;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Infrastructure.Teams;

public sealed class TeamCaptainAuthorityService(ApplicationDbContext db, TimeProvider time) : ITeamCaptainAuthorityService
{
    public async Task<TeamCaptainRoleChangeResult> ChangeRoleAsync(TeamCaptainRoleChange change, CancellationToken ct = default)
    {
        if (change.Role is not (TeamMembershipRole.Participant or TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain))
            return new(false, "Choose Participant, Captain, or Co-captain.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var now = time.GetUtcNow();
            var row = await (from membership in db.TeamMemberships
                             join team in db.Teams on membership.TeamId equals team.Id
                             join participant in db.EventParticipants on membership.EventParticipantId equals participant.Id
                             join item in db.Events on team.EventId equals item.Id
                             where membership.Id == change.MembershipId && membership.LeftAt == null && item.HiddenAt == null
                             select new { membership, team, participant, item }).SingleOrDefaultAsync(ct);
            if (row is null || row.team.EventId != change.EventId || row.participant.EventId != change.EventId)
                return new(false, "That current team membership no longer exists.");
            if (row.item.State is EventState.Cancelled or EventState.Finalized or EventState.Archived or EventState.Discarded)
                return new(false, "This event is read-only in its current lifecycle state.");
            if (row.membership.Role == change.Role)
                return new(false, "That member already has this role.");

            var previous = row.membership.Role;
            row.membership.ChangeRole(change.Role);
            db.TeamMembershipRoleTransitions.Add(new TeamMembershipRoleTransition(Guid.NewGuid(), row.membership.Id, previous, change.Role, change.ActorAccountId, now));
            db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, change.ActorAccountId, change.ActorUsername,
                "team.membership_role_changed", "membership", row.membership.Id.ToString(), null, change.EventId,
                JsonSerializer.Serialize(new { role = previous.ToString() }), JsonSerializer.Serialize(new { role = change.Role.ToString(), teamId = row.team.Id })));

            var owner = row.participant.AccountId is { } ownerId
                ? await db.Accounts.SingleOrDefaultAsync(x => x.Id == ownerId && x.Active && x.AccountType == AccountType.WebsiteAccount, ct)
                : null;
            if (owner is not null)
                db.PersonalNotifications.Add(new PersonalNotification(Guid.NewGuid(), owner.Id, "Team role updated", "Your team role was updated by an administrator.", $"/Events/{row.item.Slug}/Teams", now, row.item.Id));

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            var participantName = await db.PrimaryCharacters().Where(x => x.ParticipantId == row.participant.Id).Select(x => x.Name).SingleOrDefaultAsync(ct) ?? "Member";
            return new(true, ParticipantName: participantName);
        }
        catch (Exception exception) when (IsExpectedConflict(exception))
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return new(false, "Another administrator changed this membership first. The latest roster has been loaded.");
        }
    }

    public Task<bool> HasCurrentCaptainAuthorityAsync(Guid accountId, Guid eventId, Guid? teamId = null, CancellationToken ct = default) =>
        (from participant in db.EventParticipants.AsNoTracking()
         join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
         join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
         join account in db.Accounts.AsNoTracking() on participant.AccountId equals account.Id
         join bingoEvent in db.Events.AsNoTracking() on participant.EventId equals bingoEvent.Id
         where participant.EventId == eventId && bingoEvent.HiddenAt == null && participant.AccountId == accountId && account.Active && account.AccountType == AccountType.WebsiteAccount &&
               membership.LeftAt == null && team.Active && (teamId == null || team.Id == teamId) &&
               (membership.Role == TeamMembershipRole.Captain || membership.Role == TeamMembershipRole.CoCaptain)
         select membership.Id).AnyAsync(ct);

    public async Task<bool> HasDraftSignupTableAccessAsync(Guid accountId, Guid eventId, CancellationToken ct = default)
    {
        var draft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId, ct);
        return draft is not null && draft.State is DraftState.Setup or DraftState.Running or DraftState.Paused &&
               await (from participant in db.EventParticipants.AsNoTracking()
                      join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
                      join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
                      join account in db.Accounts.AsNoTracking() on participant.AccountId equals account.Id
                      where participant.EventId == eventId && participant.AccountId == accountId && account.Active && account.AccountType == AccountType.WebsiteAccount &&
                            membership.LeftAt == null && team.Active && team.FormationType == TeamFormationType.Drafted &&
                            (membership.Role == TeamMembershipRole.Captain || membership.Role == TeamMembershipRole.CoCaptain)
                      select membership.Id).AnyAsync(ct);
    }

    private static bool IsExpectedConflict(Exception exception) => exception is DbUpdateConcurrencyException or DbUpdateException or InvalidOperationException or PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation };
}
