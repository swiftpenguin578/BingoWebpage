using System.Data;
using System.Security.Cryptography;
using System.Text;
using Bingo.Application.Security;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Infrastructure.Signups;

public sealed class SignupService(
    ApplicationDbContext dbContext,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    ISignupLookupTokenService? lookupTokens = null) : ISignupService
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

    public async Task<SignupAdministrationResult> UpdateSignupAdministrationAsync(
        Guid eventId,
        long expectedVersion,
        int newCap,
        bool waitingListEnabled,
        Guid actorAccountId,
        string actorName,
        bool confirmWaitingListDisablement = false,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var bingoEvent = await LockEventAsync(eventId, cancellationToken);
        if (bingoEvent is null) return new(false, "The event could not be found.");
        if (bingoEvent.Version != expectedVersion) return new(false, "This event changed while you were editing it. Review the latest values and try again.");
        if (bingoEvent.DraftLocked || bingoEvent.State is not (Domain.Events.EventState.Draft or Domain.Events.EventState.SignupOpen or Domain.Events.EventState.SignupClosed))
            return new(false, "Signup administration is read-only after the draft starts or the event has moved on.");
        var waitingCount = await SignupParticipants(eventId).CountAsync(item => item.SignupStatus == SignupStatus.WaitingList, cancellationToken);
        var confirmedCount = await SignupParticipants(eventId).CountAsync(item => item.SignupStatus == SignupStatus.Confirmed, cancellationToken);
        var effectiveCap = newCap;
        if (!waitingListEnabled && waitingCount > 0)
        {
            effectiveCap = Math.Max(newCap, confirmedCount + waitingCount);
            if (!confirmWaitingListDisablement)
            {
                var capacityNote = effectiveCap == newCap ? $"Capacity remains {newCap}" : $"Capacity will increase to {effectiveCap}";
                return new(false, $"Disabling the waiting list will promote all {waitingCount} queued participant(s) in signup order. {capacityNote} so no queued participant is lost. Confirm this consequence before saving.");
            }
        }

        var before = new { bingoEvent.ParticipantCap, bingoEvent.WaitingListEnabled };
        try
        {
            bingoEvent.IncreaseParticipantCap(effectiveCap);
            bingoEvent.ConfigureSignup(waitingListEnabled, bingoEvent.RequireSignupCode, bingoEvent.SignupCodeHash);
        }
        catch (ArgumentOutOfRangeException) { return new(false, "Maximum players must be at least 1."); }
        catch (InvalidOperationException ex) { return new(false, ex.Message); }

        var promoted = await PromoteWithinLockedEventAsync(bingoEvent, actorAccountId, actorName, "signup administration", cancellationToken);
        var after = new { bingoEvent.ParticipantCap, bingoEvent.WaitingListEnabled };
        dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), timeProvider.GetUtcNow(), actorAccountId, actorName, "event.signup_administration_updated", "event",
            eventId.ToString(), System.Text.Json.JsonSerializer.Serialize(new { requestedCapacity = newCap, effectiveCapacity = effectiveCap, waitingListEnabled, promoted }), eventId,
            System.Text.Json.JsonSerializer.Serialize(before), System.Text.Json.JsonSerializer.Serialize(after)));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, null, promoted, effectiveCap);
    }
    public async Task<SignupResult> SignUpAuthenticatedAsync(AuthenticatedSignupRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var bingoEvent = await dbContext.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {request.EventId} AND hidden_at IS NULL FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
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
            var confirmed = await SignupParticipants(request.EventId).CountAsync(x => x.SignupStatus == SignupStatus.Confirmed, cancellationToken);
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
            var source = EhbSource.Manual;
            DateTimeOffset? fetchedAt = null;
            if (role == EventCharacterRole.Playing && selectedAnswer.Ehb is { } submittedEhb && selected is not null && lookupTokens?.TryValidate(selectedAnswer.WiseOldManLookupToken, selected.Character.NormalizedName, submittedEhb, now, out var trustedFetchedAt) == true)
            {
                source = EhbSource.WiseOldMan;
                fetchedAt = trustedFetchedAt;
            }
            if (role == EventCharacterRole.Playing && selected is not null)
                selected.Link.UpdatePreferences(selected.Link.PersonalLabel, selected.Link.Position, selected.Link.Preferred, selectedAnswer.Ehb, now);
            if (sameAssignment)
            {
                if (role == EventCharacterRole.Playing) current!.UpdatePlayingEhb(selectedAnswer.Ehb!.Value, source, fetchedAt);
            }
            else
            {
                current?.Release(request.AccountId, now);
                dbContext.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), request.EventId, participant.Id, selectedAnswer.OsrsCharacterId, order++, now, request.AccountId, question.Id, role, selectedAnswer.Ehb, role == EventCharacterRole.Playing ? source : null, role == EventCharacterRole.Playing ? fetchedAt : null));
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
        if (request.DestinationOwnerAccountId is null)
            return new(false, "Select an active website account to transfer ownership.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
        if (bingoEvent is null) return new(false, "The event could not be found.");
        var actor = await AdminAsync(request.ActorAccountId, cancellationToken);
        if (actor is null) return new(false, "Admin access is required.");
        if (!CanAdministerParticipants(bingoEvent)) return new(false, "Participant administration is read-only after the draft starts.");
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.EventId == request.EventId && x.Id == request.ParticipantId, cancellationToken);
        if (participant is null) return new(false, "The participant could not be found.");
        if (request.ExpectedOwnerAccountId != participant.AccountId) return new(false, "This participant ownership changed elsewhere. Reload before transferring it.");
        var destination = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == request.DestinationOwnerAccountId && x.Active && x.AccountType == AccountType.WebsiteAccount, cancellationToken);
        if (destination is null) return new(false, "The destination must be an active website account.");
        if (participant.AccountId == destination.Id) { await transaction.CommitAsync(cancellationToken); return new(true, null); }
        if (await dbContext.EventParticipants.AnyAsync(x => x.EventId == request.EventId && x.AccountId == destination.Id && x.Id != participant.Id, cancellationToken)) return new(false, "That account already owns a participant in this event.");
        var previousOwnerId = participant.AccountId;
        participant.TransferOwner(destination);
        var now = timeProvider.GetUtcNow();
        dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, request.ActorAccountId, request.ActorName, "participant.ownership_transferred", "participant", participant.Id.ToString(), "Participant ownership transferred.", request.EventId,
            $"{{\"accountId\":{Json(previousOwnerId)}}}", $"{{\"accountId\":{Json(destination.Id)},\"username\":{Json(destination.LoginName)}}}"));
        var route = $"/Events/{Uri.EscapeDataString(bingoEvent.Slug)}/Signup/Confirmation?participantId={participant.Id}";
        if (previousOwnerId is { } oldOwner && oldOwner != destination.Id)
            dbContext.PersonalNotifications.Add(new PersonalNotification(Guid.NewGuid(), oldOwner, "participant.ownership_transferred", "Your event participant access changed.", route, now, bingoEvent.Id));
        if (previousOwnerId != destination.Id)
            dbContext.PersonalNotifications.Add(new PersonalNotification(Guid.NewGuid(), destination.Id, "participant.ownership_transferred", "Your event participant access changed.", route, now, bingoEvent.Id));
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
            var confirmed = await SignupParticipants(request.EventId).CountAsync(x => x.SignupStatus == SignupStatus.Confirmed, ct);
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
            .FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} AND hidden_at IS NULL FOR UPDATE")
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
            .FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} AND hidden_at IS NULL FOR UPDATE")
            .SingleAsync(cancellationToken);
        var promoted = await PromoteWithinLockedEventAsync(bingoEvent, null, "system", "available place", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return promoted;
    }

    public Task<ParticipantLifecycleResult> WithdrawAsync(Guid eventId, Guid participantId, Guid? actorAccountId, string actorName, bool byAdmin, string? privateNote = null, CancellationToken cancellationToken = default)
        => WithdrawAsync(eventId, participantId, actorAccountId, actorName, byAdmin, privateNote, null, cancellationToken);

    public async Task<ParticipantLifecycleResult> WithdrawAsync(Guid eventId, Guid participantId, Guid? actorAccountId, string actorName, bool byAdmin, string? privateNote, long? expectedMembershipVersion, CancellationToken cancellationToken = default)
    {
        if (byAdmin && actorAccountId is { } liveActor && await dbContext.Events.AsNoTracking().AnyAsync(x => x.Id == eventId && x.HiddenAt == null && x.State == Domain.Events.EventState.Live, cancellationToken))
        {
            var live = await WithdrawLiveAsync(new LiveWithdrawalRequest(eventId, participantId, liveActor, actorName, expectedMembershipVersion), cancellationToken);
            return new(live.Succeeded, live.Error, SignupStatus.Withdrawn, null, live.Changed);
        }
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
        if (byAdmin && participant.AccountId is { } owner) AddNotification(owner, "participant.withdrawn", "Your signup was withdrawn by an administrator.", Route(bingoEvent), now, bingoEvent.Id);
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

    public async Task<LiveParticipantResult> WithdrawLiveAsync(LiveWithdrawalRequest request, CancellationToken cancellationToken = default)
    {
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
            var admin = await AdminAsync(request.ActorAccountId, cancellationToken);
            if (bingoEvent is null) return new(false, "The event could not be found.");
            if (admin is null) return new(false, "Admin access is required.");
            if (bingoEvent.State != Domain.Events.EventState.Live || !bingoEvent.DraftLocked)
                return new(false, "Live withdrawal is available only while the event is live.");

            var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.EventId == request.EventId && x.Id == request.ParticipantId, cancellationToken);
            if (participant is null) return new(false, "The participant could not be found.");
            if (participant.SignupStatus == SignupStatus.Withdrawn)
            {
                await tx.CommitAsync(cancellationToken);
                return new(true, Changed: false, ParticipantId: participant.Id);
            }
            if (participant.SignupStatus != SignupStatus.Confirmed)
                return new(false, "Only a confirmed live participant can be withdrawn.");

            var membership = await dbContext.TeamMemberships
                .SingleOrDefaultAsync(x => x.EventParticipantId == participant.Id && x.LeftAt == null, cancellationToken);
            if (membership is null) return new(false, "The participant has no current team membership.");
            if (request.ExpectedMembershipVersion is { } expected && membership.Version != expected)
                return new(false, "This team membership changed elsewhere. Reload before withdrawing it.");
            var team = await dbContext.Teams.SingleOrDefaultAsync(x => x.Id == membership.TeamId && x.EventId == request.EventId && x.Active, cancellationToken);
            if (team is null) return new(false, "The participant's current team is no longer available.");

            var eligibilityEndsAt = NextWholeUtcMinute(now);
            var formerRole = membership.Role;
            participant.Withdraw(now, "Admin withdrawal", request.ActorAccountId, eligibilityEndsAt);
            if (formerRole is TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain)
            {
                membership.ChangeRole(TeamMembershipRole.Participant);
                dbContext.TeamMembershipRoleTransitions.Add(new TeamMembershipRoleTransition(
                    Guid.NewGuid(), membership.Id, formerRole, TeamMembershipRole.Participant, request.ActorAccountId, now));
            }
            membership.Leave(now, "Live Admin withdrawal");
            dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, request.ActorAccountId, request.ActorName,
                "participant.live_withdrawn", "participant", participant.Id.ToString(), null, request.EventId,
                Json(new { status = SignupStatus.Confirmed.ToString(), membershipId = membership.Id, role = formerRole.ToString() }),
                Json(new { status = SignupStatus.Withdrawn.ToString(), eligibilityEndsAt, membershipEndedAt = now, vacancy = true })));

            var participantName = await dbContext.PrimaryCharacters().Where(x => x.ParticipantId == participant.Id).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken) ?? "Participant";
            var recipients = await LiveLeadershipAndAdminRecipientsAsync(request.EventId, membership.TeamId, request.ParticipantId, cancellationToken);
            var detail = $"{bingoEvent.Name}: {participantName} withdrew from {team.Name}. The vacancy is open for Admin follow-up.";
            foreach (var recipient in recipients)
                await AddNotificationOnceAsync("live-withdrawal", membership.Id, recipient, "participant.live_withdrawn", detail, $"/Admin/Events/Participant/{request.EventId}/Participants/{participant.Id}", now, request.EventId, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(CancellationToken.None);
            return new(true, Changed: true, MembershipId: membership.Id, ParticipantId: participant.Id, EffectiveAtUtc: eligibilityEndsAt);
        }
        catch (Exception exception) when (IsExpectedConflict(exception))
        {
            await tx.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return new(false, "Another administrator changed this participant or vacancy first. Reload and try again.");
        }
    }

    public async Task<LiveParticipantResult> ReplaceVacancyAsync(LiveReplacementRequest request, CancellationToken cancellationToken = default)
    {
        if ((request.WaitingParticipantId is null) == (request.InternalParticipant is null))
            return new(false, "Choose one available waiting-list participant or provide an internal replacement.");
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
            var admin = await AdminAsync(request.ActorAccountId, cancellationToken);
            if (bingoEvent is null) return new(false, "The event could not be found.");
            if (admin is null) return new(false, "Admin access is required.");
            if (bingoEvent.State != Domain.Events.EventState.Live || !bingoEvent.DraftLocked)
                return new(false, "Replacement is available only while the event is live.");

            var vacancy = await dbContext.TeamMemberships
                .FromSqlInterpolated($"SELECT * FROM team_memberships WHERE id = {request.EndedMembershipId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (vacancy is null || vacancy.LeftAt is null)
                return new(false, "The selected vacancy is not available.");
            if (request.ExpectedVacancyVersion is { } expected && vacancy.Version != expected)
                return new(false, "This vacancy changed elsewhere. Reload before filling it.");
            if (await dbContext.TeamMemberships.AnyAsync(x => x.ReplacesMembershipId == vacancy.Id, cancellationToken))
                return new(false, "This vacancy has already been filled.");
            var team = await dbContext.Teams.SingleOrDefaultAsync(x => x.Id == vacancy.TeamId && x.EventId == request.EventId && x.Active, cancellationToken);
            var departed = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.Id == vacancy.EventParticipantId && x.EventId == request.EventId, cancellationToken);
            if (team is null || departed is null || departed.SignupStatus != SignupStatus.Withdrawn)
                return new(false, "The selected vacancy is not a valid live withdrawal.");

            EventParticipant replacement;
            bool waitingReplacement = request.WaitingParticipantId is not null;
            if (waitingReplacement)
            {
                replacement = await dbContext.EventParticipants
                    .FromSqlInterpolated($"SELECT * FROM event_participants WHERE id = {request.WaitingParticipantId!.Value} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("The waiting-list participant could not be found.");
                var waitingError = await ValidateWaitingReplacementAsync(replacement, request.EventId, bingoEvent, cancellationToken);
                if (waitingError is not null) return new(false, waitingError);
                replacement.Promote(now);
            }
            else
            {
                var created = await CreateInternalReplacementAsync(request.InternalParticipant!, request.EventId, bingoEvent, now, cancellationToken);
                if (created.Error is not null) return new(false, created.Error);
                replacement = created.Participant!;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var primary = await dbContext.EventParticipantCharacters
                .Where(x => x.EventParticipantId == replacement.Id && x.EventId == request.EventId && x.ReleasedAt == null && x.EventRole == EventCharacterRole.Playing)
                .OrderBy(x => x.RegistrationOrder).FirstOrDefaultAsync(cancellationToken);
            if (primary is null) return new(false, "The replacement needs an active Playing account.");
            if (await dbContext.TeamMemberships.AnyAsync(x => x.EventParticipantId == replacement.Id && x.LeftAt == null, cancellationToken))
                return new(false, "The selected replacement is already on a team.");

            var membership = new TeamMembership(Guid.NewGuid(), team.Id, replacement.Id, TeamMembershipRole.Participant, now, null, "Live roster replacement");
            membership.SetSource(TeamMembershipSource.Replacement, vacancy.Id);
            dbContext.TeamMemberships.Add(membership);
            var effectiveAt = NextWholeUtcMinute(now);
            dbContext.EventParticipantCharacterSwaps.Add(new EventParticipantCharacterSwap(
                Guid.NewGuid(), request.EventId, replacement.Id, null, primary.OsrsCharacterId,
                effectiveAt, now, request.ActorAccountId, "Live roster replacement activation"));

            WaitingListPromotionFollowUp? followUp = null;
            if (waitingReplacement)
            {
                followUp = new WaitingListPromotionFollowUp(Guid.NewGuid(), request.EventId, vacancy.Id, membership.Id, replacement.Id, now);
                dbContext.WaitingListPromotionFollowUps.Add(followUp);
            }

            var departedName = await dbContext.PrimaryCharacters().Where(x => x.ParticipantId == departed.Id).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken) ?? "Participant";
            var replacementName = await dbContext.PrimaryCharacters().Where(x => x.ParticipantId == replacement.Id).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken) ?? "Participant";
            dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, request.ActorAccountId, request.ActorName,
                "participant.live_replaced", "membership", membership.Id.ToString(), null, request.EventId,
                Json(new { vacancyMembershipId = vacancy.Id, departedParticipantId = departed.Id }),
                Json(new { replacementParticipantId = replacement.Id, replacementName, effectiveAt, source = waitingReplacement ? "WaitingList" : "Internal" })));

            var recipients = await LiveLeadershipAndAdminRecipientsAsync(request.EventId, team.Id, null, cancellationToken);
            if (replacement.AccountId is { } replacementAccount) recipients.Add(replacementAccount);
            var detail = $"{bingoEvent.Name}: {replacementName} joined {team.Name} as a replacement for {departedName}.";
            foreach (var recipient in recipients.Distinct())
                await AddNotificationOnceAsync("live-replacement", membership.Id, recipient, "participant.live_replaced", detail, $"/Events/{Uri.EscapeDataString(bingoEvent.Slug)}/Teams", now, bingoEvent.Id, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(CancellationToken.None);
            return new(true, Changed: true, MembershipId: membership.Id, ParticipantId: replacement.Id, EffectiveAtUtc: effectiveAt, FollowUpId: followUp?.Id);
        }
        catch (Exception exception) when (IsExpectedConflict(exception))
        {
            await tx.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return new(false, "Another administrator filled this vacancy or changed the replacement first. Reload and try again.");
        }
    }

    public async Task<PromotionFollowUpResult> CompletePromotionFollowUpAsync(Guid eventId, Guid followUpId, Guid adminAccountId, string adminName, CancellationToken cancellationToken = default)
    {
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var admin = await AdminAsync(adminAccountId, cancellationToken);
        if (admin is null) return new(false, "Admin access is required.");
        var followUp = await dbContext.WaitingListPromotionFollowUps
            .FromSqlInterpolated($"SELECT * FROM waiting_list_promotion_follow_ups WHERE id = {followUpId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (followUp is null || followUp.EventId != eventId) return new(false, "The promotion follow-up could not be found.");
        if (followUp.CompletedAt is not null) { await tx.CommitAsync(cancellationToken); return new(true, Changed: false); }
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        followUp.MarkComplete(adminAccountId, now);
        dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, adminAccountId, adminName, "participant.promotion_follow_up_completed", "promotion_follow_up", followUp.Id.ToString(), null, eventId, null, Json(new { followUp.CompletedByAccountId, followUp.CompletedAt })));
        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(CancellationToken.None);
        return new(true, Changed: true);
    }

    private async Task<string?> ValidateWaitingReplacementAsync(EventParticipant replacement, Guid eventId, Domain.Events.BingoEvent bingoEvent, CancellationToken ct)
    {
        if (replacement.EventId != eventId || replacement.SignupStatus != SignupStatus.WaitingList)
            return "Choose a participant who is currently on this event's waiting list.";
        if (!bingoEvent.WaitingListEnabled) return "The event waiting list is not enabled.";
        if (replacement.AccountId is { } accountId && await dbContext.EventParticipants.AnyAsync(x => x.EventId == eventId && x.AccountId == accountId && x.Id != replacement.Id, ct))
            return "That website account already owns another participant in this event.";
        var assignments = await dbContext.EventParticipantCharacters
            .Where(x => x.EventParticipantId == replacement.Id && x.EventId == eventId && x.ReleasedAt == null)
            .OrderBy(x => x.RegistrationOrder).ToListAsync(ct);
        if (assignments.Count == 0 || assignments.All(x => x.EventRole != EventCharacterRole.Playing))
            return "The waiting-list participant has no frozen Playing account.";
        var characterIds = assignments.Select(x => x.OsrsCharacterId).ToList();
        if (await dbContext.EventParticipantCharacters.AnyAsync(x => x.EventId == eventId && x.EventParticipantId != replacement.Id && x.ReleasedAt == null && characterIds.Contains(x.OsrsCharacterId), ct))
            return "One of the waiting-list participant's frozen accounts is already reserved by another participant.";
        return null;
    }

    private async Task<(EventParticipant? Participant, string? Error)> CreateInternalReplacementAsync(
        AdminParticipantChangeRequest request,
        Guid eventId,
        Domain.Events.BingoEvent bingoEvent,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (request.ParticipantId is not null || request.EventId != eventId)
            return (null, "The internal replacement request is invalid.");
        var form = await dbContext.SignupForms.SingleOrDefaultAsync(x => x.EventId == eventId, ct);
        if (form is null) return (null, "This event has no signup form for internal replacement validation.");
        var questions = await dbContext.SignupQuestions.Where(x => x.SignupFormId == form.Id && x.Active).OrderBy(x => x.Position).ToListAsync(ct);
        Account? owner = null;
        if (request.OwnerAccountId is { } ownerId)
        {
            owner = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == ownerId && x.Active && x.AccountType == AccountType.WebsiteAccount, ct);
            if (owner is null) return (null, "The selected owner must be an active website account.");
            if (await dbContext.EventParticipants.AnyAsync(x => x.EventId == eventId && x.AccountId == ownerId, ct))
                return (null, "That website account already owns a participant in this event.");
        }

        var requestedCharacters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var question in questions)
        {
            if (question.Type == SignupQuestionType.Account)
            {
                request.AccountAnswers.TryGetValue(question.Id, out var answer);
                var name = answer?.CharacterName?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    if (question.Required) return (null, $"'{question.Label}' is required.");
                    continue;
                }
                if (!requestedCharacters.Add(NormalizeAccountName(name))) return (null, "Choose each account only once.");
                if (question.AccountAnswerRole == EventCharacterRole.Playing && (answer?.Ehb is null || answer.Ehb < 0))
                    return (null, $"'{question.Label}' requires EHB.");
            }
            else if (!ValidateAnswer(question, request.Answers.TryGetValue(question.Id, out var value) ? value : null, out var error))
                return (null, error);
        }

        var sequence = (await dbContext.EventParticipants.Where(x => x.EventId == eventId).MaxAsync(x => (long?)x.SignupSequence, ct) ?? 0) + 1;
        var participant = new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, sequence, now, SignupSource.AdminCreated);
        if (owner is not null) participant.AssignOwner(owner);
        dbContext.EventParticipants.Add(participant);
        var order = 0;
        foreach (var question in questions.Where(x => x.Type == SignupQuestionType.Account))
        {
            request.AccountAnswers.TryGetValue(question.Id, out var answer);
            var name = answer?.CharacterName?.Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var character = await ResolveCharacterAsync(name, NormalizeAccountName(name), ct);
            if (await dbContext.EventParticipantCharacters.AnyAsync(x => x.EventId == eventId && x.OsrsCharacterId == character.Id && x.ReleasedAt == null, ct))
                return (null, "That internal replacement account is already reserved for this event.");
            var role = question.AccountAnswerRole!.Value;
            dbContext.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), eventId, participant.Id, character.Id, order++, now, request.ActorAccountId, question.Id, role, answer?.Ehb, role == EventCharacterRole.Playing ? EhbSource.AdminCorrection : null, null));
            dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, question.Id, question.Label, string.Empty, character.Id));
        }
        foreach (var question in questions.Where(x => x.Type != SignupQuestionType.Account && x.SystemField != SignupSystemField.CaptainVolunteer))
        {
            var value = request.Answers.TryGetValue(question.Id, out var answer) ? CanonicalAnswer(question, answer) : null;
            if (!string.IsNullOrWhiteSpace(value)) dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, question.Id, question.Label, value));
        }
        form.RecordAcceptedResponse(now);
        return (participant, null);
    }

    private async Task<List<Guid>> LiveLeadershipAndAdminRecipientsAsync(Guid eventId, Guid teamId, Guid? excludedParticipantId, CancellationToken ct)
    {
        var admins = await dbContext.Accounts.AsNoTracking()
            .Where(x => x.Active && x.AccountType == AccountType.WebsiteAccount && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin))
            .Select(x => x.Id).ToListAsync(ct);
        var leaders = await (from membership in dbContext.TeamMemberships.AsNoTracking()
                             join participant in dbContext.EventParticipants.AsNoTracking() on membership.EventParticipantId equals participant.Id
                             where membership.TeamId == teamId && membership.LeftAt == null && participant.EventId == eventId && participant.Id != excludedParticipantId && participant.AccountId != null &&
                                   (membership.Role == TeamMembershipRole.Captain || membership.Role == TeamMembershipRole.CoCaptain)
                             select participant.AccountId!.Value).ToListAsync(ct);
        return admins.Concat(leaders).Distinct().ToList();
    }

    private async Task AddNotificationOnceAsync(string purpose, Guid targetId, Guid recipientId, string title, string detail, string route, DateTimeOffset now, Guid eventId, CancellationToken ct)
    {
        var id = DeterministicNotificationId(purpose, targetId, recipientId);
        if (!await dbContext.PersonalNotifications.AnyAsync(x => x.Id == id, ct))
            dbContext.PersonalNotifications.Add(new PersonalNotification(id, recipientId, title, detail, route, now, eventId));
    }

    private static Guid DeterministicNotificationId(string purpose, Guid targetId, Guid recipientId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}:{targetId:N}:{recipientId:N}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static DateTimeOffset NextWholeUtcMinute(DateTimeOffset instantUtc)
    {
        var instant = instantUtc.ToUniversalTime();
        return new DateTimeOffset(instant.Year, instant.Month, instant.Day, instant.Hour, instant.Minute, 0, TimeSpan.Zero).AddMinutes(1);
    }

    private static bool IsExpectedConflict(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException) return true;
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.UniqueViolation }) return true;
        return false;
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
        if (participant.AccountId is { } owner) AddNotification(owner, "participant.restored", "Your signup has been restored by an administrator.", Route(bingoEvent), now, bingoEvent.Id);
        await dbContext.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
        return new(true, null, status, status == SignupStatus.WaitingList ? await GetWaitingPositionAsync(participantId, eventId, cancellationToken) : null, true);
    }

    private async Task<int> PromoteWithinLockedEventAsync(Domain.Events.BingoEvent bingoEvent, Guid? actorAccountId, string actorName, string trigger, CancellationToken cancellationToken)
    {
        if (bingoEvent.DraftLocked) return 0;
        var confirmed = await SignupParticipants(bingoEvent.Id).CountAsync(participant => participant.SignupStatus == SignupStatus.Confirmed, cancellationToken);
        var places = Math.Max(0, (bingoEvent.ParticipantCap ?? 0) - confirmed);
        if (places == 0) return 0;
        var waiting = await SignupParticipants(bingoEvent.Id)
            .Where(participant => participant.SignupStatus == SignupStatus.WaitingList)
            .OrderBy(participant => participant.SignedUpAt)
            .ThenBy(participant => participant.SignupSequence)
            .Take(places)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        foreach (var participant in waiting)
        {
            participant.Promote(now);
            AddAudit(actorAccountId, actorName, "participant.promoted", participant, bingoEvent.Id, SignupStatus.WaitingList.ToString(), SignupStatus.Confirmed.ToString());
            if (participant.AccountId is { } owner) AddNotification(owner, "participant.promoted", $"Your signup for {bingoEvent.Name} is confirmed.", Route(bingoEvent), now, bingoEvent.Id);
            var admins = await dbContext.Accounts.Where(x => x.Active && x.AccountType == AccountType.WebsiteAccount && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin)).Select(x => x.Id).ToListAsync(cancellationToken);
            foreach (var admin in admins) AddNotification(admin, "participant.promoted", $"A participant was promoted for {bingoEvent.Name} ({trigger}).", $"/Admin/Events/Manage/{bingoEvent.Id}", now, bingoEvent.Id);
        }
        return waiting.Count;
    }

    private IQueryable<EventParticipant> SignupParticipants(Guid eventId) =>
        dbContext.EventParticipants.Where(participant => participant.EventId == eventId &&
            !dbContext.TeamMemberships.Any(membership => membership.EventParticipantId == participant.Id && membership.LeftAt == null &&
                dbContext.Teams.Any(team => team.Id == membership.TeamId && team.EventId == eventId && team.Active && team.FormationType == TeamFormationType.Preformed)));

    private async Task<Domain.Events.BingoEvent?> LockEventAsync(Guid eventId, CancellationToken ct) => await dbContext.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} AND hidden_at IS NULL FOR UPDATE").SingleOrDefaultAsync(ct);
    private async Task<SignupStatus> AdmissionStatusAsync(Domain.Events.BingoEvent bingoEvent, CancellationToken ct) =>
        await SignupParticipants(bingoEvent.Id).CountAsync(x => x.SignupStatus == SignupStatus.Confirmed, ct) < bingoEvent.ParticipantCap ? SignupStatus.Confirmed : SignupStatus.WaitingList;
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
    private void AddNotification(Guid recipientId, string title, string detail, string route, DateTimeOffset now, Guid eventId) => dbContext.PersonalNotifications.Add(new PersonalNotification(Guid.NewGuid(), recipientId, title, detail, route, now, eventId));
    private static string Route(Domain.Events.BingoEvent bingoEvent) => $"/Events/{Uri.EscapeDataString(bingoEvent.Slug)}/Signup/Confirmation";

    private async Task<int> GetWaitingPositionAsync(Guid participantId, Guid eventId, CancellationToken cancellationToken)
    {
        var waitingIds = await SignupParticipants(eventId).AsNoTracking()
            .Where(participant => participant.SignupStatus == SignupStatus.WaitingList)
            .OrderBy(participant => participant.SignedUpAt)
            .ThenBy(participant => participant.SignupSequence)
            .Select(participant => participant.Id)
            .ToListAsync(cancellationToken);
        return waitingIds.IndexOf(participantId) + 1;
    }

    public static string NormalizeAccountName(string value) => value.Trim().ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
