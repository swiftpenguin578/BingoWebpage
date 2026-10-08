using System.Data;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Infrastructure.Signups;

// U5-Q2: one command saves the whole Participants drawer (accounts, primary, alt
// slots, captain and custom answers, payment, private note) in one transaction with
// stale checks; all or nothing. D16: payment and the private note stay editable in
// every retained state; accounts and answers only before the draft starts.
// U5-Q1: a typed RSN becomes an event-only account (no Wise Old Man call, never a
// saved account of the player). U5-Q4: new or renamed names follow RsnRule.
public sealed partial class SignupService
{
    public const int AdminNoteLimit = 2_000;

    public async Task<AdminParticipantDrawerSaveResult> SaveAdminParticipantDrawerAsync(
        AdminParticipantDrawerSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.EventId == Guid.Empty || request.ParticipantId == Guid.Empty || request.ActorAccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.ActorName))
            return Refused("A current event, participant, and administrator are required.");
        if (request.Payment is not (PaymentStatus.Paid or PaymentStatus.Unpaid) || request.ExpectedPayment is not (PaymentStatus.Paid or PaymentStatus.Unpaid))
            return Invalid("Choose Paid or Unpaid.", "payment");
        if (request.AdminNote?.Length > AdminNoteLimit)
            return Invalid("Use 2,000 characters or fewer.", "note");
        var editsRoster = request.Playing is not null || request.Answers is not null;
        if (editsRoster && request.ExpectedResponseVersion is null)
            return Refused("The current participant version is required. Reload before saving.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var actor = await Bingo.Infrastructure.Events.EventMutationAuthorization.GetAuthorizedActorAsync(
                dbContext, new(request.ActorAccountId, request.ActorName), cancellationToken);
            if (actor is null) return Refused("Admin access is required.");
            var bingoEvent = await LockEventAsync(request.EventId, cancellationToken);
            if (bingoEvent is null) return Refused("The event could not be found.");
            if (!CanEditPrivateParticipantMetadata(bingoEvent)) return Refused("Private participant metadata is unavailable in this event lifecycle state.");
            var participant = await dbContext.EventParticipants
                .FromSqlInterpolated($"SELECT * FROM event_participants WHERE event_id = {request.EventId} AND id = {request.ParticipantId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (participant is null) return Refused("The participant could not be found.");
            if (editsRoster)
            {
                if (!CanAdministerParticipants(bingoEvent))
                    return Refused("Accounts and signup answers are locked because the draft has started or the event has moved on. Nothing was saved.");
                if (participant.SignupStatus == SignupStatus.Withdrawn)
                    return Refused("Restore this participant before changing accounts or answers. Nothing was saved.");
                if (participant.ResponseVersion != request.ExpectedResponseVersion!.Value)
                    return Stale();
            }
            if (participant.PaymentStatus != request.ExpectedPayment ||
                !string.Equals(participant.AdminNotes ?? string.Empty, request.ExpectedAdminNote ?? string.Empty, StringComparison.Ordinal))
                return Stale();

            var now = ParticipantAttributionLock.AtDatabasePrecision(timeProvider.GetUtcNow());
            var before = editsRoster ? await DrawerStateAsync(participant, cancellationToken) : null;
            var accountsChanged = false;
            var answersChanged = false;
            if (request.Playing is not null)
            {
                var applied = await ApplyDrawerAccountsAsync(request, participant, actor.Id, now, cancellationToken);
                if (applied.Failure is not null) { dbContext.ChangeTracker.Clear(); return applied.Failure; }
                accountsChanged = applied.Changed;
            }
            if (request.Answers is not null)
            {
                var applied = await ApplyDrawerAnswersAsync(request.Answers, participant, cancellationToken);
                if (applied.Failure is not null) { dbContext.ChangeTracker.Clear(); return applied.Failure; }
                answersChanged = applied.Changed;
            }
            var paymentBefore = participant.PaymentStatus;
            var paymentChanged = paymentBefore != request.Payment;
            var noteBefore = participant.AdminNotes;
            var noteAfter = Clean(request.AdminNote);
            var noteChanged = !string.Equals(noteBefore, noteAfter, StringComparison.Ordinal);
            if (!accountsChanged && !answersChanged && !paymentChanged && !noteChanged)
            {
                await transaction.CommitAsync(cancellationToken);
                return new(true, Outcome: "saved", Changed: false);
            }
            if (paymentChanged) participant.SetPaymentStatus(request.Payment);
            if (noteChanged) participant.SetAdminNotes(noteAfter);
            if (accountsChanged || answersChanged) participant.AdvanceResponseVersion();
            // Flush inside the transaction so the audit after-state reads the new rows.
            await dbContext.SaveChangesAsync(cancellationToken);

            if (accountsChanged || answersChanged)
            {
                dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actor.Id, actor.Username, "participant.corrected", "participant",
                    participant.Id.ToString(), "Participant drawer saved.", request.EventId, before, await DrawerStateAsync(participant, cancellationToken)));
            }
            if (paymentChanged)
                dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actor.Id, actor.Username, "participant.payment_updated", "participant", participant.Id.ToString(),
                    "Payment changed.", request.EventId, $"{{\"payment\":\"{paymentBefore}\"}}", $"{{\"payment\":\"{request.Payment}\"}}"));
            if (noteChanged)
                dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), now, actor.Id, actor.Username, "participant.admin_note_updated", "participant", participant.Id.ToString(),
                    "Private Admin note changed.", request.EventId, Json(new { present = noteBefore is not null }), Json(new { present = noteAfter is not null })));
            if (accountsChanged && participant.AccountId is { } owner)
                AddNotification(owner, "participant.accounts_changed", "Your registered event accounts were updated by an administrator.", Route(bingoEvent, participant.Id), now, bingoEvent.Id);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, Outcome: "saved", Changed: true);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } && !cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return Refused("One of these accounts is already used by another participant in this event. Nothing was saved.", "accounts");
        }
        catch (Exception exception) when (IsExpectedConflict(exception) && !cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            return Stale();
        }
    }

    private static AdminParticipantDrawerSaveResult Refused(string message, string? field = null) => new(false, message, "refused", Field: field);
    private static AdminParticipantDrawerSaveResult Invalid(string message, string field) => new(false, message, "invalid", Field: field);
    private static AdminParticipantDrawerSaveResult Stale() => new(false, "This participant changed while you were editing. The current details are shown; nothing was saved.", "stale");

    private sealed record DrawerApplyResult(AdminParticipantDrawerSaveResult? Failure, bool Changed);

    private async Task<DrawerApplyResult> ApplyDrawerAccountsAsync(AdminParticipantDrawerSaveRequest request, EventParticipant participant, Guid actorId, DateTimeOffset now, CancellationToken ct)
    {
        var playing = request.Playing!;
        var informational = request.Informational ?? [];
        var questions = await dbContext.SignupQuestions
            .Where(x => x.EventId == request.EventId && x.Active && x.Type == SignupQuestionType.Account)
            .OrderBy(x => x.Position).ToListAsync(ct);
        var playingQuestions = questions.Where(x => x.AccountAnswerRole == EventCharacterRole.Playing).ToList();
        var primaryQuestions = playingQuestions.Where(x => x.SystemField == SignupSystemField.PrimaryRegularAccount).ToList();
        var infoQuestions = questions.Where(x => x.AccountAnswerRole == EventCharacterRole.Informational).ToList();
        if (primaryQuestions.Count != 1) return new(Refused("The event has no unambiguous primary playing account field."), false);
        var primaryQuestion = primaryQuestions[0];
        if (playing.Count == 0) return new(Invalid("Keep at least one playing account.", "accounts"), false);
        if (playing.Count(x => x.Primary) != 1) return new(Invalid("Choose exactly one primary account.", "accounts"), false);
        if (playing.Count > playingQuestions.Count) return new(Invalid("There are more playing accounts than this event has slots for.", "accounts"), false);
        if (informational.Count > infoQuestions.Count) return new(Invalid("There are more alt accounts than this event has slots for.", "alt"), false);
        if (informational.Any(x => x.Primary || x.Ehb is not null)) return new(Invalid("Alt accounts have no EHB and can't be primary.", "alt"), false);

        var current = await dbContext.EventParticipantCharacters
            .Where(x => x.EventParticipantId == participant.Id && x.ReleasedAt == null)
            .OrderBy(x => x.RegistrationOrder).ToListAsync(ct);
        var currentById = current.ToDictionary(x => x.Id);
        var currentCharacterIds = current.Select(x => x.OsrsCharacterId).ToList();
        var currentNames = (await dbContext.OsrsCharacters.Where(x => currentCharacterIds.Contains(x.Id)).ToListAsync(ct)).ToDictionary(x => x.Id, x => NormalizeAccountName(x.DisplayName));

        var entries = playing.Select((entry, index) => (Entry: entry, Role: EventCharacterRole.Playing, Field: $"playing:{index}"))
            .Concat(informational.Select((entry, index) => (Entry: entry, Role: EventCharacterRole.Informational, Field: $"alt:{index}")))
            .ToList();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var resolved = new List<(AdminDrawerAccount Entry, EventCharacterRole Role, EventParticipantCharacter? Existing, OsrsCharacter? Character, Guid CharacterId)>();
        foreach (var (entry, role, field) in entries)
        {
            var name = entry.Name?.Trim();
            if (string.IsNullOrEmpty(name)) return new(Invalid("Enter an RSN.", field), false);
            var normalized = NormalizeAccountName(name);
            if (!seen.Add(normalized)) return new(Invalid("This account is already listed.", field), false);
            if (role == EventCharacterRole.Playing && (entry.Ehb is not { } ehb || ehb < 0 || ehb > 100_000))
                return new(Invalid("Enter an EHB from 0 to 100,000.", field), false);
            EventParticipantCharacter? existing = null;
            if (entry.AssignmentId is { } assignmentId)
            {
                if (!currentById.TryGetValue(assignmentId, out existing) || existing.EventRole != role) return new(Stale(), false);
            }
            if (existing is not null && currentNames.GetValueOrDefault(existing.OsrsCharacterId) == normalized)
            {
                resolved.Add((entry, role, existing, null, existing.OsrsCharacterId));
                continue;
            }
            // A new or renamed name: the shared rule, then an event-only character
            // (an existing record is reused; no saved link is created or changed).
            if (!RsnRule.IsValid(name)) return new(Invalid(RsnRule.Message, field), false);
            var character = await ResolveCharacterAsync(name, normalized, ct);
            if (await dbContext.EventParticipantCharacters.AnyAsync(x => x.EventId == request.EventId && x.OsrsCharacterId == character.Id && x.EventParticipantId != participant.Id && x.ReleasedAt == null, ct))
                return new(Invalid("Already used by another participant in this event.", field), false);
            resolved.Add((entry, role, existing, character, character.Id));
        }

        // Slots: primary first; other accounts keep their configured slot when it is still
        // free, then take the next free slot in form order.
        var slot = new Dictionary<int, Guid>();
        var taken = new HashSet<Guid> { primaryQuestion.Id };
        for (var index = 0; index < resolved.Count; index++)
        {
            var item = resolved[index];
            if (item.Role == EventCharacterRole.Playing && item.Entry.Primary) slot[index] = primaryQuestion.Id;
        }
        for (var index = 0; index < resolved.Count; index++)
        {
            if (slot.ContainsKey(index)) continue;
            var item = resolved[index];
            var pool = item.Role == EventCharacterRole.Playing ? playingQuestions.Where(x => x.Id != primaryQuestion.Id) : infoQuestions;
            if (item.Existing?.SignupQuestionId is { } own && pool.Any(x => x.Id == own) && taken.Add(own)) slot[index] = own;
        }
        for (var index = 0; index < resolved.Count; index++)
        {
            if (slot.ContainsKey(index)) continue;
            var item = resolved[index];
            var pool = item.Role == EventCharacterRole.Playing ? playingQuestions.Where(x => x.Id != primaryQuestion.Id) : infoQuestions;
            var free = pool.FirstOrDefault(x => !taken.Contains(x.Id));
            if (free is null) return new(Invalid("No configured account slot is free for this account.", "accounts"), false);
            taken.Add(free.Id); slot[index] = free.Id;
        }

        var changed = false;
        var order = (await dbContext.EventParticipantCharacters.Where(x => x.EventParticipantId == participant.Id)
            .MaxAsync(x => (int?)x.RegistrationOrder, ct) ?? -1) + 1;
        var kept = new HashSet<Guid>();
        var characterBySlot = new Dictionary<Guid, Guid>();
        for (var index = 0; index < resolved.Count; index++)
        {
            var (entry, role, existing, _, characterId) = resolved[index];
            var question = slot[index];
            characterBySlot[question] = characterId;
            var ehb = role == EventCharacterRole.Playing ? entry.Ehb : null;
            if (existing is not null && existing.OsrsCharacterId == characterId && (role != EventCharacterRole.Playing || existing.EhbSnapshot == ehb))
            {
                kept.Add(existing.Id);
                if (existing.SignupQuestionId != question) { existing.SetSignupQuestion(question); changed = true; }
                continue;
            }
            // Corrections are append-only assignment history (F06).
            if (existing is not null) { existing.Release(actorId, now); kept.Add(existing.Id); }
            dbContext.EventParticipantCharacters.Add(new EventParticipantCharacter(Guid.NewGuid(), request.EventId, participant.Id, characterId, order++, now, actorId,
                question, role, ehb, role == EventCharacterRole.Playing ? EhbSource.AdminCorrection : null, null));
            changed = true;
        }
        foreach (var removed in current.Where(x => !kept.Contains(x.Id)))
        {
            removed.Release(actorId, now);
            changed = true;
        }

        var accountQuestionIds = questions.Select(x => x.Id).ToList();
        var answers = await dbContext.SignupAnswers.Where(x => x.EventParticipantId == participant.Id && accountQuestionIds.Contains(x.SignupQuestionId)).ToListAsync(ct);
        foreach (var question in questions)
        {
            var answer = answers.SingleOrDefault(x => x.SignupQuestionId == question.Id);
            if (characterBySlot.TryGetValue(question.Id, out var characterId))
            {
                if (answer is null) { dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, question.Id, question.Label, string.Empty, characterId)); changed = true; }
                else if (answer.OsrsCharacterId != characterId) { answer.SetAccountCharacter(characterId); changed = true; }
            }
            else if (answer is not null) { dbContext.SignupAnswers.Remove(answer); changed = true; }
        }
        return new(null, changed);
    }

    private async Task<DrawerApplyResult> ApplyDrawerAnswersAsync(IReadOnlyDictionary<Guid, string> submitted, EventParticipant participant, CancellationToken ct)
    {
        var questions = await dbContext.SignupQuestions
            .Where(x => x.EventId == participant.EventId && x.Active && x.Type != SignupQuestionType.Account)
            .OrderBy(x => x.Position).ToListAsync(ct);
        var captain = questions.SingleOrDefault(x => x.SystemField == SignupSystemField.CaptainVolunteer);
        var volunteered = captain is null ? participant.CaptainVolunteer : IsCaptainVolunteered(captain, submitted);
        var changed = false;
        if (participant.CaptainVolunteer != volunteered) { participant.SetCaptainVolunteer(volunteered); changed = true; }
        var questionIds = questions.Select(x => x.Id).ToList();
        var answers = await dbContext.SignupAnswers.Where(x => x.EventParticipantId == participant.Id && questionIds.Contains(x.SignupQuestionId)).ToListAsync(ct);
        foreach (var question in questions.Where(x => x.SystemField != SignupSystemField.CaptainVolunteer))
        {
            var value = question.SystemField == SignupSystemField.CoCaptainName && !volunteered
                ? null
                : submitted.TryGetValue(question.Id, out var raw) ? CanonicalAnswer(question, raw) : null;
            // Admin corrections never invent answers: an empty value clears the answer and
            // required questions are not enforced (an admin-added participant has none).
            if (!string.IsNullOrWhiteSpace(value) && !ValidateAnswer(question, value, out var error))
                return new(Invalid(error ?? "Check this answer.", $"answer:{question.Id}"), false);
            var answer = answers.SingleOrDefault(x => x.SignupQuestionId == question.Id);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (answer is not null) { dbContext.SignupAnswers.Remove(answer); changed = true; }
            }
            else if (answer is null) { dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, question.Id, question.Label, value)); changed = true; }
            else if (!string.Equals(answer.Value, value, StringComparison.Ordinal)) { answer.Update(value); changed = true; }
        }
        return new(null, changed);
    }

    private async Task<string> DrawerStateAsync(EventParticipant participant, CancellationToken ct)
    {
        var accounts = await (from assignment in dbContext.EventParticipantCharacters
                              join character in dbContext.OsrsCharacters on assignment.OsrsCharacterId equals character.Id
                              where assignment.EventParticipantId == participant.Id && assignment.ReleasedAt == null
                              orderby assignment.RegistrationOrder
                              select new { assignment.SignupQuestionId, assignment.EventRole, character.NormalizedName, assignment.EhbSnapshot }).ToListAsync(ct);
        var answers = await dbContext.SignupAnswers.Where(x => x.EventParticipantId == participant.Id && x.OsrsCharacterId == null)
            .OrderBy(x => x.SignupQuestionId).Select(x => new { x.SignupQuestionId, x.Value }).ToListAsync(ct);
        return Json(new { accounts, answers, captainVolunteer = participant.CaptainVolunteer });
    }
}
