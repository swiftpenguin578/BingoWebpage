using System.Data;
using Bingo.Application.Signups;
using Bingo.Domain.Access;
using Bingo.Domain.Events;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Infrastructure.Signups;

public sealed class ParticipantLiveService(ApplicationDbContext db, TimeProvider time) : IParticipantLiveService
{
    public async Task<ParticipantLiveContext?> GetContextAsync(
        Guid eventId,
        Guid participantId,
        Guid viewerAccountId,
        CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var row = await LoadParticipantAsync(eventId, participantId, cancellationToken);
        if (row is null || !await MayViewAsync(row, viewerAccountId, cancellationToken)) return null;
        return await BuildContextAsync(row, viewerAccountId, now, cancellationToken);
    }

    public async Task<IReadOnlyList<ParticipantLiveContext>> GetTeamContextsAsync(
        Guid eventId,
        Guid teamId,
        Guid viewerAccountId,
        CancellationToken cancellationToken = default)
    {
        var participantIds = await (from membership in db.TeamMemberships.AsNoTracking()
                                    join participant in db.EventParticipants.AsNoTracking() on membership.EventParticipantId equals participant.Id
                                    where membership.TeamId == teamId && membership.LeftAt == null && participant.EventId == eventId &&
                                          participant.AccountId == viewerAccountId
                                    select participant.Id).Distinct().ToListAsync(cancellationToken);
        var contexts = new List<ParticipantLiveContext>(participantIds.Count);
        foreach (var participantId in participantIds)
        {
            var context = await GetContextAsync(eventId, participantId, viewerAccountId, cancellationToken);
            if (context is not null) contexts.Add(context);
        }
        return contexts;
    }

    public async Task<PlayingAccountHeader?> GetPlayingAccountHeaderAsync(
        Guid viewerAccountId,
        string? preferredEventSlug,
        CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var rows = await (from participant in db.EventParticipants.AsNoTracking()
                          join item in db.Events.AsNoTracking() on participant.EventId equals item.Id
                          join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
                          join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
                          where participant.AccountId == viewerAccountId && item.HiddenAt == null && item.State == EventState.Live &&
                                membership.LeftAt == null && team.Active && participant.EventId == team.EventId
                          orderby item.EventStartsAt descending, item.Id
                          select new { EventId = item.Id, item.Slug, item.EventEndsAt, participant.SignupStatus, ParticipantId = participant.Id })
            .ToListAsync(cancellationToken);
        var chosen = rows.FirstOrDefault(x => preferredEventSlug is not null && x.Slug == preferredEventSlug) ?? rows.FirstOrDefault();
        if (chosen is null) return null;
        if (!await db.Accounts.AsNoTracking().AnyAsync(
                x => x.Id == viewerAccountId && x.Active && x.AccountType == AccountType.WebsiteAccount, cancellationToken))
            return null;

        var assignments = await PlayingAssignmentsAsync(chosen.ParticipantId, cancellationToken);
        // Same source evidence crediting uses (CreditedPlayingCharacterIdAsync), without repeating its lookups.
        var active = await db.ActiveCharacterAtAsync(chosen.EventId, chosen.ParticipantId, now, cancellationToken);
        var creditedId = active?.OsrsCharacterId ?? await UnswitchedCreditedIdAsync(chosen.EventId, chosen.ParticipantId, assignments, cancellationToken);
        var credited = assignments.SingleOrDefault(x => x.Assignment.OsrsCharacterId == creditedId);
        if (credited is null) return new PlayingAccountHeader(chosen.EventId, chosen.ParticipantId, null, null, []);
        var canSwap = assignments.Count > 1 && chosen.SignupStatus == SignupStatus.Confirmed &&
                      chosen.EventEndsAt is { } endsAt && now < endsAt &&
                      !await db.EventParticipantCharacterSwaps.AsNoTracking()
                          .AnyAsync(x => x.EventParticipantId == chosen.ParticipantId && x.EffectiveAtUtc > now, cancellationToken);
        return new PlayingAccountHeader(
            chosen.EventId, chosen.ParticipantId, credited.Character.DisplayName, credited.Assignment.OsrsCharacterId,
            canSwap
                ? assignments.Where(x => x.Assignment.OsrsCharacterId != creditedId)
                    .Select(x => new ParticipantPlayingCharacter(x.Assignment.OsrsCharacterId, x.Character.DisplayName, false)).ToList()
                : []);
    }

