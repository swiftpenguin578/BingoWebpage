using System.ComponentModel.DataAnnotations;
using Bingo.Application.Security;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Signups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Events;

public sealed class EditSignupModel(
    ApplicationDbContext dbContext,
    EventParticipantCharacterService characterService,
    IPrivateEditTokenService tokenService,
    IStringLocalizer<SharedResource> text) : PageModel
{
    [BindProperty] public EditInput Input { get; set; } = new();
    public IReadOnlyList<QuestionView> Questions { get; private set; } = [];
    public async Task<IActionResult> OnGetAsync(string slug, string token, CancellationToken ct)
    {
        var participant = await FindAsync(slug, token, ct); if (participant is null) return NotFound();
        await LoadQuestionsAsync(participant.EventId, ct);
        var answers = await dbContext.SignupAnswers.AsNoTracking().Where(a => a.EventParticipantId == participant.Id).ToDictionaryAsync(a => a.SignupQuestionId, a => a.Value, ct);
        var authority = await LoadAuthorityAsync(participant.Id, ct);
        Input = new EditInput { PrimaryAccountName = authority.Primary.Name, Ehb = authority.Primary.Ehb, SecondAccountName = authority.SecondName, DiscordIdentity = participant.DiscordIdentity, Comments = participant.Comments, CaptainVolunteer = participant.CaptainVolunteer, CustomAnswers = answers }; return Page();
    }
    public async Task<IActionResult> OnPostAsync(string slug, string token, CancellationToken ct)
    {
        var participant = await FindAsync(slug, token, ct); if (participant is null) return NotFound(); await LoadQuestionsAsync(participant.EventId, ct);
        foreach (var question in Questions.Where(q => q.Required)) if (!Input.CustomAnswers.TryGetValue(question.Id, out var answer) || string.IsNullOrWhiteSpace(answer)) ModelState.AddModelError(string.Empty, text["'{0}' is required.", question.Label]);
        if (!ModelState.IsValid) return Page();
        var normalized = SignupService.NormalizeAccountName(Input.PrimaryAccountName);
        var duplicate = await dbContext.PrimaryCharacters().AnyAsync(p => p.EventId == participant.EventId && p.ParticipantId != participant.Id && p.NormalizedName == normalized, ct);
        if (duplicate) { ModelState.AddModelError("Input.PrimaryAccountName", text["That account is already signed up."]); return Page(); }
        participant.UpdateSignupDetails(Clean(Input.DiscordIdentity), Clean(Input.Comments), Input.CaptainVolunteer);
        try
        {
            await characterService.ApplyFixedSignupAssignmentsAsync(
                participant, Input.PrimaryAccountName, Input.Ehb, Input.SecondAccountName,
                EhbSource.Manual, null, ct);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("Input.PrimaryAccountName", text[exception.Message]);
            return Page();
        }
        var existing = await dbContext.SignupAnswers.Where(a => a.EventParticipantId == participant.Id).ToDictionaryAsync(a => a.SignupQuestionId, ct);
        foreach (var question in Questions) { if (!Input.CustomAnswers.TryGetValue(question.Id, out var value) || string.IsNullOrWhiteSpace(value)) continue; if (existing.TryGetValue(question.Id, out var stored)) stored.Update(value.Trim()); else dbContext.SignupAnswers.Add(new SignupAnswer(Guid.NewGuid(), participant.Id, question.Id, question.Label, value.Trim())); }
        await dbContext.SaveChangesAsync(ct); TempData["StatusMessage"] = text["Your signup was updated."]; return RedirectToPage(new { slug, token });
    }
    private async Task<EventParticipant?> FindAsync(string slug, string token, CancellationToken ct)
    {
        var hash = tokenService.Hash(token); return await (from p in dbContext.EventParticipants join e in dbContext.Events on p.EventId equals e.Id where e.Slug == slug && e.AllowPrivateSignupEditing && !e.DraftLocked && p.PrivateEditTokenHash == hash && (p.SignupStatus == SignupStatus.Confirmed || p.SignupStatus == SignupStatus.WaitingList) select p).SingleOrDefaultAsync(ct);
    }
    private async Task LoadQuestionsAsync(Guid eventId, CancellationToken ct) => Questions = await dbContext.SignupQuestions.AsNoTracking().Where(q => q.EventId == eventId && q.Active).OrderBy(q => q.Position).Select(q => new QuestionView(q.Id, q.Label, q.Type, q.Required, q.Options == null ? Array.Empty<string>() : q.Options.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))).ToListAsync(ct);
    private async Task<(EventParticipantAuthority Primary, string? SecondName)> LoadAuthorityAsync(Guid participantId, CancellationToken ct)
    {
        var primary = await dbContext.PrimaryCharacters().AsNoTracking().SingleAsync(x => x.ParticipantId == participantId, ct);
        var secondName = await (from assignment in dbContext.EventParticipantCharacters.AsNoTracking()
                                join character in dbContext.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
                                where assignment.EventParticipantId == participantId && assignment.ReleasedAt == null &&
                                      assignment.EventRole == EventCharacterRole.Informational
                                orderby assignment.RegistrationOrder
                                select character.DisplayName).FirstOrDefaultAsync(ct);
        return (primary, secondName);
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public sealed class EditInput
    {
        [Required, StringLength(100), Display(Name = "In-game name")] public string PrimaryAccountName { get; set; } = string.Empty;
        [Range(0, 100000), Display(Name = "EHB (Wise Old Man)")] public decimal Ehb { get; set; }
        [StringLength(100), Display(Name = "Second account")] public string? SecondAccountName { get; set; }
        [StringLength(100), Display(Name = "Discord name")] public string? DiscordIdentity { get; set; }
        [StringLength(4000)] public string? Comments { get; set; }
        public bool CaptainVolunteer { get; set; }
        public Dictionary<Guid, string> CustomAnswers { get; set; } = [];
    }
    public sealed record QuestionView(Guid Id, string Label, SignupQuestionType Type, bool Required, string[] OptionList);
}
