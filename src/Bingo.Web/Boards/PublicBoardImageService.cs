using Bingo.Application.Evidence;
using Bingo.Domain.Boards;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Boards;

/// <summary>Public boundary for artwork frozen into the currently published board approval.</summary>
public sealed class PublicBoardImageService(ApplicationDbContext db, IEvidenceStorage storage)
{
    public async Task<IResult> OpenAsync(string slug, Guid tileId, CancellationToken cancellationToken)
    {
        var asset = await (from bingoEvent in db.Events.AsNoTracking()
                           join board in db.Boards.AsNoTracking() on bingoEvent.Id equals board.EventId
                           join approvalTile in db.BoardApprovalTileSnapshots.AsNoTracking() on board.ActiveApprovalSnapshotId equals approvalTile.ApprovalSnapshotId
                           join image in db.BoardTileImageAssets.AsNoTracking() on approvalTile.BoardTileId equals image.BoardTileId
                           where bingoEvent.Slug == slug && bingoEvent.HiddenAt == null && board.State == BoardState.Published &&
                                 approvalTile.BoardTileId == tileId && approvalTile.ArtworkReference != null &&
                                 image.EventId == bingoEvent.Id && image.StorageKey == approvalTile.ArtworkReference
                           select image).SingleOrDefaultAsync(cancellationToken);
        if (asset is null) return Results.NotFound();
        try { return Results.File(await storage.OpenReadAsync(asset.StorageKey, cancellationToken), asset.MediaType, enableRangeProcessing: true); }
        catch (FileNotFoundException) { return Results.NotFound(); }
    }
}