    private async Task<Guid?> UnswitchedCreditedIdAsync(Guid eventId, Guid participantId, List<AssignmentRow> assignments, CancellationToken cancellationToken)
    {
        var hasSwapRows = await db.EventParticipantCharacterSwaps.AsNoTracking()
            .AnyAsync(x => x.EventId == eventId && x.EventParticipantId == participantId, cancellationToken);
        return EventParticipantActiveCharacterQueries.UnswitchedCreditedCharacterId(hasSwapRows, assignments.Select(x => x.Assignment.OsrsCharacterId).ToList());
    }

    public async Task<ParticipantCharacterSwapResult> SwapAsync(
        ParticipantCharacterSwapRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var attributionLock = await ParticipantAttributionLock.AcquireAsync(db, request.ParticipantId, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            // Share event ordering with submission creation, lifecycle and membership changes.
            await db.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {request.EventId} FOR UPDATE")
                .AsNoTracking().ToListAsync(cancellationToken);
            var now = ParticipantAttributionLock.AtDatabasePrecision(time.GetUtcNow());
            var row = await LoadParticipantAsync(request.EventId, request.ParticipantId, cancellationToken);
            if (row is null || !await MayViewAsync(row, request.ActorAccountId, cancellationToken))
                return new(false, "You are not allowed to change this participant's active account.");
            if (row.Event.State != EventState.Live || row.Event.EventEndsAt is not { } eventEndsAt || now >= eventEndsAt)
                return new(false, "Account swaps are available only while the event is live.");
            if (row.Participant.SignupStatus != SignupStatus.Confirmed)
                return new(false, "Only confirmed participants can swap accounts.");

            var assignments = await PlayingAssignmentsAsync(row.Participant.Id, cancellationToken);
            var target = assignments.SingleOrDefault(x => x.Assignment.OsrsCharacterId == request.NextCharacterId);
            if (target is null)
                return new(false, "Choose one of this participant's current Playing accounts.");

            if (await db.EventParticipantCharacterSwaps.AnyAsync(
                    x => x.EventParticipantId == row.Participant.Id && x.EffectiveAtUtc > now, cancellationToken))
                return new(false, "A previously scheduled account switch is still pending. Wait until it takes effect before switching again.");

            var current = await db.ActiveCharacterAtAsync(row.Event.Id, row.Participant.Id, now, cancellationToken);
            if (current is null)
                return new(false, "This participant has no active Playing account yet.");
            if (current.OsrsCharacterId != request.ExpectedCurrentCharacterId)
                return new(false, "The active account changed. Reload the participant context and try again.");
            if (current.OsrsCharacterId == request.NextCharacterId)
                return new(false, "Choose a different Playing account.");

            var effectiveAt = now;
            var sequence = 1 + (await db.EventParticipantCharacterSwaps
                .Where(x => x.EventParticipantId == row.Participant.Id)
                .MaxAsync(x => (long?)x.Sequence, cancellationToken) ?? 0);
            db.EventParticipantCharacterSwaps.Add(new EventParticipantCharacterSwap(
                Guid.NewGuid(), row.Event.Id, row.Participant.Id, current.OsrsCharacterId,
                request.NextCharacterId, effectiveAt, now, request.ActorAccountId, null, sequence));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, EffectiveAtUtc: effectiveAt, CharacterName: target.Character.DisplayName);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return new(false, "The participant changed while the swap was being saved. Reload and try again.");
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return new(false, "Another account change was saved first. Reload and try again.");
        }
    }

    private async Task<ParticipantRow?> LoadParticipantAsync(Guid eventId, Guid participantId, CancellationToken cancellationToken) =>
        await (from participant in db.EventParticipants.AsNoTracking()
               join item in db.Events.AsNoTracking() on participant.EventId equals item.Id
               join membership in db.TeamMemberships.AsNoTracking() on participant.Id equals membership.EventParticipantId
               join team in db.Teams.AsNoTracking() on membership.TeamId equals team.Id
               where item.Id == eventId && item.HiddenAt == null && participant.Id == participantId && membership.LeftAt == null && team.Active
               select new ParticipantRow(item, participant, membership, team)).SingleOrDefaultAsync(cancellationToken);

    private async Task<bool> MayViewAsync(ParticipantRow row, Guid viewerAccountId, CancellationToken cancellationToken)
    {
        var viewer = await db.Accounts.AsNoTracking().AnyAsync(
            x => x.Id == viewerAccountId && x.Active && x.AccountType == AccountType.WebsiteAccount,
            cancellationToken);
        if (!viewer) return false;
        return row.Participant.AccountId == viewerAccountId;
    }

    private async Task<ParticipantLiveContext> BuildContextAsync(
        ParticipantRow row,
        Guid viewerAccountId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var assignments = await PlayingAssignmentsAsync(row.Participant.Id, cancellationToken);
        // Before Live, the built-in PrimaryRegularAccount slot is the planned
        // account for every source, including AdminCreated participants. Fall
        // back to registration order only for retained legacy rows without that
        // mapping, matching the PrimaryCharacters authority query.
        var plannedPrimaryId = await db.PrimaryCharacters()
            .Where(x => x.EventId == row.Event.Id && x.ParticipantId == row.Participant.Id)
            .Select(x => (Guid?)x.OsrsCharacterId)
            .SingleOrDefaultAsync(cancellationToken);
        var planned = plannedPrimaryId is { } primaryId
            ? assignments.FirstOrDefault(x => x.Assignment.OsrsCharacterId == primaryId)
            : null;
        planned ??= assignments.OrderBy(x => x.Assignment.RegistrationOrder).FirstOrDefault();
        var active = await db.ActiveCharacterAtAsync(row.Event.Id, row.Participant.Id, now, cancellationToken);
        // Same source evidence crediting uses, so a Live participant with one account and no switch row is named too.
        var activeId = row.Event.State is EventState.Draft or EventState.SignupOpen or EventState.SignupClosed
            ? planned?.Assignment.OsrsCharacterId
            : row.Event.State == EventState.Live
                ? active?.OsrsCharacterId ?? await UnswitchedCreditedIdAsync(row.Event.Id, row.Participant.Id, assignments, cancellationToken)
                : active?.OsrsCharacterId;
        var activeAssignment = assignments.SingleOrDefault(x => x.Assignment.OsrsCharacterId == activeId);
        var hasPendingSwap = row.Event.State == EventState.Live && await db.EventParticipantCharacterSwaps.AsNoTracking()
            .AnyAsync(x => x.EventParticipantId == row.Participant.Id && x.EffectiveAtUtc > now, cancellationToken);
        var canSwap = row.Event.State == EventState.Live && row.Event.EventEndsAt is { } eventEndsAt && now < eventEndsAt &&
                      row.Participant.SignupStatus == SignupStatus.Confirmed &&
                      activeAssignment is not null && assignments.Count > 1 && !hasPendingSwap;
        return new ParticipantLiveContext(
            row.Event.Id, row.Event.Slug, row.Event.Name, row.Event.State, row.Event.EventEndsAt,
            row.Participant.Id, row.Team.Id, row.Team.Name, row.Team.Slug, row.Membership.Role,
            planned?.Character.DisplayName, activeAssignment?.Character.DisplayName, active?.EffectiveAtUtc,
            assignments.Select(x => new ParticipantPlayingCharacter(x.Assignment.OsrsCharacterId, x.Character.DisplayName, x.Assignment.OsrsCharacterId == activeId)).ToList(),
            canSwap && await MayViewAsync(row, viewerAccountId, cancellationToken));
    }

    private async Task<List<AssignmentRow>> PlayingAssignmentsAsync(Guid participantId, CancellationToken cancellationToken) =>
        await (from assignment in db.EventParticipantCharacters.AsNoTracking()
               join character in db.OsrsCharacters.AsNoTracking() on assignment.OsrsCharacterId equals character.Id
               where assignment.EventParticipantId == participantId && assignment.ReleasedAt == null && assignment.EventRole == EventCharacterRole.Playing
               orderby assignment.RegistrationOrder
               select new AssignmentRow(assignment, character)).ToListAsync(cancellationToken);

    private sealed record ParticipantRow(BingoEvent Event, EventParticipant Participant, TeamMembership Membership, Team Team);
    private sealed record AssignmentRow(EventParticipantCharacter Assignment, OsrsCharacter Character);
}
