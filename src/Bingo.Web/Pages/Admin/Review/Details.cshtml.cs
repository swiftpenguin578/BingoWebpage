using System.ComponentModel.DataAnnotations;
using Bingo.Application.Evidence;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Npgsql;

namespace Bingo.Web.Pages.Admin.Review;

public sealed class DetailsModel(ApplicationDbContext db, ISubmissionService service, IStringLocalizer<SharedResource>? text = null) : PageModel
{
    private const int MaxReviewReasonLength = 4000;
    private const string ReasonLengthError = "Reason must be 4000 characters or fewer.";
    private const string ApprovalBlockTempDataKey = "ReviewApprovalBlockSubmissionId";
    public DetailsView Details { get; private set; } = null!; public string EventTimezone { get; private set; } = DateTimePresentation.DefaultTimezoneId; public IReadOnlyList<AssetView> Assets { get; private set; } = []; public IReadOnlyList<AuditEntry> AuditHistory { get; private set; } = []; public IReadOnlyList<ContextView> PriorApproved { get; private set; } = []; public IReadOnlyList<ChecksumMatch> ChecksumMatches { get; private set; } = []; public IReadOnlyList<Option> Characters { get; private set; } = []; public IReadOnlyList<RequirementOption> Requirements { get; private set; } = []; public IReadOnlyList<DropOption> Drops { get; private set; } = []; public ApprovalBlockView? ApprovalBlock { get; private set; }
    public bool ReviewOpen { get; private set; }
    [BindProperty(SupportsGet = true)] public string Search { get; set; } = string.Empty;
    [BindProperty(SupportsGet = true)] public SubmissionStatus? Status { get; set; }
    [BindProperty] public ReviewInput Input { get; set; } = new();
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) { if (!await Load(id, ct)) return NotFound(); Input = new() { BoardTileId = Details.TileId, RequirementId = Details.RequirementId, DropSnapshotId = Details.DropId, CreditedOsrsCharacterId = Details.CharacterId, Reason = Details.Note, ExpectedVersion = Details.Version }; return Page(); }
    public Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken ct) => Execute(id, async () => { var result = await service.ApproveAsync(id, User.GetAccountId()!.Value, ct, Input.ExpectedVersion); if (result.BlockingSubmission is { } block) { TempData[ApprovalBlockTempDataKey] = block.SubmissionId.ToString("D"); TempData["StatusMessage"] = "Approve or reject the earlier upload first."; TempData[UiMessage.TypeKey] = UiMessageType.Error.ToString(); } else { TempData["StatusMessage"] = Localize("Approved with {0} contribution.", result.ApprovedContribution); TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString(); } }, ct);
    public Task<IActionResult> OnPostRejectAsync(Guid id, CancellationToken ct) => Execute(id, () => service.RejectAsync(id, User.GetAccountId()!.Value, Input.Reason ?? string.Empty, ct, Input.ExpectedVersion), ct, "Submission rejected.", requiresReason: true);
    public Task<IActionResult> OnPostReverseAsync(Guid id, CancellationToken ct) => Execute(id, () => service.ReverseAsync(id, User.GetAccountId()!.Value, Input.Reason ?? string.Empty, ct, Input.ExpectedVersion), ct, "Approval reversed and later contributions recalculated.", requiresReason: true);
    public Task<IActionResult> OnPostEditAsync(Guid id, CancellationToken ct) => Execute(id, () => service.EditMetadataAsync(new(id, User.GetAccountId()!.Value, Input.BoardTileId, Input.RequirementId, Input.DropSnapshotId, Input.CreditedOsrsCharacterId, Input.Reason ?? string.Empty, Input.ExpectedVersion), ct), ct, "Metadata corrected.", requiresReason: true);
    private async Task<IActionResult> Execute(Guid id, Func<Task> action, CancellationToken ct, string? success = null, bool requiresReason = false)
    {
        if (requiresReason && Input.Reason is { Length: > MaxReviewReasonLength })
        {
            TempData["StatusMessage"] = ReasonLengthError;
            TempData[UiMessage.TypeKey] = UiMessageType.Error.ToString();
            return await RedirectAfterPost(id, ct);
        }
        try
        {
            await action();
            if (success is not null)
            {
                TempData["StatusMessage"] = success;
                TempData[UiMessage.TypeKey] = UiMessageType.Success.ToString();
            }
        }
        catch (Exception ex) when (IsReviewPersistenceConflict(ex))
        {
            TempData["StatusMessage"] = "This review was not saved because the event or evidence changed in another request. Reload the submission, review the latest state, and try again.";
            TempData[UiMessage.TypeKey] = UiMessageType.Error.ToString();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            TempData["StatusMessage"] = ex.Message;
            TempData[UiMessage.TypeKey] = UiMessageType.Error.ToString();
        }
        return await RedirectAfterPost(id, ct);
    }

    private async Task<IActionResult> RedirectAfterPost(Guid id, CancellationToken ct)
    {
        var eventId = await db.Submissions.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.EventId).SingleOrDefaultAsync(ct);
        return RedirectToPage(new { id, eventId, search = Search, status = Status });
    }

    private static bool IsReviewPersistenceConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
            if (current is DbUpdateConcurrencyException or PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected }) return true;
        return false;
    }
    private string Localize(string key, params object[] arguments) => text?[key, arguments].Value ?? string.Format(System.Globalization.CultureInfo.CurrentCulture, key, arguments);
    private async Task<bool> Load(Guid id, CancellationToken ct)
    {
        var s = await db.Submissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return false;
        var eventItem = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == s.EventId && x.HiddenAt == null, ct);
        if (eventItem is null) return false;
        ReviewOpen = EventStatePolicy.Allows(eventItem.State, EventCapability.ReviewEvidence);
        EventTimezone = eventItem.Timezone;
        var team = await db.Teams.AsNoTracking().SingleAsync(x => x.Id == s.TeamId, ct);
        var publication = await db.PublishedObjectivesAsync(s.EventId, ct);
        if (publication is null) return false;
        var tile = publication.Tiles.SingleOrDefault(x => x.Id == s.BoardTileId);
        var req = publication.Requirements.SingleOrDefault(x => x.Id == s.RequirementId && x.BoardTileId == s.BoardTileId);
        if (tile is null || req is null) return false;
        var drop = s.DropSnapshotId is null ? null : publication.Drops.SingleOrDefault(x => x.Id == s.DropSnapshotId && x.RequirementId == s.RequirementId);
        if (s.DropSnapshotId is not null && drop is null) return false;
        var effectiveEnd = eventItem.ActualEndedAt ?? eventItem.EventEndsAt;
        var minutesAfterEnd = effectiveEnd is { } eventEnd && s.SubmittedAt > eventEnd ? (int?)Math.Ceiling((s.SubmittedAt - eventEnd).TotalMinutes) : null;
        var eligibilityGaps = await FindEligibilityGapsAsync(s.EventId, ct);
        Details = new(s.Id, s.EventId, s.TeamId, s.BoardTileId, s.RequirementId, s.DropSnapshotId, s.CreditedParticipantId, s.CreditedOsrsCharacterId, team.Name, tile.NameSnapshot, req.Description, drop?.BossName, drop?.ItemName, s.CreditedCharacterName, s.Status, s.ClaimedWeight, s.ApprovedContribution, s.SubmittedAt, s.CaptainNote, s.CurrentReviewerNote, s.ExpectedEvidenceCode, s.Version, minutesAfterEnd, effectiveEnd, eligibilityGaps);
        ApprovalBlock = await LoadApprovalBlockAsync(s, ct);
        Assets = await db.EvidenceAssets.AsNoTracking().Where(x => x.SubmissionId == id).OrderByDescending(x => x.UploadedAt).Select(x => new AssetView(x.Id, x.OriginalFilename, x.MediaType, x.ByteSize, x.PixelWidth, x.PixelHeight, x.Checksum, x.UploadedAt, x.Role, x.Active)).ToListAsync(ct);
        var activeChecksum = Assets.FirstOrDefault(x => x.Active)?.Checksum; if (activeChecksum is not null) ChecksumMatches = await (from asset in db.EvidenceAssets.AsNoTracking() join other in db.Submissions on asset.SubmissionId equals other.Id join otherTile in db.BoardTiles on other.BoardTileId equals otherTile.Id where asset.Checksum == activeChecksum && asset.Active && other.EventId == s.EventId && other.Id != s.Id orderby other.SubmittedAt descending select new ChecksumMatch(other.Id, otherTile.NameSnapshot, other.SubmittedAt, other.Status)).ToListAsync(ct);
        AuditHistory = await LoadReviewHistoryAsync(id, s.EventId, ct); PriorApproved = await db.Submissions.AsNoTracking().Where(other => other.TeamId == s.TeamId && other.RequirementId == s.RequirementId && other.Status == SubmissionStatus.Approved && other.Id != s.Id).OrderByDescending(other => other.SubmittedAt).Select(other => new ContextView(other.Id, other.CreditedCharacterName, other.ApprovedContribution, other.SubmittedAt)).ToListAsync(ct);
        Characters = await (from assignment in db.EventParticipantCharacters.AsNoTracking() join membership in db.TeamMemberships.AsNoTracking() on assignment.EventParticipantId equals membership.EventParticipantId join character in db.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id where assignment.EventId == s.EventId && membership.TeamId == s.TeamId && membership.LeftAt == null && assignment.EventRole == Bingo.Domain.Signups.EventCharacterRole.Playing && assignment.ReleasedAt == null orderby character.DisplayName select new Option(character.Id, character.DisplayName)).Distinct().ToListAsync(ct); var board = await db.Boards.AsNoTracking().SingleAsync(x => x.EventId == s.EventId, ct); var tiles = publication.Tiles; var tileMap = tiles.ToDictionary(x => x.Id); var tileIds = tiles.Select(x => x.Id).ToList(); var requirements = publication.Requirements.OrderBy(x => x.Position).ToList(); Requirements = requirements.Select(x => new RequirementOption(x.Id, x.BoardTileId, tileMap[x.BoardTileId].NameSnapshot, x.Description, x.ManualObjective, x.AllowHigherWeightings)).ToList(); var reqIds = requirements.Select(x => x.Id).ToList(); Drops = publication.Drops.OrderBy(x => x.BossName).ThenBy(x => x.ItemName).Select(x => new DropOption(x.Id, x.RequirementId, x.BossName, x.ItemName, x.DisplayRate)).ToList(); return true;
    }
    private async Task<ApprovalBlockView?> LoadApprovalBlockAsync(Submission current, CancellationToken ct)
    {
        if (TempData is null || !TempData.TryGetValue(ApprovalBlockTempDataKey, out var value) || !Guid.TryParse(value?.ToString(), out var submissionId)) return null;
        return await db.Submissions.AsNoTracking()
            .Where(x => x.Id == submissionId && x.EventId == current.EventId && x.TeamId == current.TeamId &&
                        x.BoardTileId == current.BoardTileId && x.RequirementId == current.RequirementId &&
                        x.Status == SubmissionStatus.Pending &&
                        (x.SubmittedAt < current.SubmittedAt || x.SubmittedAt == current.SubmittedAt && x.Id.CompareTo(current.Id) < 0))
            .Select(x => new ApprovalBlockView(x.Id, x.SubmittedAt))
            .SingleOrDefaultAsync(ct);
    }
    private async Task<IReadOnlyList<AuditEntry>> LoadReviewHistoryAsync(Guid submissionId, Guid eventId, CancellationToken ct)
    {
        var targetId = submissionId.ToString("D");
        var audits = await db.AuditEntries.AsNoTracking()
            .Where(a => a.TargetType == "submission" && a.TargetId == targetId)
            .ToListAsync(ct);
        var actions = await db.ReviewActions.AsNoTracking()
            .Where(a => a.SubmissionId == submissionId)
            .OrderBy(a => a.PerformedAt).ThenBy(a => a.Id)
            .ToListAsync(ct);
        if (actions.Count == 0) return audits.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).ToList();

        var actorIds = actions.Select(a => a.PerformedByAccountId).Distinct().ToList();
        var actors = await db.Accounts.AsNoTracking().Where(a => actorIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.LoginName, ct);
        var consumedAuditIds = new HashSet<Guid>();
        var history = new List<AuditEntry>(actions.Count + audits.Count);
        foreach (var action in actions)
        {
            var matchingAudit = audits.FirstOrDefault(a => !consumedAuditIds.Contains(a.Id) && Matches(a, action, targetId));
            if (matchingAudit is not null)
            {
                consumedAuditIds.Add(matchingAudit.Id);
                history.Add(matchingAudit);
                continue;
            }

            var actor = actors.GetValueOrDefault(action.PerformedByAccountId) ?? "Historical actor";
            history.Add(AuditPresenter.FromReviewAction(action, actor, eventId));
        }

        history.AddRange(audits.Where(a => !consumedAuditIds.Contains(a.Id)));
        return history.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).ToList();

        static bool Matches(AuditEntry audit, ReviewAction action, string targetId)
        {
            var expectedAction = AuditPresenter.ActionKey(action.Action);
            var actionMatches = string.Equals(audit.Action, expectedAction, StringComparison.Ordinal)
                || action.Action == ReviewActionType.ReplaceEvidence && audit.Action == "submission.corrected";
            return actionMatches && audit.TargetId == targetId && audit.ActorAccountId == action.PerformedByAccountId &&
                   audit.OccurredAt == action.PerformedAt;
        }
    }

    public sealed class ReviewInput { [StringLength(MaxReviewReasonLength)] public string? Reason { get; set; } public Guid BoardTileId { get; set; } public Guid RequirementId { get; set; } public Guid? DropSnapshotId { get; set; } public Guid CreditedOsrsCharacterId { get; set; } public int? ExpectedVersion { get; set; } }
    private async Task<IReadOnlyList<EligibilityGapView>> FindEligibilityGapsAsync(Guid eventId, CancellationToken ct)
    {
        var transitions = await db.EventStateTransitions.AsNoTracking().Where(x => x.EventId == eventId &&
            (x.ToState == EventState.AwaitingFinalReview || x.ToState == EventState.Live)).OrderBy(x => x.EffectiveAt).ThenBy(x => x.PerformedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var gaps = new List<EligibilityGapView>();
        DateTimeOffset? gapStarted = null;
        foreach (var transition in transitions)
        {
            if (transition.ToState == EventState.AwaitingFinalReview) gapStarted = transition.EffectiveAt;
            else if (transition.ToState == EventState.Live && gapStarted is { } started)
            {
                var resumed = transition.EffectiveAt;
                if (resumed > started) gaps.Add(new EligibilityGapView(started, resumed));
                gapStarted = null;
            }
        }
        return gaps;
    }

    public sealed record DetailsView(Guid Id, Guid EventId, Guid TeamId, Guid TileId, Guid RequirementId, Guid? DropId, Guid PlayerId, Guid CharacterId, string Team, string Tile, string Requirement, string? Boss, string? Drop, string Player, SubmissionStatus Status, int Claimed, int Approved, DateTimeOffset SubmittedAt, string? CaptainNote, string? Note, string? ExpectedCode, int Version = 1, int? MinutesAfterEventEnd = null, DateTimeOffset? EventEndsAt = null, IReadOnlyList<EligibilityGapView>? EligibilityGaps = null); public sealed record ApprovalBlockView(Guid SubmissionId, DateTimeOffset SubmittedAt); public sealed record EligibilityGapView(DateTimeOffset StartedAt, DateTimeOffset ResumedAt); public sealed record AssetView(Guid Id, string Filename, string MediaType, long Bytes, int Width, int Height, string Checksum, DateTimeOffset UploadedAt, EvidenceAssetRole Role, bool Active); public sealed record ContextView(Guid Id, string Player, int Amount, DateTimeOffset SubmittedAt); public sealed record ChecksumMatch(Guid Id, string Tile, DateTimeOffset SubmittedAt, SubmissionStatus Status); [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords")] public sealed record Option(Guid Id, string Label); public sealed record RequirementOption(Guid Id, Guid TileId, string Tile, string Description, bool Manual, bool Higher); public sealed record DropOption(Guid Id, Guid RequirementId, string Boss, string Item, string Rate);
}
