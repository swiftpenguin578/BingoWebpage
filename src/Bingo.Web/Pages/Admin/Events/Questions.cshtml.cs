using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bingo.Application.Access;
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
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class QuestionsModel(ApplicationDbContext dbContext, TimeProvider timeProvider, ISignupService signupService, IStringLocalizer<SharedResource>? text = null) : PageModel
{
    public IReadOnlyList<SignupQuestion> Questions { get; private set; } = [];
    public SignupQuestion? CoCaptainQuestion { get; private set; }
    public IReadOnlyDictionary<Guid, SignupQuestionImpact> QuestionImpacts { get; private set; } = new Dictionary<Guid, SignupQuestionImpact>();

    public QuestionInput Input { get; set; } = new();
    public bool CanEdit { get; private set; }
    public bool HasFirstResponse { get; private set; }
    [BindProperty] public bool Overlay { get; set; }
    public bool IsOverlay => Overlay || string.Equals(Request.Query["overlay"], "1", StringComparison.Ordinal);
    public string EventName { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        Overlay = string.Equals(Request.Query["overlay"], "1", StringComparison.Ordinal);
        return await LoadAsync(id, ct) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, [Bind(Prefix = "Input")] QuestionInput input, [FromForm] bool overlay, CancellationToken ct)
    {
        overlay = ResolveSubmittedOverlay(overlay);
        Input = input;
        Overlay = overlay;
        if (!await CanEditAsync(id, ct))
        {
            SetLockedStatus();
            return RedirectToQuestions(id, overlay);
        }

        if (input.Type == SignupQuestionType.Account)
        {
            SetStatus(Localize("Account fields are managed in the Playing and Alt account sections."), UiMessageType.Error);
            return RedirectToQuestions(id, overlay);
        }

        var options = input.Type == SignupQuestionType.SingleChoice ? NormalizeChoices(input.Options) : null;
        if (input.Type == SignupQuestionType.SingleChoice && options is null)
        {
            ModelState.AddModelError("Input.Options", Localize("Add at least one choice."));
        }
        var form = await dbContext.SignupForms.SingleAsync(item => item.EventId == id, ct);
        if (form.FirstResponseAt is not null) input.Required = false;

        if (!ModelState.IsValid)
        {
            await LoadAsync(id, ct);
            return Page();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var lockedEvent = await LockEditableEventAsync(id, ct);
        if (lockedEvent is null || !CanEditSignupQuestions(lockedEvent.State, lockedEvent.DraftLocked))
        {
            SetLockedStatus();
            return RedirectToQuestions(id, overlay);
        }
        form = await dbContext.SignupForms.SingleAsync(item => item.EventId == id, ct);
        if (form.FirstResponseAt is not null) input.Required = false;
        var position = (await dbContext.SignupQuestions
            .Where(question => question.EventId == id)
            .MaxAsync(question => (int?)question.Position, ct) ?? 0) + 1;
        var key = await CreateUniqueKeyAsync(id, input.Label, ct);
        var question = new SignupQuestion(
            Guid.NewGuid(),
            form.Id,
            id,
            key,
            input.Label,
            input.Type,
            input.Required,
            position,
            options,
            accountAnswerRole: null,
            helpText: input.HelpText);

        dbContext.SignupQuestions.Add(question);
        await CompleteMutationAsync(id, "signup_question.created", question.Id.ToString(), null, Snapshot(question), ct);
        await transaction.CommitAsync(ct);

        SetStatus(Localize("Question added."), UiMessageType.Success);
        return RedirectToQuestions(id, overlay);
    }

    public async Task<IActionResult> OnPostAddAccountAsync(
        Guid id,
        [FromForm] EventCharacterRole role,
        [FromForm] bool overlay,
        CancellationToken ct)
    {
        overlay = ResolveSubmittedOverlay(overlay);
        if (role is not (EventCharacterRole.Playing or EventCharacterRole.Informational))
        {
            SetStatus(Localize("Choose a valid account section."), UiMessageType.Error);
            return RedirectToQuestions(id, overlay);
        }
        if (!await CanEditAsync(id, ct))
        {
            SetLockedStatus();
            return RedirectToQuestions(id, overlay);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var lockedEvent = await LockEditableEventAsync(id, ct);
        if (lockedEvent is null || !CanEditSignupQuestions(lockedEvent.State, lockedEvent.DraftLocked))
        {
            SetLockedStatus();
            return RedirectToQuestions(id, overlay);
        }

        var form = await dbContext.SignupForms.SingleAsync(item => item.EventId == id, ct);
        var position = (await dbContext.SignupQuestions
            .Where(question => question.EventId == id)
            .MaxAsync(question => (int?)question.Position, ct) ?? 0) + 1;
        var label = role == EventCharacterRole.Playing ? "Playing account" : "Alt account";
        var key = await CreateUniqueKeyAsync(id, label, ct);
        var question = new SignupQuestion(
            Guid.NewGuid(),
            form.Id,
            id,
            key,
            label,
            SignupQuestionType.Account,
            false,
            position,
            options: null,
            accountAnswerRole: role,
            helpText: null);

        dbContext.SignupQuestions.Add(question);
        await CompleteMutationAsync(id, "signup_question.account_added", question.Id.ToString(), null, Snapshot(question), ct);
        await transaction.CommitAsync(ct);
        SetStatus(Localize("Account field added."), UiMessageType.Success);
        return RedirectToQuestions(id, overlay);
    }

    public async Task<IActionResult> OnPostEditAccountAsync(
        Guid id,
        Guid questionId,
        [Bind(Prefix = "Account")] AccountPresentationInput account,
        [FromForm] bool overlay,
        CancellationToken ct)
    {
        overlay = ResolveSubmittedOverlay(overlay);
        if (!await CanEditAsync(id, ct))
        {
            SetLockedStatus();
            return RedirectToQuestions(id, overlay);
        }
        if (!ModelState.IsValid)
        {
            SetStatus(Localize("Enter a question label."), UiMessageType.Error);
            return RedirectToQuestions(id, overlay);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var lockedEvent = await LockEditableEventAsync(id, ct);
        if (lockedEvent is null || !CanEditSignupQuestions(lockedEvent.State, lockedEvent.DraftLocked))
        {
            SetLockedStatus();
            return RedirectToQuestions(id, overlay);
        }
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
            return RedirectToQuestions(id, overlay);
        }

        var before = Snapshot(question);
        question.UpdatePresentation(account.Label, question.HelpText);
        await CompleteMutationAsync(id, "signup_question.account_edited", question.Id.ToString(), before, Snapshot(question), ct);
        await transaction.CommitAsync(ct);
        SetStatus(Localize("Account field saved."), UiMessageType.Success);
        return RedirectToQuestions(id, overlay);
    }

    public async Task<IActionResult> OnPostDeactivateAsync(
        Guid id,
        Guid questionId,
        [FromForm] bool overlay,
        [FromForm] bool confirmed,
        [FromForm] int? expectedAnswerCount,
        [FromForm] int? expectedEventRegistrationReleaseCount,
        [FromForm] int? expectedQuestionVersion,
        CancellationToken ct)
    {
        overlay = ResolveSubmittedOverlay(overlay);
        var actorId = User.GetAccountId();
        if (actorId is null)
        {
            SetStatus(Localize("Admin access is required."), UiMessageType.Error);
            return RedirectToQuestions(id, overlay);
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
        SetStatus(result.Succeeded ? Localize("Question removed.") : Localize(result.Error ?? "The question could not be removed."), result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToQuestions(id, overlay);
    }

    public async Task<IActionResult> OnPostCoCaptainAsync(
        Guid id,
        Guid questionId,
        [FromForm] bool enabled,
        [FromForm] bool overlay,
        [FromForm] bool confirmed,
        [FromForm] int? expectedAnswerCount,
        [FromForm] int? expectedEventRegistrationReleaseCount,
        [FromForm] int? expectedQuestionVersion,
        CancellationToken ct)
    {
        overlay = ResolveSubmittedOverlay(overlay);
        var actorId = User.GetAccountId();
        if (actorId is null)
        {
            SetStatus(Localize("Admin access is required."), UiMessageType.Error);
            return RedirectToQuestions(id, overlay);
        }
        SignupAdministrationResult result;
        if (enabled)
        {
            result = await signupService.EnableCoCaptainAsync(id, questionId, actorId.Value, User.Identity?.Name ?? string.Empty, ct);
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
        SetStatus(result.Succeeded
            ? Localize(enabled ? "Co-captain field enabled." : "Co-captain field disabled.")
            : Localize(result.Error ?? "The co-captain field could not be changed."),
            result.Succeeded ? UiMessageType.Success : UiMessageType.Error);
        return RedirectToQuestions(id, overlay);
    }

    public async Task<IActionResult> OnPostMoveAsync(Guid id, Guid questionId, bool up, [FromForm] bool overlay, CancellationToken ct)
    {
        overlay = ResolveSubmittedOverlay(overlay);
        if (!await CanEditAsync(id, ct)) { SetLockedStatus(); return RedirectToQuestions(id, overlay); }
        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var lockedEvent = await LockEditableEventAsync(id, ct);
        if (lockedEvent is null || !CanEditSignupQuestions(lockedEvent.State, lockedEvent.DraftLocked))
        {
            SetLockedStatus();
            return RedirectToQuestions(id, overlay);
        }
        var questions = await dbContext.SignupQuestions.Where(x => x.EventId == id && x.Active).OrderBy(x => x.Position).ToListAsync(ct);
        var customQuestions = questions.Where(x => x.SystemField == SignupSystemField.None && x.Type != SignupQuestionType.Account).ToList();
        var index = customQuestions.FindIndex(x => x.Id == questionId);
        var otherIndex = index + (up ? -1 : 1);
        if (index < 0 || otherIndex < 0 || otherIndex >= customQuestions.Count)
        {
            SetStatus(Localize("That question cannot be reordered."), UiMessageType.Error);
            return RedirectToQuestions(id, overlay);
        }
        var question = customQuestions[index]; var other = customQuestions[otherIndex]; var position = question.Position; question.MoveTo(other.Position); other.MoveTo(position);
        await CompleteMutationAsync(id, "signup_question.reordered", question.Id.ToString(), new { from = position }, new { to = question.Position }, ct); await transaction.CommitAsync(ct);
        SetStatus(Localize("Question order saved."), UiMessageType.Success); return RedirectToQuestions(id, overlay);
    }

    public async Task<IActionResult> OnPostEditAsync(Guid id, Guid questionId, [Bind(Prefix = "Edit")] EditQuestionInput edit, [FromForm] bool overlay, CancellationToken ct)
    {
        overlay = ResolveSubmittedOverlay(overlay);
        if (!await CanEditAsync(id, ct)) { SetLockedStatus(); return RedirectToQuestions(id, overlay); }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var lockedEvent = await LockEditableEventAsync(id, ct);
        if (lockedEvent is null || !CanEditSignupQuestions(lockedEvent.State, lockedEvent.DraftLocked))
        {
            SetLockedStatus();
            return RedirectToQuestions(id, overlay);
        }

        var form = await dbContext.SignupForms.SingleAsync(x => x.EventId == id, ct);
        var question = await dbContext.SignupQuestions.SingleOrDefaultAsync(x => x.Id == questionId && x.EventId == id && x.Active, ct);
        if (question is null || question.SystemField != SignupSystemField.None || question.Type == SignupQuestionType.Account)
        {
            SetStatus(Localize("That question cannot be edited."), UiMessageType.Error);
            return RedirectToQuestions(id, overlay);
        }

        if (form.FirstResponseAt is not null)
        {
            if (HasStructuralEditInput())
            {
                SetStatus(Localize("Answer format is locked after the first response. Delete the old question with confirmation, then add a new optional question."), UiMessageType.Error);
                return RedirectToQuestions(id, overlay);
            }
            if (TryGetEditValidationError(out var error)) { SetStatus(error, UiMessageType.Error); return RedirectToQuestions(id, overlay); }
            var presentationBefore = Snapshot(question);
            question.UpdatePresentation(edit.Label, edit.HelpText);
            await CompleteMutationAsync(id, "signup_question.edited", question.Id.ToString(), presentationBefore, Snapshot(question), ct);
            await transaction.CommitAsync(ct);
            SetStatus(Localize("Question saved."), UiMessageType.Success);
            return RedirectToQuestions(id, overlay);
        }

        if (edit.Type is null) ModelState.AddModelError("Edit.Type", Localize("Choose an answer format."));
        if (TryGetEditValidationError(out var validationError)) { SetStatus(validationError, UiMessageType.Error); return RedirectToQuestions(id, overlay); }
        var type = edit.Type!.Value;
        var options = type == SignupQuestionType.SingleChoice ? NormalizeChoices(edit.Options) : null;
        if (type == SignupQuestionType.SingleChoice && options is null) { SetStatus(Localize("Add unique nonblank choices."), UiMessageType.Error); return RedirectToQuestions(id, overlay); }
        if (type == SignupQuestionType.Account) { SetStatus(Localize("Account fields are managed in the Playing and Alt account sections."), UiMessageType.Error); return RedirectToQuestions(id, overlay); }
        if (form.FirstResponseAt is not null)
        {
            SetStatus(Localize("Answer format is locked after the first response. Delete the old question with confirmation, then add a new optional question."), UiMessageType.Error);
            return RedirectToQuestions(id, overlay);
        }
        var before = Snapshot(question);
        question.UpdateDefinition(edit.Label, edit.HelpText, type, edit.Required == true, options, null);
        await CompleteMutationAsync(id, "signup_question.edited", question.Id.ToString(), before, Snapshot(question), ct); await transaction.CommitAsync(ct);
        SetStatus(Localize("Question saved."), UiMessageType.Success); return RedirectToQuestions(id, overlay);
    }

    public async Task<IActionResult> OnPostReplaceAsync(Guid id, Guid questionId, [Bind(Prefix = "Replacement")] QuestionInput replacementInput, [FromForm] bool overlay, CancellationToken ct)
    {
        overlay = ResolveSubmittedOverlay(overlay);
        SetStatus(Localize("Format replacement is not a separate workflow. Delete the old question with its current impact confirmation, then add a new optional question."), UiMessageType.Error);
        return RedirectToQuestions(id, overlay);
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
            .Where(item => item.Id == id)
            .Select(item => new { item.Name, item.State, item.DraftLocked })
            .SingleOrDefaultAsync(ct);
        if (bingoEvent is null) return false;

        EventName = bingoEvent.Name;
        CanEdit = CanEditSignupQuestions(bingoEvent.State, bingoEvent.DraftLocked);
        var form = await dbContext.SignupForms.AsNoTracking().Where(item => item.EventId == id).Select(item => new { item.FirstResponseAt }).SingleAsync(ct);
        HasFirstResponse = form.FirstResponseAt is not null;
        var allQuestions = await dbContext.SignupQuestions
            .AsNoTracking()
            .Where(question => question.EventId == id)
            .OrderBy(question => question.Position)
            .ToListAsync(ct);
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

    private async Task<string> CreateUniqueKeyAsync(Guid id, string label, CancellationToken ct)
    {
        var baseKey = EventSlugGenerator.Generate(label).Replace('-', '_');
        if (string.Equals(baseKey, SignupQuestion.CoCaptainKey, StringComparison.OrdinalIgnoreCase)) baseKey += "_custom";
        var key = baseKey;
        for (var suffix = 2;
             await dbContext.SignupQuestions.AnyAsync(question => question.EventId == id && question.Key == key, ct);
             suffix++)
        {
            key = $"{baseKey}_{suffix}";
        }

        return key;
    }

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

    private RedirectToPageResult RedirectToQuestions(Guid id, bool? overlay = null) =>
        RedirectToPage(new { id, overlay = (overlay ?? Overlay) ? "1" : null });

    private bool ResolveSubmittedOverlay(bool overlay)
    {
        if (!Request.HasFormContentType) return overlay;

        var submitted = Request.Form["overlay"].ToString();
        ModelState.Remove("overlay");
        return overlay || string.Equals(submitted, "1", StringComparison.Ordinal);
    }

    private void SetStatus(string message, UiMessageType type)
    {
        TempData["StatusMessage"] = message;
        TempData[UiMessage.TypeKey] = type.ToString();
    }

    private string Localize(string key, params object[] arguments)
        => text?[key, arguments].Value ?? string.Format(System.Globalization.CultureInfo.CurrentCulture, key, arguments);

    public static string FormatType(SignupQuestionType type) => type switch
    {
        SignupQuestionType.Text => "Short text answer",
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

}
