namespace Bingo.Domain.Evidence;

public enum ReviewActionType
{
    Submitted = 1,
    RequestChanges = 2, // Historical audit value; no active command.
    EditMetadata = 3,
    Approve = 4,
    Reject = 5,
    MarkDuplicate = 6, // Historical audit value; no active command.
    HidePublicEvidence = 7, // Historical audit value; no active command.
    ShowPublicEvidence = 8, // Historical audit value; no active command.
    ReverseApproval = 9,
    Withdraw = 10,
    Resubmit = 11,
    ReplaceEvidence = 12,
    RebalanceContribution = 13
}
