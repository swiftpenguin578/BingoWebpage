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
    int MatchedAccountCount = 0)
{
    public int Rank { get; init; }
}

public sealed record EventCompetitionParticipantActivity(
    Guid ParticipantId,
    string ParticipantName,
    decimal TotalGainedEhb,
    IReadOnlyList<EventCompetitionAccountActivity> Accounts,
    int ExpectedAccountCount = 0,
    int MatchedAccountCount = 0);

public sealed record EventCompetitionAccountActivity(
    Guid CharacterId,
    string CharacterName,
    decimal GainedEhb);

public interface IEventCompetitionActivityProjection
{
    Task<EventCompetitionActivityProjection> GetAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<EventCompetitionTeamActivity?> GetTeamAsync(Guid eventId, Guid teamId, CancellationToken cancellationToken = default);
}
