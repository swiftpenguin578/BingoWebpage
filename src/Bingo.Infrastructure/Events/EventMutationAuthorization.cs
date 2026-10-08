using Bingo.Application.Events;
using Bingo.Domain.Access;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Events;

/// <summary>
/// Shared authorization boundary for human event lifecycle mutations.
/// Scheduled lifecycle work deliberately does not use this helper: it is
/// authorized by the system scheduler and records a system actor instead.
/// </summary>
internal static class EventMutationAuthorization
{
    internal const string UnauthorizedMessage = "Only an active website administrator can perform this event action.";

    // Provider preparation must happen outside the event mutation transaction.
    // This is an authoritative preflight only; callers that mutate state must
    // still call GetAuthorizedActorAsync after opening their transaction.
    internal static async Task<LifecycleActor?> GetActiveActorAsync(ApplicationDbContext db, LifecycleActor actor, CancellationToken ct)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(account =>
            account.Id == actor.Id &&
            account.Active &&
            account.AccountType == AccountType.WebsiteAccount &&
            (account.GlobalRole == GlobalRole.Admin || account.GlobalRole == GlobalRole.SuperAdmin), ct);
        return account is null ? null : new LifecycleActor(account.Id, account.LoginName);
    }

    internal static async Task<LifecycleActor?> GetAuthorizedActorAsync(ApplicationDbContext db, LifecycleActor actor, CancellationToken ct)
    {
        // Callers hold their serializable mutation transaction while this
        // row is locked.  That makes the authorization decision and the
        // subsequent event mutation one revocation-safe boundary.
        var account = await db.Accounts.FromSqlInterpolated($"SELECT * FROM accounts WHERE id = {actor.Id} FOR UPDATE")
            .SingleOrDefaultAsync(account =>
            account.Id == actor.Id &&
            account.Active &&
            account.AccountType == AccountType.WebsiteAccount &&
            (account.GlobalRole == GlobalRole.Admin || account.GlobalRole == GlobalRole.SuperAdmin), ct);
        return account is null ? null : new LifecycleActor(account.Id, account.LoginName);
    }

    internal static async Task<bool> IsAuthorizedAsync(ApplicationDbContext db, LifecycleActor actor, CancellationToken ct) =>
        await GetAuthorizedActorAsync(db, actor, ct) is not null;

    internal static async Task<LifecycleActor> EnsureAuthorizedAsync(ApplicationDbContext db, LifecycleActor actor, CancellationToken ct)
    {
        var authorizedActor = await GetAuthorizedActorAsync(db, actor, ct);
        if (authorizedActor is null)
            throw new InvalidOperationException(UnauthorizedMessage);
        return authorizedActor;
    }
}
