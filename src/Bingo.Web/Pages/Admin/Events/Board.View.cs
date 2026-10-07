using System.Text.Json;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Pages.Admin.Events;

// U7 1a: the page module renders Board.dc.html from this one server view. Every
// EHB figure is the server's (RC05 B7, EhbCalculator via BoardEstimateService and
// the approval snapshot); the client never recalculates. Authoritative state for
// uncertain outcomes comes from the no-store Readback.
public sealed partial class BoardModel
{
    public BoardReadback? Current { get; private set; }
    public string? CorrectionReason { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public bool EventStarted { get; private set; }
    public bool EventEndPassed { get; private set; }

    private static readonly JsonSerializerOptions ViewJson = new(JsonSerializerDefaults.Web);

    private async Task LoadViewContextAsync(Guid id, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.ActualStartedAt, x.EventEndsAt }).SingleOrDefaultAsync(ct);
        EventStarted = ev?.ActualStartedAt is not null;
        EventEndPassed = ev?.EventEndsAt is not { } ends || time.GetUtcNow() >= ends;
        var board = await db.Boards.AsNoTracking().Where(x => x.EventId == id).Select(x => new { x.Id, x.ActiveApprovalSnapshotId, x.PublishedAt, x.PublishedCorrectionInProgress }).SingleOrDefaultAsync(ct);
        if (board is null) return;
        PublishedAt = board.PublishedAt;
        if (board.ActiveApprovalSnapshotId is { } approvalId)
            ApprovedAt = await db.BoardApprovalSnapshots.AsNoTracking().Where(x => x.Id == approvalId).Select(x => (DateTimeOffset?)x.ApprovedAt).SingleOrDefaultAsync(ct);
        if (board.PublishedCorrectionInProgress)
            CorrectionReason = await db.AuditEntries.AsNoTracking()
                .Where(x => x.EventId == id && x.Action == "board.published_correction_started" && x.TargetId == board.Id.ToString())
                .OrderByDescending(x => x.OccurredAt).Select(x => x.Details).FirstOrDefaultAsync(ct);
        Current = await ReadBoardStateAsync(id, ct);
    }

    public bool TerminalReadOnly => EventState is EventState.Cancelled or EventState.Finalized or EventState.Archived;

    public string BoardMode => BoardView switch
    {
        null => "none",
        { PublishedCorrectionInProgress: true } => "correction",
        { State: BoardState.Validated } => "approved",
        { State: BoardState.Published } => "published",
        _ => "draft"
    };

    public string ViewJsonText()
    {
        if (BoardView is not { } board) return JsonSerializer.Serialize(new { mode = "none", readOnly = true, eventName = EventName }, ViewJson);
        var editorsById = TileEditors.ToDictionary(x => x.Id);
        var different = Current?.State?.DifferentTileIds ?? [];
        var workingIds = Tiles.Select(x => x.Id).ToHashSet();
        var tiles = Tiles.Select(tile =>
        {
            var editor = editorsById.GetValueOrDefault(tile.Id);
            var manual = editor is not null && editor.Requirements.Count > 0 && editor.Requirements.All(x => x.Kind == "challenge");
            return new
            {
                id = tile.Id,
                pos = tile.Position,
                name = tile.Name,
                desc = tile.Description,
                ehb = tile.Ehb,
                noEstimate = tile.Ehb <= 0,
                needsVerification = tile.EstimateNeedsVerification,
                overridden = !manual && editor?.ManualEhb is not null,
                manual,
                parts = editor?.Requirements.Count ?? 0,
                locked = ProtectedTileIds.Contains(tile.Id),
                art = editor?.ImageUrl,
                changed = board.PublishedCorrectionInProgress && different.Contains(tile.Id)
            };
        }).ToList();
        object Line(IEnumerable<TileView> line, int size) => new
        {
            total = line.Sum(x => x.Ehb),
            n = line.Count(),
            full = line.Count() == size && line.All(x => x.Ehb > 0)
        };
        var rows = Enumerable.Range(0, board.Rows).Select(r => Line(Tiles.Where(x => x.Position / board.Columns == r), board.Columns)).ToList();
        var cols = Enumerable.Range(0, board.Columns).Select(c => Line(Tiles.Where(x => x.Position % board.Columns == c), board.Rows)).ToList();
        var stats = Statistics;
        return JsonSerializer.Serialize(new
        {
            mode = BoardMode,
            eventName = EventName,
            eventState = EventState.ToString(),
            readOnly = TerminalReadOnly,
            correctionAllowed = EventState is EventState.SignupClosed or EventState.Live or EventState.AwaitingFinalReview,
            teamSizeEditable = !TerminalReadOnly && EventStatePolicy.Allows(EventState, EventCapability.ConfigureIdentityOrSchedule),
            rows = board.Rows,
            cols = board.Columns,
            version = board.Version,
            approvedAt = ApprovedAt,
            publishedAt = PublishedAt,
            reason = CorrectionReason,
            control = new
            {
                who = CanEditBoard ? "me" : BoardEditorAccountId is not null ? "other" : "none",
                name = BoardEditorName,
                expiresAt = BoardEditorLeaseExpiresAt,
                version = Current?.State?.ControlVersion
            },
            me = CurrentAccountId,
            publishReady = new { roster = DraftFinalized, started = EventStarted, ended = EventEndPassed },
            differences = new
            {
                count = different.Count,
                removed = different.Count(x => !workingIds.Contains(x))
            },
            tiles,
            lines = new { rows, cols },
            stats = stats is null ? null : new
            {
                total = stats.TotalEhb,
                teamSize = stats.TeamSize,
                perPlayer = stats.EhbPerPlayer,
                perDay = stats.EhbPerPlayerPerDay,
                days = stats.DurationDays,
                avgTile = Tiles.Count == 0 ? (decimal?)null : stats.AverageTileEhb,
                lo = stats.LowestLineEhb,
                hi = stats.HighestLineEhb,
                missing = stats.MissingEhbTiles,
                filled = Tiles.Count,
                cells = board.Rows * board.Columns
            }
        }, ViewJson);
    }
}
