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
    public async Task<IReadOnlyList<GeneratedCaptainCredential>> ProvisionEventAsync(
        Guid eventId,
        Guid actorId,
        string actorName,
        CancellationToken ct)
    {
        var teamIds = await db.Teams.AsNoTracking().Where(x => x.EventId == eventId && x.Active).Select(x => x.Id).ToListAsync(ct);
        var assignments = await (from membership in db.TeamMemberships.AsNoTracking()
                                 join participant in db.EventParticipants on membership.EventParticipantId equals participant.Id
                                 where teamIds.Contains(membership.TeamId) && membership.LeftAt == null &&
                                       (membership.Role == TeamMembershipRole.Captain || membership.Role == TeamMembershipRole.CoCaptain)
                                 select new { membership.TeamId, Participant = participant, membership.Role })
            .ToListAsync(ct);
        var generated = new List<GeneratedCaptainCredential>();
        foreach (var assignment in assignments)
        {
            var credential = await ProvisionAsync(eventId, assignment.TeamId, assignment.Participant.Id, assignment.Participant.PrimaryAccountName, assignment.Role, actorId, actorName, ct);
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
                                join participant in db.EventParticipants on membership.EventParticipantId equals participant.Id
                                where membership.TeamId == teamId && membership.EventParticipantId == participantId && membership.LeftAt == null &&
                                      (membership.Role == TeamMembershipRole.Captain || membership.Role == TeamMembershipRole.CoCaptain)
                                select new { Participant = participant, membership.Role }).SingleOrDefaultAsync(ct);
        return assignment is null ? null : await ProvisionAsync(eventId, teamId, participantId, assignment.Participant.PrimaryAccountName, assignment.Role, actorId, actorName, ct);
    }

    public async Task DisableParticipantAsync(Guid participantId, Guid actorId, string actorName, string reason, CancellationToken ct)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(x => x.CaptainParticipantId == participantId, ct);
        if (account is null || account.DisabledAt is not null) return;
        account.Disable(time.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await auditWriter.WriteAsync(actorId, actorName, "account.captain_auto_disabled", "account", account.Id.ToString(), reason, ct);
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
        var ev = await db.Events.AsNoTracking().SingleAsync(x => x.Id == eventId, ct);
        var teamName = await db.Teams.AsNoTracking().Where(x => x.Id == teamId).Select(x => x.Name).SingleAsync(ct);
        var existing = await db.Accounts.SingleOrDefaultAsync(x => x.CaptainParticipantId == participantId, ct);
        if (existing is not null)
        {
            existing.ScopeCaptain(eventId, teamId, ev.EventStartsAt, ev.SubmissionCutoffAt, ev.EventEndsAt.AddHours(24), participantId);
            if (existing.DisabledAt is not null) existing.Enable(ev.EventEndsAt.AddHours(24));
            await db.SaveChangesAsync(ct);
            return null;
        }

        var username = await GenerateUniqueUsername(playerName, ct);
        var password = GeneratePassword();
        var account = new Account(Guid.NewGuid(), username, AccountAuthenticationService.NormalizeUsername(username), AccountRole.Captain, time.GetUtcNow());
        account.ScopeCaptain(eventId, teamId, ev.EventStartsAt, ev.SubmissionCutoffAt, ev.EventEndsAt.AddHours(24), participantId);
        account.SetPasswordHash(passwordHasher.HashPassword(account, password), mustChangePassword: true);
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        await auditWriter.WriteAsync(actorId, actorName, "account.captain_auto_created", "account", account.Id.ToString(), $"{role}: {playerName}; team {teamName}", ct);
        return new GeneratedCaptainCredential(playerName, teamName, role, username, password);
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
            if (!await db.Accounts.AnyAsync(x => x.NormalizedUsername == normalized, ct)) return candidate;
        }
        return $"{root[..Math.Min(root.Length, 88)]}{RandomNumberGenerator.GetInt32(100000, 1000000)}";
    }

    private static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@$%";
        const string all = upper + lower + digits + symbols;
        Span<char> value = stackalloc char[16];
        value[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        value[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        value[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        value[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];
        for (var index = 4; index < value.Length; index++) value[index] = all[RandomNumberGenerator.GetInt32(all.Length)];
        for (var index = value.Length - 1; index > 0; index--)
        {
            var swap = RandomNumberGenerator.GetInt32(index + 1);
            (value[index], value[swap]) = (value[swap], value[index]);
        }
        return new string(value);
    }
}

public sealed record GeneratedCaptainCredential(string PlayerName, string TeamName, TeamMembershipRole Role, string Username, string Password);
