namespace Bingo.Domain.Access;

/// <summary>Durable website identity or an explicitly scoped emergency credential.</summary>
public sealed class Account
{
    private Account() { }

    private Account(Guid id, AccountType accountType, string loginName, string normalizedLoginName, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(loginName) || string.IsNullOrWhiteSpace(normalizedLoginName)) throw new ArgumentException("A login name is required.");
        Id = id;
        AccountType = accountType;
        LoginName = loginName.Trim();
        NormalizedLoginName = normalizedLoginName;
        CreatedAt = createdAt.ToUniversalTime();
        AuthorizationVersion = 1;
        PasswordVersion = 1;
        Active = accountType == AccountType.WebsiteAccount;
    }

    public static Account CreateWebsite(Guid id, string publicUsername, string normalizedUsername, DateTimeOffset createdAt) =>
        new(id, AccountType.WebsiteAccount, publicUsername, normalizedUsername, createdAt) { PublicUsername = publicUsername.Trim(), NormalizedPublicUsername = normalizedUsername, GlobalRole = global::Bingo.Domain.Access.GlobalRole.User };

    public static Account CreateEmergency(Guid id, string loginUsername, string normalizedUsername, DateTimeOffset createdAt) =>
        new(id, AccountType.EmergencyCaptain, loginUsername, normalizedUsername, createdAt) { EmergencyLoginUsername = loginUsername.Trim(), Active = false };

    public Guid Id { get; private set; }
    public AccountType AccountType { get; private set; }
    public GlobalRole? GlobalRole { get; private set; }
    public string LoginName { get; private set; } = string.Empty;
    public string NormalizedLoginName { get; private set; } = string.Empty;
    public string? PublicUsername { get; private set; }
    public string? NormalizedPublicUsername { get; private set; }
    public string? EmergencyLoginUsername { get; private set; }
    public string? DiscordUserId { get; private set; }
    public string? DiscordDisplayName { get; private set; }
    public string? PasswordHash { get; private set; }
    public DateTimeOffset? PasswordChangedAt { get; private set; }
    public bool MustChangePassword { get; private set; }
    public long AuthorizationVersion { get; private set; }
    public long PasswordVersion { get; private set; }
    public bool Active { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }
    public Guid? DisabledByAccountId { get; private set; }
    public string? DisabledReason { get; private set; }
    public DateTimeOffset? OnboardingCompletedAt { get; private set; }
    public Guid? ProfileOsrsCharacterId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public uint Version { get; private set; }

    public void AdvanceVersion() => Version++;


    public void SetPasswordHash(string passwordHash, bool mustChangePassword) => SetPassword(passwordHash, mustChangePassword, DateTimeOffset.UtcNow, incrementVersion: false);
    public void SetPassword(string passwordHash, bool mustChangePassword, DateTimeOffset now, bool incrementVersion = true)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("A password hash is required.");
        PasswordHash = passwordHash;
        MustChangePassword = mustChangePassword;
        PasswordChangedAt = now.ToUniversalTime();
        if (incrementVersion) PasswordVersion++;
    }
    public void CompleteOnboarding(Guid profileCharacterId, DateTimeOffset now)
    {
        RequireWebsite();
        ProfileOsrsCharacterId = profileCharacterId;
        OnboardingCompletedAt = now.ToUniversalTime();
    }
    public void RenameWebsiteUsername(string publicUsername, string normalizedUsername)
    {
        RequireWebsite();
        if (string.IsNullOrWhiteSpace(publicUsername) || string.IsNullOrWhiteSpace(normalizedUsername))
            throw new InvalidOperationException("A public username is required.");

        var trimmed = publicUsername.Trim();
        LoginName = trimmed;
        NormalizedLoginName = normalizedUsername;
        PublicUsername = trimmed;
        NormalizedPublicUsername = normalizedUsername;
    }
    public void SetDiscordIdentity(string discordUserId, string? displayName)
    {
        RequireWebsite();
        if (string.IsNullOrWhiteSpace(discordUserId)) throw new ArgumentException("A Discord identity is required.");
        DiscordUserId = discordUserId;
        DiscordDisplayName = displayName?.Trim();
        AuthorizationVersion++;
    }
    public void RemoveDiscordIdentity() { RequireWebsite(); DiscordUserId = null; DiscordDisplayName = null; AuthorizationVersion++; }
    public void SetGlobalRole(GlobalRole role) { RequireWebsite(); GlobalRole = role; AuthorizationVersion++; }
    public void Disable(DateTimeOffset now, Guid? actorId = null, string? reason = null) { Active = false; DisabledAt = now.ToUniversalTime(); DisabledByAccountId = actorId; DisabledReason = reason?.Trim(); AuthorizationVersion++; }
    public void Enable() { Active = true; DisabledAt = null; DisabledByAccountId = null; DisabledReason = null; AuthorizationVersion++; }
    public void RecordLogin(DateTimeOffset now) => LastLoginAt = now.ToUniversalTime();
    private void RequireWebsite() { if (AccountType != AccountType.WebsiteAccount) throw new InvalidOperationException("This operation requires a website account."); }
}
