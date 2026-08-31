namespace Bingo.Application.Teams;

public interface IAdminCollaborationNotifier
{
    Task NotifyDraftChangedAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task NotifyBoardChangedAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task NotifyEventsControlChangedAsync(CancellationToken cancellationToken = default);
}

public static class BoardEditingLease
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(5);
}

public static class DraftControlLease
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(5);
}
