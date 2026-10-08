using Bingo.Application.Evidence;

namespace Bingo.IntegrationTests;

// RL-1/BR-12 (U8, A10): the admin review actions refuse a missing or zero expected version.
// Tests that exercise other review rules send the version current at the call, read through the
// service's own no-store readback; the missing/zero refusal itself is covered by dedicated tests.
internal static class ReviewVersionTestExtensions
{
    public static async Task<int> CurrentReviewVersionAsync(this ISubmissionService service, Guid submissionId, Guid adminAccountId) =>
        (await service.GetReviewReadbackAsync(submissionId, adminAccountId)).State?.Version ?? 1;

    public static async Task<SubmissionApprovalResult> ApproveCurrentAsync(this ISubmissionService service, Guid submissionId, Guid adminAccountId, CancellationToken cancellationToken = default) =>
        await service.ApproveAsync(submissionId, adminAccountId, cancellationToken, await service.CurrentReviewVersionAsync(submissionId, adminAccountId));

    public static async Task RejectCurrentAsync(this ISubmissionService service, Guid submissionId, Guid adminAccountId, string reason, CancellationToken cancellationToken = default) =>
        await service.RejectAsync(submissionId, adminAccountId, reason, cancellationToken, await service.CurrentReviewVersionAsync(submissionId, adminAccountId));

    public static async Task ReverseCurrentAsync(this ISubmissionService service, Guid submissionId, Guid adminAccountId, string reason, CancellationToken cancellationToken = default) =>
        await service.ReverseAsync(submissionId, adminAccountId, reason, cancellationToken, await service.CurrentReviewVersionAsync(submissionId, adminAccountId));

    public static async Task EditMetadataCurrentAsync(this ISubmissionService service, EditSubmissionMetadataCommand command, CancellationToken cancellationToken = default) =>
        await service.EditMetadataAsync(command with { ExpectedVersion = await service.CurrentReviewVersionAsync(command.SubmissionId, command.AdminAccountId) }, cancellationToken);
}
