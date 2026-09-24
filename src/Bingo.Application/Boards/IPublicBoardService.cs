using Bingo.Domain.Events;

namespace Bingo.Application.Boards;

public interface IPublicBoardService
{
    Task<PublicEventBoard?> GetEventBoardAsync(string eventSlug, CancellationToken cancellationToken = default);
    Task<PublicEventBoard?> GetEventBoardAsync(string eventSlug, int recentDropCount, CancellationToken cancellationToken = default)
        => GetEventBoardAsync(eventSlug, cancellationToken);
    Task<PublicEventBoard?> GetEventBoardAsync(string eventSlug, int recentDropCount, string? dropSearch, string? dropTeam, CancellationToken cancellationToken = default)
        => GetEventBoardAsync(eventSlug, recentDropCount, cancellationToken);
    Task<PublicTileDetails?> GetTileAsync(string eventSlug, string teamSlug, Guid tileId, CancellationToken cancellationToken = default);
    Task<PublicRecentDrop?> GetRecentDropAsync(string eventSlug, Guid submissionId, CancellationToken cancellationToken = default) => Task.FromResult<PublicRecentDrop?>(null);
    Task<PublicRecentDropFeed?> GetRecentDropsAsync(string eventSlug, int limit = 25, string? dropSearch = null, string? dropTeam = null, IReadOnlyCollection<Guid>? loadedSubmissionIds = null, int offset = 0, CancellationToken cancellationToken = default) => Task.FromResult<PublicRecentDropFeed?>(null);
}

public sealed record PublicEventBoard(
    Guid EventId, string EventName, string EventSlug, EventState EventState,
    int Rows, int Columns, decimal TotalBoardEhb,
    IReadOnlyList<PublicTeamBoard> Teams,
    IReadOnlyList<PublicPlayerRanking> PlayerLeaderboard,
    IReadOnlyList<PublicRecentDrop> RecentDrops,
    DateTimeOffset? EventStartsAt = null, DateTimeOffset? EventEndsAt = null,
    PublicRecentDropSummary? RecentDropSummary = null, int? RecentDropFilteredTotal = null,
    PublicEventResult? EventResult = null, bool SubmissionsOpen = false,
    IReadOnlyList<PublicDropEhbTeam>? DropEhbTeams = null,
    IReadOnlyList<PublicRosterPlayer>? RosterPlayers = null,
    long? WiseOldManCompetitionId = null, string Timezone = "Europe/Copenhagen");

public sealed record PublicEventResult(string TeamName, string? TeamSlug, bool IsOfficial, IReadOnlyList<PublicEventResultTeam>? Teams = null);

public sealed record PublicEventResultTeam(string TeamName, string? TeamSlug);

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

// PlayerCount is the public roster count. Players contains every confirmed public roster player; contributing players are ordered first.
public sealed record PublicDropEhbTeam(
    int Rank, Guid TeamId, string TeamName, int PlayerCount, int ContributingPlayerCount,
    int TotalDrops, decimal DropEhb, IReadOnlyList<string> MvpNames,
    IReadOnlyList<PublicDropEhbPlayer> Players, decimal? MvpValue = null);

public sealed record PublicDropEhbPlayer(
    Guid PlayerId, string PlayerName, decimal DropEhb, int ApprovedContribution, int ApprovedSubmissions,
    IReadOnlyList<string>? PlayingAccountNames = null);

public sealed record PublicRosterPlayer(
    Guid TeamId, string TeamName, Guid PlayerId, string PlayerName,
    IReadOnlyList<string> PlayingAccountNames);

public sealed record PublicRecentDrop(
    Guid SubmissionId, Guid TileId, string TileName,
    string TeamName, string TeamSlug, string? PlayerName,
    string? BossName, string? DropName, int Contribution,
    DateTimeOffset SubmittedAt, DateTimeOffset? ReviewedAt, Guid? EvidenceAssetId, int ProgressAfter, int Target);

public sealed record PublicRecentDropSummary(
    int TotalDrops, int DropsLast24Hours, decimal TotalDropEhb,
    int UniqueDroppers, int TotalParticipants,
    string? HighestDropEhbTeamName, string? MostIndividualDropsTeamName,
    decimal? HighestDropEhbTeamValue = null, int? MostIndividualDropsTeamDropCount = null);

public sealed record PublicRecentDropFeed(IReadOnlyList<PublicRecentDrop> Drops, int Total, IReadOnlyList<Guid>? ValidSubmissionIds = null);

public sealed record PublicTileDetails(
    string EventName, string EventSlug, string TeamName, string TeamSlug,
    Guid TileId, string TileName, string Description, string EvidenceInstructions,
    int Approved, int Target, bool Complete, DateTimeOffset? CompletedAt,
    IReadOnlyList<PublicRequirementProgress> Requirements,
    IReadOnlyList<PublicApprovedEvidence> Evidence,
    string Timezone = "Europe/Copenhagen",
    Bingo.Application.Stats.StatsTileActivity? Activity = null);

public sealed record PublicRequirementProgress(
    Guid RequirementId, string Description, int Approved, int Target, bool Complete,
    IReadOnlyList<PublicEligibleDrop> EligibleDrops);

public sealed record PublicEligibleDrop(
    string BossName, string ItemName, string DisplayRate, int CreditedWeight,
    decimal? NumericProbability = null, int RollsPerCompletion = 1,
    Bingo.Domain.Catalogue.DropProbabilityScope ProbabilityScope = Bingo.Domain.Catalogue.DropProbabilityScope.Participant,
    bool ConditionalOnParent = false, decimal? ParentProbability = null, int AssumedParticipants = 1,
    string RollGroup = "default", string? RateCondition = null);

public sealed record PublicApprovedEvidence(
    Guid SubmissionId, string? PlayerName, string? BossName, string? DropName,
    int Contribution, DateTimeOffset SubmittedAt, Guid? EvidenceAssetId);
