using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Security;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Security;
using Bingo.Web.Events;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class QuestionsModel(ApplicationDbContext dbContext, IAuditWriter auditWriter, ISecretHasher hasher, TimeProvider timeProvider) : PageModel
{
    public IReadOnlyList<SignupQuestion> Questions { get; private set; } = [];

    public QuestionInput Input { get; set; } = new();
    public SignupSettingsInput Settings { get; set; } = new();

    public bool CanEdit { get; private set; }
    public bool CanEditSettings { get; private set; }
    public bool HasFirstResponse { get; private set; }
    [BindProperty] public bool Overlay { get; set; }
    public bool IsOverlay => Overlay || string.Equals(Request.Query["overlay"], "1", StringComparison.Ordinal);
    public string EventName { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        Overlay = string.Equals(Request.Query["overlay"], "1", StringComparison.Ordinal);
        return await LoadAsync(id, ct) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, [Bind(Prefix = "Input")] QuestionInput input, CancellationToken ct)
    {
        Input = input;
        if (!await CanEditAsync(id, ct))
        {
            SetLockedStatus();
            return RedirectToQuestions(id);
        }

        var options = input.Type == SignupQuestionType.SingleChoice ? NormalizeChoices(input.Options) : null;
        if (input.Type == SignupQuestionType.SingleChoice && options is null)
        {
            ModelState.AddModelError("Input.Options", "Add at least one choice.");
        }
        if (input.Type == SignupQuestionType.Account && input.AccountRole is null) ModelState.AddModelError("Input.AccountRole", "Choose Regular account or Alt account.");
        var form = await dbContext.SignupForms.SingleAsync(item => item.EventId == id, ct);
        if (input.Type == SignupQuestionType.Account || form.FirstResponseAt is not null) input.Required = false;

        if (!ModelState.IsValid)
        {
            await LoadAsync(id, ct);
            return Page();
        }

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
            accountAnswerRole: input.Type == SignupQuestionType.Account ? input.AccountRole : null,
            helpText: input.HelpText);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        dbContext.SignupQuestions.Add(question);
        await CompleteMutationAsync(id, "signup_question.created", question.Id.ToString(), null, Snapshot(question), ct);
        await transaction.CommitAsync(ct);

        SetStatus("Question added.", UiMessageType.Success);
        return RedirectToQuestions(id);
    }

    public async Task<IActionResult> OnPostDeactivateAsync(Guid id, Guid questionId, CancellationToken ct)
    {
        if (!await CanEditAsync(id, ct))
        {
            SetLockedStatus();
            return RedirectToQuestions(id);
        }

        var question = await dbContext.SignupQuestions
            .SingleOrDefaultAsync(item => item.Id == questionId && item.EventId == id, ct);
        if (question is null) return NotFound();
        if (question.SystemField != SignupSystemField.None)
        {
            SetStatus("Standard questions cannot be removed.", UiMessageType.Error);
            return RedirectToQuestions(id);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        var before = Snapshot(question);
        question.Deactivate(User.GetAccountId());
        await CompleteMutationAsync(id, "signup_question.deactivated", question.Id.ToString(), before, Snapshot(question), ct);
        await transaction.CommitAsync(ct);

        SetStatus("Question removed.", UiMessageType.Success);
        return RedirectToQuestions(id);
    }

    public async Task<IActionResult> OnPostMoveAsync(Guid id, Guid questionId, bool up, CancellationToken ct)
    {
        if (!await CanEditAsync(id, ct)) { SetLockedStatus(); return RedirectToQuestions(id); }
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        var questions = await dbContext.SignupQuestions.Where(x => x.EventId == id && x.Active).OrderBy(x => x.Position).ToListAsync(ct);
        var index = questions.FindIndex(x => x.Id == questionId);
        if (index < 0 || questions[index].SystemField != SignupSystemField.None) { SetStatus("That question cannot be reordered.", UiMessageType.Error); return RedirectToQuestions(id); }
        var otherIndex = index + (up ? -1 : 1); if (otherIndex < 0 || otherIndex >= questions.Count || questions[otherIndex].SystemField != SignupSystemField.None) { SetStatus("That question cannot be reordered.", UiMessageType.Error); return RedirectToQuestions(id); }
        var question = questions[index]; var other = questions[otherIndex]; var position = question.Position; question.MoveTo(other.Position); other.MoveTo(position);
        await CompleteMutationAsync(id, "signup_question.reordered", question.Id.ToString(), new { from = position }, new { to = question.Position }, ct); await transaction.CommitAsync(ct);
        SetStatus("Question order saved.", UiMessageType.Success); return RedirectToQuestions(id);
    }

    public async Task<IActionResult> OnPostEditAsync(Guid id, Guid questionId, [Bind(Prefix = "Edit")] EditQuestionInput edit, CancellationToken ct)
    {
        var form = await dbContext.SignupForms.SingleAsync(x => x.EventId == id, ct);
        if (!await CanEditAsync(id, ct)) { SetLockedStatus(); return RedirectToQuestions(id); }
        var question = await dbContext.SignupQuestions.SingleOrDefaultAsync(x => x.Id == questionId && x.EventId == id, ct);
        if (question is null || question.SystemField != SignupSystemField.None) return NotFound();
        if (form.FirstResponseAt is not null)
        {
            if (HasStructuralEditInput())
            {
                SetStatus("Answer format is locked after the first response. Use replacement for a new optional question.", UiMessageType.Error);
                return RedirectToQuestions(id);
            }
            if (TryGetEditValidationError(out var error)) { SetStatus(error, UiMessageType.Error); return RedirectToQuestions(id); }
            await using var presentationTransaction = await dbContext.Database.BeginTransactionAsync(ct);
            var presentationBefore = Snapshot(question);
            question.UpdatePresentation(edit.Label, edit.HelpText);
            await CompleteMutationAsync(id, "signup_question.edited", question.Id.ToString(), presentationBefore, Snapshot(question), ct);
            await presentationTransaction.CommitAsync(ct);
            SetStatus("Question saved.", UiMessageType.Success);
            return RedirectToQuestions(id);
        }

        if (edit.Type is null) ModelState.AddModelError("Edit.Type", "Choose an answer format.");
        if (TryGetEditValidationError(out var validationError)) { SetStatus(validationError, UiMessageType.Error); return RedirectToQuestions(id); }
        var type = edit.Type!.Value;
        var options = type == SignupQuestionType.SingleChoice ? NormalizeChoices(edit.Options) : null;
        if (type == SignupQuestionType.SingleChoice && options is null) { SetStatus("Add unique nonblank choices.", UiMessageType.Error); return RedirectToQuestions(id); }
        if (type == SignupQuestionType.Account && edit.AccountRole is null) { SetStatus("Choose an account role.", UiMessageType.Error); return RedirectToQuestions(id); }
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        var before = Snapshot(question);
        question.UpdateDefinition(edit.Label, edit.HelpText, type, type == SignupQuestionType.Account ? false : edit.Required == true, options, type == SignupQuestionType.Account ? edit.AccountRole : null);
        await CompleteMutationAsync(id, "signup_question.edited", question.Id.ToString(), before, Snapshot(question), ct); await transaction.CommitAsync(ct);
        SetStatus("Question saved.", UiMessageType.Success); return RedirectToQuestions(id);
    }

    public async Task<IActionResult> OnPostReplaceAsync(Guid id, Guid questionId, [Bind(Prefix = "Replacement")] QuestionInput replacementInput, CancellationToken ct)
    {
        if (!await CanEditAsync(id, ct)) { SetLockedStatus(); return RedirectToQuestions(id); }
        var form = await dbContext.SignupForms.SingleAsync(x => x.EventId == id, ct);
        var original = await dbContext.SignupQuestions.SingleOrDefaultAsync(x => x.Id == questionId && x.EventId == id && x.Active, ct);
        if (original is null || original.SystemField != SignupSystemField.None) { SetStatus("That question cannot be replaced.", UiMessageType.Error); return RedirectToQuestions(id); }
        if (form.FirstResponseAt is null) { SetStatus("Use ordinary editing until the first response is received.", UiMessageType.Error); return RedirectToQuestions(id); }
        var options = replacementInput.Type == SignupQuestionType.SingleChoice ? NormalizeChoices(replacementInput.Options) : null;
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(replacementInput.Label)) { SetStatus("Enter a question label.", UiMessageType.Error); return RedirectToQuestions(id); }
        if (replacementInput.Type == SignupQuestionType.SingleChoice && options is null) { SetStatus("Add unique nonblank choices.", UiMessageType.Error); return RedirectToQuestions(id); }
        if (replacementInput.Type == SignupQuestionType.Account && replacementInput.AccountRole is null) { SetStatus("Choose an account role.", UiMessageType.Error); return RedirectToQuestions(id); }
        var replacement = new SignupQuestion(Guid.NewGuid(), form.Id, id, await CreateUniqueKeyAsync(id, replacementInput.Label, ct), replacementInput.Label, replacementInput.Type, false, original.Position, options, accountAnswerRole: replacementInput.Type == SignupQuestionType.Account ? replacementInput.AccountRole : null, helpText: replacementInput.HelpText);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        var before = Snapshot(original);
        original.Deactivate(User.GetAccountId(), timeProvider.GetUtcNow(), "structural replacement");
        original.ReplaceWith(replacement.Id);
        dbContext.SignupQuestions.Add(replacement);
        await CompleteMutationAsync(id, "signup_question.replaced", original.Id.ToString(), before, new { original = Snapshot(original), replacement = Snapshot(replacement) }, ct);
        await transaction.CommitAsync(ct);
        SetStatus("Question replaced. Existing answers remain with the original question.", UiMessageType.Success);
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

    public async Task<IActionResult> OnPostSignupCodeAsync(Guid id, [Bind(Prefix = "Settings")] SignupSettingsInput settings, CancellationToken ct)
    {
        var bingoEvent = await dbContext.Events.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (bingoEvent is null) return NotFound();
        if (bingoEvent.DraftLocked || bingoEvent.State is not (EventState.Draft or EventState.SignupOpen or EventState.SignupClosed))
        {
            SetStatus("Signup settings are locked because the draft has started or this event has moved on.", UiMessageType.Error);
            return RedirectToQuestions(id);
        }
        try
        {
            var form = await dbContext.SignupForms.SingleAsync(item => item.EventId == id, ct);
            if (settings.RequireSignupCode && !form.RequireSignupCode && string.IsNullOrWhiteSpace(settings.NewSignupCode))
            {
                SetStatus("Enter a new signup code or turn code protection off.", UiMessageType.Error);
                return RedirectToQuestions(id);
            }
            var hash = !settings.RequireSignupCode ? null : string.IsNullOrWhiteSpace(settings.NewSignupCode) ? form.SignupCodeHash : hasher.Hash(settings.NewSignupCode);
            bingoEvent.ConfigureSignup(bingoEvent.WaitingListEnabled, settings.RequireSignupCode, hash);
            form.ConfigureSignupCode(settings.RequireSignupCode, hash);
            form.AdvanceVersion();
            await dbContext.SaveChangesAsync(ct);
            await auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "event.signup_code_changed", "event", id.ToString(), "Signup-code protection changed.", ct);
            SetStatus("Signup-code protection saved.", UiMessageType.Success);
        }
        catch (InvalidOperationException)
        {
            SetStatus("The signup-code setting could not be changed in this event state.", UiMessageType.Error);
        }
        return RedirectToQuestions(id);
    }

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
        CanEditSettings = !bingoEvent.DraftLocked && bingoEvent.State is (EventState.Draft or EventState.SignupOpen or EventState.SignupClosed);
        var form = await dbContext.SignupForms.AsNoTracking().Where(item => item.EventId == id).Select(item => new { item.RequireSignupCode, item.FirstResponseAt }).SingleAsync(ct);
        HasFirstResponse = form.FirstResponseAt is not null;
        Settings = new SignupSettingsInput { RequireSignupCode = form.RequireSignupCode };
        Questions = await dbContext.SignupQuestions
            .AsNoTracking()
            .Where(question => question.EventId == id && question.Active)
            .OrderBy(question => question.Position)
            .ToListAsync(ct);
        return true;
    }

    private Task<bool> CanEditAsync(Guid id, CancellationToken ct) =>
        dbContext.Events.AnyAsync(
            item => item.Id == id
                && !item.DraftLocked
                && (item.State == EventState.Draft || item.State == EventState.SignupClosed),
            ct);

    private static bool CanEditSignupQuestions(EventState state, bool draftLocked) =>
        !draftLocked && state is EventState.Draft or EventState.SignupClosed;

    private async Task<string> CreateUniqueKeyAsync(Guid id, string label, CancellationToken ct)
    {
        var baseKey = EventSlugGenerator.Generate(label).Replace('-', '_');
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
                "Edit.Label" when string.IsNullOrWhiteSpace(editLabelValue(state)) => "Enter a question label.",
                "Edit.Options" => "Enter valid choices.",
                "Edit.AccountRole" => "Choose an account role.",
                _ => state.Errors[0].ErrorMessage
            };
            if (string.IsNullOrWhiteSpace(error)) error = "Enter a valid question value.";
            return true;
        }
        error = string.Empty;
        return false;

        static string? editLabelValue(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateEntry state) => state.AttemptedValue;
    }

    private void SetLockedStatus() =>
        SetStatus(
            "Signup questions can only be changed while signups are closed and before the draft starts.",
            UiMessageType.Error);

    private RedirectToPageResult RedirectToQuestions(Guid id) =>
        RedirectToPage(new { id, overlay = IsOverlay ? "1" : null });

    private void SetStatus(string message, UiMessageType type)
    {
        TempData["StatusMessage"] = message;
        TempData[UiMessage.TypeKey] = type.ToString();
    }

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

    public sealed class EditQuestionInput
    {
        [Required, StringLength(300)] public string Label { get; set; } = string.Empty;
        [StringLength(1000)] public string? HelpText { get; set; }
        public SignupQuestionType? Type { get; set; }
        public bool? Required { get; set; }
        [StringLength(4000)] public string? Options { get; set; }
        public EventCharacterRole? AccountRole { get; set; }
    }

    public sealed class SignupSettingsInput
    {
        [Display(Name = "Require signup code")] public bool RequireSignupCode { get; set; }
        [StringLength(100), Display(Name = "New signup code")] public string? NewSignupCode { get; set; }
    }
}
