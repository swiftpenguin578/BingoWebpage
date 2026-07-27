using System.ComponentModel.DataAnnotations;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
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

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class QuestionsModel(ApplicationDbContext dbContext, IAuditWriter auditWriter) : PageModel
{
    public IReadOnlyList<SignupQuestion> Questions { get; private set; } = [];

    [BindProperty]
    public QuestionInput Input { get; set; } = new();
    [BindProperty]
    public SignupSettingsInput Settings { get; set; } = new();

    public bool CanEdit { get; private set; }
    public bool CanEditSettings { get; private set; }
    public string EventName { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) =>
        await LoadAsync(id, ct) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        if (!await CanEditAsync(id, ct))
        {
            SetLockedStatus();
            return RedirectToPage(new { id });
        }

        if (Input.Type == SignupQuestionType.SingleChoice && string.IsNullOrWhiteSpace(Input.Options))
        {
            ModelState.AddModelError("Input.Options", "Add at least one choice.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(id, ct);
            return Page();
        }

        var position = (await dbContext.SignupQuestions
            .Where(question => question.EventId == id)
            .MaxAsync(question => (int?)question.Position, ct) ?? 0) + 1;
        var key = await CreateUniqueKeyAsync(id, Input.Label, ct);
        var options = Input.Type == SignupQuestionType.SingleChoice ? Input.Options?.Trim() : null;
        var question = new SignupQuestion(
            Guid.NewGuid(),
            id,
            key,
            Input.Label,
            Input.Type,
            Input.Required,
            position,
            options);

        dbContext.SignupQuestions.Add(question);
        await dbContext.SaveChangesAsync(ct);
        await auditWriter.WriteAsync(
            User.GetAccountId(),
            User.Identity!.Name!,
            "signup_question.created",
            "signup_question",
            question.Id.ToString(),
            question.Label,
            ct);

        SetStatus("Question added.", UiMessageType.Success);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeactivateAsync(Guid id, Guid questionId, CancellationToken ct)
    {
        if (!await CanEditAsync(id, ct))
        {
            SetLockedStatus();
            return RedirectToPage(new { id });
        }

        var question = await dbContext.SignupQuestions
            .SingleOrDefaultAsync(item => item.Id == questionId && item.EventId == id, ct);
        if (question is null) return NotFound();

        question.Deactivate();
        await dbContext.SaveChangesAsync(ct);
        await auditWriter.WriteAsync(
            User.GetAccountId(),
            User.Identity!.Name!,
            "signup_question.deactivated",
            "signup_question",
            question.Id.ToString(),
            question.Label,
            ct);

        SetStatus("Question removed.", UiMessageType.Success);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSettingsAsync(Guid id, CancellationToken ct)
    {
        var bingoEvent = await dbContext.Events.SingleOrDefaultAsync(item => item.Id == id, ct);
        if (bingoEvent is null) return NotFound();
        if (bingoEvent.DraftLocked || bingoEvent.State is not (EventState.Draft or EventState.SignupOpen or EventState.SignupClosed))
        {
            SetStatus("Signup settings are locked because the draft has started or this event has moved on.", UiMessageType.Error);
            return RedirectToPage(new { id });
        }
        if (!Settings.WaitingListEnabled && await dbContext.EventParticipants.AnyAsync(item => item.EventId == id && item.SignupStatus == SignupStatus.WaitingList, ct))
        {
            SetStatus("The waiting list cannot be disabled while participants are waiting.", UiMessageType.Error);
            return RedirectToPage(new { id });
        }
        try
        {
            bingoEvent.ConfigureSignup(Settings.WaitingListEnabled, bingoEvent.AllowPrivateSignupEditing, bingoEvent.RequireSignupCode, bingoEvent.SignupCodeHash);
            await dbContext.SaveChangesAsync(ct);
            await auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "event.waiting_list_changed", "event", id.ToString(), Settings.WaitingListEnabled ? "Waiting list enabled." : "Waiting list disabled.", ct);
            SetStatus(Settings.WaitingListEnabled ? "Waiting list enabled." : "Waiting list disabled.", UiMessageType.Success);
        }
        catch (InvalidOperationException)
        {
            SetStatus("The signup settings could not be changed in this event state.", UiMessageType.Error);
        }
        return RedirectToPage(new { id });
    }

    private async Task<bool> LoadAsync(Guid id, CancellationToken ct)
    {
        var bingoEvent = await dbContext.Events
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Name, item.State, item.DraftLocked, item.WaitingListEnabled })
            .SingleOrDefaultAsync(ct);
        if (bingoEvent is null) return false;

        EventName = bingoEvent.Name;
        CanEdit = CanEditSignupQuestions(bingoEvent.State, bingoEvent.DraftLocked);
        CanEditSettings = !bingoEvent.DraftLocked && bingoEvent.State is (EventState.Draft or EventState.SignupOpen or EventState.SignupClosed);
        Settings = new SignupSettingsInput { WaitingListEnabled = bingoEvent.WaitingListEnabled };
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

    private void SetLockedStatus() =>
        SetStatus(
            "Signup questions can only be changed while signups are closed and before the draft starts.",
            UiMessageType.Error);

    private void SetStatus(string message, UiMessageType type)
    {
        TempData["StatusMessage"] = message;
        TempData[UiMessage.TypeKey] = type.ToString();
    }

    public static string FormatType(SignupQuestionType type) => type switch
    {
        SignupQuestionType.ShortText => "Short answer",
        SignupQuestionType.LongText => "Long answer",
        SignupQuestionType.Number => "Number",
        SignupQuestionType.YesNo => "Yes or no",
        SignupQuestionType.SingleChoice => "Choose one from a list",
        _ => type.ToString()
    };

    public sealed class QuestionInput
    {
        [Required, StringLength(300)]
        public string Label { get; set; } = string.Empty;

        [Display(Name = "Answer format")]
        public SignupQuestionType Type { get; set; } = SignupQuestionType.ShortText;

        public bool Required { get; set; }

        [StringLength(4000), Display(Name = "Available answers")]
        public string? Options { get; set; }
    }

    public sealed class SignupSettingsInput
    {
        [Display(Name = "Enable waiting list")]
        public bool WaitingListEnabled { get; set; }
    }
}
