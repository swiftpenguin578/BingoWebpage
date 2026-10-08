using System.ComponentModel.DataAnnotations;
using Bingo.Application.Evidence;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Evidence;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Npgsql;

namespace Bingo.Web.Pages.Admin.Review;

[AdminDesign]
public sealed class DetailsModel(ApplicationDbContext db, ISubmissionService service, IStringLocalizer<SharedResource>? text = null) : PageModel
{
    private const int MaxReviewReasonLength = 4000;
    // RL-1/BR-4: Reject and Reverse need the explicit confirmation from the decision panel or dialog;
    // without it nothing is written (no review action, audit entry, notification or version change).
    public const string ConfirmationRequiredMessage = "Confirm this decision before it is saved. Nothing was changed.";
    public const string ConflictMessage = "This review was not saved because the event or evidence changed in another request. Reload the submission, review the latest state, and try again.";

    public DetailsView Details { get; private set; } = null!;
    public bool Missing { get; private set; }
    public BingoEvent Event { get; private set; } = null!;
    public string EventTimezone { get; private set; } = DateTimePresentation.DefaultTimezoneId;
    public bool ReviewOpen { get; private set; }
    public DateTimeOffset? EventStartedAt { get; private set; }
    public IReadOnlyList<AssetView> Assets { get; private set; } = [];
    public IReadOnlyList<HistoryView> History { get; private set; } = [];
    public IReadOnlyList<ContextView> PriorApproved { get; private set; } = [];
    public IReadOnlyList<ChecksumMatch> ChecksumMatches { get; private set; } = [];
    public IReadOnlyList<RequirementOption> Requirements { get; private set; } = [];
    public IReadOnlyList<DropOption> Drops { get; private set; } = [];
    public SubmissionContributionRead? Contribution { get; private set; }
    public ApprovalBlockView? ApprovalBlock { get; private set; }
    public DecisionView? Approval { get; private set; }
    public DecisionView? Feedback { get; private set; }
    public NeighbourView? Neighbours { get; private set; }
    public int RemovedContribution { get; private set; }
    public int TargetContribution { get; private set; }
    public int UsedContribution { get; private set; }
    [BindProperty(SupportsGet = true)] public string Search { get; set; } = string.Empty;
    [BindProperty(SupportsGet = true)] public SubmissionStatus? Status { get; set; }
    [BindProperty] public ReviewInput Input { get; set; } = new();

