using System.Data;
using System.Security.Cryptography;
using System.Text;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
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

public sealed partial class SignupService(
    ApplicationDbContext dbContext,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    ISignupLookupTokenService? lookupTokens = null,
    IWiseOldManAccountValidation? accountValidation = null,
    IEventCompetitionManagementService? competitionManagement = null) : ISignupService
{
    // Retained only so older callers fail closed. In particular, this overload
    // must never manufacture a confirmed mutation request.
    public Task<SignupAdministrationResult> DeleteQuestionAsync(Guid eventId, Guid questionId, Guid actorAccountId, string actorName, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SignupAdministrationResult(false, "Signup question deletion requires a current confirmation impact."));

    public async Task<SignupAdministrationResult> ApplyQuestionMutationAsync(SignupQuestionMutationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.EventId == Guid.Empty || request.QuestionId == Guid.Empty || request.ActorAccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorName))
            return new(false, "A current question identity and administrator are required.");
        if (request.Operation is not (SignupQuestionMutationKind.DeleteQuestion or SignupQuestionMutationKind.DisableCoCaptain))
            return new(false, "That signup question mutation is not available.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
        if (bingoEvent is null) return new(false, "The event could not be found.");
        if (await AdminAsync(request.ActorAccountId, cancellationToken) is null) return new(false, "Admin access is required.");
        if (bingoEvent.DraftLocked || bingoEvent.State is not (EventState.Draft or EventState.SignupOpen or EventState.SignupClosed))
            return new(false, "Signup questions can only be changed before the draft starts.");

        var question = await dbContext.SignupQuestions.SingleOrDefaultAsync(
            item => item.EventId == request.EventId && item.Id == request.QuestionId,
            cancellationToken);
        if (question is null) return new(false, "That signup question could not be found.");

        var isCoCaptain = request.Operation == SignupQuestionMutationKind.DisableCoCaptain;
        if (isCoCaptain && question.SystemField != SignupSystemField.CoCaptainName)
            return new(false, "That is not the standard co-captain field.");
        if (!isCoCaptain && question.SystemField != SignupSystemField.None)
            return new(false, "Standard signup fields cannot be removed.");
        if (!question.Active)
            return new(false, isCoCaptain ? "The co-captain field is already disabled." : "That signup question cannot be removed.");

        var impact = await CurrentQuestionImpactAsync(request.EventId, question, cancellationToken);
        if (isCoCaptain && impact.EventRegistrationReleaseCount != 0)
            return new(false, "The co-captain field has unexpected event registrations and cannot be disabled.", Impact: impact);
        var hasExpectedImpact = request.ExpectedAnswerCount >= 0
            && request.ExpectedEventRegistrationReleaseCount >= 0
            && request.ExpectedQuestionVersion >= 0;
        if (!request.Confirmed || !hasExpectedImpact || request.ExpectedAnswerCount != impact.AnswerCount
            || request.ExpectedEventRegistrationReleaseCount != impact.EventRegistrationReleaseCount
            || request.ExpectedQuestionVersion != impact.QuestionVersion)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(false, ConfirmationMessage(impact), Impact: impact, RequiresConfirmation: true);
        }

        var form = await dbContext.SignupForms.SingleOrDefaultAsync(
            item => item.Id == question.SignupFormId && item.EventId == request.EventId,
            cancellationToken);
        if (form is null) return new(false, "This event has no signup form.");

        var answers = await dbContext.SignupAnswers
            .Where(item => item.SignupQuestionId == request.QuestionId)
            .ToListAsync(cancellationToken);
        var assignments = await dbContext.EventParticipantCharacters
            .Where(item => item.EventId == request.EventId && item.SignupQuestionId == request.QuestionId && item.ReleasedAt == null)
            .ToListAsync(cancellationToken);
        var participantIds = answers.Select(item => item.EventParticipantId)
            .Concat(assignments.Select(item => item.EventParticipantId))
            .Distinct()
            .ToList();
        var participants = await dbContext.EventParticipants
            .Where(item => participantIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var before = QuestionAuditSnapshot(question);
        var now = timeProvider.GetUtcNow();
        try
        {
            if (isCoCaptain)
                question.Deactivate(request.ActorAccountId, now, "disabled");
            else
                question.Delete(request.ActorAccountId, now);

            dbContext.SignupAnswers.RemoveRange(answers);
            foreach (var assignment in assignments) assignment.Release(request.ActorAccountId, now);
            foreach (var participant in participants) participant.AdvanceResponseVersion();
            dbContext.Entry(form).Property(item => item.Version).IsModified = true;
            var action = isCoCaptain ? "signup_cocaptain.disabled" : "signup_question.deleted";
            dbContext.AuditEntries.Add(new AuditEntry(
                Guid.NewGuid(),
                now,
                request.ActorAccountId,
                request.ActorName,
                action,
                "signup_question",
                request.QuestionId.ToString(),
                Json(new
                {
                    operation = request.Operation.ToString(),
                    answersRemoved = answers.Count,
                    eventRegistrationsReleased = assignments.Count,
                    expected = new
                    {
                        request.ExpectedAnswerCount,
                        request.ExpectedEventRegistrationReleaseCount,
                        request.ExpectedQuestionVersion
                    }
                }),
                request.EventId,
                Json(before),
                Json(QuestionAuditSnapshot(question))));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "The signup question change could not be saved. Please reload and try again.");
        }
    }

    public async Task<SignupAdministrationResult> EnableCoCaptainAsync(Guid eventId, Guid questionId, Guid actorAccountId, string actorName, int? expectedFormVersion = null, CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty || questionId == Guid.Empty || actorAccountId == Guid.Empty || string.IsNullOrWhiteSpace(actorName))
            return new(false, "A current question identity and administrator are required.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var bingoEvent = await LockEventAsync(eventId, cancellationToken);
        if (bingoEvent is null) return new(false, "The event could not be found.");
        if (await AdminAsync(actorAccountId, cancellationToken) is null) return new(false, "Admin access is required.");
        if (bingoEvent.DraftLocked || bingoEvent.State is not (EventState.Draft or EventState.SignupOpen or EventState.SignupClosed))
            return new(false, "Signup questions can only be changed before the draft starts.");
        var question = await dbContext.SignupQuestions.SingleOrDefaultAsync(
            item => item.EventId == eventId && item.Id == questionId && item.SystemField == SignupSystemField.CoCaptainName,
            cancellationToken);
        if (question is null) return new(false, "The standard co-captain field could not be found.");
        var form = await dbContext.SignupForms.SingleOrDefaultAsync(item => item.Id == question.SignupFormId && item.EventId == eventId, cancellationToken);
        if (form is null) return new(false, "This event has no signup form.");
        if (expectedFormVersion is null || expectedFormVersion != form.Version)
            return new(false, "This signup form changed while you were editing it. Review the latest values and try again.", FormVersion: form.Version);
        if (question.Active)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(true, FormVersion: form.Version);
        }

        var before = QuestionAuditSnapshot(question);
        try
        {
            question.EnableCoCaptain();
            dbContext.Entry(form).Property(item => item.Version).IsModified = true;
            dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), timeProvider.GetUtcNow(), actorAccountId, actorName,
                "signup_cocaptain.enabled", "signup_question", questionId.ToString(), "Co-captain field enabled.", eventId,
                Json(before), Json(QuestionAuditSnapshot(question))));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, FormVersion: form.Version);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "The co-captain field could not be enabled. Please reload and try again.");
        }
    }

    public async Task<ParticipantPaymentResult> SetPaymentAsync(Guid eventId, Guid participantId, Guid? actorAccountId, string actorName, PaymentStatus payment, CancellationToken cancellationToken = default)
    {
        if (payment is not (PaymentStatus.Unpaid or PaymentStatus.Paid)) return new(false, "Choose Paid or Unpaid.");
        if (actorAccountId is null) return new(false, "Admin access is required.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var bingoEvent = await LockEventAsync(eventId, cancellationToken);
        if (bingoEvent is null) return new(false, "The event could not be found.");
        var actor = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == actorAccountId && x.Active && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin), cancellationToken);
        if (actor is null) return new(false, "Admin access is required.");
        if (!CanEditPrivateParticipantMetadata(bingoEvent)) return new(false, "Private participant metadata is unavailable in this event lifecycle state.");
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.EventId == eventId && x.Id == participantId, cancellationToken);
        if (participant is null) return new(false, "The participant could not be found.");
        var before = participant.PaymentStatus;
        if (before == payment) { await transaction.CommitAsync(cancellationToken); return new(true, null); }
        try
        {
            participant.SetPaymentStatus(payment);
            dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), timeProvider.GetUtcNow(), actorAccountId, actorName, "participant.payment_updated", "participant", participant.Id.ToString(), "Payment changed.", eventId, $"{{\"payment\":\"{before}\"}}", $"{{\"payment\":\"{payment}\"}}"));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, null, true);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "Payment could not be saved. Please try again.");
        }
    }

    public async Task<ParticipantPaymentResult> SetAdminNotesAsync(Guid eventId, Guid participantId, Guid? actorAccountId, string actorName, string? notes, string? expectedNotes, CancellationToken cancellationToken = default)
    {
        if (actorAccountId is null) return new(false, "Admin access is required.");
        if (notes?.Length > 2_000) return new(false, "Admin notes must be 2,000 characters or fewer.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var bingoEvent = await LockEventAsync(eventId, cancellationToken);
        if (bingoEvent is null) return new(false, "The event could not be found.");
        var actor = await AdminAsync(actorAccountId.Value, cancellationToken);
        if (actor is null) return new(false, "Admin access is required.");
        if (!CanEditPrivateParticipantMetadata(bingoEvent)) return new(false, "Private participant metadata is unavailable in this event lifecycle state.");

        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.EventId == eventId && x.Id == participantId, cancellationToken);
        if (participant is null) return new(false, "The participant could not be found.");
        if (!string.Equals(participant.AdminNotes ?? string.Empty, expectedNotes ?? string.Empty, StringComparison.Ordinal))
            return new(false, "This note changed elsewhere. Reload before saving it.");

        var before = participant.AdminNotes;
        var after = Clean(notes);
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return new(true, null);
        }

        try
        {
            participant.SetAdminNotes(after);
            dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), timeProvider.GetUtcNow(), actorAccountId, actorName, "participant.admin_note_updated", "participant", participant.Id.ToString(), "Private Admin note changed.", eventId,
                Json(new { present = before is not null }), Json(new { present = after is not null })));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, null, true);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "Admin note could not be saved. Please try again.");
        }
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
        var result = await UpdateSignupAdministrationCoreAsync(eventId, expectedVersion, newCap, waitingListEnabled,
            actorAccountId, actorName, confirmWaitingListDisablement, cancellationToken);
        if (!result.Succeeded && result.Settings is null && await AdminAsync(actorAccountId, cancellationToken) is not null)
        {
            var current = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(item => item.Id == eventId && item.HiddenAt == null, cancellationToken);
            if (current is not null) result = result with { Settings = SettingsSnapshot(current) };
        }
        return result;
    }

    private async Task<SignupAdministrationResult> UpdateSignupAdministrationCoreAsync(
        Guid eventId,
        long expectedVersion,
        int newCap,
        bool waitingListEnabled,
        Guid actorAccountId,
        string actorName,
        bool confirmWaitingListDisablement = false,
        CancellationToken cancellationToken = default)
    {
        SignupSettingsSnapshot? settings = null;
        SignupAdministrationResult Failure(string error) => new(false, error,
            SubmittedEventVersion: expectedVersion, Settings: settings);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            // Lock the authorizing identity before the event so all human
            // event mutations share the account -> event lock order.  The
            // lookup also replaces any caller-supplied claim name with the
            // account's canonical LoginName before audit/promotion work.
            var authorizedActor = await Bingo.Infrastructure.Events.EventMutationAuthorization.GetAuthorizedActorAsync(
                dbContext,
                new(actorAccountId, actorName),
                cancellationToken);
            if (authorizedActor is null) return Failure("Admin access is required.");
            actorAccountId = authorizedActor.Id;
            actorName = authorizedActor.Username;

            var bingoEvent = await LockEventAsync(eventId, cancellationToken);
            if (bingoEvent is null) return Failure("The event could not be found.");
            settings = SettingsSnapshot(bingoEvent);
            if (bingoEvent.Version != expectedVersion) return Failure("This event changed while you were editing it. Review the latest values and try again.");
            if (!bingoEvent.AcceptsWaitingList)
                return Failure("Signup administration is read-only after the draft starts or the event has moved on.");
            if (newCap < 1) return Failure("Maximum players must be at least 1.");
            var waitingCount = await SignupParticipants(eventId).CountAsync(item => item.SignupStatus == SignupStatus.WaitingList, cancellationToken);
            var confirmedCount = await SignupParticipants(eventId).CountAsync(item => item.SignupStatus == SignupStatus.Confirmed, cancellationToken);
            if (newCap < confirmedCount)
                return Failure($"Maximum players cannot be lower than the {confirmedCount} confirmed participant(s).");

            var beforeCapacity = bingoEvent.ParticipantCap;
            var beforeWaitingListEnabled = bingoEvent.WaitingListEnabled;
            var before = new
            {
                participantCap = beforeCapacity,
                confirmed = confirmedCount,
                waiting = waitingCount,
                waitingListEnabled = beforeWaitingListEnabled
            };
            try
            {
                bingoEvent.SetParticipantCap(newCap);
                // The posted waiting-list value is retained only for source
                // compatibility with older forms.  It can no longer disable the
                // active waiting-list model.
                bingoEvent.EnableWaitingList();
            }
            catch (ArgumentOutOfRangeException) { return Failure("Maximum players must be at least 1."); }
            catch (InvalidOperationException ex) { return Failure(ex.Message); }

            try
            {
                // A legacy disabled-waiting row may already contain more queued
                // participants than the new free places.  Only a genuine capacity
                // increase may promote them; saving an unchanged cap must not
                // replay the retired "promote everyone" transition.
                var capacityIncreased = beforeCapacity is null || newCap > beforeCapacity.Value;
                var promoted = capacityIncreased
                    ? await PromoteWithinLockedEventAsync(bingoEvent, actorAccountId, actorName, "signup administration", cancellationToken)
                    : 0;
                var confirmedAfter = await SignupParticipants(eventId).CountAsync(item => item.SignupStatus == SignupStatus.Confirmed, cancellationToken);
                var waitingAfter = await SignupParticipants(eventId).CountAsync(item => item.SignupStatus == SignupStatus.WaitingList, cancellationToken);
                var promotedParticipantIds = dbContext.ChangeTracker.Entries<AuditEntry>()
                    .Where(entry => entry.State == EntityState.Added && entry.Entity.EventId == eventId && entry.Entity.Action == "participant.promoted")
                    .Select(entry => entry.Entity.TargetId)
                    .Where(id => id is not null)
                    .Select(id => id!)
                    .ToArray();
                var after = new
                {
                    participantCap = bingoEvent.ParticipantCap,
                    confirmed = confirmedAfter,
                    waiting = waitingAfter,
                    waitingListEnabled = true
                };
                // Even an accepted unchanged value invalidates the submitted operation baseline.
                bingoEvent.AdvanceVersion();
                dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), timeProvider.GetUtcNow(), actorAccountId, actorName, "event.signup_administration_updated", "event",
                    eventId.ToString(), System.Text.Json.JsonSerializer.Serialize(new
                    {
                        requestedCapacity = newCap,
                        beforeCapacity,
                        afterCapacity = bingoEvent.ParticipantCap,
                        confirmedBefore = confirmedCount,
                        waitingBefore = waitingCount,
                        waitingListEnabledBefore = beforeWaitingListEnabled,
                        confirmedAfter,
                        waitingAfter,
                        waitingListEnabled = true,
                        promoted,
                        promotedParticipantIds
                    }), eventId,
                    System.Text.Json.JsonSerializer.Serialize(before), System.Text.Json.JsonSerializer.Serialize(after)));
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new(true, null, promoted, bingoEvent.ParticipantCap, SubmittedEventVersion: expectedVersion, Settings: SettingsSnapshot(bingoEvent));
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                settings = null;
                return Failure("Signup administration could not be saved. Please try again.");
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            settings = null;
            return Failure("Signup administration could not be saved. Please try again.");
        }
    }
    private static SignupSettingsSnapshot SettingsSnapshot(BingoEvent bingoEvent) => new(
        bingoEvent.Version, bingoEvent.ParticipantCap, bingoEvent.WaitingListEnabled,
        bingoEvent.RequireSignupCode, bingoEvent.SignupCodeHash is not null);

    public async Task<SignupResult> SignUpAuthenticatedAsync(AuthenticatedSignupRequest request, CancellationToken cancellationToken = default)
    {
        var prevalidation = await PrevalidateAuthenticatedNamesAsync(request, cancellationToken);
        if (prevalidation.Error is not null)
            return new(false, prevalidation.Error, null, null, null, null, prevalidation.ConfirmationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var bingoEvent = await dbContext.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {request.EventId} AND hidden_at IS NULL FOR UPDATE").SingleOrDefaultAsync(cancellationToken);
        if (bingoEvent is null || !bingoEvent.AcceptsSignups(now)) return new(false, "Signups are not currently open.", null, null, null);
        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == request.AccountId && x.AccountType == AccountType.WebsiteAccount && x.Active, cancellationToken);
        if (account is null) return new(false, "Signups require a normal website account.", null, null, null);
        var existing = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.EventId == request.EventId && x.AccountId == request.AccountId, cancellationToken);
        if (existing?.SignupStatus == SignupStatus.Withdrawn)
            return new(false, "Withdrawn signups cannot be edited. Rejoin while signup is open.", null, null, null);
        if (existing is not null && (request.ExpectedResponseVersion is null || request.ExpectedResponseVersion != existing.ResponseVersion))
            return new(false, "Your signup changed while you were editing it. Please reload and try again.", null, null, null);
        // A code protects a new admission only.  Editing an already admitted
        // signup must remain possible after an Admin rotates the code.
        if (existing is null && bingoEvent.RequireSignupCode && (string.IsNullOrWhiteSpace(request.SignupCode) || bingoEvent.SignupCodeHash is null || !secretHasher.Verify(request.SignupCode, bingoEvent.SignupCodeHash))) return new(false, "The event code is incorrect.", null, null, null);

        var form = await dbContext.SignupForms.SingleAsync(x => x.EventId == request.EventId, cancellationToken);
        var questions = await dbContext.SignupQuestions.Where(x => x.SignupFormId == form.Id && x.Active).OrderBy(x => x.Position).ToListAsync(cancellationToken);
        var captain = questions.SingleOrDefault(x => x.SystemField == SignupSystemField.CaptainVolunteer);
        var captainVolunteered = IsCaptainVolunteered(captain, request.Answers);
        var links = await (from link in dbContext.AccountOsrsCharacters
                           join character in dbContext.OsrsCharacters on link.OsrsCharacterId equals character.Id
                           where link.AccountId == request.AccountId && link.Active
                           select new { Link = link, Character = character }).ToDictionaryAsync(x => x.Character.Id, cancellationToken);
        var currentAssignments = existing is null
            ? []
            : await dbContext.EventParticipantCharacters.Where(x => x.EventParticipantId == existing.Id && x.ReleasedAt == null).ToListAsync(cancellationToken);
        if (prevalidation.Names is not null)
        {
            var requestedIds = questions.Where(question => question.Type == SignupQuestionType.Account)
                .Select(question => request.AccountAnswers.TryGetValue(question.Id, out var answer) ? answer.OsrsCharacterId : Guid.Empty)
                .Where(id => id != Guid.Empty).ToList();
            var exactNames = await dbContext.OsrsCharacters.AsNoTracking()
                .Where(character => requestedIds.Contains(character.Id))
                .Select(character => character.DisplayName)
                .ToListAsync(cancellationToken);
            if (!SameNames(prevalidation.Names, exactNames))
                return new(false, "The selected accounts changed while you were editing it. Please reload and try again.", null, null, null);
        }
        var currentByQuestion = currentAssignments.Where(x => x.SignupQuestionId is not null).ToDictionary(x => x.SignupQuestionId!.Value);
        var requestedCharacters = new HashSet<Guid>();
        foreach (var question in questions)
        {
            if (question.Type == SignupQuestionType.Account)
            {
                var supplied = request.AccountAnswers.TryGetValue(question.Id, out var answer) && answer is not null && answer.OsrsCharacterId != Guid.Empty;
                if (!supplied)
                {
                    if (IsRequiredAccountQuestion(question)) return new(false, $"'{question.Label}' is required.", null, null, null);
                    continue;
                }
                var selectedAnswer = answer!;
                if (!requestedCharacters.Add(selectedAnswer.OsrsCharacterId)) return new(false, "Choose each account only once.", null, null, null, question.Id);
                var keepsHistoricalAssignment = currentByQuestion.TryGetValue(question.Id, out var current) && current.OsrsCharacterId == selectedAnswer.OsrsCharacterId;
                if (!keepsHistoricalAssignment && !links.ContainsKey(selectedAnswer.OsrsCharacterId)) return new(false, "Choose an account from My accounts.", null, null, null);
                if (question.AccountAnswerRole == EventCharacterRole.Playing && (selectedAnswer.Ehb is null || selectedAnswer.Ehb < 0)) return new(false, $"'{question.Label}' requires EHB.", null, null, null);
                var reserved = await dbContext.EventParticipantCharacters.AnyAsync(x => x.EventId == request.EventId && x.OsrsCharacterId == selectedAnswer.OsrsCharacterId && (existing == null || x.EventParticipantId != existing.Id) && x.ReleasedAt == null, cancellationToken);
                if (reserved) return new(false, "That account is already signed up for this event. Choose another account.", null, null, null, question.Id);
                continue;
            }
            if (question.SystemField == SignupSystemField.CoCaptainName && !captainVolunteered) continue;
            if (!ValidateAnswer(question, AnswerForValidation(question, request.Answers), out var error))
                return new(false, error, null, null, null);
        }
        if (existing is not null && !bingoEvent.AcceptsSignups(now)) return new(false, "Signups are not currently open.", null, null, null);
        var participant = existing;
        SignupStatus status;
        if (participant is null)
        {
            var confirmed = await SignupParticipants(request.EventId).CountAsync(x => x.SignupStatus == SignupStatus.Confirmed, cancellationToken);
            status = confirmed < bingoEvent.ParticipantCap ? SignupStatus.Confirmed : SignupStatus.WaitingList;
            if (status == SignupStatus.WaitingList && !bingoEvent.AcceptsWaitingList) return new(false, "Signup administration is read-only after the draft starts or the event has moved on.", null, null, null);
            var sequence = (await dbContext.EventParticipants.Where(x => x.EventId == request.EventId).MaxAsync(x => (long?)x.SignupSequence, cancellationToken) ?? 0) + 1;
            participant = new EventParticipant(Guid.NewGuid(), request.EventId, status, sequence, now, SignupSource.Website);
            participant.AssignOwner(account);
            dbContext.EventParticipants.Add(participant);
        }
        else status = participant.SignupStatus;
        participant.SetCaptainVolunteer(captainVolunteered);
        var answers = existing is null ? [] : await dbContext.SignupAnswers.Where(x => x.EventParticipantId == participant.Id).ToListAsync(cancellationToken);
        var answersByQuestion = answers.ToDictionary(x => x.SignupQuestionId);
        var order = (await dbContext.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id).MaxAsync(x => (int?)x.RegistrationOrder, cancellationToken) ?? -1) + 1;
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
                dbContext.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), request.EventId, participant.Id, selectedAnswer.OsrsCharacterId, order++, now, request.AccountId, question.Id, role, role == EventCharacterRole.Playing ? selectedAnswer.Ehb : null, role == EventCharacterRole.Playing ? source : null, role == EventCharacterRole.Playing ? fetchedAt : null));
            }
            if (answersByQuestion.TryGetValue(question.Id, out var existingAnswer)) existingAnswer.SetAccountCharacter(selectedAnswer.OsrsCharacterId);
            else dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, question.Id, question.Label, string.Empty, selectedAnswer.OsrsCharacterId));
        }
        foreach (var question in questions.Where(x => x.Type != SignupQuestionType.Account && x.SystemField != SignupSystemField.CaptainVolunteer))
        {
            var value = question.SystemField == SignupSystemField.CoCaptainName && !captainVolunteered
                ? null
                : request.Answers.TryGetValue(question.Id, out var submitted) ? CanonicalAnswer(question, submitted) : null;
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
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_event_participant_characters_event_id_osrs_character_id" })
        {
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
            dbContext.ChangeTracker.Clear();
            var reservedIds = await dbContext.EventParticipantCharacters.Where(x => x.EventId == request.EventId && requestedCharacters.Contains(x.OsrsCharacterId) && x.EventParticipantId != participant.Id && x.ReleasedAt == null).Select(x => x.OsrsCharacterId).ToListAsync(cancellationToken);
            var conflict = questions.FirstOrDefault(x => x.Type == SignupQuestionType.Account && request.AccountAnswers.TryGetValue(x.Id, out var selected) && reservedIds.Contains(selected.OsrsCharacterId));
            return conflict is not null
                ? new(false, "That account is already signed up for this event. Choose another account.", null, null, null, conflict.Id)
                : new(false, "Your signup could not be saved. Please try again.", null, null, null);
        }
        catch (DbUpdateException) { dbContext.ChangeTracker.Clear(); return new(false, "Your signup could not be saved. Please try again.", null, null, null); }
        return new(true, null, participant.Id, status, status == SignupStatus.WaitingList ? await GetWaitingPositionAsync(participant.Id, request.EventId, cancellationToken) : null);
    }

    public Task<AdminParticipantResult> CorrectAdminParticipantAsync(AdminParticipantChangeRequest request, CancellationToken cancellationToken = default) =>
        ApplyAdminParticipantChangeAsync(request, false, cancellationToken);

    public Task<AdminParticipantResult> CreateAdminParticipantAsync(AdminParticipantChangeRequest request, CancellationToken cancellationToken = default) =>
        ApplyAdminParticipantChangeAsync(request, true, cancellationToken);

    public async Task<AdminParticipantResult> AddSavedParticipantAsync(
        AddSavedParticipantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EventId == Guid.Empty || request.OwnerAccountId == Guid.Empty || request.ActorAccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorName))
            return new(false, "A current event, website account, and administrator are required.");
        if (request.ExpectedEventVersion is null)
            return new(false, "The current event version is required. Reload before adding the participant.");
        if (request.Payment is not (PaymentStatus.Paid or PaymentStatus.Unpaid))
            return new(false, "Choose Paid or Unpaid.");
        if (request.PlayingCharacterIds is null || request.PlayingCharacterIds.Count == 0)
            return new(false, "Select at least one saved Playing account.");
        if (request.PlayingCharacterIds.Any(id => id == Guid.Empty) || request.PlayingCharacterIds.Distinct().Count() != request.PlayingCharacterIds.Count)
            return new(false, "Choose each Playing account only once.");
        if (!request.PlayingCharacterIds.Contains(request.PrimaryCharacterId))
            return new(false, "The selected primary account must be one of the selected Playing accounts.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var authorizedActor = await Bingo.Infrastructure.Events.EventMutationAuthorization.GetAuthorizedActorAsync(
                dbContext, new(request.ActorAccountId, request.ActorName), cancellationToken);
            if (authorizedActor is null) return new(false, "Admin access is required.");
            var now = ParticipantAttributionLock.AtDatabasePrecision(timeProvider.GetUtcNow());
            var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
            if (bingoEvent is null) return new(false, "The event could not be found.");
            if (!CanAdministerParticipants(bingoEvent)) return new(false, "Participant administration is read-only after the draft starts.");
            if (bingoEvent.Version != request.ExpectedEventVersion.Value)
                return new(false, "The event changed while you were editing it. Reload before adding the participant.");

            var owner = await dbContext.Accounts
                .FromSqlInterpolated($"SELECT * FROM accounts WHERE id = {request.OwnerAccountId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (owner is null || !owner.Active || owner.AccountType != AccountType.WebsiteAccount)
                return new(false, "The selected owner must be an active website account.");
            if (await dbContext.EventParticipants.AnyAsync(x => x.EventId == request.EventId && x.AccountId == owner.Id, cancellationToken))
                return new(false, "That website account already owns a participant in this event.");

            var form = await dbContext.SignupForms.SingleOrDefaultAsync(x => x.EventId == request.EventId, cancellationToken);
            if (form is null) return new(false, "This event has no signup form.");
            var questions = await dbContext.SignupQuestions
                .Where(x => x.SignupFormId == form.Id && x.EventId == request.EventId && x.Active)
                .OrderBy(x => x.Position).ToListAsync(cancellationToken);
            var playingQuestions = questions
                .Where(x => x.Type == SignupQuestionType.Account && x.AccountAnswerRole == EventCharacterRole.Playing)
                .ToList();
            var primaryQuestions = playingQuestions
                .Where(x => x.SystemField == SignupSystemField.PrimaryRegularAccount)
                .ToList();
            if (primaryQuestions.Count != 1 || !primaryQuestions[0].Required)
                return new(false, "The event has no unambiguous required Playing primary account field.");
            if (request.PlayingCharacterIds.Count > playingQuestions.Count)
                return new(false, "The selected Playing accounts exceed the event's configured Playing slots.");

            var selectedLinks = await (from link in dbContext.AccountOsrsCharacters
                                       join character in dbContext.OsrsCharacters on link.OsrsCharacterId equals character.Id
                                       where link.AccountId == owner.Id && link.Active && request.PlayingCharacterIds.Contains(link.OsrsCharacterId)
                                       select new { Link = link, Character = character }).ToListAsync(cancellationToken);
            if (selectedLinks.Count != request.PlayingCharacterIds.Count)
                return new(false, "Every selected Playing account must be an active saved link for the selected website account.");
            var byCharacter = selectedLinks.ToDictionary(x => x.Link.OsrsCharacterId);
            var missingEhb = selectedLinks
                .Where(x => x.Link.SavedEhb is null || x.Link.SavedEhb < 0 || x.Link.SavedEhb > 100000)
                .Select(x => x.Character.DisplayName).ToList();
            if (missingEhb.Count != 0)
                return new(false, $"Save a supported EHB for each selected Playing account before adding this participant: {string.Join(", ", missingEhb)}.");
            if (await dbContext.EventParticipantCharacters.AnyAsync(x => x.EventId == request.EventId && x.ReleasedAt == null && request.PlayingCharacterIds.Contains(x.OsrsCharacterId), cancellationToken))
                return new(false, "One of the selected Playing accounts is already registered for this event.");

            var capacity = bingoEvent.ParticipantCap;
            if (capacity is not { } currentCapacity || currentCapacity < 1)
                return new(false, "Configure a participant capacity before adding signups.");
            var confirmed = await SignupParticipants(request.EventId).CountAsync(x => x.SignupStatus == SignupStatus.Confirmed, cancellationToken);
            var full = confirmed >= currentCapacity;
            if (full && !request.ExpandCapacityWhenFull)
            {
                // The normal Add flow admits the participant to the queue.
            }
            else if (!full && request.ExpandCapacityWhenFull)
                return new(false, "The add-one-place option is available only when the event is full.");
            else if (full && request.ExpandCapacityWhenFull)
            {
                bingoEvent.SetParticipantCap(checked(currentCapacity + 1));
                bingoEvent.AdvanceVersion();
            }
            var status = full && !request.ExpandCapacityWhenFull ? SignupStatus.WaitingList : SignupStatus.Confirmed;
            var sequence = (await dbContext.EventParticipants.Where(x => x.EventId == request.EventId)
                .MaxAsync(x => (long?)x.SignupSequence, cancellationToken) ?? 0) + 1;
            var participant = new EventParticipant(Guid.NewGuid(), request.EventId, status, sequence, now, SignupSource.AdminCreated);
            participant.AssignOwner(owner);
            participant.SetCaptainVolunteer(false);
            participant.SetPaymentStatus(request.Payment);
            dbContext.EventParticipants.Add(participant);

            var remainingQuestions = playingQuestions.Where(x => x.Id != primaryQuestions[0].Id).ToList();
            var registrationOrder = 0;
            var selectedQuestionByCharacter = new Dictionary<Guid, SignupQuestion>();
            selectedQuestionByCharacter[request.PrimaryCharacterId] = primaryQuestions[0];
            var remainingIds = request.PlayingCharacterIds.Where(x => x != request.PrimaryCharacterId).ToList();
            for (var index = 0; index < remainingIds.Count; index++)
                selectedQuestionByCharacter[remainingIds[index]] = remainingQuestions[index];
            foreach (var characterId in request.PlayingCharacterIds)
            {
                var selected = byCharacter[characterId];
                var question = selectedQuestionByCharacter[characterId];
                var ehb = selected.Link.SavedEhb!.Value;
                dbContext.EventParticipantCharacters.Add(new EventParticipantCharacter(
                    Guid.NewGuid(), request.EventId, participant.Id, characterId, registrationOrder++, now,
                    authorizedActor.Id, question.Id, EventCharacterRole.Playing, ehb, EhbSource.Manual, null));
                dbContext.SignupAnswers.Add(new SignupAnswer(
                    Guid.NewGuid(), participant.Id, question.Id, question.Label, string.Empty, characterId));
            }
            form.RecordAcceptedResponse(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            var after = await AdminStateAsync(participant.Id, cancellationToken);
            dbContext.AuditEntries.Add(new AuditEntry(
                Guid.NewGuid(), now, authorizedActor.Id, authorizedActor.Username, "participant.admin_saved_created",
                "participant", participant.Id.ToString(),
                Json(new { payment = request.Payment.ToString(), expandedCapacity = full && request.ExpandCapacityWhenFull, questionnaireAnswers = 0 }),
                request.EventId, null, after));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, null, participant.Id, status,
                status == SignupStatus.WaitingList ? await GetWaitingPositionAsync(participant.Id, request.EventId, cancellationToken) : null);
        }
        catch (Exception exception) when (IsExpectedConflict(exception) && !cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "The participant or one of the selected accounts changed elsewhere. No changes were applied; reload and try again.");
        }
    }

    public async Task<EventAccountMutationResult> SwitchAdminPrimaryAsync(
        SwitchAdminPrimaryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EventId == Guid.Empty || request.ParticipantId == Guid.Empty || request.NextCharacterId == Guid.Empty || request.ActorAccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorName))
            return new(false, "A current event, participant, account, and administrator are required.");
        if (request.ExpectedResponseVersion is null)
            return new(false, "The current participant version is required. Reload before switching the primary account.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var authorizedActor = await Bingo.Infrastructure.Events.EventMutationAuthorization.GetAuthorizedActorAsync(
                dbContext, new(request.ActorAccountId, request.ActorName), cancellationToken);
            if (authorizedActor is null) return new(false, "Admin access is required.");
            var now = ParticipantAttributionLock.AtDatabasePrecision(timeProvider.GetUtcNow());
            var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
            if (bingoEvent is null) return new(false, "The event could not be found.");
            if (!CanAdministerParticipants(bingoEvent)) return new(false, "Primary account changes are locked after the draft starts.");
            var participant = await dbContext.EventParticipants
                .FromSqlInterpolated($"SELECT * FROM event_participants WHERE event_id = {request.EventId} AND id = {request.ParticipantId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (participant is null) return new(false, "The participant could not be found.");
            if (participant.SignupStatus == SignupStatus.Withdrawn)
                return new(false, "Withdrawn participants must be restored before changing event accounts.");
            if (participant.ResponseVersion != request.ExpectedResponseVersion.Value)
                return new(false, "This participant changed while you were editing it. Reload and try again.");

            var questions = await dbContext.SignupQuestions
                .Where(x => x.EventId == request.EventId && x.Active && x.Type == SignupQuestionType.Account && x.AccountAnswerRole == EventCharacterRole.Playing)
                .OrderBy(x => x.Position).ToListAsync(cancellationToken);
            var primaryQuestions = questions.Where(x => x.SystemField == SignupSystemField.PrimaryRegularAccount).ToList();
            if (primaryQuestions.Count != 1 || !primaryQuestions[0].Required)
                return new(false, "The event has no unambiguous required Playing primary account field.");
            var primaryQuestion = primaryQuestions[0];
            var assignments = await dbContext.EventParticipantCharacters
                .Where(x => x.EventParticipantId == participant.Id && x.EventId == request.EventId && x.ReleasedAt == null && x.EventRole == EventCharacterRole.Playing)
                .OrderBy(x => x.RegistrationOrder).ToListAsync(cancellationToken);
            var current = assignments.Where(x => x.SignupQuestionId == primaryQuestion.Id).ToList();
            if (current.Count != 1) return new(false, "The participant has no unambiguous current primary Playing account.");
            var currentAssignment = current[0];
            var previousPrimaryCharacterId = currentAssignment.OsrsCharacterId;
            if (request.ExpectedCurrentCharacterId is { } expectedCurrent && expectedCurrent != currentAssignment.OsrsCharacterId)
                return new(false, "The current primary account changed. Reload before switching it.");
            var nextAssignment = assignments.SingleOrDefault(x => x.OsrsCharacterId == request.NextCharacterId);
            if (nextAssignment is null) return new(false, "Choose one of the participant's current Playing accounts.");
            if (nextAssignment.Id == currentAssignment.Id)
            {
                await transaction.CommitAsync(cancellationToken);
                return new(true, ParticipantId: participant.Id, PrimaryCharacterId: currentAssignment.OsrsCharacterId,
                    PlayingAccountCount: assignments.Count, TotalAccountCount: await dbContext.EventParticipantCharacters.CountAsync(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null, cancellationToken), Changed: false);
            }
            if (assignments.Count(x => x.SignupQuestionId == nextAssignment.SignupQuestionId) > 1)
                return new(false, "The participant has duplicate account-slot mappings. Resolve them before switching the primary account.");

            var oldQuestionId = currentAssignment.SignupQuestionId;
            var nextQuestionId = nextAssignment.SignupQuestionId;
            SignupQuestion? secondaryQuestion = null;
            if (nextQuestionId is { } validatedSecondaryQuestionId)
            {
                secondaryQuestion = questions.SingleOrDefault(x => x.Id == validatedSecondaryQuestionId);
                if (secondaryQuestion is null) return new(false, "The selected account is not mapped to an active Playing slot.");
            }
            var answers = await dbContext.SignupAnswers
                .Where(x => x.EventParticipantId == participant.Id && (x.SignupQuestionId == primaryQuestion.Id || (nextQuestionId != null && x.SignupQuestionId == nextQuestionId.Value)))
                .ToListAsync(cancellationToken);
            if (answers.Count(x => x.SignupQuestionId == primaryQuestion.Id) > 1 ||
                (nextQuestionId is { } duplicateQuestionId && answers.Count(x => x.SignupQuestionId == duplicateQuestionId) > 1))
                return new(false, "The participant has duplicate account answers. Resolve them before switching the primary account.");
            var primaryAnswer = answers.SingleOrDefault(x => x.SignupQuestionId == primaryQuestion.Id);
            var secondaryAnswer = nextQuestionId is { } selectedQuestionId
                ? answers.SingleOrDefault(x => x.SignupQuestionId == selectedQuestionId)
                : null;

            // Validate every dependent slot and answer before changing tracked
            // assignments. A rejected request must not leak a remap through a
            // later SaveChanges call on this DbContext.
            currentAssignment.SetSignupQuestion(nextQuestionId);
            nextAssignment.SetSignupQuestion(oldQuestionId);
            if (primaryAnswer is null)
                dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, primaryQuestion.Id, primaryQuestion.Label, string.Empty, request.NextCharacterId));
            else
                primaryAnswer.SetAccountCharacter(request.NextCharacterId);
            if (nextQuestionId is { } selectedSecondaryQuestionId)
            {
                if (secondaryAnswer is null)
                    dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, selectedSecondaryQuestionId, secondaryQuestion!.Label, string.Empty, currentAssignment.OsrsCharacterId));
                else
                    secondaryAnswer.SetAccountCharacter(currentAssignment.OsrsCharacterId);
            }
            participant.AdvanceResponseVersion();
            AddAudit(authorizedActor.Id, authorizedActor.Username, "participant.primary_switched", participant, request.EventId,
                Json(new { primaryCharacterId = previousPrimaryCharacterId }),
                Json(new { primaryCharacterId = request.NextCharacterId, previousPrimaryCharacterId }));
            if (participant.AccountId is { } owner)
                AddNotification(owner, "participant.accounts_changed", "Your registered event accounts were updated by an administrator.", Route(bingoEvent, participant.Id), now, bingoEvent.Id);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, ParticipantId: participant.Id, PrimaryCharacterId: request.NextCharacterId,
                PlayingAccountCount: assignments.Count,
                TotalAccountCount: await dbContext.EventParticipantCharacters.CountAsync(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null, cancellationToken), Changed: true);
        }
        catch (Exception exception) when (IsExpectedConflict(exception) && !cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "Another administrator changed this participant first. Reload and try again.");
        }
    }

    public async Task<EventAccountMutationResult> AddEventParticipantAccountAsync(
        AddEventParticipantAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EventId == Guid.Empty || request.ParticipantId == Guid.Empty || request.CharacterId == Guid.Empty || request.ActorAccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorName))
            return new(false, "A current event, participant, account, and administrator are required.");
        if (request.Role is not (EventCharacterRole.Playing or EventCharacterRole.Informational))
            return new(false, "Choose a supported event account role.");
        if (request.Role == EventCharacterRole.Playing)
        {
            if (request.Ehb is not { } playingEhb)
                return new(false, "A Playing account requires an event EHB.");
            if (playingEhb < 0 || playingEhb > 100000)
                return new(false, "Enter a supported non-negative event EHB.");
        }
        if (request.Role == EventCharacterRole.Informational && request.Ehb is not null)
            return new(false, "Informational accounts do not accept EHB.");
        if (request.ExpectedResponseVersion is null)
            return new(false, "The current participant version is required. Reload before adding the event account.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var authorizedActor = await Bingo.Infrastructure.Events.EventMutationAuthorization.GetAuthorizedActorAsync(
                dbContext, new(request.ActorAccountId, request.ActorName), cancellationToken);
            if (authorizedActor is null) return new(false, "Admin access is required.");
            var now = ParticipantAttributionLock.AtDatabasePrecision(timeProvider.GetUtcNow());
            var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
            if (bingoEvent is null) return new(false, "The event could not be found.");
            if (!CanAdministerParticipants(bingoEvent)) return new(false, "Event account changes are locked after the draft starts.");
            var participant = await dbContext.EventParticipants
                .FromSqlInterpolated($"SELECT * FROM event_participants WHERE event_id = {request.EventId} AND id = {request.ParticipantId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (participant is null) return new(false, "The participant could not be found.");
            if (participant.SignupStatus == SignupStatus.Withdrawn)
                return new(false, "Withdrawn participants must be restored before changing event accounts.");
            if (participant.ResponseVersion != request.ExpectedResponseVersion.Value)
                return new(false, "This participant changed while you were editing it. Reload and try again.");
            var character = await dbContext.OsrsCharacters.SingleOrDefaultAsync(x => x.Id == request.CharacterId, cancellationToken);
            if (character is null) return new(false, "The selected OSRS account could not be found.");
            if (await dbContext.EventParticipantCharacters.AnyAsync(x => x.EventId == request.EventId && x.ReleasedAt == null && x.OsrsCharacterId == request.CharacterId, cancellationToken))
                return new(false, "That OSRS account is already assigned to another participant in this event.");

            var questions = await dbContext.SignupQuestions
                .Where(x => x.EventId == request.EventId && x.Active && x.Type == SignupQuestionType.Account && x.AccountAnswerRole == request.Role)
                .OrderBy(x => x.Position).ToListAsync(cancellationToken);
            if (questions.Count == 0) return new(false, "The event has no configured slot for this account role.");
            var current = await dbContext.EventParticipantCharacters
                .Where(x => x.EventParticipantId == participant.Id && x.EventId == request.EventId && x.ReleasedAt == null && x.EventRole == request.Role)
                .OrderBy(x => x.RegistrationOrder).ToListAsync(cancellationToken);
            if (current.Count >= questions.Count)
                return new(false, "The participant has reached the configured event account slots for this role.");
            var occupiedQuestionIds = current.Where(x => x.SignupQuestionId is not null).Select(x => x.SignupQuestionId!.Value).ToHashSet();
            SignupQuestion? question = null;
            if (request.SignupQuestionId is { } requestedQuestionId)
            {
                question = questions.SingleOrDefault(x => x.Id == requestedQuestionId);
                if (question is null) return new(false, "The selected account slot is not active for this event.");
                if (!occupiedQuestionIds.Add(question.Id)) return new(false, "The selected account slot is already in use.");
            }
            else
                question = questions.FirstOrDefault(x => !occupiedQuestionIds.Contains(x.Id));
            if (question is null) return new(false, "No configured event account slot is available.");

            var order = (await dbContext.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id)
                .MaxAsync(x => (int?)x.RegistrationOrder, cancellationToken) ?? -1) + 1;
            var assignment = new EventParticipantCharacter(
                Guid.NewGuid(), request.EventId, participant.Id, request.CharacterId, order, now, authorizedActor.Id,
                question.Id, request.Role, request.Role == EventCharacterRole.Playing ? request.Ehb : null,
                request.Role == EventCharacterRole.Playing ? EhbSource.AdminCorrection : null, null);
            dbContext.EventParticipantCharacters.Add(assignment);
            dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, question.Id, question.Label, string.Empty, request.CharacterId));
            participant.AdvanceResponseVersion();
            AddAudit(authorizedActor.Id, authorizedActor.Username, "participant.event_account_added", participant, request.EventId,
                Json(null), Json(new { assignmentId = assignment.Id, characterId = request.CharacterId, role = request.Role.ToString(), ehb = request.Ehb }));
            if (participant.AccountId is { } owner)
                AddNotification(owner, "participant.accounts_changed", "Your registered event accounts were updated by an administrator.", Route(bingoEvent, participant.Id), now, bingoEvent.Id);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var total = await dbContext.EventParticipantCharacters.CountAsync(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null, cancellationToken);
            var playing = await dbContext.EventParticipantCharacters.CountAsync(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null && x.EventRole == EventCharacterRole.Playing, cancellationToken);
            var primary = await dbContext.PrimaryCharacters().Where(x => x.ParticipantId == participant.Id).Select(x => (Guid?)x.OsrsCharacterId).SingleOrDefaultAsync(cancellationToken);
            return new(true, ParticipantId: participant.Id, PrimaryCharacterId: primary, PlayingAccountCount: playing, TotalAccountCount: total, Changed: true);
        }
        catch (Exception exception) when (IsExpectedConflict(exception) && !cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "Another administrator changed this participant or account first. Reload and try again.");
        }
    }

    public async Task<EventAccountMutationResult> RemoveEventParticipantAccountAsync(
        RemoveEventParticipantAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EventId == Guid.Empty || request.ParticipantId == Guid.Empty || request.AssignmentId == Guid.Empty || request.ActorAccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorName))
            return new(false, "A current event, participant, assignment, and administrator are required.");
        if (request.ExpectedResponseVersion is null)
            return new(false, "The current participant version is required. Reload before removing the event account.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var authorizedActor = await Bingo.Infrastructure.Events.EventMutationAuthorization.GetAuthorizedActorAsync(
                dbContext, new(request.ActorAccountId, request.ActorName), cancellationToken);
            if (authorizedActor is null) return new(false, "Admin access is required.");
            var now = ParticipantAttributionLock.AtDatabasePrecision(timeProvider.GetUtcNow());
            var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
            if (bingoEvent is null) return new(false, "The event could not be found.");
            if (!CanAdministerParticipants(bingoEvent)) return new(false, "Event account changes are locked after the draft starts.");
            var participant = await dbContext.EventParticipants
                .FromSqlInterpolated($"SELECT * FROM event_participants WHERE event_id = {request.EventId} AND id = {request.ParticipantId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (participant is null) return new(false, "The participant could not be found.");
            if (participant.SignupStatus == SignupStatus.Withdrawn)
                return new(false, "Withdrawn participants must be restored before changing event accounts.");
            if (participant.ResponseVersion != request.ExpectedResponseVersion.Value)
                return new(false, "This participant changed while you were editing it. Reload and try again.");
            var assignment = await dbContext.EventParticipantCharacters
                .FromSqlInterpolated($"SELECT * FROM event_participant_characters WHERE id = {request.AssignmentId} AND event_id = {request.EventId} AND event_participant_id = {request.ParticipantId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (assignment is null || assignment.ReleasedAt is not null)
                return new(false, "The selected event account is no longer active.");
            var primaryQuestions = await dbContext.SignupQuestions
                .Where(x => x.EventId == request.EventId && x.Active && x.SystemField == SignupSystemField.PrimaryRegularAccount && x.AccountAnswerRole == EventCharacterRole.Playing)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            if (primaryQuestions.Count > 1)
                return new(false, "The event has an ambiguous primary Playing account field.");
            if (assignment.SignupQuestionId == primaryQuestions.SingleOrDefault())
                return new(false, "The required primary Playing account cannot be removed.");
            if (assignment.EventRole == EventCharacterRole.Playing && await dbContext.EventParticipantCharacters.CountAsync(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null && x.EventRole == EventCharacterRole.Playing, cancellationToken) <= 1)
                return new(false, "A participant must retain at least one Playing account.");
            assignment.Release(authorizedActor.Id, now);
            if (assignment.SignupQuestionId is { } questionId)
            {
                var answer = await dbContext.SignupAnswers.SingleOrDefaultAsync(x => x.EventParticipantId == participant.Id && x.SignupQuestionId == questionId, cancellationToken);
                if (answer is not null) dbContext.SignupAnswers.Remove(answer);
            }
            participant.AdvanceResponseVersion();
            AddAudit(authorizedActor.Id, authorizedActor.Username, "participant.event_account_removed", participant, request.EventId,
                Json(new { assignmentId = assignment.Id, characterId = assignment.OsrsCharacterId, role = assignment.EventRole.ToString() }), Json(null));
            if (participant.AccountId is { } owner)
                AddNotification(owner, "participant.accounts_changed", "Your registered event accounts were updated by an administrator.", Route(bingoEvent, participant.Id), now, bingoEvent.Id);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var total = await dbContext.EventParticipantCharacters.CountAsync(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null, cancellationToken);
            var playing = await dbContext.EventParticipantCharacters.CountAsync(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null && x.EventRole == EventCharacterRole.Playing, cancellationToken);
            var primary = await dbContext.PrimaryCharacters().Where(x => x.ParticipantId == participant.Id).Select(x => (Guid?)x.OsrsCharacterId).SingleOrDefaultAsync(cancellationToken);
            return new(true, ParticipantId: participant.Id, PrimaryCharacterId: primary, PlayingAccountCount: playing, TotalAccountCount: total, Changed: true);
        }
        catch (Exception exception) when (IsExpectedConflict(exception) && !cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "Another administrator changed this participant or account first. Reload and try again.");
        }
    }

    public async Task<EventAccountMutationResult> CorrectEventParticipantAccountAsync(
        CorrectEventParticipantAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EventId == Guid.Empty || request.ParticipantId == Guid.Empty || request.AssignmentId == Guid.Empty || request.CharacterId == Guid.Empty || request.ActorAccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorName))
            return new(false, "A current event, participant, assignment, account, and administrator are required.");
        if (request.ExpectedResponseVersion is null)
            return new(false, "The current participant version is required. Reload before correcting the event account.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var authorizedActor = await Bingo.Infrastructure.Events.EventMutationAuthorization.GetAuthorizedActorAsync(
                dbContext, new(request.ActorAccountId, request.ActorName), cancellationToken);
            if (authorizedActor is null) return new(false, "Admin access is required.");
            var now = ParticipantAttributionLock.AtDatabasePrecision(timeProvider.GetUtcNow());
            var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
            if (bingoEvent is null) return new(false, "The event could not be found.");
            if (!CanAdministerParticipants(bingoEvent)) return new(false, "Event account changes are locked after the draft starts.");
            var participant = await dbContext.EventParticipants
                .FromSqlInterpolated($"SELECT * FROM event_participants WHERE event_id = {request.EventId} AND id = {request.ParticipantId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (participant is null) return new(false, "The participant could not be found.");
            if (participant.SignupStatus == SignupStatus.Withdrawn)
                return new(false, "Withdrawn participants must be restored before changing event accounts.");
            if (participant.ResponseVersion != request.ExpectedResponseVersion.Value)
                return new(false, "This participant changed while you were editing it. Reload and try again.");
            var assignment = await dbContext.EventParticipantCharacters
                .FromSqlInterpolated($"SELECT * FROM event_participant_characters WHERE id = {request.AssignmentId} AND event_id = {request.EventId} AND event_participant_id = {request.ParticipantId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (assignment is null || assignment.ReleasedAt is not null)
                return new(false, "The selected event account is no longer active.");
            var character = await dbContext.OsrsCharacters.SingleOrDefaultAsync(x => x.Id == request.CharacterId, cancellationToken);
            if (character is null) return new(false, "The selected OSRS account could not be found.");
            if (await dbContext.EventParticipantCharacters.AnyAsync(x => x.EventId == request.EventId && x.ReleasedAt == null && x.OsrsCharacterId == request.CharacterId && x.Id != assignment.Id, cancellationToken))
                return new(false, "That OSRS account is already assigned to another participant in this event.");
            var previousAssignmentId = assignment.Id;
            var previousCharacterId = assignment.OsrsCharacterId;
            var previousEhb = assignment.EhbSnapshot;
            var previousEhbSource = assignment.EhbSource;
            var previousEhbFetchedAt = assignment.EhbFetchedAt;
            var previousQuestionId = assignment.SignupQuestionId;
            decimal? correctedEhb = null;
            EhbSource? correctedEhbSource = null;
            if (assignment.EventRole == EventCharacterRole.Playing)
            {
                correctedEhb = request.Ehb ?? assignment.EhbSnapshot;
                if (correctedEhb is not { } validEhb || validEhb < 0 || validEhb > 100000)
                    return new(false, "Enter a supported non-negative event EHB for the Playing account.");
                if (assignment.OsrsCharacterId == request.CharacterId && assignment.EhbSnapshot == validEhb)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return new(true, ParticipantId: participant.Id, PrimaryCharacterId: (await dbContext.PrimaryCharacters().Where(x => x.ParticipantId == participant.Id).Select(x => (Guid?)x.OsrsCharacterId).SingleOrDefaultAsync(cancellationToken)), Changed: false);
                }
                correctedEhbSource = EhbSource.AdminCorrection;
            }
            else
            {
                if (request.Ehb is not null) return new(false, "Informational accounts do not accept EHB.");
                if (assignment.OsrsCharacterId == request.CharacterId)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return new(true, ParticipantId: participant.Id, PrimaryCharacterId: (await dbContext.PrimaryCharacters().Where(x => x.ParticipantId == participant.Id).Select(x => (Guid?)x.OsrsCharacterId).SingleOrDefaultAsync(cancellationToken)), Changed: false);
                }
            }

            // Corrections are append-only assignment history. Keep the prior
            // character/EHB/provenance row released, then add a new active row
            // with the same configured slot and a fresh registration order.
            var order = (await dbContext.EventParticipantCharacters
                .Where(x => x.EventParticipantId == participant.Id)
                .MaxAsync(x => (int?)x.RegistrationOrder, cancellationToken) ?? -1) + 1;
            assignment.Release(authorizedActor.Id, now);
            var correctedAssignment = new EventParticipantCharacter(
                Guid.NewGuid(), request.EventId, participant.Id, request.CharacterId, order, now,
                authorizedActor.Id, previousQuestionId, assignment.EventRole, correctedEhb, correctedEhbSource, null);
            dbContext.EventParticipantCharacters.Add(correctedAssignment);
            if (previousQuestionId is { } questionId)
            {
                var answer = await dbContext.SignupAnswers.SingleOrDefaultAsync(x => x.EventParticipantId == participant.Id && x.SignupQuestionId == questionId, cancellationToken);
                if (answer is null)
                {
                    var question = await dbContext.SignupQuestions.SingleAsync(x => x.Id == questionId, cancellationToken);
                    dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, questionId, question.Label, string.Empty, request.CharacterId));
                }
                else answer.SetAccountCharacter(request.CharacterId);
            }
            participant.AdvanceResponseVersion();
            AddAudit(authorizedActor.Id, authorizedActor.Username, "participant.event_account_corrected", participant, request.EventId,
                Json(new
                {
                    assignmentId = previousAssignmentId,
                    characterId = previousCharacterId,
                    ehb = previousEhb,
                    ehbSource = previousEhbSource?.ToString(),
                    ehbFetchedAt = previousEhbFetchedAt,
                    signupQuestionId = previousQuestionId
                }),
                Json(new
                {
                    assignmentId = correctedAssignment.Id,
                    characterId = correctedAssignment.OsrsCharacterId,
                    ehb = correctedAssignment.EhbSnapshot,
                    ehbSource = correctedAssignment.EhbSource?.ToString(),
                    ehbFetchedAt = correctedAssignment.EhbFetchedAt,
                    signupQuestionId = correctedAssignment.SignupQuestionId,
                    replacedAssignmentId = previousAssignmentId
                }));
            if (participant.AccountId is { } owner)
                AddNotification(owner, "participant.accounts_changed", "Your registered event accounts were updated by an administrator.", Route(bingoEvent, participant.Id), now, bingoEvent.Id);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var total = await dbContext.EventParticipantCharacters.CountAsync(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null, cancellationToken);
            var playing = await dbContext.EventParticipantCharacters.CountAsync(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null && x.EventRole == EventCharacterRole.Playing, cancellationToken);
            var primary = await dbContext.PrimaryCharacters().Where(x => x.ParticipantId == participant.Id).Select(x => (Guid?)x.OsrsCharacterId).SingleOrDefaultAsync(cancellationToken);
            return new(true, ParticipantId: participant.Id, PrimaryCharacterId: primary, PlayingAccountCount: playing, TotalAccountCount: total, Changed: true);
        }
        catch (Exception exception) when (IsExpectedConflict(exception) && !cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "Another administrator changed this participant or account first. Reload and try again.");
        }
    }

    public Task<ParticipantOwnershipTransferResult> TransferParticipantOwnershipAsync(ParticipantOwnershipTransferRequest request, CancellationToken cancellationToken = default)
    {
        // Ownership transfer was a legacy correction path.  Keep the result type so
        // retained callers can fail safely, but never mutate a participant or emit a
        // misleading Audit/notification record through this retired entry point.
        return Task.FromResult(new ParticipantOwnershipTransferResult(false, "Participant ownership transfer is no longer available."));
    }

    private async Task<AdminParticipantResult> ApplyAdminParticipantChangeAsync(AdminParticipantChangeRequest request, bool creating, CancellationToken ct)
    {
        var prevalidation = await PrevalidateAdminNamesAsync(request, creating, ct);
        if (prevalidation is not null)
            return prevalidation;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var now = timeProvider.GetUtcNow();
        var authorizedActor = await Bingo.Infrastructure.Events.EventMutationAuthorization.GetAuthorizedActorAsync(
            dbContext, new(request.ActorAccountId, request.ActorName), ct);
        if (authorizedActor is null) return new(false, "Admin access is required.");
        var bingoEvent = await LockEventAsync(request.EventId, ct);
        if (bingoEvent is null) return new(false, "The event could not be found.");
        if (!CanAdministerParticipants(bingoEvent)) return new(false, "Participant administration is read-only after the draft starts.");
        var form = await dbContext.SignupForms.SingleOrDefaultAsync(x => x.EventId == request.EventId, ct);
        if (form is null) return new(false, "This event has no signup form.");
        var questions = await dbContext.SignupQuestions.Where(x => x.SignupFormId == form.Id && x.Active).OrderBy(x => x.Position).ToListAsync(ct);
        var captain = questions.SingleOrDefault(x => x.SystemField == SignupSystemField.CaptainVolunteer);
        var captainVolunteered = IsCaptainVolunteered(captain, request.Answers);
        EventParticipant? participant = null;
        if (!creating)
        {
            if (request.ParticipantId is null) return new(false, "The participant could not be found.");
            participant = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.EventId == request.EventId && x.Id == request.ParticipantId, ct);
            if (participant is null) return new(false, "The participant could not be found.");
            if (request.ExpectedResponseVersion is null || request.ExpectedResponseVersion != participant.ResponseVersion)
                return new(false, "The participant changed while you were editing it. Reload and try again.");
            if (participant.SignupStatus == SignupStatus.Withdrawn)
                return new(false, "Withdrawn participants must be restored before their signup can be corrected.");
        }
        Account? owner = null;
        if (creating && request.OwnerAccountId is not { } ownerId)
            return new(false, "Select an active website account for this participant.");
        if (creating && request.OwnerAccountId is { } selectedOwnerId)
        {
            owner = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == selectedOwnerId && x.Active && x.AccountType == AccountType.WebsiteAccount, ct);
            if (owner is null) return new(false, "The selected owner must be an active website account.");
            if (await dbContext.EventParticipants.AnyAsync(x => x.EventId == request.EventId && x.AccountId == selectedOwnerId, ct)) return new(false, "That website account already owns a participant in this event.");
        }
        var submittedCharacters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var question in questions)
        {
            if (question.Type == SignupQuestionType.Account)
            {
                request.AccountAnswers.TryGetValue(question.Id, out var answer);
                var name = answer?.CharacterName?.Trim();
                if (string.IsNullOrWhiteSpace(name)) { if (IsRequiredAccountQuestion(question)) return new(false, $"'{question.Label}' is required."); continue; }
                var normalized = NormalizeAccountName(name);
                if (!submittedCharacters.Add(normalized)) return new(false, "Choose each account only once.");
                if (question.AccountAnswerRole == EventCharacterRole.Playing && (answer?.Ehb is null || answer.Ehb < 0)) return new(false, $"'{question.Label}' requires EHB.");
            }
            else if (question.SystemField == SignupSystemField.CoCaptainName && !captainVolunteered) continue;
            else if (!ValidateAnswer(question, AnswerForValidation(question, request.Answers), out var error)) return new(false, error);
        }
        SignupStatus status;
        if (participant is null)
        {
            var confirmed = await SignupParticipants(request.EventId).CountAsync(x => x.SignupStatus == SignupStatus.Confirmed, ct);
            status = confirmed < bingoEvent.ParticipantCap ? SignupStatus.Confirmed : SignupStatus.WaitingList;
            if (status == SignupStatus.WaitingList && !bingoEvent.AcceptsWaitingList) return new(false, "Participant administration is read-only after the draft starts.");
            var sequence = (await dbContext.EventParticipants.Where(x => x.EventId == request.EventId).MaxAsync(x => (long?)x.SignupSequence, ct) ?? 0) + 1;
            participant = new EventParticipant(Guid.NewGuid(), request.EventId, status, sequence, now, SignupSource.AdminCreated);
            if (owner is not null) participant.AssignOwner(owner);
            dbContext.EventParticipants.Add(participant);
        }
        else status = participant.SignupStatus;
        var before = creating ? null : await AdminStateAsync(participant.Id, ct);
        var current = await dbContext.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null).ToListAsync(ct);
        var currentByQuestion = current.Where(x => x.SignupQuestionId is not null).ToDictionary(x => x.SignupQuestionId!.Value);
        var registeredAccountsChanged = false;
        var answers = await dbContext.SignupAnswers.Where(x => x.EventParticipantId == participant.Id).ToListAsync(ct);
        var answersByQuestion = answers.ToDictionary(x => x.SignupQuestionId);
        var order = (await dbContext.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id).MaxAsync(x => (int?)x.RegistrationOrder, ct) ?? -1) + 1;
        foreach (var question in questions.Where(x => x.Type == SignupQuestionType.Account))
        {
            request.AccountAnswers.TryGetValue(question.Id, out var supplied);
            var name = supplied?.CharacterName?.Trim(); currentByQuestion.TryGetValue(question.Id, out var existing);
            if (string.IsNullOrWhiteSpace(name)) { if (existing is not null) registeredAccountsChanged = true; existing?.Release(request.ActorAccountId, now); if (answersByQuestion.Remove(question.Id, out var removed)) dbContext.SignupAnswers.Remove(removed); continue; }
            var normalized = NormalizeAccountName(name);
            var character = await ResolveCharacterAsync(name, normalized, ct);
            if (await dbContext.EventParticipantCharacters.AnyAsync(x => x.EventId == request.EventId && x.OsrsCharacterId == character.Id && x.EventParticipantId != participant.Id && x.ReleasedAt == null, ct)) return new(false, "That account is already signed up for this event.");
            var role = question.AccountAnswerRole!.Value;
            if (existing is null || existing.OsrsCharacterId != character.Id ||
                (role == EventCharacterRole.Playing && existing.EhbSnapshot != supplied!.Ehb))
                registeredAccountsChanged = true;
            if (existing is not null && existing.OsrsCharacterId == character.Id) { if (role == EventCharacterRole.Playing) existing.UpdatePlayingEhb(supplied!.Ehb!.Value, EhbSource.AdminCorrection); }
            else { existing?.Release(request.ActorAccountId, now); dbContext.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), request.EventId, participant.Id, character.Id, order++, now, request.ActorAccountId, question.Id, role, role == EventCharacterRole.Playing ? supplied?.Ehb : null, role == EventCharacterRole.Playing ? EhbSource.AdminCorrection : null, null)); }
            if (answersByQuestion.TryGetValue(question.Id, out var saved)) saved.SetAccountCharacter(character.Id); else dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, question.Id, question.Label, string.Empty, character.Id));
        }
        participant.SetCaptainVolunteer(captainVolunteered);
        foreach (var question in questions.Where(x => x.Type != SignupQuestionType.Account && x.SystemField != SignupSystemField.CaptainVolunteer))
        {
            var value = question.SystemField == SignupSystemField.CoCaptainName && !captainVolunteered
                ? null
                : request.Answers.TryGetValue(question.Id, out var answer) ? CanonicalAnswer(question, answer) : null;
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
            if (!creating && registeredAccountsChanged && participant.AccountId is { } recipientOwnerId)
                AddNotification(recipientOwnerId, "participant.accounts_changed", "Your registered event accounts were updated by an administrator.", Route(bingoEvent, participant.Id), now, bingoEvent.Id);
            var validationDetail = string.IsNullOrWhiteSpace(request.WomValidationConfirmationToken)
                ? creating ? "Admin participant created." : "Participant signup corrected."
                : creating ? "Admin participant created after explicitly confirming a Wise Old Man operational failure."
                : "Participant signup corrected after explicitly confirming a Wise Old Man operational failure.";
            dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, authorizedActor.Id, authorizedActor.Username, creating ? "participant.admin_created" : "participant.corrected", "participant", participant.Id.ToString(), validationDetail, request.EventId, before, after));
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { dbContext.ChangeTracker.Clear(); return new(false, "The participant changed while you were editing it. Reload and try again."); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { dbContext.ChangeTracker.Clear(); return new(false, "That account is already signed up for this event."); }
        catch (DbUpdateException) { dbContext.ChangeTracker.Clear(); return new(false, "The participant could not be saved. Please try again."); }
        return new(true, null, participant.Id, status, status == SignupStatus.WaitingList ? await GetWaitingPositionAsync(participant.Id, request.EventId, ct) : null);
    }

    private async Task<AdminParticipantResult?> PrevalidateAdminNamesAsync(AdminParticipantChangeRequest request, bool creating, CancellationToken ct)
    {
        var actor = await dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == request.ActorAccountId && x.Active && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin), ct);
        if (actor is null) return new(false, "Admin access is required.");
        var bingoEvent = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.EventId && x.HiddenAt == null, ct);
        if (bingoEvent is null) return new(false, "The event could not be found.");
        if (!CanAdministerParticipants(bingoEvent))
            return new(false, "Participant administration is read-only after the draft starts.");
        Guid? participantId = request.ParticipantId;
        if (!creating)
        {
            var participant = await dbContext.EventParticipants.AsNoTracking()
                .SingleOrDefaultAsync(x => x.EventId == request.EventId && x.Id == request.ParticipantId, ct);
            if (participant is null) return new(false, "The participant could not be found.");
            if (request.ExpectedResponseVersion is null || participant.ResponseVersion != request.ExpectedResponseVersion)
                return new(false, "The participant changed while you were editing it. Reload and try again.");
            if (participant.SignupStatus == SignupStatus.Withdrawn)
                return new(false, "Withdrawn participants must be restored before their signup can be corrected.");
        }
        if (creating && request.OwnerAccountId is not { } ownerId)
            return new(false, "Select an active website account for this participant.");
        if (creating && request.OwnerAccountId is { } selectedOwnerId)
        {
            var owner = await dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(
                x => x.Id == selectedOwnerId && x.Active && x.AccountType == AccountType.WebsiteAccount, ct);
            if (owner is null) return new(false, "The selected owner must be an active website account.");
            if (await dbContext.EventParticipants.AsNoTracking().AnyAsync(x => x.EventId == request.EventId && x.AccountId == selectedOwnerId, ct))
                return new(false, "That website account already owns a participant in this event.");
        }

        var names = request.AccountAnswers.Values
            .Where(answer => !string.IsNullOrWhiteSpace(answer.CharacterName))
            .Select(answer => answer.CharacterName!.Trim())
            .DistinctBy(NormalizeAccountName, StringComparer.Ordinal)
            .ToList();
        var validation = await RequiredAccountValidation.ValidateAsync(new WiseOldManAccountValidationRequest(
            request.ActorAccountId,
            creating ? "participant.create" : "participant.edit",
            request.EventId,
            participantId,
            request.ExpectedResponseVersion,
            names,
            true,
            request.WomValidationConfirmationToken), ct);
        return validation.CanProceed
            ? null
            : new(false, ValidationFailure(validation), request.ParticipantId, null, null, validation.ConfirmationToken);
    }

    private async Task<PrevalidatedNames> PrevalidateAuthenticatedNamesAsync(AuthenticatedSignupRequest request, CancellationToken ct)
    {
        var bingoEvent = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.EventId && x.HiddenAt == null, ct);
        if (bingoEvent is null || !bingoEvent.AcceptsSignups(timeProvider.GetUtcNow()))
            return new(null, "Signups are not currently open.", null);
        var account = await dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == request.AccountId && x.AccountType == AccountType.WebsiteAccount && x.Active, ct);
        if (account is null) return new(null, "Signups require a normal website account.", null);

        var existing = await dbContext.EventParticipants.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EventId == request.EventId && x.AccountId == request.AccountId, ct);
        if (existing?.SignupStatus == SignupStatus.Withdrawn)
            return new(null, "Withdrawn signups cannot be edited. Rejoin while signup is open.", null);
        if (existing is not null && (request.ExpectedResponseVersion is null || request.ExpectedResponseVersion != existing.ResponseVersion))
            return new(null, "Your signup changed while you were editing it. Please reload and try again.", null);

        var questionIds = await dbContext.SignupQuestions.AsNoTracking()
            .Where(question => question.EventId == request.EventId && question.Active && question.Type == SignupQuestionType.Account)
            .Select(question => question.Id).ToListAsync(ct);
        var submitted = request.AccountAnswers
            .Where(answer => questionIds.Contains(answer.Key) && answer.Value.OsrsCharacterId != Guid.Empty)
            .ToDictionary(answer => answer.Key, answer => answer.Value.OsrsCharacterId);
        var linkIds = await dbContext.AccountOsrsCharacters.AsNoTracking()
            .Where(link => link.AccountId == request.AccountId && link.Active)
            .Select(link => link.OsrsCharacterId).ToListAsync(ct);
        var historicalIds = existing is null
            ? []
            : await dbContext.EventParticipantCharacters.AsNoTracking()
                .Where(assignment => assignment.EventParticipantId == existing.Id && assignment.ReleasedAt == null)
                .Select(assignment => assignment.OsrsCharacterId).ToListAsync(ct);
        var allowed = linkIds.Concat(historicalIds).ToHashSet();
        if (submitted.Values.Any(id => !allowed.Contains(id)))
            return new(null, "Choose an account from My accounts.", null);

        var names = await dbContext.OsrsCharacters.AsNoTracking()
            .Where(character => submitted.Values.Contains(character.Id))
            .Select(character => character.DisplayName).ToListAsync(ct);
        if (names.Count != submitted.Values.Distinct().Count())
            return new(null, "One of the selected accounts is no longer available. Reload and try again.", null);

        var validation = await RequiredAccountValidation.ValidateAsync(new WiseOldManAccountValidationRequest(
            request.AccountId,
            existing is null ? "signup.create" : "signup.edit",
            request.EventId,
            existing?.Id,
            request.ExpectedResponseVersion,
            names,
            false,
            request.WomValidationConfirmationToken), ct);
        if (!validation.CanProceed)
            return new(names, ValidationFailure(validation), validation.ConfirmationToken);
        return new(names, null, null);
    }

    private static string ValidationFailure(WiseOldManAccountValidationResult result) => result.HasKnownInvalid
        ? "Wise Old Man could not find one or more submitted characters. Check the names and try again."
        : result.Outcome == WiseOldManAccountValidationOutcome.ConfirmationRequired
            ? "Wise Old Man is unavailable. Confirm again to save these unverified accounts, or cancel to leave the roster unchanged."
            : "Wise Old Man is unavailable, so these account names could not be verified. Try again later.";

    private static bool SameNames(IReadOnlyCollection<string> expected, IReadOnlyCollection<string> actual) =>
        expected.Select(NormalizeAccountName).Order(StringComparer.Ordinal)
            .SequenceEqual(actual.Select(NormalizeAccountName).Order(StringComparer.Ordinal), StringComparer.Ordinal);

    private sealed record PrevalidatedNames(IReadOnlyList<string>? Names, string? Error, string? ConfirmationToken);

    private async Task<Account?> AdminAsync(Guid accountId, CancellationToken ct) => await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == accountId && x.Active && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin), ct);
    private static bool CanAdministerParticipants(Domain.Events.BingoEvent bingoEvent) => !bingoEvent.DraftLocked && bingoEvent.State is (Domain.Events.EventState.Draft or Domain.Events.EventState.SignupOpen or Domain.Events.EventState.SignupClosed);
    private static bool CanEditPrivateParticipantMetadata(Domain.Events.BingoEvent bingoEvent) => bingoEvent.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed or EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived or EventState.Cancelled;
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
    private async Task<SignupQuestionImpact> CurrentQuestionImpactAsync(Guid eventId, SignupQuestion question, CancellationToken ct)
    {
        var answerCount = await dbContext.SignupAnswers.CountAsync(item => item.SignupQuestionId == question.Id, ct);
        var releaseCount = await dbContext.EventParticipantCharacters.CountAsync(
            item => item.EventId == eventId && item.SignupQuestionId == question.Id && item.ReleasedAt == null,
            ct);
        return new(question.Id, answerCount, releaseCount, question.Version);
    }

    private static string ConfirmationMessage(SignupQuestionImpact impact) =>
        $"Reload this question before confirming: it currently has {impact.AnswerCount} saved answer(s), releases {impact.EventRegistrationReleaseCount} event registration(s), and is at version {impact.QuestionVersion}.";

    private static object QuestionAuditSnapshot(SignupQuestion question) => new
    {
        question.Id,
        question.Key,
        question.Label,
        question.HelpText,
        type = question.Type.ToString(),
        question.Required,
        question.Position,
        question.Options,
        accountRole = question.AccountAnswerRole?.ToString(),
        systemField = question.SystemField.ToString(),
        question.PublicOnSignupBoard,
        question.Active,
        question.DisabledAt,
        question.DisabledByAccountId,
        question.DisabledReason
    };
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
            var required = IsRequiredQuestion(question);
            error = required ? $"'{question.Label}' is required." : null;
            return !required;
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

    private static bool IsCaptainVolunteered(SignupQuestion? captain, IReadOnlyDictionary<Guid, string> answers) =>
        captain is not null && answers.TryGetValue(captain.Id, out var value) && bool.TryParse(value, out var volunteered) && volunteered;

    private static bool IsRequiredQuestion(SignupQuestion question) =>
        question.SystemField == SignupSystemField.CaptainVolunteer || question.Required;

    private static bool IsRequiredAccountQuestion(SignupQuestion question) =>
        question.SystemField == SignupSystemField.PrimaryRegularAccount && question.AccountAnswerRole == EventCharacterRole.Playing;

    private static string? AnswerForValidation(SignupQuestion question, IReadOnlyDictionary<Guid, string> answers) =>
        answers.TryGetValue(question.Id, out var value)
            ? value
            : question.SystemField == SignupSystemField.CaptainVolunteer ? "false" : null;

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
        var confirmed = await SignupParticipants(eventId).CountAsync(item => item.SignupStatus == SignupStatus.Confirmed, cancellationToken);
        if (newCap < confirmed)
            throw new InvalidOperationException($"The participant cap cannot be lower than the {confirmed} confirmed participant(s).");
        if (bingoEvent.ParticipantCap is { } currentCapacity && newCap <= currentCapacity)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }
        var before = bingoEvent.ParticipantCap;
        bingoEvent.IncreaseParticipantCap(newCap);
        var promoted = await PromoteWithinLockedEventAsync(bingoEvent, null, "system", "capacity increase", cancellationToken);
        dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), timeProvider.GetUtcNow(), null, "system", "event.capacity_increased", "event", eventId.ToString(), $"{before} → {newCap}; promoted {promoted}", eventId,
            before?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null", newCap.ToString(System.Globalization.CultureInfo.InvariantCulture)));
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

    public async Task<FinalizedRosterMutationResult> AddFinalizedRosterParticipantAsync(
        FinalizedRosterAddRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EventId == Guid.Empty || request.TeamId == Guid.Empty || request.ActorAccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorName))
            return new(false, "A current event, team, and administrator are required.");
        if (request.Role is not (TeamMembershipRole.Participant or TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain))
            return new(false, "Choose Participant, Captain, or Co-captain.");
        if (request.ParticipantId is null && request.WebsiteAccountId is null)
            return new(false, "Select an existing website account for the roster addition.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
            if (bingoEvent is null) return new(false, "The event could not be found.");
            if (await AdminAsync(request.ActorAccountId, cancellationToken) is null)
                return new(false, "Admin access is required.");

            var draft = await FinalizedPreLiveDraftAsync(bingoEvent, now, cancellationToken);
            if (draft is null)
                return new(false, "Finalized roster additions are available only before the event has ever gone Live and before the configured end time.");

            var team = await dbContext.Teams
                .FromSqlInterpolated($"SELECT * FROM teams WHERE id = {request.TeamId} AND event_id = {request.EventId} AND active FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (team is null) return new(false, "The selected roster team could not be found.");
            if (request.ExpectedTeamVersion is not { } expectedTeamVersion)
                return new(false, "The current roster team version is required. Reload before adding a participant.");
            if (team.Version != expectedTeamVersion)
                return new(false, "This roster team changed elsewhere. Reload before adding a participant.");

            Account? owner;
            EventParticipant? participant;
            if (request.ParticipantId is { } participantId)
            {
                participant = await dbContext.EventParticipants
                    .FromSqlInterpolated($"SELECT * FROM event_participants WHERE id = {participantId} AND event_id = {request.EventId} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken);
                if (participant is null) return new(false, "The selected participant could not be found for this event.");
                if (participant.AccountId is not { } ownerId)
                    return new(false, "Roster additions cannot create or transfer ownership for an accountless participant.");
                if (request.WebsiteAccountId is { } requestedOwnerId && requestedOwnerId != ownerId)
                    return new(false, "The selected website account does not own this participant.");
                owner = await dbContext.Accounts
                    .FromSqlInterpolated($"SELECT * FROM accounts WHERE id = {ownerId} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken);
            }
            else
            {
                owner = await dbContext.Accounts
                    .FromSqlInterpolated($"SELECT * FROM accounts WHERE id = {request.WebsiteAccountId!.Value} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken);
                participant = await dbContext.EventParticipants
                    .FromSqlInterpolated($"SELECT * FROM event_participants WHERE event_id = {request.EventId} AND account_id = {request.WebsiteAccountId!.Value} FOR UPDATE")
                    .SingleOrDefaultAsync(cancellationToken);
            }

            if (owner is null || !owner.Active || owner.AccountType != AccountType.WebsiteAccount)
                return new(false, "The selected owner must be an active website account.");
            if (participant is not null && participant.AccountId != owner.Id)
                return new(false, "The selected website account does not own this participant.");

            if (participant is not null && await dbContext.TeamMemberships.AnyAsync(x => x.EventParticipantId == participant.Id && x.LeftAt == null, cancellationToken))
                return new(false, "This participant is already on a current roster team.");

            var participantWasReused = participant is not null;
            var participantBefore = participant is null
                ? null
                : await FinalizedParticipantAuditSnapshotAsync(participant.Id, request.EventId, cancellationToken);

            var activeQuestions = await dbContext.SignupQuestions
                .Where(x => x.EventId == request.EventId && x.Active)
                .ToListAsync(cancellationToken);
            var primaryDefinitions = activeQuestions
                .Where(x => x.SystemField == SignupSystemField.PrimaryRegularAccount)
                .ToList();
            if (primaryDefinitions.Count != 1 || primaryDefinitions[0].Type != SignupQuestionType.Account ||
                primaryDefinitions[0].AccountAnswerRole != EventCharacterRole.Playing || !primaryDefinitions[0].Required)
                return new(false, "The event has no active required Playing account field.");
            var primaryQuestion = primaryDefinitions[0];
            var activeAccountRoles = activeQuestions
                .Where(x => x.Type == SignupQuestionType.Account && x.AccountAnswerRole is not null)
                .ToDictionary(x => x.Id, x => x.AccountAnswerRole!.Value);

            var currentAssignments = participant is null
                ? []
                : await dbContext.EventParticipantCharacters
                    .Where(x => x.EventParticipantId == participant.Id && x.EventId == request.EventId && x.ReleasedAt == null)
                    .OrderBy(x => x.RegistrationOrder)
                    .ToListAsync(cancellationToken);
            if (currentAssignments.Any(x => x.SignupQuestionId is not { } questionId ||
                                            !activeAccountRoles.TryGetValue(questionId, out var role) || role != x.EventRole))
                return new(false, "The selected participant has an unresolved active account assignment. Resolve it before adding them to a finalized roster.");
            var primaryAssignments = currentAssignments.Where(x => x.SignupQuestionId == primaryQuestion.Id && x.EventRole == EventCharacterRole.Playing).ToList();
            if (primaryAssignments.Count > 1)
                return new(false, "The selected participant has multiple active primary Playing accounts. Resolve that ambiguity before adding them to a finalized roster.");
            var currentPlaying = primaryAssignments.SingleOrDefault();
            var eligiblePlayingAccounts = await (from link in dbContext.AccountOsrsCharacters
                                                 join character in dbContext.OsrsCharacters on link.OsrsCharacterId equals character.Id
                                                 where link.AccountId == owner.Id && link.Active
                                                 orderby link.Preferred descending, link.Position, link.Id
                                                 select new { link.OsrsCharacterId, link.SavedEhb, character.DisplayName }).ToListAsync(cancellationToken);
            var selectedPlayingAccount = request.PlayingCharacterId is { } requestedCharacterId
                ? eligiblePlayingAccounts.FirstOrDefault(character => character.OsrsCharacterId == requestedCharacterId)
                : eligiblePlayingAccounts.Count == 1 ? eligiblePlayingAccounts[0] : null;
            if (request.PlayingCharacterId is { } requestedId && eligiblePlayingAccounts.Count(character => character.OsrsCharacterId == requestedId) != 1)
                selectedPlayingAccount = null;
            if (selectedPlayingAccount is null)
                return new(false, eligiblePlayingAccounts.Count == 0
                    ? "The selected website account has no eligible linked Playing account."
                    : request.PlayingCharacterId is not null
                        ? "The selected Playing account is not an active linked account for this website account."
                        : "Select which linked Playing account to add to the roster.");
            var selectedEhb = request.PlayingEhb ?? selectedPlayingAccount.SavedEhb;
            if (selectedEhb is not { } validEhb || validEhb < 0 || validEhb > 100000)
                return new(false, "Enter a valid EHB for the selected Playing account.");
            var existingParticipantId = participant?.Id;
            if (await dbContext.EventParticipantCharacters.AnyAsync(x => x.EventId == request.EventId && x.OsrsCharacterId == selectedPlayingAccount.OsrsCharacterId && x.ReleasedAt == null && (existingParticipantId == null || x.EventParticipantId != existingParticipantId.Value), cancellationToken))
                return new(false, "The selected Playing account is already registered for this event.");
            if (participant is not null && currentAssignments.Any(x => x.OsrsCharacterId == selectedPlayingAccount.OsrsCharacterId && x.Id != currentPlaying?.Id))
                return new(false, "The selected Playing account is already assigned to another active account field for this participant.");
            var primaryAnswers = participant is null
                ? []
                : await dbContext.SignupAnswers
                    .Where(x => x.EventParticipantId == participant.Id && x.SignupQuestionId == primaryQuestion.Id)
                    .ToListAsync(cancellationToken);
            if (primaryAnswers.Count > 1 || primaryAnswers.Any(x => x.OsrsCharacterId is null))
                return new(false, "The selected participant has an unresolved primary account answer. Resolve it before adding them to a finalized roster.");
            var existingPrimaryAnswer = primaryAnswers.SingleOrDefault();

            if (participant is null)
            {
                var sequence = (await dbContext.EventParticipants.Where(x => x.EventId == request.EventId).MaxAsync(x => (long?)x.SignupSequence, cancellationToken) ?? 0) + 1;
                participant = new EventParticipant(Guid.NewGuid(), request.EventId, SignupStatus.Confirmed, sequence, now, SignupSource.AdminCreated);
                participant.AssignOwner(owner);
                dbContext.EventParticipants.Add(participant);
            }
            else if (participant.SignupStatus == SignupStatus.Withdrawn)
            {
                var sequence = (await dbContext.EventParticipants.Where(x => x.EventId == request.EventId).MaxAsync(x => (long?)x.SignupSequence, cancellationToken) ?? 0) + 1;
                participant.Rejoin(SignupStatus.Confirmed, sequence, now);
            }
            else if (participant.SignupStatus == SignupStatus.WaitingList)
            {
                participant.Promote(now);
            }
            else if (participant.SignupStatus != SignupStatus.Confirmed)
            {
                return new(false, "The selected participant is not eligible for a finalized roster.");
            }

            var selectedParticipant = participant!;
            var selectedCharacter = selectedPlayingAccount;
            if (currentPlaying is null)
            {
                var order = (await dbContext.EventParticipantCharacters.Where(x => x.EventParticipantId == selectedParticipant.Id).MaxAsync(x => (int?)x.RegistrationOrder, cancellationToken) ?? -1) + 1;
                dbContext.EventParticipantCharacters.Add(new EventParticipantCharacter(
                    Guid.NewGuid(), request.EventId, selectedParticipant.Id, selectedCharacter.OsrsCharacterId, order, now, request.ActorAccountId,
                    primaryQuestion.Id, EventCharacterRole.Playing, validEhb, EhbSource.Manual, null));
                if (existingPrimaryAnswer is null)
                    dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), selectedParticipant.Id, primaryQuestion.Id, primaryQuestion.Label, string.Empty, selectedCharacter.OsrsCharacterId));
                else
                    existingPrimaryAnswer.SetAccountCharacter(selectedCharacter.OsrsCharacterId);
            }
            else
            {
                currentPlaying.ReplaceCharacter(selectedCharacter.OsrsCharacterId);
                currentPlaying.UpdatePlayingEhb(validEhb, EhbSource.Manual);
                if (existingPrimaryAnswer is null)
                    dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), selectedParticipant.Id, primaryQuestion.Id, primaryQuestion.Label, string.Empty, selectedCharacter.OsrsCharacterId));
                else
                    existingPrimaryAnswer.SetAccountCharacter(selectedCharacter.OsrsCharacterId);
            }

            var membership = new TeamMembership(Guid.NewGuid(), team.Id, participant.Id, request.Role, now, null, "Finalized roster addition");
            membership.SetSource(TeamMembershipSource.RetainedConversion);
            dbContext.TeamMemberships.Add(membership);
            var before = await CurrentFinalizedRosterSnapshotAsync(draft, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await RepublishFinalizedRosterAsync(bingoEvent, draft, request.ActorAccountId, now, "Finalized roster addition", cancellationToken);
            var after = await CurrentFinalizedRosterSnapshotAsync(draft, cancellationToken);
            var participantAfter = await FinalizedParticipantAuditSnapshotAsync(participant.Id, request.EventId, cancellationToken);
            dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, request.ActorAccountId, request.ActorName,
                "roster.finalized_added", "membership", membership.Id.ToString(),
                Json(new
                {
                    localMutation = participantWasReused ? "reused-participant" : "created-participant",
                    ownerAccountId = owner.Id,
                    participantBefore,
                    participantAfter
                }), request.EventId, before, after));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(CancellationToken.None);

            var wom = await QueueFinalizedRosterWomAsync(request.EventId);
            await RecordFinalizedRosterWomAuditAsync(request.EventId, request.ActorAccountId, request.ActorName, "roster.finalized_added", membership.Id, wom);
            var count = await dbContext.TeamMemberships.AsNoTracking().CountAsync(x => x.TeamId == team.Id && x.LeftAt == null, CancellationToken.None);
            var targetTeamSize = draft.TargetTeamSize > 0 ? draft.TargetTeamSize : bingoEvent.ExpectedTeamSize;
            return new(true, Changed: true, ParticipantId: participant.Id, MembershipId: membership.Id, TeamName: team.Name,
                CurrentTeamMemberCount: count, WomSyncStatus: wom.Status, WomSyncError: wom.Error,
                TargetTeamSize: targetTeamSize, TeamIsShort: targetTeamSize is > 0 && count < targetTeamSize);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "This roster changed elsewhere. Reload before adding a participant.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "The participant or Playing account is already registered for this event.");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "The finalized roster addition could not be saved. No changes were applied. Reload and try again.");
        }
    }

    public async Task<FinalizedRosterMutationResult> RemoveFinalizedRosterParticipantAsync(
        FinalizedRosterRemoveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EventId == Guid.Empty || request.ParticipantId == Guid.Empty || request.ActorAccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorName))
            return new(false, "A current event, participant, and administrator are required.");
        if (!request.Confirmed)
            return new(false, "Confirm the finalized roster removal before continuing.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
            if (bingoEvent is null) return new(false, "The event could not be found.");
            if (await AdminAsync(request.ActorAccountId, cancellationToken) is null)
                return new(false, "Admin access is required.");
            var draft = await FinalizedPreLiveDraftAsync(bingoEvent, now, cancellationToken);
            if (draft is null)
                return new(false, "Finalized roster removals are available only before the event has ever gone Live and before the configured end time.");

            var participant = await dbContext.EventParticipants
                .FromSqlInterpolated($"SELECT * FROM event_participants WHERE id = {request.ParticipantId} AND event_id = {request.EventId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (participant is null) return new(false, "The participant could not be found.");
            if (participant.SignupStatus != SignupStatus.Confirmed)
                return new(false, "Only a confirmed current roster participant can be removed.");

            var membership = await dbContext.TeamMemberships
                .FromSqlInterpolated($"SELECT * FROM team_memberships WHERE event_participant_id = {request.ParticipantId} AND left_at IS NULL FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (membership is null) return new(false, "The participant has no current roster membership.");
            if (request.ExpectedMembershipVersion is not { } expectedMembershipVersion)
                return new(false, "The current roster membership version is required. Reload before removing it.");
            if (membership.Version != expectedMembershipVersion)
                return new(false, "This roster membership changed elsewhere. Reload before removing it.");
            var team = await dbContext.Teams
                .FromSqlInterpolated($"SELECT * FROM teams WHERE id = {membership.TeamId} AND event_id = {request.EventId} AND active FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (team is null) return new(false, "The participant's current roster team is no longer available.");

            var before = await CurrentFinalizedRosterSnapshotAsync(draft, cancellationToken);
            var participantBefore = await FinalizedParticipantAuditSnapshotAsync(participant.Id, request.EventId, cancellationToken);
            var previousRole = membership.Role;
            membership.Leave(now, "Finalized roster removal");
            if (previousRole is TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain)
            {
                membership.ChangeRole(TeamMembershipRole.Participant);
                dbContext.TeamMembershipRoleTransitions.Add(new TeamMembershipRoleTransition(
                    Guid.NewGuid(), membership.Id, previousRole, TeamMembershipRole.Participant, request.ActorAccountId, now));
            }
            else
            {
                membership.AdvanceVersion();
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            await RepublishFinalizedRosterAsync(bingoEvent, draft, request.ActorAccountId, now, "Finalized roster removal", cancellationToken);
            var after = await CurrentFinalizedRosterSnapshotAsync(draft, cancellationToken);
            var participantAfter = await FinalizedParticipantAuditSnapshotAsync(participant.Id, request.EventId, cancellationToken);
            dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, request.ActorAccountId, request.ActorName,
                "roster.finalized_removed", "membership", membership.Id.ToString(),
                Json(new
                {
                    localMutation = "removed-membership-retained-participant",
                    ownerAccountId = participant.AccountId,
                    participantBefore,
                    participantAfter
                }), request.EventId, before, after));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(CancellationToken.None);

            var wom = await QueueFinalizedRosterWomAsync(request.EventId);
            await RecordFinalizedRosterWomAuditAsync(request.EventId, request.ActorAccountId, request.ActorName, "roster.finalized_removed", membership.Id, wom);
            var count = await dbContext.TeamMemberships.AsNoTracking().CountAsync(x => x.TeamId == team.Id && x.LeftAt == null, CancellationToken.None);
            var targetTeamSize = draft.TargetTeamSize > 0 ? draft.TargetTeamSize : bingoEvent.ExpectedTeamSize;
            return new(true, Changed: true, ParticipantId: participant.Id, MembershipId: membership.Id, TeamName: team.Name,
                CurrentTeamMemberCount: count, WomSyncStatus: wom.Status, WomSyncError: wom.Error,
                TargetTeamSize: targetTeamSize, TeamIsShort: targetTeamSize is > 0 && count < targetTeamSize);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "This roster changed elsewhere. Reload before removing the participant.");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "The finalized roster removal could not be saved. No changes were applied. Reload and try again.");
        }
    }

    public Task<ParticipantLifecycleResult> WithdrawAsync(Guid eventId, Guid participantId, Guid? actorAccountId, string actorName, bool byAdmin, string? privateNote = null, CancellationToken cancellationToken = default)
        => WithdrawAsync(eventId, participantId, actorAccountId, actorName, byAdmin, privateNote, null, cancellationToken);

    public async Task<ParticipantLifecycleResult> WithdrawAsync(Guid eventId, Guid participantId, Guid? actorAccountId, string actorName, bool byAdmin, string? privateNote, long? expectedMembershipVersion, CancellationToken cancellationToken = default)
    {
        if (byAdmin && actorAccountId is { } postDraftActor)
        {
            var phase = await dbContext.Events.AsNoTracking()
                .Where(x => x.Id == eventId && x.HiddenAt == null)
                .Select(x => new { x.State, x.DraftLocked }).SingleOrDefaultAsync(cancellationToken);
            if (phase is { State: EventState.SignupClosed, DraftLocked: true })
            {
                var result = await RemoveFinalizedRosterParticipantAsync(new FinalizedRosterRemoveRequest(
                    eventId, participantId, postDraftActor, actorName, Confirmed: true, ExpectedMembershipVersion: expectedMembershipVersion), cancellationToken);
                return new(result.Succeeded, result.Error, null, null, result.Changed);
            }
            if (phase is { State: EventState.Live })
            {
                return new(false, "Roster membership is fixed after the event first goes Live.");
            }
        }
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var bingoEvent = await LockEventAsync(eventId, cancellationToken);
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.EventId == eventId && x.Id == participantId, cancellationToken);
        if (bingoEvent is null || participant is null) return new(false, "The participant could not be found.");
        if (byAdmin && (actorAccountId is null || await AdminAsync(actorAccountId.Value, cancellationToken) is null)) return new(false, "Admin access is required.");
        var lifecycleStateAllowed = byAdmin
            ? CanAdministerParticipants(bingoEvent)
            : bingoEvent.State is Domain.Events.EventState.SignupOpen or Domain.Events.EventState.SignupClosed;
        if (bingoEvent.DraftLocked || !lifecycleStateAllowed) return new(false, "Participant lifecycle changes are locked because the draft has started or the event has moved on.");
        if (!byAdmin && (participant.AccountId != actorAccountId || actorAccountId is null)) return new(false, "You cannot withdraw this participant.");
        if (participant.SignupStatus == SignupStatus.Withdrawn) return new(true, null, SignupStatus.Withdrawn, null, false);
        var activeMemberships = await dbContext.TeamMemberships
            .Where(x => x.EventParticipantId == participantId && x.LeftAt == null)
            .ToListAsync(cancellationToken);
        var prior = participant.SignupStatus;
        var now = timeProvider.GetUtcNow();
        if (byAdmin && Clean(privateNote) is { } addition)
        {
            var before = participant.AdminNotes;
            var combined = string.IsNullOrEmpty(before) ? addition : before + "\n" + addition;
            if (combined.Length > 2_000)
                return new(false, "The withdrawal note and existing Admin notes must total 2,000 characters or fewer. Shorten the note or withdraw without adding one.");
            participant.SetAdminNotes(combined);
            dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actorAccountId, actorName,
                "participant.admin_note_updated", "participant", participant.Id.ToString(), "Private Admin note changed.", eventId,
                Json(new { present = before is not null }), Json(new { present = true })));
        }
        foreach (var row in activeMemberships)
        {
            var previousRole = row.Role;
            if (previousRole is TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain)
            {
                row.ChangeRole(TeamMembershipRole.Participant);
                dbContext.TeamMembershipRoleTransitions.Add(new TeamMembershipRoleTransition(
                    Guid.NewGuid(), row.Id, previousRole, TeamMembershipRole.Participant, actorAccountId, now));
            }
            row.Leave(now, byAdmin ? "Admin withdrawal" : "Participant withdrawal");
        }
        var active = await dbContext.EventParticipantCharacters.Where(x => x.EventParticipantId == participantId && x.ReleasedAt == null).ToListAsync(cancellationToken);
        foreach (var assignment in active) assignment.Release(actorAccountId, now);
        participant.Withdraw(now, byAdmin ? "Admin withdrawal" : "Participant withdrawal", byAdmin ? actorAccountId : null);
        AddAudit(actorAccountId, actorName, byAdmin ? "participant.admin_withdrawn" : "participant.withdrawn", participant, bingoEvent.Id, prior.ToString(), SignupStatus.Withdrawn.ToString());
        if (byAdmin && participant.AccountId is { } owner) AddNotification(owner, "participant.withdrawn", "Your signup was withdrawn by an administrator.", Route(bingoEvent, participant.Id), now, bingoEvent.Id);
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

    public Task<LiveParticipantResult> WithdrawLiveAsync(LiveWithdrawalRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new LiveParticipantResult(false, "Roster membership is fixed after the event first goes Live."));

    private async Task<LiveParticipantResult> WithdrawPostDraftAsync(LiveWithdrawalRequest request, bool preLive, string? privateNote, CancellationToken cancellationToken)
    {
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
            var admin = await AdminAsync(request.ActorAccountId, cancellationToken);
            if (bingoEvent is null) return new(false, "The event could not be found.");
            if (admin is null) return new(false, "Admin access is required.");
            if (preLive)
            {
                await dbContext.Entry(bingoEvent).ReloadAsync(cancellationToken);
                now = timeProvider.GetUtcNow().ToUniversalTime();
            }
            var draft = preLive ? await FinalizedPreLiveDraftAsync(bingoEvent, now, cancellationToken) : null;
            if (preLive ? draft is null : bingoEvent.State != EventState.Live || !bingoEvent.DraftLocked)
                return new(false, preLive
                    ? "Roster corrections require a finalized draft before the event starts and a future event end."
                    : "Live withdrawal is available only while the event is live.");

            var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.EventId == request.EventId && x.Id == request.ParticipantId, cancellationToken);
            if (participant is null) return new(false, "The participant could not be found.");
            if (preLive) await dbContext.Entry(participant).ReloadAsync(cancellationToken);
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

            if (preLive && Clean(privateNote) is { } addition)
            {
                var before = participant.AdminNotes;
                var combined = string.IsNullOrEmpty(before) ? addition : before + "\n" + addition;
                if (combined.Length > 2_000)
                    return new(false, "The withdrawal note and existing Admin notes must total 2,000 characters or fewer. Shorten the note or withdraw without adding one.");
                participant.SetAdminNotes(combined);
                dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, request.ActorAccountId, request.ActorName,
                    "participant.admin_note_updated", "participant", participant.Id.ToString(), "Private Admin note changed.", request.EventId,
                    Json(new { present = before is not null }), Json(new { present = true })));
            }

            var eligibilityEndsAt = preLive ? now : NextWholeUtcMinute(now);
            var formerRole = membership.Role;
            participant.Withdraw(now, "Admin withdrawal", request.ActorAccountId, eligibilityEndsAt);
            if (formerRole is TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain)
            {
                membership.ChangeRole(TeamMembershipRole.Participant);
                dbContext.TeamMembershipRoleTransitions.Add(new TeamMembershipRoleTransition(
                    Guid.NewGuid(), membership.Id, formerRole, TeamMembershipRole.Participant, request.ActorAccountId, now));
            }
            membership.Leave(now, preLive ? "Pre-Live Admin withdrawal" : "Live Admin withdrawal");
            dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, request.ActorAccountId, request.ActorName,
                preLive ? "participant.admin_withdrawn" : "participant.live_withdrawn", "participant", participant.Id.ToString(), null, request.EventId,
                Json(new { status = SignupStatus.Confirmed.ToString(), membershipId = membership.Id, role = formerRole.ToString() }),
                Json(new { status = SignupStatus.Withdrawn.ToString(), eligibilityEndsAt, membershipEndedAt = now, vacancy = true })));

            var participantName = await dbContext.PrimaryCharacters().Where(x => x.ParticipantId == participant.Id).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken) ?? "Participant";
            var recipients = await LiveLeadershipAndAdminRecipientsAsync(request.EventId, membership.TeamId, request.ParticipantId, cancellationToken);
            var detail = $"{bingoEvent.Name}: {participantName} withdrew from {team.Name}. The vacancy is open for Admin follow-up.";
            var adminRecipients = await dbContext.Accounts.AsNoTracking()
                .Where(account => recipients.Contains(account.Id) && account.Active && account.AccountType == AccountType.WebsiteAccount &&
                                  (account.GlobalRole == GlobalRole.Admin || account.GlobalRole == GlobalRole.SuperAdmin))
                .Select(account => account.Id).ToListAsync(cancellationToken);
            foreach (var recipient in recipients)
            {
                var route = adminRecipients.Contains(recipient)
                    ? $"/Admin/Events/Participant/{request.EventId}/Participants/{participant.Id}"
                    : $"/Events/{Uri.EscapeDataString(bingoEvent.Slug)}/Teams";
                await AddNotificationOnceAsync("live-withdrawal", membership.Id, recipient, preLive ? "participant.prelive_withdrawn" : "participant.live_withdrawn", detail, route, now, request.EventId, cancellationToken);
            }
            if (preLive && participant.AccountId is { } ownerId)
                await AddNotificationOnceAsync("prelive-withdrawal-owner", membership.Id, ownerId, "participant.withdrawn",
                    "Your signup was withdrawn by an administrator.", $"/Events/{Uri.EscapeDataString(bingoEvent.Slug)}/Teams", now, request.EventId, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            if (preLive) await RepublishFinalizedRosterAsync(bingoEvent, draft!, request.ActorAccountId, now, "Pre-Live participant withdrawal", cancellationToken);
            await tx.CommitAsync(CancellationToken.None);
            return new(true, Changed: true, MembershipId: membership.Id, ParticipantId: participant.Id, EffectiveAtUtc: eligibilityEndsAt);
        }
        catch (Exception exception) when (IsExpectedConflict(exception))
        {
            await tx.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return new(false, "Another administrator changed this participant or vacancy first. Reload and try again.");
        }
        catch (Exception) when (preLive && !cancellationToken.IsCancellationRequested)
        {
            await tx.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "The roster correction could not be saved. No changes were applied. Reload and try again.");
        }
    }

    public async Task<LiveParticipantResult> ReplaceVacancyAsync(LiveReplacementRequest request, CancellationToken cancellationToken = default)
    {
        return new(false, "Roster replacements and vacancies are retired; finalized roster Add and Remove are available only before the event first goes Live.");
#pragma warning disable CS0162
        if ((request.WaitingParticipantId is null) == (request.InternalParticipant is null))
            return new(false, "Choose one available waiting-list participant or provide an internal replacement.");
        var validation = await PrevalidateReplacementNamesAsync(request, cancellationToken);
        if (validation.Failure is not null) return validation.Failure;
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var preLive = false;
        try
        {
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
            var admin = await AdminAsync(request.ActorAccountId, cancellationToken);
            if (bingoEvent is null) return new(false, "The event could not be found.");
            if (admin is null) return new(false, "Admin access is required.");
            preLive = bingoEvent.State != EventState.Live;
            if (preLive)
            {
                await dbContext.Entry(bingoEvent).ReloadAsync(cancellationToken);
                now = timeProvider.GetUtcNow().ToUniversalTime();
            }
            var draft = preLive ? await FinalizedPreLiveDraftAsync(bingoEvent, now, cancellationToken) : null;
            if (preLive ? draft is null : !bingoEvent.DraftLocked)
                return new(false, "Replacement requires Live or a finalized draft before the event starts and a future event end.");

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
                var currentNames = await (from assignment in dbContext.EventParticipantCharacters
                                          join character in dbContext.OsrsCharacters on assignment.OsrsCharacterId equals character.Id
                                          where assignment.EventParticipantId == replacement.Id && assignment.EventId == request.EventId && assignment.ReleasedAt == null
                                          select character.DisplayName).ToListAsync(cancellationToken);
                if (validation.Names is not null && !SameNames(validation.Names, currentNames))
                    return new(false, "The replacement accounts changed while you were editing it. Reload and try again.");
                replacement.Promote(now);
            }
            else
            {
                var created = await CreateInternalReplacementAsync(request.InternalParticipant!, request.EventId, bingoEvent, now, cancellationToken);
                if (created.Error is not null) return new(false, created.Error);
                replacement = created.Participant!;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var primaryId = await dbContext.PrimaryCharacters()
                .Where(x => x.ParticipantId == replacement.Id && x.EventId == request.EventId)
                .Select(x => (Guid?)x.OsrsCharacterId).SingleOrDefaultAsync(cancellationToken);
            if (primaryId is null) return new(false, "The replacement needs an active Playing account.");
            if (await dbContext.TeamMemberships.AnyAsync(x => x.EventParticipantId == replacement.Id && x.LeftAt == null, cancellationToken))
                return new(false, "The selected replacement is already on a team.");

            var membership = new TeamMembership(Guid.NewGuid(), team.Id, replacement.Id, TeamMembershipRole.Participant, now, null,
                preLive ? "Pre-Live roster replacement" : "Live roster replacement");
            membership.SetSource(TeamMembershipSource.Replacement, vacancy.Id);
            dbContext.TeamMemberships.Add(membership);
            var effectiveAt = preLive ? now : NextWholeUtcMinute(now);
            if (!preLive)
                dbContext.EventParticipantCharacterSwaps.Add(new EventParticipantCharacterSwap(
                    Guid.NewGuid(), request.EventId, replacement.Id, null, primaryId.Value,
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
                preLive ? "participant.prelive_replaced" : "participant.live_replaced", "membership", membership.Id.ToString(),
                string.IsNullOrWhiteSpace(request.WomValidationConfirmationToken) && string.IsNullOrWhiteSpace(request.InternalParticipant?.WomValidationConfirmationToken)
                    ? null : "Replacement saved after explicitly confirming a Wise Old Man operational failure.", request.EventId,
                Json(new { vacancyMembershipId = vacancy.Id, departedParticipantId = departed.Id }),
                Json(new { replacementParticipantId = replacement.Id, replacementName, effectiveAt, source = waitingReplacement ? "WaitingList" : "Internal" })));

            var recipients = await LiveLeadershipAndAdminRecipientsAsync(request.EventId, team.Id, null, cancellationToken);
            if (replacement.AccountId is { } replacementAccount) recipients.Add(replacementAccount);
            var detail = $"{bingoEvent.Name}: {replacementName} joined {team.Name} as a replacement for {departedName}.";
            foreach (var recipient in recipients.Distinct())
                await AddNotificationOnceAsync("live-replacement", membership.Id, recipient, preLive ? "participant.prelive_replaced" : "participant.live_replaced", detail, $"/Events/{Uri.EscapeDataString(bingoEvent.Slug)}/Teams", now, bingoEvent.Id, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            if (preLive) await RepublishFinalizedRosterAsync(bingoEvent, draft!, request.ActorAccountId, now, "Pre-Live vacancy replacement", cancellationToken);
            await tx.CommitAsync(CancellationToken.None);
            return new(true, Changed: true, MembershipId: membership.Id, ParticipantId: replacement.Id, EffectiveAtUtc: effectiveAt, FollowUpId: followUp?.Id);
        }
        catch (Exception exception) when (IsExpectedConflict(exception))
        {
            await tx.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            return new(false, "Another administrator filled this vacancy or changed the replacement first. Reload and try again.");
        }
        catch (Exception) when (preLive && !cancellationToken.IsCancellationRequested)
        {
            await tx.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "The roster correction could not be saved. No changes were applied. Reload and try again.");
        }
    }
