using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using Bingo.Application.Access;
using Bingo.Application.Auditing;
using Bingo.Application.Boards;
using Bingo.Application.Teams;
using Bingo.Domain.Boards;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class BoardModel(ApplicationDbContext db, TimeProvider time, IAuditWriter audit, IAdminCollaborationNotifier collaboration) : PageModel
{
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
    public Guid CurrentAccountId { get; private set; }
    public bool CanEditBoard { get; private set; }
    public Guid? BoardEditorAccountId { get; private set; }
    public string? BoardEditorName { get; private set; }
    public DateTimeOffset? BoardEditorLeaseExpiresAt { get; private set; }

    [BindProperty, Range(1, 20)] public int Rows { get; set; } = 5;
    [BindProperty, Range(1, 20)] public int Columns { get; set; } = 5;
    [BindProperty] public long BoardVersion { get; set; }
    [BindProperty] public TileDraftInput TileDraft { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        CurrentAccountId = User.GetAccountId()!.Value;
        return await Load(id, ct) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostCreateAsync(Guid id, CancellationToken ct)
    {
        if (await db.Boards.AnyAsync(x => x.EventId == id, ct)) return RedirectToPage(new { id });
        var board = new Board(Guid.NewGuid(), id, "Main board", Rows, Columns);
        board.AcquireEditing(AdminId, time.GetUtcNow(), BoardEditingLease.Duration);
        db.Boards.Add(board); await db.SaveChangesAsync(ct); await WriteAudit("board.created", board.Id, $"{Rows}x{Columns}", ct);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostTakeEditingAsync(Guid id, CancellationToken ct)
    {
        var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct); if (board is null) return NotFound();
        try
        {
            var previous = board.AcquireEditing(AdminId, time.GetUtcNow(), BoardEditingLease.Duration, force: true);
            await db.SaveChangesAsync(ct);
            await WriteAudit(previous is null ? "board.editing_acquired" : "board.editing_taken_over", board.Id,
                previous is null ? $"Editor: {User.Identity!.Name}" : $"New editor: {User.Identity!.Name}; previous account: {previous}", ct);
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            TempData["StatusMessage"] = exception is DbUpdateConcurrencyException
                ? "Another administrator changed the board editor first. The latest board has been loaded."
                : exception.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReleaseEditingAsync(Guid id, CancellationToken ct)
    {
        var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct); if (board is null) return NotFound();
        try { board.ReleaseEditing(AdminId, time.GetUtcNow()); await db.SaveChangesAsync(ct); await WriteAudit("board.editing_released", board.Id, $"Released by {User.Identity!.Name}", ct); }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear(); TempData["StatusMessage"] = exception is DbUpdateConcurrencyException ? "Editing control changed before it could be released. The latest board has been loaded." : exception.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCreateTileAsync(Guid id, CancellationToken ct)
    {
        var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct); if (board is null) return NotFound();
        if (board.State != BoardState.Draft || TileDraft.Position < 0 || TileDraft.Position >= board.Rows * board.Columns) return BadRequest();
        if (await db.BoardTiles.AnyAsync(x => x.BoardId == board.Id && x.RowIndex == TileDraft.Position / board.Columns && x.ColumnIndex == TileDraft.Position % board.Columns, ct)) { TempData["StatusMessage"] = "That board position is no longer empty."; return RedirectToPage(new { id }); }
        TileDraft.Requirements = TileDraft.Requirements.Where(x => !string.IsNullOrWhiteSpace(x.Description) || x.BossIds.Count > 0 || x.DropIds.Count > 0).ToList();
        if (TileDraft.Requirements.Count == 0) ModelState.AddModelError(string.Empty, "Add at least one requirement.");
        foreach (var requirement in TileDraft.Requirements)
        {
            if (requirement.Target < 1) ModelState.AddModelError(string.Empty, "Every requirement needs a quantity of at least 1.");
            if (requirement.DropWeights.Any(x => x.Value < 1)) ModelState.AddModelError(string.Empty, "Every drop weight must be at least 1.");
            if (!requirement.IsManual && requirement.BossIds.Count == 0) ModelState.AddModelError(string.Empty, "Choose at least one boss for each collect-drops requirement.");
            if (!requirement.IsManual && requirement.DropIds.Count == 0) ModelState.AddModelError(string.Empty, "Choose at least one eligible drop for each collect-drops requirement.");
            if (requirement.IsManual && string.IsNullOrWhiteSpace(requirement.Description)) ModelState.AddModelError(string.Empty, "Describe the challenge requirements.");
        }
        if (!ModelState.IsValid) { TempData["StatusMessage"] = string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)); return RedirectToPage(new { id }); }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id });
        var selectedBossIds = TileDraft.Requirements.SelectMany(x => x.BossIds).Distinct().ToList();
        var selectedBosses = await db.BossActivities.Where(x => selectedBossIds.Contains(x.Id)).OrderBy(x => x.Name).ToListAsync(ct);
        var name = string.IsNullOrWhiteSpace(TileDraft.Name) ? DefaultTileName(selectedBosses.Select(x => x.Name)) : TileDraft.Name.Trim();
        var requirementDescriptions = TileDraft.Requirements.Select(RequirementDescription).ToList();
        var description = string.IsNullOrWhiteSpace(TileDraft.Description) ? string.Join("; ", requirementDescriptions) : TileDraft.Description.Trim();
        var objectiveType = TileDraft.Requirements.All(x => x.IsManual) ? ObjectiveType.Manual : ObjectiveType.DropRequirements;
        var template = new TileTemplate(Guid.NewGuid(), name, description, objectiveType, TileDraft.EvidenceInstructions?.Trim() ?? string.Empty, TileDraft.ManualEhb, Clean(TileDraft.ImageUrl));
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
        await PlaceTemplateAsync(board, template, requirements, TileDraft.Position, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); await WriteAudit("board.tile_created", board.Id, name, ct);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostEditTileAsync(Guid id, CancellationToken ct)
    {
        var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct); if (board is null) return NotFound();
        if (board.State != BoardState.Draft || TileDraft.TileId is null) return BadRequest();
        var tile = await db.BoardTiles.SingleOrDefaultAsync(x => x.BoardId == board.Id && x.Id == TileDraft.TileId, ct); if (tile is null) return NotFound();
        TileDraft.Requirements = TileDraft.Requirements.Where(x => !string.IsNullOrWhiteSpace(x.Description) || x.BossIds.Count > 0 || x.DropIds.Count > 0).ToList();
        if (TileDraft.Requirements.Count == 0) ModelState.AddModelError(string.Empty, "Add at least one requirement.");
        foreach (var requirement in TileDraft.Requirements)
        {
            if (requirement.Target < 1) ModelState.AddModelError(string.Empty, "Every requirement needs a quantity of at least 1.");
            if (requirement.DropWeights.Any(x => x.Value < 1)) ModelState.AddModelError(string.Empty, "Every drop weight must be at least 1.");
            if (!requirement.IsManual && requirement.BossIds.Count == 0) ModelState.AddModelError(string.Empty, "Choose at least one boss for each collect-drops requirement.");
            if (!requirement.IsManual && requirement.DropIds.Count == 0) ModelState.AddModelError(string.Empty, "Choose at least one eligible drop for each collect-drops requirement.");
            if (requirement.IsManual && string.IsNullOrWhiteSpace(requirement.Description)) ModelState.AddModelError(string.Empty, "Describe the challenge requirements.");
        }
        if (!ModelState.IsValid) { TempData["StatusMessage"] = string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)); return RedirectToPage(new { id }); }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id });
        var selectedBossIds = TileDraft.Requirements.SelectMany(x => x.BossIds).Distinct().ToList();
        var selectedBosses = await db.BossActivities.Where(x => selectedBossIds.Contains(x.Id)).OrderBy(x => x.Name).ToListAsync(ct);
        var name = string.IsNullOrWhiteSpace(TileDraft.Name) ? DefaultTileName(selectedBosses.Select(x => x.Name)) : TileDraft.Name.Trim();
        var requirementDescriptions = TileDraft.Requirements.Select(RequirementDescription).ToList();
        var description = string.IsNullOrWhiteSpace(TileDraft.Description) ? string.Join("; ", requirementDescriptions) : TileDraft.Description.Trim();
        var objectiveType = TileDraft.Requirements.All(x => x.IsManual) ? ObjectiveType.Manual : ObjectiveType.DropRequirements;
        var template = await db.TileTemplates.SingleAsync(x => x.Id == tile.TileTemplateId, ct);
        template.Update(name, description, objectiveType, TileDraft.EvidenceInstructions?.Trim() ?? string.Empty, TileDraft.ManualEhb, Clean(TileDraft.ImageUrl));

        var oldTemplateRequirements = await db.TileTemplateRequirements.Where(x => x.TileTemplateId == template.Id).ToListAsync(ct);
        var oldTemplateRequirementIds = oldTemplateRequirements.Select(x => x.Id).ToList();
        db.TemplateRequirementBosses.RemoveRange(await db.TemplateRequirementBosses.Where(x => oldTemplateRequirementIds.Contains(x.RequirementId)).ToListAsync(ct));
        db.TemplateRequirementDrops.RemoveRange(await db.TemplateRequirementDrops.Where(x => oldTemplateRequirementIds.Contains(x.RequirementId)).ToListAsync(ct));
        db.TileTemplateRequirements.RemoveRange(oldTemplateRequirements);

        var oldSnapshots = await db.BoardRequirementSnapshots.Where(x => x.BoardTileId == tile.Id).ToListAsync(ct);
        var oldSnapshotIds = oldSnapshots.Select(x => x.Id).ToList();
        db.BoardRequirementBossSnapshots.RemoveRange(await db.BoardRequirementBossSnapshots.Where(x => oldSnapshotIds.Contains(x.RequirementId)).ToListAsync(ct));
        db.BoardRequirementDropSnapshots.RemoveRange(await db.BoardRequirementDropSnapshots.Where(x => oldSnapshotIds.Contains(x.RequirementId)).ToListAsync(ct));
        db.BoardRequirementSnapshots.RemoveRange(oldSnapshots);
        await db.SaveChangesAsync(ct);

        var estimates = new List<decimal?>();
        for (var index = 0; index < TileDraft.Requirements.Count; index++)
        {
            var input = TileDraft.Requirements[index];
            var requirement = new TileTemplateRequirement(Guid.NewGuid(), template.Id, index + 1, input.Target, input.DuplicatesAllowed, input.HasHigherWeights, requirementDescriptions[index], input.IsManual);
            db.TileTemplateRequirements.Add(requirement);
            foreach (var bossId in input.BossIds.Distinct()) db.TemplateRequirementBosses.Add(new TemplateRequirementBoss(Guid.NewGuid(), requirement.Id, bossId));
            foreach (var dropId in input.DropIds.Distinct()) db.TemplateRequirementDrops.Add(new TemplateRequirementDrop(Guid.NewGuid(), requirement.Id, dropId, input.DuplicatesAllowed ? null : 1, input.WeightFor(dropId)));

            var snapshot = new BoardRequirementSnapshot(Guid.NewGuid(), tile.Id, index + 1, input.Target, input.DuplicatesAllowed, input.HasHigherWeights, requirementDescriptions[index], input.IsManual);
            db.BoardRequirementSnapshots.Add(snapshot);
            foreach (var boss in selectedBosses.Where(x => input.BossIds.Contains(x.Id))) db.BoardRequirementBossSnapshots.Add(new BoardRequirementBossSnapshot(Guid.NewGuid(), snapshot.Id, boss.Id, boss.Name, boss.EfficientCompletionsPerHour));
            var selectedDrops = await (from drop in db.SourceDrops where input.DropIds.Contains(drop.Id) join boss in db.BossActivities on drop.BossActivityId equals boss.Id join item in db.CatalogueItems on drop.ItemId equals item.Id select new { drop, boss, item }).ToListAsync(ct);
            foreach (var value in selectedDrops) db.BoardRequirementDropSnapshots.Add(new BoardRequirementDropSnapshot(Guid.NewGuid(), snapshot.Id, value.drop.Id, value.boss.Name, value.item.Name, value.drop.DisplayRate, value.drop.NumericProbability, input.DuplicatesAllowed ? null : 1, value.drop.DefaultEhbEstimate, input.WeightFor(value.drop.Id)));
            estimates.Add(input.IsManual ? null : EhbCalculator.CalculateDropRequirement(input.Target, selectedDrops.Select(x => new EligibleDropRate(x.boss.EfficientCompletionsPerHour, x.drop.NumericProbability, x.drop.ItemId, x.boss.Id, input.WeightFor(x.drop.Id), x.drop.RollsPerCompletion, x.drop.RollGroup)), input.DuplicatesAllowed));
        }
        var ehb = EhbCalculator.SumRequirements(estimates, TileDraft.ManualEhb);
        board.SetTotalEhb(Math.Max(0, board.TotalEhbEstimate - tile.EstimatedEhbSnapshot + ehb));
        tile.UpdateContent(name, description, TileDraft.EvidenceInstructions?.Trim() ?? string.Empty, ehb, template.ImageUrl);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); await WriteAudit("board.tile_edited", board.Id, name, ct);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostMoveAsync(Guid id, Guid sourceId, int targetPosition, CancellationToken ct)
    {
        var isInlineRequest = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct); if (board is null) return NotFound();
        if (board.State != BoardState.Draft || targetPosition < 0 || targetPosition >= board.Rows * board.Columns) return BadRequest();
        if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id });
        var source = await db.BoardTiles.SingleOrDefaultAsync(x => x.BoardId == board.Id && x.Id == sourceId, ct); if (source is null) return NotFound();
        var targetRow = targetPosition / board.Columns; var targetColumn = targetPosition % board.Columns;
        var target = await db.BoardTiles.SingleOrDefaultAsync(x => x.BoardId == board.Id && x.RowIndex == targetRow && x.ColumnIndex == targetColumn, ct);
        if (target?.Id == source.Id) return isInlineRequest ? new JsonResult(new { success = true, boardVersion = board.Version }) : RedirectToPage(new { id });
        var oldRow = source.RowIndex; var oldColumn = source.ColumnIndex;
        source.Move(-1, -1); await db.SaveChangesAsync(ct);
        if (target is not null) { target.Move(oldRow, oldColumn); await db.SaveChangesAsync(ct); }
        source.Move(targetRow, targetColumn); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        await WriteAudit(target is null ? "board.tile_moved" : "board.tiles_swapped", board.Id, source.NameSnapshot, ct);
        return isInlineRequest ? new JsonResult(new { success = true, boardVersion = board.Version }) : RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostResizeAsync(Guid id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct); var board = await db.Boards.SingleAsync(x => x.EventId == id, ct);
        var tiles = await db.BoardTiles.Where(x => x.BoardId == board.Id).OrderBy(x => x.RowIndex).ThenBy(x => x.ColumnIndex).ToListAsync(ct);
        try
        {
            board.Resize(Rows, Columns, tiles.Count); if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id }); for (var i = 0; i < tiles.Count; i++) tiles[i].Move(-1, -i - 1); await db.SaveChangesAsync(ct);
            for (var i = 0; i < tiles.Count; i++) tiles[i].Move(i / Columns, i % Columns); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            await WriteAudit("board.resized", board.Id, $"{Rows}x{Columns}", ct);
        }
        catch (InvalidOperationException exception) { TempData["StatusMessage"] = exception.Message; }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostTeamSizeAsync(Guid id, int expectedTeamSize, CancellationToken ct)
    {
        if (expectedTeamSize is < 1 or > 100) { TempData["StatusMessage"] = "Expected team size must be between 1 and 100."; return RedirectToPage(new { id }); }
        var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct); if (board is null) return NotFound();
        if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id });
        var bingoEvent = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct); if (bingoEvent is null) return NotFound();
        bingoEvent.ConfigurePlanning(bingoEvent.PublicRules, bingoEvent.BuyInDescription, bingoEvent.PrizeDescription, bingoEvent.ExpectedTeamCount, expectedTeamSize, bingoEvent.ExpectedBoardRows, bingoEvent.ExpectedBoardColumns);
        await db.SaveChangesAsync(ct); await WriteAudit("board.expected_team_size_changed", board.Id, expectedTeamSize.ToString(CultureInfo.InvariantCulture), ct); return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPublishAsync(Guid id, CancellationToken ct)
    {
        var board = await db.Boards.SingleAsync(x => x.EventId == id, ct); var tiles = await db.BoardTiles.Where(x => x.BoardId == board.Id).ToListAsync(ct); var errors = new List<string>();
        if (tiles.Count != board.Rows * board.Columns) errors.Add("Every board position must contain a tile.");
        var tileIds = tiles.Select(x => x.Id).ToList(); var requirements = await db.BoardRequirementSnapshots.Where(x => tileIds.Contains(x.BoardTileId)).ToListAsync(ct);
        var dropRequirementIds = requirements.Where(x => !x.ManualObjective).Select(x => x.Id).ToList(); var withDrops = await db.BoardRequirementDropSnapshots.Where(x => dropRequirementIds.Contains(x.RequirementId)).Select(x => x.RequirementId).Distinct().ToListAsync(ct);
        foreach (var tile in tiles) { var tileRequirements = requirements.Where(x => x.BoardTileId == tile.Id).ToList(); if (tileRequirements.Count == 0) errors.Add($"{tile.NameSnapshot} has no requirements."); if (tile.EstimatedEhbSnapshot <= 0) errors.Add($"{tile.NameSnapshot} needs calculable EHB data or a manual EHB estimate."); foreach (var requirement in tileRequirements.Where(x => !x.ManualObjective && !withDrops.Contains(x.Id))) errors.Add($"{tile.NameSnapshot} has a drop requirement with no eligible drops."); }
        if (errors.Count > 0) { TempData["StatusMessage"] = string.Join(" ", errors.Distinct()); return RedirectToPage(new { id }); }
        board.Publish(time.GetUtcNow()); if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id }); board.ReleaseEditing(AdminId, time.GetUtcNow()); await db.SaveChangesAsync(ct); await WriteAudit("board.published", board.Id, $"{board.Rows}x{board.Columns}; {tiles.Count} tiles", ct); return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRemoveAsync(Guid id, Guid tileId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct); var board = await db.Boards.SingleAsync(x => x.EventId == id, ct); if (board.State != BoardState.Draft) return BadRequest(); if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id });
        var tile = await db.BoardTiles.SingleOrDefaultAsync(x => x.BoardId == board.Id && x.Id == tileId, ct); if (tile is null) return NotFound();
        var requirements = await db.BoardRequirementSnapshots.Where(x => x.BoardTileId == tile.Id).ToListAsync(ct); var ids = requirements.Select(x => x.Id).ToList();
        db.BoardRequirementBossSnapshots.RemoveRange(await db.BoardRequirementBossSnapshots.Where(x => ids.Contains(x.RequirementId)).ToListAsync(ct)); db.BoardRequirementDropSnapshots.RemoveRange(await db.BoardRequirementDropSnapshots.Where(x => ids.Contains(x.RequirementId)).ToListAsync(ct)); db.BoardRequirementSnapshots.RemoveRange(requirements); db.BoardTiles.Remove(tile);
        board.SetTotalEhb(Math.Max(0, board.TotalEhbEstimate - tile.EstimatedEhbSnapshot)); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); await WriteAudit("board.tile_removed", board.Id, tile.NameSnapshot, ct); return RedirectToPage(new { id });
    }

    private async Task PlaceTemplateAsync(Board board, TileTemplate template, IReadOnlyList<TileTemplateRequirement> requirements, int position, CancellationToken ct)
    {
        var estimates = new List<decimal?>();
        foreach (var requirement in requirements)
        {
            if (requirement.ManualObjective) { estimates.Add(null); continue; }
            var rateRows = await (from link in db.TemplateRequirementDrops where link.RequirementId == requirement.Id join drop in db.SourceDrops on link.SourceDropId equals drop.Id join boss in db.BossActivities on drop.BossActivityId equals boss.Id select new { link, drop, boss }).ToListAsync(ct);
            var rates = rateRows.Select(x => new EligibleDropRate(x.boss.EfficientCompletionsPerHour, x.drop.NumericProbability, x.drop.ItemId, x.boss.Id, x.link.CreditedWeight, x.drop.RollsPerCompletion, x.drop.RollGroup));
            estimates.Add(EhbCalculator.CalculateDropRequirement(requirement.TargetContribution, rates, requirement.DuplicatesAllowed));
        }
        var ehb = EhbCalculator.SumRequirements(estimates, template.ManualEhbOverride); var boardTile = new BoardTile(Guid.NewGuid(), board.Id, template.Id, position / board.Columns, position % board.Columns, template.Name, template.Description, template.EvidenceInstructions, ehb, template.ImageUrl); db.BoardTiles.Add(boardTile);
        foreach (var requirement in requirements)
        {
            var snapshot = new BoardRequirementSnapshot(Guid.NewGuid(), boardTile.Id, requirement.Position, requirement.TargetContribution, requirement.DuplicatesAllowed, requirement.AllowHigherWeightings, requirement.Description, requirement.ManualObjective); db.BoardRequirementSnapshots.Add(snapshot);
            var bosses = await (from link in db.TemplateRequirementBosses where link.RequirementId == requirement.Id join boss in db.BossActivities on link.BossActivityId equals boss.Id select boss).ToListAsync(ct); foreach (var boss in bosses) db.BoardRequirementBossSnapshots.Add(new BoardRequirementBossSnapshot(Guid.NewGuid(), snapshot.Id, boss.Id, boss.Name, boss.EfficientCompletionsPerHour));
            var drops = await (from link in db.TemplateRequirementDrops where link.RequirementId == requirement.Id join drop in db.SourceDrops on link.SourceDropId equals drop.Id join boss in db.BossActivities on drop.BossActivityId equals boss.Id join item in db.CatalogueItems on drop.ItemId equals item.Id select new { link, drop, boss, item }).ToListAsync(ct); foreach (var value in drops) db.BoardRequirementDropSnapshots.Add(new BoardRequirementDropSnapshot(Guid.NewGuid(), snapshot.Id, value.drop.Id, value.boss.Name, value.item.Name, value.drop.DisplayRate, value.drop.NumericProbability, value.link.MaximumContribution, value.drop.DefaultEhbEstimate, value.link.CreditedWeight));
        }
        board.SetTotalEhb(board.TotalEhbEstimate + ehb);
    }

    private async Task<bool> Load(Guid id, CancellationToken ct)
    {
        var bingoEvent = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (bingoEvent is null) return false; EventName = bingoEvent.Name;
        Bosses = await db.BossActivities.AsNoTracking().Where(x => x.Active).OrderBy(x => x.Name).Select(x => new BossView(x.Id, x.Name, x.Category, x.EfficientCompletionsPerHour)).ToListAsync(ct);
        Drops = await (from drop in db.SourceDrops.AsNoTracking()
                       join boss in db.BossActivities on drop.BossActivityId equals boss.Id
                       join item in db.CatalogueItems on drop.ItemId equals item.Id
                       where drop.Active && boss.Active && item.Active
                       orderby boss.Name, item.Name
                       select new DropView(drop.Id, boss.Id, boss.Name, item.Name, drop.DisplayRate))
            .ToListAsync(ct);
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct); if (board is null) { Rows = bingoEvent.ExpectedBoardRows ?? 5; Columns = bingoEvent.ExpectedBoardColumns ?? 5; return true; }
        if (board.HasActiveEditor(time.GetUtcNow()))
        {
            BoardEditorAccountId = board.EditorAccountId;
            BoardEditorLeaseExpiresAt = board.EditorLeaseExpiresAt;
            BoardEditorName = await db.Accounts.AsNoTracking().Where(x => x.Id == board.EditorAccountId).Select(x => x.Username).SingleOrDefaultAsync(ct);
            CanEditBoard = board.EditorAccountId == CurrentAccountId;
        }
        Rows = board.Rows; Columns = board.Columns; BoardVersion = board.Version; BoardView = new(board.Rows, board.Columns, board.State, board.TotalEhbEstimate, board.Version);
        Tiles = await db.BoardTiles.AsNoTracking().Where(x => x.BoardId == board.Id).Select(x => new TileView(x.Id, x.RowIndex * board.Columns + x.ColumnIndex, x.NameSnapshot, x.DescriptionSnapshot, x.EvidenceInstructionsSnapshot, x.EstimatedEhbSnapshot)).ToListAsync(ct);
        var tileIds = Tiles.Select(x => x.Id).ToList();
        var editorRequirements = await db.BoardRequirementSnapshots.AsNoTracking().Where(x => tileIds.Contains(x.BoardTileId)).OrderBy(x => x.Position).ToListAsync(ct);
        var editorRequirementIds = editorRequirements.Select(x => x.Id).ToList();
        var editorBosses = await db.BoardRequirementBossSnapshots.AsNoTracking().Where(x => editorRequirementIds.Contains(x.RequirementId)).ToListAsync(ct);
        var editorDrops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => editorRequirementIds.Contains(x.RequirementId)).ToListAsync(ct);
        var boardTilesForEditors = await db.BoardTiles.AsNoTracking().Where(x => x.BoardId == board.Id).ToListAsync(ct);
        var editorTemplateIds = boardTilesForEditors.Select(x => x.TileTemplateId).Distinct().ToList();
        var manualEhbByTemplate = await db.TileTemplates.AsNoTracking().Where(x => editorTemplateIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ManualEhbOverride, ct);
        TileEditors = boardTilesForEditors.Select(tile => new TileEditorView(tile.Id, tile.NameSnapshot, tile.DescriptionSnapshot, tile.EvidenceInstructionsSnapshot, tile.ImageUrlSnapshot, manualEhbByTemplate.GetValueOrDefault(tile.TileTemplateId), editorRequirements.Where(r => r.BoardTileId == tile.Id).Select(r => new RequirementEditorView(r.ManualObjective ? "challenge" : "drops", r.Description, r.TargetContribution, r.DuplicatesAllowed, editorBosses.Where(b => b.RequirementId == r.Id).Select(b => b.BossActivityId).ToList(), editorDrops.Where(d => d.RequirementId == r.Id).Select(d => d.SourceDropId).ToList(), editorDrops.Where(d => d.RequirementId == r.Id).ToDictionary(d => d.SourceDropId, d => d.CreditedWeight), editorDrops.Where(d => d.RequirementId == r.Id).OrderBy(d => d.BossName).ThenBy(d => d.ItemName).Select(d => new RequirementDropView(d.SourceDropId, d.BossName, d.ItemName, d.DisplayRate, d.CreditedWeight)).ToList())).ToList())).ToList();
        var lines = new List<LineView>(); for (var row = 0; row < board.Rows; row++) lines.Add(new($"Row {row + 1}", "row", row, Tiles.Where(x => x.Position / board.Columns == row).Sum(x => x.Ehb))); for (var column = 0; column < board.Columns; column++) lines.Add(new($"Column {column + 1}", "column", column, Tiles.Where(x => x.Position % board.Columns == column).Sum(x => x.Ehb))); Lines = lines; BalanceSpread = lines.Count == 0 ? 0 : lines.Max(x => x.Ehb) - lines.Min(x => x.Ehb);
        var durationDays = Math.Max(0.5m, (decimal)(bingoEvent.EventEndsAt - bingoEvent.EventStartsAt).TotalHours / 24m); var teamSize = bingoEvent.ExpectedTeamSize; var total = Tiles.Sum(x => x.Ehb); var populatedLines = lines.Where(x => x.Ehb > 0).ToList();
        Statistics = new(total, teamSize, teamSize is > 0 ? total / teamSize.Value : null, teamSize is > 0 ? total / teamSize.Value / durationDays : null, Tiles.Count == 0 ? 0 : total / Tiles.Count, populatedLines.Count == 0 ? 0 : populatedLines.Min(x => x.Ehb), populatedLines.Count == 0 ? 0 : populatedLines.Max(x => x.Ehb), Tiles.Count(x => x.Ehb <= 0), durationDays);
        var eventTeams = await db.Teams.AsNoTracking().Where(x => x.EventId == id && x.Active).OrderBy(x => x.DraftPosition).ThenBy(x => x.Name).ToListAsync(ct);
        if (eventTeams.Count > 0)
        {
            var teamIdsForWorkload = eventTeams.Select(x => x.Id).ToList();
            var rosterSizes = await db.TeamMemberships.AsNoTracking().Where(x => teamIdsForWorkload.Contains(x.TeamId) && x.LeftAt == null).GroupBy(x => x.TeamId).ToDictionaryAsync(x => x.Key, x => x.Count(), ct);
            var draft = await db.DraftSessions.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct);
            TeamWorkloads = eventTeams.Select(team =>
            {
                var actualSize = rosterSizes.GetValueOrDefault(team.Id);
                var sizeUsed = team.FormationType == TeamFormationType.Drafted && draft?.State != DraftState.Finalized ? draft?.TargetTeamSize ?? actualSize : actualSize;
                return new TeamWorkloadView(team.Name, team.FormationType, actualSize, sizeUsed > 0 ? sizeUsed : null, sizeUsed > 0 ? total / sizeUsed : null);
            }).ToList();
        }
        return true;
    }

    private Guid AdminId => User.GetAccountId()!.Value;
    private async Task WriteAudit(string action, Guid boardId, string details, CancellationToken ct)
    {
        await audit.WriteAsync(User.GetAccountId(), User.Identity!.Name!, action, "board", boardId.ToString(), details, ct);
        var eventId = await db.Boards.AsNoTracking().Where(x => x.Id == boardId).Select(x => x.EventId).SingleAsync(ct);
        await collaboration.NotifyBoardChangedAsync(eventId, ct);
    }
    private async Task<bool> TryClaimBoardAsync(Board board, CancellationToken ct)
    {
        try { board.RenewEditing(AdminId, time.GetUtcNow(), BoardEditingLease.Duration); }
        catch (InvalidOperationException exception) { TempData["StatusMessage"] = exception.Message; db.ChangeTracker.Clear(); return false; }
        db.Entry(board).Property(x => x.Version).OriginalValue = BoardVersion;
        board.MarkChanged();
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException)
        {
            TempData["StatusMessage"] = "This board changed after you opened it. Your change was not saved. The latest board has been loaded.";
            db.ChangeTracker.Clear();
            return false;
        }
    }
    private static string DefaultTileName(IEnumerable<string> names) { var list = names.Distinct(StringComparer.OrdinalIgnoreCase).ToList(); return list.Count switch { 0 => "New tile", 1 => list[0], _ => string.Join(" + ", list) }; }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string RequirementDescription(RequirementInput input) => input.IsManual ? input.Description!.Trim() : $"Collect {input.Target} eligible drop{(input.Target == 1 ? string.Empty : "s")}";

    public sealed class TileDraftInput { public Guid? TileId { get; set; } public int Position { get; set; } public string? Name { get; set; } public string? Description { get; set; } [Url, StringLength(2000), Display(Name = "Custom tile image URL")] public string? ImageUrl { get; set; } [Range(0, 100000)] public decimal? ManualEhb { get; set; } public string? EvidenceInstructions { get; set; } public List<RequirementInput> Requirements { get; set; } = [new()]; }
    public sealed class RequirementInput { public string Kind { get; set; } = "drops"; public string? Description { get; set; } [Range(1, 10000)] public int Target { get; set; } = 1; public bool DuplicatesAllowed { get; set; } = true; public Dictionary<Guid, int> DropWeights { get; set; } = []; public List<Guid> BossIds { get; set; } = []; public List<Guid> DropIds { get; set; } = []; public bool IsManual => string.Equals(Kind, "challenge", StringComparison.OrdinalIgnoreCase); public int WeightFor(Guid dropId) => Math.Max(1, DropWeights.GetValueOrDefault(dropId, 1)); public bool HasHigherWeights => DropIds.Any(x => WeightFor(x) > 1); }
    public sealed record BoardDetails(int Rows, int Columns, BoardState State, decimal TotalEhb, long Version);
    public sealed record BoardStatistics(decimal TotalEhb, int? TeamSize, decimal? EhbPerPlayer, decimal? EhbPerPlayerPerDay, decimal AverageTileEhb, decimal LowestLineEhb, decimal HighestLineEhb, int MissingEhbTiles, decimal DurationDays);
    public sealed record TileView(Guid Id, int Position, string Name, string Description, string EvidenceInstructions, decimal Ehb);
    public sealed record LineView(string Key, string Kind, int Index, decimal Ehb);
    public sealed record BossView(Guid Id, string Name, string Category, decimal? EfficientRate);
    public sealed record DropView(Guid Id, Guid BossId, string BossName, string ItemName, string Rate);
    public sealed record TileEditorView(Guid Id, string Name, string Description, string EvidenceInstructions, string? ImageUrl, decimal? ManualEhb, IReadOnlyList<RequirementEditorView> Requirements);
    public sealed record RequirementEditorView(string Kind, string Description, int Target, bool DuplicatesAllowed, IReadOnlyList<Guid> BossIds, IReadOnlyList<Guid> DropIds, IReadOnlyDictionary<Guid, int> DropWeights, IReadOnlyList<RequirementDropView> Drops);
    public sealed record RequirementDropView(Guid Id, string BossName, string ItemName, string DisplayRate, int CreditedWeight);
    public sealed record TeamWorkloadView(string TeamName, TeamFormationType FormationType, int ActualRosterSize, int? SizeUsed, decimal? EhbPerPlayer);
}
