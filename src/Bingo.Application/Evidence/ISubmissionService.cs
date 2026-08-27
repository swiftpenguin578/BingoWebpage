using Bingo.Domain.Evidence;

namespace Bingo.Application.Evidence;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068", Justification = "Expected optimistic-concurrency versions follow the established optional cancellation-token compatibility position.")]
public interface ISubmissionService
{
    Task<SubmissionResult> CreateAsync(CreateSubmissionCommand command, CancellationToken cancellationToken = default);
    Task<SubmissionResult> CorrectAsync(CorrectSubmissionCommand command, CancellationToken cancellationToken = default);
    Task<SubmissionResult> ResubmitAsync(ResubmitSubmissionCommand command, CancellationToken cancellationToken = default);
    Task WithdrawAsync(Guid submissionId, Guid actorAccountId, CancellationToken cancellationToken = default, int? expectedVersion = null, bool ownerOnly = false);
    Task RejectAsync(Guid submissionId, Guid adminAccountId, string reason, CancellationToken cancellationToken = default, int? expectedVersion = null);
    Task<int> ApproveAsync(Guid submissionId, Guid adminAccountId, CancellationToken cancellationToken = default, int? expectedVersion = null);
    Task ReverseAsync(Guid submissionId, Guid adminAccountId, string reason, CancellationToken cancellationToken = default, int? expectedVersion = null);
    Task EditMetadataAsync(EditSubmissionMetadataCommand command, CancellationToken cancellationToken = default);
}

public sealed record CreateSubmissionCommand(Guid ActorAccountId, Guid EventId, Guid TeamId, Guid BoardTileId,
    Guid RequirementId, Guid? DropSnapshotId, Guid CreditedParticipantId, int ClaimedWeight, string? CaptainNote,
    string OriginalFilename, Stream Evidence);

public sealed record CorrectSubmissionCommand(Guid SubmissionId, Guid ActorAccountId, Guid BoardTileId,
    Guid RequirementId, Guid? DropSnapshotId, Guid CreditedParticipantId, int ClaimedWeight, string? CaptainNote,
    int? ExpectedVersion = null, string? OriginalFilename = null, Stream? Evidence = null, bool OwnerOnly = false);

public sealed record ResubmitSubmissionCommand(Guid PredecessorSubmissionId, Guid ActorAccountId, Guid BoardTileId,
    Guid RequirementId, Guid? DropSnapshotId, string? CaptainNote, string OriginalFilename, Stream Evidence,
    int? ExpectedVersion = null, bool OwnerOnly = false);

public sealed record EditSubmissionMetadataCommand(Guid SubmissionId, Guid AdminAccountId, Guid BoardTileId,
    Guid RequirementId, Guid? DropSnapshotId, Guid CreditedOsrsCharacterId, string Reason, int? ExpectedVersion = null);

public sealed record SubmissionResult(Guid SubmissionId, SubmissionStatus Status);
