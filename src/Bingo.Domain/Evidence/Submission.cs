namespace Bingo.Domain.Evidence;

public sealed class Submission
{
    private Submission() { }

    public Submission(Guid id, Guid eventId, Guid teamId, Guid boardTileId, Guid requirementId, Guid? dropSnapshotId,
        Guid creditedParticipantId, Guid submittedByAccountId, int claimedWeight, DateTimeOffset submittedAt,
        string? captainNote, string? expectedEvidenceCode, bool publicPrivacyRequested = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(claimedWeight, 1);
        Id = id; EventId = eventId; TeamId = teamId; BoardTileId = boardTileId; RequirementId = requirementId;
        DropSnapshotId = dropSnapshotId; CreditedParticipantId = creditedParticipantId; SubmittedByAccountId = submittedByAccountId;
        ClaimedWeight = claimedWeight; SubmittedAt = submittedAt.ToUniversalTime(); CaptainNote = Clean(captainNote);
        ExpectedEvidenceCode = Clean(expectedEvidenceCode); PublicPrivacyRequested = publicPrivacyRequested; Status = SubmissionStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid BoardTileId { get; private set; }
    public Guid RequirementId { get; private set; }
    public Guid? DropSnapshotId { get; private set; }
    public Guid CreditedParticipantId { get; private set; }
    public Guid SubmittedByAccountId { get; private set; }
    public int ClaimedWeight { get; private set; }
    public int ApprovedContribution { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public string? CaptainNote { get; private set; }
    public SubmissionStatus Status { get; private set; }
    public bool PublicEvidenceHidden { get; private set; }
    public bool PublicPlayerHidden { get; private set; }
    public bool PublicPrivacyRequested { get; private set; }
    public string? ExpectedEvidenceCode { get; private set; }
    public string? CurrentReviewerNote { get; private set; }
    public Guid? DuplicateOfSubmissionId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    public void EditPending(Guid tileId, Guid requirementId, Guid? dropId, Guid participantId, int weight, string? note, bool? publicPrivacyRequested = null)
    {
        if (Status is not (SubmissionStatus.Pending or SubmissionStatus.ChangesRequested)) throw new InvalidOperationException("Only pending submissions can be edited.");
        ArgumentOutOfRangeException.ThrowIfLessThan(weight, 1);
        BoardTileId = tileId; RequirementId = requirementId; DropSnapshotId = dropId; CreditedParticipantId = participantId;
        ClaimedWeight = weight; CaptainNote = Clean(note);
        if (publicPrivacyRequested is not null) PublicPrivacyRequested = publicPrivacyRequested.Value;
    }

    public void RequestChanges(string note, DateTimeOffset at) { RequireReviewable(); CurrentReviewerNote = RequireNote(note); Status = SubmissionStatus.ChangesRequested; ReviewedAt = at.ToUniversalTime(); }
    public void Resubmit() { if (Status != SubmissionStatus.ChangesRequested) throw new InvalidOperationException("Only changes-requested submissions can be resubmitted."); Status = SubmissionStatus.Pending; CurrentReviewerNote = null; ReviewedAt = null; }
    public void Withdraw(DateTimeOffset at) { if (Status is not (SubmissionStatus.Pending or SubmissionStatus.ChangesRequested)) throw new InvalidOperationException("Only unapproved submissions can be withdrawn."); Status = SubmissionStatus.Withdrawn; ReviewedAt = at.ToUniversalTime(); }
    public void Reject(string note, DateTimeOffset at, Guid? duplicateOf = null) { RequireReviewable(); CurrentReviewerNote = RequireNote(note); DuplicateOfSubmissionId = duplicateOf; Status = SubmissionStatus.Rejected; ReviewedAt = at.ToUniversalTime(); }
    public void Approve(int contribution, DateTimeOffset at) { if (Status != SubmissionStatus.Pending) throw new InvalidOperationException("Only pending submissions can be approved."); if (contribution < 1) throw new InvalidOperationException("Approved contribution must be positive."); ApprovedContribution = contribution; Status = SubmissionStatus.Approved; ReviewedAt = at.ToUniversalTime(); CurrentReviewerNote = null; }
    public void IncreaseApprovedContribution(int contribution) { if (Status != SubmissionStatus.Approved || contribution <= ApprovedContribution) throw new InvalidOperationException("Only an approved contribution can be increased."); ApprovedContribution = contribution; }
    public void Reverse(string reason, DateTimeOffset at) { if (Status != SubmissionStatus.Approved) throw new InvalidOperationException("Only approved submissions can be reversed."); CurrentReviewerNote = RequireNote(reason); Status = SubmissionStatus.Reversed; ReviewedAt = at.ToUniversalTime(); }
    public void SetPublicEvidenceHidden(bool hidden) { if (Status != SubmissionStatus.Approved) throw new InvalidOperationException("Only approved evidence has public visibility."); PublicEvidenceHidden = hidden; PublicPlayerHidden = hidden; }

    private void RequireReviewable() { if (Status is not (SubmissionStatus.Pending or SubmissionStatus.ChangesRequested)) throw new InvalidOperationException("This submission is no longer reviewable."); }
    private static string RequireNote(string value) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A note is required.", nameof(value)) : value.Trim();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
