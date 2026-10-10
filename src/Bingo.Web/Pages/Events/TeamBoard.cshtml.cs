using System.Globalization;
using Bingo.Application.Boards;
using Bingo.Application.Evidence;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Application.Signups;
using Bingo.Application.Teams;
using Bingo.Domain.Events;
using Bingo.Domain.Teams;
using Bingo.Web.HistoricalImport;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Bingo.Web.Pages.Events;

public sealed class TeamBoardModel(
    IPublicBoardService boards,
    IParticipantLiveService live,
    ITeamFocusService focus,
    IEventCompetitionActivityProjection? activity = null,
    IEvidenceAuthority? evidenceAuthority = null,
    TimeProvider? time = null) : PageModel
{
    public PublicEventBoard Board { get; private set; } = null!;
    public PublicTeamBoard Team { get; private set; } = null!;
    public PublicTeamBoard? Previous { get; private set; }
    public PublicTeamBoard? Next { get; private set; }
    public PublicTileDetails? SelectedTile { get; private set; }
    public ParticipantLiveContext? ParticipantContext { get; private set; }
    public bool CanOpenSubmissionWorkspace { get; private set; }
    public bool CanOpenCaptainWorkspace { get; private set; }
    public bool CanSubmit { get; private set; }
    public TeamFocusContext? Focus { get; private set; }
    public bool InspectFocus { get; private set; }
    public EventCompetitionActivityProjection Activity { get; private set; } = null!;
    public EventCompetitionTeamActivity? TeamActivity { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, string teamSlug, string? tileRoute, Guid? participantId, CancellationToken cancellationToken, bool inspectFocus = false)
    {
        if (!TryParseTileRoute(tileRoute, out var tileId)) return NotFound();
        var board = await boards.GetEventBoardAsync(slug, cancellationToken);
        if (board is null) return NotFound();
        Board = board;
        if (board.EventState == EventState.Cancelled) return Page();
        var index = board.Teams.ToList().FindIndex(value => value.TeamSlug == teamSlug);
        if (index < 0) return NotFound();
        Team = board.Teams[index];
        Activity = activity is null
            ? new(EventCompetitionActivityState.NotConfigured, 0, null, null, [])
            : await activity.GetAsync(board.EventId, cancellationToken);
        TeamActivity = Activity.Teams.SingleOrDefault(value => value.TeamId == Team.TeamId);
        Previous = index > 0 ? board.Teams[index - 1] : null;
        Next = index + 1 < board.Teams.Count ? board.Teams[index + 1] : null;
        if (tileId is { } selectedTileId)
        {
            SelectedTile = await boards.GetTileAsync(slug, teamSlug, selectedTileId, cancellationToken);
            if (SelectedTile is null) return NotFound();
        }
        if (!await LoadLiveContextAsync(participantId, cancellationToken)) return Forbid();
        if (User.GetAccountId() is { } actorAccountId && evidenceAuthority is not null)
        {
            try
            {
                var scope = await evidenceAuthority.ResolveActorAsync(actorAccountId, Board.EventId, Team.TeamId, (time ?? TimeProvider.System).GetUtcNow(), cancellationToken);
                CanOpenSubmissionWorkspace = scope.Kind is EvidenceActorKind.Participant or EvidenceActorKind.Captain
                    && scope.EventId == Board.EventId && scope.TeamId == Team.TeamId;
                CanOpenCaptainWorkspace = scope.Kind is EvidenceActorKind.Captain
                    && scope.EventId == Board.EventId && scope.TeamId == Team.TeamId;
                CanSubmit = CanOpenSubmissionWorkspace;
            }
            catch (InvalidOperationException)
            {
                CanOpenSubmissionWorkspace = false;
            }
        }
        InspectFocus = inspectFocus;
        var accountId = User.GetAccountId();
        if (accountId is { } viewerAccountId)
            Focus = await focus.GetContextAsync(Board.EventId, Team.TeamId, viewerAccountId, inspectFocus, cancellationToken);
        ViewData["BodyClass"] = $"public-event-shell public-ui-pass1 public-team-board-route {(Board.EventSlug == HistoricalEventImporter.EventSlug ? "public-ui-historical-board" : null)}";
        ViewData["CompactNavigation"] = true;
        ViewData["HideBreadcrumbs"] = true;
        return Page();
    }

    public async Task<IActionResult> OnGetSidebarAsync(string slug, string teamSlug, string? tileRoute, CancellationToken cancellationToken)
    {
        if (!TryParseTileRoute(tileRoute, out var tileId) || tileId is null) return NotFound();
        var board = await boards.GetEventBoardAsync(slug, cancellationToken);
        if (board?.EventState == EventState.Cancelled) return Partial("_EventCancelled", board.EventName);
        var team = board?.Teams.SingleOrDefault(value => value.TeamSlug == teamSlug);
        var tile = board is null ? null : await boards.GetTileAsync(slug, teamSlug, tileId.Value, cancellationToken);
        if (board is null || team is null || tile is null) return NotFound();

        var canSubmit = User.GetAccountId() is Guid accountId
            && await CanSubmitAsync(accountId, board.EventId, team.TeamId, cancellationToken);
        var sequenceNumber = team.Tiles.ToList().FindIndex(value => value.TileId == tile.TileId) + 1;
        return Partial("_TileSidebar", new TileSidebarView(tile, canSubmit, board.EventId, team.TeamId, sequenceNumber));
    }

    private async Task<bool> LoadLiveContextAsync(Guid? participantId, CancellationToken cancellationToken)
    {
        var accountId = User.GetAccountId();
        if (accountId is not { } viewerAccountId) return participantId is null;
        var contexts = await live.GetTeamContextsAsync(Board.EventId, Team.TeamId, viewerAccountId, cancellationToken);
        if (participantId is { } selected)
        {
            ParticipantContext = contexts.SingleOrDefault(context => context.ParticipantId == selected);
            return ParticipantContext is not null;
        }
        ParticipantContext = contexts.Count > 0 ? contexts[0] : null;
        return true;
    }

    private async Task<bool> CanSubmitAsync(Guid accountId, Guid eventId, Guid teamId, CancellationToken cancellationToken)
    {
        if (evidenceAuthority is null) return false;
        try
        {
            var scope = await evidenceAuthority.ResolveActorAsync(accountId, eventId, teamId, (time ?? TimeProvider.System).GetUtcNow(), cancellationToken);
            return scope.Kind is EvidenceActorKind.Participant or EvidenceActorKind.Captain
                && scope.EventId == eventId && scope.TeamId == teamId;
        }
        catch (InvalidOperationException) { return false; }
    }

    private static bool TryParseTileRoute(string? tileRoute, out Guid? tileId)
    {
        tileId = null;
        if (string.IsNullOrWhiteSpace(tileRoute)) return true;
        var segments = tileRoute.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 2 || !string.Equals(segments[0], "Tiles", StringComparison.OrdinalIgnoreCase) || !Guid.TryParse(segments[1], out var parsed)) return false;
        tileId = parsed;
        return true;
    }

    /// <summary>Tile EHB shown as whole "Points": nearest integer (midpoint away from zero), never below 1.</summary>
    public static int GetTilePoints(decimal estimatedEhb) =>
        Math.Max(1, (int)Math.Round(estimatedEhb, 0, MidpointRounding.AwayFromZero));

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
