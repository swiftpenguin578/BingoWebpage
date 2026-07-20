namespace Bingo.Domain.Signups;

public sealed class EventParticipant
{
    private EventParticipant() { }

    public EventParticipant(
        Guid id, Guid eventId, string primaryAccountName, string normalizedName, decimal ehbSnapshot,
        SignupStatus status, long signupSequence, DateTimeOffset signedUpAt, SignupSource source, string? privateEditTokenHash)
    {
        Id = id;
        EventId = eventId;
        PrimaryAccountName = primaryAccountName;
        NormalizedPrimaryAccountName = normalizedName;
        EhbSnapshot = ehbSnapshot;
        SignupStatus = status;
        SignupSequence = signupSequence;
        SignedUpAt = signedUpAt.ToUniversalTime();
        Source = source;
        PrivateEditTokenHash = privateEditTokenHash;
        PaymentStatus = PaymentStatus.Unknown;
        if (status == SignupStatus.Confirmed) ConfirmedAt = signedUpAt.ToUniversalTime();
        if (status == SignupStatus.WaitingList) WaitingListedAt = signedUpAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public string PrimaryAccountName { get; private set; } = string.Empty;
    public string NormalizedPrimaryAccountName { get; private set; } = string.Empty;
    public string? SecondAccountName { get; private set; }
    public string? DiscordIdentity { get; private set; }
    public decimal EhbSnapshot { get; private set; }
    public string? Comments { get; private set; }
    public string? AdminNotes { get; private set; }
    public bool CaptainVolunteer { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }
    public SignupStatus SignupStatus { get; private set; }
    public long SignupSequence { get; private set; }
    public DateTimeOffset SignedUpAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? WaitingListedAt { get; private set; }
    public DateTimeOffset? WithdrawnAt { get; private set; }
    public DateTimeOffset? RemovedAt { get; private set; }
    public string? StatusReason { get; private set; }
    public string? PrivateEditTokenHash { get; private set; }
    public int FormVersion { get; private set; } = 1;
    public SignupSource Source { get; private set; }

    public void UpdatePublicDetails(string primaryName, string normalizedName, decimal ehb, string? secondName, string? discord, string? comments, bool captainVolunteer)
    {
        PrimaryAccountName = primaryName;
        NormalizedPrimaryAccountName = normalizedName;
        EhbSnapshot = ehb;
        SecondAccountName = secondName;
        DiscordIdentity = discord;
        Comments = comments;
        CaptainVolunteer = captainVolunteer;
    }

    public void Promote(DateTimeOffset now)
    {
        if (SignupStatus != SignupStatus.WaitingList) throw new InvalidOperationException("Only waiting participants can be promoted.");
        SignupStatus = SignupStatus.Confirmed;
        ConfirmedAt = now.ToUniversalTime();
        WaitingListedAt = null;
    }

    public void Withdraw(DateTimeOffset now, string reason)
    {
        SignupStatus = SignupStatus.Withdrawn;
        WithdrawnAt = now.ToUniversalTime();
        StatusReason = reason;
    }

    public void Remove(DateTimeOffset now, string reason)
    {
        SignupStatus = SignupStatus.Removed;
        RemovedAt = now.ToUniversalTime();
        StatusReason = reason;
    }

    public void SetPaymentStatus(PaymentStatus status) => PaymentStatus = status;

    public void SetAdminNotes(string? notes) => AdminNotes = notes;

    public void ReplacePrivateEditToken(string tokenHash)
    {
        if (string.IsNullOrWhiteSpace(tokenHash)) throw new ArgumentException("A private edit token hash is required.", nameof(tokenHash));
        PrivateEditTokenHash = tokenHash;
    }
}
