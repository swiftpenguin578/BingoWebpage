using Bingo.Domain.Integrations.WiseOldMan;

namespace Bingo.Application.Integrations.WiseOldMan;

public enum EventCompetitionActivityState
{
    NotConfigured,
    WaitingForFirstSync,
    Complete,
    Partial,
    Incomplete,
    Stale,
    TemporarilyUnavailable
}

public sealed record EventCompetitionActivityProjection(
    EventCompetitionActivityState State,
    int Generation,
    DateTimeOffset? FetchedAt,
    DateTimeOffset? UpstreamUpdatedAt,
    IReadOnlyList<EventCompetitionTeamActivity> Teams,
    int ExpectedAccountCount = 0,
    int MatchedAccountCount = 0)
{
    public int MissingAccountCount => Math.Max(0, ExpectedAccountCount - MatchedAccountCount);
    public bool HasRankings => Teams.Any(team => team.Participants.Count > 0) && State is (EventCompetitionActivityState.Complete or EventCompetitionActivityState.Partial or EventCompetitionActivityState.Stale or EventCompetitionActivityState.TemporarilyUnavailable);
}

public sealed record EventCompetitionTeamActivity(
    Guid TeamId,
    string TeamName,
    decimal TotalGainedEhb,
    decimal AverageGainedEhb,
    IReadOnlyList<EventCompetitionParticipantActivity> Participants,
    IReadOnlyList<string> MvpNames,
    int ExpectedParticipantCount = 0,
    int ContributingParticipantCount = 0,
    int ExpectedAccountCount = 0,
    int MatchedAccountCount = 0,
    decimal? MvpValue = null)
{
    public int Rank { get; init; }
}

public sealed record EventCompetitionParticipantActivity(
    Guid ParticipantId,
    string ParticipantName,
    decimal TotalGainedEhb,
    IReadOnlyList<EventCompetitionAccountActivity> Accounts,
    int ExpectedAccountCount = 0,
    int MatchedAccountCount = 0,
    IReadOnlyList<string>? PlayingAccountNames = null);

public sealed record EventCompetitionAccountActivity(
    Guid CharacterId,
    string CharacterName,
    decimal GainedEhb,
    decimal? StartEhb = null,
    decimal? EndEhb = null);

public sealed record EventCompetitionMetricOption(string Metric, string DisplayName);

public sealed record EventCompetitionMetricLeaderboard(
    EventCompetitionActivityState State,
    int Generation,
    DateTimeOffset? FetchedAt,
    DateTimeOffset? UpstreamUpdatedAt,
    IReadOnlyList<EventCompetitionMetricOption> Options,
    EventCompetitionMetricOption? Selected,
    IReadOnlyList<EventCompetitionMetricTeamActivity> Teams,
    bool Compatible = false,
    bool Complete = false,
    bool Stale = false,
    DateTimeOffset? LastAttemptAt = null)
{
    public bool IsBossMode => Selected is not null;
}

public sealed record EventCompetitionMetricTeamActivity(
    Guid TeamId,
    string TeamName,
    string TeamSlug,
    decimal TotalGained,
    decimal AverageGained,
    IReadOnlyList<EventCompetitionMetricParticipantActivity> Participants,
    IReadOnlyList<string> MvpNames,
    int ExpectedParticipantCount,
    int ContributingParticipantCount,
    bool Incomplete,
    bool Stale,
    decimal? MvpValue = null)
{
    public int Rank { get; init; }
}

public sealed record EventCompetitionMetricParticipantActivity(
    Guid ParticipantId,
    string ParticipantName,
    decimal TotalGained,
    IReadOnlyList<EventCompetitionMetricAccountActivity> Accounts,
    int DropCount,
    bool Incomplete);

public sealed record EventCompetitionMetricAccountActivity(
    Guid CharacterId,
    string CharacterName,
    decimal? Gained,
    decimal? Start,
    decimal? End,
    decimal? RecordedActivity,
    MetricActivityCoverage Coverage,
    MetricActivityAvailability Availability,
    DateTimeOffset? FetchedAt,
    DateTimeOffset? UpstreamUpdatedAt,
    bool Stale);

public interface IEventCompetitionActivityProjection
{
    Task<EventCompetitionActivityProjection> GetAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<EventCompetitionTeamActivity?> GetTeamAsync(Guid eventId, Guid teamId, CancellationToken cancellationToken = default);
    Task<EventCompetitionMetricLeaderboard> GetMetricLeaderboardAsync(Guid eventId, string? metric, CancellationToken cancellationToken = default);
}
