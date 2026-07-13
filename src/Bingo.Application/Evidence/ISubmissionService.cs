using Bingo.Domain.Evidence;

namespace Bingo.Application.Evidence;

public interface ISubmissionService
{
    Task<SubmissionResult> CreateAsync(CreateSubmissionCommand command, CancellationToken cancellationToken = default);
    Task<SubmissionResult> CorrectAsync(CorrectSubmissionCommand command, CancellationToken cancellationToken = default);
    Task WithdrawAsync(Guid submissionId, Guid actorAccountId, CancellationToken cancellationToken = default);
    Task RequestChangesAsync(Guid submissionId, Guid adminAccountId, string note, CancellationToken cancellationToken = default);
    Task RejectAsync(Guid submissionId, Guid adminAccountId, string note, Guid? duplicateOfSubmissionId = null, CancellationToken cancellationToken = default);
    Task<int> ApproveAsync(Guid submissionId, Guid adminAccountId, CancellationToken cancellationToken = default);
    Task ReverseAsync(Guid submissionId, Guid adminAccountId, string reason, CancellationToken cancellationToken = default);
    Task SetVisibilityAsync(Guid submissionId, Guid adminAccountId, bool hidden, CancellationToken cancellationToken = default);
    Task EditMetadataAsync(EditSubmissionMetadataCommand command, CancellationToken cancellationToken = default);
}

public sealed record CreateSubmissionCommand(Guid ActorAccountId, Guid EventId, Guid TeamId, Guid BoardTileId,
    Guid RequirementId, Guid? DropSnapshotId, Guid CreditedParticipantId, int ClaimedWeight, string? CaptainNote,
    string OriginalFilename, Stream Evidence, bool RequestPublicPrivacy = false);

public sealed record CorrectSubmissionCommand(Guid SubmissionId, Guid ActorAccountId, Guid BoardTileId,
    Guid RequirementId, Guid? DropSnapshotId, Guid CreditedParticipantId, int ClaimedWeight, string? CaptainNote,
    string? ReplacementFilename, Stream? ReplacementEvidence, bool RequestPublicPrivacy = false);

public sealed record EditSubmissionMetadataCommand(Guid SubmissionId, Guid AdminAccountId, Guid BoardTileId,
    Guid RequirementId, Guid? DropSnapshotId, Guid CreditedParticipantId, int ClaimedWeight, string? Note);

public sealed record SubmissionResult(Guid SubmissionId, SubmissionStatus Status);
