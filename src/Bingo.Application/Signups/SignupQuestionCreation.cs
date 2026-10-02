using Bingo.Domain.Signups;

namespace Bingo.Application.Signups;

public sealed record SignupQuestionCreationRequest(
    Guid RequestId, Guid EventId, Guid ActorAccountId, int? ExpectedFormVersion,
    string Label, SignupQuestionType Type, bool Required = false, string? HelpText = null,
    string? Options = null, EventCharacterRole? AccountRole = null);

public enum SignupQuestionCreationOutcome { Completed, Removed, Invalid, Conflict, Forbidden, Unavailable, Stale, Locked, Retryable, CompletedAsOptional }

public sealed record SignupQuestionCreationResult(
    SignupQuestionCreationOutcome Outcome, Guid RequestId, int? SubmittedFormVersion,
    Guid? QuestionId = null, int? FormVersion = null, bool Replayed = false, string? Error = null,
    SignupQuestionCreatedDefinition? OriginalDefinition = null, bool RequiredNormalizedToOptional = false)
{
    public bool Succeeded => Outcome is SignupQuestionCreationOutcome.Completed or SignupQuestionCreationOutcome.CompletedAsOptional;
    public string? Message => RequiredNormalizedToOptional
        ? "Question added as optional because the form has already received its first signup response."
        : null;
}

/// <summary>The immutable definition committed by this add, not a later edited definition.</summary>
public sealed record SignupQuestionCreatedDefinition(
    Guid Id, string Key, string Label, string? HelpText, SignupQuestionType Type,
    bool Required, int Position, string? Options, EventCharacterRole? AccountRole,
    bool Active, Guid? ReplacedBySignupQuestionId);
