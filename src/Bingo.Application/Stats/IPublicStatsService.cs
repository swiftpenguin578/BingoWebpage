using Bingo.Application.Boards;
using Bingo.Domain.Events;

namespace Bingo.Application.Stats;

public interface IPublicStatsService
{
    Task<PublicEventStats?> GetAsync(string eventSlug, CancellationToken cancellationToken = default);
    Task<StatsTileActivity?> GetTileAsync(string eventSlug, Guid teamId, Guid tileId, CancellationToken cancellationToken = default);
}

// No paging or presentation truncation here: the port can select every team, player, source and hover time.
public sealed record PublicEventStats(Guid EventId, string Name, string Slug, string Timezone, EventState State,
    DateTimeOffset? StartedAt, DateTimeOffset? EndedAt, long EvidenceRevision, Guid ApprovalId,
    StatsValueTotal Value, IReadOnlyList<StatsTeam> Teams, IReadOnlyList<StatsItemDrop> Drops,
    StatsLuck Luck, IReadOnlyList<StatsMilestone> Milestones, StatsRepeatedItem? RepeatedItem,
    StatsVersatilePlayer? MostVersatile, PublicEventResult? OfficialResult, int Rows, int Columns, IReadOnlyList<StatsValuePoint> ValueHistory,
    StatsItemDrop? MostValuableDrop, IReadOnlyList<StatsAggregateProgressPoint> ProgressHistory, IReadOnlyList<StatsTile>? Tiles = null);
public sealed record StatsTile(Guid Id, string Name, string? ImageUrl);
public sealed record StatsValueTotal(int Drops, decimal KnownValueGp, int MissingPrices, decimal? ValueGp);
public sealed record StatsValuePoint(DateTimeOffset At, Guid SubmissionId, StatsValueTotal Value);
public sealed record StatsItemIdentity(Guid ItemId, string Name, string? ImageUrl);
public sealed record StatsItemDrop(Guid SubmissionId, Guid TeamId, Guid PlayerId, Guid CharacterId, string CharacterName,
    Guid TileId, Guid SourceDropId, StatsItemIdentity Item, string BossName, DateTimeOffset SubmittedAt,
    long? ValueGp, DateTimeOffset? PriceHour, DateTimeOffset? PriceCapturedAt, EventItemPriceSource? PriceSource);
public sealed record StatsPlayer(Guid PlayerId, string Name, IReadOnlyList<string> PlayingAccountNames,
    StatsValueTotal Value, decimal? TeamValueShare, decimal? EventValueShare, IReadOnlyList<StatsValuePoint> ValueHistory,
    StatsItemDrop? MostValuableDrop, int DistinctTiles, StatsRepeatedItem? RepeatedItem);
public sealed record StatsTeam(Guid TeamId, string Name, string Slug, StatsValueTotal Value, decimal? EventValueShare,
    IReadOnlyList<StatsValuePoint> ValueHistory, StatsItemDrop? MostValuableDrop, IReadOnlyList<StatsPlayer> Players,
    CalculatedBoardProgress Progress, IReadOnlyList<StatsProgressPoint> ProgressHistory, IReadOnlyList<StatsMilestone> Milestones,
    StatsOfficialCompletion? OfficialCompletion, StatsRepeatedItem? RepeatedItem, StatsVersatilePlayer? MostVersatile);
public sealed record StatsOfficialCompletion(bool BoardComplete, DateTimeOffset? CompletedAt, int CompletedTiles, int CompletedLines);
public sealed record StatsAggregateProgressPoint(DateTimeOffset At, Guid SubmissionId, int Approved, int Target, int CompletedTiles, int TotalTiles, int CompletedLines, int TotalLines, int CompletedBoards);
public sealed record StatsProgressPoint(DateTimeOffset At, Guid SubmissionId, int Approved, int Target,
    int CompletedTiles, int CompletedLines, bool BoardComplete, IReadOnlyList<CalculatedTileProgress> Tiles);
public enum StatsMilestoneState { Pending, Reached, NotReached }
public sealed record StatsMilestone(string Id, int DefaultOrder, StatsMilestoneState State, DateTimeOffset? At,
    Guid? TeamId = null, Guid? PlayerId = null, Guid? TileId = null, string? LineKind = null, int? LineIndex = null,
    StatsItemDrop? Drop = null);
public sealed record StatsRepeatedItem(StatsItemIdentity Item, int Count, Guid? TeamId = null);
public sealed record StatsVersatilePlayer(Guid PlayerId, Guid TeamId, string Name, int DistinctTiles);

public enum StatsLuckStatus { Calculated, NoEligibleActivity, WaitingForActivityData, WaitingForActivityUpdate, Incomplete }
public sealed record StatsLuckResult(int Received, decimal? Expected, decimal? Percentage, StatsLuckStatus Status,
    bool Estimated, bool ZeroRecordedApproximation);
public sealed record StatsLuckSource(Guid SourceDropId, Guid ItemId, Guid? BossId, string? Metric, decimal? Probability,
    int? Rolls, decimal? ParentProbability, string? UnavailableReason, StatsItemIdentity Item, string BossName,
    Bingo.Domain.Catalogue.DropProbabilityScope? ProbabilityScope, int? AssumedParticipants, string? RollGroup, string? RateCondition);
public sealed record StatsLuckCharacterSource(Guid CharacterId, Guid SourceDropId, Guid ItemId, int Received,
    decimal? Activity, decimal? Expected, StatsLuckStatus Status, bool Estimated, bool ZeroRecordedApproximation,
    DateTimeOffset? FetchedAt, DateTimeOffset? UpstreamUpdatedAt);
public sealed record StatsLuckPlayer(Guid PlayerId, Guid TeamId, string Name, StatsLuckResult Result,
    IReadOnlyList<StatsLuckCharacterSource> Sources);
public sealed record StatsLuckTeam(Guid TeamId, string Name, StatsLuckResult Result, IReadOnlyList<StatsLuckPlayer> Players);
public sealed record StatsLuck(StatsLuckResult Result, IReadOnlyList<StatsLuckTeam> Teams, IReadOnlyList<StatsLuckSource> Sources,
    bool Stale, DateTimeOffset? CalculatedAt, DateTimeOffset? FetchedAt, DateTimeOffset? UpstreamUpdatedAt,
    long EvidenceRevision, Guid? ActivityBatchId, int? Generation, string? UnavailableReason = null,
    IReadOnlyList<StatsTileLuck>? Tiles = null);

// Tile totals retain their own evidence numerator alongside the event's Luck checkpoint.
public sealed record StatsTileLuck(Guid TileId, bool HasDropOutcomes, IReadOnlyList<StatsTileLuckTeam> Teams);
public sealed record StatsTileLuckTeam(Guid TeamId, StatsLuckResult Result, IReadOnlyList<StatsTileKc> Metrics,
    IReadOnlyList<StatsTileLuckPlayer> Players);
public sealed record StatsTileLuckPlayer(Guid PlayerId, string Name, StatsLuckResult Result, IReadOnlyList<StatsTileKc> Metrics);
public sealed record StatsTileKc(string? Metric, string Name, decimal? Count, StatsLuckStatus Status,
    bool Estimated, bool ZeroRecordedApproximation);
public sealed record StatsTileActivity(bool HasDropOutcomes, StatsTileLuckTeam Team, bool Stale,
    DateTimeOffset? CalculatedAt, DateTimeOffset? FetchedAt, long EvidenceRevision);
