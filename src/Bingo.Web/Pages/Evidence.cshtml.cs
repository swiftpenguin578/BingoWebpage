using Bingo.Application.Evidence;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages;

public sealed class EvidenceModel(ApplicationDbContext db, IEvidenceStorage storage, IEvidenceAuthority evidenceAuthority, TimeProvider time) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var asset = await db.EvidenceAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (asset is null) return NotFound(); var submission = await db.Submissions.AsNoTracking().SingleAsync(x => x.Id == asset.SubmissionId, ct);
        var allowed = submission.Status == SubmissionStatus.Approved && asset.Active;
        if (!allowed)
        {
            var accountId = User.GetAccountId();
            if (accountId is null) return NotFound();
            var archived = await db.Events.AsNoTracking().AnyAsync(x => x.Id == submission.EventId && x.State == Bingo.Domain.Events.EventState.Archived, ct);
            if (archived)
            {
                allowed = submission.Status is SubmissionStatus.Rejected or SubmissionStatus.Withdrawn &&
                    await db.EventParticipants.AsNoTracking().AnyAsync(participant => participant.Id == submission.CreditedParticipantId &&
                        participant.EventId == submission.EventId && participant.AccountId == accountId.Value, ct);
            }
            else
            {
                allowed = await evidenceAuthority.CanViewPrivateEvidenceAsync(
                    accountId.Value, submission.EventId, submission.TeamId, submission.CreditedParticipantId, time.GetUtcNow(), ct);
            }
        }
        if (!allowed) return RedirectToPage("/Account/AccessDenied"); var stream = await storage.OpenReadAsync(asset.StorageKey, ct); return new FileStreamResult(stream, asset.MediaType) { EnableRangeProcessing = true };
    }
}
