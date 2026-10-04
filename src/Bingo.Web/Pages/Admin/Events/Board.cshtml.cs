using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Boards;
using Bingo.Application.Evidence;
using Bingo.Application.Teams;
using Bingo.Domain.Auditing;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.Teams;
using Bingo.Web.Security;
using Bingo.Web.Boards;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Npgsql;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed partial class BoardModel(ApplicationDbContext db, TimeProvider time, IAuditWriter audit, IAdminCollaborationNotifier collaboration, IEvidenceStorage storage, IStringLocalizer<SharedResource>? text = null, EventItemPriceService? itemPrices = null) : PageModel
{
    public IReadOnlyList<BoardValidationIssue> ValidationIssues { get; private set; } = [];
    public string EventName { get; private set; } = string.Empty;
    public BoardDetails? BoardView { get; private set; }
    public BoardStatistics? Statistics { get; private set; }
    public IReadOnlyList<TileView> Tiles { get; private set; } = [];
    public IReadOnlyList<LineView> Lines { get; private set; } = [];
    public IReadOnlyList<BossView> Bosses { get; private set; } = [];
    public IReadOnlyList<DropView> Drops { get; private set; } = [];
    public IReadOnlyList<TileEditorView> TileEditors { get; private set; } = [];
    public IReadOnlyList<TeamWorkloadView> TeamWorkloads { get; private set; } = [];
    public decimal BalanceSpread { get; private set; }
    public IReadOnlyList<string> ReadinessWarnings { get; private set; } = [];
    public Guid CurrentAccountId { get; private set; }
    public bool CanEditBoard { get; private set; }
    public Guid? BoardEditorAccountId { get; private set; }
    public string? BoardEditorName { get; private set; }
    public DateTimeOffset? BoardEditorLeaseExpiresAt { get; private set; }
    public bool DraftFinalized { get; private set; }
    public IReadOnlySet<Guid> ProtectedTileIds { get; private set; } = new HashSet<Guid>();
    public bool CanCreateBoard { get; private set; }
    public EventState EventState { get; private set; }

    [BindProperty, Range(1, 8)] public int Rows { get; set; } = 5;
    [BindProperty, Range(1, 8)] public int Columns { get; set; } = 5;
    [BindProperty] public long BoardVersion { get; set; }
    [BindProperty] public string? ApprovalCatalogueFingerprint { get; set; }
    [BindProperty] public TileDraftInput TileDraft { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        CurrentAccountId = User.GetAccountId()!.Value;
        if (!await EnsureBoardAndEditingLeaseAsync(id, ct)) return NotFound();
        // Board GET is an overview read. Working-copy freshness is refreshed at
        // explicit mutation boundaries, never while a form/token is loaded.
        return await Load(id, ct) ? Page() : NotFound();
    }

    /// <summary>
    /// Loads the expensive editor choices only after a user opens a tile
    /// editor. The response includes a revision token so the browser can reuse
    /// the choices for other tiles in the same page session and recover when a
    /// catalogue administrator changes them.
    /// </summary>
    public async Task<IActionResult> OnGetEditorDataAsync(Guid id, Guid? tileId, CancellationToken ct)
    {
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (board is null) return NotFound();
        var bossRows = await db.BossActivities.AsNoTracking()
            .Where(x => x.Active)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Category, x.EfficientCompletionsPerHour, x.Version })
            .ToListAsync(ct);
        var dropRows = await (from drop in db.SourceDrops.AsNoTracking()
                              join boss in db.BossActivities.AsNoTracking() on drop.BossActivityId equals boss.Id
                              join item in db.CatalogueItems.AsNoTracking() on drop.ItemId equals item.Id
                              where drop.Active && boss.Active && item.Active
                              orderby boss.Name, item.Name
                              select new { drop.Id, BossId = boss.Id, BossName = boss.Name, ItemName = item.Name, Rate = drop.DisplayRate, DropVersion = drop.Version, BossVersion = boss.Version, ItemVersion = item.Version })
            .ToListAsync(ct);
        var bosses = bossRows.Select(x => new BossView(x.Id, x.Name, x.Category, x.EfficientCompletionsPerHour)).ToList();
        var drops = dropRows.Select(x => new DropView(x.Id, x.BossId, x.BossName, x.ItemName, x.Rate)).ToList();
        TileEditorView? tile = null;
        if (tileId is { } requestedTileId)
        {
            var current = await db.BoardTiles.AsNoTracking().SingleOrDefaultAsync(x => x.BoardId == board.Id && x.Id == requestedTileId, ct);
            if (current is null) return NotFound();
            var requirements = await db.BoardRequirementSnapshots.AsNoTracking().Where(x => x.BoardTileId == current.Id).OrderBy(x => x.Position).ToListAsync(ct);
            var requirementIds = requirements.Select(x => x.Id).ToArray();
            var requirementBosses = await db.BoardRequirementBossSnapshots.AsNoTracking().Where(x => requirementIds.Contains(x.RequirementId)).ToListAsync(ct);
            var requirementDrops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => requirementIds.Contains(x.RequirementId)).ToListAsync(ct);
            var template = await db.TileTemplates.AsNoTracking().SingleOrDefaultAsync(x => x.Id == current.TileTemplateId, ct);
            if (template is null) return NotFound();
            var image = current.ActiveImageAssetId is { } imageId && await db.BoardTileImageAssets.AsNoTracking().AnyAsync(x => x.Id == imageId && x.BoardTileId == current.Id && x.EventId == id && x.ReplacedAt == null, ct)
                ? Url.Page("Board", "TileImage", new { id, tileId = current.Id })
                : null;
            var editorRequirements = requirements.Select(requirement => new RequirementEditorView(
                requirement.Id,
                requirement.ManualObjective ? "challenge" : "drops",
                requirement.Description,
                requirement.TargetContribution,
                requirement.DuplicatesAllowed,
                requirementBosses.Where(x => x.RequirementId == requirement.Id).Select(x => x.BossActivityId).ToList(),
                requirementDrops.Where(x => x.RequirementId == requirement.Id).Select(x => x.SourceDropId).ToList(),
                requirementDrops.Where(x => x.RequirementId == requirement.Id).ToDictionary(x => x.SourceDropId, x => x.CreditedWeight),
                requirementDrops.Where(x => x.RequirementId == requirement.Id).Select(drop => new RequirementDropView(drop.SourceDropId, drop.BossName, drop.ItemName, drop.DisplayRate, drop.CreditedWeight)).ToList())).ToList();
            tile = new TileEditorView(current.Id, current.NameSnapshot, current.DescriptionIsAutomatic ? string.Empty : current.DescriptionSnapshot,
                image, template.ManualEhbOverride, editorRequirements,
                (await BoardEstimateService.CalculatedBaselinesAsync(db, [current.Id], ct)).GetValueOrDefault(current.Id));
        }
        var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            Bosses = bossRows.Select(x => new { x.Id, x.Version }).ToArray(),
            Drops = dropRows.Select(x => new { x.Id, x.DropVersion, x.BossVersion, x.ItemVersion }).ToArray()
        }))));
        return new JsonResult(new
        {
            boardVersion = board.Version,
            revision,
            bosses = bosses.Select((choice, index) => new { choice.Id, choice.Name, choice.Category, choice.EfficientRate, Version = bossRows[index].Version }),
            drops = drops.Select((choice, index) => new { choice.Id, choice.BossId, choice.BossName, choice.ItemName, Rate = choice.Rate, Version = dropRows[index].DropVersion, ItemVersion = dropRows[index].ItemVersion }),
            tile
        });
    }

    public async Task<IActionResult> OnPostCreateAsync(Guid id, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await Load(id, ct) ? Page() : NotFound();
        var bingoEvent = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (bingoEvent is null) return NotFound();
        if (!CanCreateLegacyBoard(bingoEvent))
        {
            SetStatus(Localize("A board can only be created for an unfinished pre-live event."), UiMessageType.Warning);
            return RedirectToPage(new { id });
        }
        if (await db.Boards.AnyAsync(x => x.EventId == id, ct))
        {
            SetStatus(Localize("A board already exists for this event."), UiMessageType.Warning);
            return RedirectToPage(new { id });
        }
        var board = new Board(Guid.NewGuid(), id, "Main board", Rows, Columns);
        board.AcquireEditing(AdminId, time.GetUtcNow(), BoardEditingLease.Duration);
        db.Boards.Add(board); await WriteAudit("board.created", board, null, BoardAuditState(board), ct);
        await collaboration.NotifyBoardChangedAsync(id, ct);
        SetStatus(Localize("Board created."), UiMessageType.Success);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostTakeEditingAsync(Guid id, CancellationToken ct)
    {
        var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct); if (board is null) return NotFound();
        try
        {
            var before = BoardAuditState(board);
            var previous = board.AcquireEditing(AdminId, time.GetUtcNow(), BoardEditingLease.Duration, force: true);
            await WriteAudit(previous is null ? "board.editing_acquired" : "board.editing_taken_over", board, before, BoardAuditState(board), ct);
            await collaboration.NotifyBoardChangedAsync(id, ct);
            SetStatus(previous is null ? Localize("Board editing control acquired.") : Localize("Board editing control taken over."), UiMessageType.Success);
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            SetStatus(exception is DbUpdateConcurrencyException
                ? Localize("Another administrator changed the board editor first. The latest board has been loaded.")
                : Localize("The board editing control could not be acquired."), UiMessageType.Warning);
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAcquireEditingAsync(Guid id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var board = await db.Boards
                .FromSqlInterpolated($"SELECT * FROM boards WHERE event_id = {id} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (board is null) return NotFound();
            if (board.Version != BoardVersion)
            {
                SetStatus(Localize("This board changed after you opened it. Your change was not saved. The latest board has been loaded."), UiMessageType.Warning);
                return RedirectToPage(new { id });
            }

            var now = time.GetUtcNow();
            if (board.HasActiveEditor(now))
            {
                SetStatus(Localize("The board editing control could not be acquired."), UiMessageType.Warning);
                return RedirectToPage(new { id });
            }

            var before = BoardAuditState(board);
            board.AcquireEditing(AdminId, now, BoardEditingLease.Duration);
            await WriteAudit("board.editing_acquired", board, before, BoardAuditState(board), ct);
            await transaction.CommitAsync(ct);
            await collaboration.NotifyBoardChangedAsync(id, ct);
            SetStatus(Localize("Board editing control acquired."), UiMessageType.Success);
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            SetStatus(exception is DbUpdateConcurrencyException
                ? Localize("Another administrator changed the board editor first. The latest board has been loaded.")
                : Localize("The board editing control could not be acquired."), UiMessageType.Warning);
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReleaseEditingAsync(Guid id, CancellationToken ct)
    {
        var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct); if (board is null) return NotFound();
        try { var before = BoardAuditState(board); board.ReleaseEditing(AdminId, time.GetUtcNow()); await WriteAudit("board.editing_released", board, before, BoardAuditState(board), ct); await collaboration.NotifyBoardChangedAsync(id, ct); SetStatus(Localize("Board editing control released."), UiMessageType.Success); }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear(); SetStatus(exception is DbUpdateConcurrencyException ? Localize("Editing control changed before it could be released. The latest board has been loaded.") : Localize("The board editing control could not be released."), UiMessageType.Warning);
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCreateTileAsync(Guid id, CancellationToken ct)
    {
        TempData["BoardTileOutcome"] = "failed";
        var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct); if (board is null) return NotFound();
        if (!board.IsEditable || TileDraft.Position < 0 || TileDraft.Position >= board.Rows * board.Columns)
        {
            SetStatus(Localize("This board is no longer editable or that board position is invalid."), UiMessageType.Warning);
            return RedirectToPage(new { id });
        }
        if (await db.BoardTiles.AnyAsync(x => x.BoardId == board.Id && x.RowIndex == TileDraft.Position / board.Columns && x.ColumnIndex == TileDraft.Position % board.Columns, ct)) { SetStatus(Localize("That board position is no longer empty."), UiMessageType.Warning); return RedirectToPage(new { id }); }
        TileDraft.Requirements = TileDraft.Requirements.Where(x => !string.IsNullOrWhiteSpace(x.Description) || x.BossIds.Count > 0 || x.DropIds.Count > 0).ToList();
        if (TileDraft.Requirements.Count == 0) ModelState.AddModelError(string.Empty, Localize("Add at least one requirement."));
        foreach (var requirement in TileDraft.Requirements)
        {
            if (requirement.Target < 1) ModelState.AddModelError(string.Empty, Localize("Every requirement needs a quantity of at least 1."));
            if (requirement.DropWeights.Any(x => x.Value < 1)) ModelState.AddModelError(string.Empty, Localize("Every selected drop count must be at least 1."));
            if (!requirement.IsManual && requirement.BossIds.Count == 0) ModelState.AddModelError(string.Empty, Localize("Choose at least one boss for each collect-drops requirement."));
            if (!requirement.IsManual && requirement.DropIds.Count == 0) ModelState.AddModelError(string.Empty, Localize("Choose at least one eligible drop for each collect-drops requirement."));
            if (requirement.IsManual && string.IsNullOrWhiteSpace(requirement.Description)) ModelState.AddModelError(string.Empty, Localize("Describe the challenge requirements."));
        }
        if (TileDraft.Requirements.Select(x => x.IsManual).Distinct().Count() > 1)
            ModelState.AddModelError(string.Empty, Localize("Use separate tiles for catalogue drops and custom challenges. Every objective in a tile must have the same kind."));
        if (TileDraft.Requirements.All(x => x.IsManual) && TileDraft.ManualEhb is not > 0) ModelState.AddModelError(string.Empty, Localize("A custom challenge needs an explicit manual EHB estimate."));
        if (!ModelState.IsValid) { SetStatus(string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)), UiMessageType.Warning); return RedirectToPage(new { id }); }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        try
        {
            if (!await TryEnsureSelectedDropPricesAsync(id, ct)) return RedirectToPage(new { id });
        }
        catch (Exception exception) when (IsApprovalConflict(exception))
        {
            db.ChangeTracker.Clear();
            SetStatus(Localize("The catalogue changed while the tile was being saved. No tile was added; reload and try again."), UiMessageType.Warning);
            return RedirectToPage(new { id });
        }
        var before = new { board = BoardAuditState(board), tile = (object?)null };
        if (!PrepareCompetitiveEdit(board)) return RedirectToPage(new { id });
        if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id });
        var selectedBossIds = TileDraft.Requirements.SelectMany(x => x.BossIds).Distinct().ToList();
        var selectedBosses = await db.BossActivities.Where(x => selectedBossIds.Contains(x.Id)).OrderBy(x => x.Name).ToListAsync(ct);
        var name = string.IsNullOrWhiteSpace(TileDraft.Name) ? DefaultTileName(selectedBosses.Select(x => x.Name)) : TileDraft.Name.Trim();
        var requirementDescriptions = TileDraft.Requirements.Select(RequirementDescription).ToList();
        var descriptionIsAutomatic = string.IsNullOrWhiteSpace(TileDraft.Description);
        var description = descriptionIsAutomatic ? string.Empty : TileDraft.Description!.Trim();
        var objectiveType = TileDraft.Requirements.All(x => x.IsManual) ? ObjectiveType.Manual : ObjectiveType.DropRequirements;
        if (objectiveType == ObjectiveType.DropRequirements && !TileDraft.ChangeManualEhbOverride) TileDraft.ManualEhb = null;
        var template = new TileTemplate(Guid.NewGuid(), name, description, objectiveType, string.Empty, TileDraft.ManualEhb, descriptionIsAutomatic: descriptionIsAutomatic);
        db.TileTemplates.Add(template);
        var requirements = new List<TileTemplateRequirement>();
        for (var index = 0; index < TileDraft.Requirements.Count; index++)
        {
            var input = TileDraft.Requirements[index];
            var requirement = new TileTemplateRequirement(Guid.NewGuid(), template.Id, index + 1, input.Target, input.DuplicatesAllowed, input.HasHigherWeights, requirementDescriptions[index], input.IsManual);
            requirements.Add(requirement); db.TileTemplateRequirements.Add(requirement);
            foreach (var bossId in input.BossIds.Distinct()) db.TemplateRequirementBosses.Add(new TemplateRequirementBoss(Guid.NewGuid(), requirement.Id, bossId));
            foreach (var dropId in input.DropIds.Distinct()) db.TemplateRequirementDrops.Add(new TemplateRequirementDrop(Guid.NewGuid(), requirement.Id, dropId, input.DuplicatesAllowed ? null : 1, input.WeightFor(dropId)));
        }
        await db.SaveChangesAsync(ct);
        var tile = await PlaceTemplateAsync(board, template, requirements, TileDraft.Position, ct);
        // Persist the tile before assigning an image which references it in return.
        await db.SaveChangesAsync(ct);
        await BoardEstimateService.RefreshTilesAsync(db, [tile.Id], time.GetUtcNow(), ct);
        var uploaded = await ReplaceTileImageAsync(id, tile, ct);
        try
        {
            await db.SaveChangesAsync(ct);
            await WriteAudit("board.tile_created", board, before, new { board = BoardAuditState(board), tile = await TileAuditStateAsync(tile, ct) }, ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            if (uploaded is not null) await storage.DeleteAsync(uploaded, CancellationToken.None);
            throw;
        }
        await collaboration.NotifyBoardChangedAsync(id, ct);
        SetSuccessIfMissing(Localize("Tile created."));
        TempData["BoardTileOutcome"] = "committed";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostEditTileAsync(Guid id, CancellationToken ct)
    {
        TempData["BoardTileOutcome"] = "failed";
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        string? uploaded = null;
        try
        {
            if (await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {id} AND hidden_at IS NULL FOR UPDATE").SingleOrDefaultAsync(ct) is null) return NotFound();
            var board = await db.Boards.FromSqlInterpolated($"SELECT * FROM boards WHERE event_id = {id} FOR UPDATE").SingleOrDefaultAsync(ct); if (board is null) return NotFound();
            if (!board.IsEditable || TileDraft.TileId is null)
            {
                SetStatus(Localize("This board is no longer editable or the tile is invalid."), UiMessageType.Warning);
                return RedirectToPage(new { id });
            }
            var tile = await db.BoardTiles.SingleOrDefaultAsync(x => x.BoardId == board.Id && x.Id == TileDraft.TileId, ct); if (tile is null) return NotFound();
            TileDraft.Requirements = TileDraft.Requirements.Where(x => !string.IsNullOrWhiteSpace(x.Description) || x.BossIds.Count > 0 || x.DropIds.Count > 0).ToList();
            if (TileDraft.Requirements.Count == 0) ModelState.AddModelError(string.Empty, Localize("Add at least one requirement."));
            foreach (var requirement in TileDraft.Requirements)
            {
                if (requirement.Target < 1) ModelState.AddModelError(string.Empty, Localize("Every requirement needs a quantity of at least 1."));
                if (requirement.DropWeights.Any(x => x.Value < 1)) ModelState.AddModelError(string.Empty, Localize("Every selected drop count must be at least 1."));
                if (!requirement.IsManual && requirement.BossIds.Count == 0) ModelState.AddModelError(string.Empty, Localize("Choose at least one boss for each collect-drops requirement."));
                if (!requirement.IsManual && requirement.DropIds.Count == 0) ModelState.AddModelError(string.Empty, Localize("Choose at least one eligible drop for each collect-drops requirement."));
                if (requirement.IsManual && string.IsNullOrWhiteSpace(requirement.Description)) ModelState.AddModelError(string.Empty, Localize("Describe the challenge requirements."));
            }
            if (TileDraft.Requirements.Select(x => x.IsManual).Distinct().Count() > 1)
                ModelState.AddModelError(string.Empty, Localize("Use separate tiles for catalogue drops and custom challenges. Every objective in a tile must have the same kind."));
            if (TileDraft.Requirements.All(x => x.IsManual) && TileDraft.ManualEhb is not > 0) ModelState.AddModelError(string.Empty, Localize("A custom challenge needs an explicit manual EHB estimate."));
            if (!ModelState.IsValid) { SetStatus(string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)), UiMessageType.Warning); return RedirectToPage(new { id }); }

            if (!await TryEnsurePublishedCorrectionLifecycleAsync(board, id, ct))
            {
                SetStatus(Localize("This event is read-only in its current lifecycle state."), UiMessageType.Warning);
                return RedirectToPage(new { id });
            }
            var before = new { board = BoardAuditState(board), tile = await TileAuditStateAsync(tile, ct) };
            if (!PrepareCompetitiveEdit(board)) return RedirectToPage(new { id });
            if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id });
            var selectedBossIds = TileDraft.Requirements.SelectMany(x => x.BossIds).Distinct().ToList();
            var selectedBosses = await db.BossActivities.Where(x => selectedBossIds.Contains(x.Id)).OrderBy(x => x.Name).ToListAsync(ct);
            var name = string.IsNullOrWhiteSpace(TileDraft.Name) ? DefaultTileName(selectedBosses.Select(x => x.Name)) : TileDraft.Name.Trim();
            var requirementDescriptions = TileDraft.Requirements.Select(RequirementDescription).ToList();
            var descriptionIsAutomatic = string.IsNullOrWhiteSpace(TileDraft.Description);
            var description = descriptionIsAutomatic ? string.Empty : TileDraft.Description!.Trim();
            var objectiveType = TileDraft.Requirements.All(x => x.IsManual) ? ObjectiveType.Manual : ObjectiveType.DropRequirements;
            var template = await db.TileTemplates.SingleAsync(x => x.Id == tile.TileTemplateId, ct);
            if (objectiveType == ObjectiveType.DropRequirements)
                TileDraft.ManualEhb = template.ObjectiveType != ObjectiveType.DropRequirements ? null
                    : TileDraft.ChangeManualEhbOverride ? TileDraft.ManualEhb : template.ManualEhbOverride;
            var oldSnapshots = await db.BoardRequirementSnapshots.Where(x => x.BoardTileId == tile.Id).ToListAsync(ct);
            var oldSnapshotIds = oldSnapshots.Select(x => x.Id).ToList();
            var oldBosses = await db.BoardRequirementBossSnapshots.Where(x => oldSnapshotIds.Contains(x.RequirementId)).ToListAsync(ct);
            var oldDrops = await db.BoardRequirementDropSnapshots.Where(x => oldSnapshotIds.Contains(x.RequirementId)).ToListAsync(ct);
            var inputIds = TileDraft.Requirements.Where(x => x.RequirementId != null).Select(x => x.RequirementId!.Value).ToList();
            if (inputIds.Distinct().Count() != inputIds.Count || inputIds.Any(x => !oldSnapshotIds.Contains(x)))
                throw new InvalidOperationException("An objective no longer belongs to this tile. Reload before editing.");
            bool SameRules(RequirementInput input, BoardRequirementSnapshot old) =>
                old.HasSameRules(input.Target, input.DuplicatesAllowed, input.HasHigherWeights, input.IsManual, old.CreditedWeight) &&
                oldBosses.Where(x => x.RequirementId == old.Id).Select(x => x.BossActivityId).ToHashSet().SetEquals(input.BossIds) &&
                oldDrops.Where(x => x.RequirementId == old.Id).Select(x => x.SourceDropId).ToHashSet().SetEquals(input.DropIds) &&
                oldDrops.Where(x => x.RequirementId == old.Id).All(x => x.CreditedWeight == input.WeightFor(x.SourceDropId));
            var retained = TileDraft.Requirements.Where(x => template.ManualEhbOverride == TileDraft.ManualEhb && x.RequirementId is { } requirementId && SameRules(x, oldSnapshots.Single(y => y.Id == requirementId)))
                .Select(x => x.RequirementId!.Value).ToHashSet();
            if (!await TryEnsureSelectedDropPricesAsync(id, ct, retained,
                oldDrops.Where(x => retained.Contains(x.RequirementId)).Select(x => x.ItemIdSnapshot))) return RedirectToPage(new { id });
            var evidenced = await db.EvidencedObjectiveIdsAsync(id, ct);
            if (oldSnapshotIds.Any(x => evidenced.Contains(x) && !retained.Contains(x)) ||
                oldSnapshotIds.Any(evidenced.Contains) && template.ManualEhbOverride != TileDraft.ManualEhb)
                throw new InvalidOperationException("Objectives with submitted evidence cannot change requirements or scoring, or be removed. Only wording corrections are allowed.");
            var publishedTile = await db.BoardApprovalTileSnapshots.AsNoTracking()
                .SingleOrDefaultAsync(x => x.ApprovalSnapshotId == board.ActiveApprovalSnapshotId && x.BoardTileId == tile.Id, ct);
            var publishedTileRequirements = publishedTile is null ? [] : await db.BoardApprovalRequirementSnapshots.AsNoTracking()
                .Where(x => x.ApprovalTileSnapshotId == publishedTile.Id).ToListAsync(ct);
            var protectsPublishedScoring = publishedTileRequirements.Any(x => evidenced.Contains(x.BoardRequirementSnapshotId));
            if (protectsPublishedScoring && TileDraft.Requirements.Sum(x => (long)x.Target) != publishedTileRequirements.Sum(x => (long)x.TargetContribution))
                throw new InvalidOperationException("Objectives with submitted evidence cannot change requirements or scoring, or be removed. Only wording corrections are allowed.");
            template.Update(name, description, objectiveType, string.Empty, TileDraft.ManualEhb, descriptionIsAutomatic: descriptionIsAutomatic);

            var oldTemplateRequirements = await db.TileTemplateRequirements.Where(x => x.TileTemplateId == template.Id).ToListAsync(ct);
            var oldTemplateRequirementIds = oldTemplateRequirements.Select(x => x.Id).ToList();
            db.TemplateRequirementBosses.RemoveRange(await db.TemplateRequirementBosses.Where(x => oldTemplateRequirementIds.Contains(x.RequirementId)).ToListAsync(ct));
            db.TemplateRequirementDrops.RemoveRange(await db.TemplateRequirementDrops.Where(x => oldTemplateRequirementIds.Contains(x.RequirementId)).ToListAsync(ct));
            db.TileTemplateRequirements.RemoveRange(oldTemplateRequirements);

            var removedIds = oldSnapshotIds.Where(x => !retained.Contains(x)).ToList();
            var publishedIds = await db.BoardApprovalRequirementSnapshots
                .Where(x => removedIds.Contains(x.BoardRequirementSnapshotId))
                .Select(x => x.BoardRequirementSnapshotId)
                .ToHashSetAsync(ct);
            // Keep only the immutable drop identity rows needed to resolve an
            // existing approval. Working requirement/boss rows are replaced so
            // current correction queries cannot mix stale and new objectives.
            db.BoardRequirementBossSnapshots.RemoveRange(oldBosses.Where(x => removedIds.Contains(x.RequirementId)));
            db.BoardRequirementDropSnapshots.RemoveRange(oldDrops.Where(x => removedIds.Contains(x.RequirementId) && !publishedIds.Contains(x.RequirementId)));
            db.BoardRequirementSnapshots.RemoveRange(oldSnapshots.Where(x => removedIds.Contains(x.Id)));
            await db.SaveChangesAsync(ct);

            var estimates = new List<decimal?>();
            for (var index = 0; index < TileDraft.Requirements.Count; index++)
            {
                var input = TileDraft.Requirements[index];
                var requirement = new TileTemplateRequirement(Guid.NewGuid(), template.Id, index + 1, input.Target, input.DuplicatesAllowed, input.HasHigherWeights, requirementDescriptions[index], input.IsManual);
                db.TileTemplateRequirements.Add(requirement);
                foreach (var bossId in input.BossIds.Distinct()) db.TemplateRequirementBosses.Add(new TemplateRequirementBoss(Guid.NewGuid(), requirement.Id, bossId));
                foreach (var dropId in input.DropIds.Distinct()) db.TemplateRequirementDrops.Add(new TemplateRequirementDrop(Guid.NewGuid(), requirement.Id, dropId, input.DuplicatesAllowed ? null : 1, input.WeightFor(dropId)));

                var selectedDrops = await (from drop in db.SourceDrops where input.DropIds.Contains(drop.Id) join boss in db.BossActivities on drop.BossActivityId equals boss.Id join item in db.CatalogueItems on drop.ItemId equals item.Id select new { drop, boss, item }).ToListAsync(ct);
                if (selectedDrops.Count != input.DropIds.Distinct().Count() || selectedDrops.Any(x => !input.BossIds.Contains(x.boss.Id)))
                    throw new InvalidOperationException("Choose eligible drops from the selected bosses.");
                if (input.RequirementId is { } priorId && retained.Contains(priorId))
                {
                    oldSnapshots.Single(x => x.Id == priorId).CorrectWording(index + 1, requirementDescriptions[index]);
                }
                else
                {
                    var snapshot = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, index + 1, input.Target, input.DuplicatesAllowed, input.HasHigherWeights, requirementDescriptions[index], input.IsManual);
                    db.BoardRequirementSnapshots.Add(snapshot);
                    foreach (var boss in selectedBosses.Where(x => input.BossIds.Contains(x.Id))) db.BoardRequirementBossSnapshots.Add(new BoardRequirementBossSnapshot(Guid.NewGuid(), snapshot.Id, boss.Id, boss.Name, boss.EfficientCompletionsPerHour));
                    foreach (var value in selectedDrops) db.BoardRequirementDropSnapshots.Add(new BoardRequirementDropSnapshot(Guid.NewGuid(), snapshot.Id, value.drop.Id, value.drop.ItemId, value.boss.Name, value.item.Name, value.drop.DisplayRate, value.drop.NumericProbability, input.DuplicatesAllowed ? null : 1, value.drop.DefaultEhbEstimate, input.WeightFor(value.drop.Id)));
                }
                estimates.Add(input.IsManual ? null : EhbCalculator.CalculateDropRequirement(input.Target, selectedDrops.Select(x => new EligibleDropRate(x.boss.EfficientCompletionsPerHour, x.drop.NumericProbability, x.drop.ItemId, x.boss.Id, input.WeightFor(x.drop.Id), x.drop.RollsPerCompletion, x.drop.RollGroup)), input.DuplicatesAllowed));
            }
            var ehb = oldSnapshotIds.Any(evidenced.Contains) && retained.Count == oldSnapshots.Count && retained.Count == TileDraft.Requirements.Count
                ? tile.EstimatedEhbSnapshot : EhbCalculator.CalculateTileEstimate(objectiveType, TileDraft.Requirements.Zip(estimates, (requirement, estimate) => (requirement.IsManual, estimate)), TileDraft.ManualEhb);
            if (protectsPublishedScoring && decimal.Round(ehb, 4, MidpointRounding.AwayFromZero) != publishedTile!.EstimatedEhb)
                throw new InvalidOperationException("Objectives with submitted evidence cannot change requirements or scoring, or be removed. Only wording corrections are allowed.");
            board.SetTotalEhb(Math.Max(0, board.TotalEhbEstimate - tile.EstimatedEhbSnapshot + ehb));
            tile.UpdateContent(name, description, string.Empty, ehb, descriptionIsAutomatic: descriptionIsAutomatic);
            uploaded = await ReplaceTileImageAsync(id, tile, ct);
            await db.SaveChangesAsync(ct);
            await BoardEstimateService.RefreshTilesAsync(db, [tile.Id], time.GetUtcNow(), ct);
            await db.SaveChangesAsync(ct);
            await WriteAudit("board.tile_edited", board, before, new { board = BoardAuditState(board), tile = await TileAuditStateAsync(tile, ct) }, ct);
            await transaction.CommitAsync(ct);
            committed = true;
            await collaboration.NotifyBoardChangedAsync(id, ct);
            SetSuccessIfMissing(Localize("Tile updated."));
            TempData["BoardTileOutcome"] = "committed";
            return RedirectToPage(new { id });
        }
        catch (Exception exception) when (!committed)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            if (uploaded is not null) await storage.DeleteAsync(uploaded, CancellationToken.None);
            db.ChangeTracker.Clear();
            if (!IsApprovalConflict(exception) && exception is not InvalidOperationException) throw;
            SetStatus(exception is InvalidOperationException ? Localize(exception.Message) : Localize("The board changed while this correction was being saved. Reload and try again."), UiMessageType.Warning);
            return RedirectToPage(new { id });
        }
    }

    public async Task<IActionResult> OnPostMoveAsync(Guid id, Guid sourceId, int targetPosition, CancellationToken ct)
    {
        var isInlineRequest = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (board is null) return isInlineRequest ? MoveFailure(Localize("The board could not be found."), UiMessageType.Error) : NotFound();
        if (!await TryEnsurePublishedCorrectionLifecycleAsync(board, id, ct))
        {
            var message = Localize("This event is read-only in its current lifecycle state.");
            if (!isInlineRequest) SetStatus(message, UiMessageType.Warning);
            return isInlineRequest ? MoveFailure(message, UiMessageType.Warning) : RedirectToPage(new { id });
        }
        if (!board.IsEditable || targetPosition < 0 || targetPosition >= board.Rows * board.Columns)
        {
            var message = Localize("This board is no longer editable or the target position is invalid.");
            if (!isInlineRequest) SetStatus(message, UiMessageType.Warning);
            return isInlineRequest ? MoveFailure(message, UiMessageType.Warning) : RedirectToPage(new { id });
        }
        if (!PrepareCompetitiveEdit(board, reportStatus: !isInlineRequest))
        {
            var message = Localize("This board can no longer be changed here.");
            return isInlineRequest ? MoveFailure(message, UiMessageType.Warning) : RedirectToPage(new { id });
        }
        if (!await TryClaimBoardAsync(board, ct, reportStatus: !isInlineRequest))
        {
            var message = Localize("This board changed before the move could be saved. The latest board has been loaded.");
            return isInlineRequest ? MoveFailure(message, UiMessageType.Warning) : RedirectToPage(new { id });
        }
        var source = await db.BoardTiles.SingleOrDefaultAsync(x => x.BoardId == board.Id && x.Id == sourceId, ct);
        if (source is null) return isInlineRequest ? MoveFailure(Localize("The tile could not be found on this board."), UiMessageType.Warning) : NotFound();
        var targetRow = targetPosition / board.Columns; var targetColumn = targetPosition % board.Columns;
        var target = await db.BoardTiles.SingleOrDefaultAsync(x => x.BoardId == board.Id && x.RowIndex == targetRow && x.ColumnIndex == targetColumn, ct);
        if (target?.Id == source.Id)
        {
            var message = Localize("The tile is already in that position.");
            if (!isInlineRequest) SetSuccessIfMissing(message);
            return isInlineRequest ? MoveSuccess(board.Version, message) : RedirectToPage(new { id });
        }
        var before = new { source = TilePositionAuditState(source), target = target is null ? null : TilePositionAuditState(target) };
        var oldRow = source.RowIndex; var oldColumn = source.ColumnIndex;
        source.Move(-1, -1); await db.SaveChangesAsync(ct);
        if (target is not null) { target.Move(oldRow, oldColumn); await db.SaveChangesAsync(ct); }
        source.Move(targetRow, targetColumn);
        await WriteAudit(target is null ? "board.tile_moved" : "board.tiles_swapped", board, before, new { source = TilePositionAuditState(source), target = target is null ? null : TilePositionAuditState(target) }, ct);
        await transaction.CommitAsync(ct);
        await collaboration.NotifyBoardChangedAsync(id, ct);
        var successMessage = Localize("Tile moved.");
        if (!isInlineRequest) SetSuccessIfMissing(successMessage);
        return isInlineRequest ? MoveSuccess(board.Version, successMessage) : RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostResizeAsync(Guid id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct); var board = await db.Boards.SingleAsync(x => x.EventId == id, ct);
        var tiles = await db.BoardTiles.Where(x => x.BoardId == board.Id).OrderBy(x => x.RowIndex).ThenBy(x => x.ColumnIndex).ToListAsync(ct);
        try
        {
            if (!ModelState.IsValid)
            {
                SetStatus(Localize("Choose a board size between 1x1 and 8x8."), UiMessageType.Warning);
                return RedirectToPage(new { id });
            }
            var outOfBounds = tiles.Count(tile => tile.RowIndex >= Rows || tile.ColumnIndex >= Columns || tile.RowIndex < 0 || tile.ColumnIndex < 0);
            if (outOfBounds > 0)
            {
                SetStatus(Localize("Move or remove {0} tile(s) from the positions outside the new board before shrinking it.", outOfBounds), UiMessageType.Warning);
                return RedirectToPage(new { id });
            }
            if (!PrepareCompetitiveEdit(board)) return RedirectToPage(new { id });
            var before = new { board.Rows, board.Columns, tiles = TileLayoutAuditState(tiles) };
            board.Resize(Rows, Columns, tiles.Count); if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id });
            await WriteAudit("board.resized", board, before, new { board.Rows, board.Columns, tiles = TileLayoutAuditState(tiles) }, ct);
            await transaction.CommitAsync(ct);
            await collaboration.NotifyBoardChangedAsync(id, ct);
        }
        catch (InvalidOperationException exception) { SetStatus(Localize(exception.Message), UiMessageType.Warning); }
        SetSuccessIfMissing(Localize("Board resized to {0}x{1}; tile positions were preserved.", Rows, Columns));
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostTeamSizeAsync(Guid id, int expectedTeamSize, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (expectedTeamSize is < 1 or > 100) { SetStatus(Localize("Expected team size must be between 1 and 100."), UiMessageType.Warning); return RedirectToPage(new { id }); }
        var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct); if (board is null) return NotFound();
        if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id });
        var bingoEvent = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct); if (bingoEvent is null) return NotFound();
        var before = new { bingoEvent.ExpectedTeamSize };
        bingoEvent.SetExpectedTeamSize(expectedTeamSize);
        await WriteAudit("board.expected_team_size_changed", board, before, new { bingoEvent.ExpectedTeamSize }, ct); await transaction.CommitAsync(ct); await collaboration.NotifyBoardChangedAsync(id, ct); SetStatus(Localize("Expected team size updated."), UiMessageType.Success); return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPublishAsync(Guid id, bool confirmed, CancellationToken ct)
    {
        if (!confirmed)
        {
            ValidationIssues = [new("confirmation-required", null, null, null, "Confirmation required", [])];
            SetStatus(Localize("Confirmation required"), UiMessageType.Warning);
            return RedirectToPage(new { id });
        }
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct);
            var bingoEvent = await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
            var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
            if (board is null || bingoEvent is null || draft is null) { ValidationIssues = [new("board-not-found", null, null, null, "Board not found.", [])]; return NotFound(); }
            if (board.Version != BoardVersion) throw new DbUpdateConcurrencyException();
            if (draft.State != DraftState.Finalized || await db.ActiveRosterPublicationAsync(id, ct) is null)
                throw new BoardApprovalValidationException("roster-unpublished", null, null, null, "Finalize the team draft before publishing the board.");
            if (bingoEvent.ActualStartedAt is not null)
                throw new BoardApprovalValidationException("event-already-started", null, null, null, "The board must be published before the event has started and while its configured end remains in the future.");
            if (bingoEvent.EventEndsAt is not { } endsAt || time.GetUtcNow() >= endsAt)
                throw new BoardApprovalValidationException("event-end-passed", null, null, null, "The board must be published before the event has started and while its configured end remains in the future.");
            var publication = board.ActiveApprovalSnapshotId is { } approvalId ? await db.ApprovalObjectivesAsync(board.Id, approvalId, ct) : null;
            if (publication is null) throw new BoardApprovalValidationException("approval-unavailable", null, null, null, "The approved board is unavailable.");
            var priceIssues = new List<BoardValidationIssue>();
            foreach (var tile in publication.Tiles)
            {
                var requirementIds = publication.Requirements.Where(x => x.BoardTileId == tile.Id).Select(x => x.Id).ToHashSet();
                var missing = await db.ItemsWithoutEventOrCataloguePriceAsync(id, publication.Drops.Where(x => requirementIds.Contains(x.RequirementId)).Select(x => x.ItemIdSnapshot), ct);
                if (missing.Count > 0) priceIssues.Add(new("item-price-missing", tile.Id, tile.RowIndex * publication.Approval.Columns + tile.ColumnIndex, tile.NameSnapshot, MissingPriceMessage, [string.Join(", ", missing)]));
            }
            if (priceIssues.Count > 0)
            {
                ValidationIssues = priceIssues;
                var missing = await db.ItemsWithoutEventOrCataloguePriceAsync(id, publication.Drops.Select(x => x.ItemIdSnapshot), ct);
                SetStatus(Localize(MissingPriceMessage, string.Join(", ", missing)), UiMessageType.Warning);
                return RedirectToPage(new { id });
            }
            board.Publish(time.GetUtcNow());
            bingoEvent.SetBoardPublication(true, time.GetUtcNow());
            await db.RetainLuckOutcomeBasesAsync(id, time.GetUtcNow(), ct);
            AddBoardAudit("board.published", board, "Validated", $"Published approval snapshot {board.ActiveApprovalSnapshotId}",
                $"{{\"state\":\"Validated\",\"activeApprovalSnapshotId\":\"{board.ActiveApprovalSnapshotId}\"}}",
                $"{{\"state\":\"Published\",\"activeApprovalSnapshotId\":\"{board.ActiveApprovalSnapshotId}\"}}");
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            SetStatus(Localize("Board published from the approved snapshot."), UiMessageType.Success);
            await collaboration.NotifyBoardChangedAsync(id, ct);
        }
        catch (Exception exception) when (IsApprovalConflict(exception))
        {
            db.ChangeTracker.Clear();
            ValidationIssues = [new("publication-conflict", null, null, null, "Another administrator changed the board first. Reload before publishing.", [])];
            SetStatus(Localize("Another administrator changed the board first. Reload before publishing."), UiMessageType.Warning);
        }
        catch (InvalidOperationException exception)
        {
            db.ChangeTracker.Clear();
            if (exception is BoardApprovalValidationException validation) ValidationIssues = validation.Issues;
            else ValidationIssues = [new("publication-refused", null, null, null, "The board publication could not be completed.", [])];
            SetStatus(Localize("The board publication could not be completed."), UiMessageType.Warning);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            db.ChangeTracker.Clear();
            ValidationIssues = [new("publication-failed", null, null, null, "The board publication could not be completed.", [])];
            SetStatus(Localize("The board publication could not be completed."), UiMessageType.Warning);
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCorrectPublishedAsync(Guid id, bool confirmed, string? reason, CancellationToken ct)
    {
        if (!confirmed || string.IsNullOrWhiteSpace(reason))
        {
            SetStatus(Localize("Confirm the exceptional board correction and provide an Admin reason."), UiMessageType.Warning);
            return RedirectToPage(new { id });
        }
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct);
            var bingoEvent = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (board is null || bingoEvent is null) return NotFound();
            if (board.State != BoardState.Published || board.ActiveApprovalSnapshotId is null)
                throw new InvalidOperationException("Only a published board can be corrected here.");
            bingoEvent.EnsurePublishedBoardCorrectionAllowed();
            var previousSnapshotId = board.ActiveApprovalSnapshotId.Value;
            board.BeginPublishedCorrection();
            board.AcquireEditing(AdminId, time.GetUtcNow(), BoardEditingLease.Duration);
            await BoardEstimateService.RefreshStaleDraftTilesAsync(db, id, time.GetUtcNow(), ct);
            AddBoardAudit("board.published_correction_started", board, "Published", reason.Trim(),
                $"{{\"activeApprovalSnapshotId\":\"{previousSnapshotId}\"}}",
                $"{{\"activeApprovalSnapshotId\":\"{previousSnapshotId}\",\"workingCopy\":true,\"reason\":{System.Text.Json.JsonSerializer.Serialize(reason.Trim())}}}");
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            SetStatus(Localize("Published board correction started. The current public board remains live while you edit the private working copy."), UiMessageType.Success);
            await collaboration.NotifyBoardChangedAsync(id, ct);
        }
        catch (Exception exception) when (IsApprovalConflict(exception))
        {
            db.ChangeTracker.Clear();
            SetStatus(Localize("The board changed while the correction was being prepared. No correction was saved; reload and try again."), UiMessageType.Warning);
        }
        catch (InvalidOperationException)
        {
            db.ChangeTracker.Clear();
            SetStatus(Localize("The board correction could not be saved."), UiMessageType.Warning);
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDiscardCorrectionAsync(Guid id, bool confirmed, CancellationToken ct)
    {
        if (!confirmed)
        {
            SetStatus(Localize("Confirm discarding all unpublished board edits before continuing."), UiMessageType.Warning);
            return RedirectToPage(new { id });
        }
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var bingoEvent = await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {id} AND hidden_at IS NULL FOR UPDATE").SingleOrDefaultAsync(ct);
            var board = await db.Boards.FromSqlInterpolated($"SELECT * FROM boards WHERE event_id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
            if (board is null || bingoEvent is null) return NotFound();
            if (board.Version != BoardVersion)
            {
                SetStatus(Localize("This board changed after you opened it. Reload before discarding the correction."), UiMessageType.Warning);
                return RedirectToPage(new { id });
            }
            bingoEvent.EnsurePublishedBoardCorrectionAllowed();
            board.RequireEditing(AdminId, time.GetUtcNow());
            if (board.State != BoardState.Published || !board.PublishedCorrectionInProgress || board.ActiveApprovalSnapshotId is not { } approvalId)
                throw new InvalidOperationException("An open published correction is required.");
            var published = await db.ApprovalObjectivesAsync(board.Id, approvalId, ct)
                ?? throw new InvalidOperationException("The current publication cannot be restored.");
            await RestorePublishedWorkingCopyAsync(board, published, ct);
            board.DiscardPublishedCorrection(published.Approval);
            AddBoardAudit("board.published_correction_discarded", board, "Private correction", "Discarded all unpublished board edits after explicit confirmation.",
                $"{{\"activeApprovalSnapshotId\":\"{approvalId}\",\"workingCopy\":true}}",
                $"{{\"activeApprovalSnapshotId\":\"{approvalId}\",\"workingCopy\":false,\"confirmed\":true}}");
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception exception) when (IsApprovalConflict(exception) || exception is InvalidOperationException or DbUpdateException or IOException)
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            SetStatus(Localize("The private correction could not be discarded. No changes were saved. Reload and try again; if required published data or artwork is unavailable, contact an administrator."), UiMessageType.Warning);
            return RedirectToPage(new { id });
        }
        SetStatus(Localize("Private correction discarded. All unpublished board edits were removed and the working board was restored to the current publication."), UiMessageType.Success);
        await collaboration.NotifyBoardChangedAsync(id, ct);
        return RedirectToPage(new { id });
    }

    private async Task RestorePublishedWorkingCopyAsync(Board board, PublishedBoardData published, CancellationToken ct)
    {
        var approvalTiles = await db.BoardApprovalTileSnapshots.AsNoTracking().Where(x => x.ApprovalSnapshotId == published.Approval.Id).ToListAsync(ct);
        var approvalTileIds = approvalTiles.Select(x => x.Id).ToList();
        var approvalRequirements = await db.BoardApprovalRequirementSnapshots.AsNoTracking().Where(x => approvalTileIds.Contains(x.ApprovalTileSnapshotId)).ToListAsync(ct);
        var approvalRequirementIds = approvalRequirements.Select(x => x.Id).ToList();
        var bosses = await db.BoardApprovalRequirementBossSnapshots.AsNoTracking().Where(x => approvalRequirementIds.Contains(x.ApprovalRequirementSnapshotId)).ToListAsync(ct);
        var drops = await db.BoardApprovalRequirementDropSnapshots.AsNoTracking().Where(x => approvalRequirementIds.Contains(x.ApprovalRequirementSnapshotId)).ToListAsync(ct);
        var sourceIds = drops.Select(x => x.SourceDropId).ToList();
        var sourceBossIds = await db.SourceDrops.AsNoTracking().Where(x => sourceIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.BossActivityId, ct);
        var restoredTileIds = published.Tiles.Select(x => x.Id).ToList();
        var templateIds = published.Tiles.Select(x => x.TileTemplateId).ToList();
        var templates = await db.TileTemplates.Where(x => templateIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (templates.Count != templateIds.Count || await db.BoardTiles.AnyAsync(x => templateIds.Contains(x.TileTemplateId) && !restoredTileIds.Contains(x.Id), ct))
            throw new InvalidOperationException("A published template is missing or shared with another tile.");
        var tiles = await db.BoardTiles.Where(x => x.BoardId == board.Id || restoredTileIds.Contains(x.Id)).ToListAsync(ct);
        if (tiles.Any(x => x.BoardId != board.Id)) throw new InvalidOperationException("A published tile identity belongs to another board.");
        var workingTileIds = tiles.Select(x => x.Id).ToList();
        var restoredRequirementIds = published.Requirements.Select(x => x.Id).ToList();
        var requirements = await db.BoardRequirementSnapshots.Where(x => workingTileIds.Contains(x.BoardTileId) || restoredRequirementIds.Contains(x.Id)).ToListAsync(ct);
        if (requirements.Any(x => !workingTileIds.Contains(x.BoardTileId) && !restoredTileIds.Contains(x.BoardTileId)))
            throw new InvalidOperationException("A published objective identity belongs to another board.");
        var workingRequirementIds = requirements.Select(x => x.Id).ToList();
        var images = await db.BoardTileImageAssets.Where(x => workingTileIds.Contains(x.BoardTileId) || restoredTileIds.Contains(x.BoardTileId)).ToListAsync(ct);
        var restoredImages = new Dictionary<Guid, BoardTileImageAsset>();
        foreach (var tile in approvalTiles.Where(x => x.ArtworkReference != null))
        {
            var matches = images.Where(x => x.EventId == board.EventId && x.BoardTileId == tile.BoardTileId && x.StorageKey == tile.ArtworkReference).ToList();
            if (matches.Count != 1) throw new InvalidOperationException("Published artwork is missing or ambiguous.");
            try
            {
                await using var content = await storage.OpenReadAsync(matches[0].StorageKey, ct);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Amazon.Runtime.AmazonServiceException or HttpRequestException)
            {
                throw new InvalidOperationException("Required published artwork could not be read.", exception);
            }
            restoredImages.Add(tile.BoardTileId, matches[0]);
        }
        // Only private additions may be removed here. Retained historical artwork
        // must never be deleted to make a restoration fit the working rows.
        var extraImages = images.Where(x => !restoredTileIds.Contains(x.BoardTileId)).ToList();
        var extraKeys = extraImages.Select(x => x.StorageKey).ToList();
        if (await db.BoardApprovalTileSnapshots.AnyAsync(x => x.ArtworkReference != null && extraKeys.Contains(x.ArtworkReference), ct))
            throw new InvalidOperationException("A private tile still owns published artwork.");
        var manualEstimates = new Dictionary<Guid, decimal?>();
        foreach (var tile in approvalTiles)
        {
            var estimates = new List<decimal?>();
            foreach (var requirement in approvalRequirements.Where(x => x.ApprovalTileSnapshotId == tile.Id))
            {
                if (requirement.ManualObjective) { estimates.Add(null); continue; }
                var rates = new List<EligibleDropRate>();
                foreach (var drop in drops.Where(x => x.ApprovalRequirementSnapshotId == requirement.Id))
                {
                    if (!sourceBossIds.TryGetValue(drop.SourceDropId, out var bossId)) throw new InvalidOperationException("A published drop association is missing.");
                    var matches = bosses.Where(x => x.ApprovalRequirementSnapshotId == requirement.Id && x.BossActivityId == bossId).ToList();
                    if (matches.Count != 1) throw new InvalidOperationException("A published boss association is missing or ambiguous.");
                    rates.Add(new(matches[0].EfficientRate, drop.NumericProbability, drop.ItemIdSnapshot, bossId, drop.CreditedWeight, drop.RollsPerCompletion, drop.RollGroup));
                }
                if (rates.Count == 0) throw new InvalidOperationException("Published drop rules are missing.");
                estimates.Add(EhbCalculator.CalculateDropRequirement(requirement.TargetContribution, rates, requirement.DuplicatesAllowed));
            }
            if (estimates.Count == 0) throw new InvalidOperationException("Published objectives are missing.");
            // Approval stores the effective EHB, not whether an equal manual override
            // was entered. Restore that frozen value without consulting live rates.
            manualEstimates.Add(tile.BoardTileId, estimates.Any(x => x == null) || decimal.Round(EhbCalculator.SumRequirements(estimates), 4, MidpointRounding.AwayFromZero) != tile.EstimatedEhb ? tile.EstimatedEhb : null);
        }
        if (approvalTiles.Count != published.Approval.Rows * published.Approval.Columns || approvalTiles.Any(x => x.RowIndex < 0 || x.RowIndex >= published.Approval.Rows || x.ColumnIndex < 0 || x.ColumnIndex >= published.Approval.Columns))
            throw new InvalidOperationException("Published board positions are incomplete.");

        // Vacate occupied positions before restoring exact tile identities/positions.
        foreach (var tile in tiles) { tile.Move(-1, -tiles.IndexOf(tile) - 1); tile.SetActiveImageAsset(null); }
        db.BoardTileImageAssets.RemoveRange(extraImages);
        await db.SaveChangesAsync(ct);
        db.BoardTiles.RemoveRange(tiles.Where(x => !restoredTileIds.Contains(x.Id)));
        db.BoardRequirementSnapshots.RemoveRange(requirements.Where(x => !restoredRequirementIds.Contains(x.Id)));
        db.BoardRequirementBossSnapshots.RemoveRange(await db.BoardRequirementBossSnapshots.Where(x => workingRequirementIds.Contains(x.RequirementId) || restoredRequirementIds.Contains(x.RequirementId)).ToListAsync(ct));
        var retainedDropRequirements = await db.BoardApprovalRequirementSnapshots.Where(x => workingRequirementIds.Contains(x.BoardRequirementSnapshotId)).Select(x => x.BoardRequirementSnapshotId).ToListAsync(ct);
        db.BoardRequirementDropSnapshots.RemoveRange(await db.BoardRequirementDropSnapshots.Where(x => workingRequirementIds.Contains(x.RequirementId) && !retainedDropRequirements.Contains(x.RequirementId)).ToListAsync(ct));
        var templateRequirements = await db.TileTemplateRequirements.Where(x => templateIds.Contains(x.TileTemplateId)).ToListAsync(ct);
        var oldTemplateIds = templateRequirements.Select(x => x.Id).ToList();
        db.TemplateRequirementBosses.RemoveRange(await db.TemplateRequirementBosses.Where(x => oldTemplateIds.Contains(x.RequirementId)).ToListAsync(ct));
        db.TemplateRequirementDrops.RemoveRange(await db.TemplateRequirementDrops.Where(x => oldTemplateIds.Contains(x.RequirementId)).ToListAsync(ct));
        db.TileTemplateRequirements.RemoveRange(templateRequirements);
        await db.SaveChangesAsync(ct);
        foreach (var tile in published.Tiles)
        {
            var workingDescription = tile.DescriptionIsAutomatic ? string.Empty : tile.DescriptionSnapshot;
            var restored = tiles.SingleOrDefault(x => x.Id == tile.Id);
            if (restored is null) { restored = tile; db.BoardTiles.Add(restored); }
            else db.Entry(restored).CurrentValues.SetValues(tile);
            restored.UpdateContent(tile.NameSnapshot, workingDescription, tile.EvidenceInstructionsSnapshot,
                tile.EstimatedEhbSnapshot, tile.ImageUrlSnapshot, tile.DescriptionIsAutomatic);
            if (restoredImages.TryGetValue(tile.Id, out var image)) { image.Restore(); restored.SetActiveImageAsset(image.Id); }
            foreach (var imageToRetire in images.Where(x => x.BoardTileId == tile.Id && x.Id != restored.ActiveImageAssetId)) imageToRetire.Replace(time.GetUtcNow());
            var tileRequirements = published.Requirements.Where(x => x.BoardTileId == tile.Id).ToList();
            templates[tile.TileTemplateId].Update(tile.NameSnapshot, workingDescription, tileRequirements.All(x => x.ManualObjective) ? ObjectiveType.Manual : ObjectiveType.DropRequirements, tile.EvidenceInstructionsSnapshot, manualEstimates[tile.Id], descriptionIsAutomatic: tile.DescriptionIsAutomatic);
            foreach (var requirement in tileRequirements)
            {
                var restoredRequirement = requirements.SingleOrDefault(x => x.Id == requirement.Id);
                if (restoredRequirement is null) db.BoardRequirementSnapshots.Add(requirement);
                else db.Entry(restoredRequirement).CurrentValues.SetValues(requirement);
                var templateRequirement = new TileTemplateRequirement(Guid.NewGuid(), tile.TileTemplateId, requirement.Position, requirement.TargetContribution, requirement.DuplicatesAllowed, requirement.AllowHigherWeightings, requirement.Description, requirement.ManualObjective);
                db.TileTemplateRequirements.Add(templateRequirement);
                var approvalRequirement = approvalRequirements.Single(x => x.BoardRequirementSnapshotId == requirement.Id);
                foreach (var boss in bosses.Where(x => x.ApprovalRequirementSnapshotId == approvalRequirement.Id))
                {
                    db.BoardRequirementBossSnapshots.Add(new(Guid.NewGuid(), requirement.Id, boss.BossActivityId, boss.Name, boss.EfficientRate));
                    db.TemplateRequirementBosses.Add(new(Guid.NewGuid(), templateRequirement.Id, boss.BossActivityId));
                }
                foreach (var drop in published.Drops.Where(x => x.RequirementId == requirement.Id))
                {
                    var retained = await db.BoardRequirementDropSnapshots.SingleAsync(x => x.Id == drop.Id, ct);
                    db.Entry(retained).CurrentValues.SetValues(drop);
                    db.TemplateRequirementDrops.Add(new(Guid.NewGuid(), templateRequirement.Id, drop.SourceDropId, drop.MaximumContribution, drop.CreditedWeight));
                }
            }
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id, bool confirmed, CancellationToken ct)
    {
        var approved = false;
        var publishingCorrection = false;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var bingoEvent = await db.Events
                .FromSqlInterpolated($"SELECT * FROM events WHERE id = {id} AND hidden_at IS NULL FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            var board = await db.Boards.FromSqlInterpolated($"SELECT * FROM boards WHERE event_id = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
            if (board is null || bingoEvent is null) { ValidationIssues = [new("board-not-found", null, null, null, "Board not found.", [])]; return NotFound(); }
            if (board.Version != BoardVersion)
            {
                ValidationIssues = [new("board-stale", null, null, null, "This board changed after you opened it. Reload before approving it.", [])];
                SetStatus(Localize("This board changed after you opened it. Reload before approving it."), UiMessageType.Warning);
                return RedirectToPage(new { id });
            }
            try { board.RequireEditing(AdminId, time.GetUtcNow()); }
            catch (InvalidOperationException)
            {
                ValidationIssues = [new("editing-control-required", null, null, null, "The board approval could not be changed.", [])];
                throw;
            }

            publishingCorrection = board.State == BoardState.Published && board.PublishedCorrectionInProgress;
            if (publishingCorrection)
            {
                if (!confirmed)
                {
                    ValidationIssues = [new("confirmation-required", null, null, null, "Confirmation required", [])];
                    SetStatus(Localize("Confirmation required"), UiMessageType.Warning);
                    return RedirectToPage(new { id });
                }
                bingoEvent.EnsurePublishedBoardCorrectionAllowed();
            }
            var snapshot = await CreateApprovalSnapshotAsync(board, ct, publishingCorrection);
            if (publishingCorrection)
            {
                var newTileIds = db.BoardApprovalTileSnapshots.Local.Where(x => x.ApprovalSnapshotId == snapshot.Id).Select(x => x.Id).ToHashSet();
                var newRequirementIds = db.BoardApprovalRequirementSnapshots.Local.Where(x => newTileIds.Contains(x.ApprovalTileSnapshotId)).Select(x => x.Id).ToHashSet();
                var introducedItems = db.BoardApprovalRequirementDropSnapshots.Local
                    .Where(x => newRequirementIds.Contains(x.ApprovalRequirementSnapshotId)).Select(x => x.ItemIdSnapshot);
                await (itemPrices ?? new EventItemPriceService(db, time)).IntroduceAsync(bingoEvent, introducedItems, ct);
            }
            if (publishingCorrection)
                board.ReplacePublishedApproval(snapshot.Id);
            else
            {
                // Approval is the authoritative full recalculation boundary.
                // Refresh the working cache while the board is still Draft so
                // the migration/backfill flag is cleared without touching any
                // immutable approval/public rows.
                var workingTileIds = await db.BoardTiles
                    .Where(x => x.BoardId == board.Id)
                    .Select(x => x.Id)
                    .ToListAsync(ct);
                await BoardEstimateService.RefreshTilesAsync(db, workingTileIds, time.GetUtcNow(), ct);
                board.Approve(snapshot.Id);
            }
            bingoEvent.AdvanceStatsEvidenceRevision();
            db.BoardApprovalSnapshots.Add(snapshot);
            AddBoardAudit(publishingCorrection ? "board.published_corrected" : "board.approved", board,
                publishingCorrection ? "Published correction" : "Draft", $"Approved snapshot {snapshot.Version}",
                publishingCorrection
                    ? $"{{\"state\":\"Published\",\"activeApprovalSnapshotId\":\"{snapshot.SupersedesApprovalSnapshotId}\"}}"
                    : "{\"state\":\"Draft\",\"activeApprovalSnapshotId\":null}",
                publishingCorrection
                    ? $"{{\"state\":\"Published\",\"activeApprovalSnapshotId\":\"{snapshot.Id}\",\"approvalVersion\":{snapshot.Version}}}"
                    : $"{{\"state\":\"Validated\",\"activeApprovalSnapshotId\":\"{snapshot.Id}\",\"approvalVersion\":{snapshot.Version}}}");
            await db.SaveChangesAsync(ct);
            var completionPublication = await db.ApprovalObjectivesAsync(board.Id, snapshot.Id, ct)
                ?? throw new InvalidOperationException("The approved tile completion inputs are unavailable.");
            var completionTeamIds = await db.Teams.AsNoTracking()
                .Where(team => team.EventId == id && team.Active && team.FinalizedAt != null)
                .Select(team => team.Id)
                .ToListAsync(ct);
            await TileCompletionFactReconciler.ReconcileAsync(db, id, completionPublication, completionTeamIds, time.GetUtcNow(), ct);
            await db.SaveChangesAsync(ct);
            await db.RetainLuckOutcomeBasesAsync(id, time.GetUtcNow(), ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            approved = true;
        }
        catch (Exception exception) when (IsApprovalConflict(exception))
        {
            db.ChangeTracker.Clear();
            ValidationIssues = [new("approval-conflict", null, null, null, "The board or catalogue changed while approval was being prepared. No approval was saved; reload and try again.", [])];
            SetStatus(Localize("The board or catalogue changed while approval was being prepared. No approval was saved; reload and try again."), UiMessageType.Warning);
        }
        catch (BoardApprovalValidationException exception)
        {
            db.ChangeTracker.Clear();
            ValidationIssues = exception.Issues;
            SetStatus(Localize(exception.ResourceKey, exception.Arguments), UiMessageType.Warning);
        }
        catch (InvalidOperationException exception)
        {
            db.ChangeTracker.Clear();
            if (ValidationIssues.Count == 0) ValidationIssues = [new("approval-refused", null, null, null, "The board approval could not be changed.", [])];
            SetStatus(Localize(exception.Message is "Submitted evidence now references an objective changed or removed by this correction. The active publication has been preserved." or
                "A tile with submitted evidence cannot change its scoring. The active publication has been preserved." or
                "The active publication has unavailable objective identities. No replacement was published."
                ? exception.Message : "The board approval could not be changed."), UiMessageType.Warning);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            db.ChangeTracker.Clear();
            ValidationIssues = [new("approval-failed", null, null, null, "The board approval could not be changed.", [])];
            SetStatus(Localize("The board approval could not be changed."), UiMessageType.Error);
        }
        if (approved)
        {
            SetStatus(publishingCorrection
                ? Localize("Corrected board published as a replacement snapshot. The prior public snapshot remains in history.")
                : Localize("Board approved privately. Publication remains a separate later action."), UiMessageType.Success);
            try { await collaboration.NotifyBoardChangedAsync(id, ct); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                SetStatus(Localize("Board approval was saved, but the live update could not be sent. Reload to see the current board."), UiMessageType.Warning);
            }
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUnapproveAsync(Guid id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct);
            if (board is null) return NotFound();
            if (board.Version != BoardVersion)
            {
                SetStatus(Localize("This board changed after you opened it. Reload before changing its approval."), UiMessageType.Warning);
                return RedirectToPage(new { id });
            }
            board.RequireEditing(AdminId, time.GetUtcNow());
            var priorApprovalId = board.ActiveApprovalSnapshotId;
            board.Unapprove();
            AddBoardAudit("board.unapproved", board, "Validated", "Returned to live draft derivation",
                $"{{\"state\":\"Validated\",\"activeApprovalSnapshotId\":\"{priorApprovalId}\"}}",
                "{\"state\":\"Draft\",\"activeApprovalSnapshotId\":null}");
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            await collaboration.NotifyBoardChangedAsync(id, ct);
            SetStatus(Localize("Board approval was removed. The preserved approval remains in history."), UiMessageType.Success);
        }
        catch (Exception exception) when (IsApprovalConflict(exception))
        {
            db.ChangeTracker.Clear();
            SetStatus(Localize("Another administrator changed this board first. No approval change was saved; reload and try again."), UiMessageType.Warning);
        }
        catch (InvalidOperationException)
        {
            db.ChangeTracker.Clear();
            SetStatus(Localize("The board approval could not be changed."), UiMessageType.Warning);
        }
        return RedirectToPage(new { id });
    }

    private async Task<BoardApprovalSnapshot> CreateApprovalSnapshotAsync(Board board, CancellationToken ct, bool allowPublished = false)
    {
        BoardApprovalValidationException Invalid(string code, string message, BoardTile? tile = null, params object[] arguments)
            => new(code, tile?.Id, tile is null ? null : tile.RowIndex * board.Columns + tile.ColumnIndex, tile?.NameSnapshot, message, arguments);
        if (board.State != BoardState.Draft && !(allowPublished && board.State == BoardState.Published && board.PublishedCorrectionInProgress))
            throw Invalid("board-state", "Only an unapproved private board can be approved.");

        var issues = new List<BoardValidationIssue>();
        var tiles = await db.BoardTiles.Where(x => x.BoardId == board.Id).OrderBy(x => x.RowIndex).ThenBy(x => x.ColumnIndex).ToListAsync(ct);
        for (var position = 0; position < board.Rows * board.Columns; position++)
            if (!tiles.Any(x => x.RowIndex * board.Columns + x.ColumnIndex == position))
                issues.Add(new("board-incomplete", null, position, null, "Fill every board position before approving the board.", []));
        foreach (var duplicate in tiles.GroupBy(x => (x.RowIndex, x.ColumnIndex)).Where(x => x.Count() > 1).SelectMany(x => x))
            issues.Add(Invalid("board-positions", "The board has conflicting tile positions. Reload and correct the layout before approving it.", duplicate).Issue);

        var tileIds = tiles.Select(x => x.Id).ToList();
        var templateIds = tiles.Select(x => x.TileTemplateId).Distinct().ToList();
        var templates = await db.TileTemplates.Where(x => templateIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        foreach (var tile in tiles.Where(x => !templates.ContainsKey(x.TileTemplateId)))
            issues.Add(Invalid("tile-definition", "A tile definition is no longer available. Reload the board before approval.", tile).Issue);

        var requirements = await CurrentRequirementsAsync(tiles, board.Columns, ct, issues);
        foreach (var tile in tiles.Where(tile => requirements.All(requirement => requirement.BoardTileId != tile.Id) && !issues.Any(x => x.TileId == tile.Id && x.Code == "objective-positions")))
            issues.Add(Invalid("objective-missing", "Every board tile needs at least one objective before approval.", tile).Issue);

        var requirementIds = requirements.Select(x => x.Id).ToList();
        var requirementBosses = await db.BoardRequirementBossSnapshots.Where(x => requirementIds.Contains(x.RequirementId)).ToListAsync(ct);
        var requirementDrops = await db.BoardRequirementDropSnapshots.Where(x => requirementIds.Contains(x.RequirementId)).ToListAsync(ct);
        foreach (var tile in tiles)
        {
            var ids = requirements.Where(x => x.BoardTileId == tile.Id).Select(x => x.Id).ToHashSet();
            var missingPrices = await db.ItemsWithoutEventOrCataloguePriceAsync(board.EventId, requirementDrops.Where(x => ids.Contains(x.RequirementId)).Select(x => x.ItemIdSnapshot), ct);
            if (missingPrices.Count > 0) issues.Add(Invalid("item-price-missing", MissingPriceMessage, tile, string.Join(", ", missingPrices)).Issue);
        }
        // Keep the current page's historical board-wide price message while issues target each tile.
        var missingBoardPrices = issues.Any(x => x.Code == "item-price-missing")
            ? await db.ItemsWithoutEventOrCataloguePriceAsync(board.EventId, requirementDrops.Select(x => x.ItemIdSnapshot), ct) : [];
        BoardApprovalValidationException CollectedIssues() => new(issues,
            issues[0].Code == "item-price-missing" ? [string.Join(", ", missingBoardPrices)] : null);
        var evidenced = await db.EvidencedObjectiveIdsAsync(board.EventId, ct);
        var prior = allowPublished && board.ActiveApprovalSnapshotId is { } activeId
            ? await db.ApprovalObjectivesAsync(board.Id, activeId, ct) : null;
        if (allowPublished && prior is null) throw new InvalidOperationException("The active publication has unavailable objective identities. No replacement was published.");
        if (prior is not null)
        {
            foreach (var requirementId in evidenced)
            {
                var old = prior.Requirements.SingleOrDefault(x => x.Id == requirementId);
                var current = requirements.SingleOrDefault(x => x.Id == requirementId);
                if (old is null || current is null || old.BoardTileId != current.BoardTileId ||
                    !old.HasSameRules(current.TargetContribution, current.DuplicatesAllowed, current.AllowHigherWeightings, current.ManualObjective, current.CreditedWeight) ||
                    !SameDropRules(prior.Drops.Where(x => x.RequirementId == requirementId), requirementDrops.Where(x => x.RequirementId == requirementId)))
                    throw Invalid("evidence-objective-locked", "Submitted evidence now references an objective changed or removed by this correction. The active publication has been preserved.", tiles.FirstOrDefault(x => x.Id == old?.BoardTileId));
            }
        }
        if (prior is not null)
        {
            foreach (var current in requirements)
            {
                var old = prior.Requirements.SingleOrDefault(x => x.Id == current.Id);
                if (old is not null && (old.BoardTileId != current.BoardTileId ||
                    !old.HasSameRules(current.TargetContribution, current.DuplicatesAllowed, current.AllowHigherWeightings, current.ManualObjective, current.CreditedWeight) ||
                    !SameDropRules(prior.Drops.Where(x => x.RequirementId == current.Id), requirementDrops.Where(x => x.RequirementId == current.Id))))
                    throw new InvalidOperationException("An approved objective identity cannot be reused for different rules.");
            }
        }
        var priorRequirementIds = prior?.Requirements.Select(x => x.Id).ToHashSet() ?? [];
        var priorApprovalRequirements = prior is null ? [] : await db.BoardApprovalRequirementSnapshots.Where(x => priorRequirementIds.Contains(x.BoardRequirementSnapshotId))
            .Join(db.BoardApprovalTileSnapshots.Where(x => x.ApprovalSnapshotId == prior.Approval.Id), x => x.ApprovalTileSnapshotId, x => x.Id, (x, _) => x).ToListAsync(ct);
        var priorSnapshotIds = priorApprovalRequirements.Select(x => x.Id).ToList();
        var priorBosses = await db.BoardApprovalRequirementBossSnapshots.Where(x => priorSnapshotIds.Contains(x.ApprovalRequirementSnapshotId)).ToListAsync(ct);
        var priorDrops = await db.BoardApprovalRequirementDropSnapshots.Where(x => priorSnapshotIds.Contains(x.ApprovalRequirementSnapshotId)).ToListAsync(ct);
        foreach (var locked in priorApprovalRequirements.Where(x => requirementIds.Contains(x.BoardRequirementSnapshotId)))
        {
            if (!priorBosses.Where(x => x.ApprovalRequirementSnapshotId == locked.Id).Select(x => x.BossActivityId).ToHashSet()
                .SetEquals(requirementBosses.Where(x => x.RequirementId == locked.BoardRequirementSnapshotId).Select(x => x.BossActivityId)))
                throw new InvalidOperationException("An objective with submitted evidence cannot change its eligible sources.");
        }
        var requiredCurrentBossIds = requirementBosses.Where(x => !priorRequirementIds.Contains(x.RequirementId)).Select(x => x.BossActivityId).Distinct().ToArray();
        var bossIds = requirementBosses.Select(x => x.BossActivityId).Distinct().ToArray();
        var currentBosses = await db.BossActivities
            .FromSqlInterpolated($"SELECT * FROM boss_activities WHERE id = ANY({bossIds}) ORDER BY id FOR SHARE")
            .AsNoTracking().ToDictionaryAsync(x => x.Id, ct);
        foreach (var tile in tiles.Where(tile => requirements.Any(r => r.BoardTileId == tile.Id && requirementBosses.Any(b => b.RequirementId == r.Id && requiredCurrentBossIds.Contains(b.BossActivityId) && (!currentBosses.TryGetValue(b.BossActivityId, out var boss) || !boss.Active)))))
            issues.Add(Invalid("source-inactive", "A referenced boss or activity is no longer active. Correct the board before approval.", tile).Issue);

        var requiredCurrentDropIds = requirementDrops.Where(x => !priorRequirementIds.Contains(x.RequirementId)).Select(x => x.SourceDropId).Distinct().ToArray();
        var dropIds = requirementDrops.Select(x => x.SourceDropId).Distinct().ToArray();
        // Lock all referenced catalogue rows until the approval transaction commits.
        // Serializable detects changes since its snapshot; SHARE prevents later writes.
        var lockedDrops = db.SourceDrops.FromSqlInterpolated($"""
            SELECT d.* FROM source_drops AS d
            JOIN boss_activities AS b ON d.boss_activity_id = b.id
            JOIN catalogue_items AS i ON d.item_id = i.id
            WHERE d.id = ANY({dropIds}) ORDER BY d.id FOR SHARE OF d, b, i
            """).AsNoTracking();
        var currentDrops = await (from drop in lockedDrops
                                  join boss in db.BossActivities.AsNoTracking() on drop.BossActivityId equals boss.Id
                                  join item in db.CatalogueItems.AsNoTracking() on drop.ItemId equals item.Id
                                  where dropIds.Contains(drop.Id)
                                  select new ApprovalLiveDrop(drop, boss, item)).ToDictionaryAsync(x => x.Drop.Id, ct);
        foreach (var tile in tiles.Where(tile => requirements.Any(r => r.BoardTileId == tile.Id && requirementDrops.Any(d => d.RequirementId == r.Id && requiredCurrentDropIds.Contains(d.SourceDropId) && (!currentDrops.TryGetValue(d.SourceDropId, out var drop) || !drop.Drop.Active || !drop.Boss.Active || !drop.Item.Active)))))
            issues.Add(Invalid("drop-inactive", "A referenced catalogue drop is no longer active. Correct the board before approval.", tile).Issue);

        if (!string.Equals(ApprovalCatalogueFingerprint, CatalogueFingerprint(currentBosses.Values, currentDrops.Values), StringComparison.Ordinal))
            throw Invalid("catalogue-stale", "The catalogue changed after you opened this board. Reload and review the current values before approving it.");

        var images = await db.BoardTileImageAssets.Where(x => tileIds.Contains(x.BoardTileId) && x.ReplacedAt == null).ToListAsync(ct);
        var imagesById = images.ToDictionary(x => x.Id);
        var nextVersion = (await db.BoardApprovalSnapshots.Where(x => x.BoardId == board.Id).Select(x => (int?)x.Version).MaxAsync(ct) ?? 0) + 1;
        var supersedes = await db.BoardApprovalSnapshots.Where(x => x.BoardId == board.Id).OrderByDescending(x => x.Version).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        var approval = new BoardApprovalSnapshot(Guid.NewGuid(), board.Id, nextVersion, time.GetUtcNow(), AdminId, supersedes, board.Name, board.Rows, board.Columns, 0m, board.CalculationVersion, board.Version, allowPublished ? BoardState.Published : BoardState.Validated);
        var totalEhb = 0m;

        foreach (var tile in tiles)
        {
            if (issues.Any(x => x.TileId == tile.Id && x.Code is "tile-definition" or "objective-missing" or "objective-positions" or "source-inactive" or "drop-inactive")) continue;
            try
            {
            string? artwork = null;
            if (tile.ActiveImageAssetId is { } imageId)
            {
                if (!imagesById.TryGetValue(imageId, out var image) || image.EventId != board.EventId || image.BoardTileId != tile.Id)
                    throw Invalid("artwork-invalid", "A managed tile image no longer belongs to this event. Correct the tile before approval.", tile);
                artwork = image.StorageKey;
            }
            var tileRequirements = requirements.Where(x => x.BoardTileId == tile.Id).OrderBy(x => x.Position).ToList();
            if (tileRequirements.Select(x => x.ManualObjective).Distinct().Count() > 1)
                throw Invalid("objective-mixed", "Use separate tiles for catalogue drops and custom challenges. Every objective in a tile must have the same kind.", tile);
            var template = templates[tile.TileTemplateId];
            if (template.DescriptionIsAutomatic != tile.DescriptionIsAutomatic)
                throw Invalid("description-mode", "The tile description mode changed. Edit and save the tile before approval.", tile);
            var description = tile.DescriptionSnapshot;
            if (tile.DescriptionIsAutomatic)
            {
                var descriptionRequirements = tileRequirements.Select(requirement =>
                {
                    var selectedDrops = priorRequirementIds.Contains(requirement.Id)
                        ? prior!.Drops.Where(drop => drop.RequirementId == requirement.Id)
                            .Select(drop => new TileDescriptionDrop(drop.ItemIdSnapshot, drop.ItemName, drop.BossName)).ToList()
                        : requirementDrops.Where(drop => drop.RequirementId == requirement.Id)
                            .Select(drop => currentDrops.TryGetValue(drop.SourceDropId, out var selectedDrop)
                                ? new TileDescriptionDrop(drop.ItemIdSnapshot, selectedDrop.Item.Name, selectedDrop.Boss.Name) : null)
                            .Where(value => value is not null).Select(value => value!).ToList();
                    return new TileDescriptionRequirement(requirement.Position, requirement.TargetContribution,
                        requirement.ManualObjective, requirement.Description, selectedDrops);
                });
                description = ApprovalDescription(tile.DescriptionIsAutomatic, tile.DescriptionSnapshot, descriptionRequirements);
                if (description.Length > TileDescriptionFormatter.MaximumFrozenDescriptionLength)
                    throw Invalid("description-length", "{0} has an automatic description that is too long. Reduce the selected sources or objective count before approval.", tile, tile.NameSnapshot);
            }
            var approvalTile = new BoardApprovalTileSnapshot(Guid.NewGuid(), approval.Id, tile.Id, tile.TileTemplateId, tile.RowIndex, tile.ColumnIndex, tile.NameSnapshot, description, string.Empty, 0m, artwork, tile.DescriptionIsAutomatic);
            db.BoardApprovalTileSnapshots.Add(approvalTile);
            var manualTile = tileRequirements.All(x => x.ManualObjective);
            if ((template.ObjectiveType == ObjectiveType.Manual) != manualTile)
                throw Invalid("objective-kind", "The tile kind does not match its objectives. Edit and save the tile with one objective kind before approval.", tile);
            var tileEstimates = new List<decimal?>();
            foreach (var requirement in tileRequirements)
            {
                var approvalRequirement = new BoardApprovalRequirementSnapshot(Guid.NewGuid(), approvalTile.Id, requirement.Id, requirement.Position, requirement.TargetContribution, requirement.DuplicatesAllowed, requirement.AllowHigherWeightings, requirement.CreditedWeight, requirement.Description, requirement.ManualObjective);
                db.BoardApprovalRequirementSnapshots.Add(approvalRequirement);
                if (priorRequirementIds.Contains(requirement.Id))
                {
                    var previous = priorApprovalRequirements.Single(x => x.BoardRequirementSnapshotId == requirement.Id);
                    var bosses = priorBosses.Where(x => x.ApprovalRequirementSnapshotId == previous.Id).ToList();
                    var drops = priorDrops.Where(x => x.ApprovalRequirementSnapshotId == previous.Id).ToList();
                    foreach (var boss in bosses) db.BoardApprovalRequirementBossSnapshots.Add(new(Guid.NewGuid(), approvalRequirement.Id, boss.BossActivityId, boss.Name, boss.EfficientRate, boss.CatalogueVersion));
                    foreach (var drop in drops) db.BoardApprovalRequirementDropSnapshots.Add(new(Guid.NewGuid(), approvalRequirement.Id, drop.SourceDropId, drop.ItemIdSnapshot, drop.BossName, drop.ItemName, drop.DisplayRate, drop.NumericProbability, drop.MaximumContribution, drop.EhbPerContribution, drop.CreditedWeight, drop.CatalogueVersion, drop.ProbabilityScope, drop.ConditionalOnParent, drop.ParentProbability, drop.AssumedParticipants, drop.RollsPerCompletion, drop.RollGroup, drop.RateCondition));
                    var identityBosses = await db.SourceDrops.AsNoTracking().Where(x => drops.Select(d => d.SourceDropId).Contains(x.Id)).Select(x => new { x.Id, x.BossActivityId }).ToDictionaryAsync(x => x.Id, ct);
                    tileEstimates.Add(requirement.ManualObjective ? null : EhbCalculator.CalculateDropRequirement(requirement.TargetContribution, drops.Select(drop =>
                    {
                        var boss = bosses.Single(x => x.BossActivityId == identityBosses[drop.SourceDropId].BossActivityId);
                        return new EligibleDropRate(boss.EfficientRate, drop.NumericProbability, drop.ItemIdSnapshot, boss.BossActivityId, drop.CreditedWeight, drop.RollsPerCompletion, drop.RollGroup);
                    }), requirement.DuplicatesAllowed));
                    continue;
                }
                foreach (var requirementBoss in requirementBosses.Where(x => x.RequirementId == requirement.Id))
                {
                    var currentBoss = currentBosses[requirementBoss.BossActivityId];
                    db.BoardApprovalRequirementBossSnapshots.Add(new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), approvalRequirement.Id, currentBoss.Id, currentBoss.Name, currentBoss.EfficientCompletionsPerHour, currentBoss.Version));
                }

                var frozenDrops = requirementDrops.Where(x => x.RequirementId == requirement.Id).ToList();
                if (!requirement.ManualObjective && !requirement.DuplicatesAllowed) EnsureConsistentItemCaps(frozenDrops, tile, board.Columns);
                var selectedDrops = frozenDrops.Select(x => currentDrops[x.SourceDropId]).ToList();
                if (!requirement.ManualObjective && selectedDrops.Count == 0)
                    throw Invalid("drop-missing", "Every catalogue objective needs an active eligible drop before approval.", tile);
                foreach (var selected in selectedDrops)
                {
                    var sourceSnapshot = requirementDrops.Single(x => x.RequirementId == requirement.Id && x.SourceDropId == selected.Drop.Id);
                    if (selected.Drop.ItemId != sourceSnapshot.ItemIdSnapshot)
                        throw Invalid("item-identity", "A catalogue item identity changed. Reselect the affected drop in the tile before approving the board.", tile);
                    var approvalDrop = new BoardApprovalRequirementDropSnapshot(Guid.NewGuid(), approvalRequirement.Id, selected.Drop.Id, sourceSnapshot.ItemIdSnapshot, selected.Boss.Name, selected.Item.Name, selected.Drop.DisplayRate, selected.Drop.NumericProbability, sourceSnapshot.MaximumContribution, selected.Drop.DefaultEhbEstimate, sourceSnapshot.CreditedWeight, selected.Drop.Version, selected.Drop.ProbabilityScope, selected.Drop.ConditionalOnParent, selected.Drop.ParentProbability, selected.Drop.AssumedParticipants, selected.Drop.RollsPerCompletion, selected.Drop.RollGroup, selected.Drop.RateConditionNote);
                    db.BoardApprovalRequirementDropSnapshots.Add(approvalDrop);
                }

                tileEstimates.Add(requirement.ManualObjective
                    ? null
                    : EhbCalculator.CalculateDropRequirement(requirement.TargetContribution, selectedDrops.Select(drop =>
                    {
                        var snapshot = requirementDrops.Single(x => x.RequirementId == requirement.Id && x.SourceDropId == drop.Drop.Id);
                        return new EligibleDropRate(drop.Boss.EfficientCompletionsPerHour, drop.Drop.NumericProbability, drop.Drop.ItemId, drop.Boss.Id, snapshot.CreditedWeight, drop.Drop.RollsPerCompletion, drop.Drop.RollGroup);
                    }), requirement.DuplicatesAllowed));
            }
            var tileEhb = ApprovalTileEhb(template, tileRequirements.Select(x => x.ManualObjective).Zip(tileEstimates));
            if (prior is not null && requirements.Any(x => x.BoardTileId == tile.Id && evidenced.Contains(x.Id)))
            {
                var priorTile = prior.Tiles.Single(x => x.Id == tile.Id);
                if (decimal.Round(tileEhb, 4, MidpointRounding.AwayFromZero) != priorTile.EstimatedEhbSnapshot ||
                    requirements.Where(x => x.BoardTileId == tile.Id).Sum(x => (long)x.TargetContribution) !=
                    prior.Requirements.Where(x => x.BoardTileId == tile.Id).Sum(x => (long)x.TargetContribution))
                    throw Invalid("evidence-scoring-locked", "A tile with submitted evidence cannot change its scoring. The active publication has been preserved.", tile);
                tileEhb = priorTile.EstimatedEhbSnapshot;
            }
            if (tileEhb <= 0)
                throw Invalid(manualTile ? "manual-ehb-missing" : "catalogue-rates-missing", manualTile
                    ? "{0} needs a positive manual EHB estimate. Edit the custom tile before approval."
                    : "{0} needs automatic EHB. Correct the catalogue rates or drop requirements before approval; a manual estimate cannot replace them.", tile, tile.NameSnapshot);
            db.Entry(approvalTile).Property(x => x.EstimatedEhb).CurrentValue = tileEhb;
            totalEhb += tileEhb;
            }
            catch (BoardApprovalValidationException exception) { issues.AddRange(exception.Issues); }
        }
        if (issues.Count > 0) throw CollectedIssues();

        db.Entry(approval).Property(x => x.TotalEhbEstimate).CurrentValue = totalEhb;
        board.SetTotalEhb(totalEhb);
        return approval;
    }

    private const string MissingPriceMessage = "These drops have no catalogue GP value: {0}. Set a value in Admin Catalogue, then try again. An explicit 0 is valid.";

    private async Task<bool> TryEnsureSelectedDropPricesAsync(Guid eventId, CancellationToken ct, HashSet<Guid>? retainedRequirements = null, IEnumerable<Guid>? retainedItems = null)
    {
        // All newly selected references lock activities before drops, in ID order.
        // Catalogue deletion holds an exclusive row lock until its dependency
        // recheck and mutation commit. A deleted selection must never be written.
        var bossIds = TileDraft.Requirements.SelectMany(x => x.BossIds).Distinct().ToArray();
        var bosses = await db.BossActivities.FromSqlInterpolated($"SELECT * FROM boss_activities WHERE id = ANY({bossIds}) ORDER BY id FOR SHARE")
            .AsNoTracking().ToListAsync(ct);
        if (bosses.Count != bossIds.Length) throw new DbUpdateConcurrencyException("A selected activity is no longer available.");
        var dropIds = TileDraft.Requirements.Where(x => x.RequirementId is not { } id || retainedRequirements?.Contains(id) != true).SelectMany(x => x.DropIds).Distinct().ToArray();
        var itemIds = await db.SourceDrops.FromSqlInterpolated($"SELECT * FROM source_drops WHERE id = ANY({dropIds}) ORDER BY id FOR SHARE")
            .AsNoTracking().Select(x => x.ItemId).ToListAsync(ct);
        if (itemIds.Count != dropIds.Length) throw new DbUpdateConcurrencyException("A selected drop is no longer available.");
        return await TryEnsureItemPricesAsync(eventId, itemIds.Concat(retainedItems ?? []), ct);
    }

    private async Task<bool> TryEnsureItemPricesAsync(Guid eventId, IEnumerable<Guid> itemIds, CancellationToken ct)
    {
        var missing = await db.ItemsWithoutEventOrCataloguePriceAsync(eventId, itemIds, ct);
        if (missing.Count == 0) return true;
        ValidationIssues = [new("item-price-missing", null, null, null, MissingPriceMessage, [string.Join(", ", missing)])];
        SetStatus(Localize(MissingPriceMessage, string.Join(", ", missing)), UiMessageType.Warning);
        return false;
    }

    private bool PrepareCompetitiveEdit(Board board, bool reportStatus = true)
    {
        if (board.State == BoardState.Draft || board.State == BoardState.Published && board.PublishedCorrectionInProgress) return true;
        if (board.State != BoardState.Validated)
        {
            if (reportStatus) SetStatus(Localize("This board can no longer be changed here."), UiMessageType.Warning);
            return false;
        }
        var priorApprovalId = board.ActiveApprovalSnapshotId;
        board.Unapprove();
        AddBoardAudit("board.auto_unapproved", board, "Validated", "Competitive board content changed",
            $"{{\"state\":\"Validated\",\"activeApprovalSnapshotId\":\"{priorApprovalId}\"}}",
            "{\"state\":\"Draft\",\"activeApprovalSnapshotId\":null}");
        if (reportStatus) SetStatus(Localize("Board returned to Draft because a competitive edit was saved. The preserved approval remains in history."), UiMessageType.Success);
        return true;
    }

    private static bool SameDropRules(IEnumerable<BoardRequirementDropSnapshot> old, IEnumerable<BoardRequirementDropSnapshot> current)
        => old.Select(x => (x.Id, x.SourceDropId, x.ItemIdSnapshot, x.MaximumContribution, x.CreditedWeight)).ToHashSet()
            .SetEquals(current.Select(x => (x.Id, x.SourceDropId, x.ItemIdSnapshot, x.MaximumContribution, x.CreditedWeight)));

    private static bool HasTemplateRules(BoardRequirementSnapshot requirement, TileTemplateRequirement template)
        => requirement.Position == template.Position &&
           requirement.TargetContribution == template.TargetContribution &&
           requirement.DuplicatesAllowed == template.DuplicatesAllowed &&
           requirement.AllowHigherWeightings == template.AllowHigherWeightings &&
           requirement.CreditedWeight == template.CreditedWeight &&
           requirement.Description == template.Description &&
           requirement.ManualObjective == template.ManualObjective;

    private static void EnsureConsistentItemCaps(IEnumerable<BoardRequirementDropSnapshot> drops, BoardTile? tile = null, int columns = 1)
    {
        foreach (var itemDrops in drops.GroupBy(x => x.ItemIdSnapshot))
        {
            var caps = itemDrops.Select(x => x.MaximumContribution ?? 1).Distinct().ToList();
            if (caps.Count != 1) throw new BoardApprovalValidationException("item-caps", tile?.Id, tile is null ? null : tile.RowIndex * columns + tile.ColumnIndex, tile?.NameSnapshot, "A catalogue item has inconsistent contribution limits across its sources. Correct the requirement before approval.");
        }
    }

    private void AddBoardAudit(string action, Board board, string details, string description, string? beforeState, string? afterState) =>
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), time.GetUtcNow(), AdminId, User.Identity?.Name ?? "Admin", action, "board", board.Id.ToString(), description, board.EventId, beforeState, afterState));

    private static bool IsApprovalConflict(Exception exception)
        => exception is DbUpdateConcurrencyException or PostgresException { SqlState: "40001" or "40P01" }
            || exception.InnerException is { } inner && IsApprovalConflict(inner);

    public async Task<IActionResult> OnGetTileImageAsync(Guid id, Guid tileId, CancellationToken ct, Guid? approvalId = null)
    {
        if (approvalId is { } retainedApprovalId)
        {
            var retained = await (from board in db.Boards.AsNoTracking()
                                  join approval in db.BoardApprovalSnapshots.AsNoTracking() on board.Id equals approval.BoardId
                                  join tile in db.BoardApprovalTileSnapshots.AsNoTracking() on approval.Id equals tile.ApprovalSnapshotId
                                  join image in db.BoardTileImageAssets.AsNoTracking() on tile.BoardTileId equals image.BoardTileId
                                  where board.EventId == id && approval.Id == retainedApprovalId && tile.BoardTileId == tileId &&
                                        image.EventId == id && tile.ArtworkReference == image.StorageKey
                                  select image).SingleOrDefaultAsync(ct);
            if (retained is null) return NotFound();
            try { return File(await storage.OpenReadAsync(retained.StorageKey, ct), retained.MediaType); }
            catch (FileNotFoundException) { return NotFound(); }
        }
        var asset = await (from tile in db.BoardTiles.AsNoTracking()
                           join board in db.Boards.AsNoTracking() on tile.BoardId equals board.Id
                           join image in db.BoardTileImageAssets.AsNoTracking() on tile.ActiveImageAssetId equals image.Id
                           where board.EventId == id && tile.Id == tileId && image.EventId == id && image.BoardTileId == tile.Id && image.ReplacedAt == null
                           select image).SingleOrDefaultAsync(ct);
        if (asset is null) return NotFound();
        return File(await storage.OpenReadAsync(asset.StorageKey, ct), asset.MediaType);
    }

    public async Task<IActionResult> OnPostRemoveAsync(Guid id, Guid tileId, CancellationToken ct)
    {
        TempData["BoardTileOutcome"] = "failed";
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var committed = false;
        try
        {
            if (await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {id} AND hidden_at IS NULL FOR UPDATE").SingleOrDefaultAsync(ct) is null) return NotFound();
            var board = await db.Boards.FromSqlInterpolated($"SELECT * FROM boards WHERE event_id = {id} FOR UPDATE").SingleAsync(ct); if (!board.IsEditable) { SetStatus(Localize("This board is no longer editable."), UiMessageType.Warning); return RedirectToPage(new { id }); }
            if (!await TryEnsurePublishedCorrectionLifecycleAsync(board, id, ct)) { SetStatus(Localize("This event is read-only in its current lifecycle state."), UiMessageType.Warning); return RedirectToPage(new { id }); }
            var boardBefore = BoardAuditState(board);
            if (!PrepareCompetitiveEdit(board)) return RedirectToPage(new { id }); if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id });
            var tile = await db.BoardTiles.SingleOrDefaultAsync(x => x.BoardId == board.Id && x.Id == tileId, ct); if (tile is null) return NotFound();
            var before = new { board = boardBefore, tile = await TileAuditStateAsync(tile, ct) };
            var requirements = await db.BoardRequirementSnapshots.Where(x => x.BoardTileId == tile.Id).ToListAsync(ct); var ids = requirements.Select(x => x.Id).ToList();
            var evidenced = await db.EvidencedObjectiveIdsAsync(id, ct);
            if (ids.Any(evidenced.Contains) || await db.Submissions.AnyAsync(x => x.EventId == id && x.BoardTileId == tileId, ct))
                throw new InvalidOperationException("A tile with submitted evidence cannot be removed.");
            var retainedIds = await db.BoardApprovalRequirementSnapshots
                .Where(x => ids.Contains(x.BoardRequirementSnapshotId))
                .Select(x => x.BoardRequirementSnapshotId)
                .ToHashSetAsync(ct);
            var images = await db.BoardTileImageAssets.Where(x => x.EventId == id && x.BoardTileId == tile.Id).ToListAsync(ct);
            var retainedKeys = await (from approval in db.BoardApprovalSnapshots
                                      join snapshotTile in db.BoardApprovalTileSnapshots on approval.Id equals snapshotTile.ApprovalSnapshotId
                                      where approval.BoardId == board.Id && snapshotTile.BoardTileId == tile.Id && snapshotTile.ArtworkReference != null
                                      select snapshotTile.ArtworkReference).ToHashSetAsync(ct);
            var removedImages = images.Where(image => !retainedKeys.Contains(image.StorageKey)).ToList();
            foreach (var image in images.Except(removedImages).Where(image => image.ReplacedAt is null)) image.Replace(time.GetUtcNow());
            // Break the working tile/image reference before removing only unreferenced images.
            if (tile.ActiveImageAssetId is not null) { tile.SetActiveImageAsset(null); await db.SaveChangesAsync(ct); }
            db.BoardRequirementBossSnapshots.RemoveRange(await db.BoardRequirementBossSnapshots.Where(x => ids.Contains(x.RequirementId)).ToListAsync(ct)); db.BoardRequirementDropSnapshots.RemoveRange(await db.BoardRequirementDropSnapshots.Where(x => ids.Contains(x.RequirementId) && !retainedIds.Contains(x.RequirementId)).ToListAsync(ct)); db.BoardRequirementSnapshots.RemoveRange(requirements); db.BoardTileImageAssets.RemoveRange(removedImages); db.BoardTiles.Remove(tile);
            board.SetTotalEhb(Math.Max(0, board.TotalEhbEstimate - tile.EstimatedEhbSnapshot));
            await WriteAudit("board.tile_removed", board, before, new { board = BoardAuditState(board), tile = (object?)null }, ct);
            await transaction.CommitAsync(ct); committed = true;
            TempData["BoardTileOutcome"] = "committed";
            foreach (var image in removedImages) await storage.DeleteAsync(image.StorageKey, ct);
            await collaboration.NotifyBoardChangedAsync(id, ct); SetSuccessIfMissing(Localize("Tile removed.")); return RedirectToPage(new { id });
        }
        catch (Exception exception) when (!committed && (IsApprovalConflict(exception) || exception is InvalidOperationException))
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            SetStatus(exception is InvalidOperationException ? Localize(exception.Message) : Localize("The board changed while this correction was being saved. Reload and try again."), UiMessageType.Warning);
            return RedirectToPage(new { id });
        }
    }

    private async Task<BoardTile> PlaceTemplateAsync(Board board, TileTemplate template, IReadOnlyList<TileTemplateRequirement> requirements, int position, CancellationToken ct)
    {
        var estimates = new List<decimal?>();
        foreach (var requirement in requirements)
        {
            if (requirement.ManualObjective) { estimates.Add(null); continue; }
            var rateRows = await (from link in db.TemplateRequirementDrops where link.RequirementId == requirement.Id join drop in db.SourceDrops on link.SourceDropId equals drop.Id join boss in db.BossActivities on drop.BossActivityId equals boss.Id select new { link, drop, boss }).ToListAsync(ct);
            var rates = rateRows.Select(x => new EligibleDropRate(x.boss.EfficientCompletionsPerHour, x.drop.NumericProbability, x.drop.ItemId, x.boss.Id, x.link.CreditedWeight, x.drop.RollsPerCompletion, x.drop.RollGroup));
            estimates.Add(EhbCalculator.CalculateDropRequirement(requirement.TargetContribution, rates, requirement.DuplicatesAllowed));
        }
        var ehb = EhbCalculator.CalculateTileEstimate(template.ObjectiveType, requirements.Zip(estimates, (requirement, estimate) => (requirement.ManualObjective, estimate)), template.ManualEhbOverride); var boardTile = new BoardTile(Guid.NewGuid(), board.Id, template.Id, position / board.Columns, position % board.Columns, template.Name, template.Description, template.EvidenceInstructions, ehb, template.ImageUrl, template.DescriptionIsAutomatic); db.BoardTiles.Add(boardTile);
        foreach (var requirement in requirements)
        {
            var snapshot = new BoardRequirementSnapshot(Guid.NewGuid(), boardTile.Id, requirement.Position, requirement.TargetContribution, requirement.DuplicatesAllowed, requirement.AllowHigherWeightings, requirement.Description, requirement.ManualObjective); db.BoardRequirementSnapshots.Add(snapshot);
            var bosses = await (from link in db.TemplateRequirementBosses where link.RequirementId == requirement.Id join boss in db.BossActivities on link.BossActivityId equals boss.Id select boss).ToListAsync(ct); foreach (var boss in bosses) db.BoardRequirementBossSnapshots.Add(new BoardRequirementBossSnapshot(Guid.NewGuid(), snapshot.Id, boss.Id, boss.Name, boss.EfficientCompletionsPerHour));
            var drops = await (from link in db.TemplateRequirementDrops where link.RequirementId == requirement.Id join drop in db.SourceDrops on link.SourceDropId equals drop.Id join boss in db.BossActivities on drop.BossActivityId equals boss.Id join item in db.CatalogueItems on drop.ItemId equals item.Id select new { link, drop, boss, item }).ToListAsync(ct); foreach (var value in drops) db.BoardRequirementDropSnapshots.Add(new BoardRequirementDropSnapshot(Guid.NewGuid(), snapshot.Id, value.drop.Id, value.drop.ItemId, value.boss.Name, value.item.Name, value.drop.DisplayRate, value.drop.NumericProbability, value.link.MaximumContribution, value.drop.DefaultEhbEstimate, value.link.CreditedWeight));
        }
        board.SetTotalEhb(board.TotalEhbEstimate + ehb);
        return boardTile;
    }

    private async Task<bool> TryEnsurePublishedCorrectionLifecycleAsync(Board board, Guid eventId, CancellationToken ct)
    {
        if (board.State != BoardState.Published || !board.PublishedCorrectionInProgress) return true;
        var bingoEvent = await db.Events
            .FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} AND hidden_at IS NULL FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (bingoEvent is null) return false;
        try
        {
            bingoEvent.EnsurePublishedBoardCorrectionAllowed();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private async Task<bool> EnsureBoardAndEditingLeaseAsync(Guid eventId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var bingoEvent = await db.Events
            .FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (bingoEvent is null) return false;

        var board = await db.Boards
            .FromSqlInterpolated($"SELECT * FROM boards WHERE event_id = {eventId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (board is null)
        {
            // EVT-01 creates boards for new events. This fallback is deliberately
            // narrow: it repairs an unfinished pre-live legacy event only and
            // never invents a board for historical or terminal data.
            if (!CanCreateLegacyBoard(bingoEvent)) return true;

            board = new Board(Guid.NewGuid(), eventId, "Main board", 5, 5);
            board.AcquireEditing(AdminId, time.GetUtcNow(), BoardEditingLease.Duration);
            db.Boards.Add(board);
            audit.Stage(AdminId, User.Identity?.Name ?? "Admin", "board.created", "board", board.Id.ToString(),
                JsonSerializer.Serialize(new { reason = "unfinished_legacy_event" }), eventId);
        }
        // Opening the board is a read-only view. An editor lease is acquired by
        // an explicit board action (or takeover) and is renewed only by a
        // committed board action or actual collaboration activity. In
        // particular, loading a form/token before an invalid or stale request
        // must not change the board lease, version, or audit history.

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private static bool CanCreateLegacyBoard(BingoEvent bingoEvent) =>
        !bingoEvent.IsHidden && !bingoEvent.HasEverBeenLive &&
        bingoEvent.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed;

    private async Task<bool> Load(Guid id, CancellationToken ct)
    {
        // Render the lightweight overview and snapshot-backed tile details from
        // one coherent read. Full catalogue choices are loaded by EditorData on
        // demand when an editor is opened.
        await using var readTransaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct)
            : null;
        var bingoEvent = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (bingoEvent is null) return false; EventName = bingoEvent.Name; EventState = bingoEvent.State;
        Bosses = [];
        Drops = [];
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct); if (board is null) { CanCreateBoard = CanCreateLegacyBoard(bingoEvent); Rows = 5; Columns = 5; return true; }
        CanCreateBoard = false;
        var activeRosterPublication = await db.ActiveRosterPublicationAsync(id, ct);
        DraftFinalized = activeRosterPublication is not null;
        if (board.HasActiveEditor(time.GetUtcNow()))
        {
            BoardEditorAccountId = board.EditorAccountId;
            BoardEditorLeaseExpiresAt = board.EditorLeaseExpiresAt;
            BoardEditorName = await db.Accounts.AsNoTracking().Where(x => x.Id == board.EditorAccountId).Select(x => x.LoginName).SingleOrDefaultAsync(ct);
            CanEditBoard = board.EditorAccountId == CurrentAccountId;
        }
        Rows = board.Rows; Columns = board.Columns; BoardVersion = board.Version;
        var boardTilesForEditors = await db.BoardTiles.AsNoTracking().Where(x => x.BoardId == board.Id).OrderBy(x => x.RowIndex).ThenBy(x => x.ColumnIndex).ToListAsync(ct);
        var tileIds = boardTilesForEditors.Select(x => x.Id).ToList();
        var editorRequirements = await db.BoardRequirementSnapshots.AsNoTracking().Where(x => tileIds.Contains(x.BoardTileId)).OrderBy(x => x.Position).ToListAsync(ct);
        var editorRequirementIds = editorRequirements.Select(x => x.Id).ToList();
        if (tileIds.Count > 0)
        {
            var evidencedRequirementIds = await db.EvidencedObjectiveIdsAsync(id, ct);
            var protectedTileIds = editorRequirements
                .Where(requirement => evidencedRequirementIds.Contains(requirement.Id))
                .Select(requirement => requirement.BoardTileId)
                .ToHashSet();
            var submittedTileIds = await db.Submissions.AsNoTracking()
                .Where(submission => submission.EventId == id && tileIds.Contains(submission.BoardTileId))
                .Select(submission => submission.BoardTileId)
                .Distinct()
                .ToListAsync(ct);
            protectedTileIds.UnionWith(submittedTileIds);
            ProtectedTileIds = protectedTileIds;
        }
        var editorBosses = await db.BoardRequirementBossSnapshots.AsNoTracking().Where(x => editorRequirementIds.Contains(x.RequirementId)).ToListAsync(ct);
        var editorDrops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => editorRequirementIds.Contains(x.RequirementId)).ToListAsync(ct);
        var editorTemplateIds = boardTilesForEditors.Select(x => x.TileTemplateId).Distinct().ToList();
        var editorTemplates = await db.TileTemplates.AsNoTracking().Where(x => editorTemplateIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var useLiveDerivation = board.State == BoardState.Draft || board.PublishedCorrectionInProgress;
        var requirementsByTile = editorRequirements.GroupBy(value => value.BoardTileId).ToDictionary(group => group.Key, group => (IReadOnlyList<BoardRequirementSnapshot>)group.OrderBy(value => value.Position).ToList());
        var bossesByRequirement = editorBosses.GroupBy(value => value.RequirementId).ToDictionary(group => group.Key, group => (IReadOnlyList<BoardRequirementBossSnapshot>)group.ToList());
        var dropsByRequirement = editorDrops.GroupBy(value => value.RequirementId).ToDictionary(group => group.Key, group => (IReadOnlyList<BoardRequirementDropSnapshot>)group.OrderBy(value => value.BossName).ThenBy(value => value.ItemName).ToList());
        var liveDropRows = useLiveDerivation
            ? await (from snapshot in db.BoardRequirementDropSnapshots.AsNoTracking()
                     join drop in db.SourceDrops.AsNoTracking() on snapshot.SourceDropId equals drop.Id
                     join boss in db.BossActivities.AsNoTracking() on drop.BossActivityId equals boss.Id
                     join item in db.CatalogueItems.AsNoTracking() on drop.ItemId equals item.Id
                     where editorRequirementIds.Contains(snapshot.RequirementId)
                     select new LiveDrop(snapshot.RequirementId, snapshot.SourceDropId, snapshot, drop, boss, item)).ToListAsync(ct)
            : [];
        if (useLiveDerivation)
        {
            var referencedBossIds = editorBosses.Select(x => x.BossActivityId).Distinct().ToList();
            var fingerprintBosses = await db.BossActivities.AsNoTracking().Where(x => referencedBossIds.Contains(x.Id)).ToListAsync(ct);
            ApprovalCatalogueFingerprint = CatalogueFingerprint(fingerprintBosses,
                liveDropRows.Select(x => new ApprovalLiveDrop(x.Drop, x.Boss, x.Item)));
        }
        var liveDropsByRequirement = liveDropRows.GroupBy(value => value.RequirementId).ToDictionary(group => group.Key, group => (IReadOnlyList<LiveDrop>)group.ToList());
        var liveDropsById = liveDropRows.GroupBy(value => value.SourceDropId).ToDictionary(group => group.Key, group => group.First());

        // The overview only needs names for references already on this board.
        // Do not load the complete active catalogue until EditorData is asked
        // for by the create/edit dialog.
        var referencedBosses = editorBosses
            .GroupBy(value => value.BossActivityId)
            .Select(group => group.OrderByDescending(value => value.Id).First())
            .Select(value => new BossView(value.BossActivityId, value.BossName, string.Empty, value.EfficientRate))
            .OrderBy(value => value.Name)
            .ToList();
        Bosses = referencedBosses;
        var referencedDropIds = editorDrops.Select(value => value.SourceDropId).Distinct().ToArray();
        var referencedDropRows = await (from drop in db.SourceDrops.AsNoTracking()
                                        join boss in db.BossActivities.AsNoTracking() on drop.BossActivityId equals boss.Id
                                        join item in db.CatalogueItems.AsNoTracking() on drop.ItemId equals item.Id
                                        where referencedDropIds.Contains(drop.Id)
                                        select new { drop.Id, BossId = boss.Id, BossName = boss.Name, ItemName = item.Name, drop.DisplayRate })
            .ToListAsync(ct);
        var dropRowsById = referencedDropRows.ToDictionary(value => value.Id);
        Drops = editorDrops
            .GroupBy(value => value.SourceDropId)
            .Select(group =>
            {
                var snapshot = group.First();
                return dropRowsById.TryGetValue(snapshot.SourceDropId, out var current)
                    ? new DropView(snapshot.SourceDropId, current.BossId, current.BossName, current.ItemName, current.DisplayRate)
                    : new DropView(snapshot.SourceDropId, Guid.Empty, snapshot.BossName, snapshot.ItemName, snapshot.DisplayRate);
            })
            .OrderBy(value => value.BossName)
            .ThenBy(value => value.ItemName)
            .ToList();

        IReadOnlyDictionary<Guid, BoardApprovalTileSnapshot> frozenTiles = new Dictionary<Guid, BoardApprovalTileSnapshot>();
        var frozenDropsByBoardRequirementAndDrop = new Dictionary<(Guid RequirementId, Guid DropId), BoardApprovalRequirementDropSnapshot>();
        BoardApprovalSnapshot? activeApproval = null;
        if (!useLiveDerivation)
        {
            if (board.ActiveApprovalSnapshotId is not { } approvalId) return false;
            activeApproval = await db.BoardApprovalSnapshots.AsNoTracking().SingleOrDefaultAsync(value => value.Id == approvalId && value.BoardId == board.Id, ct);
            if (activeApproval is null) return false;
            frozenTiles = await db.BoardApprovalTileSnapshots.AsNoTracking().Where(value => value.ApprovalSnapshotId == approvalId).ToDictionaryAsync(value => value.BoardTileId, ct);
            if (frozenTiles.Count != boardTilesForEditors.Count || boardTilesForEditors.Any(value => !frozenTiles.ContainsKey(value.Id))) return false;
            var frozenTileSnapshotIds = frozenTiles.Values.Select(tile => tile.Id).ToList();
            var approvalRequirements = await db.BoardApprovalRequirementSnapshots.AsNoTracking().Where(value => frozenTileSnapshotIds.Contains(value.ApprovalTileSnapshotId)).ToListAsync(ct);
            var boardRequirementIdByApprovalRequirementId = approvalRequirements.ToDictionary(value => value.Id, value => value.BoardRequirementSnapshotId);
            var approvalRequirementIds = approvalRequirements.Select(value => value.Id).ToList();
            var approvalDrops = await db.BoardApprovalRequirementDropSnapshots.AsNoTracking().Where(value => approvalRequirementIds.Contains(value.ApprovalRequirementSnapshotId)).ToListAsync(ct);
            foreach (var approvalDrop in approvalDrops)
            {
                if (boardRequirementIdByApprovalRequirementId.TryGetValue(approvalDrop.ApprovalRequirementSnapshotId, out var boardRequirementId))
                    frozenDropsByBoardRequirementAndDrop[(boardRequirementId, approvalDrop.SourceDropId)] = approvalDrop;
            }
        }

        Tiles = useLiveDerivation
            ? BuildStoredTiles(boardTilesForEditors, requirementsByTile, liveDropsByRequirement, board.Columns)
            : BuildFrozenTiles(boardTilesForEditors, frozenTiles, board.Columns);
        var displayedTotal = activeApproval?.TotalEhbEstimate ?? Tiles.Sum(tile => tile.Ehb);
        BoardView = new(board.Rows, board.Columns, board.State, displayedTotal, board.Version, board.PublishedCorrectionInProgress);
        ReadinessWarnings = useLiveDerivation
            ? Tiles.Where(tile => tile.EstimateNeedsVerification || tile.Ehb <= 0).Select(tile => tile.EstimateNeedsVerification
                ? Localize("{0} needs catalogue verification. Review the affected choices or restore the missing catalogue mapping before approval.", tile.Name)
                : Localize("{0} needs a valid estimate. Catalogue tiles require automatic EHB; custom tiles require manual EHB. Keep the objective kinds in separate tiles.", tile.Name)).ToList()
            : [];
        var managedImages = await db.BoardTileImageAssets.AsNoTracking().Where(image => tileIds.Contains(image.BoardTileId) && image.ReplacedAt == null).ToDictionaryAsync(image => image.Id, ct);
        var calculatedBaselines = await BoardEstimateService.CalculatedBaselinesAsync(db, boardTilesForEditors.Select(tile => tile.Id), ct);
        TileEditors = boardTilesForEditors.Select(tile => new TileEditorView(tile.Id,
            useLiveDerivation ? tile.NameSnapshot : frozenTiles[tile.Id].Name,
            tile.DescriptionIsAutomatic ? string.Empty : useLiveDerivation ? tile.DescriptionSnapshot : frozenTiles[tile.Id].Description,
            tile.ActiveImageAssetId is { } imageId && managedImages.TryGetValue(imageId, out var image) && image.BoardTileId == tile.Id && image.EventId == id ? Url.Page("Board", "TileImage", new { id, tileId = tile.Id }) : null,
            editorTemplates.GetValueOrDefault(tile.TileTemplateId)?.ManualEhbOverride,
            requirementsByTile.GetValueOrDefault(tile.Id, []).Select(requirement => new RequirementEditorView(requirement.Id, requirement.ManualObjective ? "challenge" : "drops", requirement.Description, requirement.TargetContribution, requirement.DuplicatesAllowed,
                bossesByRequirement.GetValueOrDefault(requirement.Id, []).Select(value => value.BossActivityId).ToList(),
                dropsByRequirement.GetValueOrDefault(requirement.Id, []).Select(value => value.SourceDropId).ToList(),
                dropsByRequirement.GetValueOrDefault(requirement.Id, []).ToDictionary(value => value.SourceDropId, value => value.CreditedWeight),
                dropsByRequirement.GetValueOrDefault(requirement.Id, []).Select(drop =>
                {
                    if (useLiveDerivation && liveDropsById.TryGetValue(drop.SourceDropId, out var live)) return new RequirementDropView(drop.SourceDropId, live.Boss.Name, live.Item.Name, live.Drop.DisplayRate, drop.CreditedWeight);
                    if (!useLiveDerivation && frozenDropsByBoardRequirementAndDrop.TryGetValue((requirement.Id, drop.SourceDropId), out var frozen)) return new RequirementDropView(drop.SourceDropId, frozen.BossName, frozen.ItemName, frozen.DisplayRate, drop.CreditedWeight);
                    return new RequirementDropView(drop.SourceDropId, "Unavailable boss", "Unavailable item", "Catalogue entry unavailable", drop.CreditedWeight);
                }).ToList())).ToList(), calculatedBaselines.GetValueOrDefault(tile.Id))).ToList();
        var lines = new List<LineView>(); for (var row = 0; row < board.Rows; row++) lines.Add(new($"Row {row + 1}", "row", row, Tiles.Where(x => x.Position / board.Columns == row).Sum(x => x.Ehb))); for (var column = 0; column < board.Columns; column++) lines.Add(new($"Column {column + 1}", "column", column, Tiles.Where(x => x.Position % board.Columns == column).Sum(x => x.Ehb))); Lines = lines; BalanceSpread = lines.Count == 0 ? 0 : lines.Max(x => x.Ehb) - lines.Min(x => x.Ehb);
        var eventTeams = await db.Teams.AsNoTracking().Where(x => x.EventId == id && x.Active).OrderBy(x => x.DraftPosition).ThenBy(x => x.Name).ToListAsync(ct);
        var rosterSizes = new Dictionary<Guid, int>();
        if (eventTeams.Count > 0)
        {
            var teamIdsForWorkload = eventTeams.Select(x => x.Id).ToList();
            rosterSizes = activeRosterPublication is { } frozenRoster
                ? await db.DraftPublicationRosters.AsNoTracking()
                    .Where(x => x.DraftPublicationCycleId == frozenRoster.Id && teamIdsForWorkload.Contains(x.TeamId))
                    .GroupBy(x => x.TeamId)
                    .ToDictionaryAsync(x => x.Key, x => x.Count(), ct)
                : await db.TeamMemberships.AsNoTracking()
                    .Where(x => teamIdsForWorkload.Contains(x.TeamId) && x.LeftAt == null)
                    .GroupBy(x => x.TeamId)
                    .ToDictionaryAsync(x => x.Key, x => x.Count(), ct);
        }
        // ExpectedTeamSize is the manual planning estimate in every lifecycle
        // state. The frozen roster remains visible as actual context, but never
        // replaces the estimate used for projections.
        var teamSize = bingoEvent.ExpectedTeamSize;
        var durationDays = bingoEvent.EventEndsAt is { } eventEnd && bingoEvent.EventStartsAt is { } eventStart ? Math.Max(0.5m, (decimal)(eventEnd - eventStart).TotalHours / 24m) : 0.5m; var total = displayedTotal; var populatedLines = lines.Where(x => x.Ehb > 0).ToList();
        Statistics = new(total, teamSize, teamSize is > 0 ? total / teamSize.Value : null, teamSize is > 0 ? total / teamSize.Value / durationDays : null, Tiles.Count == 0 ? 0 : total / Tiles.Count, populatedLines.Count == 0 ? 0 : populatedLines.Min(x => x.Ehb), populatedLines.Count == 0 ? 0 : populatedLines.Max(x => x.Ehb), Tiles.Count(x => x.Ehb <= 0), durationDays);
        if (eventTeams.Count > 0)
        {
            TeamWorkloads = eventTeams.Select(team =>
            {
                var actualSize = rosterSizes.GetValueOrDefault(team.Id);
                var sizeUsed = bingoEvent.ExpectedTeamSize ?? 0;
                return new TeamWorkloadView(team.Name, team.FormationType, actualSize, sizeUsed > 0 ? sizeUsed : null, sizeUsed > 0 ? total / sizeUsed : null);
            }).ToList();
        }
        return true;
    }

    private Guid AdminId => User.GetAccountId()!.Value;
    private Task WriteAudit(string action, Board board, object? before, object? after, CancellationToken ct) =>
        audit.WriteAndSaveAsync(AdminId, User.Identity?.Name ?? "Admin", action, "board", board.Id.ToString(), JsonSerializer.Serialize(new { before, after }), board.EventId, ct);

    private static object BoardAuditState(Board board) => new { board.Id, board.Name, board.Rows, board.Columns, State = board.State.ToString(), board.TotalEhbEstimate, board.EditorAccountId, board.EditorLeaseExpiresAt, board.EditControlVersion };
    private static object TilePositionAuditState(BoardTile tile) => new { tile.Id, tile.RowIndex, tile.ColumnIndex };
    private async Task<object> TileAuditStateAsync(BoardTile tile, CancellationToken ct)
    {
        var requirements = await db.BoardRequirementSnapshots.AsNoTracking().Where(x => x.BoardTileId == tile.Id).OrderBy(x => x.Position).ToListAsync(ct);
        var ids = requirements.Select(x => x.Id).ToList();
        var bosses = await db.BoardRequirementBossSnapshots.AsNoTracking().Where(x => ids.Contains(x.RequirementId)).ToListAsync(ct);
        var drops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => ids.Contains(x.RequirementId)).ToListAsync(ct);
        var manualEhb = await db.TileTemplates.AsNoTracking().Where(x => x.Id == tile.TileTemplateId).Select(x => new { x.ManualEhbOverride }).SingleOrDefaultAsync(ct);
        var requirementState = requirements.Select(requirement => new
        {
            requirement.Position,
            requirement.TargetContribution,
            requirement.Description,
            requirement.ManualObjective,
            requirement.DuplicatesAllowed,
            requirement.AllowHigherWeightings,
            bosses = bosses.Where(x => x.RequirementId == requirement.Id).Select(x => x.BossActivityId).OrderBy(x => x).ToArray(),
            drops = drops.Where(x => x.RequirementId == requirement.Id).OrderBy(x => x.SourceDropId).Select(x => new { x.SourceDropId, x.MaximumContribution, x.CreditedWeight }).ToArray()
        }).ToArray();
        return new
        {
            tile.Id,
            tile.TileTemplateId,
            tile.RowIndex,
            tile.ColumnIndex,
            NameSnapshot = AuditText(tile.NameSnapshot),
            DescriptionSnapshot = AuditText(tile.DescriptionSnapshot),
            tile.EstimatedEhbSnapshot,
            tile.ActiveImageAssetId,
            TemplatePresent = manualEhb is not null,
            ManualEhbOverride = manualEhb?.ManualEhbOverride,
            requirements = BoundedCollectionAuditState(requirementState)
        };
    }

    // These board-specific summaries preserve valid large layouts/objectives within
    // the existing audit column. The digest covers the complete canonical value;
    // an omitted collection or text suffix is explicitly identified, never invented.
    private static JsonElement TileLayoutAuditState(IEnumerable<BoardTile> tiles) =>
        BoundedCollectionAuditState(tiles.OrderBy(tile => tile.Id).Select(TilePositionAuditState).ToArray());
    private static JsonElement BoundedCollectionAuditState<T>(T[] values)
    {
        var json = JsonSerializer.Serialize(values);
        return json.Length <= 600 ? JsonSerializer.SerializeToElement(values) : JsonSerializer.SerializeToElement(new { Count = values.Length, ValuesOmitted = true, Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))) });
    }
    private static JsonElement AuditText(string value) => JsonSerializer.Serialize(value).Length <= 100 ? JsonSerializer.SerializeToElement(value)
        : JsonSerializer.SerializeToElement(new { Preview = value[..Math.Min(value.Length, 24)], value.Length, Truncated = true, Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))) });

    private async Task<bool> TryClaimBoardAsync(Board board, CancellationToken ct, bool reportStatus = true)
    {
        try { board.RenewEditing(AdminId, time.GetUtcNow(), BoardEditingLease.Duration); }
        catch (InvalidOperationException) { if (reportStatus) SetStatus(Localize("The board change could not be saved."), UiMessageType.Warning); db.ChangeTracker.Clear(); return false; }
        db.Entry(board).Property(x => x.Version).OriginalValue = BoardVersion;
        board.MarkChanged();
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException)
        {
            if (reportStatus) SetStatus(Localize("This board changed after you opened it. Your change was not saved. The latest board has been loaded."), UiMessageType.Warning);
            db.ChangeTracker.Clear();
            return false;
        }
    }
    private void SetStatus(string message, UiMessageType type)
    {
        if (suppressPageStatus) return;
        TempData["StatusMessage"] = message;
        TempData[UiMessage.TypeKey] = type.ToString();
    }
    private string Localize(string key, params object[] arguments) => text?[key, arguments].Value ?? string.Format(CultureInfo.CurrentCulture, key, arguments);
    private void SetSuccessIfMissing(string message)
    {
        if (TempData.Peek("StatusMessage") is null) SetStatus(message, UiMessageType.Success);
    }
    private static JsonResult MoveSuccess(long boardVersion, string message) => new(new { success = true, boardVersion, message, type = UiMessageType.Success.ToString().ToLowerInvariant() });
    private static JsonResult MoveFailure(string message, UiMessageType type) => new(new { success = false, message, type = type.ToString().ToLowerInvariant() });
    private static string DefaultTileName(IEnumerable<string> names) { var list = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList(); return list.Count switch { 0 => "New tile", 1 => list[0], _ => string.Join(" + ", list) }; }
    private async Task<string?> ReplaceTileImageAsync(Guid eventId, BoardTile tile, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        if (TileDraft.RemoveImage && tile.ActiveImageAssetId is { } priorId)
        {
            (await db.BoardTileImageAssets.SingleOrDefaultAsync(x => x.Id == priorId, ct))?.Replace(now);
            tile.SetActiveImageAsset(null);
        }
        if (TileDraft.Image is not { Length: > 0 }) return null;
        var assetId = Guid.NewGuid();
        await using var content = TileDraft.Image.OpenReadStream();
        var stored = await storage.StoreAsync(eventId, assetId, TileDraft.Image.FileName, content, ct);
        if (tile.ActiveImageAssetId is { } currentId)
            (await db.BoardTileImageAssets.SingleOrDefaultAsync(x => x.Id == currentId, ct))?.Replace(now);
        db.BoardTileImageAssets.Add(new BoardTileImageAsset(assetId, eventId, tile.Id, stored.StorageKey, stored.OriginalFilename, stored.MediaType, stored.ByteSize, stored.Width, stored.Height, stored.Checksum, AdminId, now));
        tile.SetActiveImageAsset(assetId);
        return stored.StorageKey;
    }
    private static List<TileView> BuildLiveTiles(
        IReadOnlyList<BoardTile> tiles,
        IReadOnlyDictionary<Guid, IReadOnlyList<BoardRequirementSnapshot>> requirementsByTile,
        IReadOnlyDictionary<Guid, IReadOnlyList<LiveDrop>> dropsByRequirement,
        Dictionary<Guid, TileTemplate> templates,
        int columns)
    {
        return tiles.Select(tile =>
        {
            var estimates = new List<decimal?>();
            var requirements = requirementsByTile.GetValueOrDefault(tile.Id, []);
            foreach (var requirement in requirements)
            {
                if (requirement.ManualObjective) { estimates.Add(null); continue; }
                var selected = dropsByRequirement.GetValueOrDefault(requirement.Id, []);
                if (selected.Count == 0 || selected.Any(drop => !drop.Drop.Active || !drop.Boss.Active || !drop.Item.Active)) { estimates.Add(null); continue; }
                estimates.Add(EhbCalculator.CalculateDropRequirement(requirement.TargetContribution,
                    selected.Select(drop => new EligibleDropRate(drop.Boss.EfficientCompletionsPerHour, drop.Drop.NumericProbability, drop.Drop.ItemId, drop.Boss.Id, drop.Snapshot.CreditedWeight, drop.Drop.RollsPerCompletion, drop.Drop.RollGroup)),
                    requirement.DuplicatesAllowed));
            }
            var ehb = templates.TryGetValue(tile.TileTemplateId, out var template)
                ? EhbCalculator.CalculateTileEstimate(template.ObjectiveType, requirements.Zip(estimates, (requirement, estimate) => (requirement.ManualObjective, estimate)), template.ManualEhbOverride)
                : 0;
            var description = tile.DescriptionIsAutomatic
                ? TileDescriptionFormatter.Format(requirements.Select(requirement => new TileDescriptionRequirement(
                    requirement.Position,
                    requirement.TargetContribution,
                    requirement.ManualObjective,
                    requirement.Description,
                    dropsByRequirement.GetValueOrDefault(requirement.Id, [])
                        .Select(drop => new TileDescriptionDrop(drop.Drop.ItemId, drop.Item.Name, drop.Boss.Name))
                        .ToList())))
                : tile.DescriptionSnapshot;
            return new TileView(tile.Id, tile.RowIndex * columns + tile.ColumnIndex, tile.NameSnapshot, description, string.Empty, ehb, tile.EstimateNeedsVerification);
        }).ToList();
    }
    private static List<TileView> BuildStoredTiles(
        IReadOnlyList<BoardTile> tiles,
        IReadOnlyDictionary<Guid, IReadOnlyList<BoardRequirementSnapshot>> requirementsByTile,
        IReadOnlyDictionary<Guid, IReadOnlyList<LiveDrop>> dropsByRequirement,
        int columns)
    {
        return tiles.Select(tile =>
        {
            var requirements = requirementsByTile.GetValueOrDefault(tile.Id, []);
            var description = tile.DescriptionIsAutomatic && dropsByRequirement.Count > 0
                ? TileDescriptionFormatter.Format(requirements.Select(requirement => new TileDescriptionRequirement(
                    requirement.Position,
                    requirement.TargetContribution,
                    requirement.ManualObjective,
                    requirement.Description,
                    dropsByRequirement.GetValueOrDefault(requirement.Id, [])
                        .Select(drop => new TileDescriptionDrop(drop.Snapshot.ItemIdSnapshot, drop.Snapshot.ItemName, drop.Snapshot.BossName))
                        .ToList())))
                : tile.DescriptionSnapshot;
            return new TileView(tile.Id, tile.RowIndex * columns + tile.ColumnIndex, tile.NameSnapshot, description,
                tile.EvidenceInstructionsSnapshot, tile.EstimatedEhbSnapshot, tile.EstimateNeedsVerification);
        }).ToList();
    }
    private static List<TileView> BuildFrozenTiles(
        IReadOnlyList<BoardTile> tiles,
        IReadOnlyDictionary<Guid, BoardApprovalTileSnapshot> frozenTiles,
        int columns) =>
        tiles.Select(tile =>
        {
            var frozen = frozenTiles[tile.Id];
            return new TileView(tile.Id, frozen.RowIndex * columns + frozen.ColumnIndex, frozen.Name, frozen.Description, frozen.EvidenceInstructions, frozen.EstimatedEhb, false);
        }).ToList();
    private static string RequirementDescription(RequirementInput input) => input.IsManual ? input.Description!.Trim() : $"Collect {input.Target} eligible drop{(input.Target == 1 ? string.Empty : "s")}";

    public sealed class TileDraftInput { public Guid? TileId { get; set; } public int Position { get; set; } public string? Name { get; set; } [StringLength(TileDescriptionFormatter.MaximumManualDescriptionLength, ErrorMessage = "Tile description cannot be longer than 4000 characters.")] public string? Description { get; set; } public IFormFile? Image { get; set; } public bool RemoveImage { get; set; } [ModelBinder(BinderType = typeof(ManualEhbInputBinder))] [Range(typeof(decimal), "0.0001", "100000", ParseLimitsInInvariantCulture = true)] public decimal? ManualEhb { get; set; } public bool ChangeManualEhbOverride { get; set; } public List<RequirementInput> Requirements { get; set; } = [new()]; }
    public sealed class RequirementInput { public Guid? RequirementId { get; set; } public string Kind { get; set; } = "drops"; public string? Description { get; set; } [Range(1, 10000)] public int Target { get; set; } = 1; public bool DuplicatesAllowed { get; set; } = true; public Dictionary<Guid, int> DropWeights { get; set; } = []; public List<Guid> BossIds { get; set; } = []; public List<Guid> DropIds { get; set; } = []; public bool IsManual => string.Equals(Kind, "challenge", StringComparison.OrdinalIgnoreCase); public int WeightFor(Guid dropId) => Math.Max(1, DropWeights.GetValueOrDefault(dropId, 1)); public bool HasHigherWeights => DropIds.Any(x => WeightFor(x) > 1); }
    public sealed record BoardDetails(int Rows, int Columns, BoardState State, decimal TotalEhb, long Version, bool PublishedCorrectionInProgress);
    public sealed record BoardStatistics(decimal TotalEhb, int? TeamSize, decimal? EhbPerPlayer, decimal? EhbPerPlayerPerDay, decimal AverageTileEhb, decimal LowestLineEhb, decimal HighestLineEhb, int MissingEhbTiles, decimal DurationDays);
    public sealed record TileView(Guid Id, int Position, string Name, string Description, string EvidenceInstructions, decimal Ehb, bool EstimateNeedsVerification);
    public sealed record LineView(string Key, string Kind, int Index, decimal Ehb);
    public sealed record BossView(Guid Id, string Name, string Category, decimal? EfficientRate);
    public sealed record DropView(Guid Id, Guid BossId, string BossName, string ItemName, string Rate);
    private sealed record LiveDrop(Guid RequirementId, Guid SourceDropId, BoardRequirementDropSnapshot Snapshot, SourceDrop Drop, BossActivity Boss, CatalogueItem Item);
    private static string CatalogueFingerprint(IEnumerable<BossActivity> bosses, IEnumerable<ApprovalLiveDrop> drops)
    {
        var rows = drops.ToList();
        var inputs = new
        {
            Bosses = bosses.Concat(rows.Select(x => x.Boss)).DistinctBy(x => x.Id).OrderBy(x => x.Id).Select(x => new { x.Id, x.Version }),
            Drops = rows.Select(x => x.Drop).DistinctBy(x => x.Id).OrderBy(x => x.Id).Select(x => new { x.Id, x.Version, x.BossActivityId, x.ItemId }),
            Items = rows.Select(x => x.Item).DistinctBy(x => x.Id).OrderBy(x => x.Id).Select(x => new { x.Id, x.Version })
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(inputs))));
    }

    private sealed class BoardApprovalValidationException : InvalidOperationException
    {
        public BoardApprovalValidationException(string code, Guid? tileId, int? position, string? tileName, string resourceKey, params object[] arguments)
            : this([new BoardValidationIssue(code, tileId, position, tileName, resourceKey, arguments)]) { }
        private readonly object[]? pageArguments;
        public BoardApprovalValidationException(IReadOnlyList<BoardValidationIssue> issues, object[]? pageArguments = null)
        {
            Issues = issues;
            this.pageArguments = pageArguments;
        }
        public IReadOnlyList<BoardValidationIssue> Issues { get; }
        public BoardValidationIssue Issue => Issues[0];
        public string ResourceKey => Issue.ResourceKey;
        public object[] Arguments => pageArguments ?? Issue.Arguments.ToArray();
    }

    private sealed record ApprovalLiveDrop(SourceDrop Drop, BossActivity Boss, CatalogueItem Item);
    public sealed record TileEditorView(Guid Id, string Name, string Description, string? ImageUrl, decimal? ManualEhb, IReadOnlyList<RequirementEditorView> Requirements, decimal? CalculatedEhb = null);
    public sealed record RequirementEditorView(Guid RequirementId, string Kind, string Description, int Target, bool DuplicatesAllowed, IReadOnlyList<Guid> BossIds, IReadOnlyList<Guid> DropIds, IReadOnlyDictionary<Guid, int> DropWeights, IReadOnlyList<RequirementDropView> Drops);
    public sealed record RequirementDropView(Guid Id, string BossName, string ItemName, string DisplayRate, int CreditedWeight);
    public sealed record TeamWorkloadView(string TeamName, TeamFormationType FormationType, int ActualRosterSize, int? SizeUsed, decimal? EhbPerPlayer);
}
