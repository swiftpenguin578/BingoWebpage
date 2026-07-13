namespace Bingo.Domain.Access;

public sealed class Account
{
    private Account()
    {
    }

    public Account(Guid id, string username, string normalizedUsername, AccountRole role, DateTimeOffset createdAt)
    {
        Id = id;
        Username = username;
        NormalizedUsername = normalizedUsername;
        Role = role;
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public string Username { get; private set; } = string.Empty;

    public string NormalizedUsername { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public AccountRole Role { get; private set; }

    public Guid? EventId { get; private set; }

    public Guid? TeamId { get; private set; }

    public Guid? CaptainParticipantId { get; private set; }

    public DateTimeOffset? ActiveFrom { get; private set; }

    public DateTimeOffset? CorrectionOnlyFrom { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? DisabledAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public bool MustChangePassword { get; private set; }

    public void SetPasswordHash(string passwordHash, bool mustChangePassword)
    {
        PasswordHash = passwordHash;
        MustChangePassword = mustChangePassword;
    }

    public void ScopeCaptain(
        Guid eventId,
        Guid teamId,
        DateTimeOffset? activeFrom,
        DateTimeOffset? correctionOnlyFrom,
        DateTimeOffset? expiresAt,
        Guid? captainParticipantId = null)
    {
        if (Role != AccountRole.Captain)
        {
            throw new InvalidOperationException("Only captain accounts can be event and team scoped.");
        }

        EventId = eventId;
        TeamId = teamId;
        CaptainParticipantId = captainParticipantId;
        ActiveFrom = activeFrom?.ToUniversalTime();
        CorrectionOnlyFrom = correctionOnlyFrom?.ToUniversalTime();
        ExpiresAt = expiresAt?.ToUniversalTime();
    }

    public void Disable(DateTimeOffset now) => DisabledAt = now.ToUniversalTime();

    public void Enable(DateTimeOffset? expiresAt)
    {
        DisabledAt = null;
        ExpiresAt = expiresAt?.ToUniversalTime();
    }

    public void ScheduleExpiry(DateTimeOffset? expiresAt) => ExpiresAt = expiresAt?.ToUniversalTime();

    public void RecordLogin(DateTimeOffset now) => LastLoginAt = now.ToUniversalTime();

    public AccountAccessMode GetAccessMode(DateTimeOffset now)
    {
        if (DisabledAt is not null || (ActiveFrom is not null && now < ActiveFrom) ||
            (ExpiresAt is not null && now >= ExpiresAt))
        {
            return AccountAccessMode.Disabled;
        }

        if (Role == AccountRole.Captain && CorrectionOnlyFrom is not null && now >= CorrectionOnlyFrom)
        {
            return AccountAccessMode.CorrectionOnly;
        }

        return AccountAccessMode.Full;
    }
}
