using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Security;
using Bingo.Application.Signups;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Npgsql;

namespace Bingo.Web.Pages.Admin.Events;

[AdminDesign]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class SignupSetupModel(ApplicationDbContext dbContext, TimeProvider timeProvider, ISignupService signupService, IStringLocalizer<SharedResource>? text = null, IAuditWriter? auditWriter = null, ISecretHasher? hasher = null) : PageModel
{
    public Guid EventId { get; private set; }
    public string EventTimezone { get; private set; } = string.Empty;
    public bool HasUnresolvableTimezone => !TimeZoneInfo.TryFindSystemTimeZoneById(EventTimezone, out _);
    public EventState EventState { get; private set; }
    public bool DraftLocked { get; private set; }
    public SignupSettingsSnapshot Settings { get; private set; } = new(0, null, true, false, false);
    public int ConfirmedCount { get; private set; }
    public int WaitingCount { get; private set; }
    public IReadOnlyList<SignupQuestion> AllQuestions { get; private set; } = [];
    [BindProperty] public SignupAdministrationInput SignupAdministration { get; set; } = new();
    [BindProperty] public SignupCodeInput SignupCode { get; set; } = new();
    public IReadOnlyList<SignupQuestion> Questions { get; private set; } = [];
    public SignupQuestion? CoCaptainQuestion { get; private set; }
    public IReadOnlyDictionary<Guid, SignupQuestionImpact> QuestionImpacts { get; private set; } = new Dictionary<Guid, SignupQuestionImpact>();

    public QuestionInput Input { get; set; } = new();
    [BindProperty] public Guid AddRequestId { get; set; }
    public Guid CustomAddRequestId => AddRequestId == Guid.Empty ? initialCustomAddRequestId : AddRequestId;
    private readonly Guid initialCustomAddRequestId = Guid.NewGuid();
    [BindProperty] public int? ExpectedFormVersion { get; set; }
    public int? FormVersion { get; private set; }
    public bool HasForm { get; private set; }
    public bool CanEdit { get; private set; }
    public bool HasFirstResponse { get; private set; }
    public DateTimeOffset? FirstResponseAt { get; private set; }
    public string EventName { get; private set; } = string.Empty;

    public object CurrentSnapshot => new
    {
        eventId = EventId,
        phase = EventState.ToString(),
        draftLocked = DraftLocked,
        editable = CanEdit,
        settings = Settings,
        confirmed = ConfirmedCount,
        waiting = WaitingCount,
        hasForm = HasForm,
        formVersion = FormVersion,
        hasFirstResponse = HasFirstResponse,
        firstResponseDay = FirstResponseAt is { } first ? DateTimePresentation.ToTimezone(first, HasUnresolvableTimezone ? "UTC" : null).ToString("d MMM yyyy, HH':'mm", CultureInfo.CurrentCulture) : null,
        questions = AllQuestions.Select(question => new
        {
            question.Id,
            question.Key,
            question.Label,
            question.HelpText,
            type = question.Type.ToString(),
            question.Required,
            question.Options,
            question.Position,
            question.Active,
            systemField = question.SystemField.ToString(),
            accountRole = question.AccountAnswerRole?.ToString(),
            question.Version,
            impact = QuestionImpacts.GetValueOrDefault(question.Id)
        }).ToArray()
    };

    public async Task<IActionResult> OnGetCurrentAsync(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!await LoadAsync(id, ct)) return NotFound();
        var result = CurrentSnapshot;
        await transaction.CommitAsync(ct);
        return new JsonResult(result);
    }

    public async Task<IActionResult> OnPostSignupAdministrationAsync(Guid id, CancellationToken ct)
    {
        var actorId = User.GetAccountId();
        if (actorId is null) return Forbid();
        var result = await signupService.UpdateSignupAdministrationAsync(id, SignupAdministration.Version, SignupAdministration.ParticipantCap, true, actorId.Value, User.Identity?.Name ?? "Admin", cancellationToken: ct);
        if (WantsSignupSettingsJson) return new JsonResult(result);
        if (!result.Succeeded)
        {
            SetStatus(result.Error ?? Localize("Signup settings could not be saved."), UiMessageType.Error);
            return RedirectToPage("SignupSetup", new { id });
        }

        var message = result.PromotedParticipants > 0
            ? Localize("Signup settings saved at capacity {0}. {1} waiting-list participant(s) were promoted automatically.", result.EffectiveParticipantCap?.ToString(CultureInfo.CurrentCulture) ?? string.Empty, result.PromotedParticipants)
            : Localize("Signup capacity and waiting-list settings saved at capacity {0}.", result.EffectiveParticipantCap?.ToString(CultureInfo.CurrentCulture) ?? string.Empty);
        SetStatus(message, UiMessageType.Success);
        return RedirectToPage("SignupSetup", new { id });
    }

    private bool WantsSignupSettingsJson => Request.GetTypedHeaders().Accept?.Any(value => value.MediaType.Value == "application/json") == true;

    public async Task<IActionResult> OnPostSignupCodeAsync(Guid id, CancellationToken ct)
    {
        IActionResult response;
        try { response = await SaveSignupCodeAsync(id, ct); }
        catch (Exception ex) when (IsSignupSerializationConflict(ex))
        {
            dbContext.ChangeTracker.Clear();
            SetStatus(Localize("This event changed while you were editing it. Review the latest values and try again."), UiMessageType.Error);
            response = WantsSignupSettingsJson
                ? new JsonResult(new SignupAdministrationResult(false, "This event changed while you were editing it. Review the latest values and try again.", SubmittedEventVersion: SignupCode.Version))
                : RedirectToPage("SignupSetup", new { id });
        }
        if (response is JsonResult { Value: SignupAdministrationResult { Succeeded: false, Settings: null } result })
        {
            var current = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.HiddenAt == null, ct);
            if (current is not null) response = new JsonResult(result with
            {
                Settings = new(current.Version, current.ParticipantCap, current.WaitingListEnabled,
                    current.RequireSignupCode, current.SignupCodeHash is not null)
            });
        }
        return response;
    }

    private static bool IsSignupSerializationConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.SerializationFailure }) return true;
        return false;
    }

    private async Task<IActionResult> SaveSignupCodeAsync(Guid id, CancellationToken ct)
    {
        var actorId = User.GetAccountId();
        if (actorId is null) return Forbid();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var bingoEvent = await dbContext.Events
            .FromSqlInterpolated($"SELECT * FROM events WHERE id = {id} AND hidden_at IS NULL FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (bingoEvent is null) return NotFound();
        if (bingoEvent.Version != SignupCode.Version)
        {
            SetStatus(Localize("This event changed while you were editing it. Review the latest values and try again."), UiMessageType.Error);
            return CodeResult(false, "This event changed while you were editing it. Review the latest values and try again.");
        }
        if (!bingoEvent.AcceptsWaitingList)
        {
            SetStatus(Localize("Signup settings are locked because the draft has started or this event has moved on."), UiMessageType.Error);
            return CodeResult(false, "Signup settings are locked because the draft has started or this event has moved on.");
        }

        const string codeField = "SignupCode.NewSignupCode";
        if (ModelState.TryGetValue(codeField, out var codeState) && codeState.Errors.Count > 0)
        {
            var errors = codeState.Errors.Select(error => error.ErrorMessage).ToArray();
            if (WantsSignupSettingsJson) return CodeResult(false, string.Join(" ", errors));
            var requireCode = SignupCode.RequireSignupCode;
            if (!await LoadAsync(id, ct)) return NotFound();
            SignupCode.RequireSignupCode = requireCode;
            // Render only this form's validation, without echoing the submitted secret.
            ModelState.Clear();
            foreach (var error in errors) ModelState.AddModelError(codeField, error);
            return Page();
        }

        try
        {
            var form = await dbContext.SignupForms.SingleAsync(item => item.EventId == id, ct);
            var currentRequiresCode = bingoEvent.RequireSignupCode;
            var currentCodeHash = bingoEvent.SignupCodeHash;
            if (SignupCode.RequireSignupCode && !currentRequiresCode && string.IsNullOrWhiteSpace(SignupCode.NewSignupCode))
            {
                SetStatus(Localize("Enter a new signup code or turn code protection off."), UiMessageType.Error);
                return CodeResult(false, "Enter a new signup code or turn code protection off.");
            }

            var hash = !SignupCode.RequireSignupCode
                ? null
                : string.IsNullOrWhiteSpace(SignupCode.NewSignupCode)
                    ? currentCodeHash
                    : (hasher ?? throw new InvalidOperationException("Signup-code hashing is unavailable.")).Hash(SignupCode.NewSignupCode);
            if (SignupCode.RequireSignupCode && string.IsNullOrWhiteSpace(hash))
            {
                SetStatus(Localize("Enter a new signup code or turn code protection off."), UiMessageType.Error);
                return CodeResult(false, "Enter a new signup code or turn code protection off.");
            }

            var before = new { RequireSignupCode = currentRequiresCode, HasSignupCode = currentCodeHash is not null };
            bingoEvent.ConfigureSignup(true, SignupCode.RequireSignupCode, hash);
            // Code settings share the event admission version.  Invalidate every
            // form rendered before this rotation, even when the compatibility
            // form row is the only other versioned record being advanced.
            bingoEvent.AdvanceVersion();
            // SignupForm retains the historical columns for old rows and old
            // readers, but the event is the single active admission authority.
            form.ConfigureSignupCode(SignupCode.RequireSignupCode, hash);
            form.AdvanceVersion();
            var changeKind = !SignupCode.RequireSignupCode
                ? "disabled"
                : !currentRequiresCode
                    ? "enabled"
                    : !string.IsNullOrWhiteSpace(SignupCode.NewSignupCode) ? "changed" : "retained";
            await (auditWriter ?? throw new InvalidOperationException("Signup-code auditing is unavailable.")).WriteAndSaveAsync(
                actorId,
                User.Identity?.Name ?? "Admin",
                "event.signup_code_changed",
                "event",
                id.ToString(),
                JsonSerializer.Serialize(new
                {
                    before,
                    after = new { RequireSignupCode = bingoEvent.RequireSignupCode, HasSignupCode = bingoEvent.SignupCodeHash is not null },
                    changeKind,
                    codeReplaced = SignupCode.RequireSignupCode && !string.IsNullOrWhiteSpace(SignupCode.NewSignupCode)
                }),
                id,
                ct);
            await transaction.CommitAsync(ct);
            SetStatus(Localize("Signup-code protection saved."), UiMessageType.Success);
            return CodeResult(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            SetStatus(Localize("This event changed while you were editing it. Review the latest values and try again."), UiMessageType.Error);
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            dbContext.ChangeTracker.Clear();
            SetStatus(Localize("The signup-code setting could not be changed in this event state."), UiMessageType.Error);
        }
        return WantsSignupSettingsJson
            ? new JsonResult(new SignupAdministrationResult(false, "The signup-code setting could not be saved.", SubmittedEventVersion: SignupCode.Version))
            : RedirectToPage("SignupSetup", new { id });

        IActionResult CodeResult(bool succeeded, string? error = null) => WantsSignupSettingsJson
            ? new JsonResult(new SignupAdministrationResult(succeeded, error,
                SubmittedEventVersion: SignupCode.Version,
                Settings: new(bingoEvent.Version, bingoEvent.ParticipantCap, bingoEvent.WaitingListEnabled,
                    bingoEvent.RequireSignupCode, bingoEvent.SignupCodeHash is not null)))
            : RedirectToPage("SignupSetup", new { id });
    }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        return await LoadAsync(id, ct) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, [Bind(Prefix = "Input")] QuestionInput input, CancellationToken ct)
    {
        Input = input;
        if (!HasSubmittedFormBaseline())
            return AddResponse(new(SignupQuestionCreationOutcome.Stale, AddRequestId, ExpectedFormVersion, Error: "This signup form changed while you were editing it. Review the latest values and try again."), id);
        if (input.Type == SignupQuestionType.SingleChoice && NormalizeChoices(input.Options) is null)
            ModelState.AddModelError("Input.Options", Localize("Add at least one choice."));
        if (!ModelState.IsValid || input.Type == SignupQuestionType.Account)
        {
            if (input.Type == SignupQuestionType.Account)
                ModelState.AddModelError("Input.Type", Localize("Account fields are managed in the Playing and Alt account sections."));
            if (WantsAddJson()) return AddResponse(new(SignupQuestionCreationOutcome.Invalid, AddRequestId, ExpectedFormVersion, Error: "Enter valid question values."), id);
            await LoadAsync(id, ct);
            return Page();
        }
        var result = await signupService.AddQuestionAsync(new(AddRequestId, id, User.GetAccountId() ?? Guid.Empty,
            ExpectedFormVersion, input.Label, input.Type, input.Required, input.HelpText, input.Options, input.AccountRole), ct);
        return AddResponse(result, id);
    }

    public async Task<IActionResult> OnPostAddAccountAsync(Guid id, [FromForm] EventCharacterRole role, CancellationToken ct)
    {
        if (!HasSubmittedFormBaseline())
            return AddResponse(new(SignupQuestionCreationOutcome.Stale, AddRequestId, ExpectedFormVersion, Error: "This signup form changed while you were editing it. Review the latest values and try again."), id);
        if (!ModelState.IsValid)
            return AddResponse(new(SignupQuestionCreationOutcome.Invalid, AddRequestId, ExpectedFormVersion, Error: "Enter valid account field values."), id);
        var result = await signupService.AddQuestionAsync(new(AddRequestId, id, User.GetAccountId() ?? Guid.Empty,
            ExpectedFormVersion, string.Empty, SignupQuestionType.Account, AccountRole: role), ct);
        return AddResponse(result, id, "Account field added.");
    }

    internal bool HasExactCommittedAddReplay { get; private set; }

    private bool WantsAddJson() => Request.GetTypedHeaders().Accept?.Any(x => x.MediaType.Value == "application/json") == true;

    private IActionResult AddResponse(SignupQuestionCreationResult result, Guid id, string successMessage = "Question added.")
    {
        HasExactCommittedAddReplay = result.Replayed && result.QuestionId is not null;
        if (WantsAddJson()) return new JsonResult(result);
        SetStatus(Localize(result.Succeeded ? result.Message ?? successMessage : result.Error ?? "The add result could not be confirmed."),
            result.Succeeded ? result.RequiredNormalizedToOptional ? UiMessageType.Information : UiMessageType.Success : UiMessageType.Error);
        return RedirectToQuestions(id);
    }

    public async Task<IActionResult> OnPostEditAccountAsync(
        Guid id,
        Guid questionId,
        [Bind(Prefix = "Account")] AccountPresentationInput account,
        CancellationToken ct)
    {
        if (!HasSubmittedFormBaseline()) return RedirectToQuestions(id);
        if (!await CanEditAsync(id, ct))
        {
            SetLockedStatus();
            return RedirectToQuestions(id);
        }
        if (!ModelState.IsValid)
        {
            SetStatus(Localize("Enter a question label."), UiMessageType.Error);
            return RedirectToQuestions(id);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var lockedEvent = await LockEditableEventAsync(id, ct);
        if (lockedEvent is null || !CanEditSignupQuestions(lockedEvent.State, lockedEvent.DraftLocked))
        {
            SetLockedStatus();
            return RedirectToQuestions(id);
        }
        if (!await HasCurrentFormBaselineAsync(id, ct)) return RedirectToQuestions(id);
        var question = await dbContext.SignupQuestions.SingleOrDefaultAsync(item =>
            item.Id == questionId
            && item.EventId == id
            && item.Active
            && item.SystemField == SignupSystemField.None
            && item.Type == SignupQuestionType.Account
            && (item.AccountAnswerRole == null || item.AccountAnswerRole == EventCharacterRole.Playing || item.AccountAnswerRole == EventCharacterRole.Informational), ct);
        if (question is null)
        {
            SetStatus(Localize("That account field cannot be edited."), UiMessageType.Error);
            return RedirectToQuestions(id);
        }

        var before = Snapshot(question);
        question.UpdatePresentation(account.Label, question.HelpText);
        await CompleteMutationAsync(id, "signup_question.account_edited", question.Id.ToString(), before, Snapshot(question), ct);
        await transaction.CommitAsync(ct);
        SetStatus(Localize("Account field saved."), UiMessageType.Success);
        return RedirectToQuestions(id);
    }

    public async Task<IActionResult> OnPostDeactivateAsync(
        Guid id,
        Guid questionId,
        [FromForm] bool confirmed,
        [FromForm] int? expectedAnswerCount,
        [FromForm] int? expectedEventRegistrationReleaseCount,
        [FromForm] int? expectedQuestionVersion,
        CancellationToken ct)
    {
        var actorId = User.GetAccountId();
        if (actorId is null)
        {
            SetStatus(Localize("Admin access is required."), UiMessageType.Error);
            return RedirectToQuestions(id);
        }
        var result = await signupService.ApplyQuestionMutationAsync(new SignupQuestionMutationRequest(
            id,
            questionId,
            actorId.Value,
            User.Identity?.Name ?? string.Empty,
            SignupQuestionMutationKind.DeleteQuestion,
            confirmed,
            expectedAnswerCount ?? -1,
            expectedEventRegistrationReleaseCount ?? -1,
            expectedQuestionVersion ?? -1), ct);
        if (WantsAddJson()) return new JsonResult(result);
        SetStatus(result.Succeeded ? Localize("Question removed.") : Localize(result.Error ?? "The question could not be removed."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToQuestions(id);
    }

    public async Task<IActionResult> OnPostCoCaptainAsync(
        Guid id,
        Guid questionId,
        [FromForm] bool enabled,
        [FromForm] bool confirmed,
        [FromForm] int? expectedAnswerCount,
        [FromForm] int? expectedEventRegistrationReleaseCount,
        [FromForm] int? expectedQuestionVersion,
        CancellationToken ct)
    {
        var actorId = User.GetAccountId();
        if (actorId is null)
        {
            SetStatus(Localize("Admin access is required."), UiMessageType.Error);
            return RedirectToQuestions(id);
        }
        SignupAdministrationResult result;
        if (enabled)
        {
            result = await signupService.EnableCoCaptainAsync(id, questionId, actorId.Value, User.Identity?.Name ?? string.Empty, ExpectedFormVersion, ct);
        }
        else
        {
            result = await signupService.ApplyQuestionMutationAsync(new SignupQuestionMutationRequest(
                id,
                questionId,
                actorId.Value,
                User.Identity?.Name ?? string.Empty,
                SignupQuestionMutationKind.DisableCoCaptain,
                confirmed,
                expectedAnswerCount ?? -1,
                expectedEventRegistrationReleaseCount ?? -1,
                expectedQuestionVersion ?? -1), ct);
        }
        if (WantsAddJson()) return new JsonResult(result);
        SetStatus(result.Succeeded
            ? Localize(enabled ? "Co-captain field enabled." : "Co-captain field disabled.")
            : Localize(result.Error ?? "The co-captain field could not be changed."),
            result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToQuestions(id);
    }

    public async Task<IActionResult> OnPostMoveAsync(Guid id, Guid questionId, bool up, CancellationToken ct)
    {
        if (!HasSubmittedFormBaseline()) return RedirectToQuestions(id);
        if (!await CanEditAsync(id, ct)) { SetLockedStatus(); return RedirectToQuestions(id); }
        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var lockedEvent = await LockEditableEventAsync(id, ct);
        if (lockedEvent is null || !CanEditSignupQuestions(lockedEvent.State, lockedEvent.DraftLocked))
        {
            SetLockedStatus();
            return RedirectToQuestions(id);
        }
        if (!await HasCurrentFormBaselineAsync(id, ct)) return RedirectToQuestions(id);
        var questions = await dbContext.SignupQuestions.Where(x => x.EventId == id && x.Active).OrderBy(x => x.Position).ToListAsync(ct);
        var customQuestions = questions.Where(x => x.SystemField == SignupSystemField.None && x.Type != SignupQuestionType.Account).ToList();
        var index = customQuestions.FindIndex(x => x.Id == questionId);
        var otherIndex = index + (up ? -1 : 1);
        if (index < 0 || otherIndex < 0 || otherIndex >= customQuestions.Count)
        {
            SetStatus(Localize("That question cannot be reordered."), UiMessageType.Error);
            return RedirectToQuestions(id);
        }
        var question = customQuestions[index]; var other = customQuestions[otherIndex]; var position = question.Position; question.MoveTo(other.Position); other.MoveTo(position);
        await CompleteMutationAsync(id, "signup_question.reordered", question.Id.ToString(), new { from = position }, new { to = question.Position }, ct); await transaction.CommitAsync(ct);
        SetStatus(Localize("Question order saved."), UiMessageType.Success); return RedirectToQuestions(id);
    }

    public async Task<IActionResult> OnPostEditAsync(Guid id, Guid questionId, [Bind(Prefix = "Edit")] EditQuestionInput edit, CancellationToken ct)
    {
        if (!HasSubmittedFormBaseline()) return RedirectToQuestions(id);
        if (!await CanEditAsync(id, ct)) { SetLockedStatus(); return RedirectToQuestions(id); }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var lockedEvent = await LockEditableEventAsync(id, ct);
        if (lockedEvent is null || !CanEditSignupQuestions(lockedEvent.State, lockedEvent.DraftLocked))
        {
            SetLockedStatus();
            return RedirectToQuestions(id);
        }
        if (!await HasCurrentFormBaselineAsync(id, ct)) return RedirectToQuestions(id);

        var form = await dbContext.SignupForms.SingleAsync(x => x.EventId == id, ct);
        var question = await dbContext.SignupQuestions.SingleOrDefaultAsync(x => x.Id == questionId && x.EventId == id && x.Active, ct);
        if (question is null || question.SystemField != SignupSystemField.None || question.Type == SignupQuestionType.Account)
        {
            SetStatus(Localize("That question cannot be edited."), UiMessageType.Error);
            return RedirectToQuestions(id);
        }

        if (form.FirstResponseAt is not null)
        {
            if (HasStructuralEditInput())
            {
                SetStatus(Localize("Answer format is locked after the first response. Delete the old question with confirmation, then add a new optional question."), UiMessageType.Error);
                return RedirectToQuestions(id);
            }
            if (TryGetEditValidationError(out var error)) { SetStatus(error, UiMessageType.Error); return RedirectToQuestions(id); }
            var presentationBefore = Snapshot(question);
            question.UpdatePresentation(edit.Label, edit.HelpText);
            await CompleteMutationAsync(id, "signup_question.edited", question.Id.ToString(), presentationBefore, Snapshot(question), ct);
            await transaction.CommitAsync(ct);
            SetStatus(Localize("Question saved."), UiMessageType.Success);
            return RedirectToQuestions(id);
        }

        if (edit.Type is null) ModelState.AddModelError("Edit.Type", Localize("Choose an answer format."));
        if (TryGetEditValidationError(out var validationError)) { SetStatus(validationError, UiMessageType.Error); return RedirectToQuestions(id); }
        var type = edit.Type!.Value;
        var options = type == SignupQuestionType.SingleChoice ? NormalizeChoices(edit.Options) : null;
        if (type == SignupQuestionType.SingleChoice && options is null) { SetStatus(Localize("Add unique nonblank choices."), UiMessageType.Error); return RedirectToQuestions(id); }
        if (type == SignupQuestionType.Account) { SetStatus(Localize("Account fields are managed in the Playing and Alt account sections."), UiMessageType.Error); return RedirectToQuestions(id); }
        if (form.FirstResponseAt is not null)
        {
            SetStatus(Localize("Answer format is locked after the first response. Delete the old question with confirmation, then add a new optional question."), UiMessageType.Error);
            return RedirectToQuestions(id);
        }
        var before = Snapshot(question);
        question.UpdateDefinition(edit.Label, edit.HelpText, type, edit.Required == true, options, null);
        await CompleteMutationAsync(id, "signup_question.edited", question.Id.ToString(), before, Snapshot(question), ct); await transaction.CommitAsync(ct);
        SetStatus(Localize("Question saved."), UiMessageType.Success); return RedirectToQuestions(id);
    }

    public async Task<IActionResult> OnPostReplaceAsync(Guid id, Guid questionId, [Bind(Prefix = "Replacement")] QuestionInput replacementInput, CancellationToken ct)
    {
        SetStatus(Localize("Format replacement is not a separate workflow. Delete the old question with its current impact confirmation, then add a new optional question."), UiMessageType.Error);
        return RedirectToQuestions(id);
    }

    private async Task CompleteMutationAsync(Guid eventId, string action, string targetId, object? before, object? after, CancellationToken ct)
    {
        var form = await dbContext.SignupForms.SingleAsync(x => x.EventId == eventId, ct);
        dbContext.Entry(form).Property(item => item.Version).IsModified = true;
        dbContext.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), timeProvider.GetUtcNow(), User.GetAccountId(), User.Identity?.Name ?? "admin", action, "signup_question", targetId, null, eventId, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after)));
        await dbContext.SaveChangesAsync(ct);
    }

    private static object Snapshot(SignupQuestion question) => new { question.Id, question.Key, question.Label, question.HelpText, Type = question.Type.ToString(), question.Required, question.Position, question.Options, AccountRole = question.AccountAnswerRole?.ToString(), question.Active, question.ReplacedBySignupQuestionId };

    private async Task<bool> LoadAsync(Guid id, CancellationToken ct)
    {
        var bingoEvent = await dbContext.Events
            .AsNoTracking()
            .Where(item => item.Id == id && item.HiddenAt == null)
            .Select(item => new { item.Name, item.Timezone, item.State, item.DraftLocked, item.Version, item.ParticipantCap, item.RequireSignupCode, HasSignupCode = item.SignupCodeHash != null })
            .SingleOrDefaultAsync(ct);
        if (bingoEvent is null) return false;

        EventId = id;
        EventTimezone = bingoEvent.Timezone;
        EventState = bingoEvent.State;
        DraftLocked = bingoEvent.DraftLocked;
        Settings = new(bingoEvent.Version, bingoEvent.ParticipantCap, true, bingoEvent.RequireSignupCode, bingoEvent.HasSignupCode);
        ConfirmedCount = await dbContext.EventParticipants.AsNoTracking().CountAsync(item => item.EventId == id && item.SignupStatus == SignupStatus.Confirmed, ct);
        WaitingCount = await dbContext.EventParticipants.AsNoTracking().CountAsync(item => item.EventId == id && item.SignupStatus == SignupStatus.WaitingList, ct);
        SignupAdministration = new() { ParticipantCap = bingoEvent.ParticipantCap ?? 1, Version = bingoEvent.Version, WaitingListEnabled = true };
        SignupCode = new() { RequireSignupCode = bingoEvent.RequireSignupCode, Version = bingoEvent.Version };
        EventName = bingoEvent.Name;
        CanEdit = CanEditSignupQuestions(bingoEvent.State, bingoEvent.DraftLocked);
        var form = await dbContext.SignupForms.AsNoTracking().Where(item => item.EventId == id).Select(item => new { item.FirstResponseAt, item.Version }).SingleOrDefaultAsync(ct);
        if (form is null)
        {
            // U3-Q11 / review B-L1: imported history has no recorded form, including after reopening. Reads never invent one.
            CanEdit = false;
            return true;
        }
        HasForm = true;
        HasFirstResponse = form.FirstResponseAt is not null;
        FirstResponseAt = form.FirstResponseAt;
        FormVersion = form.Version;
        var allQuestions = await dbContext.SignupQuestions
            .AsNoTracking()
            .Where(question => question.EventId == id)
            .OrderBy(question => question.Position)
            .ToListAsync(ct);
        AllQuestions = allQuestions;
        Questions = allQuestions.Where(question => question.Active).ToList();
        CoCaptainQuestion = allQuestions.SingleOrDefault(question => question.SystemField == SignupSystemField.CoCaptainName);
        var impactedQuestions = allQuestions.Where(question => question.Active && (question.SystemField == SignupSystemField.None || question.SystemField == SignupSystemField.CoCaptainName)).ToList();
        var impactedIds = impactedQuestions.Select(question => question.Id).ToList();
        if (impactedIds.Count == 0)
        {
            QuestionImpacts = new Dictionary<Guid, SignupQuestionImpact>();
        }
        else
        {
            var answerCounts = await dbContext.SignupAnswers.AsNoTracking()
                .Where(answer => impactedIds.Contains(answer.SignupQuestionId))
                .GroupBy(answer => answer.SignupQuestionId)
                .Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.Key, item => item.Count, ct);
            var releaseCounts = await dbContext.EventParticipantCharacters.AsNoTracking()
                .Where(assignment => assignment.EventId == id && assignment.SignupQuestionId != null && impactedIds.Contains(assignment.SignupQuestionId.Value) && assignment.ReleasedAt == null)
                .GroupBy(assignment => assignment.SignupQuestionId!.Value)
                .Select(group => new { Key = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.Key, item => item.Count, ct);
            QuestionImpacts = impactedQuestions.ToDictionary(
                question => question.Id,
                question => new SignupQuestionImpact(
                    question.Id,
                    answerCounts.GetValueOrDefault(question.Id),
                    releaseCounts.GetValueOrDefault(question.Id),
                    question.Version));
        }
        return true;
    }

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var executed = await next();
        var conflict = executed.Exception;
        while (conflict is not null && conflict is not DbUpdateConcurrencyException
            && conflict is not PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
            conflict = conflict.InnerException;
        if (conflict is null) return;
        // Handler transaction disposal rolls back before the exception reaches this filter.
        dbContext.ChangeTracker.Clear();
        executed.ExceptionHandled = true;
        SetStatus(Localize("This signup form changed while you were editing it. Review the latest values and try again."), UiMessageType.Error);
        executed.Result = RedirectToQuestions((Guid)context.HandlerArguments["id"]!);
    }

    private bool HasSubmittedFormBaseline()
    {
        if (ExpectedFormVersion is not null
            && (!ModelState.TryGetValue(nameof(ExpectedFormVersion), out var state) || state.Errors.Count == 0)) return true;
        SetStatus(Localize("This signup form changed while you were editing it. Review the latest values and try again."), UiMessageType.Error);
        return false;
    }

    private async Task<bool> HasCurrentFormBaselineAsync(Guid id, CancellationToken ct)
    {
        var version = await dbContext.SignupForms.AsNoTracking()
            .Where(item => item.EventId == id).Select(item => item.Version).SingleAsync(ct);
        if (ExpectedFormVersion is not null
            && (!ModelState.TryGetValue(nameof(ExpectedFormVersion), out var state) || state.Errors.Count == 0)
            && ExpectedFormVersion == version) return true;
        SetStatus(Localize("This signup form changed while you were editing it. Review the latest values and try again."), UiMessageType.Error);
        return false;
    }

    private Task<bool> CanEditAsync(Guid id, CancellationToken ct) =>
        dbContext.Events.AnyAsync(
            item => item.Id == id
                && item.HiddenAt == null
                && !item.DraftLocked
                && (item.State == EventState.Draft || item.State == EventState.SignupOpen || item.State == EventState.SignupClosed),
            ct);

    private Task<BingoEvent?> LockEditableEventAsync(Guid id, CancellationToken ct) =>
        dbContext.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {id} AND hidden_at IS NULL FOR UPDATE").SingleOrDefaultAsync(ct);

    private static bool CanEditSignupQuestions(EventState state, bool draftLocked) =>
        !draftLocked && state is (EventState.Draft or EventState.SignupOpen or EventState.SignupClosed);

    private static string? NormalizeChoices(string? source)
    {
        var choices = (source ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return choices.Length == 0 || choices.Distinct(StringComparer.OrdinalIgnoreCase).Count() != choices.Length ? null : string.Join('\n', choices);
    }

    private bool HasStructuralEditInput() =>
        Request.HasFormContentType && Request.Form.Keys.Any(key => key is "Edit.Type" or "Edit.Options" or "Edit.AccountRole" or "Edit.Required");

    private bool TryGetEditValidationError(out string error)
    {
        foreach (var key in new[] { "Edit.Label", "Edit.HelpText", "Edit.Type", "Edit.Options", "Edit.AccountRole", "Edit.Required" })
        {
            if (!ModelState.TryGetValue(key, out var state) || state.Errors.Count == 0) continue;
            error = key switch
            {
                "Edit.Label" when string.IsNullOrWhiteSpace(editLabelValue(state)) => Localize("Enter a question label."),
                "Edit.Options" => Localize("Enter valid choices."),
                "Edit.AccountRole" => Localize("Choose an account role."),
                _ => state.Errors[0].ErrorMessage
            };
            if (string.IsNullOrWhiteSpace(error)) error = Localize("Enter a valid question value.");
            return true;
        }
        error = string.Empty;
        return false;

        static string? editLabelValue(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateEntry state) => state.AttemptedValue;
    }

    private void SetLockedStatus() =>
        SetStatus(
            Localize("Signup questions can only be changed before the draft starts."),
            UiMessageType.Error);

    private IActionResult RedirectToQuestions(Guid id) => WantsAddJson()
        ? new JsonResult(new { succeeded = operationType != UiMessageType.Error, error = operationType == UiMessageType.Error ? operationMessage : null, message = operationMessage })
        : RedirectToPage("SignupSetup", new { id, tab = "form" });

    private string? operationMessage;
    private UiMessageType operationType;
    private void SetStatus(string message, UiMessageType type)
    {
        operationMessage = message; operationType = type;
        if (WantsAddJson()) return;
        TempData["StatusMessage"] = message;
        TempData[UiMessage.TypeKey] = type.ToString();
    }

    private string Localize(string key, params object[] arguments)
        => text?[key, arguments].Value ?? string.Format(System.Globalization.CultureInfo.CurrentCulture, key, arguments);

    public static string FormatType(SignupQuestionType type) => type switch
    {
        SignupQuestionType.Text => "Text",
        SignupQuestionType.Number => "Number",
        SignupQuestionType.YesNo => "Yes or no",
        SignupQuestionType.SingleChoice => "Choose one answer",
        SignupQuestionType.Account => "OSRS account",
        _ => "Answer"
    };

    public sealed class QuestionInput
    {
        [Required, StringLength(300)]
        public string Label { get; set; } = string.Empty;

        [StringLength(1000)] public string? HelpText { get; set; }

        [Display(Name = "Answer format")]
        public SignupQuestionType Type { get; set; } = SignupQuestionType.Text;

        public bool Required { get; set; }

        [StringLength(4000), Display(Name = "Available answers")]
        public string? Options { get; set; }
        [Display(Name = "Account role")] public EventCharacterRole? AccountRole { get; set; }
    }

    public sealed class AccountPresentationInput
    {
        [Required, StringLength(300)] public string Label { get; set; } = string.Empty;
    }

    public sealed class EditQuestionInput
    {
        [Required, StringLength(300)] public string Label { get; set; } = string.Empty;
        [StringLength(1000)] public string? HelpText { get; set; }
        public SignupQuestionType? Type { get; set; }
        public bool? Required { get; set; }
        [StringLength(4000)] public string? Options { get; set; }
        public EventCharacterRole? AccountRole { get; set; }
    }

    public sealed class SignupAdministrationInput
    {
        public int ParticipantCap { get; set; }
        public bool WaitingListEnabled { get; set; }
        public long Version { get; set; }
        public bool ConfirmWaitingListDisablement { get; set; }
    }

    public sealed class SignupCodeInput
    {
        public bool RequireSignupCode { get; set; }
        [StringLength(100), Display(Name = "New signup code")] public string? NewSignupCode { get; set; }
        public long Version { get; set; }
    }
}
