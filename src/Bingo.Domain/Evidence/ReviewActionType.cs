namespace Bingo.Domain.Evidence;

public enum ReviewActionType
{
    Submitted = 1,
    RequestChanges = 2,
    EditMetadata = 3,
    Approve = 4,
    Reject = 5,
    MarkDuplicate = 6,
    HidePublicEvidence = 7,
    ShowPublicEvidence = 8,
    ReverseApproval = 9,
    Withdraw = 10,
    Resubmit = 11,
    ReplaceEvidence = 12,
    RebalanceContribution = 13
}
