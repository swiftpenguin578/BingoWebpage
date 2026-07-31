using Bingo.Domain.Teams;

namespace Bingo.Application.Teams;

public interface ITeamFocusService
{
    Task<TeamFocusContext?> GetContextAsync(
        Guid eventId,
        Guid teamId,
        Guid viewerAccountId,
        bool inspectEnabled,
        CancellationToken cancellationToken = default);

    Task<TeamFocusMutationResult> SetFocusAsync(
        TeamFocusMutationRequest request,
        CancellationToken cancellationToken = default);

    Task<TeamFocusMutationResult> ClearAllFocusAsync(
        TeamFocusClearAllRequest request,
        CancellationToken cancellationToken = default);

    // Called by an enclosing authoritative progress transaction. It does not
    // start or commit a transaction and returns whether a persisted tile marker
    // was automatically unfocused.
    Task<bool> ClearCompletedTileFocusAsync(
        Guid eventId,
        Guid teamId,
        Guid boardTileId,
        CancellationToken cancellationToken = default);
}

public interface ITeamFocusNotifier
{
    Task NotifyTeamFocusChangedAsync(Guid eventId, Guid teamId, CancellationToken cancellationToken = default);
}

public sealed record TeamFocusContext(
    Guid EventId,
    Guid TeamId,
    bool IsVisible,
    bool IsInspection,
    bool CanInspect,
    bool CanMutate,
    IReadOnlyList<TeamFocusMarkerView> Markers);

public sealed record TeamFocusMarkerView(
    Guid Id,
    TeamFocusTargetKind TargetKind,
    Guid? BoardTileId,
    int? RowIndex,
    int? ColumnIndex,
    bool Focused,
    long Version);

public sealed record TeamFocusMutationRequest(
    Guid EventId,
    Guid TeamId,
    TeamFocusTargetKind TargetKind,
    Guid? BoardTileId,
    int? RowIndex,
    int? ColumnIndex,
    bool Focused,
    long ExpectedVersion,
    Guid ActorAccountId);

public sealed record TeamFocusClearAllRequest(
    Guid EventId,
    Guid TeamId,
    IReadOnlyList<TeamFocusMarkerVersion> Markers,
    Guid ActorAccountId);

public sealed record TeamFocusMarkerVersion(Guid Id, long Version);

public sealed record TeamFocusMutationResult(bool Succeeded, string? Error = null, long? Version = null);
