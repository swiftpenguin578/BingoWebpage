using System.Data;
using Bingo.Application.Security;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Infrastructure.Signups;

public sealed class SignupService(
    ApplicationDbContext dbContext,
    ISecretHasher secretHasher,
    TimeProvider timeProvider) : ISignupService
{
    public async Task<ParticipantPaymentResult> SetPaymentAsync(Guid eventId, Guid participantId, Guid? actorAccountId, string actorName, PaymentStatus payment, CancellationToken cancellationToken = default)
    {
        if (payment is not (PaymentStatus.Unpaid or PaymentStatus.Paid)) return new(false, "Choose Paid or Unpaid.");
        if (actorAccountId is null) return new(false, "Admin access is required.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var bingoEvent = await LockEventAsync(eventId, cancellationToken);
        if (bingoEvent is null) return new(false, "The event could not be found.");
        var actor = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == actorAccountId && x.Active && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin), cancellationToken);
        if (actor is null) return new(false, "Admin access is required.");
        if (bingoEvent.DraftLocked || bingoEvent.State is not (Domain.Events.EventState.SignupOpen or Domain.Events.EventState.SignupClosed)) return new(false, "Participant administration is read-only after the draft starts.");
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.EventId == eventId && x.Id == participantId, cancellationToken);
        if (participant is null) return new(false, "The participant could not be found.");
        var before = participant.PaymentStatus;
        if (before == payment) { await transaction.CommitAsync(cancellationToken); return new(true, null); }
        participant.SetPaymentStatus(payment);
        dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), timeProvider.GetUtcNow(), actorAccountId, actorName, "participant.payment_updated", "participant", participant.Id.ToString(), "Payment changed.", eventId, $"{{\"payment\":\"{before}\"}}", $"{{\"payment\":\"{payment}\"}}"));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, null, true);
    }
    public async Task<SignupResult> SignUpAuthenticatedAsync(AuthenticatedSignupRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var bingoEvent = await dbContext.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {request.EventId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (bingoEvent is null || !bingoEvent.AcceptsSignups(now)) return new(false, "Signups are not currently open.", null, null, null);
        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == request.AccountId && x.AccountType == AccountType.WebsiteAccount && x.Active, cancellationToken);
        if (account is null) return new(false, "Signups require a normal website account.", null, null, null);
        var existing = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.EventId == request.EventId && x.AccountId == request.AccountId, cancellationToken);
        if (existing is not null && (request.ExpectedResponseVersion is null || request.ExpectedResponseVersion != existing.ResponseVersion))
            return new(false, "Your signup changed while you were editing it. Please reload and try again.", null, null, null);
        if (bingoEvent.RequireSignupCode && (string.IsNullOrWhiteSpace(request.SignupCode) || bingoEvent.SignupCodeHash is null || !secretHasher.Verify(request.SignupCode, bingoEvent.SignupCodeHash))) return new(false, "The event code is incorrect.", null, null, null);

        var form = await dbContext.SignupForms.SingleAsync(x => x.EventId == request.EventId, cancellationToken);
        var questions = await dbContext.SignupQuestions.Where(x => x.SignupFormId == form.Id && x.Active).OrderBy(x => x.Position).ToListAsync(cancellationToken);
        var links = await (from link in dbContext.AccountOsrsCharacters
                           join character in dbContext.OsrsCharacters on link.OsrsCharacterId equals character.Id
                           where link.AccountId == request.AccountId && link.Active
                           select new { Link = link, Character = character }).ToDictionaryAsync(x => x.Character.Id, cancellationToken);
        var currentAssignments = existing is null
            ? []
            : await dbContext.EventParticipantCharacters.Where(x => x.EventParticipantId == existing.Id && x.ReleasedAt == null).ToListAsync(cancellationToken);
        var currentByQuestion = currentAssignments.Where(x => x.SignupQuestionId is not null).ToDictionary(x => x.SignupQuestionId!.Value);
        var requestedCharacters = new HashSet<Guid>();
        foreach (var question in questions)
        {
            if (question.Type == SignupQuestionType.Account)
            {
                var supplied = request.AccountAnswers.TryGetValue(question.Id, out var answer) && answer is not null && answer.OsrsCharacterId != Guid.Empty;
                if (!supplied)
                {
                    if (question.Required) return new(false, $"'{question.Label}' is required.", null, null, null);
                    continue;
                }
                var selectedAnswer = answer!;
                if (!requestedCharacters.Add(selectedAnswer.OsrsCharacterId)) return new(false, "Choose each account only once.", null, null, null);
                var keepsHistoricalAssignment = currentByQuestion.TryGetValue(question.Id, out var current) && current.OsrsCharacterId == selectedAnswer.OsrsCharacterId;
                if (!keepsHistoricalAssignment && !links.ContainsKey(selectedAnswer.OsrsCharacterId)) return new(false, "Choose an account from My accounts.", null, null, null);
                if (question.AccountAnswerRole == EventCharacterRole.Playing && (selectedAnswer.Ehb is null || selectedAnswer.Ehb < 0)) return new(false, $"'{question.Label}' requires EHB.", null, null, null);
                continue;
            }
            if (!ValidateAnswer(question, request.Answers.TryGetValue(question.Id, out var value) ? value : null, out var error))
                return new(false, error, null, null, null);
        }
        if (existing is not null && !bingoEvent.AcceptsSignups(now)) return new(false, "Signups are not currently open.", null, null, null);
        var participant = existing;
        SignupStatus status;
        if (participant is null)
        {
            var confirmed = await dbContext.EventParticipants.CountAsync(x => x.EventId == request.EventId && x.SignupStatus == SignupStatus.Confirmed, cancellationToken);
            status = confirmed < bingoEvent.ParticipantCap ? SignupStatus.Confirmed : SignupStatus.WaitingList;
            if (status == SignupStatus.WaitingList && !bingoEvent.WaitingListEnabled) return new(false, "This event is full and does not have a waiting list.", null, null, null);
            var sequence = (await dbContext.EventParticipants.Where(x => x.EventId == request.EventId).MaxAsync(x => (long?)x.SignupSequence, cancellationToken) ?? 0) + 1;
            participant = new EventParticipant(Guid.NewGuid(), request.EventId, status, sequence, now, SignupSource.Website);
            participant.AssignOwner(account);
            dbContext.EventParticipants.Add(participant);
        }
        else status = participant.SignupStatus;
        var captain = questions.SingleOrDefault(x => x.SystemField == SignupSystemField.CaptainVolunteer);
        participant.SetCaptainVolunteer(captain is not null && request.Answers.TryGetValue(captain.Id, out var captainAnswer) && bool.TryParse(captainAnswer, out var volunteered) && volunteered);
        var answers = existing is null ? [] : await dbContext.SignupAnswers.Where(x => x.EventParticipantId == participant.Id).ToListAsync(cancellationToken);
        var answersByQuestion = answers.ToDictionary(x => x.SignupQuestionId);
        var order = (currentAssignments.Select(x => (int?)x.RegistrationOrder).Max() ?? -1) + 1;
        foreach (var question in questions.Where(x => x.Type == SignupQuestionType.Account))
        {
            var supplied = request.AccountAnswers.TryGetValue(question.Id, out var answer) && answer is not null && answer.OsrsCharacterId != Guid.Empty;
            currentByQuestion.TryGetValue(question.Id, out var current);
            if (!supplied)
            {
                current?.Release(request.AccountId, now);
                if (answersByQuestion.Remove(question.Id, out var removedAnswer)) dbContext.SignupAnswers.Remove(removedAnswer);
                continue;
            }
            var selectedAnswer = answer!;
            var selected = links.GetValueOrDefault(selectedAnswer.OsrsCharacterId);
            var sameAssignment = current is not null && current.OsrsCharacterId == selectedAnswer.OsrsCharacterId;
            var reserved = await dbContext.EventParticipantCharacters.AnyAsync(x => x.EventId == request.EventId && x.OsrsCharacterId == selectedAnswer.OsrsCharacterId && x.EventParticipantId != participant.Id && x.ReleasedAt == null, cancellationToken);
            if (reserved) return new(false, "That account is already signed up for this event.", null, null, null);
            var role = question.AccountAnswerRole!.Value;
            if (role == EventCharacterRole.Playing && selected is not null)
                selected.Link.UpdatePreferences(selected.Link.PersonalLabel, selected.Link.Position, selected.Link.Preferred, selectedAnswer.Ehb, now);
            if (sameAssignment)
            {
                if (role == EventCharacterRole.Playing) current!.UpdatePlayingEhb(selectedAnswer.Ehb!.Value, EhbSource.Manual);
            }
            else
            {
                current?.Release(request.AccountId, now);
                dbContext.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), request.EventId, participant.Id, selectedAnswer.OsrsCharacterId, order++, now, request.AccountId, question.Id, role, selectedAnswer.Ehb, role == EventCharacterRole.Playing ? EhbSource.Manual : null, null));
            }
            if (answersByQuestion.TryGetValue(question.Id, out var existingAnswer)) existingAnswer.SetAccountCharacter(selectedAnswer.OsrsCharacterId);
            else dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, question.Id, question.Label, string.Empty, selectedAnswer.OsrsCharacterId));
        }
        foreach (var question in questions.Where(x => x.Type != SignupQuestionType.Account && x.SystemField != SignupSystemField.CaptainVolunteer))
        {
            var value = request.Answers.TryGetValue(question.Id, out var submitted) ? CanonicalAnswer(question, submitted) : null;
            if (string.IsNullOrWhiteSpace(value))
            {
                if (answersByQuestion.Remove(question.Id, out var removedAnswer)) dbContext.SignupAnswers.Remove(removedAnswer);
            }
            else if (answersByQuestion.TryGetValue(question.Id, out var existingAnswer)) existingAnswer.Update(value);
            else dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, question.Id, question.Label, value));
        }
        if (existing is null) form.RecordAcceptedResponse(now);
        else participant.AdvanceResponseVersion();
        try { await dbContext.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { dbContext.ChangeTracker.Clear(); return new(false, "Your signup changed while you were editing it. Please reload and try again.", null, null, null); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { dbContext.ChangeTracker.Clear(); return new(false, "That account is already signed up for this event.", null, null, null); }
        catch (DbUpdateException) { dbContext.ChangeTracker.Clear(); return new(false, "Your signup could not be saved. Please try again.", null, null, null); }
        return new(true, null, participant.Id, status, status == SignupStatus.WaitingList ? await GetWaitingPositionAsync(participant.Id, request.EventId, cancellationToken) : null);
    }

    public Task<AdminParticipantResult> CorrectAdminParticipantAsync(AdminParticipantChangeRequest request, CancellationToken cancellationToken = default) =>
        ApplyAdminParticipantChangeAsync(request, false, cancellationToken);

    public Task<AdminParticipantResult> CreateAdminParticipantAsync(AdminParticipantChangeRequest request, CancellationToken cancellationToken = default) =>
        ApplyAdminParticipantChangeAsync(request, true, cancellationToken);

    public async Task<ParticipantOwnershipTransferResult> TransferParticipantOwnershipAsync(ParticipantOwnershipTransferRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DestinationUsername) || !string.Equals(request.DestinationUsername, request.ConfirmationUsername, StringComparison.Ordinal))
            return new(false, "Enter the exact destination username to confirm the transfer.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
        if (bingoEvent is null) return new(false, "The event could not be found.");
        var actor = await AdminAsync(request.ActorAccountId, cancellationToken);
        if (actor is null) return new(false, "Admin access is required.");
        if (!CanAdministerParticipants(bingoEvent)) return new(false, "Participant administration is read-only after the draft starts.");
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.EventId == request.EventId && x.Id == request.ParticipantId, cancellationToken);
        if (participant is null) return new(false, "The participant could not be found.");
        if (request.ExpectedOwnerAccountId != participant.AccountId) return new(false, "This participant ownership changed elsewhere. Reload before transferring it.");
        var destination = await dbContext.Accounts.SingleOrDefaultAsync(x => x.LoginName == request.DestinationUsername && x.Active && x.AccountType == AccountType.WebsiteAccount, cancellationToken);
        if (destination is null) return new(false, "The destination must be an active website account.");
        if (!string.Equals(destination.LoginName, request.DestinationUsername, StringComparison.Ordinal)) return new(false, "Enter the destination username exactly as shown.");
        if (participant.AccountId == destination.Id) { await transaction.CommitAsync(cancellationToken); return new(true, null); }
        if (await dbContext.EventParticipants.AnyAsync(x => x.EventId == request.EventId && x.AccountId == destination.Id && x.Id != participant.Id, cancellationToken)) return new(false, "That account already owns a participant in this event.");
        var previousOwnerId = participant.AccountId;
        participant.TransferOwner(destination);
        var now = timeProvider.GetUtcNow();
        dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, request.ActorAccountId, request.ActorName, "participant.ownership_transferred", "participant", participant.Id.ToString(), "Participant ownership transferred.", request.EventId,
            $"{{\"accountId\":{Json(previousOwnerId)}}}", $"{{\"accountId\":{Json(destination.Id)},\"username\":{Json(destination.LoginName)}}}"));
        var route = $"/Events/{Uri.EscapeDataString(bingoEvent.Slug)}/Signup/Confirmation?participantId={participant.Id}";
        if (previousOwnerId is { } oldOwner && oldOwner != destination.Id)
            dbContext.PersonalNotifications.Add(new PersonalNotification(Guid.NewGuid(), oldOwner, "participant.ownership_transferred", "Your event participant access changed.", route, now));
        if (previousOwnerId != destination.Id)
            dbContext.PersonalNotifications.Add(new PersonalNotification(Guid.NewGuid(), destination.Id, "participant.ownership_transferred", "Your event participant access changed.", route, now));
        try { await dbContext.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { dbContext.ChangeTracker.Clear(); return new(false, "That account already owns a participant in this event."); }
        catch (Exception exception) when (HasSerializationConflict(exception)) { dbContext.ChangeTracker.Clear(); return new(false, "This participant ownership changed elsewhere. Reload before transferring it."); }
        return new(true, null, true);
    }

    private async Task<AdminParticipantResult> ApplyAdminParticipantChangeAsync(AdminParticipantChangeRequest request, bool creating, CancellationToken ct)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var now = timeProvider.GetUtcNow();
        var bingoEvent = await LockEventAsync(request.EventId, ct);
        if (bingoEvent is null) return new(false, "The event could not be found.");
        var actor = await AdminAsync(request.ActorAccountId, ct);
        if (actor is null) return new(false, "Admin access is required.");
        if (!CanAdministerParticipants(bingoEvent)) return new(false, "Participant administration is read-only after the draft starts.");
        var form = await dbContext.SignupForms.SingleOrDefaultAsync(x => x.EventId == request.EventId, ct);
        if (form is null) return new(false, "This event has no signup form.");
        var questions = await dbContext.SignupQuestions.Where(x => x.SignupFormId == form.Id && x.Active).OrderBy(x => x.Position).ToListAsync(ct);
        EventParticipant? participant = null;
        if (!creating)
        {
            if (request.ParticipantId is null) return new(false, "The participant could not be found.");
            participant = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.EventId == request.EventId && x.Id == request.ParticipantId, ct);
            if (participant is null) return new(false, "The participant could not be found.");
            if (request.ExpectedResponseVersion is null || request.ExpectedResponseVersion != participant.ResponseVersion)
                return new(false, "The participant changed while you were editing it. Reload and try again.");
        }
        Account? owner = null;
        if (creating && request.OwnerAccountId is { } ownerId)
        {
            owner = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == ownerId && x.Active && x.AccountType == AccountType.WebsiteAccount, ct);
            if (owner is null) return new(false, "The selected owner must be an active website account.");
            if (await dbContext.EventParticipants.AnyAsync(x => x.EventId == request.EventId && x.AccountId == ownerId, ct)) return new(false, "That website account already owns a participant in this event.");
        }
        var submittedCharacters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var question in questions)
        {
            if (question.Type == SignupQuestionType.Account)
            {
                request.AccountAnswers.TryGetValue(question.Id, out var answer);
                var name = answer?.CharacterName?.Trim();
                if (string.IsNullOrWhiteSpace(name)) { if (question.Required) return new(false, $"'{question.Label}' is required."); continue; }
                var normalized = NormalizeAccountName(name);
                if (!submittedCharacters.Add(normalized)) return new(false, "Choose each account only once.");
                if (question.AccountAnswerRole == EventCharacterRole.Playing && (answer?.Ehb is null || answer.Ehb < 0)) return new(false, $"'{question.Label}' requires EHB.");
            }
            else if (!ValidateAnswer(question, request.Answers.TryGetValue(question.Id, out var value) ? value : null, out var error)) return new(false, error);
        }
        SignupStatus status;
        if (participant is null)
        {
            var confirmed = await dbContext.EventParticipants.CountAsync(x => x.EventId == request.EventId && x.SignupStatus == SignupStatus.Confirmed, ct);
            status = confirmed < bingoEvent.ParticipantCap ? SignupStatus.Confirmed : SignupStatus.WaitingList;
            if (status == SignupStatus.WaitingList && !bingoEvent.WaitingListEnabled) return new(false, "This event is full and does not have a waiting list.");
            var sequence = (await dbContext.EventParticipants.Where(x => x.EventId == request.EventId).MaxAsync(x => (long?)x.SignupSequence, ct) ?? 0) + 1;
            participant = new EventParticipant(Guid.NewGuid(), request.EventId, status, sequence, now, SignupSource.AdminCreated);
            if (owner is not null) participant.AssignOwner(owner);
            dbContext.EventParticipants.Add(participant);
        }
        else status = participant.SignupStatus;
        var before = creating ? null : await AdminStateAsync(participant.Id, ct);
        var current = await dbContext.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null).ToListAsync(ct);
        var currentByQuestion = current.Where(x => x.SignupQuestionId is not null).ToDictionary(x => x.SignupQuestionId!.Value);
        var answers = await dbContext.SignupAnswers.Where(x => x.EventParticipantId == participant.Id).ToListAsync(ct);
        var answersByQuestion = answers.ToDictionary(x => x.SignupQuestionId);
        var order = (await dbContext.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id).MaxAsync(x => (int?)x.RegistrationOrder, ct) ?? -1) + 1;
        foreach (var question in questions.Where(x => x.Type == SignupQuestionType.Account))
        {
            request.AccountAnswers.TryGetValue(question.Id, out var supplied);
            var name = supplied?.CharacterName?.Trim(); currentByQuestion.TryGetValue(question.Id, out var existing);
            if (string.IsNullOrWhiteSpace(name)) { existing?.Release(request.ActorAccountId, now); if (answersByQuestion.Remove(question.Id, out var removed)) dbContext.SignupAnswers.Remove(removed); continue; }
            var normalized = NormalizeAccountName(name);
            var character = await ResolveCharacterAsync(name, normalized, ct);
            if (await dbContext.EventParticipantCharacters.AnyAsync(x => x.EventId == request.EventId && x.OsrsCharacterId == character.Id && x.EventParticipantId != participant.Id && x.ReleasedAt == null, ct)) return new(false, "That account is already signed up for this event.");
            var role = question.AccountAnswerRole!.Value;
            if (existing is not null && existing.OsrsCharacterId == character.Id) { if (role == EventCharacterRole.Playing) existing.UpdatePlayingEhb(supplied!.Ehb!.Value, EhbSource.AdminCorrection); }
            else { existing?.Release(request.ActorAccountId, now); dbContext.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), request.EventId, participant.Id, character.Id, order++, now, request.ActorAccountId, question.Id, role, supplied?.Ehb, role == EventCharacterRole.Playing ? EhbSource.AdminCorrection : null, null)); }
            if (answersByQuestion.TryGetValue(question.Id, out var saved)) saved.SetAccountCharacter(character.Id); else dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, question.Id, question.Label, string.Empty, character.Id));
        }
        var captain = questions.SingleOrDefault(x => x.SystemField == SignupSystemField.CaptainVolunteer);
        participant.SetCaptainVolunteer(captain is not null && request.Answers.TryGetValue(captain.Id, out var captainValue) && bool.TryParse(captainValue, out var volunteered) && volunteered);
        foreach (var question in questions.Where(x => x.Type != SignupQuestionType.Account && x.SystemField != SignupSystemField.CaptainVolunteer))
        {
            var value = request.Answers.TryGetValue(question.Id, out var answer) ? CanonicalAnswer(question, answer) : null;
            if (string.IsNullOrWhiteSpace(value)) { if (answersByQuestion.Remove(question.Id, out var removed)) dbContext.SignupAnswers.Remove(removed); }
            else if (answersByQuestion.TryGetValue(question.Id, out var saved)) saved.Update(value); else dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, question.Id, question.Label, value));
        }
        if (creating) form.RecordAcceptedResponse(now);
        else participant.AdvanceResponseVersion();
        try
        {
            // Flush inside the enclosing transaction so the structured after-state includes new/released assignments.
            await dbContext.SaveChangesAsync(ct);
            var after = await AdminStateAsync(participant.Id, ct);
            dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, request.ActorAccountId, request.ActorName, creating ? "participant.admin_created" : "participant.corrected", "participant", participant.Id.ToString(), creating ? "Admin participant created." : "Participant signup corrected.", request.EventId, before, after));
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { dbContext.ChangeTracker.Clear(); return new(false, "The participant changed while you were editing it. Reload and try again."); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { dbContext.ChangeTracker.Clear(); return new(false, "That account is already signed up for this event."); }
        catch (DbUpdateException) { dbContext.ChangeTracker.Clear(); return new(false, "The participant could not be saved. Please try again."); }
        return new(true, null, participant.Id, status, status == SignupStatus.WaitingList ? await GetWaitingPositionAsync(participant.Id, request.EventId, ct) : null);
    }

    private async Task<Account?> AdminAsync(Guid accountId, CancellationToken ct) => await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == accountId && x.Active && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin), ct);
    private static bool CanAdministerParticipants(Domain.Events.BingoEvent bingoEvent) => !bingoEvent.DraftLocked && bingoEvent.State is Domain.Events.EventState.SignupOpen or Domain.Events.EventState.SignupClosed;
    private async Task<OsrsCharacter> ResolveCharacterAsync(string name, string normalized, CancellationToken ct)
    {
        var character = await dbContext.OsrsCharacters.SingleOrDefaultAsync(x => x.NormalizedName == normalized, ct);
        if (character is not null) return character;
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({normalized}, 0))", ct);
        character = await dbContext.OsrsCharacters.SingleOrDefaultAsync(x => x.NormalizedName == normalized, ct);
        if (character is not null) return character;
        character = new OsrsCharacter(Guid.NewGuid(), name.Trim(), normalized, timeProvider.GetUtcNow());
        dbContext.OsrsCharacters.Add(character);
        return character;
    }
    private async Task<string> AdminStateAsync(Guid participantId, CancellationToken ct) => Json(await (from assignment in dbContext.EventParticipantCharacters join character in dbContext.OsrsCharacters on assignment.OsrsCharacterId equals character.Id where assignment.EventParticipantId == participantId && assignment.ReleasedAt == null select new { assignment.SignupQuestionId, assignment.EventRole, character.NormalizedName, assignment.EhbSnapshot }).ToListAsync(ct));
    private static string Json(object? value) => System.Text.Json.JsonSerializer.Serialize(value);
    private static bool HasSerializationConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }) return true;
        return false;
    }

    private static bool ValidateAnswer(SignupQuestion question, string? value, out string? error)
    {
        value = value?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            error = question.Required ? $"'{question.Label}' is required." : null;
            return !question.Required;
        }
        var valid = question.Type switch
        {
            SignupQuestionType.Number => decimal.TryParse(value, out _),
            SignupQuestionType.YesNo => bool.TryParse(value, out _),
            SignupQuestionType.SingleChoice => question.Options?.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(value, StringComparer.Ordinal) == true,
            _ => value.Length <= 4_000
        };
        error = valid ? null : $"'{question.Label}' has an invalid answer.";
        return valid;
    }

    private static string? CanonicalAnswer(SignupQuestion question, string? value)
    {
        var trimmed = value?.Trim();
        return question.Type == SignupQuestionType.YesNo && bool.TryParse(trimmed, out var boolean)
            ? boolean ? "true" : "false"
            : trimmed;
    }

    public async Task<int> IncreaseCapacityAndPromoteAsync(Guid eventId, int newCap, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var bingoEvent = await dbContext.Events
            .FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        if (bingoEvent.DraftLocked)
        {
            throw new InvalidOperationException("The participant cap is locked because the draft has started.");
        }
        bingoEvent.IncreaseParticipantCap(newCap);
        var promoted = await PromoteWithinLockedEventAsync(bingoEvent, null, "system", "capacity increase", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return promoted;
    }

    public async Task<int> PromoteAvailablePlacesAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var bingoEvent = await dbContext.Events
            .FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        var promoted = await PromoteWithinLockedEventAsync(bingoEvent, null, "system", "available place", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return promoted;
    }

    public async Task<ParticipantLifecycleResult> WithdrawAsync(Guid eventId, Guid participantId, Guid? actorAccountId, string actorName, bool byAdmin, string? privateNote = null, CancellationToken cancellationToken = default)
    {
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var bingoEvent = await LockEventAsync(eventId, cancellationToken);
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.EventId == eventId && x.Id == participantId, cancellationToken);
        if (bingoEvent is null || participant is null) return new(false, "The participant could not be found.");
        if (byAdmin && (actorAccountId is null || await AdminAsync(actorAccountId.Value, cancellationToken) is null)) return new(false, "Admin access is required.");
        if (bingoEvent.DraftLocked || bingoEvent.State is not (Domain.Events.EventState.SignupOpen or Domain.Events.EventState.SignupClosed)) return new(false, "Participant lifecycle changes are locked because the draft has started or the event has moved on.");
        if (byAdmin && await dbContext.TeamMemberships.AnyAsync(x => x.EventParticipantId == participantId && x.LeftAt == null, cancellationToken)) return new(false, "This player belongs to a team. Change or remove their roster membership before withdrawing them.");
        if (!byAdmin && (participant.AccountId != actorAccountId || actorAccountId is null)) return new(false, "You cannot withdraw this participant.");
        if (participant.SignupStatus == SignupStatus.Withdrawn) return new(true, null, SignupStatus.Withdrawn, null, false);
        var prior = participant.SignupStatus;
        var now = timeProvider.GetUtcNow();
        var active = await dbContext.EventParticipantCharacters.Where(x => x.EventParticipantId == participantId && x.ReleasedAt == null).ToListAsync(cancellationToken);
        foreach (var assignment in active) assignment.Release(actorAccountId, now);
        participant.Withdraw(now, byAdmin ? "Admin withdrawal" : "Participant withdrawal", byAdmin ? actorAccountId : null);
        AddAudit(actorAccountId, actorName, byAdmin ? "participant.admin_withdrawn" : "participant.withdrawn", participant, bingoEvent.Id, prior.ToString(), SignupStatus.Withdrawn.ToString());
        if (byAdmin && participant.AccountId is { } owner) AddNotification(owner, "participant.withdrawn", "Your signup was withdrawn by an administrator.", Route(bingoEvent), now);
        // The promotion count is a SQL query. Flush the withdrawal first while retaining the enclosing transaction,
        // otherwise PostgreSQL still counts this former confirmed participant as occupying the place.
        if (prior == SignupStatus.Confirmed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await PromoteWithinLockedEventAsync(bingoEvent, actorAccountId, actorName, byAdmin ? "Admin withdrawal" : "participant withdrawal", cancellationToken);
        }
        await dbContext.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
        return new(true, null, SignupStatus.Withdrawn, null, true);
    }

    public async Task<ParticipantLifecycleResult> RejoinAsync(Guid eventId, Guid participantId, Guid accountId, string actorName, CancellationToken cancellationToken = default)
    {
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var bingoEvent = await LockEventAsync(eventId, cancellationToken);
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.EventId == eventId && x.Id == participantId, cancellationToken);
        if (bingoEvent is null || participant is null) return new(false, "The participant could not be found.");
        if (participant.AccountId != accountId) return new(false, "You cannot rejoin this participant.");
        if (bingoEvent.DraftLocked || !bingoEvent.AcceptsSignups(timeProvider.GetUtcNow())) return new(false, "Signup is closed. Contact an Admin if you need to be restored.");
        if (participant.SignupStatus != SignupStatus.Withdrawn) return new(true, null, participant.SignupStatus, null, false);
        var status = await AdmissionStatusAsync(bingoEvent, cancellationToken);
        if (status == SignupStatus.WaitingList && !bingoEvent.WaitingListEnabled) return new(false, "This event is full and does not have a waiting list.");
        var now = timeProvider.GetUtcNow();
        if (!await ReacquireAssignmentsAsync(participant, accountId, now, cancellationToken)) return new(false, "One of your accounts is now assigned to another participant.");
        var next = (await dbContext.EventParticipants.Where(x => x.EventId == eventId).MaxAsync(x => (long?)x.SignupSequence, cancellationToken) ?? 0) + 1;
        participant.Rejoin(status, next, now);
        AddAudit(accountId, actorName, "participant.rejoined", participant, eventId, SignupStatus.Withdrawn.ToString(), status.ToString());
        await dbContext.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
        return new(true, null, status, status == SignupStatus.WaitingList ? await GetWaitingPositionAsync(participantId, eventId, cancellationToken) : null, true);
    }

    public async Task<ParticipantLifecycleResult> RestoreAsync(Guid eventId, Guid participantId, Guid adminAccountId, string adminName, CancellationToken cancellationToken = default)
    {
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var bingoEvent = await LockEventAsync(eventId, cancellationToken);
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.EventId == eventId && x.Id == participantId, cancellationToken);
        if (bingoEvent is null || participant is null) return new(false, "The participant could not be found.");
        if (bingoEvent.DraftLocked || bingoEvent.State is not (Domain.Events.EventState.SignupOpen or Domain.Events.EventState.SignupClosed)) return new(false, "Participant lifecycle changes are locked because the draft has started or the event has moved on.");
        if (participant.SignupStatus != SignupStatus.Withdrawn) return new(true, null, participant.SignupStatus, null, false);
        var status = await AdmissionStatusAsync(bingoEvent, cancellationToken);
        if (status == SignupStatus.WaitingList && !bingoEvent.WaitingListEnabled) return new(false, "This event is full and does not have a waiting list.");
        var now = timeProvider.GetUtcNow();
        if (!await ReacquireAssignmentsAsync(participant, adminAccountId, now, cancellationToken)) return new(false, "One of this participant's accounts is now assigned to another participant.");
        var next = (await dbContext.EventParticipants.Where(x => x.EventId == eventId).MaxAsync(x => (long?)x.SignupSequence, cancellationToken) ?? 0) + 1;
        participant.Rejoin(status, next, now);
        AddAudit(adminAccountId, adminName, "participant.admin_restored", participant, eventId, SignupStatus.Withdrawn.ToString(), status.ToString());
        if (participant.AccountId is { } owner) AddNotification(owner, "participant.restored", "Your signup has been restored by an administrator.", Route(bingoEvent), now);
        await dbContext.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
        return new(true, null, status, status == SignupStatus.WaitingList ? await GetWaitingPositionAsync(participantId, eventId, cancellationToken) : null, true);
    }

    private async Task<int> PromoteWithinLockedEventAsync(Domain.Events.BingoEvent bingoEvent, Guid? actorAccountId, string actorName, string trigger, CancellationToken cancellationToken)
    {
        if (bingoEvent.DraftLocked) return 0;
        var confirmed = await dbContext.EventParticipants.CountAsync(
            participant => participant.EventId == bingoEvent.Id &&
                participant.SignupStatus == SignupStatus.Confirmed,
            cancellationToken);
        var places = Math.Max(0, (bingoEvent.ParticipantCap ?? 0) - confirmed);
        if (places == 0) return 0;
        var waiting = await dbContext.EventParticipants
            .Where(participant => participant.EventId == bingoEvent.Id && participant.SignupStatus == SignupStatus.WaitingList)
            .OrderBy(participant => participant.SignedUpAt)
            .ThenBy(participant => participant.SignupSequence)
            .Take(places)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var participant in waiting)
        {
            participant.Promote(now);
            AddAudit(actorAccountId, actorName, "participant.promoted", participant, bingoEvent.Id, SignupStatus.WaitingList.ToString(), SignupStatus.Confirmed.ToString());
            if (participant.AccountId is { } owner) AddNotification(owner, "participant.promoted", $"Your signup for {bingoEvent.Name} is confirmed.", Route(bingoEvent), now);
            var admins = await dbContext.Accounts.Where(x => x.Active && x.AccountType == AccountType.WebsiteAccount && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin)).Select(x => x.Id).ToListAsync(cancellationToken);
            foreach (var admin in admins) AddNotification(admin, "participant.promoted", $"A participant was promoted for {bingoEvent.Name} ({trigger}).", $"/Admin/Events/Manage/{bingoEvent.Id}", now);
        }
        return waiting.Count;
    }

    private async Task<Domain.Events.BingoEvent?> LockEventAsync(Guid eventId, CancellationToken ct) => await dbContext.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} FOR UPDATE").SingleOrDefaultAsync(ct);
    private async Task<SignupStatus> AdmissionStatusAsync(Domain.Events.BingoEvent bingoEvent, CancellationToken ct) =>
        await dbContext.EventParticipants.CountAsync(x => x.EventId == bingoEvent.Id && x.SignupStatus == SignupStatus.Confirmed, ct) < bingoEvent.ParticipantCap ? SignupStatus.Confirmed : SignupStatus.WaitingList;
    private async Task<bool> ReacquireAssignmentsAsync(EventParticipant participant, Guid? actorId, DateTimeOffset now, CancellationToken ct)
    {
        var released = await dbContext.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt != null).OrderByDescending(x => x.RegistrationOrder).ToListAsync(ct);
        var originals = released.GroupBy(x => x.SignupQuestionId).Select(x => x.First()).ToList();
        var ids = originals.Select(x => x.OsrsCharacterId).ToList();
        if (ids.Count != 0 && await dbContext.EventParticipantCharacters.AnyAsync(x => x.EventId == participant.EventId && ids.Contains(x.OsrsCharacterId) && x.ReleasedAt == null, ct)) return false;
        var order = (await dbContext.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id).MaxAsync(x => (int?)x.RegistrationOrder, ct) ?? -1) + 1;
        foreach (var original in originals) dbContext.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), participant.EventId, participant.Id, original.OsrsCharacterId, order++, now, actorId, original.SignupQuestionId, original.EventRole, original.EhbSnapshot, original.EhbSource, original.EhbFetchedAt));
        return true;
    }
    private void AddAudit(Guid? actorId, string actorName, string action, EventParticipant participant, Guid eventId, string before, string after) => dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), timeProvider.GetUtcNow(), actorId, actorName, action, "participant", participant.Id.ToString(), null, eventId, before, after));
    private void AddNotification(Guid recipientId, string title, string detail, string route, DateTimeOffset now) => dbContext.PersonalNotifications.Add(new PersonalNotification(Guid.NewGuid(), recipientId, title, detail, route, now));
    private static string Route(Domain.Events.BingoEvent bingoEvent) => $"/Events/{Uri.EscapeDataString(bingoEvent.Slug)}/Signup/Confirmation";

    private async Task<int> GetWaitingPositionAsync(Guid participantId, Guid eventId, CancellationToken cancellationToken)
    {
        var waitingIds = await dbContext.EventParticipants.AsNoTracking()
            .Where(participant => participant.EventId == eventId && participant.SignupStatus == SignupStatus.WaitingList)
            .OrderBy(participant => participant.SignedUpAt)
            .ThenBy(participant => participant.SignupSequence)
            .Select(participant => participant.Id)
            .ToListAsync(cancellationToken);
        return waitingIds.IndexOf(participantId) + 1;
    }

    public static string NormalizeAccountName(string value) => value.Trim().ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
