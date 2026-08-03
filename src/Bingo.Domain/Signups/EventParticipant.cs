using Bingo.Domain.Access;

namespace Bingo.Domain.Signups;

public sealed class EventParticipant
{
    private EventParticipant() { }

    public EventParticipant(
        Guid id, Guid eventId, SignupStatus status, long signupSequence, DateTimeOffset signedUpAt,
        SignupSource source)
    {
        Id = id;
        EventId = eventId;
        SignupStatus = status;
        SignupSequence = signupSequence;
        SignedUpAt = signedUpAt.ToUniversalTime();
        Source = source;
        PaymentReceived = false;
        if (status == SignupStatus.Confirmed) ConfirmedAt = signedUpAt.ToUniversalTime();
        if (status == SignupStatus.WaitingList) WaitingListedAt = signedUpAt.ToUniversalTime();
    }

    // Retained only so older callers can be upgraded independently; the retired value is never stored or used.
    public EventParticipant(
        Guid id, Guid eventId, SignupStatus status, long signupSequence, DateTimeOffset signedUpAt,
        SignupSource source, string? retiredValue)
        : this(id, eventId, status, signupSequence, signedUpAt, source)
    {
        _ = retiredValue;
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid? AccountId { get; private set; }
    public string? AdminNotes { get; private set; }
    public bool CaptainVolunteer { get; private set; }
    public bool PaymentReceived { get; private set; }
    public PaymentStatus PaymentStatus => PaymentReceived ? PaymentStatus.Paid : PaymentStatus.Unpaid;
    public SignupStatus SignupStatus { get; private set; }
    public long SignupSequence { get; private set; }
    public DateTimeOffset SignedUpAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; private set; }
    public DateTimeOffset? WaitingListedAt { get; private set; }
    public DateTimeOffset? WithdrawnAt { get; private set; }
    public Guid? WithdrawnByAccountId { get; private set; }
    public string? StatusReason { get; private set; }
    public int FormVersion { get; private set; } = 1;
    public int ResponseVersion { get; private set; } = 1;
    public SignupSource Source { get; private set; }

    public void AssignOwner(Account account)
    {
        if (account.AccountType != AccountType.WebsiteAccount)
            throw new InvalidOperationException("Only a website account can own an event participant.");
        if (AccountId is not null && AccountId != account.Id)
            throw new InvalidOperationException("Participant ownership is already assigned.");
        AccountId = account.Id;
    }

    public void TransferOwner(Account account)
    {
        if (account.AccountType != AccountType.WebsiteAccount)
            throw new InvalidOperationException("Only a website account can own an event participant.");
        AccountId = account.Id;
    }

    public void SetCaptainVolunteer(bool captainVolunteer) => CaptainVolunteer = captainVolunteer;
    public void AdvanceResponseVersion() => ResponseVersion++;

    public void Promote(DateTimeOffset now)
    {
        if (SignupStatus != SignupStatus.WaitingList) throw new InvalidOperationException("Only waiting participants can be promoted.");
        SignupStatus = SignupStatus.Confirmed;
        ConfirmedAt = now.ToUniversalTime();
        WaitingListedAt = null;
    }

    public void Withdraw(DateTimeOffset now, string reason, Guid? actorAccountId = null)
        => Withdraw(now, reason, actorAccountId, null);

    public void Withdraw(DateTimeOffset now, string reason, Guid? actorAccountId, DateTimeOffset? eligibilityEndsAt)
    {
        if (SignupStatus == SignupStatus.Withdrawn) return;
        SignupStatus = SignupStatus.Withdrawn;
        WithdrawnAt = (eligibilityEndsAt ?? now).ToUniversalTime();
        StatusReason = reason;
        WithdrawnByAccountId = actorAccountId;
    }

    public void Rejoin(SignupStatus status, long sequence, DateTimeOffset now)
    {
        if (SignupStatus != SignupStatus.Withdrawn) throw new InvalidOperationException("Only withdrawn participants can rejoin.");
        SignupStatus = status;
        SignupSequence = sequence;
        SignedUpAt = now.ToUniversalTime();
        ConfirmedAt = status == SignupStatus.Confirmed ? SignedUpAt : null;
        WaitingListedAt = status == SignupStatus.WaitingList ? SignedUpAt : null;
        WithdrawnAt = null;
        WithdrawnByAccountId = null;
        StatusReason = null;
    }

    public void SetPaymentReceived(bool received) => PaymentReceived = received;
    public void SetPaymentStatus(PaymentStatus status) => SetPaymentReceived(status == PaymentStatus.Paid);

    public void SetAdminNotes(string? notes) => AdminNotes = notes;

}
