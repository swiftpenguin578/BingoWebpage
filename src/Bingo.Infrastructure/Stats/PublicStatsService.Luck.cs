using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bingo.Application.Stats;
using Bingo.Domain.Events;
using Bingo.Domain.Integrations.WiseOldMan;
using Bingo.Infrastructure.Boards;
using Bingo.Infrastructure.Events;
using Bingo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Stats;

public sealed partial class PublicStatsService
{
    // Indented JSON bounds the compacted PostgreSQL jsonb representation too.
    private static readonly JsonSerializerOptions CheckpointJsonOptions = new() { WriteIndented = true };

    private async Task<StatsLuck> ReadLuckAsync(StatsData data, CancellationToken ct, bool retainTileActivity = false)
    {
        var cache = await EventCompetitionSynchronizationService.ReadMetricCacheAsync(db, time, data.Event.Id, ct);
        var sources = cache?.Sources ?? await db.LuckSourceRequestAsync(data.Event.Id, ct);
        var checkpoint = await db.EventStatsLuckCheckpoints.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == data.Event.Id, ct);
        if (checkpoint is not null && Compatible(checkpoint, data, cache))
        {
            var saved = JsonSerializer.Deserialize<StatsLuck>(checkpoint.Payload);
            if (saved is not null)
                return RescoreLuck(saved with
                {
                    Stale = checkpoint.EvidenceRevision != data.Event.StatsEvidenceRevision || !CanCalculate(cache) || checkpoint.ActivityBatchId != cache!.ActivityBatchId,
                    // Legacy snapshots retain the denominator but not tile attribution. Current
                    // tile evidence is coherent only at the exact retained evidence revision.
                    Tiles = retainTileActivity && saved.Tiles is null
                        ? CalculateTileLuck(data, saved.Teams, saved.Sources, checkpoint.EvidenceRevision == data.Event.StatsEvidenceRevision, retainKnownActivity: true)
                        : saved.Tiles
                }, data);
        }
        // Raw compatible observations can retain KC without a checkpoint, but cannot recover
        // a Luck score or bypass an existing checkpoint's evidence/lifecycle invalidation.
        var canCalculate = CanCalculate(cache) && !(retainTileActivity && checkpoint is null && cache?.Complete == false);
        return CalculateLuck(data, cache, sources, canCalculate ? time.GetUtcNow() : null,
            retainKnownTileActivity: retainTileActivity && checkpoint is null && cache?.Compatible == true);
    }

    private static bool CanCalculate(CompetitionMetricCache? cache) => cache is { SuccessfulBatch: true, Compatible: true, ActivityBatchId: not null } &&
        cache.LastAttemptAt is { } attempted && attempted.AddHours(1) > cache.ReadAt;
    private static string LifecycleFingerprint(BingoEvent ev) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { ev.State, ev.ActualStartedAt, ev.ActualEndedAt, ev.FinalizedAt, ev.ArchivedAt, ev.ResultsPublished })))).ToLowerInvariant();
    private static string AssignmentFingerprint(StatsData data, CompetitionMetricCache cache) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new
        {
            cache.AssignmentFingerprint,
            Teams = data.Board.Teams.OrderBy(x => x.TeamId).Select(x => new { x.TeamId, x.TeamName }),
            Roster = data.Roster.OrderBy(x => x.TeamId).ThenBy(x => x.PlayerId).Select(x => new { x.TeamId, x.PlayerId, x.PlayerName })
        })))).ToLowerInvariant();
    private static bool Compatible(EventStatsLuckCheckpoint saved, StatsData data, CompetitionMetricCache? cache) =>
        cache is { Compatible: true } && saved.SchemaVersion == EventStatsLuckCheckpoint.CurrentSchemaVersion &&
        // Only purely additive approvals may retain an older full calculation for stale display.
        // The write boundary below still requires the exact current evidence revision.
        saved.EvidenceRevision >= data.Event.StatsLuckInvalidatedAtRevision && saved.EvidenceRevision <= data.Event.StatsEvidenceRevision && saved.CompetitionId == cache.CompetitionId && saved.Generation == cache.Generation &&
        (saved.ActivityBatchId == cache.ActivityBatchId || !cache.Complete) && saved.AssignmentFingerprint == AssignmentFingerprint(data, cache) &&
        saved.SourceFingerprint == cache.Sources.Fingerprint && saved.LifecycleFingerprint == LifecycleFingerprint(data.Event);

    private static StatsLuck CalculateLuck(StatsData data, CompetitionMetricCache? cache, LuckSourceRequest sources, DateTimeOffset? calculatedAt,
        bool retainKnownTileActivity = false)
    {
        var usableBatch = calculatedAt is not null;
        var retainActivity = retainKnownTileActivity && !usableBatch;
        var sourceDtos = sources.Outcomes.Select(x => new StatsLuckSource(x.SourceDropId, x.ItemIdSnapshot, x.Basis?.BossActivityId,
            x.Basis?.Metric, x.Basis?.NumericProbability, x.Basis?.RollsPerCompletion,
            x.Basis?.ConditionalOnParent == true ? x.Basis.ParentProbability : null, x.UnavailableReason, data.Items[x.ItemIdSnapshot],
            data.Publication.Drops.First(d => d.SourceDropId == x.SourceDropId && d.ItemIdSnapshot == x.ItemIdSnapshot).BossName,
            x.Basis?.ProbabilityScope, x.Basis?.AssumedParticipants, x.Basis?.RollGroup, x.Basis?.RateCondition)).ToArray();
        var teams = data.Board.Teams.Select(team =>
        {
            var roster = data.Roster.Where(x => x.TeamId == team.TeamId).ToArray();
            var drops = data.Drops.Where(x => x.TeamId == team.TeamId).ToArray();
            var ids = roster.Select(x => x.PlayerId).Concat(drops.Select(x => x.PlayerId)).Distinct().Order().ToArray();
            var players = ids.Select(id =>
            {
                var personal = drops.Where(x => x.PlayerId == id).ToArray();
                var characters = data.Assignments.Where(x => x.PlayerId == id).Select(x => x.CharacterId)
                    .Concat(personal.Select(x => x.CharacterId)).Distinct().Order().ToArray();
                var details = characters.SelectMany(characterId => sources.Outcomes.Select(outcome =>
                {
                    var basis = outcome.Basis;
                    var received = personal.Count(x => x.CharacterId == characterId && x.SourceDropId == outcome.SourceDropId && x.Item.ItemId == outcome.ItemIdSnapshot);
                    // Presence is source-wide, not item-wide: -1/-1 is not a usable zero if any approved source drop exists.
                    var sourceDropPresent = personal.Any(x => x.CharacterId == characterId && sources.Outcomes.Any(o => o.SourceDropId == x.SourceDropId &&
                        o.Basis?.BossActivityId is not null && o.Basis.BossActivityId == basis?.BossActivityId));
                    var assigned = roster.Any(x => x.PlayerId == id) && data.Assignments.Any(x => x.PlayerId == id && x.CharacterId == characterId);
                    var row = assigned && cache?.Compatible == true && outcome.UnavailableReason is null
                        ? cache.Rows.SingleOrDefault(x => x.OsrsCharacterId == characterId && x.Metric == basis?.Metric &&
                            (x.ActivityBatchId == cache.ActivityBatchId || retainActivity)) : null;
                    var available = row?.Availability(sourceDropPresent);
                    var status = available switch
                    {
                        MetricActivityAvailability.Available or MetricActivityAvailability.Estimated => StatsLuckStatus.Calculated,
                        MetricActivityAvailability.NoRecordedActivity => StatsLuckStatus.NoEligibleActivity,
                        MetricActivityAvailability.WaitingForActivityUpdate => StatsLuckStatus.WaitingForActivityUpdate,
                        _ => StatsLuckStatus.WaitingForActivityData
                    };
                    var locallyUsable = status is StatsLuckStatus.Calculated or StatsLuckStatus.NoEligibleActivity;
                    var probability = basis?.NumericProbability * (basis?.ConditionalOnParent == true ? basis.ParentProbability : 1m);
                    var expectation = locallyUsable && outcome.UnavailableReason is null && probability is > 0 && usableBatch
                        ? row?.RecordedActivity() * probability * basis?.RollsPerCompletion : null;
                    if (!usableBatch && locallyUsable) status = StatsLuckStatus.WaitingForActivityData;
                    // Failed refreshes retain the last observation. Keep its KC, while preserving
                    // the existing zero/unranked-with-evidence uncertainty and withholding Luck.
                    var knownActivity = locallyUsable || retainActivity &&
                        (row?.RecordedActivity() > 0 || row?.RecordedActivity() == 0 && !sourceDropPresent);
                    return new StatsLuckCharacterSource(characterId, outcome.SourceDropId, outcome.ItemIdSnapshot, received,
                        knownActivity ? row?.RecordedActivity() : null, expectation, status,
                        row?.Coverage == MetricActivityCoverage.EstimatedBaseline, row?.Coverage == MetricActivityCoverage.ZeroRecorded,
                        row?.FetchedAt, row?.UpstreamUpdatedAt);
                })).ToArray();
                var result = Pool(details.Select(x => Result(x.Received, x.Expected, x.Status, x.Estimated, x.ZeroRecordedApproximation)).ToArray());
                if (characters.Length == 0 && sources.Outcomes.Count > 0) result = Result(0, null, StatsLuckStatus.WaitingForActivityData);
                return new StatsLuckPlayer(id, team.TeamId, roster.FirstOrDefault(x => x.PlayerId == id)?.PlayerName ?? personal.First().CharacterName, result, details);
            }).ToArray();
            return new StatsLuckTeam(team.TeamId, team.TeamName, Pool(players.Select(x => x.Result).ToArray()), players);
        }).ToArray();
        var rows = cache?.Rows ?? [];
        var retainedFetchedAt = retainActivity ? teams.SelectMany(x => x.Players).SelectMany(x => x.Sources)
            .Where(x => x.Activity is not null).Min(x => x.FetchedAt) : null;
        return RescoreLuck(new(Pool(teams.Select(x => x.Result).ToArray()), teams, sourceDtos, retainedFetchedAt is not null, calculatedAt,
            calculatedAt is null || rows.Count == 0 ? retainedFetchedAt : rows.Min(x => x.FetchedAt),
            calculatedAt is null || rows.Count == 0 || rows.Any(x => x.UpstreamUpdatedAt is null) ? null : rows.Min(x => x.UpstreamUpdatedAt),
            data.Event.StatsEvidenceRevision, cache?.ActivityBatchId, cache?.Generation,
            sources.SourcesAvailable ? usableBatch ? null : "ActivityBatchUnavailable" : "SourceBasisUnavailable",
            CalculateTileLuck(data, teams, sourceDtos, usableBatch, retainActivity)), data);
    }

    private static StatsLuckResult Result(int received, decimal? expected, StatsLuckStatus status, bool estimated = false, bool zeroRecorded = false) =>
        new(received, expected, null, status, estimated, zeroRecorded);
    private static StatsLuckResult Pool(StatsLuckResult[] results)
    {
        var received = results.Sum(x => x.Received); var complete = results.All(x => x.Expected is not null);
        decimal? expected = complete ? results.Sum(x => x.Expected!.Value) : null;
        var status = !complete
            ? results.Length > 0 && results.All(x => x.Status == StatsLuckStatus.WaitingForActivityUpdate) ? StatsLuckStatus.WaitingForActivityUpdate
                : results.Length > 0 && results.All(x => x.Status == StatsLuckStatus.WaitingForActivityData) ? StatsLuckStatus.WaitingForActivityData : StatsLuckStatus.Incomplete
            : expected > 0 ? StatsLuckStatus.Calculated : received > 0 ? StatsLuckStatus.WaitingForActivityUpdate : StatsLuckStatus.NoEligibleActivity;
        return Result(received, expected, status, results.Any(x => x.Estimated), results.Any(x => x.ZeroRecordedApproximation));
    }

    // Checkpoints predate the bounded score. Rebuild distributions from THEIR observations,
    // never from a fresh numerator or provider batch, and leave persisted JSON/timestamps intact.
    private static StatsLuck RescoreLuck(StatsLuck luck, StatsData data)
    {
        var teams = luck.Teams.Select(team => team with
        {
            Players = team.Players.Select(player => player with
            {
                Result = Score(player.Result, player.Sources, luck.Sources)
            }).ToArray(),
            Result = Score(team.Result, team.Players.SelectMany(x => x.Sources), luck.Sources)
        }).ToArray();
        var tiles = luck.Tiles?.Select(tile =>
        {
            var requirements = data.Publication.Requirements.Where(x => x.BoardTileId == tile.TileId && !x.ManualObjective).Select(x => x.Id).ToHashSet();
            var outcomes = data.Publication.Drops.Where(x => requirements.Contains(x.RequirementId))
                .Select(x => (x.SourceDropId, x.ItemIdSnapshot)).ToHashSet();
            var sources = luck.Sources.Where(x => outcomes.Contains((x.SourceDropId, x.ItemId))).ToArray();
            return tile with
            {
                Teams = tile.Teams.Select(team =>
                {
                    var retainedTeam = luck.Teams.Single(x => x.TeamId == team.TeamId);
                    var details = retainedTeam.Players.Where(player => team.Players.Any(x => x.PlayerId == player.PlayerId))
                        .SelectMany(player => player.Sources).Where(x => outcomes.Contains((x.SourceDropId, x.ItemId))).ToArray();
                    return team with
                    {
                        Result = Score(team.Result, details, sources),
                        Players = team.Players.Select(player => player with
                        {
                            Result = Score(player.Result, retainedTeam.Players.Single(x => x.PlayerId == player.PlayerId).Sources
                                .Where(x => outcomes.Contains((x.SourceDropId, x.ItemId))), sources)
                        }).ToArray()
                    };
                }).ToArray()
            };
        }).ToArray();
        return luck with
        {
            Result = Score(luck.Result, luck.Teams.SelectMany(x => x.Players).SelectMany(x => x.Sources), luck.Sources),
            Teams = teams,
            Tiles = tiles
        };
    }

    private static StatsLuckResult Score(StatsLuckResult result, IEnumerable<StatsLuckCharacterSource> observations,
        IReadOnlyList<StatsLuckSource> sources)
    {
        // Unavailable/zero-activity states keep their existing meanings; an old ratio is never exposed.
        result = result with { Percentage = null };
        if (result.Status != StatsLuckStatus.Calculated || result.Expected is not > 0) return result;
        var rows = observations.DistinctBy(x => (x.CharacterId, x.SourceDropId, x.ItemId)).ToArray();
        var components = new List<LuckBinomialComponent>();
        var mechanics = rows.Select(row => (Row: row, Source: sources.SingleOrDefault(source =>
            source.SourceDropId == row.SourceDropId && source.ItemId == row.ItemId))).ToArray();
        if (mechanics.Length == 0 || mechanics.Any(x => x.Source is null || x.Source.UnavailableReason is not null ||
            x.Source.BossId is null || x.Source.Metric is null || x.Source.RollGroup is null || x.Source.Rolls is not > 0 ||
            x.Source.Probability is not (> 0 and <= 1) || x.Source.ParentProbability is <= 0 or > 1 ||
            x.Row.Expected is null || x.Row.Activity is null or < 0 || decimal.Truncate(x.Row.Activity.Value) != x.Row.Activity))
            return result with { Status = StatsLuckStatus.Incomplete };
        foreach (var group in mechanics.GroupBy(x => (x.Row.CharacterId, x.Source!.BossId, x.Source.Metric, x.Source.RollGroup)))
        {
            var first = group.First();
            // One mutually exclusive roll has one count. Contradictory frozen mechanics cannot
            // be repaired by treating its outcomes as independent opportunities.
            if (group.Any(x => x.Source!.Rolls != first.Source!.Rolls || x.Row.Activity != first.Row.Activity))
                return result with { Status = StatsLuckStatus.Incomplete };
            var probability = group.Sum(x => x.Source!.Probability!.Value * (x.Source.ParentProbability ?? 1m));
            var trials = first.Row.Activity!.Value * first.Source!.Rolls!.Value;
            if (probability is < 0 or > 1 || trials > long.MaxValue)
                return result with { Status = StatsLuckStatus.Incomplete };
            components.Add(new((long)trials, probability));
        }
        // Expected remains the frozen displayed denominator, and must describe this distribution.
        if (components.Sum(x => x.Trials * x.Probability) != result.Expected)
            return result with { Status = StatsLuckStatus.Incomplete };
        var percentage = LuckScoreCalculator.Calculate(components, result.Received);
        return result with { Percentage = percentage, Status = percentage is null ? StatsLuckStatus.Incomplete : result.Status };
    }

    /// <summary>Existing mutation/sync owners invoke this after saving, before committing their event transaction.</summary>
    public static async Task RefreshCheckpointAsync(ApplicationDbContext context, TimeProvider clock, Guid eventId, CancellationToken ct = default)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("Checkpoint capture requires the event write transaction.");
        RequireConsistentTransaction(context);
        var ev = await context.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId, ct);
        if (ev is null) return;
        var service = new PublicStatsService(context, clock);
        var data = await service.ReadDataAsync(ev.Slug, ct);
        if (data is null) return;
        var cache = await EventCompetitionSynchronizationService.ReadMetricCacheAsync(context, clock, eventId, ct);
        if (!CanCalculate(cache)) return;
        var existing = await context.EventStatsLuckCheckpoints.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId, ct);
        if (existing is not null && Compatible(existing, data, cache) && !cache!.Complete && existing.ActivityBatchId != cache.ActivityBatchId) return;
        var calculated = CalculateLuck(data, cache, cache!.Sources, clock.GetUtcNow());
        var payload = JsonSerializer.Serialize(calculated, CheckpointJsonOptions);
        // Oversized results remain available through the read query; never truncate players or sources.
        if (Encoding.UTF8.GetByteCount(payload) > EventStatsLuckCheckpoint.MaximumPayloadBytes) return;
        var checkpoint = new EventStatsLuckCheckpoint(eventId, ev.StatsEvidenceRevision, cache.CompetitionId, cache.Generation,
            cache.ActivityBatchId!.Value, AssignmentFingerprint(data, cache), cache.Sources.Fingerprint, LifecycleFingerprint(ev),
            calculated.CalculatedAt!.Value, calculated.FetchedAt, calculated.UpstreamUpdatedAt, payload);
        await TryWriteCheckpointAsync(context, clock, checkpoint, ct);
    }

    // Public infrastructure boundary permits a real stale-writer fixture. Caller owns transaction;
    // lock plus full-key comparison prevents a calculation made before another write from replacing it.
    public static async Task<bool> TryWriteCheckpointAsync(ApplicationDbContext context, TimeProvider clock, EventStatsLuckCheckpoint candidate, CancellationToken ct = default)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("Checkpoint writes require an event transaction.");
        RequireConsistentTransaction(context);
        var ev = await context.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {candidate.EventId} FOR UPDATE").AsNoTracking().SingleAsync(ct);
        var cache = await EventCompetitionSynchronizationService.ReadMetricCacheAsync(context, clock, ev.Id, ct);
        var data = await new PublicStatsService(context, clock).ReadDataAsync(ev.Slug, ct);
        if (data is null || !CanCalculate(cache) || candidate.SchemaVersion != EventStatsLuckCheckpoint.CurrentSchemaVersion ||
            candidate.EvidenceRevision != ev.StatsEvidenceRevision || candidate.CompetitionId != cache!.CompetitionId || candidate.Generation != cache.Generation ||
            candidate.ActivityBatchId != cache.ActivityBatchId || candidate.AssignmentFingerprint != AssignmentFingerprint(data, cache) ||
            candidate.SourceFingerprint != cache.Sources.Fingerprint || candidate.LifecycleFingerprint != LifecycleFingerprint(ev)) return false;
        var changed = await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO event_stats_luck_checkpoints (event_id, schema_version, evidence_revision, competition_id, generation, activity_batch_id,
                assignment_fingerprint, source_fingerprint, lifecycle_fingerprint, calculated_at, fetched_at, upstream_updated_at, payload)
            VALUES ({candidate.EventId}, {candidate.SchemaVersion}, {candidate.EvidenceRevision}, {candidate.CompetitionId}, {candidate.Generation}, {candidate.ActivityBatchId},
                {candidate.AssignmentFingerprint}, {candidate.SourceFingerprint}, {candidate.LifecycleFingerprint}, {candidate.CalculatedAt}, {candidate.FetchedAt}, {candidate.UpstreamUpdatedAt}, CAST({candidate.Payload} AS jsonb))
            ON CONFLICT (event_id) DO UPDATE SET schema_version = EXCLUDED.schema_version, evidence_revision = EXCLUDED.evidence_revision,
                competition_id = EXCLUDED.competition_id, generation = EXCLUDED.generation, activity_batch_id = EXCLUDED.activity_batch_id,
                assignment_fingerprint = EXCLUDED.assignment_fingerprint, source_fingerprint = EXCLUDED.source_fingerprint, lifecycle_fingerprint = EXCLUDED.lifecycle_fingerprint,
                calculated_at = EXCLUDED.calculated_at, fetched_at = EXCLUDED.fetched_at, upstream_updated_at = EXCLUDED.upstream_updated_at, payload = EXCLUDED.payload
            WHERE event_stats_luck_checkpoints.evidence_revision <= EXCLUDED.evidence_revision AND event_stats_luck_checkpoints.calculated_at <= EXCLUDED.calculated_at
            """, ct);
        return changed == 1;
    }
}
