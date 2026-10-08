using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;

namespace Bingo.Application.Events;

public sealed record FinalizationOperationResult(bool Published, bool AlreadyPublished, string? Feedback = null, EventCompetitionEndUpdateStatus? WomEndUpdateStatus = null, EventState? State = null, long? Version = null, Guid? LatestFinalizationId = null, FinalWomRefreshOutcome? FinalRefresh = null);

public interface IEventFinalizationService
{
    const int MaximumUnfinalizeReasonLength = 2_000;
    Task<FinalReviewReadiness?> GetReadinessAsync(Guid eventId, CancellationToken ct = default);
    Task ResolveBlockerAsync(Guid eventId, string blockerKey, string reason, bool confirmed, Guid adminId, long? expectedVersion = null, Guid? expectedReviewCycleId = null, CancellationToken ct = default);
    Task AcknowledgeCompletionTimeAsync(Guid eventId, Guid teamId, Guid adminId, long? expectedVersion = null, Guid? expectedReviewCycleId = null, string? expectedInspectionKey = null, CancellationToken ct = default);
    Task CorrectCompletionAsync(Guid eventId, Guid teamId, DateTimeOffset correctedAt, string reason, Guid adminId, long? expectedVersion = null, Guid? expectedReviewCycleId = null, CancellationToken ct = default);
    Task<FinalizationOperationResult> FinalizeAsync(Guid eventId, LifecycleActor actor, long? expectedVersion = null, CancellationToken ct = default);
    Task UnfinalizeAsync(Guid eventId, string reason, bool confirmed, LifecycleActor actor, long? expectedVersion = null, CancellationToken ct = default);
    Task ArchiveAsync(Guid eventId, bool confirmed, LifecycleActor actor, CancellationToken ct = default);
}

public sealed record FinalReviewReadiness(Guid EventId, string EventName, EventState State, DateTimeOffset? EventStartsAt, DateTimeOffset? EventEndsAt, DateTimeOffset? SubmissionCutoff, bool SubmissionWindowOpen, IReadOnlyList<FinalReviewBlocker> Blockers, IReadOnlyList<ProvisionalPlacement> Placements, IReadOnlyList<FinalizationHistoryRow> History, Guid ReviewCycleId = default, long EventVersion = 0, PlacementRule PlacementRule = PlacementRule.LegacyScoreTimeThenEhb, EventCompetitionEndUpdateStatus WomEndUpdateStatus = EventCompetitionEndUpdateStatus.NotRequired, FinalReviewBlockingEvent? BlockingCurrentEvent = null)
{
    /// <summary>
    /// A blocker is an authoritative prerequisite, not a task an administrator
    /// may acknowledge away. Exact competitive ties are therefore represented by
    /// the calculated placements and do not create a blocker.
    /// </summary>
    public bool CanFinalize => State == EventState.AwaitingFinalReview && Blockers.Count == 0;
}
public sealed record FinalReviewBlockingEvent(Guid Id, string Name, EventState State, string Reason);
public sealed record FinalReviewBlocker(string Key, string Title, string Description, string? Link, bool CanOverride, bool Resolved, string? ResolutionReason, bool IsCompletionTimeAcknowledgement = false, Guid? TeamId = null);
public sealed record ProvisionalPlacement(Guid TeamId, string TeamName, int Placement, bool BoardComplete, DateTimeOffset? CalculatedCompletedAt, DateTimeOffset? CorrectedCompletedAt, int CompletedLines, int CompletedTiles, decimal EhbTiebreak, DateTimeOffset? CurrentScoreReachedAt = null);
public sealed record FinalizationHistoryRow(Guid Id, int Version, DateTimeOffset FinalizedAt, bool Active, DateTimeOffset? UnfinalizedAt, string? UnfinalizeReason, IReadOnlyList<OfficialPlacementRow> Placements, Guid? FinalizedByAccountId = null,
    string? FinalizedByUsername = null, Guid? UnfinalizedByAccountId = null, string? UnfinalizedByUsername = null,
    FinalWomRefreshOutcome? FinalWomRefresh = null);
// Explicit values preserve the numeric form written by early local AU18 versions.
public enum FinalWomRefreshStatus { Succeeded = 0, Failed = 1, Skipped = 2 }
public sealed record FinalWomRefreshOutcome(FinalWomRefreshStatus Status,
    EventCompetitionRefreshSkipReason? SkipReason = null, DateTimeOffset? NextEligibleAt = null);
public sealed record OfficialPlacementRow(int Placement, string TeamName, bool BoardComplete, DateTimeOffset? BoardCompletedAt, int CompletedLines, int CompletedTiles, decimal EhbTiebreak, DateTimeOffset? CurrentScoreReachedAt = null);
