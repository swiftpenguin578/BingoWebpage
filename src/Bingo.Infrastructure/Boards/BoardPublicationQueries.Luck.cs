using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bingo.Domain.Boards;
using Bingo.Domain.Catalogue;
using Bingo.Domain.Events;
using Bingo.Infrastructure.Persistence;
using Bingo.Infrastructure.WiseOldMan;
using Microsoft.EntityFrameworkCore;

namespace Bingo.Infrastructure.Boards;

public static partial class BoardPublicationQueries
{
    /// <summary>Called under the event write lock, inside the successful approval or sync transaction.</summary>
    public static async Task RetainLuckOutcomeBasesAsync(this ApplicationDbContext db, Guid eventId, DateTimeOffset now, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Luck basis retention requires the event transaction.");
        var approvals = await db.BoardApprovalSnapshots.AsNoTracking()
            .Where(x => db.Boards.Any(board => board.Id == x.BoardId && board.EventId == eventId)).ToListAsync(ct);
        var approvalIds = approvals.Select(x => x.Id).ToArray();
        var entries = await (from drop in db.BoardApprovalRequirementDropSnapshots.AsNoTracking()
                             join requirement in db.BoardApprovalRequirementSnapshots.AsNoTracking() on drop.ApprovalRequirementSnapshotId equals requirement.Id
                             join tile in db.BoardApprovalTileSnapshots.AsNoTracking() on requirement.ApprovalTileSnapshotId equals tile.Id
                             where approvalIds.Contains(tile.ApprovalSnapshotId) && !requirement.ManualObjective
                             select new { Drop = drop, tile.ApprovalSnapshotId }).ToListAsync(ct);
        var requirementIds = entries.Select(x => x.Drop.ApprovalRequirementSnapshotId).Distinct().ToArray();
        var retainedBosses = await db.BoardApprovalRequirementBossSnapshots.AsNoTracking().Where(x => requirementIds.Contains(x.ApprovalRequirementSnapshotId)).ToListAsync(ct);
        var sourceIds = entries.Select(x => x.Drop.SourceDropId).Distinct().ToArray();
        var identities = await db.SourceDrops.AsNoTracking().Where(x => sourceIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var bossIds = retainedBosses.Select(x => x.BossActivityId).Distinct().ToArray();
        var bosses = await db.BossActivities.AsNoTracking().Where(x => bossIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        // A long-lived sync context may still track pre-fetch bases. Another successful approval
        // can bind one while HTTP is in flight; re-read persisted bindings under the event lock.
        foreach (var entry in db.ChangeTracker.Entries<EventLuckOutcomeBasis>().Where(x => x.Entity.EventId == eventId && x.State == EntityState.Unchanged).ToArray())
            entry.State = EntityState.Detached;
        var existing = await db.EventLuckOutcomeBases.Where(x => x.EventId == eventId).ToListAsync(ct);
        var byApproval = approvals.ToDictionary(x => x.Id);

        foreach (var outcome in entries.GroupBy(x => (x.Drop.SourceDropId, x.Drop.ItemIdSnapshot)))
        {
            var basis = existing.SingleOrDefault(x => x.SourceDropId == outcome.Key.SourceDropId && x.ItemIdSnapshot == outcome.Key.ItemIdSnapshot);
            if (basis is null)
            {
                var earliestAt = outcome.Min(x => byApproval[x.ApprovalSnapshotId].ApprovedAt);
                var earliest = outcome.Where(x => byApproval[x.ApprovalSnapshotId].ApprovedAt == earliestAt).ToArray();
                // Identical duplicates in ONE first approval are equivalent. Tied approvals or conflicting
                // mechanics are unavailable; UUID ordering never selects between conflicting rates.
                var unique = earliest.Select(x => x.ApprovalSnapshotId).Distinct().Count() == 1 && earliest.Select(x => MechanicsKey(x.Drop)).Distinct(StringComparer.Ordinal).Count() == 1;
                var chosen = unique ? earliest.OrderBy(x => x.Drop.Id).First() : null;
                Guid? bossId = null;
                if (chosen is not null && chosen.Drop.SourceDropId != Guid.Empty && chosen.Drop.ItemIdSnapshot != Guid.Empty && identities.TryGetValue(chosen.Drop.SourceDropId, out var identity) &&
                    earliest.All(x => retainedBosses.Any(boss => boss.ApprovalRequirementSnapshotId == x.Drop.ApprovalRequirementSnapshotId && boss.BossActivityId == identity.BossActivityId)))
                    bossId = identity.BossActivityId;
                var status = !unique ? LuckBasisStatus.ConflictingEarliestApproval : bossId is null ? LuckBasisStatus.MissingIdentity
                    : chosen!.Drop.NumericProbability is not (> 0 and <= 1) || chosen.Drop.RollsPerCompletion < 1 ? LuckBasisStatus.UnavailableMechanics : LuckBasisStatus.Retained;
                basis = new EventLuckOutcomeBasis(eventId, outcome.Key.SourceDropId, outcome.Key.ItemIdSnapshot, bossId,
                    chosen is null ? null : byApproval[chosen.ApprovalSnapshotId], chosen?.Drop, status);
                db.EventLuckOutcomeBases.Add(basis); existing.Add(basis);
            }
            if (basis.Metric is null && basis.BossActivityId is { } id && bosses.TryGetValue(id, out var current) &&
                current.MappingStatus == ApiMappingStatus.Verified && current.MappingCheckedAt is { } checkedAt &&
                current.ExternalIdentifier is { } metric && CompetitionMetricContract.SupportedMetrics.Contains(metric))
                basis.BindMetric(metric, now, checkedAt, current.Version);
        }
    }

    public static async Task<LuckSourceRequest> LuckSourceRequestAsync(this ApplicationDbContext db, Guid eventId, CancellationToken ct = default)
    {
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId, ct);
        var approvalId = board?.State is BoardState.Published or BoardState.Validated ? board.ActiveApprovalSnapshotId : null;
        var drops = approvalId is null ? [] : await (from drop in db.BoardApprovalRequirementDropSnapshots.AsNoTracking()
                                                     join requirement in db.BoardApprovalRequirementSnapshots.AsNoTracking() on drop.ApprovalRequirementSnapshotId equals requirement.Id
                                                     join tile in db.BoardApprovalTileSnapshots.AsNoTracking() on requirement.ApprovalTileSnapshotId equals tile.Id
                                                     where tile.ApprovalSnapshotId == approvalId && !requirement.ManualObjective
                                                     select drop).ToListAsync(ct);
        var bases = await db.EventLuckOutcomeBases.AsNoTracking().Where(x => x.EventId == eventId).ToListAsync(ct);
        var outcomes = drops.GroupBy(x => (x.SourceDropId, x.ItemIdSnapshot)).OrderBy(x => x.Key.SourceDropId).ThenBy(x => x.Key.ItemIdSnapshot)
            .Select(group =>
            {
                var basis = bases.SingleOrDefault(x => x.SourceDropId == group.Key.SourceDropId && x.ItemIdSnapshot == group.Key.ItemIdSnapshot);
                var issue = basis is null ? "BasisUnavailable" : basis.Status != LuckBasisStatus.Retained ? basis.Status.ToString() : CompetitionMetricContract.UnavailableReason(basis.Metric);
                return new LuckSourceOutcome(group.Key.SourceDropId, group.Key.ItemIdSnapshot, basis, issue);
            }).ToArray();
        var fingerprint = Hash(JsonSerializer.Serialize(new
        {
            EventId = eventId,
            BoardId = board?.Id,
            State = board?.State,
            ApprovalId = approvalId,
            // Approval/drop identities detect a changed eligible set even when all metric names are unchanged.
            Drops = drops.OrderBy(x => x.Id).Select(x => new { x.Id, x.SourceDropId, x.ItemIdSnapshot }),
            Outcomes = outcomes
        }));
        return new(fingerprint, outcomes.Select(x => x.Basis?.Metric).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            outcomes.All(x => x.UnavailableReason is null), outcomes);
    }

    private static string MechanicsKey(BoardApprovalRequirementDropSnapshot drop) => JsonSerializer.Serialize(new
    {
        drop.NumericProbability,
        drop.RollsPerCompletion,
        drop.ProbabilityScope,
        drop.ConditionalOnParent,
        drop.ParentProbability,
        drop.AssumedParticipants,
        drop.RollGroup,
        drop.RateCondition
    });

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

public sealed record LuckSourceOutcome(Guid SourceDropId, Guid ItemIdSnapshot, EventLuckOutcomeBasis? Basis, string? UnavailableReason);
public sealed record LuckSourceRequest(string Fingerprint, IReadOnlyCollection<string> Metrics, bool SourcesAvailable, IReadOnlyList<LuckSourceOutcome> Outcomes);
