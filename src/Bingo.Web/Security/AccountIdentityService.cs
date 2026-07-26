using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Security;

/// <summary>Transactional Slice 1 identity mutations. Raw credential links never persist.</summary>
public sealed class AccountIdentityService(ApplicationDbContext db, IPasswordHasher<Account> passwords, TimeProvider time)
{
    public async Task<UsernameRenameResult> RenameUsernameAsync(Guid accountId, string proposedUsername, string currentPassword, CancellationToken ct)
    {
        var username = proposedUsername.Trim();
        if (string.IsNullOrWhiteSpace(username)) return UsernameRenameResult.InvalidUsername;

        var normalized = AccountAuthenticationService.NormalizeUsername(username);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var account = await db.Accounts.SingleOrDefaultAsync(candidate => candidate.Id == accountId, ct);
        if (account is null || !account.Active || account.AccountType != AccountType.WebsiteAccount) return UsernameRenameResult.NotAvailable;
        if (account.PasswordHash is null || passwords.VerifyHashedPassword(account, account.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            return UsernameRenameResult.WrongPassword;

        var beforeUsername = account.PublicUsername;
        var beforeNormalizedUsername = account.NormalizedPublicUsername;
        account.RenameWebsiteUsername(username, normalized);
        var now = time.GetUtcNow();
        db.AuditEntries.Add(new AuditEntry(
            Guid.NewGuid(), now, account.Id, account.LoginName, "account.username_changed", "account", account.Id.ToString(),
            "Website username changed.",
            beforeState: JsonSerializer.Serialize(new { username = beforeUsername, normalizedUsername = beforeNormalizedUsername }),
            afterState: JsonSerializer.Serialize(new { username = account.PublicUsername, normalizedUsername = account.NormalizedPublicUsername })));
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return UsernameRenameResult.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            return UsernameRenameResult.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (IsExpectedUsernameCollision(exception))
        {
            await transaction.RollbackAsync(ct);
            return UsernameRenameResult.UsernameTaken;
        }
    }
    public async Task<Account> CompleteOnboardingAsync(string discordUserId, string? displayName, string username, string firstOsrsCharacter, string password, CancellationToken ct)
    {
        ValidatePassword(password);
        var name = username.Trim(); var normalized = AccountAuthenticationService.NormalizeUsername(name);
        var characterName = firstOsrsCharacter.Trim(); var normalizedCharacter = NormalizeOsrsCharacterName(characterName);
        if (string.IsNullOrWhiteSpace(discordUserId)) throw new InvalidOperationException("Discord authentication is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("A public username is required.");
        if (string.IsNullOrWhiteSpace(characterName)) throw new InvalidOperationException("An OSRS character name is required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.Accounts.AnyAsync(x => x.NormalizedLoginName == normalized || x.DiscordUserId == discordUserId, ct)) throw new InvalidOperationException("That username or Discord account is already in use.");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({normalizedCharacter}, 0))", ct);
        var now = time.GetUtcNow();
        var character = await db.OsrsCharacters.SingleOrDefaultAsync(x => x.NormalizedName == normalizedCharacter, ct) ?? new OsrsCharacter(Guid.NewGuid(), characterName, normalizedCharacter, now);
        if (db.Entry(character).State == EntityState.Detached) db.OsrsCharacters.Add(character);
        var account = Account.CreateWebsite(Guid.NewGuid(), name, normalized, now);
        account.SetDiscordIdentity(discordUserId, displayName);
        account.SetPassword(passwords.HashPassword(account, password), false, now, incrementVersion: false);
        account.CompleteOnboarding(character.Id, now);
        db.Accounts.Add(account);
        db.AccountOsrsCharacters.Add(new AccountOsrsCharacter(Guid.NewGuid(), account.Id, character.Id, true, 0, now));
        db.AccountDiscordIdentityTransitions.Add(new AccountDiscordIdentityTransition(Guid.NewGuid(), account.Id, "linked", null, discordUserId, now));
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, account.Id, account.LoginName, "account.onboarded", "account", account.Id.ToString(), "Website account onboarding completed."));
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return account;
        }
        catch (DbUpdateException exception) when (IsExpectedOnboardingCollision(exception))
        {
            throw new InvalidOperationException("That username or Discord account is already in use.");
        }
    }

