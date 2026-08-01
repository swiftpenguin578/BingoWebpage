using Bingo.Domain.Events;

namespace Bingo.Application.Boards;

public interface IPublicBoardService
{
    Task<PublicEventBoard?> GetEventBoardAsync(string eventSlug, CancellationToken cancellationToken = default);
    Task<PublicTileDetails?> GetTileAsync(string eventSlug, string teamSlug, Guid tileId, CancellationToken cancellationToken = default);
}

public sealed record PublicEventBoard(
    Guid EventId, string EventName, string EventSlug, EventState EventState,
    int Rows, int Columns, decimal TotalBoardEhb,
    IReadOnlyList<PublicTeamBoard> Teams,
    IReadOnlyList<PublicPlayerRanking> PlayerLeaderboard,
    IReadOnlyList<PublicRecentDrop> RecentDrops);

public sealed record PublicTeamBoard(
    Guid TeamId, string TeamName, string TeamSlug, string? Affiliation, string? ImageUrl,
    int Rank, bool ProvisionalWinner, CalculatedBoardProgress Progress,
    IReadOnlyList<PublicTileProgress> Tiles);

public sealed record PublicTileProgress(
    Guid TileId, int Row, int Column, string Name, string Description,
    string? ImageUrl, IReadOnlyList<string> BossImageUrls,
    decimal EstimatedEhb, int Approved, int Target, bool Complete, DateTimeOffset? CompletedAt);

public sealed record PublicPlayerRanking(
    int Rank, Guid PlayerId, string PlayerName, string TeamName,
    decimal EstimatedEhb, int ApprovedContribution, int ApprovedSubmissions);

public sealed record PublicRecentDrop(
    Guid SubmissionId, Guid TileId, string TileName,
    string TeamName, string TeamSlug, string? PlayerName,
    string? BossName, string? DropName, int Contribution,
    DateTimeOffset ApprovedAt, Guid? EvidenceAssetId);

public sealed record PublicTileDetails(
    string EventName, string EventSlug, string TeamName, string TeamSlug,
    Guid TileId, string TileName, string Description, string EvidenceInstructions,
    int Approved, int Target, bool Complete, DateTimeOffset? CompletedAt,
    IReadOnlyList<PublicRequirementProgress> Requirements,
    IReadOnlyList<PublicApprovedEvidence> Evidence);

public sealed record PublicRequirementProgress(
    Guid RequirementId, string Description, int Approved, int Target, bool Complete,
    IReadOnlyList<PublicEligibleDrop> EligibleDrops);

public sealed record PublicEligibleDrop(
    string BossName, string ItemName, string DisplayRate, int CreditedWeight);

public sealed record PublicApprovedEvidence(
    Guid SubmissionId, string? PlayerName, string? BossName, string? DropName,
    int Contribution, DateTimeOffset SubmittedAt, Guid? EvidenceAssetId);
