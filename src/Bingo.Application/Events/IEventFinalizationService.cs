using Bingo.Domain.Events;

namespace Bingo.Application.Events;

public interface IEventFinalizationService
{
    Task<FinalReviewReadiness?> GetReadinessAsync(Guid eventId, CancellationToken ct = default);
    Task ResolveBlockerAsync(Guid eventId, string blockerKey, string reason, Guid adminId, CancellationToken ct = default);
    Task CorrectCompletionAsync(Guid eventId, Guid teamId, DateTimeOffset correctedAt, string reason, Guid adminId, CancellationToken ct = default);
    Task FinalizeAsync(Guid eventId, Guid adminId, CancellationToken ct = default);
    Task UnfinalizeAsync(Guid eventId, string reason, Guid adminId, CancellationToken ct = default);
    Task ArchiveAsync(Guid eventId, Guid adminId, CancellationToken ct = default);
}

public sealed record FinalReviewReadiness(Guid EventId, string EventName, EventState State, DateTimeOffset EventStartsAt, DateTimeOffset EventEndsAt, DateTimeOffset SubmissionCutoff, bool SubmissionWindowOpen, IReadOnlyList<FinalReviewBlocker> Blockers, IReadOnlyList<ProvisionalPlacement> Placements, IReadOnlyList<FinalizationHistoryRow> History)
{ public bool CanFinalize => State == EventState.AwaitingFinalReview && Blockers.All(x => x.Resolved); }
public sealed record FinalReviewBlocker(string Key, string Title, string Description, string? Link, bool CanOverride, bool Resolved, string? ResolutionReason);
public sealed record ProvisionalPlacement(Guid TeamId, string TeamName, int Placement, bool BoardComplete, DateTimeOffset? CalculatedCompletedAt, DateTimeOffset? CorrectedCompletedAt, int CompletedLines, int CompletedTiles, decimal EhbTiebreak);
public sealed record FinalizationHistoryRow(Guid Id, int Version, DateTimeOffset FinalizedAt, bool Active, DateTimeOffset? UnfinalizedAt, string? UnfinalizeReason, IReadOnlyList<OfficialPlacementRow> Placements);
public sealed record OfficialPlacementRow(int Placement, string TeamName, bool BoardComplete, DateTimeOffset? BoardCompletedAt, int CompletedLines, int CompletedTiles, decimal EhbTiebreak);
