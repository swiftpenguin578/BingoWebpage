using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using Bingo.Application.Access;
using Bingo.Application.Evidence;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Security;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
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
public sealed class CreateModel(ApplicationDbContext db, ISecretHasher hasher, IEvidenceStorage storage, TimeProvider time, IWiseOldManCompetitionClient? competitionClient = null) : PageModel
{
    private static readonly IReadOnlyList<TimezoneOption> DefaultTimezones =
    [
        new("Europe/Copenhagen", "Copenhagen (Europe/Copenhagen)"), new("UTC", "UTC")
    ];

    [BindProperty] public CreateInput Input { get; set; } = new();
    public IReadOnlyList<TimezoneOption> Timezones => OptionsFor(Input.Timezone);

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!TryTimezone(Input.Timezone, out var timezone))
        {
            ModelState.AddModelError("Input.Timezone", "Choose a supported timezone.");
            timezone = TimeZoneInfo.Utc;
        }
        var schedule = ParseSchedule(timezone, "Input");
        ValidateQuestions();
        ValidatePlanning();
        var slug = NormalizeSlug(Input.Slug, Input.Name);
        if (string.IsNullOrWhiteSpace(slug)) ModelState.AddModelError("Input.Slug", "Enter a valid event link.");
        else if (!string.IsNullOrWhiteSpace(Input.Slug) && !string.Equals(Input.Slug.Trim(), slug, StringComparison.Ordinal)) ModelState.AddModelError("Input.Slug", "Use lowercase letters, numbers, and hyphens for the event link.");
        if (!string.IsNullOrWhiteSpace(slug) && await db.Events.AnyAsync(item => item.Slug == slug, ct)) ModelState.AddModelError("Input.Slug", "That event link is already in use.");
        if (Input.RequireSignupCode && string.IsNullOrWhiteSpace(Input.SignupCode)) ModelState.AddModelError("Input.SignupCode", "Enter an event code or turn this setting off.");
        if (!ModelState.IsValid) return Page();

        WiseOldManCompetition? linkedCompetition = null;
        if (Input.CompetitionId is { } competitionId)
        {
            if (competitionClient is null) { ModelState.AddModelError("Input.CompetitionId", "Competition validation is not configured."); return Page(); }
            var result = await competitionClient.GetCompetitionAsync(competitionId, ct);
            if (!result.Succeeded) { ModelState.AddModelError("Input.CompetitionId", result.Message ?? "The competition could not be validated."); return Page(); }
            linkedCompetition = result.Competition!;
            schedule = schedule with { Starts = linkedCompetition.StartsAt, Ends = linkedCompetition.EndsAt };
        }

        var actorId = User.GetAccountId()!.Value;
        var now = time.GetUtcNow();
        var item = new BingoEvent(Guid.NewGuid(), Input.Name.Trim(), slug!, Input.Timezone.Trim(), actorId, now);
        item.UpdateIdentity(Input.Name, slug, Input.Description, Input.Timezone);
        item.ConfigureInitialSchedule(schedule.SignupOpens, schedule.SignupCloses, schedule.DraftAt, schedule.Starts, schedule.Ends, Input.ParticipantCap);
        if (schedule.SignupOpens is { } scheduledOpening && scheduledOpening > now)
            item.ConfigureScheduledSignupOpening(true, []);
        item.ConfigureSignup(Input.WaitingListEnabled, Input.RequireSignupCode, Input.RequireSignupCode ? hasher.Hash(Input.SignupCode!) : null);
        item.ConfigurePlanning(null, Input.BuyInDescription, null, null, null, Input.ExpectedBoardRows, Input.ExpectedBoardColumns);

        StoredEvidence? uploaded = null;
        EventBannerAsset? banner = null;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            db.Events.Add(item);
            if (linkedCompetition is not null)
            {
                db.EventCompetitionSynchronizations.Add(new EventCompetitionSynchronization(Guid.NewGuid(), item.Id, 1, linkedCompetition.Id,
                    linkedCompetition.Title, linkedCompetition.StartsAt, linkedCompetition.EndsAt,
                    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Array.Empty<byte>())).ToLowerInvariant(), now));
            }
            var form = new SignupForm(Guid.NewGuid(), item.Id, now);
            form.ConfigureSignupCode(Input.RequireSignupCode, Input.RequireSignupCode ? hasher.Hash(Input.SignupCode!) : null);
            db.SignupForms.Add(form);
            db.SignupQuestions.Add(new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "primary_regular_account", "Account", SignupQuestionType.Account, true, 0, null, SignupSystemField.PrimaryRegularAccount, EventCharacterRole.Playing));
            db.SignupQuestions.Add(new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, "captain_volunteer", "Captain volunteer", SignupQuestionType.YesNo, false, 1, null, SignupSystemField.CaptainVolunteer));
            if (Input.Banner is { Length: > 0 })
            {
                banner = new EventBannerAsset(Guid.NewGuid(), item.Id, string.Empty, string.Empty, string.Empty, 0, 0, 0, string.Empty, actorId, now);
                await using var content = Input.Banner.OpenReadStream();
                uploaded = await storage.StoreAsync(item.Id, banner.Id, Input.Banner.FileName, content, ct);
                banner = new EventBannerAsset(banner.Id, item.Id, uploaded.StorageKey, uploaded.OriginalFilename, uploaded.MediaType, uploaded.ByteSize, uploaded.Width, uploaded.Height, uploaded.Checksum, actorId, now);
                db.EventBannerAssets.Add(banner);
                item.SetBannerAsset(banner.Id);
            }
            AddQuestions(item, form);
            var after = JsonSerializer.Serialize(AuditState(item));
            db.AuditEntries.Add(new Bingo.Domain.Auditing.AuditEntry(Guid.NewGuid(), now, actorId, User.Identity!.Name!, "event.created", "event", item.Id.ToString(), "Created as a private draft.", item.Id, null, after));
            if (linkedCompetition is not null)
                db.AuditEntries.Add(new Bingo.Domain.Auditing.AuditEntry(Guid.NewGuid(), now, actorId, User.Identity!.Name!, "event.competition_linked", "event", item.Id.ToString(), $"Linked Wise Old Man competition {linkedCompetition.Id} ({linkedCompetition.Title}) during event creation.", item.Id));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (IsSlugCollision(ex))
        {
            await transaction.RollbackAsync(ct);
            await DeleteUploadedAsync(uploaded, ct);
            db.ChangeTracker.Clear();
            ModelState.AddModelError("Input.Slug", "That event link is already in use.");
            return Page();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(ct);
            await DeleteUploadedAsync(uploaded, ct);
            db.ChangeTracker.Clear();
            ModelState.AddModelError(string.Empty, "The event could not be created. Try again.");
            return Page();
        }

        TempData["StatusMessage"] = $"{item.Name} was created as a private draft.";
        TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
        return RedirectToPage("Manage", new { id = item.Id });
    }

    private Schedule ParseSchedule(TimeZoneInfo timezone, string prefix)
    {
        var signupOpens = ParseOptional(Input.SignupOpensLocal, timezone, $"{prefix}.SignupOpensLocal", "Signup opening");
        var signupCloses = ParseOptional(Input.SignupClosesLocal, timezone, $"{prefix}.SignupClosesLocal", "Signup closing");
        var draftAt = ParseOptional(Input.DraftLocal, timezone, $"{prefix}.DraftLocal", "Draft time");
        var starts = ParseOptional(Input.EventStartsLocal, timezone, $"{prefix}.EventStartsLocal", "Event start");
        var ends = ParseOptional(Input.EventEndsLocal, timezone, $"{prefix}.EventEndsLocal", "Event end");
        if (signupOpens is not null && signupCloses is not null && signupCloses <= signupOpens) ModelState.AddModelError($"{prefix}.SignupClosesLocal", "Signup closing must be after opening.");
        if (starts is not null && ends is not null && ends <= starts) ModelState.AddModelError($"{prefix}.EventEndsLocal", "Event end must be after event start.");
        return new(signupOpens, signupCloses, draftAt, starts, ends);
    }

    private DateTimeOffset? ParseOptional(string? localText, TimeZoneInfo timezone, string field, string label)
    {
        if (string.IsNullOrWhiteSpace(localText)) return null;
        if (!DateTime.TryParseExact(localText, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var localValue) || localValue.Minute % 5 != 0)
        {
            ModelState.AddModelError(field, $"{label} must use a valid five-minute time.");
            return null;
        }
        var parsedLocal = DateTime.SpecifyKind(localValue, DateTimeKind.Unspecified);
        if (timezone.IsInvalidTime(parsedLocal)) { ModelState.AddModelError(field, $"{label} falls inside a daylight-saving time change."); return null; }
        if (timezone.IsAmbiguousTime(parsedLocal)) { ModelState.AddModelError(field, $"{label} is ambiguous because of daylight-saving time. Choose another time."); return null; }
        return new DateTimeOffset(parsedLocal, timezone.GetUtcOffset(parsedLocal));
    }

    private void ValidateQuestions()
    {
        for (var index = 0; index < Input.CustomQuestions.Count; index++)
        {
            var question = Input.CustomQuestions[index];
            if (string.IsNullOrWhiteSpace(question.Label)) ModelState.AddModelError($"Input.CustomQuestions[{index}].Label", "Enter the question shown to players.");
            if (question.Type == SignupQuestionType.SingleChoice && Split(question.Options).Length == 0) ModelState.AddModelError($"Input.CustomQuestions[{index}].Options", "Add at least one available answer.");
        }
    }

    private void ValidatePlanning()
    {
        if (Input.ExpectedBoardRows is <= 0 or > 8) ModelState.AddModelError("Input.ExpectedBoardRows", "Board rows must be between 1 and 8.");
        if (Input.ExpectedBoardColumns is <= 0 or > 8) ModelState.AddModelError("Input.ExpectedBoardColumns", "Board columns must be between 1 and 8.");
    }

    private void AddQuestions(BingoEvent item, SignupForm form)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < Input.CustomQuestions.Count; index++)
        {
            var question = Input.CustomQuestions[index];
            var baseKey = EventSlugGenerator.Generate(question.Label).Replace('-', '_');
            var key = baseKey;
            for (var suffix = 2; !keys.Add(key); suffix++) key = $"{baseKey}_{suffix}";
            db.SignupQuestions.Add(new SignupQuestion(Guid.NewGuid(), form.Id, item.Id, key, question.Label.Trim(), question.Type, question.Required, index + 2, question.Type == SignupQuestionType.SingleChoice ? string.Join('\n', Split(question.Options)) : null));
        }
    }

    private async Task DeleteUploadedAsync(StoredEvidence? uploaded, CancellationToken ct)
    {
        if (uploaded is null) return;
        try { await storage.DeleteAsync(uploaded.StorageKey, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { }
    }

    private static string[] Split(string? value) => value?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
    private static object AuditState(BingoEvent item) => new { item.Name, item.Slug, Description = AuditDescription(item.Description), item.Timezone, item.BannerAssetId };
    private static string? AuditDescription(string? description) => description is null ? null : description.Length <= 500 ? description : $"{description[..500]}…";
    private static string? NormalizeSlug(string? value, string name) => EventSlugGenerator.Generate(string.IsNullOrWhiteSpace(value) ? name : value);
    private static bool IsSlugCollision(DbUpdateException ex) => ex.InnerException?.Message.Contains("events_slug", StringComparison.OrdinalIgnoreCase) == true || ex.InnerException?.Message.Contains("slug", StringComparison.OrdinalIgnoreCase) == true;
    private static bool TryTimezone(string? timezoneId, out TimeZoneInfo timezone) { timezone = null!; return !string.IsNullOrWhiteSpace(timezoneId) && OptionsFor(timezoneId).Any(x => x.Id == timezoneId) && TryFind(timezoneId, out timezone); }
    private static bool TryFind(string timezoneId, out TimeZoneInfo timezone) { try { timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId); return true; } catch (TimeZoneNotFoundException) { timezone = null!; return false; } catch (InvalidTimeZoneException) { timezone = null!; return false; } }
    private static List<TimezoneOption> OptionsFor(string? selected)
    {
        return DefaultTimezones.Select(option => new TimezoneOption(option.Id, Label(option.Id, option.Label))).ToList();
    }
    private static string Label(string timezoneId, string place)
    {
        if (!TryFind(timezoneId, out var timezone)) return place;
        var offset = timezone.GetUtcOffset(DateTimeOffset.UtcNow);
        return $"{place} (UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset.Duration():hh\\:mm})";
    }
    private sealed record Schedule(DateTimeOffset? SignupOpens, DateTimeOffset? SignupCloses, DateTimeOffset? DraftAt, DateTimeOffset? Starts, DateTimeOffset? Ends);
    public sealed record TimezoneOption(string Id, string Label);
    public sealed class CreateInput
    {
        [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
        [StringLength(120)] public string? Slug { get; set; }
        [StringLength(4000)] public string? Description { get; set; }
        [Required, StringLength(100)] public string Timezone { get; set; } = "Europe/Copenhagen";
        public IFormFile? Banner { get; set; }
        public string? SignupOpensLocal { get; set; }
        public string? SignupClosesLocal { get; set; }
        public string? DraftLocal { get; set; }
        public string? EventStartsLocal { get; set; }
        public string? EventEndsLocal { get; set; }
        [Range(1, 10000)] public int? ParticipantCap { get; set; }
        public bool WaitingListEnabled { get; set; } = true; public bool RequireSignupCode { get; set; }
        [StringLength(100)] public string? SignupCode { get; set; }
        [StringLength(2000)] public string? BuyInDescription { get; set; }
        [Range(1, long.MaxValue)] public long? CompetitionId { get; set; }
        [Range(1, 8)] public int? ExpectedBoardRows { get; set; }
        [Range(1, 8)] public int? ExpectedBoardColumns { get; set; }
        public List<CustomQuestionInput> CustomQuestions { get; set; } = [];
    }
    public sealed class CustomQuestionInput { [StringLength(300)] public string Label { get; set; } = string.Empty; public SignupQuestionType Type { get; set; } = SignupQuestionType.Text; public bool Required { get; set; } [StringLength(4000)] public string? Options { get; set; } }
}
