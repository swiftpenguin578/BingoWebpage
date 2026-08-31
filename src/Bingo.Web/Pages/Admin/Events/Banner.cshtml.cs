using Bingo.Application.Access;
using Bingo.Application.Evidence;
using Bingo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class BannerModel(ApplicationDbContext db, IEvidenceStorage storage) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var asset = await db.Events.AsNoTracking()
            .Where(item => item.Id == id && item.HiddenAt == null && item.BannerAssetId != null)
            .Join(db.EventBannerAssets.AsNoTracking(), item => item.BannerAssetId, banner => banner.Id, (_, banner) => banner)
            .SingleOrDefaultAsync(ct);
        if (asset is null || asset.ReplacedAt is not null) return NotFound();

        try
        {
            var stream = await storage.OpenReadAsync(asset.StorageKey, ct);
            return new FileStreamResult(stream, asset.MediaType) { EnableRangeProcessing = true };
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }
}
