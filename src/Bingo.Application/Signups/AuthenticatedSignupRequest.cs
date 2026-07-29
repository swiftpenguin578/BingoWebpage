namespace Bingo.Application.Signups;

public sealed record AuthenticatedSignupRequest(
    Guid EventId,
    Guid AccountId,
    IReadOnlyDictionary<Guid, AuthenticatedAccountAnswer> AccountAnswers,
    IReadOnlyDictionary<Guid, string> Answers,
    string? SignupCode,
    int? ExpectedResponseVersion = null);

public sealed record AuthenticatedAccountAnswer(Guid OsrsCharacterId, decimal? Ehb);
