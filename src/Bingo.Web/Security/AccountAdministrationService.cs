using System.Text.Json;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Security;

public sealed class AccountAdministrationService(ApplicationDbContext db, IPasswordHasher<Account> passwords, TimeProvider time)
{
    public async Task GrantAdminAsync(Guid actorId, Guid targetId, long expectedAuthorizationVersion, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (actor, target) = await LoadPair(actorId, targetId, ct);
        RequireFreshTarget(target, expectedAuthorizationVersion);
        RequireOwner(actor); RequireWebsite(target); if (!target.Active) throw new AccountActionException("Restore this account before granting Admin access.");
        if (target.GlobalRole != GlobalRole.User) throw new AccountActionException("Only a User can be granted Admin access.");
        target.SetGlobalRole(GlobalRole.Admin);
        await AccountResetTokenPolicy.SupersedeResetTokensAsync(db, target.Id, time.GetUtcNow(), ct);
        Audit(actor, "account.admin_granted", target, "User", "Admin"); Notify(target, "account.admin_granted", "/Account/Settings");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task RevokeAdminAsync(Guid actorId, Guid targetId, long expectedAuthorizationVersion, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (actor, target) = await LoadPair(actorId, targetId, ct);
        RequireFreshTarget(target, expectedAuthorizationVersion);
        RequireOwner(actor); if (target.GlobalRole != GlobalRole.Admin) throw new AccountActionException("Only an Admin can be revoked.");
        target.SetGlobalRole(GlobalRole.User);
        await AccountResetTokenPolicy.SupersedeResetTokensAsync(db, target.Id, time.GetUtcNow(), ct);
        Audit(actor, "account.admin_revoked", target, "Admin", "User"); Notify(target, "account.admin_revoked", "/Account/Settings");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task TransferOwnershipAsync(Guid actorId, string actorPassword, string destinationUsername, CancellationToken ct)
    {
        var destination = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.NormalizedLoginName == AccountAuthenticationService.NormalizeUsername(destinationUsername), ct)
            ?? throw new AccountActionException("The selected account is no longer available.");
        await TransferOwnershipAsync(actorId, actorPassword, destination.Id, destination.AuthorizationVersion, ct);
    }

    public async Task TransferOwnershipAsync(Guid actorId, string actorPassword, Guid destinationId, long expectedAuthorizationVersion, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (actor, destination) = await LoadPair(actorId, destinationId, ct);
        RequireOwner(actor);
        RequireFreshTarget(destination, expectedAuthorizationVersion);
        if (actor.PasswordHash is null || passwords.VerifyHashedPassword(actor, actor.PasswordHash, actorPassword) == PasswordVerificationResult.Failed) throw new AccountActionException("The current password is incorrect.");
        if (destination.Id == actor.Id || destination.GlobalRole == GlobalRole.SuperAdmin) throw new AccountActionException("Choose another active website account.");
        if (!destination.Active) throw new AccountActionException("The selected account is disabled. Restore it before transferring ownership.");
        var before = destination.GlobalRole?.ToString() ?? "none";
        destination.SetGlobalRole(GlobalRole.SuperAdmin);
        actor.SetGlobalRole(GlobalRole.Admin);
        var now = time.GetUtcNow();
        await AccountResetTokenPolicy.SupersedeResetTokensAsync(db, destination.Id, now, ct);
        await AccountResetTokenPolicy.SupersedeResetTokensAsync(db, actor.Id, now, ct);
        Audit(actor, "account.ownership_transferred", destination, before, "SuperAdmin"); Audit(actor, "account.ownership_transferred", actor, "SuperAdmin", "Admin");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task DisableAsync(Guid actorId, Guid targetId, string reason, long expectedAuthorizationVersion, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) throw new AccountActionException("A disable reason between 1 and 500 characters is required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct); var (actor, target) = await LoadPair(actorId, targetId, ct);
        RequireFreshTarget(target, expectedAuthorizationVersion);
        RequireManageTarget(actor, target);
        if (!target.Active) throw new StaleAccountChangeException();
        var now = time.GetUtcNow();
        target.Disable(now, actor.Id, reason);
        await AccountResetTokenPolicy.SupersedeResetTokensAsync(db, target.Id, now, ct);
        Audit(actor, "account.disabled", target, "Active", "Disabled", details: reason.Trim()); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task RestoreAsync(Guid actorId, Guid targetId, long expectedAuthorizationVersion, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); var (actor, target) = await LoadPair(actorId, targetId, ct);
        RequireFreshTarget(target, expectedAuthorizationVersion);
        RequireManageTarget(actor, target);
        if (target.Active) throw new StaleAccountChangeException();
        target.Enable(); Audit(actor, "account.restored", target, "Disabled", "Active"); Notify(target, "account.restored", "/Account/Settings"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public Task SetEmergencyEnabledAsync(Guid actorId, Guid targetId, bool enabled, CancellationToken ct) =>
        throw new InvalidOperationException("This account is not available.");
    private async Task<(Account Actor, Account Target)> LoadPair(Guid actorId, Guid targetId, CancellationToken ct)
    {
        // Lock both identities in the same order, including the authorizing actor.
        // Refresh tracked instances after acquiring locks so concurrent role changes
        // cannot authorize a later operation with stale in-memory state.
        var accounts = await db.Accounts.FromSqlInterpolated($"SELECT * FROM accounts WHERE id = {actorId} OR id = {targetId} ORDER BY id FOR UPDATE").ToListAsync(ct);
        foreach (var account in accounts) await db.Entry(account).ReloadAsync(ct);
        var actor = accounts.SingleOrDefault(x => x.Id == actorId) ?? throw new AccountActionException("This account is no longer available.");
        var target = accounts.SingleOrDefault(x => x.Id == targetId) ?? throw new AccountActionException("The selected account is no longer available.");
        RequireWebsite(actor); RequireWebsite(target);
        if (!actor.Active) throw new AccountActionException("Your account is disabled. Sign in again after access is restored.");
        return (actor, target);
    }
    private static void RequireFreshTarget(Account target, long expectedAuthorizationVersion)
    {
        if (target.AuthorizationVersion != expectedAuthorizationVersion) throw new StaleAccountChangeException();
    }
    private static void RequireOwner(Account account) { if (account.AccountType != AccountType.WebsiteAccount || account.GlobalRole != GlobalRole.SuperAdmin || !account.Active) throw new AccountActionException("Only the active Super Admin can perform this action."); }
    private static void RequireWebsite(Account account) { if (account.AccountType != AccountType.WebsiteAccount) throw new InvalidOperationException("This account is not available."); }
    private static void RequireManageTarget(Account actor, Account target)
    {
        if (actor.Id == target.Id) throw new AccountActionException("You cannot disable or restore your own account.");
        if (target.GlobalRole == GlobalRole.SuperAdmin) throw new AccountActionException("The Super Admin account is protected. Use ownership transfer or operator recovery.");
        if (actor.GlobalRole is not (GlobalRole.Admin or GlobalRole.SuperAdmin) || (actor.GlobalRole == GlobalRole.Admin && target.GlobalRole != GlobalRole.User))
            throw new AccountActionException("You do not have permission to manage this account.");
    }
    private void Audit(Account actor, string action, Account target, string before, string after, Guid? eventId = null, string? details = null) => db.AuditEntries.Add(new AuditEntry(
        Guid.NewGuid(), time.GetUtcNow(), actor.Id, actor.LoginName, action, "account", target.Id.ToString(),
        details ?? $"Role/state changed from {before} to {after}.",
        eventId: eventId,
        beforeState: JsonSerializer.Serialize(new { roleOrState = before }),
        afterState: JsonSerializer.Serialize(new { roleOrState = after })));
    private void Notify(Account target, string type, string route) =>
        db.PersonalNotifications.Add(new PersonalNotification(Guid.NewGuid(), target.Id, type, string.Empty, route, time.GetUtcNow()));
}

public sealed class StaleAccountChangeException : InvalidOperationException;
public sealed class AccountActionException(string message) : InvalidOperationException(message);
