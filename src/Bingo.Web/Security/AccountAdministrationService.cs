using System.Text.Json;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Security;

public sealed class AccountAdministrationService(ApplicationDbContext db, IPasswordHasher<Account> passwords, TimeProvider time)
{
    public async Task GrantAdminAsync(Guid actorId, Guid targetId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (actor, target) = await LoadPair(actorId, targetId, ct);
        RequireOwner(actor); RequireWebsite(target); if (target.GlobalRole != GlobalRole.User) throw new InvalidOperationException("Only a User can be granted Admin access.");
        target.SetGlobalRole(GlobalRole.Admin); Audit(actor, "account.admin_granted", target, "User", "Admin"); Notify(target, "account.admin_granted", "/Account/Settings");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task RevokeAdminAsync(Guid actorId, Guid targetId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (actor, target) = await LoadPair(actorId, targetId, ct);
        RequireOwner(actor); if (target.GlobalRole != GlobalRole.Admin) throw new InvalidOperationException("Only an Admin can be revoked.");
        target.SetGlobalRole(GlobalRole.User); Audit(actor, "account.admin_revoked", target, "Admin", "User"); Notify(target, "account.admin_revoked", "/Account/Settings");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task TransferOwnershipAsync(Guid actorId, string actorPassword, string destinationUsername, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var actor = await db.Accounts.SingleAsync(x => x.Id == actorId, ct); RequireOwner(actor);
        if (actor.PasswordHash is null || passwords.VerifyHashedPassword(actor, actor.PasswordHash, actorPassword) == PasswordVerificationResult.Failed) throw new InvalidOperationException("The current password is incorrect.");
        var destination = await db.Accounts.SingleAsync(x => x.NormalizedLoginName == AccountAuthenticationService.NormalizeUsername(destinationUsername), ct);
        if (destination.Id == actor.Id || destination.AccountType != AccountType.WebsiteAccount || !destination.Active) throw new InvalidOperationException("Choose another active website account.");
        var before = destination.GlobalRole?.ToString() ?? "none"; destination.SetGlobalRole(GlobalRole.SuperAdmin); actor.SetGlobalRole(GlobalRole.Admin);
        Audit(actor, "account.ownership_transferred", destination, before, "SuperAdmin"); Audit(actor, "account.ownership_transferred", actor, "SuperAdmin", "Admin");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task DisableAsync(Guid actorId, Guid targetId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("A disable reason is required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct); var (actor, target) = await LoadPair(actorId, targetId, ct);
        if (actor.Id == target.Id || target.GlobalRole == GlobalRole.SuperAdmin || (actor.GlobalRole == GlobalRole.Admin && target.GlobalRole != GlobalRole.User) || actor.GlobalRole is not (GlobalRole.Admin or GlobalRole.SuperAdmin)) throw new InvalidOperationException("You cannot disable this account.");
        target.Disable(time.GetUtcNow(), actor.Id, reason); Audit(actor, "account.disabled", target, "Active", "Disabled"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task RestoreAsync(Guid actorId, Guid targetId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); var (actor, target) = await LoadPair(actorId, targetId, ct);
        if (actor.GlobalRole is not (GlobalRole.Admin or GlobalRole.SuperAdmin) || target.GlobalRole == GlobalRole.SuperAdmin || (actor.GlobalRole == GlobalRole.Admin && target.GlobalRole != GlobalRole.User)) throw new InvalidOperationException("You cannot restore this account.");
        target.Enable(); Audit(actor, "account.restored", target, "Disabled", "Active"); Notify(target, "account.restored", "/Account/Settings"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task SetEmergencyEnabledAsync(Guid actorId, Guid targetId, bool enabled, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (actor, target) = await LoadPair(actorId, targetId, ct);
        if (actor.GlobalRole is not (GlobalRole.Admin or GlobalRole.SuperAdmin) || target.AccountType != AccountType.EmergencyCaptain)
            throw new InvalidOperationException("Only an administrator can manage an emergency credential.");
        var accesses = await db.AccountEventAccesses.Where(x => x.AccountId == target.Id).ToListAsync(ct);
        if (accesses.Count == 0) throw new InvalidOperationException("This emergency credential has no event access scope.");
        if (enabled)
        {
            if (target.PasswordHash is null) throw new InvalidOperationException("Create and use a setup link before enabling this credential.");
            var now = time.GetUtcNow();
            var events = await db.Events.Where(x => accesses.Select(access => access.EventId).Contains(x.Id)).ToListAsync(ct);
            if (events.Count != accesses.Count || events.Any(bingoEvent => !bingoEvent.AcceptsEmergencySubmissions(now)))
                throw new InvalidOperationException("Submissions must be explicitly open before enabling an emergency credential.");
            target.Enable();
            foreach (var access in accesses) access.Enable();
            Audit(actor, "account.emergency_enabled", target, "Disabled", "Enabled");
        }
        else
        {
            target.Disable(time.GetUtcNow(), actor.Id, "Disabled by administrator");
            foreach (var access in accesses) access.Disable();
            Audit(actor, "account.emergency_disabled", target, "Enabled", "Disabled");
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    private async Task<(Account Actor, Account Target)> LoadPair(Guid actorId, Guid targetId, CancellationToken ct) => (await db.Accounts.SingleAsync(x => x.Id == actorId, ct), await db.Accounts.SingleAsync(x => x.Id == targetId, ct));
    private static void RequireOwner(Account account) { if (account.GlobalRole != GlobalRole.SuperAdmin || !account.Active) throw new InvalidOperationException("Only the active Super Admin can perform this action."); }
    private static void RequireWebsite(Account account) { if (account.AccountType != AccountType.WebsiteAccount) throw new InvalidOperationException("Emergency credentials cannot hold global roles."); }
    private void Audit(Account actor, string action, Account target, string before, string after) => db.AuditEntries.Add(new AuditEntry(
        Guid.NewGuid(), time.GetUtcNow(), actor.Id, actor.LoginName, action, "account", target.Id.ToString(),
        $"Role/state changed from {before} to {after}.",
        beforeState: JsonSerializer.Serialize(new { roleOrState = before }),
        afterState: JsonSerializer.Serialize(new { roleOrState = after })));
    private void Notify(Account target, string type, string route) =>
        db.PersonalNotifications.Add(new PersonalNotification(Guid.NewGuid(), target.Id, type, string.Empty, route, time.GetUtcNow()));
}
