using System.Security.Cryptography;
using Bingo.Application.Auditing;
using Bingo.Domain.Access;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Security;

public sealed class CaptainAccountProvisioner(
    ApplicationDbContext db,
    IPasswordHasher<Account> passwordHasher,
    IAuditWriter auditWriter,
    TimeProvider time)
{
    private readonly IPasswordHasher<Account> legacyPasswordHasher = passwordHasher;
    public async Task<IReadOnlyList<GeneratedCaptainCredential>> ProvisionEventAsync(
        Guid eventId,
        Guid actorId,
        string actorName,
        CancellationToken ct)
    {
        var teamIds = await db.Teams.AsNoTracking().Where(x => x.EventId == eventId && x.Active).Select(x => x.Id).ToListAsync(ct);
        var assignments = await (from membership in db.TeamMemberships.AsNoTracking()
                                 join participant in db.PrimaryCharacters() on membership.EventParticipantId equals participant.ParticipantId
                                 where teamIds.Contains(membership.TeamId) && membership.LeftAt == null &&
                                       (membership.Role == TeamMembershipRole.Captain || membership.Role == TeamMembershipRole.CoCaptain)
                                 select new { membership.TeamId, Participant = participant, membership.Role })
            .ToListAsync(ct);
        var generated = new List<GeneratedCaptainCredential>();
        foreach (var assignment in assignments)
        {
            var credential = await ProvisionAsync(eventId, assignment.TeamId, assignment.Participant.ParticipantId, assignment.Participant.Name, assignment.Role, actorId, actorName, ct);
            if (credential is not null) generated.Add(credential);
        }
        return generated;
    }

    public async Task<GeneratedCaptainCredential?> ProvisionParticipantAsync(
        Guid eventId,
        Guid teamId,
        Guid participantId,
        Guid actorId,
        string actorName,
        CancellationToken ct)
    {
        var assignment = await (from membership in db.TeamMemberships.AsNoTracking()
                                join participant in db.PrimaryCharacters() on membership.EventParticipantId equals participant.ParticipantId
                                where membership.TeamId == teamId && membership.EventParticipantId == participantId && membership.LeftAt == null &&
                                      (membership.Role == TeamMembershipRole.Captain || membership.Role == TeamMembershipRole.CoCaptain)
                                select new { Participant = participant, membership.Role }).SingleOrDefaultAsync(ct);
        return assignment is null ? null : await ProvisionAsync(eventId, teamId, participantId, assignment.Participant.Name, assignment.Role, actorId, actorName, ct);
    }

    public async Task DisableParticipantAsync(Guid participantId, Guid actorId, string actorName, string reason, CancellationToken ct)
    {
        var access = await db.AccountEventAccesses.SingleOrDefaultAsync(x => x.ParticipantId == participantId, ct);
        if (access is null) return;
        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == access.AccountId, ct);
        if (account is null) return;
        account.Disable(time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await auditWriter.WriteAsync(actorId, actorName, "account.captain_auto_disabled", "account", account.Id.ToString(), reason, access.EventId, ct);
    }

    private async Task<GeneratedCaptainCredential?> ProvisionAsync(
        Guid eventId,
        Guid teamId,
        Guid participantId,
        string playerName,
        TeamMembershipRole role,
        Guid actorId,
        string actorName,
        CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId && x.HiddenAt == null, ct);
        var teamName = await db.Teams.AsNoTracking().Where(x => x.Id == teamId).Select(x => x.Name).SingleAsync(ct);
        var existingAccess = await db.AccountEventAccesses.SingleOrDefaultAsync(x => x.ParticipantId == participantId, ct);
        if (existingAccess is not null)
        {
            await db.SaveChangesAsync(ct);
            return null;
        }

        var username = await GenerateUniqueUsername(playerName, ct);
        var now = time.GetUtcNow();
        var account = Account.CreateEmergency(Guid.NewGuid(), username, AccountAuthenticationService.NormalizeUsername(username), now);
        db.Accounts.Add(account);
        db.AccountEventAccesses.Add(new AccountEventAccess(Guid.NewGuid(), account.Id, eventId, teamId, participantId, ev.EventStartsAt, null, null));
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.PasswordCredentialTokens.Add(new PasswordCredentialToken(Guid.NewGuid(), account.Id, PasswordCredentialTokenPurpose.EmergencySetup, AccountIdentityService.Hash(token), now.AddMinutes(60), now, actorId));
        await db.SaveChangesAsync(ct);
        await auditWriter.WriteAsync(actorId, actorName, "account.captain_auto_created", "account", account.Id.ToString(), $"{role}: {playerName}; team {teamName}", eventId, ct);
        return new GeneratedCaptainCredential(playerName, teamName, role, username, token);
    }

    private async Task<string> GenerateUniqueUsername(string playerName, CancellationToken ct)
    {
        var root = string.Concat(playerName.Where(char.IsLetterOrDigit));
        if (string.IsNullOrWhiteSpace(root)) root = "Captain";
        if (root.Length > 90) root = root[..90];
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var candidate = $"{root}{RandomNumberGenerator.GetInt32(1000, 10000)}";
            var normalized = AccountAuthenticationService.NormalizeUsername(candidate);
            if (!await db.Accounts.AnyAsync(x => x.NormalizedLoginName == normalized, ct)) return candidate;
        }
        return $"{root[..Math.Min(root.Length, 88)]}{RandomNumberGenerator.GetInt32(100000, 1000000)}";
    }

}

public sealed record GeneratedCaptainCredential(string PlayerName, string TeamName, TeamMembershipRole Role, string Username, string SetupToken)
{
    public string Password => SetupToken;
}
