using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using Bingo.Application.Access;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Security;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
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
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class CreateModel(
    ApplicationDbContext db,
    ISecretHasher hasher,
    TimeProvider time,
    IWiseOldManCompetitionClient? competitionClient = null,
    IStringLocalizer<SharedResource>? text = null,
    ILogger<CreateModel>? logger = null) : PageModel
{
    private const int MaximumSlugAllocationAttempts = 10;
    private static readonly Action<ILogger, string, Exception?> LogCreationFailure =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(630101), "Event creation failed. Diagnostic reference {DiagnosticReference}.");
    private static readonly IReadOnlyList<TimezoneOption> DefaultTimezones =
    [
        new("Europe/Copenhagen", "Copenhagen (Europe/Copenhagen)"),
        new("UTC", "UTC")
    ];

    // The obsolete properties remain bind-compatible for one release so a stale
    // wizard post can be rejected explicitly instead of being silently accepted.
    [BindProperty] public CreateInput Input { get; set; } = new();
    public IReadOnlyList<TimezoneOption> Timezones => Options();

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        _ = hasher;
        _ = competitionClient;
        if (await ContainsRetiredWizardInputAsync(ct))
        {
            ModelState.AddModelError(string.Empty, Localize("Event creation now accepts only a name and timezone. Reload the page and try again."));
            return Page();
        }

        Input.Name = Input.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(Input.Name))
            ModelState.AddModelError("Input.Name", Localize("Enter an event name."));
        else if (WiseOldManCompetitionRules.ProviderCharacterCount(Input.Name) > WiseOldManCompetitionRules.MaximumCompetitionTitleLength)
            ModelState.AddModelError("Input.Name", Localize("Event names must be 50 characters or fewer."));

        if (!TryTimezone(Input.Timezone, out _))
            ModelState.AddModelError("Input.Timezone", Localize("Choose a supported timezone."));

        if (!ModelState.IsValid)
            return Page();

        var actorId = User.GetAccountId()!.Value;
        var actorName = User.Identity?.Name ?? "Admin";
        var now = time.GetUtcNow();
        var timezone = Input.Timezone.Trim();

        for (var sequence = 1; sequence <= MaximumSlugAllocationAttempts; sequence++)
        {
            var slug = EventSlugGenerator.GenerateCandidate(Input.Name, sequence);
            var item = new BingoEvent(Guid.NewGuid(), Input.Name, slug, timezone, actorId, now);
            var form = new SignupForm(Guid.NewGuid(), item.Id, now);
            var board = new Board(Guid.NewGuid(), item.Id, "Main board", 5, 5);

            // These records are intentionally all attached to one transaction. A
            // unique slug race must roll the entire new aggregate back before the
            // next suffix gets a fresh event, form, questions, board and audit row.
            item.ConfigureSignup(waitingListEnabled: true, requireCode: false, codeHash: null);
            var questions = DefaultQuestions(form.Id, item.Id);
            var audit = new AuditEntry(
                Guid.NewGuid(),
                now,
                actorId,
                actorName,
                "event.created",
                "event",
                item.Id.ToString(),
                "Created as a private draft.",
                item.Id,
                null,
                JsonSerializer.Serialize(AuditState(item)));

            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                db.Events.Add(item);
                db.SignupForms.Add(form);
                db.SignupQuestions.AddRange(questions);
                db.Boards.Add(board);
                db.AuditEntries.Add(audit);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (DbUpdateException exception) when (IsSlugCollision(exception))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                if (sequence < MaximumSlugAllocationAttempts)
                    continue;

                ModelState.AddModelError(string.Empty, Localize("An event link could not be allocated. Try creating the event again."));
                return Page();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                var reference = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                if (logger is not null) LogCreationFailure(logger, reference, exception);
                ModelState.AddModelError(string.Empty, Localize("The event could not be created. Try again. Diagnostic reference: {0}.", reference));
                return Page();
            }

            TempData["StatusMessage"] = Localize("{0} was created as a private draft.", item.Name);
            TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
            return RedirectToPage("Manage", new { id = item.Id });
        }

        // The loop always returns, but keep a safe form result if the bound is
        // changed later without updating the allocation handling.
        ModelState.AddModelError(string.Empty, Localize("An event link could not be allocated. Try creating the event again."));
        return Page();
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

    private static IReadOnlyList<SignupQuestion> DefaultQuestions(Guid formId, Guid eventId) =>
    [
        new SignupQuestion(Guid.NewGuid(), formId, eventId, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing),
        new SignupQuestion(Guid.NewGuid(), formId, eventId, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, true, 1, null, SignupSystemField.CaptainVolunteer),
        new SignupQuestion(Guid.NewGuid(), formId, eventId, SignupQuestion.CoCaptainKey, SignupQuestion.CoCaptainLabel, SignupQuestionType.Text, false, 2, null, SignupSystemField.CoCaptainName)
    ];

    private static object AuditState(BingoEvent item) => new
    {
        item.Name,
        item.Slug,
        Description = AuditDescription(item.Description),
        item.Timezone,
        item.State
    };

    private static string? AuditDescription(string? description) => description is null
        ? null
        : description.Length <= 500 ? description : $"{description[..500]}…";

    private static bool IsSlugCollision(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_events_slug"
        };

    private string Localize(string key, params object[] arguments) =>
        text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);

    private static bool TryTimezone(string? timezoneId, out TimeZoneInfo timezone)
    {
        timezone = null!;
        return !string.IsNullOrWhiteSpace(timezoneId)
            && DefaultTimezones.Any(option => option.Id == timezoneId.Trim())
            && TryFind(timezoneId.Trim(), out timezone);
    }

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
