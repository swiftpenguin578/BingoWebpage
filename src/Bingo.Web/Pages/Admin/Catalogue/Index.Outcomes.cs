using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Catalogue;

// T2 item 1: in-place (fetch) outcomes for every Catalogue handler, the S10 deactivation
// impact read and the D7/D9 named-activity confirmation carried in the response.
// A script request (Accept: application/json + X-Requested-With) gets a JSON outcome;
// a plain form post keeps today's redirect with a status message.
public sealed partial class IndexModel
{
    /// <summary>C-CAT-2 / T2-Q4 (a): the fixed activity categories (Catalogue.dc.html:491).</summary>
    public static readonly IReadOnlyList<string> Categories = ["Boss", "Skilling boss", "Minigame"];
    public const int ItemNameLimit = 200;
    // Item 13 (brief 82): wording of the unchanged roll-group refusal (08 "Catalogue layout").
    public const string RollGroupSuperAdminOnly = "Roll group can only be changed by the Super Admin.";
    public const string RetiredFieldsRefused = "These rate settings can’t be changed here. Your changes were not saved. Reload the editor.";
    public const string CategoryInvalid = "Choose Boss, Skilling boss or Minigame.";
    public const string ItemNameTooLong = "Use 200 characters or fewer.";
    public const string StaleMessage = "This record was changed by another administrator. Current values are shown; review them before saving.";

    internal bool JsonRequest => Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
        && Request.Headers.XRequestedWith == "XMLHttpRequest";

    /// <summary>Activities named in the last D7/D9 refusal (the response carries them; never TempData).</summary>
    public IReadOnlyList<SharedItemActivity> SharedItemAffectedActivities { get; private set; } = [];
    /// <summary>The last S10 impact this request built (also returned in the JSON response).</summary>
    public DeactivationImpact? LastDeactivationImpact { get; private set; }

    /// <summary>One outcome for both transports. JSON: { outcome, message, ...data }. Form: status + redirect.</summary>
    private IActionResult Respond(string outcome, string message, UiMessageType type, IReadOnlyDictionary<string, object?>? data = null)
    {
        if (JsonRequest)
        {
            var body = new Dictionary<string, object?> { ["outcome"] = outcome, ["message"] = message };
            if (data is not null) foreach (var (key, value) in data) body[key] = value;
            Response.Headers.CacheControl = "no-store";
            return new JsonResult(body);
        }
        SetStatus(message, type);
        return CataloguePage();
    }

    private IActionResult Invalid(string field, string message) =>
        Respond("invalid", message, UiMessageType.Error, new Dictionary<string, object?> { ["field"] = field, ["errors"] = new Dictionary<string, string> { [field] = message } });

    private IActionResult InvalidModel(string prefix)
    {
        var errors = new Dictionary<string, string>();
        foreach (var (key, state) in ModelState)
        {
            var error = state.Errors.FirstOrDefault();
            if (error is null) continue;
            var name = key.StartsWith(prefix + ".", StringComparison.Ordinal) ? key[(prefix.Length + 1)..] : key;
            var field = name switch
            {
                "Name" => "name", "Category" => "category", "EfficientRate" => "rate", "TeamSize" => "teamSize", "ImageUrl" => "image",
                "ItemName" => "name", "DisplayRate" => "rate", _ => name
            };
            errors.TryAdd(field, error.ErrorMessage);
        }
        var message = string.Join(" ", errors.Values);
        return Respond("invalid", message, UiMessageType.Error, new Dictionary<string, object?> { ["field"] = errors.Keys.FirstOrDefault(), ["errors"] = errors });
    }

    private IActionResult Stale() => Respond("stale", Localize(StaleMessage), UiMessageType.Warning);
    private IActionResult RetiredFields() => Respond("refused", L(RetiredFieldsRefused), UiMessageType.Warning);
    private IActionResult RollGroupRefused() => Respond("refused", L(RollGroupSuperAdminOnly), UiMessageType.Warning, new Dictionary<string, object?> { ["field"] = "rollGroup" });

