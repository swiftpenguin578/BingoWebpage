using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Access;
using Bingo.Application.Events;
using Bingo.Domain.Signups;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CreateModel(
    IEventCreationService creation,
    IStringLocalizer<SharedResource>? text = null,
    ILogger<CreateModel>? logger = null) : PageModel
{
    private static readonly Action<ILogger, string, Exception?> LogCreationFailure =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(630101), "Event creation failed. Diagnostic reference {DiagnosticReference}.");
    private static readonly IReadOnlyList<TimezoneOption> DefaultTimezones =
    [
        new("Europe/Copenhagen", "Copenhagen (Europe/Copenhagen)"),
        new("UTC", "UTC")
    ];

    // Retained only to explicitly reject stale wizard input.
    [BindProperty] public CreateInput Input { get; set; } = new();
    public IReadOnlyList<TimezoneOption> Timezones => Options();

    public void OnGet() => Input.RequestId = Guid.NewGuid();

    public async Task<IActionResult> OnGetCheckAgainAsync(Guid requestId, CancellationToken ct)
    {
        if (!ModelState.IsValid || requestId == Guid.Empty) return BadRequest();
        var result = await creation.CheckAgainAsync(requestId, new(User.GetAccountId()!.Value, User.Identity?.Name ?? "Admin"), ct);
        return result.Outcome switch
        {
            EventCreationOutcome.Completed => new JsonResult(new { eventId = result.EventId }),
            EventCreationOutcome.Forbidden => Forbid(),
            _ => NotFound()
        };
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (await ContainsRetiredWizardInputAsync(ct))
        {
            ModelState.AddModelError(string.Empty, Localize("Event creation now accepts only a name and timezone. Reload the page and try again."));
            return Page();
        }
        if (Input.RequestId == Guid.Empty)
            ModelState.AddModelError(string.Empty, Localize("Reload the page before creating an event."));
        if (!ModelState.IsValid) return Page();

        try
        {
            var result = await creation.CreateAsync(Input.RequestId, Input.Name, Input.Timezone,
                new(User.GetAccountId()!.Value, User.Identity?.Name ?? "Admin"), ct);
            if (result.Outcome == EventCreationOutcome.Forbidden) return Forbid();
            if (result.Outcome == EventCreationOutcome.NotFound) return NotFound();
            if (result.Outcome != EventCreationOutcome.Completed)
            {
                if (result.Outcome == EventCreationOutcome.Conflict) Response.StatusCode = StatusCodes.Status409Conflict;
                ModelState.AddModelError(result.Field is null ? string.Empty : $"Input.{result.Field}", Localize(result.Error!));
                return Page();
            }
            TempData["StatusMessage"] = Localize("{0} was created as a private draft.", result.Name!);
            TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
            return RedirectToPage("Manage", new { id = result.EventId });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var reference = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            if (logger is not null) LogCreationFailure(logger, reference, exception);
            ModelState.AddModelError(string.Empty, Localize("The event creation outcome could not be confirmed. Retry this request with the same values. Diagnostic reference: {0}.", reference));
            return Page();
        }
    }

    private async Task<bool> ContainsRetiredWizardInputAsync(CancellationToken ct)
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            if (form.Keys.Any(IsRetiredWizardKey))
                return true;
        }

        return Input.Slug is not null
            || Input.Description is not null
            || Input.SignupOpensLocal is not null
            || Input.SignupClosesLocal is not null
            || Input.DraftLocal is not null
            || Input.EventStartsLocal is not null
            || Input.EventEndsLocal is not null
            || Input.ParticipantCap is not null
            || !Input.WaitingListEnabled
            || Input.RequireSignupCode
            || Input.SignupCode is not null
            || Input.BuyInDescription is not null
            || Input.CompetitionId is not null
            || Input.ExpectedBoardRows is not null
            || Input.ExpectedBoardColumns is not null
            || Input.CustomQuestions.Count > 0;
    }

    private static bool IsRetiredWizardKey(string key)
    {
        if (key is "__RequestVerificationToken" or "Input.Name" or "Input.Timezone")
            return false;

        return key.StartsWith("Input.Slug", StringComparison.Ordinal)
            || key.StartsWith("Input.Description", StringComparison.Ordinal)
            || key.StartsWith("Input.Banner", StringComparison.Ordinal)
            || key.StartsWith("Input.SignupOpensLocal", StringComparison.Ordinal)
            || key.StartsWith("Input.SignupClosesLocal", StringComparison.Ordinal)
            || key.StartsWith("Input.DraftLocal", StringComparison.Ordinal)
            || key.StartsWith("Input.EventStartsLocal", StringComparison.Ordinal)
            || key.StartsWith("Input.EventEndsLocal", StringComparison.Ordinal)
            || key.StartsWith("Input.ParticipantCap", StringComparison.Ordinal)
            || key.StartsWith("Input.WaitingListEnabled", StringComparison.Ordinal)
            || key.StartsWith("Input.RequireSignupCode", StringComparison.Ordinal)
            || key.StartsWith("Input.SignupCode", StringComparison.Ordinal)
            || key.StartsWith("Input.BuyInDescription", StringComparison.Ordinal)
            || key.StartsWith("Input.CompetitionId", StringComparison.Ordinal)
            || key.StartsWith("Input.ExpectedBoardRows", StringComparison.Ordinal)
            || key.StartsWith("Input.ExpectedBoardColumns", StringComparison.Ordinal)
            || key.StartsWith("Input.CustomQuestions", StringComparison.Ordinal);
    }

    private string Localize(string key, params object[] arguments) =>
        text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);

    private static bool TryFind(string timezoneId, out TimeZoneInfo timezone)
    {
        try
        {
            timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            timezone = null!;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            timezone = null!;
            return false;
        }
    }

    private static List<TimezoneOption> Options() =>
        DefaultTimezones.Select(option => new TimezoneOption(option.Id, Label(option.Id, option.Label))).ToList();

    private static string Label(string timezoneId, string place)
    {
        if (!TryFind(timezoneId, out var timezone))
            return place;
        var offset = timezone.GetUtcOffset(DateTimeOffset.UtcNow);
        return $"{place} (UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset.Duration():hh\\:mm})";
    }

    public sealed record TimezoneOption(string Id, string Label);

    public sealed class CreateInput
    {
        public Guid RequestId { get; set; }
        [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
        [StringLength(120)] public string? Slug { get; set; }
        [StringLength(4000)] public string? Description { get; set; }
        [Required, StringLength(100)] public string Timezone { get; set; } = "Europe/Copenhagen";
        public string? SignupOpensLocal { get; set; }
        public string? SignupClosesLocal { get; set; }
        public string? DraftLocal { get; set; }
        public string? EventStartsLocal { get; set; }
        public string? EventEndsLocal { get; set; }
        [Range(1, 10000)] public int? ParticipantCap { get; set; }
        public bool WaitingListEnabled { get; set; } = true;
        public bool RequireSignupCode { get; set; }
        [StringLength(100)] public string? SignupCode { get; set; }
        [StringLength(2000)] public string? BuyInDescription { get; set; }
        [Range(1, long.MaxValue)] public long? CompetitionId { get; set; }
        [Range(1, 8)] public int? ExpectedBoardRows { get; set; }
        [Range(1, 8)] public int? ExpectedBoardColumns { get; set; }
        public List<CustomQuestionInput> CustomQuestions { get; set; } = [];
    }

    public sealed class CustomQuestionInput
    {
        [StringLength(300)] public string Label { get; set; } = string.Empty;
        public SignupQuestionType Type { get; set; } = SignupQuestionType.Text;
        public bool Required { get; set; }
        [StringLength(4000)] public string? Options { get; set; }
    }
}
