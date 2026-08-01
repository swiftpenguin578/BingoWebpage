namespace Bingo.Domain.Evidence;

public sealed class Submission
{
    private Submission() { }

    public Submission(Guid id, Guid eventId, Guid teamId, Guid boardTileId, Guid requirementId, Guid? dropSnapshotId,
        Guid creditedParticipantId, Guid creditedOsrsCharacterId, string creditedCharacterName, Guid submittedByAccountId,
        int claimedWeight, DateTimeOffset submittedAt, string? captainNote, string? expectedEvidenceCode,
        Guid? resubmissionOfSubmissionId = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(claimedWeight, 1);
        Id = id; EventId = eventId; TeamId = teamId; BoardTileId = boardTileId; RequirementId = requirementId;
        if (creditedOsrsCharacterId == Guid.Empty) throw new ArgumentException("A credited OSRS character is required.", nameof(creditedOsrsCharacterId));
        if (string.IsNullOrWhiteSpace(creditedCharacterName)) throw new ArgumentException("A credited character name is required.", nameof(creditedCharacterName));
        DropSnapshotId = dropSnapshotId; CreditedParticipantId = creditedParticipantId; CreditedOsrsCharacterId = creditedOsrsCharacterId;
        CreditedCharacterName = creditedCharacterName.Trim(); SubmittedByAccountId = submittedByAccountId;
        ClaimedWeight = claimedWeight; SubmittedAt = submittedAt.ToUniversalTime(); CaptainNote = Clean(captainNote);
        ExpectedEvidenceCode = Clean(expectedEvidenceCode);
        ResubmissionOfSubmissionId = resubmissionOfSubmissionId; Status = SubmissionStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid BoardTileId { get; private set; }
    public Guid RequirementId { get; private set; }
    public Guid? DropSnapshotId { get; private set; }
    public Guid CreditedParticipantId { get; private set; }
    public Guid CreditedOsrsCharacterId { get; private set; }
    public string CreditedCharacterName { get; private set; } = string.Empty;
    public Guid SubmittedByAccountId { get; private set; }
    public int Version { get; private set; } = 1;
    public int ClaimedWeight { get; private set; }
    public int ApprovedContribution { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public string? CaptainNote { get; private set; }
    public SubmissionStatus Status { get; private set; }
    public string? ExpectedEvidenceCode { get; private set; }
    public string? CurrentReviewerNote { get; private set; }
    public Guid? ResubmissionOfSubmissionId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    public void EditPending(Guid tileId, Guid requirementId, Guid? dropId, Guid participantId, int weight, string? note)
    {
        if (Status != SubmissionStatus.Pending) throw new InvalidOperationException("Only pending submissions can be edited.");
        ArgumentOutOfRangeException.ThrowIfLessThan(weight, 1);
        if (participantId != CreditedParticipantId) throw new InvalidOperationException("The credited participant is immutable for an existing submission.");
        BoardTileId = tileId; RequirementId = requirementId; DropSnapshotId = dropId;
        ClaimedWeight = weight; CaptainNote = Clean(note);
    }

    public void CorrectCreditedAttribution(Guid participantId, Guid osrsCharacterId, string characterName)
    {
        if (Status != SubmissionStatus.Pending) throw new InvalidOperationException("Only pending submissions can be corrected.");
        if (osrsCharacterId == Guid.Empty || string.IsNullOrWhiteSpace(characterName)) throw new ArgumentException("A credited character is required.");
        CreditedParticipantId = participantId; CreditedOsrsCharacterId = osrsCharacterId; CreditedCharacterName = characterName.Trim();
    }

    public void AdvanceVersion() => Version++;

    public void EnsureRejectedResubmissionSource()
    {
        if (Status != SubmissionStatus.Rejected) throw new InvalidOperationException("Only rejected submissions can create a linked resubmission.");
    }
    public void Withdraw(DateTimeOffset at) { if (Status != SubmissionStatus.Pending) throw new InvalidOperationException("Only pending submissions can be withdrawn."); Status = SubmissionStatus.Withdrawn; ReviewedAt = at.ToUniversalTime(); }
    public void Reject(string note, DateTimeOffset at) { if (Status != SubmissionStatus.Pending) throw new InvalidOperationException("Only pending submissions can be rejected."); CurrentReviewerNote = RequireNote(note); Status = SubmissionStatus.Rejected; ReviewedAt = at.ToUniversalTime(); }
    public void Approve(int contribution, DateTimeOffset at) { if (Status != SubmissionStatus.Pending) throw new InvalidOperationException("Only pending submissions can be approved."); if (contribution < 1) throw new InvalidOperationException("Approved contribution must be positive."); ApprovedContribution = contribution; Status = SubmissionStatus.Approved; ReviewedAt = at.ToUniversalTime(); CurrentReviewerNote = null; }
    public void IncreaseApprovedContribution(int contribution) { if (Status != SubmissionStatus.Approved || contribution <= ApprovedContribution) throw new InvalidOperationException("Only an approved contribution can be increased."); ApprovedContribution = contribution; }
    public void Reverse(string reason, DateTimeOffset at) { if (Status != SubmissionStatus.Approved) throw new InvalidOperationException("Only approved submissions can be reversed."); CurrentReviewerNote = RequireNote(reason); Status = SubmissionStatus.Reversed; ReviewedAt = at.ToUniversalTime(); }

    private static string RequireNote(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A note is required.", nameof(value)) : value.Trim();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
