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
using Microsoft.Extensions.Logging;

namespace Bingo.Infrastructure.Stats;

public sealed partial class PublicStatsService
{
    // Compact JSON; the bound is checked on the jsonb text form (see CheckpointPayloadBytes).
    private static readonly JsonSerializerOptions CheckpointJsonOptions = new() { WriteIndented = false };
    private const string TileObservationsProperty = "TileObservations";
    private static readonly Action<ILogger, Guid, int, int, Exception?> LogCheckpointNearLimit =
        LoggerMessage.Define<Guid, int, int>(LogLevel.Warning, new EventId(10201),
            "Luck checkpoint for event {EventId} is {PayloadBytes} bytes, above 75% of the {LimitBytes} byte bound");

    private async Task<StatsLuck> ReadLuckAsync(StatsData data, CancellationToken ct, bool retainTileActivity = false)
    {
        var cache = await EventCompetitionSynchronizationService.ReadMetricCacheAsync(db, time, data.Event.Id, ct);
        var checkpoint = await db.EventStatsLuckCheckpoints.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == data.Event.Id, ct);
        if (checkpoint is not null && checkpoint.SchemaVersion == EventStatsLuckCheckpoint.CurrentSchemaVersion &&
            (cache is null || Compatible(checkpoint, data, cache)))
        {
            var saved = DeserializeCheckpointPayload(checkpoint.Payload, retainTileActivity);
            if (saved is not null)
                return saved with
                {
                    // Age is presentation metadata only. The saved values, provenance and
                    // original timestamps are never rebuilt on a read.
                    Stale = saved.Stale || saved.FetchedAt is { } fetched && fetched.AddHours(1) <= time.GetUtcNow(),
                    Tiles = retainTileActivity ? saved.Tiles : null
                };
        }
        // Public reads never calculate from current evidence or the metric cache. A
        // successful fetch must publish a checkpoint before a number becomes visible.
        return UnavailableLuck(data, retainTileActivity);
    }

    private static bool CanCalculate(CompetitionMetricCache? cache) => cache is { SuccessfulBatch: true, Compatible: true, ActivityBatchId: not null } &&
        cache.LastAttemptAt is { } attempted && attempted.AddHours(1) > cache.ReadAt;
    private static string LifecycleFingerprint(BingoEvent ev) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        // ActualStartedAt identifies the competition window. Ending an event is an
        // ordinary lifecycle transition and must not invalidate a retained checkpoint.
        JsonSerializer.Serialize(new { ev.Id, ev.ActualStartedAt })))).ToLowerInvariant();
    private static string LegacyLifecycleFingerprint(BingoEvent ev) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        // Schema-v1 rows retained this exact lifecycle/provenance shape.
        JsonSerializer.Serialize(new { ev.State, ev.ActualStartedAt, ev.ActualEndedAt, ev.FinalizedAt, ev.ArchivedAt, ev.ResultsPublished })))).ToLowerInvariant();
    private static bool CompatibleLifecycle(EventStatsLuckCheckpoint saved, BingoEvent ev) =>
        saved.LifecycleFingerprint == LifecycleFingerprint(ev) ||
        saved.ConvertedFromSchemaVersion == 1 && saved.AlgorithmVersion == "luck-percentile-kc-v2-conversion" &&
        saved.LifecycleFingerprint == LegacyLifecycleFingerprint(ev);
    private static string AssignmentFingerprint(StatsData data, CompetitionMetricCache cache) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new
        {
            cache.AssignmentFingerprint,
            Teams = data.Board.Teams.OrderBy(x => x.TeamId).Select(x => new { x.TeamId, x.TeamName }),
            Roster = data.Roster.OrderBy(x => x.TeamId).ThenBy(x => x.PlayerId).Select(x => new { x.TeamId, x.PlayerId, x.PlayerName })
        })))).ToLowerInvariant();
    private static bool Compatible(EventStatsLuckCheckpoint saved, StatsData data, CompetitionMetricCache? cache) =>
        cache is { Compatible: true } && saved.SchemaVersion == EventStatsLuckCheckpoint.CurrentSchemaVersion && saved.CompetitionId == cache.CompetitionId && saved.Generation == cache.Generation &&
        saved.AssignmentFingerprint == AssignmentFingerprint(data, cache) && saved.SourceFingerprint == cache.Sources.Fingerprint &&
        CompatibleLifecycle(saved, data.Event);

    private static StatsLuck UnavailableLuck(StatsData data, bool includeTiles)
    {
        var unavailable = Result(0, null, StatsLuckStatus.WaitingForActivityData);
        var teams = data.Board.Teams.Select(team =>
        {
            var players = data.Roster.Where(x => x.TeamId == team.TeamId)
                .Select(player => new StatsLuckPlayer(player.PlayerId, team.TeamId, player.PlayerName, unavailable, []))
                .ToArray();
            return new StatsLuckTeam(team.TeamId, team.TeamName, unavailable, players);
        }).ToArray();
        var tiles = includeTiles
            ? data.Publication.Tiles.Select(tile =>
            {
                var requirements = data.Publication.Requirements.Where(x => x.BoardTileId == tile.Id && !x.ManualObjective).Select(x => x.Id).ToHashSet();
                var hasDrops = data.Publication.Drops.Any(x => requirements.Contains(x.RequirementId));
                var tileTeams = teams.Select(team => new StatsTileLuckTeam(team.TeamId, unavailable, [],
                    team.Players.Select(player => new StatsTileLuckPlayer(player.PlayerId, player.Name, unavailable, [])).ToArray())).ToArray();
                return new StatsTileLuck(tile.Id, hasDrops, tileTeams);
            }).ToArray()
            : null;
        return new StatsLuck(unavailable, teams, [], false, null, null, null, data.Event.StatsEvidenceRevision,
            null, null, "SavedLuckUnavailable", tiles);
    }

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
    // Calculated from the data we have (user decision, 16 October 2026): results without an expectation
    // (waiting for KC, or not calculable) are left out of the pool instead of blocking it. Only when
    // nothing is calculable does the pooled result keep the waiting or incomplete status.
    private static StatsLuckResult Pool(StatsLuckResult[] results)
    {
        var calculable = results.Where(x => x.Expected is not null).ToArray();
        if (calculable.Length == 0 && results.Length > 0)
        {
            var waiting = results.All(x => x.Status == StatsLuckStatus.WaitingForActivityUpdate) ? StatsLuckStatus.WaitingForActivityUpdate
                : results.All(x => x.Status == StatsLuckStatus.WaitingForActivityData) ? StatsLuckStatus.WaitingForActivityData : StatsLuckStatus.Incomplete;
            return Result(results.Sum(x => x.Received), null, waiting, results.Any(x => x.Estimated), results.Any(x => x.ZeroRecordedApproximation));
        }
        var received = calculable.Sum(x => x.Received);
        var expected = calculable.Sum(x => x.Expected!.Value);
        var status = expected > 0 ? StatsLuckStatus.Calculated : received > 0 ? StatsLuckStatus.WaitingForActivityUpdate : StatsLuckStatus.NoEligibleActivity;
        return Result(received, expected, status, calculable.Any(x => x.Estimated), calculable.Any(x => x.ZeroRecordedApproximation));
    }

    // Retained checkpoints are authoritative until an accepted fetch replaces them.
    // Reads must not rebuild a score from current evidence, provider data, or a fresh numerator.
    private static StatsLuck RescoreLuck(StatsLuck luck, StatsData? data, bool rebuildTiles = true)
    {
        var teams = luck.Teams.Select(team => team with
        {
            Players = team.Players.Select(player => player with
            {
                Result = Score(player.Result, player.Sources, luck.Sources)
            }).ToArray(),
            Result = Score(team.Result, team.Players.SelectMany(x => x.Sources), luck.Sources)
        }).ToArray();
        var tiles = rebuildTiles && data is not null
            ? luck.Tiles?.Select(tile =>
            {
                var requirements = data!.Publication.Requirements.Where(x => x.BoardTileId == tile.TileId && !x.ManualObjective).Select(x => x.Id).ToHashSet();
                var outcomes = data.Publication.Drops.Where(x => requirements.Contains(x.RequirementId))
                    .Select(x => (x.SourceDropId, x.ItemIdSnapshot)).ToHashSet();
                var sources = luck.Sources.Where(x => outcomes.Contains((x.SourceDropId, x.ItemId))).ToArray();
                return tile with
                {
                    Teams = tile.Teams.Select(team =>
                    {
                        var retainedTeam = luck.Teams.Single(x => x.TeamId == team.TeamId);
                        var details = team.Players.SelectMany(player => player.Sources ?? []).ToArray();
                        return team with
                        {
                            Result = Score(team.Result, details, sources),
                            Players = team.Players.Select(player => player with
                            {
                                Result = Score(player.Result, player.Sources ?? [], sources)
                            }).ToArray()
                        };
                    }).ToArray()
                };
            }).ToArray()
            : luck.Tiles;
        return luck with
        {
            Result = Score(luck.Result, luck.Teams.SelectMany(x => x.Players).SelectMany(x => x.Sources), luck.Sources),
            Teams = teams,
            Tiles = tiles
        };
    }

    // Internal for tests: the derived Luck values for a set of retained observation rows.
    internal static StatsLuckResult Score(StatsLuckResult result, IEnumerable<StatsLuckCharacterSource> observations,
        IReadOnlyList<StatsLuckSource> sources)
    {
        // Rebuild only derived values from the retained observation rows. The rows
        // themselves remain the snapshot's source of truth.
        result = result with { Percentage = null, KcDifference = null, Activities = null };
        var rows = observations.DistinctBy(x => (x.CharacterId, x.SourceDropId, x.ItemId)).ToArray();
        var mechanics = rows.Select(row => (Row: row, Source: sources.SingleOrDefault(source =>
            source.SourceDropId == row.SourceDropId && source.ItemId == row.ItemId))).ToArray();
        if (mechanics.Length == 0) return result;

        var characterActivities = mechanics.GroupBy(x => (x.Row.CharacterId, x.Source?.BossId, x.Source?.Metric))
            .Select(activityGroup =>
            {
                var activityRows = activityGroup.ToArray();
                var first = activityRows[0].Source;
                var activityValues = activityRows.Select(x => x.Row.Activity).Distinct().ToArray();
                var activity = activityValues.Length == 1 ? activityValues[0] : null;
                var valid = activityRows.All(x => x.Source is not null && x.Source.UnavailableReason is null &&
                    x.Source.BossId is not null && x.Source.Metric is not null && x.Source.RollGroup is not null && x.Source.Rolls is > 0 &&
                    x.Source.Probability is > 0 and <= 1 && x.Source.ParentProbability is null or (> 0 and <= 1) &&
                    x.Row.Expected is not null && x.Row.Activity is not null && x.Row.Activity >= 0 && decimal.Truncate(x.Row.Activity.Value) == x.Row.Activity);
                // A roll group whose drops have different roll counts is split by roll count; each
                // sub-group is its own mutually exclusive roll (user decision, 16 October 2026).
                var coherentMechanics = valid && activity is not null && activityRows.GroupBy(x => (x.Source!.RollGroup, x.Source.Rolls)).All(group =>
                {
                    var sample = group.First();
                    return group.All(x => x.Row.Activity == sample.Row.Activity);
                });
                var complete = coherentMechanics;
                var received = activityRows.Sum(x => x.Row.Received);
                decimal? expected = complete ? activityRows.Sum(x => x.Row.Expected!.Value) : null;
                decimal? lambda = complete ? activityRows.Sum(x => x.Source!.Probability!.Value * (x.Source.ParentProbability ?? 1m) * x.Source.Rolls!.Value) : null;
                var componentGroups = complete ? activityRows.GroupBy(x => (x.Source!.RollGroup, x.Source.Rolls)).ToArray() : [];
                var coherentComponents = complete && componentGroups.All(group =>
                {
                    var sample = group.First();
                    var probability = group.Sum(x => x.Source!.Probability!.Value * (x.Source.ParentProbability ?? 1m));
                    return TryCreateComponent(sample.Row.Activity!.Value, sample.Source!.Rolls!.Value, probability, out _);
                });
                var components = coherentComponents
                    ? componentGroups.Select(group =>
                    {
                        var sample = group.First();
                        var probability = group.Sum(x => x.Source!.Probability!.Value * (x.Source.ParentProbability ?? 1m));
                        TryCreateComponent(sample.Row.Activity!.Value, sample.Source!.Rolls!.Value, probability, out var component);
                        return component;
                    }).ToArray()
                    : Array.Empty<LuckBinomialComponent>();
                var status = !complete
                    ? activityRows.All(x => x.Row.Status == StatsLuckStatus.WaitingForActivityUpdate) ? StatsLuckStatus.WaitingForActivityUpdate
                        : activityRows.All(x => x.Row.Status == StatsLuckStatus.WaitingForActivityData) ? StatsLuckStatus.WaitingForActivityData
                        : StatsLuckStatus.Incomplete
                    : !coherentComponents ? StatsLuckStatus.Incomplete
                    : activity!.Value > 0 ? StatsLuckStatus.Calculated : StatsLuckStatus.NoEligibleActivity;
                // A bounded PMF work-limit leaves the valid KC/lambda result usable.
                var percentage = status == StatsLuckStatus.Calculated && expected is > 0 && components.Length > 0
                    ? LuckScoreCalculator.Calculate(components, received) : null;
                decimal? kcDifference = status is StatsLuckStatus.Calculated or StatsLuckStatus.NoEligibleActivity && lambda is > 0 && activity is >= 0
                    ? received / lambda.Value - activity.Value : null;
                return new
                {
                    activityGroup.Key.CharacterId,
                    BossId = first?.BossId,
                    Metric = first?.Metric,
                    Name = first?.BossName ?? activityGroup.Key.Metric ?? "Activity",
                    Received = received,
                    Expected = expected,
                    Kc = complete ? activity : null,
                    Percentage = percentage,
                    KcDifference = kcDifference,
                    Status = status,
                    Estimated = activityRows.Any(x => x.Row.Estimated),
                    ZeroRecordedApproximation = activityRows.Any(x => x.Row.ZeroRecordedApproximation),
                    Components = components,
                    Complete = complete && coherentComponents
                };
            }).ToArray();

        var activities = characterActivities.GroupBy(x => (x.BossId, x.Metric))
            .Select(activityGroup =>
            {
                var members = activityGroup.ToArray();
                // A boss line pools the characters with data for that boss; the others are left out.
                // With no calculable character the line keeps its waiting or incomplete status.
                var calculable = members.Where(x => x.Complete).ToArray();
                var complete = calculable.Length > 0;
                var pooled = complete ? calculable : members;
                var received = pooled.Sum(x => x.Received);
                decimal? expected = complete ? pooled.Sum(x => x.Expected!.Value) : null;
                decimal? kc = complete && pooled.All(x => x.Kc is not null) ? pooled.Sum(x => x.Kc!.Value) : null;
                var components = complete ? pooled.SelectMany(x => x.Components).ToArray() : Array.Empty<LuckBinomialComponent>();
                var status = !complete
                    ? members.All(x => x.Status == StatsLuckStatus.WaitingForActivityUpdate) ? StatsLuckStatus.WaitingForActivityUpdate
                        : members.All(x => x.Status == StatsLuckStatus.WaitingForActivityData) ? StatsLuckStatus.WaitingForActivityData
                        : StatsLuckStatus.Incomplete
                    : expected > 0 ? StatsLuckStatus.Calculated : StatsLuckStatus.NoEligibleActivity;
                var percentage = status == StatsLuckStatus.Calculated && expected is > 0 && components.Length > 0
                    ? LuckScoreCalculator.Calculate(components, received) : null;
                decimal? kcDifference = complete && pooled.All(x => x.KcDifference is not null)
                    ? pooled.Sum(x => x.KcDifference!.Value) : null;
                var first = members[0];
                return new StatsLuckActivityResult(first.BossId, first.Metric, first.Name, received, expected, kc,
                    percentage, kcDifference, status, pooled.Any(x => x.Estimated), pooled.Any(x => x.ZeroRecordedApproximation));
            }).OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.Metric, StringComparer.Ordinal).ToArray();

        // The result is calculated from the bosses each character has data for. A waiting or contradictory
        // boss (combined chance > 1, KC mismatch) keeps its own line and is left out of the pool.
        var allCalculable = characterActivities.All(x => x.Complete);
        if (!allCalculable)
        {
            var calculable = characterActivities.Where(x => x.Complete).ToArray();
            if (calculable.Length > 0)
            {
                var pooledReceived = calculable.Sum(x => x.Received);
                var pooledExpected = calculable.Sum(x => x.Expected!.Value);
                result = result with
                {
                    Received = pooledReceived,
                    Expected = pooledExpected,
                    Status = pooledExpected > 0 ? StatsLuckStatus.Calculated : pooledReceived > 0 ? StatsLuckStatus.WaitingForActivityUpdate : StatsLuckStatus.NoEligibleActivity,
                    Estimated = calculable.Any(x => x.Estimated),
                    ZeroRecordedApproximation = calculable.Any(x => x.ZeroRecordedApproximation)
                };
            }
            else if (result.Expected is not null)
                result = result with { Status = StatsLuckStatus.Incomplete };
        }
        var calculableKeys = characterActivities.Where(x => x.Complete).Select(x => (x.CharacterId, x.BossId, x.Metric)).ToHashSet();
        var validMechanics = mechanics.Where(x => x.Source is not null && x.Source.UnavailableReason is null &&
            x.Source.BossId is not null && x.Source.Metric is not null && x.Source.RollGroup is not null && x.Source.Rolls is > 0 &&
            x.Source.Probability is > 0 and <= 1 && x.Source.ParentProbability is null or (> 0 and <= 1) &&
            x.Row.Expected is not null && x.Row.Activity is not null && x.Row.Activity >= 0 && decimal.Truncate(x.Row.Activity.Value) == x.Row.Activity &&
            calculableKeys.Contains((x.Row.CharacterId, x.Source.BossId, x.Source.Metric))).ToArray();
        var components = new List<LuckBinomialComponent>();
        // Drops of one roll group with different roll counts form one sub-group per roll count;
        // a group with a single roll count is exactly one component, as before.
        foreach (var group in validMechanics.GroupBy(x => (x.Row.CharacterId, x.Source!.BossId, x.Source.Metric, x.Source.RollGroup, x.Source.Rolls)))
        {
            var first = group.First();
            // One sub-group (roll group and roll count) has one KC; a mismatch is refused.
            // Calculable bosses already passed this check, so it only guards the invariant.
            if (group.Any(x => x.Row.Activity != first.Row.Activity))
                return result with { Status = StatsLuckStatus.Incomplete, Activities = activities };
            var probability = group.Sum(x => x.Source!.Probability!.Value * (x.Source.ParentProbability ?? 1m));
            if (!TryCreateComponent(first.Row.Activity!.Value, first.Source!.Rolls!.Value, probability, out var component))
                return result with { Status = StatsLuckStatus.Incomplete, Activities = activities };
            components.Add(component);
        }
        var percentage = result.Status == StatsLuckStatus.Calculated && result.Expected is > 0 &&
            components.Sum(x => x.Trials * x.Probability) == result.Expected
            ? LuckScoreCalculator.Calculate(components, result.Received) : null;
        var status = result.Status;
        var knownKcDifferences = allCalculable ? activities : activities.Where(x => x.KcDifference is not null).ToArray();
        decimal? kcDifference = knownKcDifferences.Length > 0 && knownKcDifferences.All(x => x.KcDifference is not null)
            ? knownKcDifferences.Sum(x => x.KcDifference!.Value) : null;
        return result with { Percentage = percentage, Status = status, KcDifference = kcDifference, Activities = activities };
    }

    private static bool TryCreateComponent(decimal activity, int rolls, decimal probability, out LuckBinomialComponent component)
    {
        component = default;
        if (rolls <= 0 || activity < 0 || decimal.Truncate(activity) != activity || probability is < 0 or > 1)
            return false;
        try
        {
            var trials = activity * rolls;
            if (trials > long.MaxValue) return false;
            component = new((long)trials, probability);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    /// <summary>The accepted competition synchronization transaction invokes this after a successful provider response is accepted, before committing its event transaction.</summary>
    /// <param name="payloadByteLimit">Test seam; it can only lower <see cref="EventStatsLuckCheckpoint.MaximumPayloadBytes"/>.</param>
    public static async Task<string?> PublishCheckpointAfterAcceptedFetchAsync(ApplicationDbContext context, TimeProvider clock, Guid eventId,
        ILogger? logger = null, int? payloadByteLimit = null, CancellationToken ct = default)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("Checkpoint capture requires the event write transaction.");
        RequireConsistentTransaction(context);
        var ev = await context.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId, ct);
        if (ev is null) return "event-not-found";
        var service = new PublicStatsService(context, clock);
        var data = await service.ReadDataAsync(ev.Slug, ct);
        if (data is null) return "published-data-unavailable";
        var cache = await EventCompetitionSynchronizationService.ReadMetricCacheAsync(context, clock, eventId, ct);
        if (!CanCalculate(cache)) return "accepted-fetch-cache-unavailable";
        var existing = await context.EventStatsLuckCheckpoints.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId, ct);
        if (existing is not null && Compatible(existing, data, cache) && !cache!.Complete && existing.ActivityBatchId != cache.ActivityBatchId && IsCompleteSnapshot(existing.Payload))
            return "partial-batch-write-fenced";
        var calculated = CalculateLuck(data, cache, cache!.Sources, clock.GetUtcNow());
        var payload = SerializeCheckpointPayload(calculated);
        // Accepted fetch results are serialized as a whole; reject oversized payloads without truncating players or sources.
        var payloadBytes = CheckpointPayloadBytes(payload);
        var limit = Math.Min(payloadByteLimit ?? EventStatsLuckCheckpoint.MaximumPayloadBytes, EventStatsLuckCheckpoint.MaximumPayloadBytes);
        if (payloadBytes > limit)
            return $"checkpoint-discarded: payload {payloadBytes} bytes exceeds {limit} byte bound";
        if (logger is not null && payloadBytes > limit * 0.75)
            LogCheckpointNearLimit(logger, eventId, payloadBytes, limit, null);
        var checkpoint = new EventStatsLuckCheckpoint(eventId, ev.StatsEvidenceRevision, cache.CompetitionId, cache.Generation,
            cache.ActivityBatchId!.Value, AssignmentFingerprint(data, cache), cache.Sources.Fingerprint, LifecycleFingerprint(ev),
            calculated.CalculatedAt!.Value, calculated.FetchedAt, calculated.UpstreamUpdatedAt, payload);
        return await TryWriteCheckpointAsync(context, clock, checkpoint, ct) ? null : "checkpoint-write-fenced";
    }

    // Test seam: the full in-memory calculation an accepted fetch publishes, before serialization.
    internal static async Task<StatsLuck?> CalculateAcceptedFetchLuckAsync(ApplicationDbContext context, TimeProvider clock, Guid eventId, CancellationToken ct = default)
    {
        var ev = await context.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId, ct);
        var data = ev is null ? null : await new PublicStatsService(context, clock).ReadDataAsync(ev.Slug, ct);
        var cache = await EventCompetitionSynchronizationService.ReadMetricCacheAsync(context, clock, eventId, ct);
        return data is null || !CanCalculate(cache) ? null : CalculateLuck(data, cache, cache!.Sources, clock.GetUtcNow());
    }

    // The database bound measures octet_length(payload::text). PostgreSQL renders jsonb with a space
    // after every ':' and ',' and never escapes more than System.Text.Json, so counting one extra byte
    // for each of those characters (inside strings too) over-approximates it: an accepted payload fits.
    internal static int CheckpointPayloadBytes(string payload)
    {
        var separators = 0;
        foreach (var character in payload)
            if (character is ':' or ',') separators++;
        return Encoding.UTF8.GetByteCount(payload) + separators;
    }

    // Tile observation rows are the player's event rows for the tile's outcomes with the tile's own
    // numerator. They are stored once: each tile keeps the indices of its outcomes in Sources and the
    // non-zero tile numerators, and a read rebuilds the rows from the retained player rows only.
    // Anything that would not rebuild exactly is stored in full instead.
    private sealed record CheckpointTileObservations(Guid TileId, IReadOnlyList<int> Sources, IReadOnlyList<CheckpointTileReceived> Received);
    private sealed record CheckpointTileReceived(Guid TeamId, Guid PlayerId, Guid CharacterId, int Source, int Received);

    internal static string SerializeCheckpointPayload(StatsLuck luck)
    {
        var observations = CompactTileObservations(luck);
        if (observations is null) return JsonSerializer.Serialize(luck, CheckpointJsonOptions);
        var stripped = luck with
        {
            Tiles = luck.Tiles!.Select(tile => tile with
            {
                Teams = tile.Teams.Select(team => team with
                {
                    Players = team.Players.Select(player => player with { Sources = null }).ToArray()
                }).ToArray()
            }).ToArray()
        };
        var root = JsonSerializer.SerializeToNode(stripped, CheckpointJsonOptions)!.AsObject();
        root.Add(TileObservationsProperty, JsonSerializer.SerializeToNode(observations, CheckpointJsonOptions));
        return root.ToJsonString(CheckpointJsonOptions);
    }

    // Reads both the compact form and full (including indented) payloads written before it.
    internal static StatsLuck? DeserializeCheckpointPayload(string payload, bool includeTiles)
    {
        if (!includeTiles) return JsonSerializer.Deserialize<StatsLuck>(payload);
        using var document = JsonDocument.Parse(payload);
        var saved = document.RootElement.Deserialize<StatsLuck>();
        if (saved?.Tiles is null || !document.RootElement.TryGetProperty(TileObservationsProperty, out var element)) return saved;
        var observations = element.Deserialize<CheckpointTileObservations[]>();
        // An inconsistent tile map fails closed to the unavailable tile view, as a missing one does.
        return saved with { Tiles = observations is null ? null : RebuildTileObservations(saved, observations) };
    }

    private static CheckpointTileObservations[]? CompactTileObservations(StatsLuck luck)
    {
        if (luck.Tiles is null) return null;
        var sourceIndex = new Dictionary<(Guid, Guid), int>();
        for (var i = 0; i < luck.Sources.Count; i++) sourceIndex.TryAdd((luck.Sources[i].SourceDropId, luck.Sources[i].ItemId), i);
        var result = new List<CheckpointTileObservations>(luck.Tiles.Count);
        foreach (var tile in luck.Tiles)
        {
            var rows = tile.Teams.SelectMany(team => team.Players.SelectMany(player => player.Sources ?? [])).ToArray();
            if (tile.Teams.Any(team => team.Players.Any(player => player.Sources is null)) ||
                rows.Any(row => !sourceIndex.ContainsKey((row.SourceDropId, row.ItemId))))
                return null;
            var sources = rows.Select(row => sourceIndex[(row.SourceDropId, row.ItemId)]).Distinct().Order().ToArray();
            var received = tile.Teams.SelectMany(team => team.Players.SelectMany(player => player.Sources!
                .Where(row => row.Received != 0)
                .Select(row => new CheckpointTileReceived(team.TeamId, player.PlayerId, row.CharacterId, sourceIndex[(row.SourceDropId, row.ItemId)], row.Received))))
                .ToArray();
            result.Add(new(tile.TileId, sources, received));
        }
        var compact = result.ToArray();
        var rebuilt = RebuildTileObservations(luck, compact);
        // Store once only when the rebuild reproduces every stored row exactly.
        return rebuilt is not null && luck.Tiles.Zip(rebuilt).All(pair => pair.First.Teams.Zip(pair.Second.Teams).All(teams =>
            teams.First.Players.Zip(teams.Second.Players).All(players => players.First.Sources!.SequenceEqual(players.Second.Sources!))))
            ? compact : null;
    }

    private static StatsTileLuck[]? RebuildTileObservations(StatsLuck saved, IReadOnlyList<CheckpointTileObservations> observations)
    {
        var byTile = new Dictionary<Guid, CheckpointTileObservations>();
        foreach (var observation in observations)
            if (observation?.Sources is null || observation.Received is null || !byTile.TryAdd(observation.TileId, observation)) return null;
        var eventPlayers = new Dictionary<(Guid, Guid), StatsLuckPlayer>();
        foreach (var team in saved.Teams)
            foreach (var player in team.Players)
                if (player.Sources is null || !eventPlayers.TryAdd((team.TeamId, player.PlayerId), player)) return null;
        var tiles = new List<StatsTileLuck>(saved.Tiles!.Count);
        foreach (var tile in saved.Tiles!)
        {
            if (!byTile.TryGetValue(tile.TileId, out var observation) ||
                observation.Sources.Any(index => index < 0 || index >= saved.Sources.Count) ||
                observation.Received.Any(row => row is null || row.Source < 0 || row.Source >= saved.Sources.Count))
                return null;
            var outcomes = observation.Sources.Select(index => (saved.Sources[index].SourceDropId, saved.Sources[index].ItemId)).ToHashSet();
            var received = new Dictionary<(Guid, Guid, Guid, Guid, Guid), int>();
            foreach (var row in observation.Received)
                if (!received.TryAdd((row.TeamId, row.PlayerId, row.CharacterId, saved.Sources[row.Source].SourceDropId, saved.Sources[row.Source].ItemId), row.Received)) return null;
            var teams = new List<StatsTileLuckTeam>(tile.Teams.Count);
            foreach (var team in tile.Teams)
            {
                var players = new List<StatsTileLuckPlayer>(team.Players.Count);
                foreach (var player in team.Players)
                {
                    if (!eventPlayers.TryGetValue((team.TeamId, player.PlayerId), out var eventPlayer)) return null;
                    players.Add(player with
                    {
                        Sources = eventPlayer.Sources.Where(row => outcomes.Contains((row.SourceDropId, row.ItemId)))
                            .Select(row => row with
                            {
                                Received = received.GetValueOrDefault((team.TeamId, player.PlayerId, row.CharacterId, row.SourceDropId, row.ItemId))
                            }).ToArray()
                    });
                }
                teams.Add(team with { Players = players.ToArray() });
            }
            tiles.Add(tile with { Teams = teams.ToArray() });
        }
        return tiles.ToArray();
    }

    // Convert one retained v1 snapshot in place. This operation never reads the metric cache or
    // current WOM evidence; the old payload is the only input to the v2 derivation. The event lock
    // and schema predicate make retries idempotent and preserve all original observation times.
    public static async Task<bool> ConvertLegacyCheckpointAsync(ApplicationDbContext context, TimeProvider clock, Guid eventId, CancellationToken ct = default) =>
        (await ConvertLegacyCheckpointWithDiagnosticAsync(context, clock, eventId, ct)).Converted;

    public static async Task<StatsLegacyCheckpointConversionResult> ConvertLegacyCheckpointWithDiagnosticAsync(
        ApplicationDbContext context, TimeProvider clock, Guid eventId, CancellationToken ct = default)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("Checkpoint conversion requires an event transaction.");
        RequireConsistentTransaction(context);
        var ev = await context.Events.FromSqlInterpolated($"SELECT * FROM events WHERE id = {eventId} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct);
        if (ev is null) return new(false, "event-not-found");
        var legacy = await context.EventStatsLuckCheckpoints.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId, ct);
        if (legacy is null) return new(false, "legacy-row-not-found");
        if (legacy.SchemaVersion != 1) return new(false, "row-is-not-legacy-v1");

        StatsLuck saved;
        try
        {
            using var document = JsonDocument.Parse(legacy.Payload);
            if (HasDuplicateJsonProperties(document.RootElement))
                return new(false, BoundedDiagnostic("duplicate-property", "payload contains duplicate JSON properties"));
            saved = JsonSerializer.Deserialize<StatsLuck>(legacy.Payload)
                ?? throw new JsonException("payload root is null");
            if (!ValidateRetainedLegacySnapshot(saved, out var diagnostic))
                return new(false, diagnostic);
            // Version-1 payloads do not retain the tile-to-source observation map needed to
            // recompute tile percentiles. Keep their retained KC/identities, but clear the
            // derived tile Luck so conversion never republishes the retired signed score.
            var converted = RescoreLuck(saved, null, rebuildTiles: false) with
            {
                Tiles = ClearLegacyTileScores(saved.Tiles)
            };
            var payload = SerializeCheckpointPayload(converted);
            if (CheckpointPayloadBytes(payload) > EventStatsLuckCheckpoint.MaximumPayloadBytes)
                return new(false, "converted-payload-too-large");
            var convertedAt = clock.GetUtcNow();
            var changed = await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE event_stats_luck_checkpoints
                SET schema_version = {EventStatsLuckCheckpoint.CurrentSchemaVersion},
                    algorithm_version = {"luck-percentile-kc-v2-conversion"},
                    converted_from_schema_version = {1},
                    converted_at = {convertedAt},
                    payload = CAST({payload} AS jsonb)
                WHERE event_id = {eventId} AND schema_version = {1}
                """, ct);
            return changed == 1 ? new(true, null) : new(false, "legacy-row-changed-before-conversion");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or OverflowException)
        {
            return new(false, BoundedDiagnostic("unsupported-retained-payload", ex.Message));
        }
    }

    private static string BoundedDiagnostic(string code, string detail)
    {
        var value = code + ": " + detail.Replace('\n', ' ').Replace('\r', ' ');
        return value.Length <= 240 ? value : value[..240];
    }

    private static bool IsCompleteSnapshot(string payload)
    {
        try
        {
            var saved = JsonSerializer.Deserialize<StatsLuck>(payload);
            // Complete means every observation was calculable. A result calculated from the available
            // data (a waiting or impossible boss left out) must not fence later partial batches.
            return saved?.Result.Status is StatsLuckStatus.Calculated or StatsLuckStatus.NoEligibleActivity &&
                (saved.Teams ?? []).All(team => (team.Players ?? []).All(player =>
                    player.Result?.Status is StatsLuckStatus.Calculated or StatsLuckStatus.NoEligibleActivity &&
                    (player.Sources ?? []).All(row => row.Expected is not null) &&
                    (player.Result.Activities ?? []).All(line => line.Status is StatsLuckStatus.Calculated or StatsLuckStatus.NoEligibleActivity)));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasDuplicateJsonProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name) || HasDuplicateJsonProperties(property.Value)) return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array && element.EnumerateArray().Any(HasDuplicateJsonProperties))
            return true;
        return false;
    }

    private static bool ValidateRetainedLegacySnapshot(StatsLuck saved, out string diagnostic)
    {
        diagnostic = "unsupported-retained-payload: retained structure is incomplete";
        if (saved.Result is null || saved.Teams is null || saved.Sources is null) return false;
        if (!ValidateResult(saved.Result, out diagnostic)) return false;

        var sourceKeys = new HashSet<(Guid SourceDropId, Guid ItemId)>();
        foreach (var source in saved.Sources)
        {
            if (source is null || source.SourceDropId == Guid.Empty || source.ItemId == Guid.Empty || source.BossId == Guid.Empty ||
                string.IsNullOrWhiteSpace(source.Metric) || string.IsNullOrWhiteSpace(source.RollGroup) ||
                source.Rolls is not > 0 || source.Probability is < 0 or > 1 ||
                source.ParentProbability is < 0 or > 1)
            {
                diagnostic = "unsupported-retained-payload: invalid source mechanics";
                return false;
            }
            if (!sourceKeys.Add((source.SourceDropId, source.ItemId)))
            {
                diagnostic = "unsupported-retained-payload: duplicate source mechanics";
                return false;
            }
        }

        var teamIds = new HashSet<Guid>();
        foreach (var team in saved.Teams)
        {
            if (team is null || team.TeamId == Guid.Empty || string.IsNullOrWhiteSpace(team.Name) || team.Players is null ||
                !teamIds.Add(team.TeamId))
            {
                diagnostic = "unsupported-retained-payload: invalid retained team";
                return false;
            }
            if (team.Result is null)
            {
                diagnostic = "unsupported-retained-payload: retained team result is null";
                return false;
            }
            if (!ValidateResult(team.Result, out diagnostic)) return false;
            var playerIds = new HashSet<Guid>();
            foreach (var player in team.Players)
            {
                if (player is null || player.PlayerId == Guid.Empty || string.IsNullOrWhiteSpace(player.Name) ||
                    player.Sources is null || !playerIds.Add(player.PlayerId))
                {
                    diagnostic = "unsupported-retained-payload: invalid retained player";
                    return false;
                }
                if (player.Result is null)
                {
                    diagnostic = "unsupported-retained-payload: retained player result is null";
                    return false;
                }
                if (!ValidateResult(player.Result, out diagnostic)) return false;
                var observedKeys = new HashSet<(Guid CharacterId, Guid SourceDropId, Guid ItemId)>();
                foreach (var row in player.Sources)
                {
                    if (row is null || row.CharacterId == Guid.Empty || !sourceKeys.Contains((row.SourceDropId, row.ItemId)) ||
                        row.Received < 0 || row.Activity is < 0 || row.Expected is < 0 ||
                        row.Activity is { } activity && decimal.Truncate(activity) != activity)
                    {
                        diagnostic = "unsupported-retained-payload: invalid retained observation";
                        return false;
                    }
                    if (!observedKeys.Add((row.CharacterId, row.SourceDropId, row.ItemId)))
                    {
                        diagnostic = "unsupported-retained-payload: duplicate retained observation";
                        return false;
                    }
                }
            }
        }

        if (saved.Tiles is not null)
        {
            var tileIds = new HashSet<Guid>();
            foreach (var tile in saved.Tiles)
            {
                if (tile is null || tile.TileId == Guid.Empty || tile.Teams is null || !tileIds.Add(tile.TileId))
                {
                    diagnostic = "unsupported-retained-payload: invalid retained tile scope";
                    return false;
                }
                foreach (var team in tile.Teams)
                {
                    if (team is null || team.TeamId == Guid.Empty || team.Players is null || team.Metrics is null)
                    {
                        diagnostic = "unsupported-retained-payload: invalid retained tile team";
                        return false;
                    }
                    if (!ValidateResult(team.Result, out diagnostic)) return false;
                    if (team.Metrics.Any(metric => metric is null))
                    {
                        diagnostic = "unsupported-retained-payload: retained tile team metric is null";
                        return false;
                    }
                    foreach (var player in team.Players)
                    {
                        if (player is null || player.PlayerId == Guid.Empty || player.Metrics is null)
                        {
                            diagnostic = "unsupported-retained-payload: invalid retained tile player";
                            return false;
                        }
                        if (!ValidateResult(player.Result, out diagnostic)) return false;
                        if (player.Metrics.Any(metric => metric is null))
                        {
                            diagnostic = "unsupported-retained-payload: retained tile player metric is null";
                            return false;
                        }
                    }
                }
            }
        }
        return true;
    }

    private static bool ValidateResult(StatsLuckResult? result, out string diagnostic)
    {
        diagnostic = "unsupported-retained-payload: invalid retained result";
        if (result is null || result.Received < 0 || result.Expected is < 0 ||
            !Enum.IsDefined(result.Status) || result.Activities?.Any(activity =>
                activity is null || activity.Received < 0 || activity.Expected is < 0 ||
                activity.Kc is < 0 || !Enum.IsDefined(activity.Status)) == true)
            return false;
        diagnostic = string.Empty;
        return true;
    }

    private static StatsTileLuck[]? ClearLegacyTileScores(IReadOnlyList<StatsTileLuck>? tiles)
    {
        if (tiles is null) return null;
        return tiles.Select(tile =>
        {
            var status = tile.HasDropOutcomes ? StatsLuckStatus.WaitingForActivityData : StatsLuckStatus.NoEligibleActivity;
            return tile with
            {
                Teams = tile.Teams.Select(team => team with
                {
                    Result = ClearLegacyTileResult(team.Result, status),
                    Metrics = team.Metrics.Select(metric => metric with
                    {
                        Status = tile.HasDropOutcomes ? StatsLuckStatus.WaitingForActivityData : StatsLuckStatus.NoEligibleActivity,
                        Percentage = null,
                        KcDifference = null
                    }).ToArray(),
                    Players = team.Players.Select(player => player with
                    {
                        Result = ClearLegacyTileResult(player.Result, status),
                        Metrics = player.Metrics.Select(metric => metric with
                        {
                            Status = tile.HasDropOutcomes ? StatsLuckStatus.WaitingForActivityData : StatsLuckStatus.NoEligibleActivity,
                            Percentage = null,
                            KcDifference = null
                        }).ToArray()
                    }).ToArray()
                }).ToArray()
            };
        }).ToArray();
    }

    private static StatsLuckResult ClearLegacyTileResult(StatsLuckResult result, StatsLuckStatus status) =>
        result with { Expected = null, Percentage = null, KcDifference = null, Activities = null, Status = status };

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
                assignment_fingerprint, source_fingerprint, lifecycle_fingerprint, calculated_at, fetched_at, upstream_updated_at,
                algorithm_version, converted_from_schema_version, converted_at, payload)
            VALUES ({candidate.EventId}, {candidate.SchemaVersion}, {candidate.EvidenceRevision}, {candidate.CompetitionId}, {candidate.Generation}, {candidate.ActivityBatchId},
                {candidate.AssignmentFingerprint}, {candidate.SourceFingerprint}, {candidate.LifecycleFingerprint}, {candidate.CalculatedAt}, {candidate.FetchedAt}, {candidate.UpstreamUpdatedAt},
                {candidate.AlgorithmVersion}, {candidate.ConvertedFromSchemaVersion}, {candidate.ConvertedAt}, CAST({candidate.Payload} AS jsonb))
            ON CONFLICT (event_id) DO UPDATE SET schema_version = EXCLUDED.schema_version, evidence_revision = EXCLUDED.evidence_revision,
                competition_id = EXCLUDED.competition_id, generation = EXCLUDED.generation, activity_batch_id = EXCLUDED.activity_batch_id,
                assignment_fingerprint = EXCLUDED.assignment_fingerprint, source_fingerprint = EXCLUDED.source_fingerprint, lifecycle_fingerprint = EXCLUDED.lifecycle_fingerprint,
                calculated_at = EXCLUDED.calculated_at, fetched_at = EXCLUDED.fetched_at, upstream_updated_at = EXCLUDED.upstream_updated_at,
                algorithm_version = EXCLUDED.algorithm_version, converted_from_schema_version = EXCLUDED.converted_from_schema_version,
                converted_at = EXCLUDED.converted_at, payload = EXCLUDED.payload
            WHERE (event_stats_luck_checkpoints.schema_version < EXCLUDED.schema_version)
                OR (event_stats_luck_checkpoints.schema_version = EXCLUDED.schema_version
                    AND event_stats_luck_checkpoints.evidence_revision <= EXCLUDED.evidence_revision
                    AND event_stats_luck_checkpoints.calculated_at <= EXCLUDED.calculated_at)
            """, ct);
        return changed == 1;
    }
}
