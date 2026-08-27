using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Signups;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Events;

public sealed class SignupModel(ApplicationDbContext dbContext, ISignupService signupService, TimeProvider timeProvider, IStringLocalizer<SharedResource>? text = null, IWiseOldManPlayerLookup? wiseOldMan = null, ISignupLookupTokenService? lookupTokens = null) : PageModel
{
    public EventInfo? EventView { get; private set; }
    public IReadOnlyList<QuestionView> Questions { get; private set; } = [];
    public bool IsEditing { get; private set; }
    public Guid? LookupQuestionId { get; private set; }
    public DateTimeOffset? LookupFetchedAt { get; private set; }
    [BindProperty] public SignupInput Input { get; set; } = new();
    // Direct-model compatibility only; it is deliberately not a Razor Pages handler.
    public async Task<IActionResult> GetForTestAsync(string slug, CancellationToken ct)
    {
        PageContext = new PageContext { HttpContext = new DefaultHttpContext() };
        return await LoadAsync(slug, ct) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnGetAsync(string slug, bool edit = false, CancellationToken ct = default)
    {
        var redirect = await RedirectForPublishedSurfaceAsync(slug, ct);
        if (redirect is not null) return redirect;
        if (User.Identity?.IsAuthenticated != true) return RedirectToPage("/Account/Login", new { ReturnUrl = SignupReturnUrl(slug, edit) });
        if (User.FindFirst("bingo:account_type")?.Value != "WebsiteAccount") return Forbid();
        if (!await LoadAsync(slug, ct)) return NotFound();
        var existing = await dbContext.EventParticipants.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == EventView!.Id && x.AccountId == User.GetAccountId(), ct);
        if (existing is not null && !edit) return RedirectToPage("Confirmation", new { slug, participantId = existing.Id });
        if (existing is not null && !EventView!.Accepting) return RedirectToPage("Confirmation", new { slug, participantId = existing.Id });
        IsEditing = existing is not null;
        await PopulateInputAsync(existing?.Id, ct);
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(string slug, CancellationToken ct)
    {
        var redirect = await RedirectForPublishedSurfaceAsync(slug, ct);
        if (redirect is not null) return redirect;
        if (User.Identity?.IsAuthenticated != true) return RedirectToPage("/Account/Login", new { ReturnUrl = SignupReturnUrl(slug, true) });
        if (User.FindFirst("bingo:account_type")?.Value != "WebsiteAccount") return Forbid();
        if (!await LoadAsync(slug, ct)) return NotFound();
        if (Input.FetchQuestionId is { } fetchQuestionId)
        {
            await FetchEhbAsync(fetchQuestionId, ct);
            return Page();
        }
        if (!ModelState.IsValid) { await PopulateInputAsync(null, ct, preserveSubmitted: true); return Page(); }
        var accountAnswers = Input.AccountAnswers
            .Where(item => item.Value.OsrsCharacterId is { } characterId && characterId != Guid.Empty)
            .ToDictionary(item => item.Key, item => new AuthenticatedAccountAnswer(item.Value.OsrsCharacterId!.Value, item.Value.Ehb, item.Value.WiseOldManLookupToken));
        var result = await signupService.SignUpAuthenticatedAsync(new AuthenticatedSignupRequest(EventView!.Id, User.GetAccountId()!.Value, accountAnswers, Input.Answers, Input.SignupCode, Input.ExpectedResponseVersion), ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, IsResponseConflict(result.Error) ? (text?["Your signup changed while you were editing it. Please reload and try again."].Value ?? "Your signup changed while you were editing it. Please reload and try again.") : Localize(result.Error!));
            if (result.Error == "The event code is incorrect.") ModelState.AddModelError(nameof(Input.SignupCode), Localize(result.Error!));
            await PopulateInputAsync(null, ct, preserveSubmitted: true);
            return Page();
        }
        return RedirectToPage("Confirmation", new { slug, participantId = result.ParticipantId });
    }
    private async Task FetchEhbAsync(Guid questionId, CancellationToken ct)
    {
        var question = Questions.SingleOrDefault(item => item.Id == questionId);
        if (question is null || question.Type != SignupQuestionType.Account || question.AccountRole != EventCharacterRole.Playing)
        {
            ModelState.AddModelError(string.Empty, Localize("Wise Old Man fetching is available only for regular accounts."));
            return;
        }
        var input = Input.AccountAnswers.GetValueOrDefault(questionId);
        if (input?.OsrsCharacterId is not { } characterId || characterId == Guid.Empty)
        {
            ModelState.AddModelError(string.Empty, Localize("Choose a regular account before fetching its EHB."));
            return;
        }
        var account = question.Accounts.SingleOrDefault(item => item.Id == characterId);
        if (account is null)
        {
            ModelState.AddModelError(string.Empty, Localize("Choose a regular account from My accounts before fetching its EHB."));
            return;
        }
        if (wiseOldMan is null || lookupTokens is null)
        {
            ModelState.AddModelError(string.Empty, Localize("Wise Old Man is unavailable right now. Your current EHB was kept."));
            return;
        }
        var normalizedCharacterName = await dbContext.OsrsCharacters.AsNoTracking()
            .Where(character => character.Id == characterId)
            .Select(character => character.NormalizedName)
            .SingleOrDefaultAsync(ct);
        if (normalizedCharacterName is null)
        {
            ModelState.AddModelError(string.Empty, Localize("That character is no longer available. Your current EHB was kept."));
            return;
        }
        var result = await wiseOldMan.LookupPlayerAsync(account.Name, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, LookupFailure(result));
            return;
        }
        var fetchedAt = result.FetchedAt!.Value;
        var issuedAt = timeProvider.GetUtcNow();
        input.Ehb = result.Ehb;
        input.WiseOldManLookupToken = lookupTokens.Create(normalizedCharacterName, result.Ehb!.Value, fetchedAt, issuedAt, issuedAt.AddMinutes(5));
        LookupQuestionId = questionId;
        LookupFetchedAt = fetchedAt;
    }
    private async Task<bool> LoadAsync(string slug, CancellationToken ct)
    {
        var item = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(e => e.Slug == slug, ct); if (item is null || item.State == Bingo.Domain.Events.EventState.Discarded || item.State == Bingo.Domain.Events.EventState.Cancelled && item.FirstPublicAt is null || User.IsInRole("Admin") == false && item.FirstPublicAt is null) return false;
        var cancelled = item.State == Bingo.Domain.Events.EventState.Cancelled;
        var rosterExists = await dbContext.DraftPublicationCycles.AsNoTracking().AnyAsync(x => x.SupersededAt == null && dbContext.DraftSessions.Any(d => d.Id == x.DraftSessionId && d.EventId == item.Id), ct);
        var signupCounts = await dbContext.EventParticipants.AsNoTracking().Where(participant => participant.EventId == item.Id).GroupBy(participant => participant.EventId).Select(group => new
        {
            Confirmed = group.Count(participant => participant.SignupStatus == SignupStatus.Confirmed),
            Waiting = group.Count(participant => participant.SignupStatus == SignupStatus.WaitingList)
        }).SingleOrDefaultAsync(ct);
        EventView = new EventInfo(item.Id, item.Slug, item.Name, item.Description ?? string.Empty, item.SignupClosesAt, item.EventStartsAt, item.EventEndsAt, item.RequireSignupCode, !cancelled && item.AcceptsSignups(timeProvider.GetUtcNow()), cancelled, EventDestinationPolicy.MayUseSignupTable(EventDestinationPolicy.From(item, rosterExists), User.IsInRole("Admin")), item.ParticipantCap, signupCounts?.Confirmed ?? 0, signupCounts?.Waiting ?? 0);
        var accountId = User.GetAccountId();
        var links = accountId is null ? [] : await (from link in dbContext.AccountOsrsCharacters.AsNoTracking() join character in dbContext.OsrsCharacters.AsNoTracking() on link.OsrsCharacterId equals character.Id where link.AccountId == accountId && link.Active orderby link.Preferred descending, link.Position select new AccountOption(character.Id, character.DisplayName, link.SavedEhb, link.Preferred, false)).ToListAsync(ct);
        var questions = cancelled ? [] : await dbContext.SignupQuestions.AsNoTracking().Where(q => q.EventId == item.Id && q.Active).OrderBy(q => q.Position).ToListAsync(ct);
        Questions = questions.Select(q => new QuestionView(q.Id, q.Label, q.Type, q.Required, q.Options == null ? [] : q.Options.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), q.AccountAnswerRole, q.SystemField, links)).ToList(); return true;
    }
    private string LookupFailure(WiseOldManPlayerLookupResult result) => result.Status switch
    {
        WiseOldManLookupStatus.NotFound => text?["Wise Old Man could not find that character."].Value ?? "Wise Old Man could not find that character.",
        WiseOldManLookupStatus.RateLimited when result.RetryAt is { } retryAt => text?["Wise Old Man is temporarily busy. Try again after {0}.", retryAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)].Value ?? $"Wise Old Man is temporarily busy. Try again after {retryAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}.",
        WiseOldManLookupStatus.RateLimited => text?["Wise Old Man is temporarily busy. Try again in about 1 minute."].Value ?? "Wise Old Man is temporarily busy. Try again in about 1 minute.",
        _ when result.RetryAt is { } retryAt => text?["Wise Old Man is unavailable right now. Your current EHB was kept. Try again after {0}.", retryAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)].Value ?? $"Wise Old Man is unavailable right now. Your current EHB was kept. Try again after {retryAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}.",
        _ => text?["Wise Old Man is unavailable right now. Your current EHB was kept."].Value ?? "Wise Old Man is unavailable right now. Your current EHB was kept."
    };
    private async Task<IActionResult?> RedirectForPublishedSurfaceAsync(string slug, CancellationToken ct)
    {
        var item = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug, ct);
        if (item is null || User.IsInRole("Admin") || item.FirstPublicAt is null) return null;
        var rosterExists = await dbContext.DraftPublicationCycles.AsNoTracking().AnyAsync(x => x.SupersededAt == null && dbContext.DraftSessions.Any(d => d.Id == x.DraftSessionId && d.EventId == item.Id), ct);
        return EventDestinationPolicy.Decide(EventDestinationPolicy.From(item, rosterExists), false) switch
        {
            EventDestination.Roster => RedirectToPage("Teams", new { slug }),
            EventDestination.Board => RedirectToPage("Board", new { slug }),
            _ => null
        };
    }
    private async Task PopulateInputAsync(Guid? participantId, CancellationToken ct, bool preserveSubmitted = false)
    {
        if (EventView is null || preserveSubmitted) return;
        if (participantId is null)
        {
            foreach (var question in Questions.Where(x => x.Type == SignupQuestionType.Account && x.AccountRole == EventCharacterRole.Playing))
            {
                var preferred = question.Accounts.FirstOrDefault(x => x.Preferred);
                if (preferred is not null) Input.AccountAnswers[question.Id] = new AccountInput { OsrsCharacterId = preferred.Id, Ehb = preferred.SavedEhb };
            }
            return;
        }
        var assignments = await dbContext.EventParticipantCharacters.AsNoTracking().Where(x => x.EventParticipantId == participantId && x.ReleasedAt == null && x.SignupQuestionId != null).ToListAsync(ct);
        var historicalIds = assignments.Select(x => x.OsrsCharacterId).Except(Questions.SelectMany(x => x.Accounts).Select(x => x.Id)).ToList();
        if (historicalIds.Count > 0)
        {
            var historical = await dbContext.OsrsCharacters.AsNoTracking().Where(x => historicalIds.Contains(x.Id)).ToListAsync(ct);
            Questions = Questions.Select(question => question.Type != SignupQuestionType.Account ? question : question with { Accounts = question.Accounts.Concat(historical.Where(character => assignments.Any(assignment => assignment.SignupQuestionId == question.Id && assignment.OsrsCharacterId == character.Id)).Select(character => new AccountOption(character.Id, character.DisplayName, null, false, true))).ToList() }).ToList();
        }
        foreach (var assignment in assignments)
            Input.AccountAnswers[assignment.SignupQuestionId!.Value] = new AccountInput { OsrsCharacterId = assignment.OsrsCharacterId, Ehb = assignment.EhbSnapshot };
        var answers = await dbContext.SignupAnswers.AsNoTracking().Where(x => x.EventParticipantId == participantId && x.OsrsCharacterId == null).ToListAsync(ct);
        foreach (var answer in answers) Input.Answers[answer.SignupQuestionId] = answer.Value;
        var participant = await dbContext.EventParticipants.AsNoTracking().SingleAsync(x => x.Id == participantId, ct);
        Input.ExpectedResponseVersion = participant.ResponseVersion;
        var captain = Questions.FirstOrDefault(x => x.SystemField == SignupSystemField.CaptainVolunteer);
        if (captain is not null && participant.CaptainVolunteer) Input.Answers[captain.Id] = "true";
    }
    public string MyAccountsUrl => Url.Page("/Account/MyAccounts", new { returnUrl = SignupReturnUrl(EventView!.Slug, IsEditing) })!;
    private string SignupReturnUrl(string slug, bool edit) => Url.Page("/Events/Signup", new { slug, edit = edit ? true : (bool?)null })!;
    private static bool IsResponseConflict(string? error) => error?.Contains("changed while you were editing", StringComparison.OrdinalIgnoreCase) == true;
    private string Localize(string message) => text?[message].Value ?? message;
    public sealed record EventInfo(Guid Id, string Slug, string Name, string Description, DateTimeOffset? SignupClosesAt, DateTimeOffset? EventStartsAt, DateTimeOffset? EventEndsAt, bool RequireCode, bool Accepting, bool Cancelled, bool TableAvailable, int? ParticipantCap, int ConfirmedCount, int WaitingCount);
    public sealed record AccountOption(Guid Id, string Name, decimal? SavedEhb, bool Preferred, bool Historical);
    public sealed record QuestionView(Guid Id, string Label, SignupQuestionType Type, bool Required, string[] OptionList, EventCharacterRole? AccountRole, SignupSystemField SystemField, IReadOnlyList<AccountOption> Accounts);
    public sealed class SignupInput
    {
        [StringLength(100), Display(Name = "Event code")] public string? SignupCode { get; set; }
        public Dictionary<Guid, string> Answers { get; set; } = [];
        public Dictionary<Guid, AccountInput> AccountAnswers { get; set; } = [];
        public int? ExpectedResponseVersion { get; set; }
        public Guid? FetchQuestionId { get; set; }
    }
    public sealed class AccountInput { public Guid? OsrsCharacterId { get; set; } [Range(0, 100000)] public decimal? Ehb { get; set; } public string? WiseOldManLookupToken { get; set; } }
}
