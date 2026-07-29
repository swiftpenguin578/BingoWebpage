namespace Bingo.Domain.Access;

public sealed class AccountEventAccess
{
    private AccountEventAccess() { }
    public AccountEventAccess(Guid id, Guid accountId, Guid eventId, Guid teamId, Guid? participantId, DateTimeOffset? activeFrom, DateTimeOffset? correctionOnlyFrom, DateTimeOffset? expiresAt) { Id = id; AccountId = accountId; EventId = eventId; TeamId = teamId; ParticipantId = participantId; ActiveFrom = activeFrom?.ToUniversalTime(); CorrectionOnlyFrom = correctionOnlyFrom?.ToUniversalTime(); ExpiresAt = expiresAt?.ToUniversalTime(); }
    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid? ParticipantId { get; private set; }
    public DateTimeOffset? ActiveFrom { get; private set; }
    public DateTimeOffset? CorrectionOnlyFrom { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public bool Enabled { get; private set; }
    /// <summary>Records that the lifecycle worker has applied the event's original submission cutoff.</summary>
    public bool CutoffDisabled { get; private set; }
    public void Enable() => Enabled = true;
    public void Disable() => Enabled = false;
    public void DisableAtCutoff() { Enabled = false; CutoffDisabled = true; }
    /// <summary>Lifecycle mutation windows are authoritative on the event, not on this credential projection.</summary>
    public AccountAccessMode GetAccessMode(DateTimeOffset now) => !Enabled || (ActiveFrom is not null && now < ActiveFrom) || (ExpiresAt is not null && now >= ExpiresAt) ? AccountAccessMode.Disabled : AccountAccessMode.Full;
}
