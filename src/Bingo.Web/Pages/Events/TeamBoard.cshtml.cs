using Bingo.Application.Boards;
using Bingo.Application.Signups;
using Bingo.Application.Teams;
using Bingo.Domain.Teams;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Events;

public sealed class TeamBoardModel(IPublicBoardService boards, IParticipantLiveService live, ITeamFocusService focus) : PageModel
{
    public PublicEventBoard Board { get; private set; } = null!;
    public PublicTeamBoard Team { get; private set; } = null!;
    public PublicTeamBoard? Previous { get; private set; }
    public PublicTeamBoard? Next { get; private set; }
    public ParticipantLiveContext? ParticipantContext { get; private set; }
    public IReadOnlyList<ParticipantLiveContext> LiveContexts { get; private set; } = [];
    public TeamFocusContext? Focus { get; private set; }
    public bool InspectFocus { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, string teamSlug, Guid? participantId, CancellationToken cancellationToken, bool inspectFocus = false)
    {
        var board = await boards.GetEventBoardAsync(slug, cancellationToken);
        if (board is null) return NotFound();
        var index = board.Teams.ToList().FindIndex(value => value.TeamSlug == teamSlug);
        if (index < 0) return NotFound();
        Board = board;
        Team = board.Teams[index];
        Previous = index > 0 ? board.Teams[index - 1] : null;
        Next = index + 1 < board.Teams.Count ? board.Teams[index + 1] : null;
        if (!await LoadLiveContextAsync(participantId, cancellationToken)) return Forbid();
        InspectFocus = inspectFocus;
        var accountId = User.GetAccountId();
        if (accountId is { } viewerAccountId)
            Focus = await focus.GetContextAsync(Board.EventId, Team.TeamId, viewerAccountId, inspectFocus, cancellationToken);
        ViewData["BodyClass"] = "public-event-shell";
        ViewData["CompactNavigation"] = true;
        ViewData["HideBreadcrumbs"] = true;
        return Page();
    }

    public async Task<IActionResult> OnPostSwapAsync(string slug, string teamSlug, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();
        if (accountId is null) return Challenge();
        var board = await boards.GetEventBoardAsync(slug, cancellationToken);
        var team = board?.Teams.SingleOrDefault(value => value.TeamSlug == teamSlug);
        if (board is null || team is null) return NotFound();
        var result = await live.SwapAsync(new(
            board.EventId, Input.ParticipantId, Input.ExpectedCurrentCharacterId, Input.NextCharacterId,
            accountId.Value, User.Identity?.Name ?? "participant"), cancellationToken);
        TempData["StatusMessage"] = result.Succeeded
            ? $"Account swap saved. It becomes active at {result.EffectiveAtUtc!.Value:dd MMM yyyy HH:mm 'UTC'}."
            : result.Error ?? "The account swap could not be saved.";
        TempData[Bingo.Web.UI.UiMessage.TypeKey] = (result.Succeeded ? Bingo.Web.UI.UiMessageType.Success : Bingo.Web.UI.UiMessageType.Error).ToString();
        return RedirectToPage(new { slug, teamSlug, participantId = Input.ParticipantId });
    }

    public async Task<IActionResult> OnPostToggleFocusAsync(string slug, string teamSlug, bool inspectFocus, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();
        if (accountId is null) return Challenge();
        var board = await boards.GetEventBoardAsync(slug, cancellationToken);
        var team = board?.Teams.SingleOrDefault(value => value.TeamSlug == teamSlug);
        if (board is null || team is null) return NotFound();
        var result = await focus.SetFocusAsync(new(
            board.EventId, team.TeamId, FocusInput.TargetKind, FocusInput.BoardTileId,
            FocusInput.RowIndex, FocusInput.ColumnIndex, FocusInput.Focused,
            FocusInput.ExpectedVersion, accountId.Value), cancellationToken);
        TempData["StatusMessage"] = result.Succeeded
            ? "Team focus updated."
            : result.Error ?? "Team focus could not be updated.";
        TempData[Bingo.Web.UI.UiMessage.TypeKey] = (result.Succeeded ? Bingo.Web.UI.UiMessageType.Success : Bingo.Web.UI.UiMessageType.Error).ToString();
        return RedirectToPage(new { slug, teamSlug, inspectFocus });
    }

    public async Task<IActionResult> OnPostClearAllFocusAsync(string slug, string teamSlug, bool inspectFocus, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();
        if (accountId is null) return Challenge();
        var board = await boards.GetEventBoardAsync(slug, cancellationToken);
        var team = board?.Teams.SingleOrDefault(value => value.TeamSlug == teamSlug);
        if (board is null || team is null) return NotFound();
        var result = await focus.ClearAllFocusAsync(new(
            board.EventId, team.TeamId,
            FocusInput.MarkerVersions.Select(value => new TeamFocusMarkerVersion(value.Id, value.Version)).ToList(),
            accountId.Value), cancellationToken);
        TempData["StatusMessage"] = result.Succeeded
            ? "All team focus cleared."
            : result.Error ?? "Team focus could not be cleared.";
        TempData[Bingo.Web.UI.UiMessage.TypeKey] = (result.Succeeded ? Bingo.Web.UI.UiMessageType.Success : Bingo.Web.UI.UiMessageType.Error).ToString();
        return RedirectToPage(new { slug, teamSlug, inspectFocus });
    }

    [BindProperty]
    public SwapInput Input { get; set; } = new();

    [BindProperty]
    public FocusInputModel FocusInput { get; set; } = new();

    private async Task<bool> LoadLiveContextAsync(Guid? participantId, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();
        if (accountId is not { } viewerAccountId) return participantId is null;
        LiveContexts = await live.GetTeamContextsAsync(Board.EventId, Team.TeamId, viewerAccountId, cancellationToken);
        if (participantId is { } selected)
        {
            ParticipantContext = LiveContexts.SingleOrDefault(context => context.ParticipantId == selected);
            return ParticipantContext is not null;
        }
        ParticipantContext = LiveContexts.Count > 0 ? LiveContexts[0] : null;
        return true;
    }

    public sealed class SwapInput
    {
        public Guid ParticipantId { get; set; }
        public Guid ExpectedCurrentCharacterId { get; set; }
        public Guid NextCharacterId { get; set; }
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

    public sealed class FocusMarkerVersionInput
    {
        public Guid Id { get; set; }
        public long Version { get; set; }
    }

    public static TeamFocusPresentation GetFocusPresentation(
        PublicTileProgress tile,
        IReadOnlyList<TeamFocusMarkerView> markers)
    {
        var tileIsIncomplete = !tile.Complete;
        return new(
            tileIsIncomplete && markers.Any(marker => marker.Focused && marker.TargetKind == TeamFocusTargetKind.Tile && marker.BoardTileId == tile.TileId),
            tileIsIncomplete && markers.Any(marker => marker.Focused && marker.TargetKind == TeamFocusTargetKind.Row && marker.RowIndex == tile.Row),
            tileIsIncomplete && markers.Any(marker => marker.Focused && marker.TargetKind == TeamFocusTargetKind.Column && marker.ColumnIndex == tile.Column));
    }

    public sealed record TeamFocusPresentation(bool Tile, bool Row, bool Column);
}
