using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bingo.Application.Signups;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Infrastructure.Signups;

public sealed partial class SignupService
{
    private static readonly JsonSerializerOptions CreationAuditJson = new()
    {
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<SignupQuestionCreationResult> AddQuestionAsync(SignupQuestionCreationRequest request, CancellationToken cancellationToken = default)
    {
        SignupQuestionCreationResult Failure(SignupQuestionCreationOutcome outcome, string error, int? version = null) =>
            new(outcome, request.RequestId, request.ExpectedFormVersion, FormVersion: version, Error: error);
        if (request.RequestId == Guid.Empty || request.EventId == Guid.Empty || request.ActorAccountId == Guid.Empty)
            return Failure(SignupQuestionCreationOutcome.Invalid, "A valid add request, event and administrator are required.");
        if (request.ExpectedFormVersion is null or < 0)
            return Failure(SignupQuestionCreationOutcome.Stale, "This signup form changed while you were editing it. Review the latest values and try again.");

        var label = request.Label?.Trim() ?? string.Empty;
        var help = string.IsNullOrWhiteSpace(request.HelpText) ? null : request.HelpText.Trim();
        string? options = null;
        if (request.Type == SignupQuestionType.Account)
        {
            if (request.AccountRole is not (EventCharacterRole.Playing or EventCharacterRole.Informational) || request.Required
                || request.Options is not null || help is not null)
                return Failure(SignupQuestionCreationOutcome.Invalid, "Choose a valid optional account field.");
            label = request.AccountRole == EventCharacterRole.Playing ? "Playing account" : "Alt account";
        }
        else if (request.Type is not (SignupQuestionType.Text or SignupQuestionType.Number or SignupQuestionType.YesNo or SignupQuestionType.SingleChoice)
            || request.AccountRole is not null)
            return Failure(SignupQuestionCreationOutcome.Invalid, "Choose a valid question format.");
        if (label.Length is 0 or > 300 || help?.Length > 1000 || request.Options?.Length > 4000)
            return Failure(SignupQuestionCreationOutcome.Invalid, "Enter a valid question label, help text and choices.");
        if (request.Type == SignupQuestionType.SingleChoice)
        {
            var choices = (request.Options ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (choices.Length == 0 || choices.Distinct(StringComparer.OrdinalIgnoreCase).Count() != choices.Length)
                return Failure(SignupQuestionCreationOutcome.Invalid, "Add distinct choices for this question.");
            options = string.Join('\n', choices);
        }
        // Compare intended input, never the effective optional value or subsequently edited definition.
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        { request.ExpectedFormVersion, Label = label, HelpText = help, request.Type, request.Required, Options = options, request.AccountRole }))));

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
                var actor = await AdminAsync(request.ActorAccountId, cancellationToken);
                if (actor is null) return Failure(SignupQuestionCreationOutcome.Forbidden, "Admin access is required.");
                if (bingoEvent is null || bingoEvent.State == EventState.Discarded)
                    return Failure(SignupQuestionCreationOutcome.Unavailable, "The event is not available.");
                var previous = await dbContext.SignupQuestionCreationOperations.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.RequestId == request.RequestId, cancellationToken);
                if (previous is not null)
                {
                    if (previous.ActorAccountId != request.ActorAccountId || previous.EventId != request.EventId || previous.InputFingerprint != fingerprint)
                        return Failure(SignupQuestionCreationOutcome.Conflict, "This add request belongs to another operation. Use the original values to retry it, or start a new request.");
                    // The operation identifies one immutable creation audit, never a same-label field
                    // or the mutable current definition. AU06 committed this snapshot atomically.
                    var snapshots = await dbContext.AuditEntries.AsNoTracking()
                        .Where(x => x.EventId == request.EventId && x.ActorAccountId == request.ActorAccountId
                            && x.TargetType == "signup_question" && x.TargetId == previous.QuestionId.ToString()
                            && x.Action == (request.Type == SignupQuestionType.Account ? "signup_question.account_added" : "signup_question.created"))
                        .Select(x => x.AfterState).Take(2).ToListAsync(cancellationToken);
                    SignupQuestionCreatedDefinition? original = null;
                    if (snapshots.Count == 1 && snapshots[0] is { } snapshot)
                    {
                        try { original = JsonSerializer.Deserialize<SignupQuestionCreatedDefinition>(snapshot, CreationAuditJson); }
                        catch (JsonException) { /* Fail closed without guessing a result from current state. */ }
                    }
                    if (original is null || original.Id != previous.QuestionId || string.IsNullOrWhiteSpace(original.Key)
                        || original.Label != label || original.HelpText != help || original.Type != request.Type
                        || original.Options != options || original.AccountRole != request.AccountRole
                        || (original.Required && !request.Required) || !original.Active || original.ReplacedBySignupQuestionId is not null)
                        return Failure(SignupQuestionCreationOutcome.Unavailable, "The original add result is unavailable. No new field was created.");
                    var normalized = request.Required && !original.Required;
                    var active = await dbContext.SignupQuestions.AsNoTracking().AnyAsync(x => x.Id == previous.QuestionId && x.EventId == request.EventId && x.Active, cancellationToken);
                    var currentVersion = await dbContext.SignupForms.AsNoTracking().Where(x => x.EventId == request.EventId).Select(x => (int?)x.Version).SingleOrDefaultAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return new(active ? normalized ? SignupQuestionCreationOutcome.CompletedAsOptional : SignupQuestionCreationOutcome.Completed : SignupQuestionCreationOutcome.Removed,
                        request.RequestId, request.ExpectedFormVersion, previous.QuestionId, currentVersion, Replayed: true,
                        Error: active ? null : "The field created by this request has been removed.",
                        OriginalDefinition: original, RequiredNormalizedToOptional: normalized);
                }
                if (bingoEvent.DraftLocked || bingoEvent.State is not (EventState.Draft or EventState.SignupOpen or EventState.SignupClosed))
                    return Failure(SignupQuestionCreationOutcome.Locked, "Signup questions can only be changed before the draft starts.");
                var form = await dbContext.SignupForms.SingleOrDefaultAsync(x => x.EventId == request.EventId, cancellationToken);
                if (form is null) return Failure(SignupQuestionCreationOutcome.Unavailable, "This event has no signup form.");
                if (form.Version != request.ExpectedFormVersion)
                    return Failure(SignupQuestionCreationOutcome.Stale, "This signup form changed while you were editing it. Review the latest values and try again.", form.Version);
                var position = (await dbContext.SignupQuestions.Where(x => x.EventId == request.EventId).MaxAsync(x => (int?)x.Position, cancellationToken) ?? 0) + 1;
                var stem = EventSlugGenerator.Generate(label).Replace('-', '_');
                if (string.Equals(stem, SignupQuestion.CoCaptainKey, StringComparison.OrdinalIgnoreCase)) stem += "_custom";
                // Keep the existing stable-key rules within the persistence column limit.
                stem = stem[..Math.Min(stem.Length, 90)];
                var key = stem;
                for (var suffix = 2; await dbContext.SignupQuestions.AnyAsync(x => x.EventId == request.EventId && x.Key == key, cancellationToken); suffix++)
                    key = $"{stem}_{suffix}";
                var question = new SignupQuestion(Guid.NewGuid(), form.Id, request.EventId, key, label, request.Type,
                    request.Required && form.FirstResponseAt is null, position, options, accountAnswerRole: request.AccountRole, helpText: help);
                dbContext.SignupQuestions.Add(question);
                dbContext.SignupQuestionCreationOperations.Add(new(request.RequestId, actor.Id, request.EventId, fingerprint, question.Id));
                dbContext.Entry(form).Property(x => x.Version).IsModified = true;
                dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), timeProvider.GetUtcNow(), actor.Id, actor.LoginName,
                    request.Type == SignupQuestionType.Account ? "signup_question.account_added" : "signup_question.created", "signup_question", question.Id.ToString(), null, request.EventId, null,
                    JsonSerializer.Serialize(new { question.Id, question.Key, question.Label, question.HelpText, Type = question.Type.ToString(), question.Required, question.Position, question.Options, AccountRole = question.AccountAnswerRole?.ToString(), question.Active, question.ReplacedBySignupQuestionId })));
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                var wasNormalized = request.Required && !question.Required;
                return new(wasNormalized ? SignupQuestionCreationOutcome.CompletedAsOptional : SignupQuestionCreationOutcome.Completed,
                    request.RequestId, request.ExpectedFormVersion, question.Id, form.Version,
                    OriginalDefinition: new(question.Id, question.Key, question.Label, question.HelpText, question.Type,
                        question.Required, question.Position, question.Options, question.AccountAnswerRole, question.Active, question.ReplacedBySignupQuestionId),
                    RequiredNormalizedToOptional: wasNormalized);
            }
            catch (Exception exception) when (IsQuestionCreationContention(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
            }
            catch
            {
                // A lost commit response remains uncertain; retain the same request for reconciliation.
                dbContext.ChangeTracker.Clear();
                throw;
            }
        }
        return Failure(SignupQuestionCreationOutcome.Retryable, "The add result could not be confirmed. Retry the same request with its original values.");
    }

    private static bool IsQuestionCreationContention(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException) return true;
            if (current is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected }) return true;
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_signup_question_creation_operations" }) return true;
        }
        return false;
    }
}