    /// <summary>C-CAT-2: the category must be one of the fixed list (exact, after trimming).</summary>
    public static bool ValidCategory(string? category) => category is not null && Categories.Contains(category.Trim(), StringComparer.Ordinal);

    // D7/D9: the affected activities travel in the response; accepted only for the exact current set.
    private IActionResult RefuseSharedItemChange(IReadOnlyList<SharedItemActivity> affectedActivities)
    {
        SharedItemAffectedActivities = affectedActivities;
        var names = affectedActivities.Count == 0 ? Localize("no other activities") : string.Join(", ", affectedActivities.Select(x => x.Name));
        return Respond("confirm-shared",
            Localize("This shared item is also used by {0}. Confirm the name or image change by confirming those activities. Nothing was saved.", names),
            UiMessageType.Warning,
            new Dictionary<string, object?> { ["activities"] = affectedActivities.Select(x => new { id = x.Id, name = x.Name }).ToArray() });
    }

    private string L(string key, params object[] arguments) => community?[key, arguments].Value ?? string.Format(System.Globalization.CultureInfo.CurrentCulture, key, arguments);

    /* ---------------- S10: draft boards that deactivation keeps from being approved ---------------- */

    public sealed record ImpactBoard(Guid EventId, string EventName, bool Hidden, bool Correction, string BoardUrl);
    public sealed record DeactivationImpact(string RecordType, Guid RecordId, string Name, bool Active, IReadOnlyList<ImpactBoard> Boards, int HiddenCount);

    /// <summary>
    /// Read-only S10 query (kept in the Catalogue page model, 42f §2.7). A draft board is listed when one of its
    /// current tiles has an objective whose eligible activity or drop is the record (for an activity, also any of
    /// its drops), because approval refuses inactive sources (Board.cshtml.cs "source-inactive"/"drop-inactive").
    /// A published board's correction copy is listed only when such an objective is not part of the active
    /// approval: approval of a correction checks only those (T2-Q3b (a), verified in CreateApprovalSnapshotAsync).
    /// Events that can no longer approve a board (Finalized, Archived, Cancelled, Discarded) are left out.
    /// T2-Q3 (a): hidden events are named only for the Super Admin; others get a count.
    /// </summary>
    private async Task<DeactivationImpact?> BuildDeactivationImpactAsync(string recordType, Guid recordId, CancellationToken ct)
    {
        string name; bool active;
        IQueryable<Guid> requirementIds;
        if (recordType == "boss")
        {
            var boss = await dbContext.BossActivities.AsNoTracking().Where(x => x.Id == recordId).Select(x => new { x.Name, x.Active }).SingleOrDefaultAsync(ct);
            if (boss is null) return null;
            (name, active) = (boss.Name, boss.Active);
            var dropIds = dbContext.SourceDrops.Where(x => x.BossActivityId == recordId).Select(x => x.Id);
            requirementIds = dbContext.BoardRequirementBossSnapshots.Where(x => x.BossActivityId == recordId).Select(x => x.RequirementId)
                .Union(dbContext.BoardRequirementDropSnapshots.Where(x => dropIds.Contains(x.SourceDropId)).Select(x => x.RequirementId));
        }
        else if (recordType == "drop")
        {
            var drop = await (from d in dbContext.SourceDrops.AsNoTracking()
                              join i in dbContext.CatalogueItems.AsNoTracking() on d.ItemId equals i.Id
                              where d.Id == recordId
                              select new { i.Name, d.Active }).SingleOrDefaultAsync(ct);
            if (drop is null) return null;
            (name, active) = (drop.Name, drop.Active);
            requirementIds = dbContext.BoardRequirementDropSnapshots.Where(x => x.SourceDropId == recordId).Select(x => x.RequirementId);
        }
        else return null;

        var closed = new[] { EventState.Finalized, EventState.Archived, EventState.Cancelled, EventState.Discarded };
        var uses = await (from requirement in dbContext.BoardRequirementSnapshots.AsNoTracking()
                          join tile in dbContext.BoardTiles.AsNoTracking() on requirement.BoardTileId equals tile.Id
                          join board in dbContext.Boards.AsNoTracking() on tile.BoardId equals board.Id
                          join bingoEvent in dbContext.Events.AsNoTracking() on board.EventId equals bingoEvent.Id
                          where requirementIds.Contains(requirement.Id)
                                && (board.State == BoardState.Draft || board.State == BoardState.Published && board.PublishedCorrectionInProgress)
                                && !closed.Contains(bingoEvent.State)
                          select new
                          {
                              RequirementId = requirement.Id,
                              board.State,
                              board.ActiveApprovalSnapshotId,
                              EventId = bingoEvent.Id,
                              EventName = bingoEvent.Name,
                              bingoEvent.HiddenAt
                          }).ToListAsync(ct);

        var approvalIds = uses.Where(x => x.State == BoardState.Published && x.ActiveApprovalSnapshotId is not null).Select(x => x.ActiveApprovalSnapshotId!.Value).Distinct().ToArray();
        var approved = approvalIds.Length == 0 ? new HashSet<Guid>() : (await (
            from requirement in dbContext.BoardApprovalRequirementSnapshots.AsNoTracking()
            join tile in dbContext.BoardApprovalTileSnapshots.AsNoTracking() on requirement.ApprovalTileSnapshotId equals tile.Id
            where approvalIds.Contains(tile.ApprovalSnapshotId)
            select requirement.BoardRequirementSnapshotId).ToListAsync(ct)).ToHashSet();

        var superAdmin = User.IsInRole("SuperAdmin");
        var events = uses
            .Where(x => x.State == BoardState.Draft || !approved.Contains(x.RequirementId))
            .GroupBy(x => x.EventId)
            .Select(group => new ImpactBoard(group.Key, group.First().EventName, group.First().HiddenAt is not null,
                group.First().State == BoardState.Published,
                // T2-1 (a): a hidden event's Board page is Not Found even for the Super Admin, so its name links
                // to Overview's limited view (as A7). Ordinary Admins never receive hidden events.
                group.First().HiddenAt is null ? $"/Admin/Events/Board/{group.Key}" : $"/Admin/Events/Manage/{group.Key}?hidden=true"))
            .OrderBy(x => x.EventName, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.EventId)
            .ToList();
        var hiddenCount = superAdmin ? 0 : events.Count(x => x.Hidden);
        var visible = superAdmin ? events : events.Where(x => !x.Hidden).ToList();
        return LastDeactivationImpact = new DeactivationImpact(recordType, recordId, name, active, visible, hiddenCount);
    }

