namespace Bingo.Application.Integrations.WiseOldMan;

public interface IWiseOldManPlayerLookup
{
    Task<WiseOldManPlayerLookupResult> LookupPlayerAsync(string characterName, CancellationToken cancellationToken = default);
}

public interface IWiseOldManCompetitionClient
{
    Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, CancellationToken cancellationToken = default);
    Task<WiseOldManCompetitionResult> GetCompetitionAsync(long competitionId, IReadOnlyCollection<string> metrics, CancellationToken cancellationToken = default);
}

public interface IWiseOldManStatus
{
    WiseOldManRequestStatus GetStatus();
}

public enum WiseOldManLookupStatus
{
    Success,
    NotFound,
    RateLimited,
    Unavailable
}

public sealed record WiseOldManPlayerLookupResult(
    WiseOldManLookupStatus Status,
    decimal? Ehb = null,
    DateTimeOffset? FetchedAt = null,
    DateTimeOffset? RetryAt = null,
    string? Message = null)
{
    public bool Succeeded => Status == WiseOldManLookupStatus.Success && Ehb is not null && FetchedAt is not null;
}

public sealed record WiseOldManRequestStatus(
    int? ObservedLimit,
    int? ObservedRemaining,
    DateTimeOffset? ResetAt,
    DateTimeOffset? LastRequestAt,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastErrorAt,
    DateTimeOffset? LastRateLimitedAt,
    DateTimeOffset? NextPermittedAt);

public enum WiseOldManCompetitionStatus
{
    Success,
    NotFound,
    Invalid,
    RateLimited,
    Unavailable
}

public sealed record WiseOldManCompetitionParticipant(
    string Username,
    string? Type,
    decimal? EhbDelta,
    decimal? StartEhb = null,
    decimal? EndEhb = null,
    IReadOnlyDictionary<string, WiseOldManMetricDelta>? Metrics = null,
    DateTimeOffset? UpstreamUpdatedAt = null);

public sealed record WiseOldManMetricDelta(decimal? Start, decimal? End, decimal? Gained);

public sealed record WiseOldManCompetition(
    long Id,
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateTimeOffset? LastUpdatedAt,
    IReadOnlyList<WiseOldManCompetitionParticipant> Participants);

public sealed record WiseOldManCompetitionResult(
    WiseOldManCompetitionStatus Status,
    WiseOldManCompetition? Competition = null,
    DateTimeOffset? RetryAt = null,
    string? Message = null)
{
    public bool Succeeded => Status == WiseOldManCompetitionStatus.Success && Competition is not null;
}
