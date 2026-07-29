using Bingo.Domain.Signups;

namespace Bingo.Application.Signups;

public sealed record SignupResult(
    bool Succeeded,
    string? Error,
    Guid? ParticipantId,
    SignupStatus? Status,
    int? WaitingListPosition);
