using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Captain;

[Authorize]
public sealed class SubmissionModel : PageModel
{
    public IActionResult OnGet(Guid id, Guid? eventId, Guid? teamId)
        => RedirectToPage("/Submissions/Submission", new { id, eventId, teamId });

    public IActionResult OnGetCorrect(Guid id, Guid? eventId, Guid? teamId)
        => RedirectToPage("/Submissions/Submission", new { id, eventId, teamId });

    public IActionResult OnGetResubmit(Guid id, Guid? eventId, Guid? teamId)
        => RedirectToPage("/Submissions/Submission", new { id, eventId, teamId });

    public IActionResult OnGetWithdraw(Guid id, Guid? eventId, Guid? teamId)
        => RedirectToPage("/Submissions/Submission", new { id, eventId, teamId });
}
