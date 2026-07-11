using System.ComponentModel.DataAnnotations;
using Bingo.Application.Signups;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Events;

public sealed class SignupModel(ApplicationDbContext dbContext, ISignupService signupService, TimeProvider timeProvider) : PageModel
{
    public EventInfo? EventView { get; private set; }
    public IReadOnlyList<QuestionView> Questions { get; private set; } = [];
    [BindProperty] public SignupInput Input { get; set; } = new();
    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct) => await LoadAsync(slug, ct) ? Page() : NotFound();
    public async Task<IActionResult> OnPostAsync(string slug, CancellationToken ct)
    {
        if (!await LoadAsync(slug, ct)) return NotFound();
        if (!ModelState.IsValid) return Page();
        var result = await signupService.SignUpAsync(new SignupRequest(EventView!.Id, Input.PrimaryAccountName, Input.Ehb, Input.SecondAccountName, Input.DiscordIdentity, Input.Comments, Input.CaptainVolunteer, Input.SignupCode, Input.CustomAnswers), ct);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return Page(); }
        TempData["SignupEventName"] = EventView.Name; TempData["SignupStatus"] = result.Status!.Value.ToString(); TempData["WaitingPosition"] = result.WaitingListPosition; TempData["EditToken"] = result.PrivateEditToken;
        return RedirectToPage("Confirmation", new { slug });
    }
    private async Task<bool> LoadAsync(string slug, CancellationToken ct)
    {
        var item = await dbContext.Events.AsNoTracking().SingleOrDefaultAsync(e => e.Slug == slug, ct); if (item is null) return false;
        EventView = new EventInfo(item.Id, item.Name, item.Description, item.SignupClosesAt, item.EventStartsAt, item.EventEndsAt, item.RequireSignupCode, item.AcceptsSignups(timeProvider.GetUtcNow()));
        Questions = await dbContext.SignupQuestions.AsNoTracking().Where(q => q.EventId == item.Id && q.Active).OrderBy(q => q.Position).Select(q => new QuestionView(q.Id, q.Label, q.Type, q.Required, q.Options == null ? Array.Empty<string>() : q.Options.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))).ToListAsync(ct); return true;
    }
    public sealed record EventInfo(Guid Id, string Name, string Description, DateTimeOffset SignupClosesAt, DateTimeOffset EventStartsAt, DateTimeOffset EventEndsAt, bool RequireCode, bool Accepting);
    public sealed record QuestionView(Guid Id, string Label, SignupQuestionType Type, bool Required, string[] OptionList);
    public sealed class SignupInput
    {
        [Required, StringLength(100), Display(Name = "In-game name")] public string PrimaryAccountName { get; set; } = string.Empty;
        [Range(0, 100000), Display(Name = "EHB (Wise Old Man)")] public decimal Ehb { get; set; }
        [StringLength(100), Display(Name = "Second account (optional)")] public string? SecondAccountName { get; set; }
        [StringLength(100), Display(Name = "Discord name (optional)")] public string? DiscordIdentity { get; set; }
        [StringLength(4000)] public string? Comments { get; set; }
        public bool CaptainVolunteer { get; set; }
        [StringLength(100), Display(Name = "Event code")] public string? SignupCode { get; set; }
        public Dictionary<Guid, string> CustomAnswers { get; set; } = [];
    }
}
