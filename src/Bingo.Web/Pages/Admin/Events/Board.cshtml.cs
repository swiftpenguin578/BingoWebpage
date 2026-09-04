using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
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
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Npgsql;

namespace Bingo.Web.Pages.Admin.Events;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class BoardModel(ApplicationDbContext db, TimeProvider time, IAuditWriter audit, IAdminCollaborationNotifier collaboration, IEvidenceStorage storage, IStringLocalizer<SharedResource>? text = null) : PageModel
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
    public IReadOnlyList<string> ReadinessWarnings { get; private set; } = [];
    public Guid CurrentAccountId { get; private set; }
    public bool CanEditBoard { get; private set; }
    public Guid? BoardEditorAccountId { get; private set; }
    public string? BoardEditorName { get; private set; }
    public DateTimeOffset? BoardEditorLeaseExpiresAt { get; private set; }
    public bool DraftFinalized { get; private set; }
    public EventState EventState { get; private set; }

    [BindProperty, Range(1, 8)] public int Rows { get; set; } = 5;
    [BindProperty, Range(1, 8)] public int Columns { get; set; } = 5;
    [BindProperty] public long BoardVersion { get; set; }
    [BindProperty] public TileDraftInput TileDraft { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        CurrentAccountId = User.GetAccountId()!.Value;
        return await Load(id, ct) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostCreateAsync(Guid id, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await Load(id, ct) ? Page() : NotFound();
        if (await db.Boards.AnyAsync(x => x.EventId == id, ct))
        {
            SetStatus(Localize("A board already exists for this event."), UiMessageType.Warning);
            return RedirectToPage(new { id });
        }
        var board = new Board(Guid.NewGuid(), id, "Main board", Rows, Columns);
        board.AcquireEditing(AdminId, time.GetUtcNow(), BoardEditingLease.Duration);
        db.Boards.Add(board); await db.SaveChangesAsync(ct); await WriteAudit("board.created", board.Id, $"{Rows}x{Columns}", ct);
        SetStatus(Localize("Board created."), UiMessageType.Success);
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

    public async Task<IActionResult> OnPostReleaseEditingAsync(Guid id, CancellationToken ct)
    {
        var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct); if (board is null) return NotFound();
        try { board.ReleaseEditing(AdminId, time.GetUtcNow()); await db.SaveChangesAsync(ct); await WriteAudit("board.editing_released", board.Id, $"Released by {User.Identity!.Name}", ct); SetStatus(Localize("Board editing control released."), UiMessageType.Success); }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear(); SetStatus(exception is DbUpdateConcurrencyException ? Localize("Editing control changed before it could be released. The latest board has been loaded.") : Localize("The board editing control could not be released."), UiMessageType.Warning);
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCreateTileAsync(Guid id, CancellationToken ct)
    {
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
            if (requirement.DropWeights.Any(x => x.Value < 1)) ModelState.AddModelError(string.Empty, Localize("Every drop weight must be at least 1."));
            if (!requirement.IsManual && requirement.BossIds.Count == 0) ModelState.AddModelError(string.Empty, Localize("Choose at least one boss for each collect-drops requirement."));
            if (!requirement.IsManual && requirement.DropIds.Count == 0) ModelState.AddModelError(string.Empty, Localize("Choose at least one eligible drop for each collect-drops requirement."));
            if (requirement.IsManual && string.IsNullOrWhiteSpace(requirement.Description)) ModelState.AddModelError(string.Empty, Localize("Describe the challenge requirements."));
        }
        if (TileDraft.Requirements.Any(x => x.IsManual) && TileDraft.ManualEhb is not > 0) ModelState.AddModelError(string.Empty, Localize("A custom challenge needs an explicit manual EHB estimate."));
        if (!ModelState.IsValid) { SetStatus(string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)), UiMessageType.Warning); return RedirectToPage(new { id }); }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!PrepareCompetitiveEdit(board)) return RedirectToPage(new { id });
        if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id });
        var selectedBossIds = TileDraft.Requirements.SelectMany(x => x.BossIds).Distinct().ToList();
        var selectedBosses = await db.BossActivities.Where(x => selectedBossIds.Contains(x.Id)).OrderBy(x => x.Name).ToListAsync(ct);
        var name = string.IsNullOrWhiteSpace(TileDraft.Name) ? DefaultTileName(selectedBosses.Select(x => x.Name)) : TileDraft.Name.Trim();
        var requirementDescriptions = TileDraft.Requirements.Select(RequirementDescription).ToList();
        var description = string.IsNullOrWhiteSpace(TileDraft.Description) ? string.Join("; ", requirementDescriptions) : TileDraft.Description.Trim();
        var objectiveType = TileDraft.Requirements.All(x => x.IsManual) ? ObjectiveType.Manual : ObjectiveType.DropRequirements;
        var template = new TileTemplate(Guid.NewGuid(), name, description, objectiveType, string.Empty, TileDraft.ManualEhb);
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
        await ReplaceTileImageAsync(id, tile, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); await WriteAudit("board.tile_created", board.Id, name, ct);
        SetSuccessIfMissing(Localize("Tile created."));
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostEditTileAsync(Guid id, CancellationToken ct)
    {
        var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct); if (board is null) return NotFound();
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
            if (requirement.DropWeights.Any(x => x.Value < 1)) ModelState.AddModelError(string.Empty, Localize("Every drop weight must be at least 1."));
            if (!requirement.IsManual && requirement.BossIds.Count == 0) ModelState.AddModelError(string.Empty, Localize("Choose at least one boss for each collect-drops requirement."));
            if (!requirement.IsManual && requirement.DropIds.Count == 0) ModelState.AddModelError(string.Empty, Localize("Choose at least one eligible drop for each collect-drops requirement."));
            if (requirement.IsManual && string.IsNullOrWhiteSpace(requirement.Description)) ModelState.AddModelError(string.Empty, Localize("Describe the challenge requirements."));
        }
        if (TileDraft.Requirements.Any(x => x.IsManual) && TileDraft.ManualEhb is not > 0) ModelState.AddModelError(string.Empty, Localize("A custom challenge needs an explicit manual EHB estimate."));
        if (!ModelState.IsValid) { SetStatus(string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)), UiMessageType.Warning); return RedirectToPage(new { id }); }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        if (!PrepareCompetitiveEdit(board)) return RedirectToPage(new { id });
        if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id });
        var selectedBossIds = TileDraft.Requirements.SelectMany(x => x.BossIds).Distinct().ToList();
        var selectedBosses = await db.BossActivities.Where(x => selectedBossIds.Contains(x.Id)).OrderBy(x => x.Name).ToListAsync(ct);
        var name = string.IsNullOrWhiteSpace(TileDraft.Name) ? DefaultTileName(selectedBosses.Select(x => x.Name)) : TileDraft.Name.Trim();
        var requirementDescriptions = TileDraft.Requirements.Select(RequirementDescription).ToList();
        var description = string.IsNullOrWhiteSpace(TileDraft.Description) ? string.Join("; ", requirementDescriptions) : TileDraft.Description.Trim();
        var objectiveType = TileDraft.Requirements.All(x => x.IsManual) ? ObjectiveType.Manual : ObjectiveType.DropRequirements;
        var template = await db.TileTemplates.SingleAsync(x => x.Id == tile.TileTemplateId, ct);
        template.Update(name, description, objectiveType, string.Empty, TileDraft.ManualEhb);

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
        tile.UpdateContent(name, description, string.Empty, ehb);
        await ReplaceTileImageAsync(id, tile, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); await WriteAudit("board.tile_edited", board.Id, name, ct);
        SetSuccessIfMissing(Localize("Tile updated."));
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostMoveAsync(Guid id, Guid sourceId, int targetPosition, CancellationToken ct)
    {
        var isInlineRequest = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct);
        if (board is null) return isInlineRequest ? MoveFailure(Localize("The board could not be found."), UiMessageType.Error) : NotFound();
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
        var oldRow = source.RowIndex; var oldColumn = source.ColumnIndex;
        source.Move(-1, -1); await db.SaveChangesAsync(ct);
        if (target is not null) { target.Move(oldRow, oldColumn); await db.SaveChangesAsync(ct); }
        source.Move(targetRow, targetColumn); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        await WriteAudit(target is null ? "board.tile_moved" : "board.tiles_swapped", board.Id, source.NameSnapshot, ct);
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
            if (!PrepareCompetitiveEdit(board)) return RedirectToPage(new { id });
            board.Resize(Rows, Columns, tiles.Count); if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id }); for (var i = 0; i < tiles.Count; i++) tiles[i].Move(-1, -i - 1); await db.SaveChangesAsync(ct);
            for (var i = 0; i < tiles.Count; i++) tiles[i].Move(i / Columns, i % Columns); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            await WriteAudit("board.resized", board.Id, $"{Rows}x{Columns}", ct);
        }
        catch (InvalidOperationException) { SetStatus(Localize("The board change could not be saved."), UiMessageType.Warning); }
        SetSuccessIfMissing(Localize("Board resized to {0}x{1}.", Rows, Columns));
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostTeamSizeAsync(Guid id, int expectedTeamSize, CancellationToken ct)
    {
        if (expectedTeamSize is < 1 or > 100) { SetStatus(Localize("Expected team size must be between 1 and 100."), UiMessageType.Warning); return RedirectToPage(new { id }); }
        var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct); if (board is null) return NotFound();
        if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id });
        var bingoEvent = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct); if (bingoEvent is null) return NotFound();
        bingoEvent.ConfigurePlanning(bingoEvent.PublicRules, bingoEvent.BuyInDescription, bingoEvent.PrizeDescription, bingoEvent.ExpectedTeamCount, expectedTeamSize, bingoEvent.ExpectedBoardRows, bingoEvent.ExpectedBoardColumns);
        await db.SaveChangesAsync(ct); await WriteAudit("board.expected_team_size_changed", board.Id, expectedTeamSize.ToString(CultureInfo.InvariantCulture), ct); SetStatus(Localize("Expected team size updated."), UiMessageType.Success); return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPublishAsync(Guid id, bool confirmed, CancellationToken ct)
    {
        if (!confirmed)
        {
            SetStatus(Localize("Confirmation required"), UiMessageType.Warning);
            return RedirectToPage(new { id });
        }
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct);
            var bingoEvent = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
            var draft = await db.DraftSessions.SingleOrDefaultAsync(x => x.EventId == id, ct);
            if (board is null || bingoEvent is null || draft is null) return NotFound();
            if (board.Version != BoardVersion) throw new DbUpdateConcurrencyException();
            if (draft.State != DraftState.Finalized || !bingoEvent.TeamRostersPublished)
                throw new InvalidOperationException("Finalize the team draft before publishing the board.");
            if (bingoEvent.ActualStartedAt is not null || bingoEvent.EventEndsAt is not { } endsAt || time.GetUtcNow() >= endsAt)
                throw new InvalidOperationException("The board must be published before the event has started and while its configured end remains in the future.");
            board.Publish(time.GetUtcNow());
            bingoEvent.SetBoardPublication(true, time.GetUtcNow());
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
            SetStatus(Localize("Another administrator changed the board first. Reload before publishing."), UiMessageType.Warning);
        }
        catch (InvalidOperationException)
        {
            db.ChangeTracker.Clear();
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

    public async Task<IActionResult> OnPostApproveAsync(Guid id, bool confirmed, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var board = await db.Boards.SingleOrDefaultAsync(x => x.EventId == id, ct);
            var bingoEvent = await db.Events.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (board is null || bingoEvent is null) return NotFound();
            if (board.Version != BoardVersion)
            {
                SetStatus(Localize("This board changed after you opened it. Reload before approving it."), UiMessageType.Warning);
                return RedirectToPage(new { id });
            }
            board.RequireEditing(AdminId, time.GetUtcNow());

            var publishingCorrection = board.State == BoardState.Published && board.PublishedCorrectionInProgress;
            if (publishingCorrection)
            {
                if (!confirmed)
                {
                    SetStatus(Localize("Confirmation required"), UiMessageType.Warning);
                    return RedirectToPage(new { id });
                }
                bingoEvent.EnsurePublishedBoardCorrectionAllowed();
            }
            var snapshot = await CreateApprovalSnapshotAsync(board, ct, publishingCorrection);
            if (publishingCorrection)
                board.ReplacePublishedApproval(snapshot.Id);
            else
                board.Approve(snapshot.Id);
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
            await transaction.CommitAsync(ct);
            await collaboration.NotifyBoardChangedAsync(id, ct);
            SetStatus(publishingCorrection
                ? Localize("Corrected board published as a replacement snapshot. The prior public snapshot remains in history.")
                : Localize("Board approved privately. Publication remains a separate later action."), UiMessageType.Success);
        }
        catch (Exception exception) when (IsApprovalConflict(exception))
        {
            db.ChangeTracker.Clear();
            SetStatus(Localize("The board or catalogue changed while approval was being prepared. No approval was saved; reload and try again."), UiMessageType.Warning);
        }
        catch (InvalidOperationException)
        {
            db.ChangeTracker.Clear();
            SetStatus(Localize("The board approval could not be changed."), UiMessageType.Warning);
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
        if (board.State != BoardState.Draft && !(allowPublished && board.State == BoardState.Published && board.PublishedCorrectionInProgress))
            throw new InvalidOperationException("Only an unapproved private board can be approved.");

        var tiles = await db.BoardTiles.Where(x => x.BoardId == board.Id).OrderBy(x => x.RowIndex).ThenBy(x => x.ColumnIndex).ToListAsync(ct);
        if (tiles.Count != board.Rows * board.Columns)
            throw new InvalidOperationException("Fill every board position before approving the board.");
        if (tiles.Select(x => (x.RowIndex, x.ColumnIndex)).Distinct().Count() != tiles.Count)
            throw new InvalidOperationException("The board has conflicting tile positions. Reload and correct the layout before approving it.");

        var tileIds = tiles.Select(x => x.Id).ToList();
        var requirements = await db.BoardRequirementSnapshots.Where(x => tileIds.Contains(x.BoardTileId)).OrderBy(x => x.Position).ToListAsync(ct);
        if (tiles.Any(tile => requirements.All(requirement => requirement.BoardTileId != tile.Id)))
            throw new InvalidOperationException("Every board tile needs at least one objective before approval.");

        var templateIds = tiles.Select(x => x.TileTemplateId).Distinct().ToList();
        var templates = await db.TileTemplates.Where(x => templateIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (templates.Count != templateIds.Count)
            throw new InvalidOperationException("A tile definition is no longer available. Reload the board before approval.");

        var requirementIds = requirements.Select(x => x.Id).ToList();
        var requirementBosses = await db.BoardRequirementBossSnapshots.Where(x => requirementIds.Contains(x.RequirementId)).ToListAsync(ct);
        var requirementDrops = await db.BoardRequirementDropSnapshots.Where(x => requirementIds.Contains(x.RequirementId)).ToListAsync(ct);
        var bossIds = requirementBosses.Select(x => x.BossActivityId).Distinct().ToList();
        var currentBosses = await db.BossActivities.Where(x => bossIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (currentBosses.Count != bossIds.Count || currentBosses.Values.Any(x => !x.Active))
            throw new InvalidOperationException("A referenced boss or activity is no longer active. Correct the board before approval.");

        var dropIds = requirementDrops.Select(x => x.SourceDropId).Distinct().ToList();
        var currentDrops = await (from drop in db.SourceDrops
                                  join boss in db.BossActivities on drop.BossActivityId equals boss.Id
                                  join item in db.CatalogueItems on drop.ItemId equals item.Id
                                  where dropIds.Contains(drop.Id)
                                  select new ApprovalLiveDrop(drop, boss, item)).ToDictionaryAsync(x => x.Drop.Id, ct);
        if (currentDrops.Count != dropIds.Count || currentDrops.Values.Any(x => !x.Drop.Active || !x.Boss.Active || !x.Item.Active))
            throw new InvalidOperationException("A referenced catalogue drop is no longer active. Correct the board before approval.");

        var images = await db.BoardTileImageAssets.Where(x => tileIds.Contains(x.BoardTileId) && x.ReplacedAt == null).ToListAsync(ct);
        var imagesById = images.ToDictionary(x => x.Id);
        var nextVersion = (await db.BoardApprovalSnapshots.Where(x => x.BoardId == board.Id).Select(x => (int?)x.Version).MaxAsync(ct) ?? 0) + 1;
        var supersedes = await db.BoardApprovalSnapshots.Where(x => x.BoardId == board.Id).OrderByDescending(x => x.Version).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        var approval = new BoardApprovalSnapshot(Guid.NewGuid(), board.Id, nextVersion, time.GetUtcNow(), AdminId, supersedes, board.Name, board.Rows, board.Columns, 0m, board.CalculationVersion, board.Version, allowPublished ? BoardState.Published : BoardState.Validated);
        var totalEhb = 0m;

        foreach (var tile in tiles)
        {
            string? artwork = null;
            if (tile.ActiveImageAssetId is { } imageId)
            {
                if (!imagesById.TryGetValue(imageId, out var image) || image.EventId != board.EventId || image.BoardTileId != tile.Id)
                    throw new InvalidOperationException("A managed tile image no longer belongs to this event. Correct the tile before approval.");
                artwork = image.StorageKey;
            }
            var approvalTile = new BoardApprovalTileSnapshot(Guid.NewGuid(), approval.Id, tile.Id, tile.TileTemplateId, tile.RowIndex, tile.ColumnIndex, tile.NameSnapshot, tile.DescriptionSnapshot, string.Empty, 0m, artwork);
            db.BoardApprovalTileSnapshots.Add(approvalTile);
            var tileEstimates = new List<decimal?>();
            foreach (var requirement in requirements.Where(x => x.BoardTileId == tile.Id).OrderBy(x => x.Position))
            {
                var approvalRequirement = new BoardApprovalRequirementSnapshot(Guid.NewGuid(), approvalTile.Id, requirement.Id, requirement.Position, requirement.TargetContribution, requirement.DuplicatesAllowed, requirement.AllowHigherWeightings, requirement.CreditedWeight, requirement.Description, requirement.ManualObjective);
                db.BoardApprovalRequirementSnapshots.Add(approvalRequirement);
                foreach (var requirementBoss in requirementBosses.Where(x => x.RequirementId == requirement.Id))
                {
                    var currentBoss = currentBosses[requirementBoss.BossActivityId];
                    db.BoardApprovalRequirementBossSnapshots.Add(new BoardApprovalRequirementBossSnapshot(Guid.NewGuid(), approvalRequirement.Id, currentBoss.Id, currentBoss.Name, currentBoss.EfficientCompletionsPerHour, currentBoss.Version));
                }

                var selectedDrops = requirementDrops.Where(x => x.RequirementId == requirement.Id).Select(x => currentDrops[x.SourceDropId]).ToList();
                if (!requirement.ManualObjective && selectedDrops.Count == 0)
                    throw new InvalidOperationException("Every catalogue objective needs an active eligible drop before approval.");
                foreach (var selected in selectedDrops)
                {
                    var sourceSnapshot = requirementDrops.Single(x => x.RequirementId == requirement.Id && x.SourceDropId == selected.Drop.Id);
                    var approvalDrop = new BoardApprovalRequirementDropSnapshot(Guid.NewGuid(), approvalRequirement.Id, selected.Drop.Id, selected.Boss.Name, selected.Item.Name, selected.Drop.DisplayRate, selected.Drop.NumericProbability, sourceSnapshot.MaximumContribution, selected.Drop.DefaultEhbEstimate, sourceSnapshot.CreditedWeight, selected.Drop.Version, selected.Drop.ProbabilityScope, selected.Drop.ConditionalOnParent, selected.Drop.ParentProbability, selected.Drop.AssumedParticipants, selected.Drop.RollsPerCompletion, selected.Drop.RollGroup, selected.Drop.RateConditionNote);
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
            var tileEhb = EhbCalculator.SumRequirements(tileEstimates, templates[tile.TileTemplateId].ManualEhbOverride);
            if (tileEhb <= 0)
                throw new InvalidOperationException($"{tile.NameSnapshot} needs authoritative catalogue EHB data or an explicit manual EHB estimate before approval.");
            db.Entry(approvalTile).Property(x => x.EstimatedEhb).CurrentValue = tileEhb;
            totalEhb += tileEhb;
        }

        db.Entry(approval).Property(x => x.TotalEhbEstimate).CurrentValue = totalEhb;
        board.SetTotalEhb(totalEhb);
        return approval;
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

    private void AddBoardAudit(string action, Board board, string details, string description, string? beforeState, string? afterState) =>
        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), time.GetUtcNow(), AdminId, User.Identity?.Name ?? "Admin", action, "board", board.Id.ToString(), description, board.EventId, beforeState, afterState));

    private static bool IsApprovalConflict(Exception exception) => exception is DbUpdateConcurrencyException or PostgresException { SqlState: "40001" } or DbUpdateException { InnerException: PostgresException { SqlState: "40001" } };

    public async Task<IActionResult> OnGetTileImageAsync(Guid id, Guid tileId, CancellationToken ct)
    {
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
        await using var transaction = await db.Database.BeginTransactionAsync(ct); var board = await db.Boards.SingleAsync(x => x.EventId == id, ct); if (!board.IsEditable) { SetStatus(Localize("This board is no longer editable."), UiMessageType.Warning); return RedirectToPage(new { id }); }
        if (!PrepareCompetitiveEdit(board)) return RedirectToPage(new { id }); if (!await TryClaimBoardAsync(board, ct)) return RedirectToPage(new { id });
        var tile = await db.BoardTiles.SingleOrDefaultAsync(x => x.BoardId == board.Id && x.Id == tileId, ct); if (tile is null) return NotFound();
        var requirements = await db.BoardRequirementSnapshots.Where(x => x.BoardTileId == tile.Id).ToListAsync(ct); var ids = requirements.Select(x => x.Id).ToList();
        var images = await db.BoardTileImageAssets.Where(x => x.BoardTileId == tile.Id).ToListAsync(ct);
        db.BoardRequirementBossSnapshots.RemoveRange(await db.BoardRequirementBossSnapshots.Where(x => ids.Contains(x.RequirementId)).ToListAsync(ct)); db.BoardRequirementDropSnapshots.RemoveRange(await db.BoardRequirementDropSnapshots.Where(x => ids.Contains(x.RequirementId)).ToListAsync(ct)); db.BoardRequirementSnapshots.RemoveRange(requirements); db.BoardTileImageAssets.RemoveRange(images); db.BoardTiles.Remove(tile);
        board.SetTotalEhb(Math.Max(0, board.TotalEhbEstimate - tile.EstimatedEhbSnapshot)); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        foreach (var image in images) await storage.DeleteAsync(image.StorageKey, ct);
        await WriteAudit("board.tile_removed", board.Id, tile.NameSnapshot, ct); SetSuccessIfMissing(Localize("Tile removed.")); return RedirectToPage(new { id });
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
        var ehb = EhbCalculator.SumRequirements(estimates, template.ManualEhbOverride); var boardTile = new BoardTile(Guid.NewGuid(), board.Id, template.Id, position / board.Columns, position % board.Columns, template.Name, template.Description, template.EvidenceInstructions, ehb, template.ImageUrl); db.BoardTiles.Add(boardTile);
        foreach (var requirement in requirements)
        {
            var snapshot = new BoardRequirementSnapshot(Guid.NewGuid(), boardTile.Id, requirement.Position, requirement.TargetContribution, requirement.DuplicatesAllowed, requirement.AllowHigherWeightings, requirement.Description, requirement.ManualObjective); db.BoardRequirementSnapshots.Add(snapshot);
            var bosses = await (from link in db.TemplateRequirementBosses where link.RequirementId == requirement.Id join boss in db.BossActivities on link.BossActivityId equals boss.Id select boss).ToListAsync(ct); foreach (var boss in bosses) db.BoardRequirementBossSnapshots.Add(new BoardRequirementBossSnapshot(Guid.NewGuid(), snapshot.Id, boss.Id, boss.Name, boss.EfficientCompletionsPerHour));
            var drops = await (from link in db.TemplateRequirementDrops where link.RequirementId == requirement.Id join drop in db.SourceDrops on link.SourceDropId equals drop.Id join boss in db.BossActivities on drop.BossActivityId equals boss.Id join item in db.CatalogueItems on drop.ItemId equals item.Id select new { link, drop, boss, item }).ToListAsync(ct); foreach (var value in drops) db.BoardRequirementDropSnapshots.Add(new BoardRequirementDropSnapshot(Guid.NewGuid(), snapshot.Id, value.drop.Id, value.boss.Name, value.item.Name, value.drop.DisplayRate, value.drop.NumericProbability, value.link.MaximumContribution, value.drop.DefaultEhbEstimate, value.link.CreditedWeight));
        }
        board.SetTotalEhb(board.TotalEhbEstimate + ehb);
        return boardTile;
    }

    private async Task<bool> Load(Guid id, CancellationToken ct)
    {
        var bingoEvent = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (bingoEvent is null) return false; EventName = bingoEvent.Name; EventState = bingoEvent.State;
        Bosses = await db.BossActivities.AsNoTracking().Where(x => x.Active).OrderBy(x => x.Name).Select(x => new BossView(x.Id, x.Name, x.Category, x.EfficientCompletionsPerHour)).ToListAsync(ct);
        var catalogueDrops = await (from drop in db.SourceDrops.AsNoTracking()
                                    join boss in db.BossActivities on drop.BossActivityId equals boss.Id
                                    join item in db.CatalogueItems on drop.ItemId equals item.Id
                                    where drop.Active && boss.Active && item.Active
                                    orderby boss.Name, item.Name
                                    select new { drop.Id, BossId = boss.Id, BossName = boss.Name, ItemName = item.Name, drop.DisplayRate })
            .ToListAsync(ct);
        Drops = catalogueDrops.Select(drop => new DropView(drop.Id, drop.BossId, drop.BossName, drop.ItemName, drop.DisplayRate)).ToList();
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id, ct); if (board is null) { Rows = bingoEvent.ExpectedBoardRows ?? 5; Columns = bingoEvent.ExpectedBoardColumns ?? 5; return true; }
        DraftFinalized = await db.DraftSessions.AsNoTracking().AnyAsync(x => x.EventId == id && x.State == DraftState.Finalized, ct);
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
        var editorBosses = await db.BoardRequirementBossSnapshots.AsNoTracking().Where(x => editorRequirementIds.Contains(x.RequirementId)).ToListAsync(ct);
        var editorDrops = await db.BoardRequirementDropSnapshots.AsNoTracking().Where(x => editorRequirementIds.Contains(x.RequirementId)).ToListAsync(ct);
        var editorTemplateIds = boardTilesForEditors.Select(x => x.TileTemplateId).Distinct().ToList();
        var manualEhbByTemplate = await db.TileTemplates.AsNoTracking().Where(x => editorTemplateIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ManualEhbOverride, ct);
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
        var liveDropsByRequirement = liveDropRows.GroupBy(value => value.RequirementId).ToDictionary(group => group.Key, group => (IReadOnlyList<LiveDrop>)group.ToList());
        var liveDropsById = liveDropRows.GroupBy(value => value.SourceDropId).ToDictionary(group => group.Key, group => group.First());

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
            ? BuildLiveTiles(boardTilesForEditors, requirementsByTile, liveDropsByRequirement, manualEhbByTemplate, board.Columns)
            : BuildFrozenTiles(boardTilesForEditors, frozenTiles, board.Columns);
        var displayedTotal = activeApproval?.TotalEhbEstimate ?? Tiles.Sum(tile => tile.Ehb);
        BoardView = new(board.Rows, board.Columns, board.State, displayedTotal, board.Version, board.PublishedCorrectionInProgress);
        ReadinessWarnings = useLiveDerivation
            ? Tiles.Where(tile => tile.Ehb <= 0).Select(tile => $"{tile.Name} needs authoritative catalogue EHB data or an explicit manual EHB estimate.").ToList()
            : [];
        var managedImages = await db.BoardTileImageAssets.AsNoTracking().Where(image => tileIds.Contains(image.BoardTileId) && image.ReplacedAt == null).ToDictionaryAsync(image => image.Id, ct);
        TileEditors = boardTilesForEditors.Select(tile => new TileEditorView(tile.Id,
            useLiveDerivation ? tile.NameSnapshot : frozenTiles[tile.Id].Name,
            useLiveDerivation ? tile.DescriptionSnapshot : frozenTiles[tile.Id].Description,
            tile.ActiveImageAssetId is { } imageId && managedImages.TryGetValue(imageId, out var image) && image.BoardTileId == tile.Id && image.EventId == id ? Url.Page("Board", "TileImage", new { id, tileId = tile.Id }) : null,
            manualEhbByTemplate.GetValueOrDefault(tile.TileTemplateId),
            requirementsByTile.GetValueOrDefault(tile.Id, []).Select(requirement => new RequirementEditorView(requirement.ManualObjective ? "challenge" : "drops", requirement.Description, requirement.TargetContribution, requirement.DuplicatesAllowed,
                bossesByRequirement.GetValueOrDefault(requirement.Id, []).Select(value => value.BossActivityId).ToList(),
                dropsByRequirement.GetValueOrDefault(requirement.Id, []).Select(value => value.SourceDropId).ToList(),
                dropsByRequirement.GetValueOrDefault(requirement.Id, []).ToDictionary(value => value.SourceDropId, value => value.CreditedWeight),
                dropsByRequirement.GetValueOrDefault(requirement.Id, []).Select(drop =>
                {
                    if (useLiveDerivation && liveDropsById.TryGetValue(drop.SourceDropId, out var live)) return new RequirementDropView(drop.SourceDropId, live.Boss.Name, live.Item.Name, live.Drop.DisplayRate, drop.CreditedWeight);
                    if (!useLiveDerivation && frozenDropsByBoardRequirementAndDrop.TryGetValue((requirement.Id, drop.SourceDropId), out var frozen)) return new RequirementDropView(drop.SourceDropId, frozen.BossName, frozen.ItemName, frozen.DisplayRate, drop.CreditedWeight);
                    return new RequirementDropView(drop.SourceDropId, "Unavailable boss", "Unavailable item", "Catalogue entry unavailable", drop.CreditedWeight);
                }).ToList())).ToList())).ToList();
        var lines = new List<LineView>(); for (var row = 0; row < board.Rows; row++) lines.Add(new($"Row {row + 1}", "row", row, Tiles.Where(x => x.Position / board.Columns == row).Sum(x => x.Ehb))); for (var column = 0; column < board.Columns; column++) lines.Add(new($"Column {column + 1}", "column", column, Tiles.Where(x => x.Position % board.Columns == column).Sum(x => x.Ehb))); Lines = lines; BalanceSpread = lines.Count == 0 ? 0 : lines.Max(x => x.Ehb) - lines.Min(x => x.Ehb);
        var durationDays = bingoEvent.EventEndsAt is { } eventEnd && bingoEvent.EventStartsAt is { } eventStart ? Math.Max(0.5m, (decimal)(eventEnd - eventStart).TotalHours / 24m) : 0.5m; var teamSize = bingoEvent.ExpectedTeamSize; var total = displayedTotal; var populatedLines = lines.Where(x => x.Ehb > 0).ToList();
        Statistics = new(total, teamSize, teamSize is > 0 ? total / teamSize.Value : null, teamSize is > 0 ? total / teamSize.Value / durationDays : null, Tiles.Count == 0 ? 0 : total / Tiles.Count, populatedLines.Count == 0 ? 0 : populatedLines.Min(x => x.Ehb), populatedLines.Count == 0 ? 0 : populatedLines.Max(x => x.Ehb), Tiles.Count(x => x.Ehb <= 0), durationDays);
        var eventTeams = await db.Teams.AsNoTracking().Where(x => x.EventId == id && x.Active).OrderBy(x => x.DraftPosition).ThenBy(x => x.Name).ToListAsync(ct);
        if (eventTeams.Count > 0)
        {
            var teamIdsForWorkload = eventTeams.Select(x => x.Id).ToList();
            var rosterSizes = await db.TeamMemberships.AsNoTracking().Where(x => teamIdsForWorkload.Contains(x.TeamId) && x.LeftAt == null).GroupBy(x => x.TeamId).ToDictionaryAsync(x => x.Key, x => x.Count(), ct);
            TeamWorkloads = eventTeams.Select(team =>
            {
                var actualSize = rosterSizes.GetValueOrDefault(team.Id);
                // Board planning estimates are independent from the derived website-draft distribution.
                var sizeUsed = actualSize;
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
    private async Task ReplaceTileImageAsync(Guid eventId, BoardTile tile, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        if (TileDraft.RemoveImage && tile.ActiveImageAssetId is { } priorId)
        {
            (await db.BoardTileImageAssets.SingleOrDefaultAsync(x => x.Id == priorId, ct))?.Replace(now);
            tile.SetActiveImageAsset(null);
        }
        if (TileDraft.Image is not { Length: > 0 }) return;
        var assetId = Guid.NewGuid();
        await using var content = TileDraft.Image.OpenReadStream();
        var stored = await storage.StoreAsync(eventId, assetId, TileDraft.Image.FileName, content, ct);
        if (tile.ActiveImageAssetId is { } currentId)
            (await db.BoardTileImageAssets.SingleOrDefaultAsync(x => x.Id == currentId, ct))?.Replace(now);
        db.BoardTileImageAssets.Add(new BoardTileImageAsset(assetId, eventId, tile.Id, stored.StorageKey, stored.OriginalFilename, stored.MediaType, stored.ByteSize, stored.Width, stored.Height, stored.Checksum, AdminId, now));
        tile.SetActiveImageAsset(assetId);
    }
    private static List<TileView> BuildLiveTiles(
        IReadOnlyList<BoardTile> tiles,
        IReadOnlyDictionary<Guid, IReadOnlyList<BoardRequirementSnapshot>> requirementsByTile,
        IReadOnlyDictionary<Guid, IReadOnlyList<LiveDrop>> dropsByRequirement,
        IReadOnlyDictionary<Guid, decimal?> manualEhbByTemplate,
        int columns)
    {
        return tiles.Select(tile =>
        {
            var estimates = new List<decimal?>();
            foreach (var requirement in requirementsByTile.GetValueOrDefault(tile.Id, []))
            {
                if (requirement.ManualObjective) { estimates.Add(null); continue; }
                var selected = dropsByRequirement.GetValueOrDefault(requirement.Id, []);
                if (selected.Count == 0 || selected.Any(drop => !drop.Drop.Active || !drop.Boss.Active || !drop.Item.Active)) { estimates.Add(null); continue; }
                estimates.Add(EhbCalculator.CalculateDropRequirement(requirement.TargetContribution,
                    selected.Select(drop => new EligibleDropRate(drop.Boss.EfficientCompletionsPerHour, drop.Drop.NumericProbability, drop.Drop.ItemId, drop.Boss.Id, drop.Snapshot.CreditedWeight, drop.Drop.RollsPerCompletion, drop.Drop.RollGroup)),
                    requirement.DuplicatesAllowed));
            }
            var ehb = EhbCalculator.SumRequirements(estimates, manualEhbByTemplate.GetValueOrDefault(tile.TileTemplateId));
            return new TileView(tile.Id, tile.RowIndex * columns + tile.ColumnIndex, tile.NameSnapshot, tile.DescriptionSnapshot, string.Empty, ehb);
        }).ToList();
    }
    private static List<TileView> BuildFrozenTiles(
        IReadOnlyList<BoardTile> tiles,
        IReadOnlyDictionary<Guid, BoardApprovalTileSnapshot> frozenTiles,
        int columns) =>
        tiles.Select(tile =>
        {
            var frozen = frozenTiles[tile.Id];
            return new TileView(tile.Id, frozen.RowIndex * columns + frozen.ColumnIndex, frozen.Name, frozen.Description, frozen.EvidenceInstructions, frozen.EstimatedEhb);
        }).ToList();
    private static string RequirementDescription(RequirementInput input) => input.IsManual ? input.Description!.Trim() : $"Collect {input.Target} eligible drop{(input.Target == 1 ? string.Empty : "s")}";

    public sealed class TileDraftInput { public Guid? TileId { get; set; } public int Position { get; set; } public string? Name { get; set; } public string? Description { get; set; } public IFormFile? Image { get; set; } public bool RemoveImage { get; set; } [Range(0, 100000)] public decimal? ManualEhb { get; set; } public List<RequirementInput> Requirements { get; set; } = [new()]; }
    public sealed class RequirementInput { public string Kind { get; set; } = "drops"; public string? Description { get; set; } [Range(1, 10000)] public int Target { get; set; } = 1; public bool DuplicatesAllowed { get; set; } = true; public Dictionary<Guid, int> DropWeights { get; set; } = []; public List<Guid> BossIds { get; set; } = []; public List<Guid> DropIds { get; set; } = []; public bool IsManual => string.Equals(Kind, "challenge", StringComparison.OrdinalIgnoreCase); public int WeightFor(Guid dropId) => Math.Max(1, DropWeights.GetValueOrDefault(dropId, 1)); public bool HasHigherWeights => DropIds.Any(x => WeightFor(x) > 1); }
    public sealed record BoardDetails(int Rows, int Columns, BoardState State, decimal TotalEhb, long Version, bool PublishedCorrectionInProgress);
    public sealed record BoardStatistics(decimal TotalEhb, int? TeamSize, decimal? EhbPerPlayer, decimal? EhbPerPlayerPerDay, decimal AverageTileEhb, decimal LowestLineEhb, decimal HighestLineEhb, int MissingEhbTiles, decimal DurationDays);
    public sealed record TileView(Guid Id, int Position, string Name, string Description, string EvidenceInstructions, decimal Ehb);
    public sealed record LineView(string Key, string Kind, int Index, decimal Ehb);
    public sealed record BossView(Guid Id, string Name, string Category, decimal? EfficientRate);
    public sealed record DropView(Guid Id, Guid BossId, string BossName, string ItemName, string Rate);
    private sealed record LiveDrop(Guid RequirementId, Guid SourceDropId, BoardRequirementDropSnapshot Snapshot, SourceDrop Drop, BossActivity Boss, CatalogueItem Item);
    private sealed record ApprovalLiveDrop(SourceDrop Drop, BossActivity Boss, CatalogueItem Item);
    public sealed record TileEditorView(Guid Id, string Name, string Description, string? ImageUrl, decimal? ManualEhb, IReadOnlyList<RequirementEditorView> Requirements);
    public sealed record RequirementEditorView(string Kind, string Description, int Target, bool DuplicatesAllowed, IReadOnlyList<Guid> BossIds, IReadOnlyList<Guid> DropIds, IReadOnlyDictionary<Guid, int> DropWeights, IReadOnlyList<RequirementDropView> Drops);
    public sealed record RequirementDropView(Guid Id, string BossName, string ItemName, string DisplayRate, int CreditedWeight);
    public sealed record TeamWorkloadView(string TeamName, TeamFormationType FormationType, int ActualRosterSize, int? SizeUsed, decimal? EhbPerPlayer);
}