    public async Task<string> GenerateResetLinkAsync(Guid actorId, Guid targetId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({targetId.ToString()}, 0))", ct);
        var actor = await db.Accounts.SingleAsync(x => x.Id == actorId, ct); var target = await db.Accounts.SingleAsync(x => x.Id == targetId, ct);
        if (actor.GlobalRole != GlobalRole.SuperAdmin && (actor.GlobalRole != GlobalRole.Admin || target.GlobalRole != GlobalRole.User)) throw new InvalidOperationException("You cannot reset this account.");
        if (target.GlobalRole == GlobalRole.SuperAdmin) throw new InvalidOperationException("Super Admin reset requires operator recovery.");
        var now = time.GetUtcNow(); var purpose = PasswordCredentialTokenPurpose.Reset;
        foreach (var token in await db.PasswordCredentialTokens.Where(x => x.AccountId == targetId && x.Purpose == purpose && x.UsedAt == null && x.SupersededAt == null).ToListAsync(ct)) token.Supersede(now);
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.PasswordCredentialTokens.Add(new PasswordCredentialToken(Guid.NewGuid(), targetId, purpose, Hash(raw), now.AddMinutes(60), now, actorId));
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actor.Id, actor.LoginName, "account.reset_link_created", "account", target.Id.ToString(), "One-time password-reset link created."));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return raw;
    }
    public async Task<string> GenerateEmergencyCredentialLinkAsync(Guid actorId, Guid targetId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({targetId.ToString()}, 0))", ct);
        var actor = await db.Accounts.SingleAsync(x => x.Id == actorId, ct);
        var target = await db.Accounts.SingleAsync(x => x.Id == targetId, ct);
        if (actor.GlobalRole is not (GlobalRole.Admin or GlobalRole.SuperAdmin) || target.AccountType != AccountType.EmergencyCaptain)
            throw new InvalidOperationException("Only an administrator can create an emergency credential link.");

        var now = time.GetUtcNow();
        var purpose = target.PasswordHash is null ? PasswordCredentialTokenPurpose.EmergencySetup : PasswordCredentialTokenPurpose.EmergencyReset;
        foreach (var token in await db.PasswordCredentialTokens.Where(x => x.AccountId == targetId && x.UsedAt == null && x.SupersededAt == null).ToListAsync(ct)) token.Supersede(now);
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.PasswordCredentialTokens.Add(new PasswordCredentialToken(Guid.NewGuid(), targetId, purpose, Hash(raw), now.AddMinutes(60), now, actorId));
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actor.Id, actor.LoginName, "account.emergency_credential_link_created", "account", target.Id.ToString(), "One-time emergency credential link created."));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return raw;
    }
    public async Task<PasswordCredentialTokenPurpose> ConsumeResetAsync(string rawToken, string password, CancellationToken ct)
    {
        ValidatePassword(password); var now = time.GetUtcNow(); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var token = await db.PasswordCredentialTokens.FromSqlInterpolated($"SELECT * FROM password_credential_tokens WHERE \"TokenHash\" = {Hash(rawToken)} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new InvalidOperationException("This link is no longer valid.");
        if (!token.IsUsable(now)) throw new InvalidOperationException("This link is no longer valid.");
        var account = await db.Accounts.SingleAsync(x => x.Id == token.AccountId, ct);
        if (token.Purpose == PasswordCredentialTokenPurpose.OwnerRecovery && (!account.Active || account.GlobalRole != GlobalRole.SuperAdmin))
            throw new InvalidOperationException("This link is no longer valid.");
        account.SetPassword(passwords.HashPassword(account, password), false, now); token.Use(now);
        var isOwnerRecovery = token.Purpose == PasswordCredentialTokenPurpose.OwnerRecovery;
        db.AuditEntries.Add(new AuditEntry(
            Guid.NewGuid(),
            now,
            isOwnerRecovery ? null : account.Id,
            isOwnerRecovery ? "System" : account.LoginName,
            isOwnerRecovery ? "account.owner_recovery_password_reset" : "account.password_reset",
            "account",
            account.Id.ToString(),
            isOwnerRecovery
                ? "Operator-only owner password reset completed through a single-use credential link."
                : "Password reset completed through a single-use credential link."));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return token.Purpose;
    }
    public async Task ChangePasswordAsync(Account account, string password, CancellationToken ct) { ValidatePassword(password); var now = time.GetUtcNow(); account.SetPassword(passwords.HashPassword(account, password), false, now); db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, account.Id, account.LoginName, "account.password_changed", "account", account.Id.ToString(), "Password changed.")); await db.SaveChangesAsync(ct); }
    public async Task SetDiscordAsync(Account account, string discordUserId, string? displayName, string action, CancellationToken ct) { if (await db.Accounts.AnyAsync(x => x.Id != account.Id && x.DiscordUserId == discordUserId, ct)) throw new InvalidOperationException("That Discord account is already linked."); var before = account.DiscordUserId; var now = time.GetUtcNow(); account.SetDiscordIdentity(discordUserId, displayName); db.AccountDiscordIdentityTransitions.Add(new AccountDiscordIdentityTransition(Guid.NewGuid(), account.Id, action, before, discordUserId, now)); db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, account.Id, account.LoginName, $"account.discord_{action}", "account", account.Id.ToString(), $"Discord identity {action}.", beforeState: before is null ? "null" : "{\"discordLinked\":true}", afterState: "{\"discordLinked\":true}")); try { await db.SaveChangesAsync(ct); } catch (DbUpdateException exception) when (IsExpectedIdentityCollision(exception)) { throw new InvalidOperationException("That Discord account is already linked."); } }
    public async Task RemoveDiscordAsync(Account account, CancellationToken ct) { var before = account.DiscordUserId; if (before is null) return; var now = time.GetUtcNow(); account.RemoveDiscordIdentity(); db.AccountDiscordIdentityTransitions.Add(new AccountDiscordIdentityTransition(Guid.NewGuid(), account.Id, "unlinked", before, null, now)); db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, account.Id, account.LoginName, "account.discord_unlinked", "account", account.Id.ToString(), "Discord identity unlinked.", beforeState: "{\"discordLinked\":true}", afterState: "{\"discordLinked\":false}")); await db.SaveChangesAsync(ct); }
    public static void ValidatePassword(string password) { if (password.Length is < 10 or > 200) throw new InvalidOperationException("Passwords must be between 10 and 200 characters."); }
    public static string NormalizeOsrsCharacterName(string name) => name.Trim().ToUpperInvariant();
    public static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    private static bool IsExpectedOnboardingCollision(DbUpdateException exception) => exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_accounts_normalized_login_name" or "IX_accounts_discord_user_id" };
    private static bool IsExpectedUsernameCollision(DbUpdateException exception) => exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_accounts_normalized_login_name" };
    private static bool IsExpectedIdentityCollision(DbUpdateException exception) => exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_accounts_discord_user_id" };
}

public enum UsernameRenameResult
{
    Success,
    InvalidUsername,
    WrongPassword,
    UsernameTaken,
    ConcurrencyConflict,
    NotAvailable
}
