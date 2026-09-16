using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bingo.Application.Boards;
using Bingo.Application.Stats;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Catalogue;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Web.Pages.Events;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StatsModel(IPublicStatsService stats, IPublicBoardService boards, ApplicationDbContext db, TimeProvider time,
    OsrsWikiImageCache? wikiImages = null) : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
    public PublicEventStats Stats { get; private set; } = null!;
    public string? CancelledEventName { get; private set; }
    public bool CanSaveGuidance { get; private set; }
    public bool CanEditArtwork { get; private set; }
    public bool GuidanceHidden { get; private set; }
    public string BootstrapJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        var result = await stats.GetAsync(slug, cancellationToken);
        if (result is null)
        {
            // Cancelled presentation is shared; never query/fall back to a preferred event.
            if (slug == "det-store-danske-sommerbingo-2026") return NotFound();
            var visibleCancelled = await db.Events.AsNoTracking().AnyAsync(x => x.Slug == slug && x.HiddenAt == null &&
                x.FirstPublicAt != null && x.BoardPublished && x.State == EventState.Cancelled, cancellationToken);
            if (!visibleCancelled) return NotFound();
            var board = await boards.GetEventBoardAsync(slug, 0, cancellationToken);
            if (board is null) return NotFound();
            CancelledEventName = board.EventName;
            return Page();
        }
        Stats = result;
        BootstrapJson = JsonSerializer.Serialize(await PresentationAsync(result, cancellationToken), JsonOptions);
        return Page();
    }

    public async Task<IActionResult> OnGetDataAsync(string slug, CancellationToken cancellationToken)
    {
        var result = await stats.GetAsync(slug, cancellationToken);
        return result is null ? NotFound() : new JsonResult(await PresentationAsync(result, cancellationToken), JsonOptions);
    }

    private async Task<object> PresentationAsync(PublicEventStats result, CancellationToken ct)
    {
        result = MapWikiImages(result);
        var actor = User.Identity?.IsAuthenticated == true && User.GetAccountId() is Guid actorId
            ? await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId && x.Active, ct) : null;
        CanSaveGuidance = actor is not null;
        CanEditArtwork = actor?.GlobalRole == GlobalRole.SuperAdmin;
        GuidanceHidden = actor?.StatsGuidanceHidden == true;
        var itemIds = result.Drops.Select(x => x.Item.ItemId).Concat(result.Luck.Sources.Select(x => x.ItemId)).Distinct().ToArray();
        var items = await db.CatalogueItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToArrayAsync(ct);
        var tiles = result.Tiles ?? [];
        var teamIds = result.Teams.Select(x => x.TeamId).ToArray();
        var currentMembership = actor is null ? null : await (from participant in db.EventParticipants.AsNoTracking()
                                                              join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
                                                              where participant.EventId == result.EventId && participant.AccountId == actor.Id &&
                                                                  participant.SignupStatus == SignupStatus.Confirmed && membership.LeftAt == null && teamIds.Contains(membership.TeamId)
                                                              select new { playerId = participant.Id, teamId = membership.TeamId }).SingleOrDefaultAsync(ct);
        return new
        {
            stats = result,
            generatedAt = time.GetUtcNow(),
            tiles,
            currentMembership,
            preference = new { hidden = GuidanceHidden, version = actor?.Version, canSave = CanSaveGuidance },
            canEditArtwork = CanEditArtwork,
            artwork = items.Select(ArtworkState).ToArray()
        };
    }

    private PublicEventStats MapWikiImages(PublicEventStats result)
    {
        if (wikiImages is null) return result;

        StatsItemDrop MapDrop(StatsItemDrop drop) => drop with { Item = MapItem(drop.Item) };
        StatsRepeatedItem? MapRepeated(StatsRepeatedItem? repeated) => repeated is null ? null : repeated with { Item = MapItem(repeated.Item) };
        StatsTeam MapTeam(StatsTeam team) => team with
        {
            MostValuableDrop = team.MostValuableDrop is { } valuable ? MapDrop(valuable) : null,
            Players = team.Players.Select(player => player with
            {
                MostValuableDrop = player.MostValuableDrop is { } playerValuable ? MapDrop(playerValuable) : null,
                RepeatedItem = MapRepeated(player.RepeatedItem)
            }).ToArray(),
            Milestones = team.Milestones.Select(milestone => milestone with
            {
                Drop = milestone.Drop is { } milestoneDrop ? MapDrop(milestoneDrop) : null
            }).ToArray(),
            RepeatedItem = MapRepeated(team.RepeatedItem)
        };

        var drops = result.Drops.Select(MapDrop).ToArray();
        var luck = result.Luck with
        {
            Sources = result.Luck.Sources.Select(source => source with { Item = MapItem(source.Item) }).ToArray()
        };
        return result with
        {
            Drops = drops,
            Teams = result.Teams.Select(MapTeam).ToArray(),
            Luck = luck,
            Milestones = result.Milestones.Select(milestone => milestone with
            {
                Drop = milestone.Drop is { } milestoneDrop ? MapDrop(milestoneDrop) : null
            }).ToArray(),
            RepeatedItem = MapRepeated(result.RepeatedItem),
            MostValuableDrop = result.MostValuableDrop is { } mostValuable ? MapDrop(mostValuable) : null,
            Tiles = result.Tiles?.Select(tile => tile with { ImageUrl = wikiImages.GetPublicUrl(tile.ImageUrl) }).ToArray()
        };

        StatsItemIdentity MapItem(StatsItemIdentity item) => item with { ImageUrl = wikiImages.GetPublicUrl(item.ImageUrl) };
    }

    // Razor Pages validates antiforgery for every POST. Actor/role are checked from current storage.
    public async Task<IActionResult> OnPostGuidanceAsync(string slug, bool? hidden, uint? expectedVersion, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true || User.GetAccountId() is not Guid actorId) return Unauthorized();
        if (!ModelState.IsValid || hidden is null || expectedVersion is null) return BadRequest();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (await stats.GetAsync(slug, cancellationToken) is null) return NotFound();
        var actor = await db.Accounts.SingleOrDefaultAsync(x => x.Id == actorId && x.Active, cancellationToken);
        if (actor is null) return StatusCode(403, new { error = "You no longer have permission to save these settings." });
        if (actor.Version != expectedVersion) return Conflict("Your account changed. Reload the page before saving this preference.");
        actor.SetStatsGuidanceHidden(hidden.Value);
        db.Entry(actor).Property(x => x.Version).IsModified = true;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new JsonResult(new { hidden = actor.StatsGuidanceHidden, version = actor.Version });
        }
        catch (Exception exception) when (IsRace(exception)) { return Conflict("Your account changed. Reload the page before saving this preference."); }
    }

    public async Task<IActionResult> OnPostArtworkAsync(string slug, Guid itemId, long? expectedVersion, bool reset,
        decimal? x, decimal? y, decimal? width, decimal? height, decimal? scale, decimal? rotation, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true || User.GetAccountId() is not Guid actorId) return Unauthorized();
        if (!ModelState.IsValid || expectedVersion is null) return BadRequest();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var actor = await db.Accounts.SingleOrDefaultAsync(a => a.Id == actorId && a.Active, cancellationToken);
        if (actor?.GlobalRole != GlobalRole.SuperAdmin) return StatusCode(403, new { error = "You no longer have permission to save these settings." });
        var result = await stats.GetAsync(slug, cancellationToken);
        if (result is null) return NotFound();
        // The approved editor edits the actual displayed repeat item, never a client-selected catalogue row.
        if (result.RepeatedItem?.Item.ItemId != itemId) return NotFound();
        var item = await db.CatalogueItems.SingleOrDefaultAsync(a => a.Id == itemId, cancellationToken);
        if (item is null) return NotFound();
        if (item.Version != expectedVersion) return Conflict("This artwork changed. Cancel and reload before editing it again.");
        var before = JsonSerializer.Serialize(ArtworkState(item), JsonOptions);
        if (reset) item.ResetArtwork();
        else
        {
            if (x is null || y is null || width is null || height is null || scale is null || rotation is null) return BadRequest();
            try { item.SetArtwork(x.Value, y.Value, width.Value, height.Value, scale.Value, rotation.Value); }
            catch (ArgumentOutOfRangeException) { return BadRequest("Artwork settings are outside the supported range."); }
        }
        // Check a no-op/reset against concurrent catalogue changes too.
        db.Entry(item).Property(a => a.Version).IsModified = true;
        db.Entry(actor).Property(a => a.Version).IsModified = true;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            var saved = ArtworkState(item);
            db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), time.GetUtcNow(), actor.Id, actor.LoginName,
                reset ? "catalogue.artwork_reset" : "catalogue.artwork_updated", "catalogue_item", item.Id.ToString(), item.Name,
                result.EventId, before, JsonSerializer.Serialize(saved, JsonOptions)));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new JsonResult(new { artwork = saved, accountVersion = actor.Version }, JsonOptions);
        }
        catch (Exception exception) when (IsRace(exception)) { return Conflict("This artwork or your account changed. Cancel and reload before editing it again."); }
    }

    private ObjectResult Conflict(string message) => StatusCode(409, new { error = message });
    private static bool IsRace(Exception exception) => exception is DbUpdateConcurrencyException
        or PostgresException { SqlState: "40001" or "40P01" }
        or DbUpdateException { InnerException: PostgresException { SqlState: "40001" or "40P01" } }
        || exception.InnerException is { } inner && IsRace(inner);
    public static ArtworkView ArtworkState(CatalogueItem item) => new(item.Id, item.Version, item.ExternalIdentifier, item.ArtworkX is null ? null :
        new(item.ArtworkX.Value, item.ArtworkY!.Value, item.ArtworkWidth!.Value, item.ArtworkHeight!.Value, item.ArtworkScale!.Value, item.ArtworkRotation!.Value));
    public sealed record ArtworkView(Guid ItemId, long Version, string? ExternalIdentifier, ArtworkFit? Fit);
    public sealed record ArtworkFit(decimal X, decimal Y, decimal Width, decimal Height, decimal Scale, decimal Rotation);
}
