using System.Globalization;
using Bingo.Application.Evidence;
using Bingo.Application.Teams;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Evidence;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Bingo.Web.UI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Submissions;

[Authorize]
public sealed class IndexModel(
    ApplicationDbContext db,
    IEvidenceAuthority evidenceAuthority,
    ITeamFocusService teamFocus,
    TimeProvider time,
    IStringLocalizer<SharedResource> text) : PageModel
{
    public const int PageSize = 25;

    public string EventName { get; private set; } = string.Empty;
    public string EventTimezone { get; private set; } = DateTimePresentation.DefaultTimezoneId;
    public string TeamName { get; private set; } = string.Empty;
    public string EventSlug { get; private set; } = string.Empty;
    public string TeamSlug { get; private set; } = string.Empty;
    public Guid EventId { get; private set; }
    public Guid TeamId { get; private set; }
    public bool IsCaptainWorkspace { get; private set; }
    public int BoardRows { get; private set; }
    public int BoardColumns { get; private set; }
    public string? CurrentCode { get; private set; }
    public bool CodeEnabled { get; private set; }
    public bool NewSubmissionsOpen { get; private set; }
    public TeamFocusContext Focus { get; private set; } = null!;
    public IReadOnlyList<TileView> FocusTiles { get; private set; } = [];
    public IReadOnlyList<LineView> FocusRows { get; private set; } = [];
    public IReadOnlyList<LineView> FocusColumns { get; private set; } = [];
    public SummaryView Summary { get; private set; } = new(0, 0, 0);
    public IReadOnlyList<PlayerOption> PlayerOptions { get; private set; } = [];
    public IReadOnlyList<SubmissionView> Submissions { get; private set; } = [];
    public int PageNumber { get; private set; }
    public int TotalPages { get; private set; }
    public int TotalSubmissionCount { get; private set; }
    public SubmissionLedgerViewModel Ledger => new(EventId, TeamId, "/Submissions/Index", "/Submissions/Submission", Submissions, PageNumber, TotalPages, TotalSubmissionCount, Search, PlayerFilter, EventTimezone);

    [BindProperty(SupportsGet = true, Name = "search")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "player")]
    public Guid? PlayerFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "ledgerPage")]
    public int RequestedLedgerPage { get; set; } = 1;

    [BindProperty]
    public FocusInputModel FocusInput { get; set; } = new();

    [BindProperty]
    public AddFocusInputModel AddFocusInput { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid? eventId, Guid? teamId, CancellationToken cancellationToken)
    {
        var scope = await ResolveScopeAsync(eventId, teamId, cancellationToken);
        if (scope is null || !await LoadAsync(scope, cancellationToken)) return NotFound();
        ViewData["BodyClass"] = "public-ui-pass2";
        ViewData["CompactNavigation"] = true;
        return Page();
    }

    public async Task<IActionResult> OnGetLedgerAsync(Guid? eventId, Guid? teamId, string? search, Guid? player, int ledgerPage = 1, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveScopeAsync(eventId, teamId, cancellationToken);
        if (scope is null) return NotFound();
        var board = await db.Boards.AsNoTracking()
            .SingleOrDefaultAsync(value => value.EventId == scope.EventId && value.State == BoardState.Published, cancellationToken);
        if (board is null) return NotFound();
        EventId = scope.EventId;
        TeamId = scope.TeamId;
        IsCaptainWorkspace = scope.Kind is EvidenceActorKind.Captain or EvidenceActorKind.EmergencyCaptain;
        Search = search;
        PlayerFilter = player;
        RequestedLedgerPage = ledgerPage;
        await LoadLedgerAsync(scope, board, includePlayerOptions: false, cancellationToken: cancellationToken);
        return Partial("/Pages/Captain/_SubmissionLedger.cshtml", Ledger);
    }

    public async Task<IActionResult> OnPostToggleFocusAsync(Guid eventId, Guid teamId, CancellationToken cancellationToken)
    {
        var scope = await ResolveScopeAsync(eventId, teamId, cancellationToken);
        if (scope is null) return NotFound();
        var result = await teamFocus.SetFocusAsync(new(
            eventId, teamId, FocusInput.TargetKind, FocusInput.BoardTileId, FocusInput.RowIndex,
            FocusInput.ColumnIndex, FocusInput.Focused, FocusInput.ExpectedVersion, scope.ActorAccountId), cancellationToken);
        SetFocusMessage(result);
        return RedirectToIndex(eventId, teamId);
    }

    public async Task<IActionResult> OnPostAddFocusAsync(Guid eventId, Guid teamId, CancellationToken cancellationToken)
    {
        var scope = await ResolveScopeAsync(eventId, teamId, cancellationToken);
        if (scope is null) return NotFound();

        if (!TryTranslateFocusTarget(AddFocusInput.TargetKind, AddFocusInput.TargetValue,
                out var targetKind, out var boardTileId, out var rowIndex, out var columnIndex, out var expectedVersion))
        {
            SetFocusMessage(new(false, text["That focus target is not part of this board."].Value));
            return RedirectToIndex(eventId, teamId);
        }

        var result = await teamFocus.SetFocusAsync(new(
            eventId, teamId, targetKind, boardTileId, rowIndex, columnIndex, true, expectedVersion, scope.ActorAccountId), cancellationToken);
        SetFocusMessage(result);
        return RedirectToIndex(eventId, teamId);
    }

    public async Task<IActionResult> OnPostClearAllFocusAsync(Guid eventId, Guid teamId, CancellationToken cancellationToken)
    {
        var scope = await ResolveScopeAsync(eventId, teamId, cancellationToken);
        if (scope is null) return NotFound();
        var result = await teamFocus.ClearAllFocusAsync(new(
            eventId, teamId,
            FocusInput.MarkerVersions.Select(marker => new TeamFocusMarkerVersion(marker.Id, marker.Version)).ToList(),
            scope.ActorAccountId), cancellationToken);
        SetFocusMessage(result);
        return RedirectToIndex(eventId, teamId);
    }

    private async Task<EvidenceActorScope?> ResolveScopeAsync(Guid? requestedEventId, Guid? requestedTeamId, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();
        if (accountId is not { } actorAccountId) return null;

        EvidenceActorScope scope;
        try
        {
            scope = await evidenceAuthority.ResolveActorAsync(
                actorAccountId, requestedEventId ?? User.GetEventId(), requestedTeamId ?? User.GetTeamId(),
                time.GetUtcNow(), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        if (scope.Kind == EvidenceActorKind.Administrator)
        {
            var leadership = await (from participant in db.EventParticipants.AsNoTracking()
                                    join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
                                    join authorizedTeam in db.Teams.AsNoTracking() on membership.TeamId equals authorizedTeam.Id
                                    where participant.AccountId == actorAccountId && participant.EventId == scope.EventId &&
                                          membership.TeamId == scope.TeamId && membership.LeftAt == null && authorizedTeam.Active &&
                                          (membership.Role == TeamMembershipRole.Captain || membership.Role == TeamMembershipRole.CoCaptain)
                                    select new { participant.EventId, TeamId = authorizedTeam.Id, ParticipantId = participant.Id })
                .SingleOrDefaultAsync(cancellationToken);
            if (leadership is null) return null;
            scope = new(EvidenceActorKind.Captain, actorAccountId, leadership.EventId, leadership.TeamId, leadership.ParticipantId);
        }

        if (scope.Kind is not (EvidenceActorKind.Participant or EvidenceActorKind.Captain or EvidenceActorKind.EmergencyCaptain)) return null;

        var validScope = await (from eventItem in db.Events.AsNoTracking()
                                join team in db.Teams.AsNoTracking() on eventItem.Id equals team.EventId
                                where eventItem.Id == scope.EventId && eventItem.HiddenAt == null && team.Id == scope.TeamId && team.Active
                                select eventItem.Id).SingleOrDefaultAsync(cancellationToken);
        return validScope == Guid.Empty ? null : scope;
    }

    private async Task<bool> LoadAsync(EvidenceActorScope scope, CancellationToken cancellationToken)
    {
        EventId = scope.EventId;
        TeamId = scope.TeamId;
        IsCaptainWorkspace = scope.Kind is EvidenceActorKind.Captain or EvidenceActorKind.EmergencyCaptain;
        var now = time.GetUtcNow();
        var context = await (from eventItem in db.Events.AsNoTracking()
                             join team in db.Teams.AsNoTracking() on eventItem.Id equals team.EventId
                             where eventItem.Id == scope.EventId && eventItem.HiddenAt == null && team.Id == scope.TeamId && team.Active
                             select new { eventItem.Name, eventItem.Slug, eventItem.Timezone, TeamName = team.Name, TeamSlug = team.Slug }).SingleOrDefaultAsync(cancellationToken);
        if (context is null) return false;
        EventName = context.Name;
        EventTimezone = context.Timezone;
        EventSlug = context.Slug;
        TeamName = context.TeamName;
        TeamSlug = context.TeamSlug;
        var eventRecord = await db.Events.AsNoTracking().SingleAsync(value => value.Id == scope.EventId && value.HiddenAt == null, cancellationToken);
        CodeEnabled = eventRecord.EvidenceCodeEnabled;
        NewSubmissionsOpen = eventRecord.AcceptsNewSubmissions(now);

        if (CodeEnabled)
        {
            CurrentCode = await db.EvidenceCodes.AsNoTracking()
                .Where(value => value.EventId == scope.EventId && value.ActivatesAt <= now && (value.RetiresAt == null || value.RetiresAt > now))
                .OrderByDescending(value => value.ActivatesAt)
                .Select(value => value.Code)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var board = await db.Boards.AsNoTracking()
            .SingleOrDefaultAsync(value => value.EventId == scope.EventId && value.State == BoardState.Published, cancellationToken);
        if (board is null) return false;
        BoardRows = board.Rows;
        BoardColumns = board.Columns;

        if (IsCaptainWorkspace)
        {
            Focus = await teamFocus.GetContextAsync(scope.EventId, scope.TeamId, scope.ActorAccountId, false, cancellationToken)
                ?? throw new InvalidOperationException("The team focus projection was not available for the authorized scope.");
            await LoadFocusTargetsAsync(board, cancellationToken);
            await LoadSummaryAsync(scope, cancellationToken);
        }
        await LoadLedgerAsync(scope, board, includePlayerOptions: true, cancellationToken: cancellationToken);
        return true;
    }

    private async Task LoadFocusTargetsAsync(Board board, CancellationToken cancellationToken)
    {
        var tiles = await db.BoardTiles.AsNoTracking()
            .Where(value => value.BoardId == board.Id)
            .OrderBy(value => value.RowIndex).ThenBy(value => value.ColumnIndex)
            .Select(value => new { value.Id, value.RowIndex, value.ColumnIndex, value.NameSnapshot })
            .ToListAsync(cancellationToken);
        var tileIds = tiles.Select(value => value.Id).ToList();
        var requirements = await db.BoardRequirementSnapshots.AsNoTracking()
            .Where(value => tileIds.Contains(value.BoardTileId))
            .Select(value => new { value.Id, value.BoardTileId, value.TargetContribution })
            .ToListAsync(cancellationToken);
        var requirementIds = requirements.Select(value => value.Id).ToList();
        var contributions = await db.SubmissionContributions.AsNoTracking()
            .Where(value => value.TeamId == TeamId && requirementIds.Contains(value.RequirementId) && value.ReversedAt == null)
            .GroupBy(value => value.RequirementId)
            .Select(group => new { RequirementId = group.Key, Amount = group.Sum(value => value.Amount) })
            .ToDictionaryAsync(value => value.RequirementId, value => value.Amount, cancellationToken);

        var focusMarkers = Focus.Markers;
        FocusTiles = tiles.Select(tile =>
        {
            var tileRequirements = requirements.Where(value => value.BoardTileId == tile.Id).ToList();
            var progress = tileRequirements.Sum(value => Math.Min(value.TargetContribution, contributions.GetValueOrDefault(value.Id)));
            var target = tileRequirements.Sum(value => value.TargetContribution);
            var complete = tileRequirements.Count > 0 && tileRequirements.All(value => contributions.GetValueOrDefault(value.Id) >= value.TargetContribution);
            var marker = focusMarkers.SingleOrDefault(value => value.TargetKind == TeamFocusTargetKind.Tile && value.BoardTileId == tile.Id);
            return new TileView(tile.Id, tile.RowIndex, tile.ColumnIndex, tile.NameSnapshot, complete, progress, target, marker?.Focused == true, marker?.Version ?? 0);
        }).Where(value => !value.Complete).ToList();

        FocusRows = Enumerable.Range(0, board.Rows)
            .Select(row => LineView.ForRow(row, focusMarkers.SingleOrDefault(value => value.TargetKind == TeamFocusTargetKind.Row && value.RowIndex == row)))
            .ToList();
        FocusColumns = Enumerable.Range(0, board.Columns)
            .Select(column => LineView.ForColumn(column, focusMarkers.SingleOrDefault(value => value.TargetKind == TeamFocusTargetKind.Column && value.ColumnIndex == column)))
            .ToList();
    }

    private async Task LoadSummaryAsync(EvidenceActorScope scope, CancellationToken cancellationToken)
    {
        var teamSubmissions = db.Submissions.AsNoTracking().Where(value => value.EventId == scope.EventId && value.TeamId == scope.TeamId);
        var replacedIds = db.Submissions.AsNoTracking()
            .Where(value => value.EventId == scope.EventId && value.TeamId == scope.TeamId && value.ResubmissionOfSubmissionId != null)
            .Select(value => value.ResubmissionOfSubmissionId!.Value);
        Summary = new(
            await teamSubmissions.CountAsync(value => value.Status == SubmissionStatus.Pending, cancellationToken),
            await teamSubmissions.CountAsync(value => value.Status == SubmissionStatus.Rejected && !replacedIds.Contains(value.Id), cancellationToken),
            await teamSubmissions.CountAsync(value => value.Status == SubmissionStatus.Approved, cancellationToken));
    }

    private async Task LoadLedgerAsync(EvidenceActorScope scope, Board board, bool includePlayerOptions, CancellationToken cancellationToken)
    {
        var replacedIds = db.Submissions.AsNoTracking()
            .Where(value => value.EventId == scope.EventId && value.TeamId == scope.TeamId && value.ResubmissionOfSubmissionId != null)
            .Select(value => value.ResubmissionOfSubmissionId!.Value);
        var submissions = db.Submissions.AsNoTracking()
            .Where(value => value.EventId == scope.EventId && value.TeamId == scope.TeamId);
        if (PlayerFilter is { } playerId) submissions = submissions.Where(value => value.CreditedParticipantId == playerId);

        var ledger = from submission in submissions
                     join tile in db.BoardTiles.AsNoTracking() on submission.BoardTileId equals tile.Id
                     join drop in db.BoardRequirementDropSnapshots.AsNoTracking() on submission.DropSnapshotId equals drop.Id into dropGroup
                     from drop in dropGroup.DefaultIfEmpty()
                     where tile.BoardId == board.Id
                     select new
                     {
                         Submission = submission,
                         Tile = tile.NameSnapshot,
                         Drop = drop == null ? null : drop.ItemName,
                         IsReplaced = replacedIds.Contains(submission.Id)
                     };
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var pattern = $"%{Search.Trim()}%";
            ledger = ledger.Where(value =>
                (value.Drop != null && EF.Functions.ILike(value.Drop, pattern)) ||
                EF.Functions.ILike(value.Tile, pattern) ||
                EF.Functions.ILike(value.Submission.CreditedCharacterName, pattern));
        }
        TotalSubmissionCount = await ledger.CountAsync(cancellationToken);
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalSubmissionCount / (double)PageSize));
        PageNumber = Math.Clamp(RequestedLedgerPage, 1, TotalPages);
        Submissions = await ledger.OrderByDescending(value => value.Submission.SubmittedAt).ThenByDescending(value => value.Submission.Id)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .Select(value => new SubmissionView(
                value.Submission.Id,
                value.Drop,
                value.Tile,
                value.Submission.CreditedCharacterName,
                value.IsReplaced ? "Replaced" : value.Submission.Status.ToString(),
                value.Submission.ClaimedWeight,
                value.Submission.ApprovedContribution,
                value.Submission.SubmittedAt,
                value.Submission.CurrentReviewerNote != null))
            .ToListAsync(cancellationToken);

        if (includePlayerOptions)
        {
            PlayerOptions = await db.Submissions.AsNoTracking()
                .Where(value => value.EventId == scope.EventId && value.TeamId == scope.TeamId)
                .Select(value => new { Id = value.CreditedParticipantId, Name = value.CreditedCharacterName })
                .Distinct()
                .OrderBy(value => value.Name).ThenBy(value => value.Id)
                .Select(value => new PlayerOption(value.Id, value.Name))
                .ToListAsync(cancellationToken);
        }
    }

    private RedirectToPageResult RedirectToIndex(Guid eventId, Guid teamId) => RedirectToPage(new
    {
        eventId,
        teamId,
        search = Search,
        player = PlayerFilter,
        ledgerPage = RequestedLedgerPage
    });

    private void SetFocusMessage(TeamFocusMutationResult result)
    {
        TempData["StatusMessage"] = result.Succeeded ? text["Team focus updated."].Value : text[result.Error ?? "Team focus could not be updated."].Value;
        TempData[Bingo.Web.UI.UiMessage.TypeKey] = (result.Succeeded ? Bingo.Web.UI.UiMessageType.Success : Bingo.Web.UI.UiMessageType.Error).ToString();
    }

    public static bool TryTranslateFocusTarget(
        string? targetKindValue,
        string? targetValue,
        out TeamFocusTargetKind targetKind,
        out Guid? boardTileId,
        out int? rowIndex,
        out int? columnIndex,
        out long expectedVersion)
    {
        targetKind = default;
        boardTileId = null;
        rowIndex = null;
        columnIndex = null;
        expectedVersion = default;
        if (string.IsNullOrWhiteSpace(targetKindValue) || string.IsNullOrWhiteSpace(targetValue)) return false;

        var targetParts = targetValue.Split('|', StringSplitOptions.None);
        if (targetParts is not [var targetIdentity, var versionValue] ||
            string.IsNullOrWhiteSpace(targetIdentity) ||
            !long.TryParse(versionValue, NumberStyles.None, CultureInfo.InvariantCulture, out expectedVersion) ||
            expectedVersion < 0)
            return false;

        switch (targetKindValue.Trim())
        {
            case "Tile":
                if (!Guid.TryParse(targetIdentity, out var tileId)) return false;
                targetKind = TeamFocusTargetKind.Tile;
                boardTileId = tileId;
                return true;
            case "Row":
                if (!int.TryParse(targetIdentity, NumberStyles.None, CultureInfo.InvariantCulture, out var row) || row < 0) return false;
                targetKind = TeamFocusTargetKind.Row;
                rowIndex = row;
                return true;
            case "Column":
                if (!int.TryParse(targetIdentity, NumberStyles.None, CultureInfo.InvariantCulture, out var column) || column < 0) return false;
                targetKind = TeamFocusTargetKind.Column;
                columnIndex = column;
                return true;
            default:
                return false;
        }
    }

    public sealed class FocusInputModel
    {
        public TeamFocusTargetKind TargetKind { get; set; }
        public Guid? BoardTileId { get; set; }
        public int? RowIndex { get; set; }
        public int? ColumnIndex { get; set; }
        public bool Focused { get; set; }
        public long ExpectedVersion { get; set; }
        public List<FocusMarkerVersionInput> MarkerVersions { get; set; } = [];
    }

    public sealed class AddFocusInputModel
    {
        public string? TargetKind { get; set; }
        public string? TargetValue { get; set; }
    }

    public sealed class FocusMarkerVersionInput
    {
        public Guid Id { get; set; }
        public long Version { get; set; }
    }

    public sealed record TileView(Guid Id, int Row, int Column, string Name, bool Complete, int Progress, int Target, bool Focused, long Version);
    public sealed record LineView(string Label, string Kind, int Index, bool Focused, long Version)
    {
        public static LineView ForRow(int index, TeamFocusMarkerView? marker) => new($"Row {index + 1}", "Row", index, marker?.Focused == true, marker?.Version ?? 0);
        public static LineView ForColumn(int index, TeamFocusMarkerView? marker) => new($"Column {index + 1}", "Column", index, marker?.Focused == true, marker?.Version ?? 0);
    }

    public sealed record SummaryView(int Pending, int Rejected, int Approved);
    public sealed record PlayerOption(Guid Id, string Name);
    public sealed record SubmissionView(Guid Id, string? Drop, string Tile, string Player, string DisplayStatus, int Claimed, int Approved, DateTimeOffset SubmittedAt, bool HasFeedback);
}
