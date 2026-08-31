using System.Collections.Concurrent;
using Bingo.Application.Access;
using Bingo.Application.Teams;
using Bingo.Infrastructure.Persistence;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Web.Hubs;

[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class AdminCollaborationHub(ApplicationDbContext db, TimeProvider time) : Hub
{
    private static readonly ConcurrentDictionary<string, BoardViewer> BoardViewers = new();

    public async Task WatchBoard(string eventId)
    {
        if (!Guid.TryParse(eventId, out var parsed)) throw new HubException("A valid event identifier is required.");
        if (!await db.Events.AsNoTracking().AnyAsync(value => value.Id == parsed && value.HiddenAt == null, Context.ConnectionAborted)) throw new HubException("This event is not available.");
        var accountId = Context.User?.GetAccountId() ?? throw new HubException("Administrator identity is required.");
        var viewer = new BoardViewer(parsed, accountId, Context.User?.Identity?.Name ?? "Administrator");
        BoardViewers[Context.ConnectionId] = viewer;
        await Groups.AddToGroupAsync(Context.ConnectionId, BoardGroup(parsed));
        await BroadcastBoardPresence(parsed, Context.ConnectionAborted);
    }

    public async Task WatchDraft(string eventId)
    {
        if (!Guid.TryParse(eventId, out var parsed)) throw new HubException("A valid event identifier is required.");
        if (!await db.Events.AsNoTracking().AnyAsync(value => value.Id == parsed && value.HiddenAt == null, Context.ConnectionAborted)) throw new HubException("This event is not available.");
        await Groups.AddToGroupAsync(Context.ConnectionId, DraftGroup(parsed));
    }

    public async Task RenewDraftControl(string eventId)
    {
        if (!Guid.TryParse(eventId, out var parsed)) return;
        var accountId = Context.User?.GetAccountId();
        if (accountId is null) return;
        if (!await db.Events.AsNoTracking().AnyAsync(value => value.Id == parsed && value.HiddenAt == null, Context.ConnectionAborted)) return;
        var draft = await db.DraftSessions.SingleOrDefaultAsync(value => value.EventId == parsed);
        if (draft is null || !draft.HasActiveController(time.GetUtcNow()) || draft.ControllerAccountId != accountId) return;
        try
        {
            draft.RenewControl(accountId.Value, time.GetUtcNow(), DraftControlLease.Duration);
            await db.SaveChangesAsync();
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateConcurrencyException)
        {
            // A page action or takeover won the race; the next page state is authoritative.
        }
    }

    public async Task RenewBoardEditing(string eventId)
    {
        if (!Guid.TryParse(eventId, out var parsed)) return;
        var accountId = Context.User?.GetAccountId();
        if (accountId is null) return;
        if (!await db.Events.AsNoTracking().AnyAsync(value => value.Id == parsed && value.HiddenAt == null, Context.ConnectionAborted)) return;
        var board = await db.Boards.SingleOrDefaultAsync(value => value.EventId == parsed);
        if (board is null || !board.HasActiveEditor(time.GetUtcNow()) || board.EditorAccountId != accountId) return;
        try
        {
            board.RenewEditing(accountId.Value, time.GetUtcNow(), BoardEditingLease.Duration);
            await db.SaveChangesAsync();
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbUpdateConcurrencyException)
        {
            // A page action or takeover won the race; the latest page state is authoritative.
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (BoardViewers.TryRemove(Context.ConnectionId, out var viewer)) await BroadcastBoardPresence(viewer.EventId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task WatchEventsControl()
    {
        if (Context.User?.IsInRole("SuperAdmin") != true) throw new HubException("Super Admin identity is required.");
        await Groups.AddToGroupAsync(Context.ConnectionId, EventsControlGroup(), Context.ConnectionAborted);
    }

    private async Task BroadcastBoardPresence(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (!await db.Events.AsNoTracking().AnyAsync(value => value.Id == eventId && value.HiddenAt == null, cancellationToken)) return;
        var viewers = BoardViewers.Values.Where(value => value.EventId == eventId)
            .GroupBy(value => value.AccountId)
            .Select(group => new { AccountId = group.Key, Username = group.First().Username })
            .OrderBy(value => value.Username)
            .ToList();
        await Clients.Group(BoardGroup(eventId)).SendAsync("boardPresenceChanged", viewers, cancellationToken: cancellationToken);
    }

    public static string BoardGroup(Guid eventId) => $"admin-board-{eventId:N}";
    public static string DraftGroup(Guid eventId) => $"admin-draft-{eventId:N}";
    public static string EventsControlGroup() => "admin-events-control";
    private sealed record BoardViewer(Guid EventId, Guid AccountId, string Username);
}
