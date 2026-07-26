using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Security;

/// <summary>Creates the explicit, disabled emergency fallback and its audited event scope atomically.</summary>
public sealed class EmergencyCredentialService(ApplicationDbContext db, TimeProvider time)
{
    public async Task<Account> CreateAsync(Guid actorId, string username, Guid eventId, Guid teamId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var actor = await db.Accounts.SingleAsync(account => account.Id == actorId, ct);
        if (!actor.Active || actor.GlobalRole is not (GlobalRole.Admin or GlobalRole.SuperAdmin)) throw new InvalidOperationException("Only an active administrator can create an emergency credential.");
        var bingoEvent = await db.Events.SingleOrDefaultAsync(item => item.Id == eventId, ct) ?? throw new InvalidOperationException("The event no longer exists.");
        if (bingoEvent.SubmissionCutoffAt <= time.GetUtcNow()) throw new InvalidOperationException("Emergency credentials cannot be created after the submission cutoff.");
        if (!await db.Teams.AnyAsync(team => team.Id == teamId && team.EventId == eventId && team.Active, ct)) throw new InvalidOperationException("Choose a team belonging to the selected event.");
        var normalized = AccountAuthenticationService.NormalizeUsername(username);
        if (await db.Accounts.AnyAsync(account => account.NormalizedLoginName == normalized, ct)) throw new InvalidOperationException("That username is already in use.");
        var account = Account.CreateEmergency(Guid.NewGuid(), username.Trim(), normalized, time.GetUtcNow());
        db.Accounts.Add(account);
        db.AccountEventAccesses.Add(new AccountEventAccess(Guid.NewGuid(), account.Id, eventId, teamId, null, bingoEvent.EventStartsAt, bingoEvent.SubmissionCutoffAt, null));
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), time.GetUtcNow(), actor.Id, actor.LoginName, "account.emergency_created", "account", account.Id.ToString(), "Disabled emergency credential created.", eventId));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return account;
    }
}