    public string QueueUrl => ReviewList.QueueUrl(Details.EventId, Search, Status);
    public string DetailsUrl(Guid id) => ReviewList.DetailsUrl(id, Details.EventId, Search, Status);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        // C-CMP-1: unknown and hidden are one Not Found, rendered as the reference's unavailable state.
        if (!await Load(id, ct)) { Missing = true; return new PageResult { StatusCode = StatusCodes.Status404NotFound }; }
        Input = new() { BoardTileId = Details.TileId, RequirementId = Details.RequirementId, DropSnapshotId = Details.DropId, CreditedOsrsCharacterId = Details.CharacterId, ExpectedVersion = Details.Version };
        return Page();
    }

    public async Task<IActionResult> OnGetReadbackAsync(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (User.GetAccountId() is not { } adminId) return Forbid();
        // C-CMP-1: an unknown submission and one of a hidden event are indistinguishable Not Found.
        try { if (!await VisibleSubmissionAsync(id, ct)) return NotFound(); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new JsonResult(new SubmissionReviewReadback(null)); }
        try { return new JsonResult(await service.GetReviewReadbackAsync(id, adminId, ct)); }
        catch (InvalidOperationException) { return Forbid(); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new JsonResult(new SubmissionReviewReadback(null)); }
    }

    public async Task<IActionResult> OnGetCorrectionCharactersAsync(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (User.GetAccountId() is not { } adminId) return Forbid();
        try { return new JsonResult(await service.GetCorrectionCharactersAsync(id, adminId, ct)); }
        catch (InvalidOperationException ex) when (ex.Message == "Administrator access is required.") { return Forbid(); }
        catch (InvalidOperationException) { return NotFound(); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return StatusCode(StatusCodes.Status503ServiceUnavailable); }
    }

    public Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken ct) => Decide(id, "approve", async () =>
    {
        var result = await service.ApproveAsync(id, User.GetAccountId()!.Value, ct, Input.ExpectedVersion);
        return result.BlockingSubmission is { } block ? Blocked(id, block) : null;
    }, ct, amount: async () => await db.Submissions.AsNoTracking().Where(x => x.Id == id).Select(x => x.ApprovedContribution).SingleAsync(ct));

    public Task<IActionResult> OnPostRejectAsync(Guid id, bool confirmed, CancellationToken ct) => !confirmed ? Refuse(id, ConfirmationRequiredMessage, ct) : Decide(id, "reject",
        async () => { await service.RejectAsync(id, User.GetAccountId()!.Value, Input.Reason ?? string.Empty, ct, Input.ExpectedVersion); return null; }, ct);

    public Task<IActionResult> OnPostReverseAsync(Guid id, bool confirmed, CancellationToken ct) => !confirmed ? Refuse(id, ConfirmationRequiredMessage, ct) : Decide(id, "reverse",
        async () => { await service.ReverseAsync(id, User.GetAccountId()!.Value, Input.Reason ?? string.Empty, ct, Input.ExpectedVersion); return null; }, ct,
        amount: async () => await db.Submissions.AsNoTracking().Where(x => x.Id == id).Select(x => x.ApprovedContribution).SingleAsync(ct));

    public Task<IActionResult> OnPostEditAsync(Guid id, CancellationToken ct) => Decide(id, "correct", async () =>
    {
        await service.EditMetadataAsync(new(id, User.GetAccountId()!.Value, Input.BoardTileId, Input.RequirementId, Input.DropSnapshotId, Input.CreditedOsrsCharacterId, Input.Reason ?? string.Empty, Input.ExpectedVersion), ct);
        return null;
    }, ct);

    // In-place decisions answer with a definite outcome as JSON for the page's fetch (X-Requested-With);
    // a non-script form post is redirected back to the workspace, which shows the current state.
    // Outcomes: saved, refused (the server's reason), stale (another request changed it first; nothing was
    // saved), blocked (G1: an earlier pending upload must be resolved first). An unexpected failure is not
    // answered here, so the client re-reads through Readback and never guesses.
    private async Task<IActionResult> Decide(Guid id, string kind, Func<Task<DecisionOutcome?>> action, CancellationToken ct, Func<Task<int>>? amount = null)
    {
        DecisionOutcome outcome;
        try
        {
            outcome = await action() ?? new DecisionOutcome("saved", kind) { Amount = amount is null ? null : await amount() };
        }
        catch (Exception ex) when (IsReviewPersistenceConflict(ex)) { outcome = await StaleAsync(id, kind, Localize(ConflictMessage), ct); }
        catch (InvalidOperationException ex) when (ex.Message == SubmissionService.StaleEvidenceMessage) { outcome = await StaleAsync(id, kind, Localize(ex.Message), ct); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { outcome = new DecisionOutcome("refused", kind) { Message = Localize(ex.Message) }; }
        return Answer(id, outcome);
    }

    private Task<IActionResult> Refuse(Guid id, string message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Answer(id, new DecisionOutcome("refused", Request.Query["handler"].ToString().ToLowerInvariant()) { Message = Localize(message) }));
    }

    private IActionResult Answer(Guid id, DecisionOutcome outcome)
    {
        Response.Headers.CacheControl = "no-store";
        if (string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.Ordinal)) return new JsonResult(outcome);
        return Redirect($"/Admin/Review/Details/{id:D}" + FilterQuery(Request.QueryString.Value ?? string.Empty));
    }

    private async Task<DecisionOutcome> StaleAsync(Guid id, string kind, string message, CancellationToken ct)
    {
        var state = User.GetAccountId() is { } adminId ? (await service.GetReviewReadbackAsync(id, adminId, ct)).State : null;
        var actor = state?.LatestAction is { } latest ? await ActorNameAsync(latest.ActorId, ct) : null;
        return new DecisionOutcome("stale", kind) { Message = message, Status = state?.Status.ToString(), ChangedBy = actor, ChangedAction = state?.LatestAction?.Type.ToString() };
    }

    private DecisionOutcome Blocked(Guid id, SubmissionApprovalBlock block) => new("blocked", "approve")
    {
        Message = Localize("Approve or reject the earlier upload first."),
        BlockingSubmissionId = block.SubmissionId,
        BlockingUrl = $"/Admin/Review/Details/{block.SubmissionId:D}" + FilterQuery(Request.QueryString.Value ?? string.Empty)
    };

    private static string FilterQuery(string query)
    {
        var parts = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Where(part => part.StartsWith("eventId=", StringComparison.OrdinalIgnoreCase) || part.StartsWith("search=", StringComparison.OrdinalIgnoreCase) || part.StartsWith("status=", StringComparison.OrdinalIgnoreCase));
        var joined = string.Join('&', parts);
        return joined.Length == 0 ? string.Empty : "?" + joined;
    }

    public sealed record DecisionOutcome(string Outcome, string Kind)
    {
        public string? Message { get; init; }
        public int? Amount { get; init; }
        public string? Status { get; init; }
        public string? ChangedBy { get; init; }
        public string? ChangedAction { get; init; }
        public Guid? BlockingSubmissionId { get; init; }
        public string? BlockingUrl { get; init; }
    }

    private Task<bool> VisibleSubmissionAsync(Guid id, CancellationToken ct) =>
        (from submission in db.Submissions.AsNoTracking() join item in db.Events.AsNoTracking() on submission.EventId equals item.Id
         where submission.Id == id && item.HiddenAt == null select submission.Id).AnyAsync(ct);

    private static bool IsReviewPersistenceConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException or PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected }) return true;
        return false;
    }

    private string Localize(string key, params object[] arguments) => text?[key, arguments].Value ?? string.Format(System.Globalization.CultureInfo.CurrentCulture, key, arguments);

    private async Task<string?> ActorNameAsync(Guid actorId, CancellationToken ct) =>
        await db.Accounts.AsNoTracking().Where(x => x.Id == actorId).Select(x => x.LoginName).SingleOrDefaultAsync(ct);

    private async Task<bool> Load(Guid id, CancellationToken ct)
    {
        var s = await db.Submissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return false;
        // The event comes from the submission; a wrong eventId in the link is corrected (A2).
        var eventItem = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == s.EventId && x.HiddenAt == null, ct);
        if (eventItem is null) return false;
        Event = eventItem;
        ReviewOpen = EventStatePolicy.Allows(eventItem.State, EventCapability.ReviewEvidence);
        EventTimezone = eventItem.Timezone is { Length: > 0 } zone ? zone : DateTimePresentation.DefaultTimezoneId;
        EventStartedAt = eventItem.ActualStartedAt ?? eventItem.EventStartsAt;
        var team = await db.Teams.AsNoTracking().SingleAsync(x => x.Id == s.TeamId, ct);
        var publication = await db.PublishedObjectivesAsync(s.EventId, ct);
        if (publication is null) return false;
        var tile = publication.Tiles.SingleOrDefault(x => x.Id == s.BoardTileId);
        var req = publication.Requirements.SingleOrDefault(x => x.Id == s.RequirementId && x.BoardTileId == s.BoardTileId);
        if (tile is null || req is null) return false;
        var drop = s.DropSnapshotId is null ? null : publication.Drops.SingleOrDefault(x => x.Id == s.DropSnapshotId && x.RequirementId == s.RequirementId);
        if (s.DropSnapshotId is not null && drop is null) return false;
        var effectiveEnd = ReviewList.EffectiveEnd(eventItem);
        var minutesAfterEnd = effectiveEnd is { } eventEnd && s.SubmittedAt > eventEnd ? (int?)Math.Ceiling((s.SubmittedAt - eventEnd).TotalMinutes) : null;
        var gaps = (await ReviewList.PausedGapsAsync(db, s.EventId, ct)).Select(x => new EligibilityGapView(x.StartedAt, x.ResumedAt)).ToList();
        var memberships = await db.TeamMemberships.AsNoTracking().Where(x => x.TeamId == s.TeamId && x.EventParticipantId == s.CreditedParticipantId).Select(x => x.LeftAt).ToListAsync(ct);
        Details = new(s.Id, s.EventId, s.TeamId, s.BoardTileId, s.RequirementId, s.DropSnapshotId, s.CreditedParticipantId, s.CreditedOsrsCharacterId, team.Name, tile.NameSnapshot, req.Description, drop?.BossName, drop?.ItemName, s.CreditedCharacterName, s.Status, s.ClaimedWeight, s.ApprovedContribution, s.SubmittedAt, s.CaptainNote, s.CurrentReviewerNote, s.ExpectedEvidenceCode, s.Version, minutesAfterEnd, effectiveEnd, gaps, ReviewList.LeftAt(memberships), req.ManualObjective);
        TargetContribution = req.TargetContribution;
        if (s.Status == SubmissionStatus.Reversed) RemovedContribution = s.ApprovedContribution;
        if (HttpContext?.User?.GetAccountId() is { } adminId)
        {
            Contribution = (await service.GetReviewReadbackAsync(id, adminId, ct)).State?.Contribution;
            if (Contribution?.BlockingSubmission is { } block) ApprovalBlock = new(block.SubmissionId, block.SubmittedAt);
        }
        UsedContribution = Contribution?.Values?.Used ?? await db.SubmissionContributions.AsNoTracking()
            .Where(x => x.TeamId == s.TeamId && x.RequirementId == s.RequirementId && x.ReversedAt == null && x.SubmissionId != s.Id).SumAsync(x => (int?)x.Amount, ct) ?? 0;

        Assets = await db.EvidenceAssets.AsNoTracking().Where(x => x.SubmissionId == id).OrderByDescending(x => x.Active).ThenByDescending(x => x.UploadedAt).Select(x => new AssetView(x.Id, x.OriginalFilename, x.MediaType, x.ByteSize, x.PixelWidth, x.PixelHeight, x.Checksum, x.UploadedAt, x.Role, x.Active)).ToListAsync(ct);
        var activeChecksum = Assets.FirstOrDefault(x => x.Active)?.Checksum;
        if (activeChecksum is not null)
            ChecksumMatches = await (from asset in db.EvidenceAssets.AsNoTracking() join other in db.Submissions on asset.SubmissionId equals other.Id join otherTile in db.BoardTiles on other.BoardTileId equals otherTile.Id
                                     where asset.Checksum == activeChecksum && asset.Active && other.EventId == s.EventId && other.Id != s.Id
                                     orderby other.SubmittedAt descending select new ChecksumMatch(other.Id, otherTile.NameSnapshot, other.CreditedCharacterName, other.SubmittedAt, other.Status)).Distinct().ToListAsync(ct);
        History = await LoadHistoryAsync(id, ct);
        Approval = s.Status == SubmissionStatus.Approved ? History.FirstOrDefault(x => x.Action == "submission.approved") is { } approved ? new(approved.Actor, approved.At, null) : null : null;
        Feedback = s.Status is SubmissionStatus.Rejected or SubmissionStatus.Reversed && History.FirstOrDefault(x => x.Action is "submission.rejected" or "submission.reversed") is { } decided ? new(decided.Actor, decided.At, s.CurrentReviewerNote) : null;
        var drops = publication.Drops.ToDictionary(x => x.Id);
        PriorApproved = (await db.Submissions.AsNoTracking().Where(other => other.TeamId == s.TeamId && other.RequirementId == s.RequirementId && other.Status == SubmissionStatus.Approved && other.Id != s.Id).OrderByDescending(other => other.SubmittedAt)
            .Select(other => new { other.Id, other.CreditedCharacterName, other.ApprovedContribution, other.SubmittedAt, other.DropSnapshotId }).ToListAsync(ct))
            .Select(other => new ContextView(other.Id, other.CreditedCharacterName, other.ApprovedContribution, other.SubmittedAt, other.DropSnapshotId is { } dropId && drops.TryGetValue(dropId, out var priorDrop) ? priorDrop.ItemName : null)).ToList();
        var tileMap = publication.Tiles.ToDictionary(x => x.Id);
        Requirements = publication.Requirements.OrderBy(x => tileMap[x.BoardTileId].RowIndex).ThenBy(x => tileMap[x.BoardTileId].ColumnIndex).ThenBy(x => x.Position)
            .Select(x => new RequirementOption(x.Id, x.BoardTileId, tileMap[x.BoardTileId].NameSnapshot, x.Description, x.ManualObjective, x.CreditedWeight)).ToList();
        Drops = publication.Drops.OrderBy(x => x.BossName).ThenBy(x => x.ItemName).Select(x => new DropOption(x.Id, x.RequirementId, x.BossName, x.ItemName, x.CreditedWeight)).ToList();
        var rows = ReviewList.Filter(await ReviewList.RowsAsync(db, eventItem, ct), Search?.Trim() ?? string.Empty, Status);
        var index = rows.ToList().FindIndex(x => x.Id == s.Id);
        if (index >= 0) Neighbours = new(index + 1, rows.Count, index > 0 ? rows[index - 1].Id : null, index < rows.Count - 1 ? rows[index + 1].Id : null);
        return true;
    }

    private async Task<IReadOnlyList<HistoryView>> LoadHistoryAsync(Guid submissionId, CancellationToken ct)
    {
        var targetId = submissionId.ToString("D");
        var audits = await db.AuditEntries.AsNoTracking().Where(a => a.TargetType == "submission" && a.TargetId == targetId).ToListAsync(ct);
        var actions = await db.ReviewActions.AsNoTracking().Where(a => a.SubmissionId == submissionId).OrderBy(a => a.PerformedAt).ThenBy(a => a.Id).ToListAsync(ct);
        // R-R6: one name per actor, resolved by ActorId, for history, approval, feedback and the stale notice.
        var actorIds = audits.Select(a => a.ActorAccountId).Concat(actions.Select(a => (Guid?)a.PerformedByAccountId)).OfType<Guid>().Distinct().ToList();
        var actors = await db.Accounts.AsNoTracking().Where(a => actorIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.LoginName, ct);
        var consumed = new HashSet<Guid>();
        var history = new List<AuditEntry>(actions.Count + audits.Count);
        foreach (var action in actions)
        {
            var match = audits.FirstOrDefault(a => !consumed.Contains(a.Id) && Matches(a, action));
            if (match is not null) { consumed.Add(match.Id); history.Add(match); continue; }
            history.Add(AuditPresenter.FromReviewAction(action, actors.GetValueOrDefault(action.PerformedByAccountId) ?? "Historical actor", Details.EventId));
        }
        history.AddRange(audits.Where(a => !consumed.Contains(a.Id)));
        return history.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Select(a => new HistoryView(a, a.Action, a.ActorAccountId is { } actorId && actors.TryGetValue(actorId, out var login) ? login : a.ActorUsername, a.OccurredAt)).ToList();

        static bool Matches(AuditEntry audit, ReviewAction action)
        {
            var expected = AuditPresenter.ActionKey(action.Action);
            var actionMatches = string.Equals(audit.Action, expected, StringComparison.Ordinal) || action.Action == ReviewActionType.ReplaceEvidence && audit.Action == "submission.corrected";
            return actionMatches && audit.ActorAccountId == action.PerformedByAccountId && audit.OccurredAt == action.PerformedAt;
        }
    }

    public sealed class ReviewInput { [StringLength(MaxReviewReasonLength)] public string? Reason { get; set; } public Guid BoardTileId { get; set; } public Guid RequirementId { get; set; } public Guid? DropSnapshotId { get; set; } public Guid CreditedOsrsCharacterId { get; set; } public int? ExpectedVersion { get; set; } }

    public sealed record DetailsView(Guid Id, Guid EventId, Guid TeamId, Guid TileId, Guid RequirementId, Guid? DropId, Guid PlayerId, Guid CharacterId, string Team, string Tile, string Requirement, string? Boss, string? Drop, string Player, SubmissionStatus Status, int Claimed, int Approved, DateTimeOffset SubmittedAt, string? CaptainNote, string? Note, string? ExpectedCode, int Version = 1, int? MinutesAfterEventEnd = null, DateTimeOffset? EventEndsAt = null, IReadOnlyList<EligibilityGapView>? EligibilityGaps = null, DateTimeOffset? CreditedParticipantLeftTeamAt = null, bool Manual = false);
    public sealed record ApprovalBlockView(Guid SubmissionId, DateTimeOffset SubmittedAt);
    public sealed record EligibilityGapView(DateTimeOffset StartedAt, DateTimeOffset ResumedAt);
    public sealed record AssetView(Guid Id, string Filename, string MediaType, long Bytes, int Width, int Height, string Checksum, DateTimeOffset UploadedAt, EvidenceAssetRole Role, bool Active);
    public sealed record ContextView(Guid Id, string Player, int Amount, DateTimeOffset SubmittedAt, string? Drop);
    public sealed record ChecksumMatch(Guid Id, string Tile, string Account, DateTimeOffset SubmittedAt, SubmissionStatus Status);
    public sealed record HistoryView(AuditEntry Entry, string Action, string Actor, DateTimeOffset At);
    public sealed record DecisionView(string Actor, DateTimeOffset At, string? Text);
    public sealed record NeighbourView(int Position, int Count, Guid? PreviousId, Guid? NextId);
    public sealed record RequirementOption(Guid Id, Guid TileId, string Tile, string Description, bool Manual, int Weight);
    public sealed record DropOption(Guid Id, Guid RequirementId, string Boss, string Item, int Weight);
}
