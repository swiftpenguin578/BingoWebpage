using Bingo.Domain.Signups;

namespace Bingo.Application.Signups;

public sealed record SignupRequest(
    Guid EventId,
    string PrimaryAccountName,
    decimal Ehb,
    string? SecondAccountName,
    string? DiscordIdentity,
    string? Comments,
    bool CaptainVolunteer,
    string? SignupCode,
    IReadOnlyDictionary<Guid, string> CustomAnswers,
    SignupSource Source = SignupSource.Website,
    bool BypassAvailability = false);
