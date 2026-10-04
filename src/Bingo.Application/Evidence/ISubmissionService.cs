using Bingo.Domain.Evidence;

namespace Bingo.Application.Evidence;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Expected optimistic-concurrency versions follow the established optional cancellation-token compatibility position.")]
public interface ISubmissionService
{
    Task<SubmissionResult> CreateAsync(CreateSubmissionCommand command, CancellationToken cancellationToken = default);
    Task<SubmissionResult> CorrectAsync(CorrectSubmissionCommand command, CancellationToken cancellationToken = default);
    Task WithdrawAsync(Guid submissionId, Guid actorAccountId, CancellationToken cancellationToken = default, int? expectedVersion = null, bool ownerOnly = false);
    Task RejectAsync(Guid submissionId, Guid adminAccountId, string reason, CancellationToken cancellationToken = default, int? expectedVersion = null);
    Task<SubmissionApprovalResult> ApproveAsync(Guid submissionId, Guid adminAccountId, CancellationToken cancellationToken = default, int? expectedVersion = null);
    Task ReverseAsync(Guid submissionId, Guid adminAccountId, string reason, CancellationToken cancellationToken = default, int? expectedVersion = null);
    Task<SubmissionReviewReadback> GetReviewReadbackAsync(Guid submissionId, Guid adminAccountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubmissionCorrectionCharacter>> GetCorrectionCharactersAsync(Guid submissionId, Guid adminAccountId, CancellationToken cancellationToken = default);
    Task EditMetadataAsync(EditSubmissionMetadataCommand command, CancellationToken cancellationToken = default);
}

public sealed record CreateSubmissionCommand(Guid ActorAccountId, Guid EventId, Guid TeamId, Guid BoardTileId,
    Guid RequirementId, Guid? DropSnapshotId, Guid CreditedParticipantId, int ClaimedWeight, string? CaptainNote,
    string OriginalFilename, Stream Evidence);

public sealed record CorrectSubmissionCommand(Guid SubmissionId, Guid ActorAccountId, Guid BoardTileId,
    Guid RequirementId, Guid? DropSnapshotId, Guid CreditedParticipantId, int ClaimedWeight, string? CaptainNote,
    int? ExpectedVersion = null, string? OriginalFilename = null, Stream? Evidence = null, bool OwnerOnly = false);

public sealed record EditSubmissionMetadataCommand(Guid SubmissionId, Guid AdminAccountId, Guid BoardTileId,
    Guid RequirementId, Guid? DropSnapshotId, Guid CreditedOsrsCharacterId, string Reason, int? ExpectedVersion = null);

public sealed record SubmissionResult(Guid SubmissionId, SubmissionStatus Status);

public sealed record SubmissionApprovalResult(int ApprovedContribution, SubmissionApprovalBlock? BlockingSubmission = null);

public sealed record SubmissionApprovalBlock(Guid SubmissionId, DateTimeOffset SubmittedAt);

public sealed record SubmissionCorrectionCharacter(Guid CharacterId, Guid ParticipantId, string CharacterName, bool Released, bool LeftTeam)
{
    public bool Current => !Released && !LeftTeam;
}

// Current authoritative state only. A match never identifies a request or permits replay.
// Unknown has no state and must never be rendered as "not saved".
public sealed record SubmissionReviewReadback(SubmissionReviewState? State)
{
    public bool Known => State is not null;
}
public sealed record SubmissionReviewState(Guid SubmissionId, Guid EventId, Guid TeamId, int Version,
    SubmissionStatus Status, Guid BoardTileId, Guid RequirementId, Guid? DropSnapshotId,
    Guid CreditedParticipantId, Guid CreditedCharacterId, string CreditedCharacterName, int Weight,
    int ApprovedContribution, SubmissionLatestReviewAction? LatestAction, SubmissionContributionRead Contribution);
public sealed record SubmissionLatestReviewAction(Guid Id, ReviewActionType Type, Guid ActorId,
    string? ActorName, DateTimeOffset At, bool ReasonPresent);
public sealed record SubmissionContributionRead(SubmissionContributionNumbers? Values, SubmissionApprovalBlock? BlockingSubmission = null);
public sealed record SubmissionContributionNumbers(int Add, int Weight, int Remaining, int Used, int Target, bool Completes);