#pragma warning restore CS0162

    private async Task<ReplacementPrevalidation> PrevalidateReplacementNamesAsync(LiveReplacementRequest request, CancellationToken ct)
    {
        var actorIsAdmin = await dbContext.Accounts.AsNoTracking().AnyAsync(
            x => x.Id == request.ActorAccountId && x.Active && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin), ct);
        if (!actorIsAdmin) return new(new(false, "Admin access is required."), null);
        var bingoEvent = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.EventId && x.HiddenAt == null, ct);
        if (bingoEvent is null) return new(new(false, "The event could not be found."), null);
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var preLive = bingoEvent.State != EventState.Live;
        var draft = preLive ? await FinalizedPreLiveDraftAsync(bingoEvent, now, ct) : null;
        if (preLive ? draft is null : !bingoEvent.DraftLocked)
            return new(new(false, "Replacement requires Live or a finalized draft before the event starts and a future event end."), null);
        var vacancy = await dbContext.TeamMemberships.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.EndedMembershipId, ct);
        if (vacancy is null || vacancy.LeftAt is null) return new(new(false, "The selected vacancy is not available."), null);
        if (request.ExpectedVacancyVersion is { } expected && vacancy.Version != expected)
            return new(new(false, "This vacancy changed elsewhere. Reload before filling it."), null);
        if (await dbContext.TeamMemberships.AsNoTracking().AnyAsync(x => x.ReplacesMembershipId == vacancy.Id, ct))
            return new(new(false, "This vacancy has already been filled."), null);
        var team = await dbContext.Teams.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == vacancy.TeamId && x.EventId == request.EventId && x.Active, ct);
        var departed = await dbContext.EventParticipants.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == vacancy.EventParticipantId && x.EventId == request.EventId, ct);
        if (team is null || departed is null || departed.SignupStatus != SignupStatus.Withdrawn)
            return new(new(false, "The selected vacancy is not a valid live withdrawal."), null);
        if (request.WaitingParticipantId is { } waitingParticipantId)
        {
            var waitingParticipant = await dbContext.EventParticipants.AsNoTracking().SingleOrDefaultAsync(
                x => x.Id == waitingParticipantId && x.EventId == request.EventId && x.SignupStatus == SignupStatus.WaitingList, ct);
            if (waitingParticipant is null)
                return new(new(false, "Choose a participant who is currently on this event's waiting list."), null);
        }
        if (request.InternalParticipant?.OwnerAccountId is { } replacementOwnerId)
        {
            var owner = await dbContext.Accounts.AsNoTracking().SingleOrDefaultAsync(
                x => x.Id == replacementOwnerId && x.Active && x.AccountType == AccountType.WebsiteAccount, ct);
            if (owner is null) return new(new(false, "The selected owner must be an active website account."), null);
            if (await dbContext.EventParticipants.AsNoTracking().AnyAsync(x => x.EventId == request.EventId && x.AccountId == replacementOwnerId, ct))
                return new(new(false, "That website account already owns a participant in this event."), null);
        }
        else if (request.InternalParticipant is not null)
        {
            return new(new(false, "Select an active website account for the internal replacement."), null);
        }
        List<string> names;
        if (request.InternalParticipant is { } internalRequest)
        {
            names = internalRequest.AccountAnswers.Values
                .Where(answer => !string.IsNullOrWhiteSpace(answer.CharacterName))
                .Select(answer => answer.CharacterName!.Trim())
                .DistinctBy(NormalizeAccountName, StringComparer.Ordinal)
                .ToList();
        }
        else
        {
            names = await (from assignment in dbContext.EventParticipantCharacters.AsNoTracking()
                           join character in dbContext.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                           where assignment.EventId == request.EventId && assignment.EventParticipantId == request.WaitingParticipantId && assignment.ReleasedAt == null
                           select character.DisplayName).ToListAsync(ct);
            if (names.Count == 0) return new(new(false, "The waiting-list participant has no accounts to validate."), names);
        }

        var validation = await RequiredAccountValidation.ValidateAsync(new WiseOldManAccountValidationRequest(
            request.ActorAccountId,
            "participant.replacement",
            request.EventId,
            request.WaitingParticipantId,
            request.ExpectedVacancyVersion,
            names,
            true,
            request.InternalParticipant?.WomValidationConfirmationToken ?? request.WomValidationConfirmationToken,
            TargetVacancyId: request.EndedMembershipId), ct);
        return validation.CanProceed
            ? new(null, names)
            : new(new(false, ValidationFailure(validation), WomValidationConfirmationToken: validation.ConfirmationToken), names);
    }

    private sealed record ReplacementPrevalidation(LiveParticipantResult? Failure, IReadOnlyList<string>? Names);

    public async Task<PromotionFollowUpResult> CompletePromotionFollowUpAsync(Guid eventId, Guid followUpId, Guid adminAccountId, string adminName, CancellationToken cancellationToken = default)
    {
        return new(false, "Promotion follow-up is retired; finalized roster Add and Remove are the only roster corrections before Live.");
#pragma warning disable CS0162
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var bingoEvent = await LockEventAsync(eventId, cancellationToken);
        if (bingoEvent is null) return new(false, "The event could not be found.");
        if (bingoEvent.State is not (EventState.Live or EventState.AwaitingFinalReview) &&
            await FinalizedPreLiveDraftAsync(bingoEvent, timeProvider.GetUtcNow(), cancellationToken) is null)
            return new(false, "Promotion follow-up is unavailable in this event lifecycle state.");
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
#pragma warning restore CS0162
    }

    private async Task<DraftSession?> FinalizedPreLiveDraftAsync(BingoEvent bingoEvent, DateTimeOffset now, CancellationToken ct)
    {
        var draft = await dbContext.DraftSessions.SingleOrDefaultAsync(x => x.EventId == bingoEvent.Id, ct);
        if (draft is not null) await dbContext.Entry(draft).ReloadAsync(ct);
        return bingoEvent.CanCorrectFinalizedRoster(draft?.State, now) &&
            await dbContext.DraftPublicationCycles.AnyAsync(x => x.DraftSessionId == draft!.Id && x.SupersededAt == null &&
                dbContext.DraftPublicationRosters.Any(roster => roster.DraftPublicationCycleId == x.Id), ct)
                ? draft : null;
    }

    private async Task<string> CurrentFinalizedRosterSnapshotAsync(DraftSession draft, CancellationToken ct)
    {
        var cycle = await dbContext.DraftPublicationCycles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.DraftSessionId == draft.Id && x.SupersededAt == null, ct);
        if (cycle is null) return Json(new { cycleId = (Guid?)null, rows = Array.Empty<object>() });
        var rows = await dbContext.DraftPublicationRosters.AsNoTracking()
            .Where(x => x.DraftPublicationCycleId == cycle.Id)
            .OrderBy(x => x.TeamId).ThenBy(x => x.EffectivePickNumber).ThenBy(x => x.PublicCharacterName)
            .Select(x => new { x.TeamId, x.EventParticipantId, x.Role, x.EffectivePickNumber, x.PublicCharacterName })
            .ToListAsync(ct);
        return Json(new { cycleId = cycle.Id, cycle.CycleNumber, cycle.PublicationMethod, rows });
    }

    private async Task<object?> FinalizedParticipantAuditSnapshotAsync(Guid participantId, Guid eventId, CancellationToken ct)
    {
        var participant = await dbContext.EventParticipants.AsNoTracking()
            .Where(x => x.Id == participantId && x.EventId == eventId)
            .Select(x => new { x.Id, x.AccountId, Status = x.SignupStatus.ToString() })
            .SingleOrDefaultAsync(ct);
        if (participant is null) return null;

        var assignments = await (from assignment in dbContext.EventParticipantCharacters.AsNoTracking()
                                 join character in dbContext.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                                 where assignment.EventParticipantId == participantId
                                     && assignment.EventId == eventId
                                     && assignment.ReleasedAt == null
                                     && assignment.EventRole == EventCharacterRole.Playing
                                 orderby assignment.RegistrationOrder, assignment.Id
                                 select new
                                 {
                                     assignment.Id,
                                     assignment.OsrsCharacterId,
                                     character.DisplayName,
                                     assignment.EventRole,
                                     assignment.RegistrationOrder,
                                     assignment.EhbSnapshot,
                                     assignment.EhbSource,
                                     assignment.SignupQuestionId
                                 }).ToListAsync(ct);
        return new
        {
            participantId = participant.Id,
            ownerAccountId = participant.AccountId,
            status = participant.Status,
            activePlayingAssignments = assignments
        };
    }

    private async Task<(string? Status, string? Error)> QueueFinalizedRosterWomAsync(Guid eventId)
    {
        if (competitionManagement is null) return ("NotManaged", null);
        try
        {
            var result = await competitionManagement.QueueUpdateAsync(eventId, CancellationToken.None);
            var status = result.Status ?? (result.Pending ? "Pending" : result.Succeeded ? "Queued" : "Failed");
            return (status, result.Error);
        }
        catch (Exception exception)
        {
            // Local roster state has already committed. Keep the provider failure
            // visible to the caller while allowing the managed-operation worker to
            // reconcile the event later.
            return ("Failed", exception.Message);
        }
    }

    private async Task RecordFinalizedRosterWomAuditAsync(
        Guid eventId,
        Guid actorAccountId,
        string actorName,
        string action,
        Guid membershipId,
        (string? Status, string? Error) wom)
    {
        try
        {
            dbContext.ChangeTracker.Clear();
            var now = timeProvider.GetUtcNow().ToUniversalTime();
            dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actorAccountId, actorName,
                $"{action}.wom_sync", "membership", membershipId.ToString(),
                Json(new { status = wom.Status, error = wom.Error }), eventId, null,
                Json(new { localCommitted = true, womStatus = wom.Status, womError = wom.Error })));
            await dbContext.SaveChangesAsync(CancellationToken.None);
        }
        catch
        {
            // Provider status is still returned to the handler. A transient audit
            // writer failure must not undo the already committed local roster.
            dbContext.ChangeTracker.Clear();
        }
    }

    private async Task RepublishFinalizedRosterAsync(BingoEvent bingoEvent, DraftSession draft, Guid actorId, DateTimeOffset now, string reason, CancellationToken ct)
    {
        var current = await dbContext.DraftPublicationCycles.SingleAsync(x => x.DraftSessionId == draft.Id && x.SupersededAt == null, ct);
        var members = await dbContext.TeamMemberships.Where(x => x.LeftAt == null &&
            dbContext.Teams.Any(team => team.Id == x.TeamId && team.EventId == bingoEvent.Id && team.Active)).ToListAsync(ct);
        var participantIds = members.Select(x => x.EventParticipantId).Distinct().ToList();
        var names = await dbContext.PrimaryCharacters().Where(x => x.EventId == bingoEvent.Id && participantIds.Contains(x.ParticipantId))
            .ToDictionaryAsync(x => x.ParticipantId, x => x.Name, ct);
        if (participantIds.Any(id => !names.TryGetValue(id, out var name) || string.IsNullOrWhiteSpace(name)))
            throw new InvalidOperationException("Every published roster entry requires an unambiguous Playing account.");
        var picks = await dbContext.DraftPicks.Where(x => x.DraftSessionId == draft.Id && x.UndoneAt == null).ToDictionaryAsync(x => x.Id, x => x.PickNumber, ct);
        var number = await dbContext.DraftPublicationCycles.Where(x => x.DraftSessionId == draft.Id).MaxAsync(x => x.CycleNumber, ct) + 1;
        current.Supersede(now, actorId, reason);
        // The partial unique index permits only one active cycle. Retain the transaction
        // while making supersession visible before inserting its replacement.
        await dbContext.SaveChangesAsync(ct);
        var cycle = new DraftPublicationCycle(Guid.NewGuid(), draft.Id, number, now, actorId, current.PublicationMethod);
        dbContext.DraftPublicationCycles.Add(cycle);
        foreach (var member in members)
            dbContext.DraftPublicationRosters.Add(new DraftPublicationRoster(Guid.NewGuid(), cycle.Id, member.TeamId, member.EventParticipantId, member.Role,
                member.AssignedByDraftPickId is { } pickId && picks.TryGetValue(pickId, out var pickNumber) ? pickNumber : null,
                names[member.EventParticipantId]));
        bingoEvent.SetDraftRosterPublication(true);
        await dbContext.SaveChangesAsync(ct);
    }

    private async Task<string?> ValidateWaitingReplacementAsync(EventParticipant replacement, Guid eventId, Domain.Events.BingoEvent bingoEvent, CancellationToken ct)
    {
        if (replacement.EventId != eventId || replacement.SignupStatus != SignupStatus.WaitingList)
            return "Choose a participant who is currently on this event's waiting list.";
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
        var captain = questions.SingleOrDefault(x => x.SystemField == SignupSystemField.CaptainVolunteer);
        var captainVolunteered = IsCaptainVolunteered(captain, request.Answers);
        Account? owner = null;
        if (request.OwnerAccountId is not { } ownerId)
            return (null, "Select an active website account for the internal replacement.");
        owner = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == ownerId && x.Active && x.AccountType == AccountType.WebsiteAccount, ct);
        if (owner is null) return (null, "The selected owner must be an active website account.");
        if (await dbContext.EventParticipants.AnyAsync(x => x.EventId == eventId && x.AccountId == ownerId, ct))
            return (null, "That website account already owns a participant in this event.");

        var requestedCharacters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var question in questions)
        {
            if (question.Type == SignupQuestionType.Account)
            {
                request.AccountAnswers.TryGetValue(question.Id, out var answer);
                var name = answer?.CharacterName?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    if (IsRequiredAccountQuestion(question)) return (null, $"'{question.Label}' is required.");
                    continue;
                }
                if (!requestedCharacters.Add(NormalizeAccountName(name))) return (null, "Choose each account only once.");
                if (question.AccountAnswerRole == EventCharacterRole.Playing && (answer?.Ehb is null || answer.Ehb < 0))
                    return (null, $"'{question.Label}' requires EHB.");
            }
            else if (question.SystemField == SignupSystemField.CoCaptainName && !captainVolunteered) continue;
            else if (!ValidateAnswer(question, AnswerForValidation(question, request.Answers), out var error))
                return (null, error);
        }

        var sequence = (await dbContext.EventParticipants.Where(x => x.EventId == eventId).MaxAsync(x => (long?)x.SignupSequence, ct) ?? 0) + 1;
        var participant = new EventParticipant(Guid.NewGuid(), eventId, SignupStatus.Confirmed, sequence, now, SignupSource.AdminCreated);
        participant.SetCaptainVolunteer(captainVolunteered);
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
            var value = question.SystemField == SignupSystemField.CoCaptainName && !captainVolunteered
                ? null
                : request.Answers.TryGetValue(question.Id, out var answer) ? CanonicalAnswer(question, answer) : null;
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

    public async Task<ParticipantQueueMutationResult> ConfirmWaitingParticipantAsync(
        ConfirmWaitingParticipantRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EventId == Guid.Empty || request.ParticipantId == Guid.Empty || request.ActorAccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorName))
            return new(false, "A current event, participant, and administrator are required.");
        if (request.ExpectedEventVersion is null)
            return new(false, "The current event version is required. Reload before confirming the participant.");
        if (request.ExpectedResponseVersion is null)
            return new(false, "The current participant version is required. Reload before confirming the participant.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var authorizedActor = await Bingo.Infrastructure.Events.EventMutationAuthorization.GetAuthorizedActorAsync(
                dbContext, new(request.ActorAccountId, request.ActorName), cancellationToken);
            if (authorizedActor is null) return new(false, "Admin access is required.");
            var now = ParticipantAttributionLock.AtDatabasePrecision(timeProvider.GetUtcNow());
            var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
            if (bingoEvent is null) return new(false, "The event could not be found.");
            if (!CanAdministerParticipants(bingoEvent)) return new(false, "Participant administration is read-only after the draft starts.");
            if (bingoEvent.Version != request.ExpectedEventVersion.Value)
                return new(false, "The event changed while you were editing it. Reload before confirming the participant.");

            var participant = await dbContext.EventParticipants
                .FromSqlInterpolated($"SELECT * FROM event_participants WHERE id = {request.ParticipantId} AND event_id = {request.EventId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (participant is null) return new(false, "The participant could not be found.");
            if (!await DirectSignupParticipants(request.EventId).AnyAsync(x => x.Id == participant.Id, cancellationToken))
                return new(false, "That participant is managed by the finalized/direct roster workflow.");
            if (participant.ResponseVersion != request.ExpectedResponseVersion.Value)
                return new(false, "This participant changed while you were editing it. Reload before confirming it.");
            if (participant.SignupStatus == SignupStatus.Confirmed)
            {
                await transaction.CommitAsync(cancellationToken);
                return new(true, ParticipantId: participant.Id, Status: SignupStatus.Confirmed, EffectiveParticipantCap: bingoEvent.ParticipantCap, Changed: false);
            }
            if (participant.SignupStatus != SignupStatus.WaitingList)
                return new(false, "Only a waiting-list participant can be confirmed.");

            var capacity = bingoEvent.ParticipantCap;
            if (capacity is not { } currentCapacity || currentCapacity < 1)
                return new(false, "Configure a participant capacity before confirming signups.");
            var confirmed = await SignupParticipants(request.EventId).CountAsync(x => x.SignupStatus == SignupStatus.Confirmed, cancellationToken);
            var full = confirmed >= currentCapacity;
            if (full && !request.ExpandCapacityWhenFull)
                return new(false, "The event is full. Confirm the add-one-place option to confirm this participant.");
            if (!full && request.ExpandCapacityWhenFull)
                return new(false, "The add-one-place option is available only when the event is full.");

            if (full)
            {
                bingoEvent.SetParticipantCap(checked(currentCapacity + 1));
                bingoEvent.AdvanceVersion();
            }
            participant.Promote(now);
            participant.AdvanceResponseVersion();
            var actorId = authorizedActor.Id;
            AddAudit(actorId, authorizedActor.Username, "participant.admin_confirmed", participant, request.EventId,
                SignupStatus.WaitingList.ToString(), SignupStatus.Confirmed.ToString(),
                full ? "Selected confirmation added exactly one capacity place." : "Selected confirmation used an existing capacity place.");
            if (participant.AccountId is { } owner)
                AddNotification(owner, "participant.promoted", $"Your signup for {bingoEvent.Name} is confirmed.", Route(bingoEvent, participant.Id), now, bingoEvent.Id);
            var admins = await dbContext.Accounts
                .Where(x => x.Active && x.AccountType == AccountType.WebsiteAccount && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            foreach (var admin in admins)
                AddNotification(admin, "participant.promoted", $"A participant was promoted for {bingoEvent.Name} (selected admin confirmation).", $"/Admin/Events/Manage/{bingoEvent.Id}", now, bingoEvent.Id);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, ParticipantId: participant.Id, Status: SignupStatus.Confirmed,
                EffectiveParticipantCap: bingoEvent.ParticipantCap, Changed: true);
        }
        catch (Exception exception) when (IsExpectedConflict(exception) && !cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "Another administrator changed this participant or event first. Reload and try again.");
        }
    }

    public async Task<ParticipantQueueMutationResult> MoveConfirmedParticipantToWaitingAsync(
        MoveConfirmedParticipantToWaitingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EventId == Guid.Empty || request.ParticipantId == Guid.Empty || request.ActorAccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorName))
            return new(false, "A current event, participant, and administrator are required.");
        if (request.ExpectedEventVersion is null)
            return new(false, "The current event version is required. Reload before moving the participant.");
        if (request.ExpectedResponseVersion is null)
            return new(false, "The current participant version is required. Reload before moving the participant.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var authorizedActor = await Bingo.Infrastructure.Events.EventMutationAuthorization.GetAuthorizedActorAsync(
                dbContext, new(request.ActorAccountId, request.ActorName), cancellationToken);
            if (authorizedActor is null) return new(false, "Admin access is required.");
            var now = ParticipantAttributionLock.AtDatabasePrecision(timeProvider.GetUtcNow());
            var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
            if (bingoEvent is null) return new(false, "The event could not be found.");
            if (!CanAdministerParticipants(bingoEvent)) return new(false, "Participant administration is read-only after the draft starts.");
            if (bingoEvent.Version != request.ExpectedEventVersion.Value)
                return new(false, "The event changed while you were editing it. Reload before moving the participant.");

            var participant = await dbContext.EventParticipants
                .FromSqlInterpolated($"SELECT * FROM event_participants WHERE id = {request.ParticipantId} AND event_id = {request.EventId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (participant is null) return new(false, "The participant could not be found.");
            if (!await DirectSignupParticipants(request.EventId).AnyAsync(x => x.Id == participant.Id, cancellationToken))
                return new(false, "That participant is managed by the finalized/direct roster workflow.");
            if (participant.ResponseVersion != request.ExpectedResponseVersion.Value)
                return new(false, "This participant changed while you were editing it. Reload before moving it.");
            if (participant.SignupStatus == SignupStatus.WaitingList)
            {
                await transaction.CommitAsync(cancellationToken);
                return new(true, ParticipantId: participant.Id, Status: SignupStatus.WaitingList,
                    WaitingPosition: await GetWaitingPositionAsync(participant.Id, request.EventId, cancellationToken), Changed: false);
            }
            if (participant.SignupStatus != SignupStatus.Confirmed)
                return new(false, "Only a confirmed participant can be moved to the waiting list.");

            var capacity = bingoEvent.ParticipantCap;
            if (capacity is not { } currentCapacity || currentCapacity < 1)
                return new(false, "Configure a participant capacity before moving a participant.");
            var confirmed = await SignupParticipants(request.EventId).CountAsync(x => x.SignupStatus == SignupStatus.Confirmed, cancellationToken);
            if (confirmed < currentCapacity)
                return new(false, "A place is available; a confirmed participant cannot be forced onto the waiting list.");

            // Capture the next eligible waiter before appending the selected
            // participant, otherwise the mover can be immediately re-promoted.
            var next = await SignupParticipants(request.EventId)
                .Where(x => x.SignupStatus == SignupStatus.WaitingList && x.Id != participant.Id)
                .OrderBy(x => x.WaitingListedAt ?? x.SignedUpAt)
                .ThenBy(x => x.SignupSequence)
                .ThenBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (next is null)
                return new(false, "Another eligible waiting-list participant is required to fill the released place.");

            var memberships = await dbContext.TeamMemberships
                .Where(x => x.EventParticipantId == participant.Id && x.LeftAt == null)
                .ToListAsync(cancellationToken);
            foreach (var membership in memberships)
            {
                var previousRole = membership.Role;
                if (previousRole is TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain)
                {
                    membership.ChangeRole(TeamMembershipRole.Participant);
                    dbContext.TeamMembershipRoleTransitions.Add(new TeamMembershipRoleTransition(
                        Guid.NewGuid(), membership.Id, previousRole, TeamMembershipRole.Participant, authorizedActor.Id, now));
                }
                membership.Leave(now, "Admin moved participant to waiting list");
            }

            var sequence = (await dbContext.EventParticipants.Where(x => x.EventId == request.EventId)
                .MaxAsync(x => (long?)x.SignupSequence, cancellationToken) ?? 0) + 1;
            participant.MoveToWaiting(sequence, now);
            next.Promote(now);
            next.AdvanceResponseVersion();
            AddAudit(authorizedActor.Id, authorizedActor.Username, "participant.admin_moved_to_waiting", participant, request.EventId,
                SignupStatus.Confirmed.ToString(), SignupStatus.WaitingList.ToString(),
                Json(new { promotedParticipantId = next.Id, endedMembershipCount = memberships.Count }));
            AddAudit(authorizedActor.Id, authorizedActor.Username, "participant.promoted", next, request.EventId,
                SignupStatus.WaitingList.ToString(), SignupStatus.Confirmed.ToString(), "Promoted into the place vacated by a selected participant.");
            if (participant.AccountId is { } owner)
                AddNotification(owner, "participant.waiting", $"Your signup for {bingoEvent.Name} is now on the waiting list.", Route(bingoEvent, participant.Id), now, bingoEvent.Id);
            if (next.AccountId is { } nextOwner)
                AddNotification(nextOwner, "participant.promoted", $"Your signup for {bingoEvent.Name} is confirmed.", Route(bingoEvent, next.Id), now, bingoEvent.Id);
            var admins = await dbContext.Accounts
                .Where(x => x.Active && x.AccountType == AccountType.WebsiteAccount && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            foreach (var admin in admins)
                AddNotification(admin, "participant.promoted", $"A participant was promoted for {bingoEvent.Name} (selected participant moved to waiting).", $"/Admin/Events/Manage/{bingoEvent.Id}", now, bingoEvent.Id);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, ParticipantId: participant.Id, Status: SignupStatus.WaitingList,
                WaitingPosition: await GetWaitingPositionAsync(participant.Id, request.EventId, cancellationToken),
                PromotedParticipantId: next.Id, EffectiveParticipantCap: bingoEvent.ParticipantCap, Changed: true);
        }
        catch (Exception exception) when (IsExpectedConflict(exception) && !cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "Another administrator changed this participant or event first. Reload and try again.");
        }
    }

    public Task<ParticipantLifecycleResult> RejoinAsync(Guid eventId, Guid participantId, Guid accountId, string actorName, CancellationToken cancellationToken = default) =>
        RejoinAsync(eventId, participantId, accountId, actorName, null, null, cancellationToken);

    public async Task<ParticipantLifecycleResult> RejoinAsync(Guid eventId, Guid participantId, Guid accountId, string actorName, string? womValidationConfirmationToken, CancellationToken cancellationToken = default)
        => await RejoinAsync(eventId, participantId, accountId, actorName, null, womValidationConfirmationToken, cancellationToken);

    public async Task<ParticipantLifecycleResult> RejoinAsync(
        Guid eventId,
        Guid participantId,
        Guid accountId,
        string actorName,
        string? signupCode,
        string? womValidationConfirmationToken,
        CancellationToken cancellationToken = default)
    {
        var validation = await PrevalidateReacquireNamesAsync(eventId, participantId, accountId, false, null, signupCode, womValidationConfirmationToken, cancellationToken);
        if (validation.Failure is not null) return validation.Failure;
        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var bingoEvent = await LockEventAsync(eventId, cancellationToken);
        var participant = await dbContext.EventParticipants.SingleOrDefaultAsync(x => x.EventId == eventId && x.Id == participantId, cancellationToken);
        if (bingoEvent is null || participant is null) return new(false, "The participant could not be found.");
        if (participant.AccountId != accountId) return new(false, "You cannot rejoin this participant.");
        if (validation.ExpectedVersion is { } expectedVersion && participant.ResponseVersion != expectedVersion)
            return new(false, "This participant changed while you were editing it. Reload and try again.");
        if (bingoEvent.DraftLocked || !bingoEvent.AcceptsSignups(timeProvider.GetUtcNow())) return new(false, "Signup is closed. Contact an Admin if you need to be restored.");
        if (participant.SignupStatus != SignupStatus.Withdrawn) return new(true, null, participant.SignupStatus, null, false);
        if (bingoEvent.RequireSignupCode && (string.IsNullOrWhiteSpace(signupCode) || bingoEvent.SignupCodeHash is null || !secretHasher.Verify(signupCode, bingoEvent.SignupCodeHash)))
            return new(false, "The event code is incorrect.");
        var status = await AdmissionStatusAsync(bingoEvent, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (validation.Names is not null && !SameNames(validation.Names, await LoadReacquireNamesAsync(participant, cancellationToken)))
            return new(false, "The reacquired accounts changed while you were editing it. Please reload and try again.");
        if (!await ReacquireAssignmentsAsync(participant, accountId, now, cancellationToken)) return new(false, "One of your accounts is now assigned to another participant.");
        var next = (await dbContext.EventParticipants.Where(x => x.EventId == eventId).MaxAsync(x => (long?)x.SignupSequence, cancellationToken) ?? 0) + 1;
        participant.Rejoin(status, next, now);
        AddAudit(accountId, actorName, "participant.rejoined", participant, eventId, SignupStatus.Withdrawn.ToString(), status.ToString());
        await dbContext.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
        return new(true, null, status, status == SignupStatus.WaitingList ? await GetWaitingPositionAsync(participantId, eventId, cancellationToken) : null, true);
    }

    public Task<ParticipantLifecycleResult> RestoreAsync(Guid eventId, Guid participantId, Guid adminAccountId, string adminName, CancellationToken cancellationToken = default) =>
        RestoreLegacyAsync(eventId, participantId, adminAccountId, adminName, null, cancellationToken);

    public Task<ParticipantLifecycleResult> RestoreAsync(Guid eventId, Guid participantId, Guid adminAccountId, string adminName, string? womValidationConfirmationToken, CancellationToken cancellationToken = default) =>
        RestoreLegacyAsync(eventId, participantId, adminAccountId, adminName, womValidationConfirmationToken, cancellationToken);

    private async Task<ParticipantLifecycleResult> RestoreLegacyAsync(
        Guid eventId,
        Guid participantId,
        Guid adminAccountId,
        string adminName,
        string? womValidationConfirmationToken,
        CancellationToken cancellationToken)
    {
        var observed = await (from bingoEvent in dbContext.Events.AsNoTracking()
                              join participant in dbContext.EventParticipants.AsNoTracking() on bingoEvent.Id equals participant.EventId
                              where bingoEvent.Id == eventId && participant.Id == participantId
                              select new { EventVersion = bingoEvent.Version, ResponseVersion = participant.ResponseVersion })
            .SingleOrDefaultAsync(cancellationToken);
        if (observed is null) return new(false, "The participant could not be found.");
        return await RestoreAdminParticipantAsync(new(
            eventId,
            participantId,
            adminAccountId,
            adminName,
            ExpectedEventVersion: observed.EventVersion,
            ExpectedResponseVersion: observed.ResponseVersion,
            WomValidationConfirmationToken: womValidationConfirmationToken), cancellationToken);
    }

    public async Task<ParticipantLifecycleResult> RestoreAdminParticipantAsync(
        AdminParticipantRestoreRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ExpectedEventVersion is null)
            return new(false, "The current event version is required. Reload before restoring the participant.");
        if (request.ExpectedResponseVersion is null)
            return new(false, "The current participant version is required. Reload before restoring the participant.");
        var validation = await PrevalidateReacquireNamesAsync(
            request.EventId, request.ParticipantId, request.ActorAccountId, true, request.ActorAccountId,
            null, request.WomValidationConfirmationToken, cancellationToken);
        if (validation.Failure is not null) return validation.Failure;

        await using var tx = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            // The preflight is only advisory. Recheck the actor while holding its
            // row lock before locking the event and changing roster state.
            var authorizedActor = await Bingo.Infrastructure.Events.EventMutationAuthorization.GetAuthorizedActorAsync(
                dbContext, new(request.ActorAccountId, request.ActorName), cancellationToken);
            if (authorizedActor is null) return new(false, "Admin access is required.");
            var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
            var participant = await dbContext.EventParticipants
                .FromSqlInterpolated($"SELECT * FROM event_participants WHERE event_id = {request.EventId} AND id = {request.ParticipantId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (bingoEvent is null || participant is null) return new(false, "The participant could not be found.");
            if (!CanAdministerParticipants(bingoEvent)) return new(false, "Participant lifecycle changes are locked because the draft has started or the event has moved on.");
            if (bingoEvent.Version != request.ExpectedEventVersion.Value)
                return new(false, "The event changed while you were editing it. Reload before restoring the participant.");
            if (!await DirectSignupParticipants(request.EventId).AnyAsync(x => x.Id == participant.Id, cancellationToken))
                return new(false, "That participant is managed by the finalized/direct roster workflow.");
            if (validation.ExpectedVersion is { } expectedVersion && participant.ResponseVersion != expectedVersion)
                return new(false, "This participant changed while you were editing it. Reload and try again.");
            if (participant.ResponseVersion != request.ExpectedResponseVersion.Value)
                return new(false, "This participant changed while you were editing it. Reload and try again.");
            if (participant.SignupStatus != SignupStatus.Withdrawn)
                return new(true, null, participant.SignupStatus,
                    participant.SignupStatus == SignupStatus.WaitingList
                        ? await GetWaitingPositionAsync(participant.Id, request.EventId, cancellationToken) : null,
                    false);

            var now = ParticipantAttributionLock.AtDatabasePrecision(timeProvider.GetUtcNow());
            var capacity = bingoEvent.ParticipantCap;
            if (capacity is not { } currentCapacity || currentCapacity < 1)
                return new(false, "Configure a participant capacity before restoring signups.");
            var confirmed = await SignupParticipants(request.EventId).CountAsync(x => x.SignupStatus == SignupStatus.Confirmed, cancellationToken);
            var full = confirmed >= currentCapacity;
            if (full && !request.ExpandCapacityWhenFull)
            {
                // A normal restore goes to the end of the queue. It never
                // displaces a participant already promoted into a place.
            }
            else if (!full && request.ExpandCapacityWhenFull)
                return new(false, "The add-one-place option is available only when the event is full.");

            if (validation.Names is not null && !SameNames(validation.Names, await LoadReacquireNamesAsync(participant, cancellationToken)))
                return new(false, "The reacquired accounts changed while you were editing it. Please reload and try again.");
            if (!await ReacquireAssignmentsAsync(participant, authorizedActor.Id, now, cancellationToken))
                return new(false, "One of this participant's accounts is now assigned to another participant.");
            // Do not mutate the tracked event until account reacquisition has
            // passed. A rejected restore can then safely be followed by a
            // different mutation on this same DbContext.
            if (full && request.ExpandCapacityWhenFull)
            {
                bingoEvent.SetParticipantCap(checked(currentCapacity + 1));
                bingoEvent.AdvanceVersion();
            }
            var status = full && !request.ExpandCapacityWhenFull ? SignupStatus.WaitingList : SignupStatus.Confirmed;
            var next = (await dbContext.EventParticipants.Where(x => x.EventId == request.EventId)
                .MaxAsync(x => (long?)x.SignupSequence, cancellationToken) ?? 0) + 1;
            participant.Rejoin(status, next, now);
            AddAudit(authorizedActor.Id, authorizedActor.Username, "participant.admin_restored", participant, request.EventId,
                SignupStatus.Withdrawn.ToString(), status.ToString(),
                Json(new { expandedCapacity = full && request.ExpandCapacityWhenFull }));
            if (participant.AccountId is { } owner)
                AddNotification(owner, "participant.restored", "Your signup has been restored by an administrator.", Route(bingoEvent, participant.Id), now, bingoEvent.Id);
            await dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return new(true, null, status,
                status == SignupStatus.WaitingList ? await GetWaitingPositionAsync(request.ParticipantId, request.EventId, cancellationToken) : null,
                true, request.WomValidationConfirmationToken);
        }
        catch (Exception exception) when (IsExpectedConflict(exception) && !cancellationToken.IsCancellationRequested)
        {
            await tx.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return new(false, "Another administrator changed this participant or event first. Reload and try again.");
        }
    }

    private async Task<ReacquirePrevalidation> PrevalidateReacquireNamesAsync(
        Guid eventId,
        Guid participantId,
        Guid actorId,
        bool enabledAdmin,
        Guid? adminActorId,
        string? signupCode,
        string? confirmationToken,
        CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var bingoEvent = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, ct);
        var participant = await dbContext.EventParticipants.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId && x.Id == participantId, ct);
        if (bingoEvent is null || participant is null) return new(new(false, "The participant could not be found."), null, null);
        if (!enabledAdmin && participant.AccountId != actorId) return new(new(false, "You cannot rejoin this participant."), null, null);
        if (enabledAdmin)
        {
            var admin = await dbContext.Accounts.AsNoTracking().AnyAsync(x => x.Id == adminActorId && x.Active && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin), ct);
            if (!admin) return new(new(false, "Admin access is required."), null, null);
            if (!CanAdministerParticipants(bingoEvent))
                return new(new(false, "Participant lifecycle changes are locked because the draft has started or the event has moved on."), null, null);
        }
        else if (bingoEvent.DraftLocked || !bingoEvent.AcceptsSignups(now))
            return new(new(false, "Signup is closed. Contact an Admin if you need to be restored."), null, null);
        if (participant.SignupStatus != SignupStatus.Withdrawn) return new(null, null, null);

        var names = await LoadReacquireNamesAsync(participant, ct);
        if (!enabledAdmin && bingoEvent.RequireSignupCode && (string.IsNullOrWhiteSpace(signupCode) || bingoEvent.SignupCodeHash is null || !secretHasher.Verify(signupCode, bingoEvent.SignupCodeHash)))
            return new(new(false, "The event code is incorrect."), null, participant.ResponseVersion);

        var validation = await RequiredAccountValidation.ValidateAsync(new WiseOldManAccountValidationRequest(
            actorId,
            enabledAdmin ? "participant.restore" : "participant.rejoin",
            eventId,
            participantId,
            participant.ResponseVersion,
            names,
            enabledAdmin,
            confirmationToken), ct);
        return validation.CanProceed
            ? new(null, names, participant.ResponseVersion)
            : new(new(false, ValidationFailure(validation), WomValidationConfirmationToken: validation.ConfirmationToken), names, participant.ResponseVersion);
    }

    private IWiseOldManAccountValidation RequiredAccountValidation => accountValidation ??
        throw new InvalidOperationException("Wise Old Man account validation is required for account-bearing signup changes.");

    private async Task<List<string>> LoadReacquireNamesAsync(EventParticipant participant, CancellationToken ct)
    {
        var released = await dbContext.EventParticipantCharacters.AsNoTracking()
            .Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt != null)
            .OrderByDescending(x => x.RegistrationOrder).ToListAsync(ct);
        var deletedQuestions = await dbContext.SignupQuestions.AsNoTracking()
            .Where(x => x.EventId == participant.EventId && x.DisabledReason == SignupQuestion.DeletedReason)
            .Select(x => x.Id).ToListAsync(ct);
        var savedSelections = await dbContext.SignupAnswers.AsNoTracking()
            .Where(x => x.EventParticipantId == participant.Id && x.OsrsCharacterId != null)
            .ToDictionaryAsync(x => x.SignupQuestionId, x => x.OsrsCharacterId!.Value, ct);
        var legacyQuestions = await dbContext.SignupQuestions.AsNoTracking()
            .Where(x => x.EventId == participant.EventId && (x.SystemField == SignupSystemField.PrimaryRegularAccount || (!x.Active && x.Key == "legacy_alt_account")))
            .Select(x => x.Id).ToListAsync(ct);
        var originals = released.Where(x => x.SignupQuestionId is { } questionId && !deletedQuestions.Contains(questionId) &&
                (savedSelections.TryGetValue(questionId, out var characterId) ? x.OsrsCharacterId == characterId : legacyQuestions.Contains(questionId)))
            .GroupBy(x => x.SignupQuestionId).Select(x => x.First()).ToList();
        originals.AddRange(released.Where(x => x.SignupQuestionId == null && x.ReleasedAt >= participant.WithdrawnAt));
        var ids = originals.Select(x => x.OsrsCharacterId).Distinct().ToList();
        return await dbContext.OsrsCharacters.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => x.DisplayName).ToListAsync(ct);
    }

    private sealed record ReacquirePrevalidation(ParticipantLifecycleResult? Failure, IReadOnlyList<string>? Names, int? ExpectedVersion);

    private async Task<int> PromoteWithinLockedEventAsync(Domain.Events.BingoEvent bingoEvent, Guid? actorAccountId, string actorName, string trigger, CancellationToken cancellationToken)
    {
        if (bingoEvent.DraftLocked) return 0;
        var confirmed = await SignupParticipants(bingoEvent.Id).CountAsync(participant => participant.SignupStatus == SignupStatus.Confirmed, cancellationToken);
        var places = Math.Max(0, (bingoEvent.ParticipantCap ?? 0) - confirmed);
        if (places == 0) return 0;
        var waiting = await SignupParticipants(bingoEvent.Id)
            .Where(participant => participant.SignupStatus == SignupStatus.WaitingList)
            .OrderBy(participant => participant.WaitingListedAt ?? participant.SignedUpAt)
            .ThenBy(participant => participant.SignupSequence)
            .ThenBy(participant => participant.Id)
            .Take(places)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var admins = await dbContext.Accounts
            .Where(x => x.Active && x.AccountType == AccountType.WebsiteAccount && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        foreach (var participant in waiting)
        {
            participant.Promote(now);
            AddAudit(actorAccountId, actorName, "participant.promoted", participant, bingoEvent.Id, SignupStatus.WaitingList.ToString(), SignupStatus.Confirmed.ToString());
            if (participant.AccountId is { } owner) AddNotification(owner, "participant.promoted", $"Your signup for {bingoEvent.Name} is confirmed.", Route(bingoEvent, participant.Id), now, bingoEvent.Id);
            foreach (var admin in admins) AddNotification(admin, "participant.promoted", $"A participant was promoted for {bingoEvent.Name} ({trigger}).", $"/Admin/Events/Manage/{bingoEvent.Id}", now, bingoEvent.Id);
        }
        return waiting.Count;
    }

    // Capacity and waiting-list decisions apply to every event participant. A
    // manual roster membership changes draft participation, not signup capacity.
    private IQueryable<EventParticipant> SignupParticipants(Guid eventId) =>
        dbContext.EventParticipants.Where(participant => participant.EventId == eventId);

    // Selected signup administration operations still reject participants owned
    // by the finalized/direct-roster workflow. Keep that workflow distinction
    // separate from capacity counting.
    private IQueryable<EventParticipant> DirectSignupParticipants(Guid eventId) =>
        SignupParticipants(eventId).Where(participant =>
            !dbContext.TeamMemberships.Any(membership => membership.EventParticipantId == participant.Id && membership.LeftAt == null &&
                dbContext.Teams.Any(team => team.Id == membership.TeamId && team.EventId == eventId && team.Active && !team.IncludedInDraft)));

    private async Task<Domain.Events.BingoEvent?> LockEventAsync(Guid eventId, CancellationToken ct) => await dbContext.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} AND hidden_at IS NULL FOR UPDATE").SingleOrDefaultAsync(ct);
    private async Task<SignupStatus> AdmissionStatusAsync(Domain.Events.BingoEvent bingoEvent, CancellationToken ct) =>
        await SignupParticipants(bingoEvent.Id).CountAsync(x => x.SignupStatus == SignupStatus.Confirmed, ct) < (bingoEvent.ParticipantCap ?? 0) ? SignupStatus.Confirmed : SignupStatus.WaitingList;
    private async Task<bool> ReacquireAssignmentsAsync(EventParticipant participant, Guid? actorId, DateTimeOffset now, CancellationToken ct)
    {
        var released = await dbContext.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt != null).OrderByDescending(x => x.RegistrationOrder).ToListAsync(ct);
        var deletedQuestions = await dbContext.SignupQuestions.Where(x => x.EventId == participant.EventId && x.DisabledReason == SignupQuestion.DeletedReason).Select(x => x.Id).ToListAsync(ct);
        var savedSelections = await dbContext.SignupAnswers.Where(x => x.EventParticipantId == participant.Id && x.OsrsCharacterId != null)
            .ToDictionaryAsync(x => x.SignupQuestionId, x => x.OsrsCharacterId!.Value, ct);
        // The retained conversion omitted account answers. Primary is required; legacy Alt stays disabled,
        // so ordinary edits cannot clear either fallback without leaving a saved selection.
        var legacyQuestions = await dbContext.SignupQuestions.Where(x => x.EventId == participant.EventId &&
            (x.SystemField == SignupSystemField.PrimaryRegularAccount || (!x.Active && x.Key == "legacy_alt_account")))
            .Select(x => x.Id).ToListAsync(ct);
        var originals = released.Where(x => x.SignupQuestionId is { } questionId && !deletedQuestions.Contains(questionId) &&
                (savedSelections.TryGetValue(questionId, out var characterId) ? x.OsrsCharacterId == characterId : legacyQuestions.Contains(questionId)))
            .GroupBy(x => x.SignupQuestionId).Select(x => x.First()).ToList();
        // External rosters have no question/answer rows; roster removal releases after setting WithdrawnAt.
        originals.AddRange(released.Where(x => x.SignupQuestionId == null && x.ReleasedAt >= participant.WithdrawnAt));
        var ids = originals.Select(x => x.OsrsCharacterId).ToList();
        if (ids.Count != 0 && await dbContext.EventParticipantCharacters.AnyAsync(x => x.EventId == participant.EventId && ids.Contains(x.OsrsCharacterId) && x.ReleasedAt == null, ct)) return false;
        var order = (await dbContext.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id).MaxAsync(x => (int?)x.RegistrationOrder, ct) ?? -1) + 1;
        foreach (var original in originals) dbContext.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), participant.EventId, participant.Id, original.OsrsCharacterId, order++, now, actorId, original.SignupQuestionId, original.EventRole, original.EhbSnapshot, original.EhbSource, original.EhbFetchedAt));
        return true;
    }
    private void AddAudit(Guid? actorId, string actorName, string action, EventParticipant participant, Guid eventId, string before, string after, string? detail = null) => dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), timeProvider.GetUtcNow(), actorId, actorName, action, "participant", participant.Id.ToString(), detail, eventId, before, after));
    private void AddNotification(Guid recipientId, string title, string detail, string route, DateTimeOffset now, Guid eventId) => dbContext.PersonalNotifications.Add(new PersonalNotification(Guid.NewGuid(), recipientId, title, detail, route, now, eventId));
    private static string Route(Domain.Events.BingoEvent bingoEvent, Guid? participantId = null)
        => $"/Events/{Uri.EscapeDataString(bingoEvent.Slug)}/Signup/Confirmation" + (participantId is { } id ? $"?participantId={id}" : string.Empty);

    private async Task<int> GetWaitingPositionAsync(Guid participantId, Guid eventId, CancellationToken cancellationToken)
    {
        var waitingIds = await SignupParticipants(eventId).AsNoTracking()
            .Where(participant => participant.SignupStatus == SignupStatus.WaitingList)
            .OrderBy(participant => participant.WaitingListedAt ?? participant.SignedUpAt)
            .ThenBy(participant => participant.SignupSequence)
            .ThenBy(participant => participant.Id)
            .Select(participant => participant.Id)
            .ToListAsync(cancellationToken);
        return waitingIds.IndexOf(participantId) + 1;
    }

    public static string NormalizeAccountName(string value) => value.Trim().ToUpperInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
