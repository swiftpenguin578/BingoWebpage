using Bingo.Application.Evidence;
using Bingo.Domain.Access;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages;

public sealed class EvidenceModel(ApplicationDbContext db, IEvidenceStorage storage) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var asset = await db.EvidenceAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (asset is null) return NotFound(); var submission = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == asset.SubmissionId, ct);
        var allowed = submission.Status == SubmissionStatus.Approved && !submission.PublicEvidenceHidden && asset.Active;
        if (!allowed)
        {
            var accountId = User.GetAccountId(); if (accountId is null) return NotFound(); var account = await db.Accounts.AsNoTracking().SingleAsync(x => x.Id == accountId, ct); allowed = account.Role == AccountRole.Admin || (account.Role == AccountRole.Captain && account.EventId == submission.EventId && account.TeamId == submission.TeamId);
        }
        if (!allowed) return RedirectToPage("/Account/AccessDenied"); var stream = await storage.OpenReadAsync(asset.StorageKey, ct); return new FileStreamResult(stream, asset.MediaType) { EnableRangeProcessing = true };
    }
}
