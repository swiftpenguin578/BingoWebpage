namespace Bingo.Domain.Events;

public sealed class FinalReviewResolution
{
    private FinalReviewResolution() { }
    public FinalReviewResolution(Guid id, Guid eventId, Guid reviewCycleId, string blockerKey, string blockerDescription, string? reason, Guid resolvedByAccountId, DateTimeOffset resolvedAt, FinalReviewResolutionKind kind = FinalReviewResolutionKind.ExceptionalOverride, Guid? teamId = null)
    {
        if (reviewCycleId == Guid.Empty) throw new ArgumentException("A final-review cycle is required.", nameof(reviewCycleId));
        if (string.IsNullOrWhiteSpace(blockerKey) || string.IsNullOrWhiteSpace(blockerDescription)) throw new ArgumentException("A blocker and description are required.");
        if (kind == FinalReviewResolutionKind.ExceptionalOverride && string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("An override reason is required.", nameof(reason));
        Id = id; EventId = eventId; ReviewCycleId = reviewCycleId; BlockerKey = blockerKey.Trim(); BlockerDescription = blockerDescription.Trim(); Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(); ResolvedByAccountId = resolvedByAccountId; ResolvedAt = resolvedAt.ToUniversalTime(); Kind = kind; TeamId = teamId;
    }

    // Retained-data compatibility constructor. New final-review writes must provide the cycle identity.
    public FinalReviewResolution(Guid id, Guid eventId, string blockerKey, string blockerDescription, string reason, Guid resolvedByAccountId, DateTimeOffset resolvedAt)
    { Id = id; EventId = eventId; BlockerKey = blockerKey; BlockerDescription = blockerDescription; Reason = reason; ResolvedByAccountId = resolvedByAccountId; ResolvedAt = resolvedAt.ToUniversalTime(); Kind = FinalReviewResolutionKind.ExceptionalOverride; }
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid ReviewCycleId { get; private set; }
    public string BlockerKey { get; private set; } = string.Empty;
    public string BlockerDescription { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public Guid ResolvedByAccountId { get; private set; }
    public DateTimeOffset ResolvedAt { get; private set; }
    public FinalReviewResolutionKind Kind { get; private set; }
    public Guid? TeamId { get; private set; }
}

public enum FinalReviewResolutionKind
{
    ExceptionalOverride = 1,
    CompletionTimeAcknowledgement = 2
}
