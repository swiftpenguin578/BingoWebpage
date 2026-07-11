using System.ComponentModel.DataAnnotations;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Events;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class QuestionsModel(ApplicationDbContext dbContext, IAuditWriter auditWriter) : PageModel
{
    public IReadOnlyList<SignupQuestion> Questions { get; private set; } = []; [BindProperty] public QuestionInput Input { get; set; } = new();
    public bool CanEdit { get; private set; }
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) => await LoadAsync(id, ct) ? Page() : NotFound();
    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        if (!await IsDraftAsync(id, ct)) return BadRequest("Signup questions lock once signups have been opened.");
        if (Input.Type == SignupQuestionType.SingleChoice && string.IsNullOrWhiteSpace(Input.Options)) ModelState.AddModelError("Input.Options", "Add at least one choice.");
        if (!ModelState.IsValid) { await LoadAsync(id, ct); return Page(); }
        var position = (await dbContext.SignupQuestions.Where(q => q.EventId == id).MaxAsync(q => (int?)q.Position, ct) ?? 0) + 1;
        var key = await CreateUniqueKeyAsync(id, Input.Label, ct);
        var options = Input.Type == SignupQuestionType.SingleChoice ? Input.Options?.Trim() : null;
        var q = new SignupQuestion(Guid.NewGuid(), id, key, Input.Label, Input.Type, Input.Required, position, options); dbContext.SignupQuestions.Add(q); await dbContext.SaveChangesAsync(ct);
        await auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "signup_question.created", "signup_question", q.Id.ToString(), q.Label, ct); return RedirectToPage(new { id });
    }
    public async Task<IActionResult> OnPostDeactivateAsync(Guid id, Guid questionId, CancellationToken ct) { if (!await IsDraftAsync(id, ct)) return BadRequest("Signup questions lock once signups have been opened."); var q = await dbContext.SignupQuestions.SingleOrDefaultAsync(x => x.Id == questionId && x.EventId == id, ct); if (q is null) return NotFound(); q.Deactivate(); await dbContext.SaveChangesAsync(ct); await auditWriter.WriteAsync(User.GetAccountId(), User.Identity!.Name!, "signup_question.deactivated", "signup_question", q.Id.ToString(), q.Label, ct); return RedirectToPage(new { id }); }
    private async Task<bool> LoadAsync(Guid id, CancellationToken ct) { var state = await dbContext.Events.Where(e => e.Id == id).Select(e => (EventState?)e.State).SingleOrDefaultAsync(ct); if (state is null) return false; CanEdit = state == EventState.Draft; Questions = await dbContext.SignupQuestions.AsNoTracking().Where(q => q.EventId == id && q.Active).OrderBy(q => q.Position).ToListAsync(ct); return true; }
    private Task<bool> IsDraftAsync(Guid id, CancellationToken ct) => dbContext.Events.AnyAsync(e => e.Id == id && e.State == EventState.Draft, ct);
    private async Task<string> CreateUniqueKeyAsync(Guid id, string label, CancellationToken ct) { var baseKey = EventSlugGenerator.Generate(label).Replace('-', '_'); var key = baseKey; for (var suffix = 2; await dbContext.SignupQuestions.AnyAsync(q => q.EventId == id && q.Key == key, ct); suffix++) key = $"{baseKey}_{suffix}"; return key; }
    public static string FormatType(SignupQuestionType type) => type switch { SignupQuestionType.ShortText => "Short answer", SignupQuestionType.LongText => "Long answer", SignupQuestionType.Number => "Number", SignupQuestionType.YesNo => "Yes or no", SignupQuestionType.SingleChoice => "Choose one from a list", _ => type.ToString() };
    public sealed class QuestionInput { [Required, StringLength(300)] public string Label { get; set; } = string.Empty; [Display(Name = "Answer format")] public SignupQuestionType Type { get; set; } = SignupQuestionType.ShortText; public bool Required { get; set; } [StringLength(4000), Display(Name = "Available answers")] public string? Options { get; set; } }
}
