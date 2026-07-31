using System.Data;
using Bingo.Application.Teams;
using Bingo.Domain.Access;
using Bingo.Domain.Boards;
using Bingo.Domain.Events;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Infrastructure.Teams;

public sealed class TeamFocusService(
    ApplicationDbContext db,
    TimeProvider time,
    ITeamFocusNotifier? notifier = null) : ITeamFocusService
{
    public async Task<TeamFocusContext?> GetContextAsync(
        Guid eventId,
        Guid teamId,
        Guid viewerAccountId,
        bool inspectEnabled,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessAsync(eventId, teamId, viewerAccountId, inspectEnabled, cancellationToken);
        if (access is null) return null;
        if (!access.IsVisible)
            return new TeamFocusContext(eventId, teamId, false, false, access.CanInspect, false, []);

        var markers = await db.TeamFocusMarkers.AsNoTracking()
            .Where(x => x.EventId == eventId && x.TeamId == teamId)
            .OrderBy(x => x.TargetKind).ThenBy(x => x.RowIndex).ThenBy(x => x.ColumnIndex).ThenBy(x => x.BoardTileId)
            .Select(x => new TeamFocusMarkerView(x.Id, x.TargetKind, x.BoardTileId, x.RowIndex, x.ColumnIndex, x.Focused, x.Version))
            .ToListAsync(cancellationToken);
        var completedTileIds = new HashSet<Guid>();
        foreach (var marker in markers.Where(marker => marker.TargetKind == TeamFocusTargetKind.Tile && marker.BoardTileId is { }))
        {
            if (await IsTileCompleteAsync(teamId, marker.BoardTileId!.Value, cancellationToken))
                completedTileIds.Add(marker.BoardTileId.Value);
        }
        var visibleMarkers = markers
            .Where(marker => marker.TargetKind != TeamFocusTargetKind.Tile || marker.BoardTileId is not { } tileId || !completedTileIds.Contains(tileId))
            .ToList();
        return new TeamFocusContext(eventId, teamId, true, access.IsInspection, access.CanInspect, access.CanMutate, visibleMarkers);
    }

    public async Task<TeamFocusMutationResult> SetFocusAsync(
        TeamFocusMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var now = time.GetUtcNow().ToUniversalTime();
            await LockTeamAsync(request.TeamId, cancellationToken);
            var access = await ResolveAccessAsync(request.EventId, request.TeamId, request.ActorAccountId, false, cancellationToken);
            if (access is null || !access.CanMutate)
                return new(false, "Only a current team captain or co-captain can change team focus.");
            var board = await db.Boards.AsNoTracking()
                .SingleOrDefaultAsync(x => x.EventId == request.EventId && x.State == BoardState.Published, cancellationToken);
            if (board is null)
                return new(false, "Team focus is unavailable until the board is published.");
            if (!await ValidTargetAsync(board, request, cancellationToken))
                return new(false, "That focus target is not part of this board.");
            if (request.TargetKind == TeamFocusTargetKind.Tile && request.Focused &&
                await IsTileCompleteAsync(request.TeamId, request.BoardTileId!.Value, cancellationToken))
                return new(false, "Completed tiles cannot be focused.");

            var marker = await db.TeamFocusMarkers.SingleOrDefaultAsync(
                x => x.EventId == request.EventId && x.TeamId == request.TeamId &&
                     x.TargetKind == request.TargetKind && x.BoardTileId == request.BoardTileId &&
                     x.RowIndex == request.RowIndex && x.ColumnIndex == request.ColumnIndex,
                cancellationToken);
            if (marker is null)
            {
                if (request.ExpectedVersion != 0)
                    return new(false, "This focus target changed. Reload the team board and try again.");
                if (!request.Focused)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return new(true, Version: 0);
                }
                marker = new TeamFocusMarker(Guid.NewGuid(), request.EventId, request.TeamId, request.TargetKind,
                    request.BoardTileId, request.RowIndex, request.ColumnIndex, true, now, request.ActorAccountId);
                db.TeamFocusMarkers.Add(marker);
            }
            else
            {
                if (marker.Version != request.ExpectedVersion)
                    return new(false, "This focus target changed. Reload the team board and try again.");
                marker.SetFocused(request.Focused, now, request.ActorAccountId);
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await NotifyAsync(request.EventId, request.TeamId, cancellationToken);
            return new(true, Version: marker.Version);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return new(false, "This focus target changed. Reload the team board and try again.");
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return new(false, "Another captain changed this focus target first. Reload and try again.");
        }
    }

    public async Task<TeamFocusMutationResult> ClearAllFocusAsync(
        TeamFocusClearAllRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var now = time.GetUtcNow().ToUniversalTime();
            await LockTeamAsync(request.TeamId, cancellationToken);
            var access = await ResolveAccessAsync(request.EventId, request.TeamId, request.ActorAccountId, false, cancellationToken);
            if (access is null || !access.CanMutate)
                return new(false, "Only a current team captain or co-captain can change team focus.");
            var board = await db.Boards.AsNoTracking()
                .SingleOrDefaultAsync(x => x.EventId == request.EventId && x.State == BoardState.Published, cancellationToken);
            if (board is null)
                return new(false, "Team focus is unavailable until the board is published.");

            var markers = await db.TeamFocusMarkers
                .Where(x => x.EventId == request.EventId && x.TeamId == request.TeamId)
                .OrderBy(x => x.Id)
                .ToListAsync(cancellationToken);
            var expected = request.Markers;
            var expectedIds = expected.Select(marker => marker.Id).ToList();
            var expectedById = expectedIds.Count == expectedIds.Distinct().Count()
                ? expected.ToDictionary(marker => marker.Id)
                : null;
            if (expectedById is null || markers.Count != expected.Count || markers.Any(marker =>
                    !expectedById.TryGetValue(marker.Id, out var expectedMarker) || expectedMarker.Version != marker.Version))
                return new(false, "This focus state changed. Reload the team board and try again.");

            var changed = markers.Where(marker => marker.Focused).ToList();
            foreach (var marker in changed) marker.SetFocused(false, now, request.ActorAccountId);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (changed.Count > 0) await NotifyAsync(request.EventId, request.TeamId, cancellationToken);
            return new(true, Version: markers.Count == 0 ? 0 : markers.Max(marker => marker.Version));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return new(false, "This focus state changed. Reload the team board and try again.");
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return new(false, "Another captain changed team focus first. Reload and try again.");
        }
    }

    public async Task<bool> ClearCompletedTileFocusAsync(
        Guid eventId,
        Guid teamId,
        Guid boardTileId,
        CancellationToken cancellationToken = default)
    {
        if (!await IsTileCompleteAsync(teamId, boardTileId, cancellationToken)) return false;

        var markers = await db.TeamFocusMarkers
            .Where(x => x.EventId == eventId && x.TeamId == teamId &&
                        x.TargetKind == TeamFocusTargetKind.Tile && x.BoardTileId == boardTileId && x.Focused)
            .ToListAsync(cancellationToken);
        if (markers.Count == 0) return false;

        var now = time.GetUtcNow().ToUniversalTime();
        foreach (var marker in markers) marker.SetFocused(false, now, null);
        return true;
    }

    private async Task<FocusAccess?> ResolveAccessAsync(
        Guid eventId,
        Guid teamId,
        Guid viewerAccountId,
        bool inspectEnabled,
        CancellationToken cancellationToken)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == viewerAccountId, cancellationToken);
        if (account is null || !account.Active || account.AccountType != AccountType.WebsiteAccount) return null;
        var membership = await (from item in db.EventParticipants.AsNoTracking()
                                join member in db.TeamMemberships.AsNoTracking() on item.Id equals member.EventParticipantId
                                where item.EventId == eventId && item.AccountId == viewerAccountId && member.TeamId == teamId && member.LeftAt == null
                                select new { member.Role }).SingleOrDefaultAsync(cancellationToken);
        var isMember = membership is not null;
        var isCaptain = membership is { Role: TeamMembershipRole.Captain or TeamMembershipRole.CoCaptain };
        var isSuperAdmin = account.GlobalRole == GlobalRole.SuperAdmin;
        var canInspect = isSuperAdmin && !isMember;
        var isInspection = canInspect && inspectEnabled;
        var visible = isMember || isInspection;
        var eventItem = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId, cancellationToken);
        var canMutate = isCaptain && eventItem is not null &&
                        (eventItem.State is EventState.SignupClosed or EventState.Live) &&
                        eventItem.EventEndsAt is { } eventEnd && time.GetUtcNow() < eventEnd;
        return new FocusAccess(visible, isInspection, canInspect, canMutate);
    }

    private async Task<bool> ValidTargetAsync(Board board, TeamFocusMutationRequest request, CancellationToken cancellationToken)
    {
        return request.TargetKind switch
        {
            TeamFocusTargetKind.Tile => request.BoardTileId is { } tileId && request.RowIndex is null && request.ColumnIndex is null &&
                await db.BoardTiles.AsNoTracking().AnyAsync(x => x.Id == tileId && x.BoardId == board.Id, cancellationToken),
            TeamFocusTargetKind.Row => request.BoardTileId is null && request.RowIndex is >= 0 and var row && row < board.Rows && request.ColumnIndex is null,
            TeamFocusTargetKind.Column => request.BoardTileId is null && request.RowIndex is null && request.ColumnIndex is >= 0 and var column && column < board.Columns,
            _ => false
        };
    }

    private async Task<bool> IsTileCompleteAsync(Guid teamId, Guid boardTileId, CancellationToken cancellationToken)
    {
        var requirements = await db.BoardRequirementSnapshots.AsNoTracking()
            .Where(x => x.BoardTileId == boardTileId)
            .Select(x => new { x.Id, x.TargetContribution })
            .ToListAsync(cancellationToken);
        if (requirements.Count == 0) return false;

        var requirementIds = requirements.Select(x => x.Id).ToList();
        var approved = await db.SubmissionContributions.AsNoTracking()
            .Where(x => x.TeamId == teamId && requirementIds.Contains(x.RequirementId) && x.ReversedAt == null)
            .GroupBy(x => x.RequirementId)
            .Select(group => new { RequirementId = group.Key, Amount = group.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.RequirementId, x => x.Amount, cancellationToken);
        return requirements.All(x => approved.GetValueOrDefault(x.Id) >= x.TargetContribution);
    }

    private Task<int> LockTeamAsync(Guid teamId, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(7303006, hashtext({teamId}::text))", cancellationToken);

    private async Task NotifyAsync(Guid eventId, Guid teamId, CancellationToken cancellationToken)
    {
        if (notifier is null) return;
        try { await notifier.NotifyTeamFocusChangedAsync(eventId, teamId, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { }
    }

    private sealed record FocusAccess(bool IsVisible, bool IsInspection, bool CanInspect, bool CanMutate);
}
