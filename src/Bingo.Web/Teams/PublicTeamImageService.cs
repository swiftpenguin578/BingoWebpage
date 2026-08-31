using Bingo.Application.Evidence;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Teams;

/// <summary>Single public boundary for active, event-owned managed team images.</summary>
public sealed class PublicTeamImageService(ApplicationDbContext db, IEvidenceStorage storage)
{
    public async Task<IReadOnlySet<Guid>> CurrentTeamIdsAsync(Guid eventId, IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken)
    {
        if (teamIds.Count == 0) return new HashSet<Guid>();
        if (!await db.Events.AsNoTracking().AnyAsync(item => item.Id == eventId && item.HiddenAt == null, cancellationToken)) return new HashSet<Guid>();
        return await (from team in db.Teams.AsNoTracking()
                      join image in db.TeamImageAssets.AsNoTracking() on team.ActiveImageAssetId equals image.Id
                      where team.EventId == eventId && teamIds.Contains(team.Id) && team.Active &&
                            image.EventId == eventId && image.TeamId == team.Id && image.ReplacedAt == null
                      select team.Id).ToHashSetAsync(cancellationToken);
    }

    public async Task<IResult> OpenAsync(string slug, Guid teamId, CancellationToken cancellationToken)
    {
        var asset = await (from item in db.Events.AsNoTracking()
                           join team in db.Teams.AsNoTracking() on item.Id equals team.EventId
                           join image in db.TeamImageAssets.AsNoTracking() on team.ActiveImageAssetId equals image.Id
                           where item.Slug == slug && item.HiddenAt == null && team.Id == teamId && team.Active &&
                                 image.EventId == item.Id && image.TeamId == team.Id && image.ReplacedAt == null
                           select image).SingleOrDefaultAsync(cancellationToken);
        if (asset is null) return Results.NotFound();
        try { return Results.File(await storage.OpenReadAsync(asset.StorageKey, cancellationToken), asset.MediaType, enableRangeProcessing: true); }
        catch (FileNotFoundException) { return Results.NotFound(); }
    }
}
