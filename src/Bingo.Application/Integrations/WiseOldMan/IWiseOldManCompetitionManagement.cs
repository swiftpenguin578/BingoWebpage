namespace Bingo.Application.Integrations.WiseOldMan;

public interface IWiseOldManCompetitionManagementClient
{
    Task<WiseOldManCompetitionWriteResult> CreateAsync(
        WiseOldManCompetitionWritePayload payload,
        CancellationToken cancellationToken = default);

    Task<WiseOldManCompetitionWriteResult> UpdateAsync(
        long competitionId,
        WiseOldManCompetitionWritePayload payload,
        string verificationCode,
        CancellationToken cancellationToken = default);

    Task<WiseOldManCompetitionWriteResult> DeleteAsync(
        long competitionId,
        string verificationCode,
        CancellationToken cancellationToken = default);

    Task<WiseOldManUpdateAllResult> UpdateAllAsync(
        long competitionId,
        string verificationCode,
        DateTimeOffset dispatchDeadline,
        Func<CancellationToken, Task<bool>> recheckEligibility,
        CancellationToken cancellationToken = default);
}

public sealed record WiseOldManCompetitionWritePayload(
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    IReadOnlyList<WiseOldManCompetitionWriteTeam> Teams,
    bool IncludeTeams = true);

public sealed record WiseOldManCompetitionWriteTeam(
    string Name,
    IReadOnlyList<string> Participants);

public enum WiseOldManCompetitionWriteStatus
{
    Success,
    Validation,
    Unauthorized,
    NotFound,
    RateLimited,
    Unavailable,
    Unknown
}

public sealed record WiseOldManCompetitionWriteResult(
    WiseOldManCompetitionWriteStatus Status,
    WiseOldManCompetition? Competition = null,
    string? ProtectedVerificationCode = null,
    DateTimeOffset? RetryAt = null,
    string? ErrorCode = null,
    string? Message = null,
    IReadOnlyList<string>? AffectedParticipants = null)
{
    public bool Succeeded => Status == WiseOldManCompetitionWriteStatus.Success && Competition is not null;
}

/// <summary>
/// The update-all endpoint acknowledges that an asynchronous queue request was
/// accepted; it does not return a competition representation. Keep this
/// contract separate from configuration writes so an empty successful response
/// cannot be mistaken for an unknown configuration receipt.
/// </summary>
public enum WiseOldManUpdateAllStatus
{
    Acknowledged,
    Validation,
    Unauthorized,
    NotFound,
    RateLimited,
    Unknown
}

public sealed record WiseOldManUpdateAllResult(
    WiseOldManUpdateAllStatus Status,
    DateTimeOffset? RetryAt = null,
    string? ErrorCode = null,
    string? Message = null)
{
    public bool Acknowledged => Status == WiseOldManUpdateAllStatus.Acknowledged;
}