    private static object ImpactJson(DeactivationImpact impact) => new
    {
        recordType = impact.RecordType,
        recordId = impact.RecordId,
        name = impact.Name,
        active = impact.Active,
        boards = impact.Boards.Select(x => new { eventId = x.EventId, eventName = x.EventName, hidden = x.Hidden, correction = x.Correction, url = x.BoardUrl }).ToArray(),
        hiddenCount = impact.HiddenCount
    };

    /// <summary>S10 read for the deactivate confirmation (activity or drop). Read-only; any Admin.</summary>
    public async Task<IActionResult> OnGetDeactivationImpactAsync(string recordType, Guid recordId, long expectedVersion, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        long? version = recordType switch
        {
            "boss" => await dbContext.BossActivities.AsNoTracking().Where(x => x.Id == recordId).Select(x => (long?)x.Version).SingleOrDefaultAsync(ct),
            "drop" => await dbContext.SourceDrops.AsNoTracking().Where(x => x.Id == recordId).Select(x => (long?)x.Version).SingleOrDefaultAsync(ct),
            _ => null
        };
        if (recordType is not ("boss" or "drop")) return BadRequest();
        if (version is null) return NotFound();
        if (version != expectedVersion) return new JsonResult(new { outcome = "stale", message = Localize(StaleMessage) });
        var impact = await BuildDeactivationImpactAsync(recordType, recordId, ct);
        if (impact is null) return NotFound();
        return new JsonResult(new { outcome = "impact", impact = ImpactJson(impact) });
    }
}
