namespace Bingo.Application.Integrations.WiseOldMan;

/// <summary>
/// The narrow validation seam used by account-bearing writes. It deliberately
/// contains no EHB or persistence semantics: a successful result only proves
/// that the submitted character name was found by Wise Old Man recently.
/// </summary>
public interface IWiseOldManAccountValidation
{
    void RememberSuccessfulLookup(string characterName, DateTimeOffset fetchedAt) { }

    Task<WiseOldManAccountValidationResult> ValidateAsync(
        WiseOldManAccountValidationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record WiseOldManAccountValidationRequest(
    Guid ActorAccountId,
    string Action,
    Guid? EventId,
    Guid? ParticipantId,
    long? ExpectedVersion,
    IReadOnlyCollection<string> CharacterNames,
    bool EnabledAdmin,
    string? ConfirmationToken = null,
    Guid? TargetTeamId = null,
    Guid? TargetVacancyId = null);

public enum WiseOldManAccountValidationOutcome
{
    Success,
    KnownInvalid,
    OperationalFailure,
    ConfirmationRequired,
    ConfirmedOperationalFailure
}

public sealed record WiseOldManAccountValidationIssue(
    string CharacterName,
    string NormalizedName,
    WiseOldManLookupStatus Status,
    DateTimeOffset? RetryAt = null);

public sealed record WiseOldManAccountValidationResult(
    WiseOldManAccountValidationOutcome Outcome,
    IReadOnlyList<WiseOldManAccountValidationIssue> Issues,
    string? ConfirmationToken = null)
{
    public bool CanProceed => Outcome is WiseOldManAccountValidationOutcome.Success or WiseOldManAccountValidationOutcome.ConfirmedOperationalFailure;
    public bool HasKnownInvalid => Issues.Any(issue => issue.Status == WiseOldManLookupStatus.NotFound);
    public bool HasOperationalFailure => Issues.Any(issue => issue.Status is WiseOldManLookupStatus.RateLimited or WiseOldManLookupStatus.Unavailable);
}

public sealed class WiseOldManAccountValidationException(WiseOldManAccountValidationResult result)
    : InvalidOperationException("Wise Old Man account validation blocked this change.")
{
    public WiseOldManAccountValidationResult Result { get; } = result;
}
