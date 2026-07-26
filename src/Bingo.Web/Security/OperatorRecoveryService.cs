using System.Data;
using System.Security.Cryptography;
using System.Text;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Security;

/// <summary>Explicit command-line-only ownership recovery. It is intentionally not exposed as a web action.</summary>
public sealed class OperatorRecoveryService(
    ApplicationDbContext db,
    TimeProvider time,
    IPasswordHasher<Account> passwords)
{
    public async Task BootstrapOwnerAsync(string username, string password, string confirmation, CancellationToken ct)
    {
        var normalized = AccountAuthenticationService.NormalizeUsername(username);
        if (string.IsNullOrWhiteSpace(username) || normalized != AccountAuthenticationService.NormalizeUsername(confirmation))
            throw new InvalidOperationException("Bootstrap confirmation must exactly match the owner username.");
        AccountIdentityService.ValidatePassword(password);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.Accounts.AnyAsync(ct))
            throw new InvalidOperationException("Owner bootstrap requires an empty account table.");

        var now = time.GetUtcNow();
        var owner = Account.CreateWebsite(Guid.NewGuid(), username.Trim(), normalized, now);
        owner.SetPassword(passwords.HashPassword(owner, password), false, now, incrementVersion: false);
        owner.SetGlobalRole(GlobalRole.SuperAdmin);
        db.Accounts.Add(owner);
        db.AuditEntries.Add(new AuditEntry(
            Guid.NewGuid(), now, null, "System", "account.owner_bootstrapped", "account", owner.Id.ToString(),
            "Initial Super Admin provisioned through the controlled operator command.",
            afterState: "{\"role\":\"SuperAdmin\"}"));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task RecoverOwnerAsync(string username, string confirmation, CancellationToken ct)
    {
        var normalized = AccountAuthenticationService.NormalizeUsername(username);
        if (!string.Equals(normalized, AccountAuthenticationService.NormalizeUsername(confirmation), StringComparison.Ordinal))
            throw new InvalidOperationException("Recovery confirmation must exactly match the destination username.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var destination = await db.Accounts.SingleOrDefaultAsync(account => account.NormalizedLoginName == normalized, ct)
            ?? throw new InvalidOperationException("The requested website account does not exist.");
        if (destination.AccountType != AccountType.WebsiteAccount || !destination.Active)
            throw new InvalidOperationException("The requested owner must be an active website account.");

        var previousOwner = await db.Accounts.SingleOrDefaultAsync(account => account.GlobalRole == GlobalRole.SuperAdmin, ct);
        if (previousOwner?.Id == destination.Id) return;
        if (previousOwner is not null) previousOwner.SetGlobalRole(GlobalRole.Admin);
        destination.SetGlobalRole(GlobalRole.SuperAdmin);
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), time.GetUtcNow(), null, "System", "account.owner_recovered", "account", destination.Id.ToString(),
            "Owner recovery executed through the controlled operator command.", beforeState: previousOwner is null ? "null" : "{\"role\":\"SuperAdmin\"}", afterState: "{\"role\":\"SuperAdmin\"}"));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<string> CreateOwnerRecoveryResetLinkAsync(string username, string confirmation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || !string.Equals(username, confirmation, StringComparison.Ordinal))
            throw new InvalidOperationException("Owner reset confirmation must exactly match the owner username.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var owner = await db.Accounts.SingleOrDefaultAsync(account => account.LoginName == username, ct)
            ?? throw new InvalidOperationException("The requested owner does not exist.");
        if (owner.AccountType != AccountType.WebsiteAccount || !owner.Active || owner.GlobalRole != GlobalRole.SuperAdmin ||
            await db.Accounts.CountAsync(account => account.Active && account.GlobalRole == GlobalRole.SuperAdmin, ct) != 1)
            throw new InvalidOperationException("Owner reset requires the sole active Super Admin.");

        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({owner.Id.ToString()}, 0))", ct);
        var now = time.GetUtcNow();
        foreach (var token in await db.PasswordCredentialTokens.Where(token => token.AccountId == owner.Id && token.Purpose == PasswordCredentialTokenPurpose.OwnerRecovery && token.UsedAt == null && token.SupersededAt == null).ToListAsync(ct))
            token.Supersede(now);
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.PasswordCredentialTokens.Add(new PasswordCredentialToken(Guid.NewGuid(), owner.Id, PasswordCredentialTokenPurpose.OwnerRecovery, AccountIdentityService.Hash(raw), now.AddMinutes(60), now));
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, null, "System", "account.owner_recovery_reset_link_created", "account", owner.Id.ToString(), "Operator-only owner password-reset link created."));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return raw;
    }

    public async Task PromoteRetainedOwnerAsync(string username, string confirmation, CancellationToken ct)
    {
        var normalized = AccountAuthenticationService.NormalizeUsername(username);
        if (!string.Equals(normalized, AccountAuthenticationService.NormalizeUsername(confirmation), StringComparison.Ordinal)) throw new InvalidOperationException("Owner confirmation must exactly match the selected Admin username.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var owner = await db.Accounts.SingleOrDefaultAsync(x => x.NormalizedLoginName == normalized, ct) ?? throw new InvalidOperationException("The selected Admin does not exist.");
        if (owner.AccountType != AccountType.WebsiteAccount || !owner.Active || owner.GlobalRole != GlobalRole.Admin) throw new InvalidOperationException("The selected owner must be an active existing Admin.");
        if (await db.Accounts.AnyAsync(x => x.GlobalRole == GlobalRole.SuperAdmin, ct)) throw new InvalidOperationException("Retained owner promotion requires no existing Super Admin.");
        owner.SetGlobalRole(GlobalRole.SuperAdmin);
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), time.GetUtcNow(), null, "System", "account.retained_owner_promoted", "account", owner.Id.ToString(), "Preflight-selected retained Admin promoted to Super Admin."));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
