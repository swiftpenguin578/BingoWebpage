using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Security;
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
public sealed class CreateModel(ApplicationDbContext dbContext, ISecretHasher secretHasher, IAuditWriter auditWriter, TimeProvider timeProvider) : PageModel
{
    private static readonly IReadOnlyList<string> HalfHourOptions = Enumerable.Range(0, 48).Select(index => $"{index / 2:00}:{index % 2 * 30:00}").ToList();
    [BindProperty] public CreateInput Input { get; set; } = new();
    public IReadOnlyList<string> HalfHourTimes => HalfHourOptions;
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!TryCreateSchedule(out var signupOpens, out var signupCloses, out var eventStarts, out var eventEnds)) return Page();
        ValidateSchedule(signupOpens, signupCloses, eventStarts, eventEnds);
        if (Input.RequireSignupCode && string.IsNullOrWhiteSpace(Input.SignupCode)) ModelState.AddModelError("Input.SignupCode", "Enter an event code or disable the code requirement.");
        ValidateCustomQuestions();
        if (!ModelState.IsValid) return Page();
        var actorId = User.GetAccountId()!.Value;
        var slug = await CreateUniqueSlugAsync(Input.Name, cancellationToken);
        var item = new BingoEvent(Guid.NewGuid(), Input.Name.Trim(), slug, Input.Description.Trim(), Input.Timezone.Trim(), signupOpens, signupCloses, eventStarts, eventEnds, eventEnds.AddMinutes(30), Input.ParticipantCap, actorId, timeProvider.GetUtcNow());
        item.ConfigureSignup(Input.WaitingListEnabled, Input.AllowPrivateEditing, Input.RequireSignupCode, Input.RequireSignupCode ? secretHasher.Hash(Input.SignupCode!) : null);
        item.ConfigurePlanning(null, null, null, Input.ExpectedTeamCount, Input.ExpectedTeamSize, Input.ExpectedBoardRows, Input.ExpectedBoardColumns);
        dbContext.Events.Add(item);
        var questionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < Input.CustomQuestions.Count; index++)
        {
            var question = Input.CustomQuestions[index];
            var baseKey = EventSlugGenerator.Generate(question.Label).Replace('-', '_');
            var key = baseKey;
            for (var suffix = 2; !questionKeys.Add(key); suffix++) key = $"{baseKey}_{suffix}";
            var options = question.Type == SignupQuestionType.SingleChoice
                ? string.Join('\n', SplitOptions(question.Options))
                : null;
            dbContext.SignupQuestions.Add(new SignupQuestion(Guid.NewGuid(), item.Id, key, question.Label.Trim(), question.Type, question.Required, index + 1, options));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditWriter.WriteAsync(actorId, User.Identity!.Name!, "event.created", "event", item.Id.ToString(), $"Name: {item.Name}", cancellationToken);
        TempData["StatusMessage"] = $"{item.Name} was created as a private event.";
        TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
        return RedirectToPage("Manage", new { id = item.Id });
    }
    private void ValidateSchedule(DateTimeOffset signupOpens, DateTimeOffset signupCloses, DateTimeOffset eventStarts, DateTimeOffset eventEnds)
    {
        if (signupCloses <= signupOpens) ModelState.AddModelError("Input.SignupClosesDate", "Signup closing must be after opening.");
        if (eventEnds <= eventStarts) ModelState.AddModelError("Input.EventEndsDate", "Event end must be after event start.");
    }
    private bool TryCreateSchedule(out DateTimeOffset signupOpens, out DateTimeOffset signupCloses, out DateTimeOffset eventStarts, out DateTimeOffset eventEnds)
    {
        signupOpens = signupCloses = eventStarts = eventEnds = default;
        TimeZoneInfo timezone;
        try { timezone = TimeZoneInfo.FindSystemTimeZoneById(Input.Timezone.Trim()); }
        catch (TimeZoneNotFoundException) { ModelState.AddModelError("Input.Timezone", "The timezone was not recognized."); return false; }
        catch (InvalidTimeZoneException) { ModelState.AddModelError("Input.Timezone", "The timezone configuration is invalid."); return false; }
        return TryCombine(Input.SignupOpensDate, Input.SignupOpensTime, timezone, "Input.SignupOpensTime", "Signup opening", out signupOpens)
            & TryCombine(Input.SignupClosesDate, Input.SignupClosesTime, timezone, "Input.SignupClosesTime", "Signup closing", out signupCloses)
            & TryCombine(Input.EventStartsDate, Input.EventStartsTime, timezone, "Input.EventStartsTime", "Event start", out eventStarts)
            & TryCombine(Input.EventEndsDate, Input.EventEndsTime, timezone, "Input.EventEndsTime", "Event end", out eventEnds);
    }
    private bool TryCombine(DateOnly date, string timeText, TimeZoneInfo timezone, string field, string label, out DateTimeOffset result)
    {
        result = default;
        if (!TimeOnly.TryParseExact(timeText, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) || time.Minute is not (0 or 30))
        { ModelState.AddModelError(field, $"{label} must use a valid half-hour time."); return false; }
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (timezone.IsInvalidTime(local)) { ModelState.AddModelError(field, $"{label} falls inside a daylight-saving time change."); return false; }
        result = new DateTimeOffset(local, timezone.GetUtcOffset(local)); return true;
    }
    private void ValidateCustomQuestions()
    {
        for (var index = 0; index < Input.CustomQuestions.Count; index++)
        {
            var question = Input.CustomQuestions[index];
            if (string.IsNullOrWhiteSpace(question.Label))
                ModelState.AddModelError($"Input.CustomQuestions[{index}].Label", "Enter the question shown to players.");
            if (question.Type == SignupQuestionType.SingleChoice && SplitOptions(question.Options).Length == 0)
                ModelState.AddModelError($"Input.CustomQuestions[{index}].Options", "Add at least one available answer.");
        }
    }
    private static string[] SplitOptions(string? value) => value?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
    private async Task<string> CreateUniqueSlugAsync(string name, CancellationToken cancellationToken)
    {
        var baseSlug = EventSlugGenerator.Generate(name);
        var slug = baseSlug;
        for (var suffix = 2; await dbContext.Events.AnyAsync(item => item.Slug == slug, cancellationToken); suffix++) slug = $"{baseSlug}-{suffix}";
        return slug;
    }
    private static DateTimeOffset NextHalfHour()
    {
        var now = DateTimeOffset.Now;
        var rounded = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute < 30 ? 30 : 0, 0, now.Offset);
        return now.Minute < 30 ? rounded : rounded.AddHours(1);
    }
    public sealed class CreateInput
    {
        public CreateInput()
        {
            var start = NextHalfHour();
            SignupOpensDate = DateOnly.FromDateTime(start.DateTime); SignupOpensTime = start.ToString("HH:mm", CultureInfo.InvariantCulture);
            SignupClosesDate = DateOnly.FromDateTime(start.AddDays(14).DateTime); SignupClosesTime = SignupOpensTime;
            EventStartsDate = DateOnly.FromDateTime(start.AddDays(21).DateTime); EventStartsTime = SignupOpensTime;
            EventEndsDate = DateOnly.FromDateTime(start.AddDays(26).DateTime); EventEndsTime = SignupOpensTime;
        }
        [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
        [Required, StringLength(4000)] public string Description { get; set; } = string.Empty;
        [Required, StringLength(100)] public string Timezone { get; set; } = "Europe/Copenhagen";
        [DataType(DataType.Date), Display(Name = "Signup opening date")] public DateOnly SignupOpensDate { get; set; }
        [Required, Display(Name = "Signup opening time")] public string SignupOpensTime { get; set; }
        [DataType(DataType.Date), Display(Name = "Signup closing date")] public DateOnly SignupClosesDate { get; set; }
        [Required, Display(Name = "Signup closing time")] public string SignupClosesTime { get; set; }
        [DataType(DataType.Date), Display(Name = "Event start date")] public DateOnly EventStartsDate { get; set; }
        [Required, Display(Name = "Event start time")] public string EventStartsTime { get; set; }
        [DataType(DataType.Date), Display(Name = "Event end date")] public DateOnly EventEndsDate { get; set; }
        [Required, Display(Name = "Event end time")] public string EventEndsTime { get; set; }
        [Range(1, 10000), Display(Name = "Participant cap")] public int ParticipantCap { get; set; } = 50;
        public bool WaitingListEnabled { get; set; } = true; public bool AllowPrivateEditing { get; set; } = true; public bool RequireSignupCode { get; set; }
        [StringLength(100), Display(Name = "Event code")] public string? SignupCode { get; set; }
        [Range(1, 100), Display(Name = "Expected teams")] public int? ExpectedTeamCount { get; set; }
        [Range(1, 100), Display(Name = "Expected team size")] public int? ExpectedTeamSize { get; set; }
        [Range(1, 20), Display(Name = "Expected board rows")] public int? ExpectedBoardRows { get; set; } = 5;
        [Range(1, 20), Display(Name = "Expected board columns")] public int? ExpectedBoardColumns { get; set; } = 5;
        public List<CustomQuestionInput> CustomQuestions { get; set; } = [];
    }
    public sealed class CustomQuestionInput
    {
        [StringLength(300)] public string Label { get; set; } = string.Empty;
        public SignupQuestionType Type { get; set; } = SignupQuestionType.ShortText;
        public bool Required { get; set; }
        [StringLength(4000)] public string? Options { get; set; }
    }
}
