using System.Data;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Domain.Signups;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Bingo.Infrastructure.Events;

public sealed partial class EventCompetitionSynchronizationService
{
    /// <summary>Infrastructure cache boundary for the later Stats projection. No provider IO or Luck calculation.</summary>
    public async Task<CompetitionMetricCache?> ReadMetricCacheAsync(Guid eventId, CancellationToken ct = default)
        => await ReadMetricCacheAsync(db, time, eventId, ct);

    internal static async Task<CompetitionMetricCache?> ReadMetricCacheAsync(ApplicationDbContext db, TimeProvider time, Guid eventId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is { } current && current.GetDbTransaction().IsolationLevel is not (IsolationLevel.RepeatableRead or IsolationLevel.Serializable))
            throw new InvalidOperationException("Metric cache reads require a consistent read snapshot.");
        await using var ownedTransaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct) : null;
        var item = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId && x.HiddenAt == null, ct);
        if (item is null || item.State is EventState.Cancelled or EventState.Discarded) return null;
        var state = await db.EventCompetitionSynchronizations.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId, ct);
        if (state?.CompetitionId is null) return null;
        var sources = await db.LuckSourceRequestAsync(eventId, ct);
        var assignmentFingerprint = await StatsAssignmentFingerprintAsync(db, eventId, ct);
        var compatible = state.SourceRequestFingerprint == sources.Fingerprint && state.AssignmentFingerprint == assignmentFingerprint;
        var rows = compatible ? await db.EventCompetitionCharacterMetricActivities.AsNoTracking()
            .Where(x => x.EventId == eventId && x.Generation == state.Generation && x.CompetitionId == state.CompetitionId &&
                        x.AssignmentFingerprint == assignmentFingerprint && x.SourceRequestFingerprint == sources.Fingerprint && sources.Metrics.Contains(x.Metric))
            .ToListAsync(ct) : [];
        var expected = await db.EventParticipantCharacters.AsNoTracking()
            .Where(x => x.EventId == eventId && x.EventRole == EventCharacterRole.Playing && x.ReleasedAt == null &&
                db.EventParticipants.Any(p => p.Id == x.EventParticipantId && p.SignupStatus == SignupStatus.Confirmed))
            .Select(x => x.OsrsCharacterId).ToListAsync(ct);
        var complete = compatible && sources.SourcesAvailable && state.LatestMetricsComplete == true &&
            expected.All(account => sources.Metrics.All(metric => rows.Any(row => row.OsrsCharacterId == account && row.Metric == metric &&
                row.ActivityBatchId == state.MetricActivityBatchId && row.LastIssue is null && row.RecordedActivity() is not null)));
        var stale = !complete || rows.Any(x => x.FetchedAt is null || x.FetchedAt.Value.AddHours(2) <= time.GetUtcNow());
        return new(state.CompetitionId.Value, state.Generation, assignmentFingerprint, sources, state.MetricActivityBatchId,
            compatible, complete, stale, state.LastMetricAttemptAt, rows,
            state.LastMetricAttemptAt is not null && state.LastMetricAttemptAt == state.LastSuccessfulAt &&
                state.LastErrorKind is null or "Incomplete" && rows.All(x => x.LastIssue != MetricActivityCoverage.ProviderFailure), time.GetUtcNow());
    }
}

public sealed record CompetitionMetricCache(long CompetitionId, int Generation, string AssignmentFingerprint,
    LuckSourceRequest Sources, Guid? ActivityBatchId, bool Compatible, bool Complete, bool Stale, DateTimeOffset? LastAttemptAt,
    IReadOnlyList<EventCompetitionCharacterMetricActivity> Rows, bool SuccessfulBatch = false, DateTimeOffset ReadAt = default);
