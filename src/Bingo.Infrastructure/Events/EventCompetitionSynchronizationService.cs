using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bingo.Application.Events;
using Bingo.Application.Integrations.WiseOldMan;
using Bingo.Domain.Access;
using Bingo.Domain.Auditing;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Domain.Teams;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bingo.Infrastructure.Events;

public sealed class EventCompetitionSynchronizationService(
    ApplicationDbContext db,
    IWiseOldManCompetitionClient competitionClient,
    IWiseOldManStatus womStatus,
    TimeProvider time) : IEventCompetitionSynchronizationService
{
    private const string DevelopmentTest15Slug = "test-15-dkl-live";
    private static readonly TimeSpan NormalInterval = TimeSpan.FromHours(2);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    public async Task<EventCompetitionView?> GetAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (!await db.Events.AsNoTracking().AnyAsync(x => x.Id == eventId && x.HiddenAt == null, cancellationToken)) return null;
        var state = await db.EventCompetitionSynchronizations.AsNoTracking()
            .Where(x => x.EventId == eventId)
            .OrderByDescending(x => x.Generation)
            .FirstOrDefaultAsync(cancellationToken);
        if (state is null) return new EventCompetitionView(0, null, null, null, null, null, null, null, null, [], null, null, null, null, 0, womStatus.GetStatus());
        return ToView(state);
    }

    public Task<EventCompetitionConfigurationResult> ConfigureAsync(
        Guid eventId, long expectedEventVersion, long? competitionId, bool synchronizeSchedule,
        LifecycleActor actor, bool confirmScheduleChanges = false, CancellationToken cancellationToken = default) =>
        ConfigureAsync(eventId, expectedEventVersion, competitionId, synchronizeSchedule, actor,
            confirmScheduleChanges, confirmCompetitionClear: false, competitionClearReason: null,
            cancellationToken: cancellationToken);

    public async Task<EventCompetitionConfigurationResult> ConfigureAsync(
        Guid eventId, long expectedEventVersion, long? competitionId, bool synchronizeSchedule,
        LifecycleActor actor, bool confirmScheduleChanges, bool confirmCompetitionClear,
        string? competitionClearReason, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(actor, cancellationToken);
        WiseOldManCompetition? competition = null;
        if (competitionId is { } requestedId)
        {
            var result = await competitionClient.GetCompetitionAsync(requestedId, cancellationToken);
            if (!result.Succeeded) return new(false, result.Message ?? "The competition could not be validated.");
            competition = result.Competition!;
            if (competition.Id != requestedId) return new(false, "Wise Old Man returned a different competition ID.");
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var item = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, cancellationToken);
            if (item is null) return new(false, "The event was not found.");
            if (item.Version != expectedEventVersion) return new(false, "This event changed in another request. Reload before changing its competition.");
            if (item.State is EventState.AwaitingFinalReview or EventState.Finalized or EventState.Archived or EventState.Cancelled or EventState.Discarded)
                return new(false, "Competition integration is read-only after live play.");
            var clearReason = competitionClearReason?.Trim();
            if (item.State == EventState.Live && competitionId is null && (!confirmCompetitionClear || string.IsNullOrWhiteSpace(clearReason)))
                return new(false, "Clearing a live event's competition requires explicit confirmation and a reason.");
            if (item.State == EventState.Live && synchronizeSchedule)
                return new(false, "A live event cannot change its schedule through competition integration.");
            if (item.State == EventState.Live && competition is not null && !ScheduleMatches(item, competition))
                return new(false, "A live replacement competition must match the event window within five minutes.");
            if (item.State != EventState.Live && competition is not null)
            {
                if (synchronizeSchedule)
                {
                    try
                    {
                        var scheduleValues = new EventScheduleValues(item.SignupOpensAt, item.SignupClosesAt, item.DraftAt,
                            competition.StartsAt, competition.EndsAt, item.ParticipantCap, item.ScheduledSignupOpeningEnabled);
                        var draftState = await db.DraftSessions.AsNoTracking()
                            .Where(x => x.EventId == eventId)
                            .Select(x => (DraftState?)x.State)
                            .SingleOrDefaultAsync(cancellationToken);
                        var validationError = await EventSignupLifecycleService.ValidateScheduleChangeAsync(
                            db, item, scheduleValues, time.GetUtcNow(), draftState, confirmChanges: confirmScheduleChanges, reason: null, ct: cancellationToken, proposedCompetition: competition);
                        if (validationError is not null) return new(false, validationError);
                        if (draftState == DraftState.Finalized)
                            item.ConfigureFinalizedDraftEventWindow(competition.StartsAt, competition.EndsAt);
                        else
                            item.ConfigureSchedule(item.SignupOpensAt, item.SignupClosesAt, item.DraftAt, competition.StartsAt, competition.EndsAt, item.ParticipantCap);
                        db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), time.GetUtcNow(), actor.Id, actor.Username,
                            "event.schedule_updated", "event", eventId.ToString(), "Synchronized the event window to the validated Wise Old Man competition.", eventId));
                    }
                    catch (InvalidOperationException exception) { return new(false, exception.Message); }
                }
                else if (!ScheduleMatches(item, competition))
                    return new(false, "The competition window must match the event window within five minutes, or explicitly synchronize the pre-live schedule.");
            }

            var fingerprint = await AssignmentFingerprintAsync(eventId, cancellationToken);
            var state = await db.EventCompetitionSynchronizations.SingleOrDefaultAsync(x => x.EventId == eventId, cancellationToken);
            var previousCompetitionId = state?.CompetitionId;
            if (state is null)
            {
                state = new EventCompetitionSynchronization(Guid.NewGuid(), eventId, 1, competitionId,
                    competition?.Title, competition?.StartsAt, competition?.EndsAt, fingerprint, time.GetUtcNow());
                db.EventCompetitionSynchronizations.Add(state);
            }
            else
            {
                state.Reconfigure(competitionId, competition?.Title, competition?.StartsAt, competition?.EndsAt, fingerprint, time.GetUtcNow());
            }
            item.AdvanceVersion();
            db.AuditEntries.Add(new AuditEntry(Guid.NewGuid(), time.GetUtcNow(), actor.Id, actor.Username,
                competitionId is null ? "event.competition_cleared" : previousCompetitionId is null ? "event.competition_linked" : "event.competition_changed",
                "event", eventId.ToString(), competitionId is null
                    ? clearReason is null ? "Cleared Wise Old Man competition integration." : $"Cleared Wise Old Man competition integration. Reason: {clearReason}"
                    : $"Linked Wise Old Man competition {competitionId.Value} ({competition!.Title}).", eventId));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(false, "This event changed in another request. Reload before changing its competition.");
        }
        catch (PostgresException exception) when (exception.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
        {
            return new(false, "This event changed in another request. Reload before changing its competition.");
        }
        catch (DbUpdateException)
        {
            return new(false, "The competition integration could not be saved safely. Reload and try again.");
        }
    }

    public async Task<EventCompetitionRefreshResult> RefreshAsync(Guid eventId, LifecycleActor actor, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(actor, cancellationToken);
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, cancellationToken);
        if (item is null) return new(false, true, "The event was not found.");
        if (item.State != EventState.Live) return new(false, true, "Competition refresh is available only while the event is Live.");
        return await SynchronizeOneAsync(eventId, manual: true, cancellationToken);
    }

    public async Task<bool> MakeDevelopmentRefreshDueAsync(Guid eventId, LifecycleActor actor, CancellationToken cancellationToken = default)
    {
        await RequireAdminAsync(actor, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, cancellationToken);
        if (item is null || !item.IsDevelopmentFixture || item.Slug != DevelopmentTest15Slug || item.State != EventState.Live)
            return false;

        var state = await db.EventCompetitionSynchronizations
            .FromSqlInterpolated($"SELECT * FROM event_competition_synchronizations WHERE event_id = {eventId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (state?.CompetitionId is null) return false;

        state.PrepareDevelopmentRefreshDue(time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var eventIds = await db.EventCompetitionSynchronizations.AsNoTracking()
            .Where(x => x.CompetitionId != null && (x.NormalDueAt <= now || x.RetryDueAt <= now))
            .Join(db.Events.AsNoTracking().Where(x => x.HiddenAt == null && x.State == EventState.Live), x => x.EventId, x => x.Id, (x, _) => x.EventId)
            .Distinct().ToListAsync(cancellationToken);
        foreach (var eventId in eventIds)
        {
            try { await SynchronizeOneAsync(eventId, manual: false, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        }
    }

    private async Task<EventCompetitionRefreshResult> SynchronizeOneAsync(Guid eventId, bool manual, CancellationToken cancellationToken)
    {
        var lease = await AcquireLeaseAsync(eventId, manual, cancellationToken);
        if (lease is null) return new(false, true, manual ? "The cached competition result is still within its refresh window." : null);

        WiseOldManCompetitionResult result;
        try { result = await competitionClient.GetCompetitionAsync(lease.CompetitionId, cancellationToken); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = new(WiseOldManCompetitionStatus.Unavailable, Message: "Wise Old Man timed out.");
        }
        await FinalizeLeaseAsync(lease, result, cancellationToken);
        return new(result.Succeeded, false, result.Message, result.Succeeded ? null : result.Status.ToString(), result.RetryAt);
    }

    private async Task<SyncLease?> AcquireLeaseAsync(Guid eventId, bool manual, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var item = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, cancellationToken);
        if (item is null || item.State != EventState.Live) return null;
        var state = await db.EventCompetitionSynchronizations
            .FromSqlInterpolated($"SELECT * FROM event_competition_synchronizations WHERE event_id = {eventId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (state?.CompetitionId is not { } competitionId) return null;
        if (state.LeaseExpiresAt is { } leaseExpiry && leaseExpiry > now) return null;
        var fingerprint = await AssignmentFingerprintAsync(eventId, cancellationToken);
        if (!string.Equals(state.AssignmentFingerprint, fingerprint, StringComparison.Ordinal))
            state.BeginReplacementGeneration(fingerprint, now);
        if (manual)
        {
            if (state.LastSuccessfulAt is { } successfulAt && successfulAt.Add(NormalInterval) > now) return null;
            if (state.RetryDueAt is { } retryDue && retryDue > now) return null;
            if (state.NormalDueAt is { } normalDue && normalDue > now) return null;
        }
        else
        {
            if (state.RetryDueAt is { } retryDue && retryDue > now) return null;
            if (state.RetryDueAt is null && (state.NormalDueAt is null || state.NormalDueAt > now)) return null;
        }

        if (state.RetryDueAt is null && state.NormalDueAt is { } normalDueAt && normalDueAt <= now)
            state.BeginNormalCycle(now);

        var expected = await CurrentPlayingAssignmentsAsync(eventId, cancellationToken);
        var owner = Guid.NewGuid().ToString("N");
        state.AcquireLease(owner, now.Add(LeaseDuration));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SyncLease(eventId, state.Id, state.Generation, competitionId, owner, fingerprint, expected);
    }

    private async Task FinalizeLeaseAsync(SyncLease lease, WiseOldManCompetitionResult result, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var item = await db.Events.SingleOrDefaultAsync(x => x.Id == lease.EventId && x.HiddenAt == null, cancellationToken);
        var state = await db.EventCompetitionSynchronizations.SingleOrDefaultAsync(x => x.Id == lease.StateId, cancellationToken);
        if (item is null || state is null || item.State != EventState.Live || state.Generation != lease.Generation || state.CompetitionId != lease.CompetitionId || state.LeaseOwner != lease.Owner || state.AssignmentFingerprint != lease.AssignmentFingerprint)
        {
            if (state?.LeaseOwner == lease.Owner) state.ReleaseLease(lease.Owner);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var currentFingerprint = await AssignmentFingerprintAsync(lease.EventId, cancellationToken);
        if (!string.Equals(currentFingerprint, lease.AssignmentFingerprint, StringComparison.Ordinal))
        {
            state.BeginReplacementGeneration(currentFingerprint, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (result.Succeeded && result.Competition!.Id == lease.CompetitionId)
        {
            var byName = result.Competition.Participants
                .GroupBy(x => Normalize(x.Username), StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Last(), StringComparer.Ordinal);
            var missing = lease.Expected.Where(expected => !byName.TryGetValue(expected.NormalizedName, out var participant) || participant.EhbDelta is null)
                .Select(expected => expected.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            var rows = lease.Expected.Where(expected => byName.TryGetValue(expected.NormalizedName, out var participant) && participant.EhbDelta is not null)
                .Select(expected => new EventCompetitionCharacterActivity(Guid.NewGuid(), lease.EventId, lease.Generation, lease.CompetitionId,
                    expected.CharacterId, byName[expected.NormalizedName].EhbDelta!.Value, now, result.Competition.LastUpdatedAt,
                    lease.AssignmentFingerprint, byName[expected.NormalizedName].StartEhb, byName[expected.NormalizedName].EndEhb)).ToList();
            await db.EventCompetitionCharacterActivities.Where(x => x.EventId == lease.EventId && x.Generation == lease.Generation).ExecuteDeleteAsync(cancellationToken);
            db.EventCompetitionCharacterActivities.AddRange(rows);
            state.MarkSuccess(now, result.Competition.LastUpdatedAt, missing.Count == 0, JsonSerializer.Serialize(missing), missing.Count == 0 ? null : "The competition response is missing one or more current Playing accounts.");
        }
        else if (result.Status is WiseOldManCompetitionStatus.NotFound or WiseOldManCompetitionStatus.Invalid)
        {
            state.MarkFailure(now, "Permanent", result.Message ?? "The Wise Old Man competition configuration is invalid.", null);
        }
        else
        {
            var retryNumber = state.RetryCount;
            var fallback = now.AddMinutes(retryNumber switch { 0 => 1, 1 => 2, _ => 4 });
            state.MarkFailure(now, result.Status.ToString(), result.Message ?? "Wise Old Man could not refresh the competition.", result.RetryAt ?? fallback);
        }
        state.ReleaseLease(lease.Owner);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RequireAdminAsync(LifecycleActor actor, CancellationToken cancellationToken)
    {
        var allowed = await db.Accounts.AsNoTracking().AnyAsync(x => x.Id == actor.Id && x.Active && x.AccountType == AccountType.WebsiteAccount && (x.GlobalRole == GlobalRole.Admin || x.GlobalRole == GlobalRole.SuperAdmin), cancellationToken);
        if (!allowed) throw new UnauthorizedAccessException("Administrator access is required.");
    }

    private async Task<List<ExpectedAssignment>> CurrentPlayingAssignmentsAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var rows = await db.EventParticipantCharacters.AsNoTracking()
            .Where(x => x.EventId == eventId && x.EventRole == EventCharacterRole.Playing && x.ReleasedAt == null &&
                        db.EventParticipants.Any(participant => participant.Id == x.EventParticipantId && participant.SignupStatus == SignupStatus.Confirmed))
            .Join(db.OsrsCharacters.AsNoTracking(), x => x.OsrsCharacterId, x => x.Id,
                (assignment, character) => new { assignment.OsrsCharacterId, character.DisplayName, character.NormalizedName })
            .OrderBy(x => x.NormalizedName).ToListAsync(cancellationToken);
        return rows.Select(x => new ExpectedAssignment(x.OsrsCharacterId, x.DisplayName, Normalize(x.NormalizedName))).ToList();
    }

    private async Task<string> AssignmentFingerprintAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var values = await db.EventParticipantCharacters.AsNoTracking()
            .Where(x => x.EventId == eventId && x.EventRole == EventCharacterRole.Playing && x.ReleasedAt == null &&
                        db.EventParticipants.Any(participant => participant.Id == x.EventParticipantId && participant.SignupStatus == SignupStatus.Confirmed))
            .OrderBy(x => x.Id).Select(x => $"{x.Id:N}:{x.EventParticipantId:N}:{x.OsrsCharacterId:N}").ToListAsync(cancellationToken);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', values)))).ToLowerInvariant();
    }

    private static bool ScheduleMatches(BingoEvent item, WiseOldManCompetition competition) =>
        item.EventStartsAt is { } start && item.EventEndsAt is { } end &&
        Math.Abs((start - competition.StartsAt).TotalMinutes) <= 5 && Math.Abs((end - competition.EndsAt).TotalMinutes) <= 5;

    private EventCompetitionView ToView(EventCompetitionSynchronization state) => new(state.Generation, state.CompetitionId, state.CompetitionTitle,
        state.CompetitionStartsAt, state.CompetitionEndsAt, state.LastAttemptAt, state.LastSuccessfulAt, state.LastUpstreamUpdatedAt,
        state.LatestComplete, ParseMissing(state.MissingAccountsJson), state.LastErrorKind, state.LastError, state.NormalDueAt, state.RetryDueAt, state.RetryCount, womStatus.GetStatus());

    private static List<string> ParseMissing(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; } catch (JsonException) { return []; }
    }

    private static string Normalize(string value) => value.Trim().Replace('_', ' ').ToUpperInvariant();
    private sealed record ExpectedAssignment(Guid CharacterId, string DisplayName, string NormalizedName);
    private sealed record SyncLease(Guid EventId, Guid StateId, int Generation, long CompetitionId, string Owner, string AssignmentFingerprint, IReadOnlyList<ExpectedAssignment> Expected);
}
