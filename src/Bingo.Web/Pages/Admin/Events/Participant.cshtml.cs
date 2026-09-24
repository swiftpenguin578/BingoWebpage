using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Access;
using Bingo.Application.Signups;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class ParticipantModel(
    ApplicationDbContext dbContext,
    EventParticipantCharacterService characterService,
    ISignupService? signupService = null,
    IStringLocalizer<SharedResource>? text = null,
    TimeProvider? timeProvider = null) : PageModel
{
    [BindProperty] public EditInput Input { get; set; } = new();
    [BindProperty, StringLength(2000)] public string? AdminNote { get; set; }
    [BindProperty] public string? ExpectedAdminNote { get; set; }
    [BindProperty] public bool ConfirmLifecycleAction { get; set; }
    [BindProperty] public bool ConfirmOwnershipTransfer { get; set; }
    [BindProperty] public Guid? DestinationOwnerAccountId { get; set; }
    [BindProperty] public Guid? ExpectedOwnerAccountId { get; set; }
    [BindProperty, StringLength(4000)] public string? PrivateWithdrawalNote { get; set; }
    [BindProperty] public long? ExpectedMembershipVersion { get; set; }
    [BindProperty] public Guid? ReplacementWaitingParticipantId { get; set; }
    [BindProperty] public string? WomValidationConfirmationToken { get; set; }
    [BindProperty] public InternalReplacementInput InternalReplacement { get; set; } = new();
    [BindProperty] public bool Overlay { get; set; }
    public bool IsOverlay => Overlay || string.Equals(Request.Query["overlay"], "1", StringComparison.Ordinal);
    public bool CanAdminWithdraw { get; private set; }
    public bool CanAdminLiveWithdraw { get; private set; }
    public bool CanCorrectPreLiveRoster { get; private set; }
    public bool WithdrawalFailed { get; private set; }
    public bool CanEditParticipant { get; private set; }
    public bool CanEditPrivateMetadata { get; private set; }
    public bool CanTransferOwnership { get; private set; }
    public bool CanFillVacancy { get; private set; }
    [BindProperty] public Guid? VacancyMembershipId { get; set; }
    [BindProperty] public long? VacancyMembershipVersion { get; set; }
    public bool CanAdminRestore { get; private set; }
    public bool WomValidationConfirmationRequired { get; private set; }
    public string WomValidationConfirmationNames => string.Join(", ", InternalReplacement.AccountAnswers.Values
        .Where(answer => !string.IsNullOrWhiteSpace(answer.CharacterName))
        .Select(answer => answer.CharacterName!.Trim())
        .DistinctBy(name => name.ToUpperInvariant(), StringComparer.Ordinal));
    public Guid EventId { get; private set; }
    public Guid RouteParticipantId { get; private set; }
    public PaymentStatus Payment { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public string EventTimezone { get; private set; } = DateTimePresentation.DefaultTimezoneId;
    public string Name { get; private set; } = string.Empty;
    public SignupStatus Status { get; private set; }
    public string StatusLabel { get; private set; } = string.Empty;
    public string SourceLabel { get; private set; } = string.Empty;
    public DateTimeOffset SignedUpAt { get; private set; }
    public long SignupSequence { get; private set; }
    public string? TeamName { get; private set; }
    public string? WebsiteOwner { get; private set; }
    public string? StatusReason { get; private set; }
    public IReadOnlyList<QuestionView> Questions { get; private set; } = [];
    public IReadOnlyList<ReplacementCandidate> WaitingReplacementCandidates { get; private set; } = [];
    public PromotionFollowUpView? PromotionFollowUp { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, Guid participantId, CancellationToken ct)
    { _ = characterService; Overlay = string.Equals(Request.Query["overlay"], "1", StringComparison.Ordinal); return await LoadAsync(id, participantId, true, ct) ? Page() : NotFound(); }

    public async Task<IActionResult> OnPostAsync(Guid id, Guid participantId, [FromForm] bool overlay, CancellationToken ct)
    {
        Overlay = ResolveSubmittedOverlay(overlay);
        var actorId = User.GetAccountId(); if (actorId is null) return Forbid();
        var result = signupService is null ? new AdminParticipantResult(false, "Participant correction is not available.") : await signupService.CorrectAdminParticipantAsync(new AdminParticipantChangeRequest(id, participantId, actorId.Value, User.Identity?.Name ?? "Admin", null, Input.AccountAnswers.ToDictionary(x => x.Key, x => new AdminAccountAnswer(x.Value.CharacterName, PostedEhb(x.Key, x.Value.Ehb))), Input.CustomAnswers, Input.ExpectedResponseVersion, Input.WomValidationConfirmationToken), ct);
        if (!result.Succeeded)
        {
            var confirmationToken = result.WomValidationConfirmationToken;
            Input.WomValidationConfirmationToken = confirmationToken ?? Input.WomValidationConfirmationToken;
            if (confirmationToken is not null)
            {
                ModelState.Remove("Input.WomValidationConfirmationToken");
            }
            ModelState.AddModelError(string.Empty, IsResponseConflict(result.Error)
                ? Localize("Your signup changed while you were editing it. Please reload and try again.")
                : Localize(result.Error ?? "Participant details could not be saved."));
            if (!await LoadAsync(id, participantId, false, ct)) return NotFound();
            if (confirmationToken is not null)
            {
                Input.WomValidationConfirmationToken = confirmationToken;
                ModelState.Remove("Input.WomValidationConfirmationToken");
            }
            return Page();
        }
        SetStatus(Localize("Participant details saved."), UiMessageType.Success);
        return RedirectToParticipant(id, participantId);
    }

    public async Task<IActionResult> OnPostTransferOwnershipAsync(Guid id, Guid participantId, [FromForm] bool overlay, CancellationToken ct)
    {
        Overlay = ResolveSubmittedOverlay(overlay);
        var actorId = User.GetAccountId(); if (actorId is null) return Forbid();
        var result = signupService is null ? new ParticipantOwnershipTransferResult(false, "Participant ownership transfer is not available.") : await signupService.TransferParticipantOwnershipAsync(new ParticipantOwnershipTransferRequest(id, participantId, actorId.Value, User.Identity?.Name ?? "Admin", DestinationOwnerAccountId, ExpectedOwnerAccountId, ConfirmOwnershipTransfer), ct);
        SetStatus(result.Succeeded ? Localize("Participant ownership transferred.") : result.Error ?? Localize("Participant ownership could not be transferred."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToParticipant(id, participantId, "ownership");
    }

    public async Task<IActionResult> OnPostAdminNoteAsync(Guid id, Guid participantId, [FromForm] bool overlay, CancellationToken ct)
    {
        Overlay = ResolveSubmittedOverlay(overlay);
        var result = signupService is null
            ? new ParticipantPaymentResult(false, "Participant notes are not available.")
            : await signupService.SetAdminNotesAsync(id, participantId, User.GetAccountId(), User.Identity?.Name ?? "Admin", AdminNote, ExpectedAdminNote, ct);
        SetStatus(result.Succeeded ? Localize("Private Admin note saved.") : result.Error ?? Localize("Private Admin note could not be saved."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToParticipant(id, participantId, "admin-notes");
    }

    public async Task<IActionResult> OnPostPaymentAsync(Guid id, Guid participantId, PaymentStatus payment, [FromForm] bool overlay, CancellationToken ct)
    {
        Overlay = ResolveSubmittedOverlay(overlay);
        var result = signupService is null ? new ParticipantPaymentResult(false, "Participant payment is not available.") : await signupService.SetPaymentAsync(id, participantId, User.GetAccountId(), User.Identity?.Name ?? "Admin", payment, ct);
        SetStatus(result.Succeeded ? Localize("Payment saved.") : result.Error ?? Localize("Payment could not be saved."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToParticipant(id, participantId, "payment");
    }

    public async Task<IActionResult> OnPostWithdrawAsync(Guid id, Guid participantId, [FromForm] bool overlay, CancellationToken ct)
    {
        Overlay = ResolveSubmittedOverlay(overlay);
        if (!ConfirmLifecycleAction) { SetStatus(Localize("Confirm the withdrawal before continuing."), UiMessageType.Error); return RedirectToParticipant(id, participantId); }
        var result = signupService is null ? new ParticipantLifecycleResult(false, "Participant lifecycle is not available.") : await signupService.WithdrawAsync(id, participantId, User.GetAccountId(), User.Identity?.Name ?? "Admin", true, PrivateWithdrawalNote, ExpectedMembershipVersion, ct);
        if (!result.Succeeded)
        {
            // Keep the submitted optional note available when a pre-Live correction fails.
            if (!await LoadAsync(id, participantId, true, ct)) return NotFound();
            if (CanCorrectPreLiveRoster)
            {
                WithdrawalFailed = true;
                ModelState.AddModelError(string.Empty, Localize(result.Error ?? "Participant could not be withdrawn."));
                return Page();
            }
        }
        SetStatus(result.Succeeded ? Localize("Participant withdrawn.") : Localize(result.Error ?? "Participant could not be withdrawn."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToParticipant(id, participantId);
    }

    public async Task<IActionResult> OnPostFillVacancyAsync(Guid id, Guid participantId, [FromForm] bool overlay, CancellationToken ct)
    {
        Overlay = ResolveSubmittedOverlay(overlay);
        var actorId = User.GetAccountId();
        if (actorId is null || VacancyMembershipId is null) return Forbid();
        Guid? ownerId = null;
        if (!string.IsNullOrWhiteSpace(InternalReplacement.OwnerUsername))
        {
            ownerId = await dbContext.Accounts.AsNoTracking().Where(x => x.LoginName == InternalReplacement.OwnerUsername.Trim() && x.Active && x.AccountType == Bingo.Domain.Access.AccountType.WebsiteAccount).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            if (ownerId is null)
            {
                SetStatus(Localize("The internal owner must be an active website account."), UiMessageType.Error);
                return RedirectToParticipant(id, participantId);
            }
        }
        var internalRequest = ReplacementWaitingParticipantId is null
            ? new AdminParticipantChangeRequest(id, null, actorId.Value, User.Identity?.Name ?? "Admin", ownerId,
                InternalReplacement.AccountAnswers.ToDictionary(x => x.Key, x => new AdminAccountAnswer(x.Value.CharacterName, x.Value.Ehb)), InternalReplacement.Answers, null, InternalReplacement.WomValidationConfirmationToken)
            : null;
        var result = signupService is null
            ? new LiveParticipantResult(false, "Live participant replacement is not available.")
            : await signupService.ReplaceVacancyAsync(new LiveReplacementRequest(id, VacancyMembershipId.Value, actorId.Value, User.Identity?.Name ?? "Admin", ReplacementWaitingParticipantId, internalRequest, VacancyMembershipVersion, WomValidationConfirmationToken), ct);
        if (internalRequest is not null && result.WomValidationConfirmationToken is { } confirmationToken)
        {
            if (!await LoadAsync(id, participantId, false, ct)) return NotFound();
            WomValidationConfirmationToken = confirmationToken;
            InternalReplacement.WomValidationConfirmationToken = confirmationToken;
            ModelState.Remove(nameof(WomValidationConfirmationToken));
            ModelState.Remove("InternalReplacement.WomValidationConfirmationToken");
            WomValidationConfirmationRequired = true;
            return Page();
        }
        WomValidationConfirmationToken = result.WomValidationConfirmationToken ?? WomValidationConfirmationToken;
        if (result.WomValidationConfirmationToken is not null && internalRequest is not null)
            InternalReplacement.WomValidationConfirmationToken = result.WomValidationConfirmationToken;
        if (result.WomValidationConfirmationToken is not null)
            TempData["WomValidationConfirmationToken"] = result.WomValidationConfirmationToken;
        SetStatus(result.Succeeded ? Localize("Replacement saved.") : Localize(result.Error ?? "The vacancy could not be filled."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToParticipant(id, participantId);
    }

    public async Task<IActionResult> OnPostCancelWomValidationAsync(Guid id, Guid participantId, [FromForm] bool overlay, CancellationToken ct)
    {
        Overlay = ResolveSubmittedOverlay(overlay);
        WomValidationConfirmationToken = null;
        InternalReplacement.WomValidationConfirmationToken = null;
        ModelState.Remove(nameof(WomValidationConfirmationToken));
        ModelState.Remove("InternalReplacement.WomValidationConfirmationToken");
        TempData.Remove("WomValidationConfirmationToken");
        if (!await LoadAsync(id, participantId, false, ct)) return NotFound();
        WomValidationConfirmationToken = null;
        InternalReplacement.WomValidationConfirmationToken = null;
        ModelState.Remove(nameof(WomValidationConfirmationToken));
        ModelState.Remove("InternalReplacement.WomValidationConfirmationToken");
        WomValidationConfirmationRequired = false;
        return Page();
    }

    public async Task<IActionResult> OnPostCompletePromotionFollowUpAsync(Guid id, Guid participantId, Guid followUpId, [FromForm] bool overlay, CancellationToken ct)
    {
        Overlay = ResolveSubmittedOverlay(overlay);
        var actorId = User.GetAccountId(); if (actorId is null) return Forbid();
        var result = signupService is null ? new PromotionFollowUpResult(false, "Promotion follow-up is not available.") : await signupService.CompletePromotionFollowUpAsync(id, followUpId, actorId.Value, User.Identity?.Name ?? "Admin", ct);
        SetStatus(result.Succeeded ? Localize("Promotion follow-up marked complete.") : result.Error ?? Localize("The promotion follow-up could not be completed."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToParticipant(id, participantId);
    }

    public async Task<IActionResult> OnPostRestoreAsync(Guid id, Guid participantId, [FromForm] bool overlay, CancellationToken ct)
    {
        Overlay = ResolveSubmittedOverlay(overlay);
        if (!ConfirmLifecycleAction) { SetStatus(Localize("Confirm the restoration before continuing."), UiMessageType.Error); return RedirectToParticipant(id, participantId); }
        var accountId = User.GetAccountId(); if (accountId is null) return Forbid();
        var result = signupService is null ? new ParticipantLifecycleResult(false, "Participant lifecycle is not available.") : await signupService.RestoreAsync(id, participantId, accountId.Value, User.Identity?.Name ?? "Admin", WomValidationConfirmationToken, ct);
        if (result.WomValidationConfirmationToken is not null)
        {
            WomValidationConfirmationToken = result.WomValidationConfirmationToken;
            TempData["WomValidationConfirmationToken"] = result.WomValidationConfirmationToken;
        }
        SetStatus(result.Succeeded ? Localize("Participant restored.") : Localize(result.Error ?? "Participant could not be restored."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToParticipant(id, participantId);
    }

    private async Task<bool> LoadAsync(Guid id, Guid participantId, bool initializeInput, CancellationToken ct)
    {
        var bingoEvent = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.HiddenAt == null && item.State != EventState.Discarded, ct);
        var participant = await dbContext.EventParticipants.AsNoTracking().SingleOrDefaultAsync(item => item.EventId == id && item.Id == participantId, ct);
        if (bingoEvent is null || participant is null) return false;

        var draft = await dbContext.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct);
        CanCorrectPreLiveRoster = bingoEvent.CanCorrectFinalizedRoster(draft?.State, (timeProvider ?? TimeProvider.System).GetUtcNow()) &&
            await dbContext.DraftPublicationCycles.AnyAsync(x => x.DraftSessionId == draft!.Id && x.SupersededAt == null, ct);

        EventId = id;
        if (TempData.Peek("WomValidationConfirmationToken") is string pendingValidation)
        {
            WomValidationConfirmationToken = pendingValidation;
            InternalReplacement.WomValidationConfirmationToken = pendingValidation;
        }
        RouteParticipantId = participantId;
        Payment = participant.PaymentStatus;
        EventName = bingoEvent.Name;
        EventTimezone = bingoEvent.Timezone;
        var authority = await dbContext.AdminPrimaryCharacters().AsNoTracking().SingleOrDefaultAsync(x => x.ParticipantId == participantId, ct);
        var secondName = await (from assignment in dbContext.EventParticipantCharacters.AsNoTracking()
                                join character in dbContext.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                                where assignment.EventParticipantId == participantId && assignment.ReleasedAt == null &&
                                      assignment.EventRole == EventCharacterRole.Informational
                                orderby assignment.RegistrationOrder
                                select character.DisplayName).FirstOrDefaultAsync(ct);
        Name = authority?.Name ?? "External roster member";
        WebsiteOwner = participant.AccountId is { } ownerId
            ? await dbContext.Accounts.AsNoTracking()
                .Where(account => account.Id == ownerId && account.Active && account.AccountType == Bingo.Domain.Access.AccountType.WebsiteAccount)
                .Select(account => account.LoginName)
                .SingleOrDefaultAsync(ct)
            : null;
        Status = participant.SignupStatus;
        StatusLabel = participant.SignupStatus switch
        {
            SignupStatus.Confirmed => "Confirmed",
            SignupStatus.WaitingList => "Waiting list",
            SignupStatus.Withdrawn => "Withdrawn",
            _ => "Withdrawn"
        };
        SourceLabel = participant.Source switch
        {
            SignupSource.Website => "Website signup",
            SignupSource.CsvImport => "CSV import",
            _ => "Added by an admin"
        };
        SignedUpAt = participant.SignedUpAt;
        SignupSequence = participant.SignupSequence;
        StatusReason = participant.StatusReason;
        TeamName = await (from membership in dbContext.TeamMemberships.AsNoTracking()
                          join team in dbContext.Teams.AsNoTracking() on membership.TeamId equals team.Id
                          where membership.EventParticipantId == participantId && membership.LeftAt == null
                          select team.Name).SingleOrDefaultAsync(ct);
        ExpectedMembershipVersion = await dbContext.TeamMemberships.AsNoTracking()
            .Where(x => x.EventParticipantId == participantId && x.LeftAt == null)
            .Select(x => (long?)x.Version).SingleOrDefaultAsync(ct);

        var vacancy = await (from membership in dbContext.TeamMemberships.AsNoTracking()
                             join team in dbContext.Teams.AsNoTracking() on membership.TeamId equals team.Id
                             where membership.EventParticipantId == participantId && membership.LeftAt != null && team.EventId == id &&
                                   !dbContext.TeamMemberships.Any(replacement => replacement.ReplacesMembershipId == membership.Id)
                             orderby membership.LeftAt descending
                             select new { membership.Id, membership.Version, TeamName = team.Name }).FirstOrDefaultAsync(ct);
        if (vacancy is not null && TeamName is null) TeamName = $"{vacancy.TeamName} (vacancy)";
        VacancyMembershipId = vacancy?.Id;
        VacancyMembershipVersion = vacancy?.Version;
        CanFillVacancy = (bingoEvent.State == EventState.Live || CanCorrectPreLiveRoster) && participant.SignupStatus == SignupStatus.Withdrawn && vacancy is not null;
        if (CanFillVacancy)
        {
            WaitingReplacementCandidates = await (from candidate in dbContext.EventParticipants.AsNoTracking()
                                                  join primary in dbContext.AdminPrimaryCharacters().AsNoTracking() on candidate.Id equals primary.ParticipantId into primaries
                                                  from primary in primaries.DefaultIfEmpty()
                                                  where candidate.EventId == id && candidate.SignupStatus == SignupStatus.WaitingList
                                                  orderby candidate.SignedUpAt, candidate.SignupSequence
                                                  select new ReplacementCandidate(candidate.Id, primary == null ? "Participant" : primary.Name, candidate.SignupSequence)).ToListAsync(ct);
        }
        if (bingoEvent.State is EventState.Live or EventState.AwaitingFinalReview || CanCorrectPreLiveRoster)
        {
            var followUp = await dbContext.WaitingListPromotionFollowUps.AsNoTracking()
                .SingleOrDefaultAsync(x => x.EventId == id && x.PromotedParticipantId == participantId, ct);
            if (followUp is not null) PromotionFollowUp = new PromotionFollowUpView(followUp.Id, followUp.CompletedAt, followUp.CompletedByAccountId);
        }

        var questions = await dbContext.SignupQuestions.AsNoTracking()
            .Where(question => question.EventId == id && question.DisabledReason != SignupQuestion.DeletedReason)
            .OrderBy(question => question.Position)
            .ToListAsync(ct);
        var answers = await dbContext.SignupAnswers.AsNoTracking()
            .Where(answer => answer.EventParticipantId == participantId)
            .ToDictionaryAsync(answer => answer.SignupQuestionId, ct);
        Questions = questions
            .Where(question => question.Active || answers.ContainsKey(question.Id))
            .Select(question => new QuestionView(
                question.Id,
                answers.TryGetValue(question.Id, out var answer) ? answer.QuestionLabelSnapshot : question.Label,
                question.Type,
                question.Required,
                question.Active,
                question.AccountAnswerRole,
                question.SystemField,
                question.Options?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
                answers.GetValueOrDefault(question.Id)?.Value))
            .ToList();

        var assignments = await (from assignment in dbContext.EventParticipantCharacters.AsNoTracking()
                                 join character in dbContext.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                                 where assignment.EventParticipantId == participantId && assignment.ReleasedAt == null && assignment.SignupQuestionId != null
                                 select new { QuestionId = assignment.SignupQuestionId!.Value, character.DisplayName, assignment.EhbSnapshot }).ToListAsync(ct);

        if (initializeInput)
        {
            Input = new EditInput
            {
                AccountAnswers = assignments.ToDictionary(x => x.QuestionId, x => new AccountInput { CharacterName = x.DisplayName, Ehb = x.EhbSnapshot }),
                CustomAnswers = Questions.Where(question => question.Active && !string.IsNullOrWhiteSpace(question.Value))
                    .ToDictionary(question => question.Id, question => question.Type == SignupQuestionType.YesNo && bool.TryParse(question.Value, out var value) ? value ? "true" : "false" : question.Value!)
            };
            Input.ExpectedResponseVersion = participant.ResponseVersion;
            var captain = questions.SingleOrDefault(question => question.Active && question.SystemField == SignupSystemField.CaptainVolunteer);
            if (captain is not null) Input.CustomAnswers[captain.Id] = participant.CaptainVolunteer ? "true" : "false";
            AdminNote = participant.AdminNotes;
            ExpectedAdminNote = participant.AdminNotes;
            ExpectedOwnerAccountId = participant.AccountId;
        }

        CanAdminLiveWithdraw = (bingoEvent.State == EventState.Live || CanCorrectPreLiveRoster) && participant.SignupStatus == SignupStatus.Confirmed && await dbContext.TeamMemberships.AnyAsync(x => x.EventParticipantId == participantId && x.LeftAt == null, ct);
        CanEditParticipant = !bingoEvent.DraftLocked && bingoEvent.State is (EventState.SignupOpen or EventState.SignupClosed);
        CanEditPrivateMetadata = bingoEvent.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed or EventState.Live or EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived or EventState.Cancelled;
        CanTransferOwnership = bingoEvent.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed or EventState.Live or EventState.AwaitingFinalReview;
        CanAdminWithdraw = (CanEditParticipant && participant.SignupStatus is (SignupStatus.Confirmed or SignupStatus.WaitingList)) || CanAdminLiveWithdraw;
        CanAdminRestore = CanEditParticipant && participant.SignupStatus == SignupStatus.Withdrawn;
        return true;
    }

    private decimal? PostedEhb(Guid questionId, decimal? boundValue)
    {
        if (ModelState.TryGetValue($"Input.AccountAnswers[{questionId}].Ehb", out var state) &&
            state.AttemptedValue is { } attempted && attempted.Contains('.', StringComparison.Ordinal) &&
            decimal.TryParse(attempted, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantValue))
            return invariantValue;
        return boundValue;
    }

    private string Localize(string key, params object[] arguments) => text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);
    private void SetStatus(string message, UiMessageType type)
    {
        TempData["StatusMessage"] = message;
        TempData[UiMessage.TypeKey] = type.ToString();
    }

    private RedirectToPageResult RedirectToParticipant(Guid id, Guid participantId, string? fragment = null) =>
        RedirectToPage(null, null, new { id, participantId, overlay = Overlay ? "1" : null }, fragment);

    private bool ResolveSubmittedOverlay(bool overlay)
    {
        if (!Request.HasFormContentType) return overlay || IsOverlay;

        var submitted = Request.Form["overlay"].ToString();
        ModelState.Remove("overlay");
        return overlay || string.Equals(submitted, "1", StringComparison.Ordinal) || string.Equals(Request.Query["overlay"], "1", StringComparison.Ordinal);
    }

    private static bool IsResponseConflict(string? error) => error?.Contains("changed while you were editing", StringComparison.OrdinalIgnoreCase) == true;
    private static bool IsAssignmentReservationConflict(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_event_participant_characters_event_id_osrs_character_id" };

    public sealed class EditInput
    {
        // Kept for the older direct PageModel test seam; active-form account values live in AccountAnswers.
        public decimal Ehb { get; set; }
        public Dictionary<Guid, AccountInput> AccountAnswers { get; set; } = [];
        public Dictionary<Guid, string> CustomAnswers { get; set; } = [];
        public int? ExpectedResponseVersion { get; set; }
        public string? WomValidationConfirmationToken { get; set; }
    }
    public sealed class AccountInput { [StringLength(100)] public string? CharacterName { get; set; } [Range(0, 100000)] public decimal? Ehb { get; set; } }

    public sealed class InternalReplacementInput
    {
        [StringLength(100)] public string? OwnerUsername { get; set; }
        public Dictionary<Guid, AccountInput> AccountAnswers { get; set; } = [];
        public Dictionary<Guid, string> Answers { get; set; } = [];
        public string? WomValidationConfirmationToken { get; set; }
    }

    public sealed record ReplacementCandidate(Guid Id, string Name, long Sequence);
    public sealed record PromotionFollowUpView(Guid Id, DateTimeOffset? CompletedAt, Guid? CompletedByAccountId);

    public sealed record QuestionView(
        Guid Id, string Label, SignupQuestionType Type, bool Required, bool Active, EventCharacterRole? AccountRole, SignupSystemField SystemField, string[] Options, string? Value);
}
