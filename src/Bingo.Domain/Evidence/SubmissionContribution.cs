namespace Bingo.Domain.Evidence;

public sealed class SubmissionContribution
{
    private SubmissionContribution() { }
    public SubmissionContribution(Guid id, Guid submissionId, Guid teamId, Guid requirementId, Guid? dropSnapshotId, Guid participantId, int amount, DateTimeOffset appliedAt)
    { ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1); Id = id; SubmissionId = submissionId; TeamId = teamId; RequirementId = requirementId; DropSnapshotId = dropSnapshotId; CreditedParticipantId = participantId; Amount = amount; AppliedAt = appliedAt.ToUniversalTime(); }
    public Guid Id { get; private set; }
    public Guid SubmissionId { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid RequirementId { get; private set; }
    public Guid? DropSnapshotId { get; private set; }
    public Guid CreditedParticipantId { get; private set; }
    public int Amount { get; private set; }
    public DateTimeOffset AppliedAt { get; private set; }
    public DateTimeOffset? ReversedAt { get; private set; }
    public void IncreaseAmount(int amount) { if (amount <= Amount) throw new InvalidOperationException("The adjusted contribution must be greater than its current amount."); Amount = amount; }
    public void Reverse(DateTimeOffset at) { if (ReversedAt is not null) throw new InvalidOperationException("Contribution is already reversed."); ReversedAt = at.ToUniversalTime(); }
}
