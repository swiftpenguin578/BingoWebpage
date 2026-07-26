namespace Bingo.Domain.Access;

public enum PasswordCredentialTokenPurpose { Reset = 1, EmergencySetup = 2, EmergencyReset = 3, OwnerRecovery = 4 }
public sealed class PasswordCredentialToken
{
    private PasswordCredentialToken() { }
    public PasswordCredentialToken(Guid id, Guid accountId, PasswordCredentialTokenPurpose purpose, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset createdAt, Guid? createdByAccountId = null) { Id = id; AccountId = accountId; Purpose = purpose; TokenHash = tokenHash; ExpiresAt = expiresAt.ToUniversalTime(); CreatedAt = createdAt.ToUniversalTime(); CreatedByAccountId = createdByAccountId; }
    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public PasswordCredentialTokenPurpose Purpose { get; private set; }
    public string TokenHash { get; private set; } = string.Empty; public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedByAccountId { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }
    public DateTimeOffset? SupersededAt { get; private set; }
    public bool IsUsable(DateTimeOffset now) => UsedAt is null && SupersededAt is null && ExpiresAt > now;
    public void Use(DateTimeOffset now) { if (!IsUsable(now)) throw new InvalidOperationException("The credential link is no longer valid."); UsedAt = now.ToUniversalTime(); }
    public void Supersede(DateTimeOffset now) { if (UsedAt is null) SupersededAt = now.ToUniversalTime(); }
}
