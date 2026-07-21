namespace Bingo.Application.Boards;

public static class EhbCalculator
{
    private const int MaximumDistinctItems = 20;
    private const decimal ProbabilityRoundingTolerance = 0.000000001m;

    public static decimal? CalculateDropRequirement(
        int targetContribution,
        IEnumerable<EligibleDropRate> eligibleDrops,
        bool duplicatesAllowed = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(targetContribution, 1);
        var drops = eligibleDrops.ToList();
        if (drops.Count == 0 || drops.Any(x => x.EfficientCompletionsPerHour is not > 0 || x.NumericProbability is not > 0 || x.NumericProbability > 1 || x.CreditedWeight < 1 || x.RollsPerCompletion < 1))
        {
            return null;
        }

        return duplicatesAllowed
            ? CalculateRepeatable(targetContribution, drops)
            : CalculateDistinct(targetContribution, drops);
    }

    private static decimal? CalculateRepeatable(int target, IReadOnlyList<EligibleDropRate> drops)
    {
        var bosses = BuildBosses(drops, x => x.CreditedWeight, out var valid);
        if (!valid) return null;
        var values = new decimal[target + 1];

        for (var progress = target - 1; progress >= 0; progress--)
        {
            decimal? best = null;
            foreach (var boss in bosses)
            {
                var outcomes = BuildProgressOutcomes(boss.Drops, target - progress);
                if (outcomes is null) return null;
                var noProgress = outcomes.GetValueOrDefault(0);
                if (noProgress >= 1) continue;
                var future = outcomes.Where(x => x.Key > 0).Sum(x => x.Value * values[Math.Min(target, progress + x.Key)]);
                var estimate = ((1m / boss.CompletionsPerHour) + future) / (1m - noProgress);
                best = best is null ? estimate : Math.Min(best.Value, estimate);
            }
            if (best is null) return null;
            values[progress] = best.Value;
        }

        return values[0];
    }

    private static decimal? CalculateDistinct(int target, IReadOnlyList<EligibleDropRate> drops)
    {
        if (drops.Any(x => x.DistinctDropId is null)) return null;
        var items = drops.Select(x => x.DistinctDropId!.Value).Distinct().ToList();
        if (items.Count > MaximumDistinctItems) return null;
        var weights = new int[items.Count];
        for (var index = 0; index < items.Count; index++)
        {
            var itemWeights = drops.Where(x => x.DistinctDropId == items[index]).Select(x => x.CreditedWeight).Distinct().ToList();
            if (itemWeights.Count != 1) return null;
            weights[index] = itemWeights[0];
        }
        if (weights.Sum() < target) return null;

        var indexes = items.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        var bosses = BuildBosses(drops, x => 1 << indexes[x.DistinctDropId!.Value], out var valid);
        if (!valid) return null;
        var memo = new Dictionary<int, decimal>();

        decimal? Solve(int mask)
        {
            if (Weight(mask, weights) >= target) return 0;
            if (memo.TryGetValue(mask, out var cached)) return cached;
            decimal? best = null;
            foreach (var boss in bosses)
            {
                var outcomes = BuildMaskOutcomes(boss.Drops, mask);
                if (outcomes is null) return null;
                var unchanged = outcomes.GetValueOrDefault(mask);
                if (unchanged >= 1) continue;
                decimal future = 0;
                foreach (var outcome in outcomes.Where(x => x.Key != mask))
                {
                    var next = Solve(outcome.Key);
                    if (next is null) return null;
                    future += outcome.Value * next.Value;
                }
                var estimate = ((1m / boss.CompletionsPerHour) + future) / (1m - unchanged);
                best = best is null ? estimate : Math.Min(best.Value, estimate);
            }
            if (best is not null) memo[mask] = best.Value;
            return best;
        }

        return Solve(0);
    }

    private static List<BossRate> BuildBosses(IReadOnlyList<EligibleDropRate> drops, Func<EligibleDropRate, int> outcome, out bool valid)
    {
        valid = true;
        var result = new List<BossRate>();
        foreach (var group in drops.GroupBy(x => x.BossActivityId))
        {
            var rates = group.Select(x => x.EfficientCompletionsPerHour!.Value).Distinct().ToList();
            if (rates.Count != 1) { valid = false; return result; }
            result.Add(new BossRate(rates[0], group.Select(x => new RollRate(x.NumericProbability!.Value, outcome(x), x.RollsPerCompletion, x.RollGroup ?? "default")).ToList()));
        }
        return result;
    }

    private static Dictionary<int, decimal>? BuildProgressOutcomes(IReadOnlyList<RollRate> drops, int cap)
    {
        var outcomes = new Dictionary<int, decimal> { [0] = 1m };
        foreach (var group in drops.GroupBy(x => x.RollGroup))
        {
            var rolls = group.Select(x => x.Rolls).Distinct().ToList();
            var useful = group.Sum(x => x.Probability);
            if (rolls.Count != 1 || useful > 1 + ProbabilityRoundingTolerance) return null;
            var probabilityScale = useful > 1 ? useful : 1m;
            var noUsefulOutcome = Math.Max(0, 1m - useful);
            for (var roll = 0; roll < rolls[0]; roll++)
            {
                var next = new Dictionary<int, decimal>();
                foreach (var state in outcomes)
                {
                    Add(next, state.Key, state.Value * noUsefulOutcome);
                    foreach (var drop in group) Add(next, Math.Min(cap, state.Key + drop.Outcome), state.Value * (drop.Probability / probabilityScale));
                }
                outcomes = next;
            }
        }
        return outcomes;
    }

    private static Dictionary<int, decimal>? BuildMaskOutcomes(IReadOnlyList<RollRate> drops, int startingMask)
    {
        var outcomes = new Dictionary<int, decimal> { [startingMask] = 1m };
        foreach (var group in drops.GroupBy(x => x.RollGroup))
        {
            var rolls = group.Select(x => x.Rolls).Distinct().ToList();
            var useful = group.Sum(x => x.Probability);
            if (rolls.Count != 1 || useful > 1 + ProbabilityRoundingTolerance) return null;
            var probabilityScale = useful > 1 ? useful : 1m;
            var noUsefulOutcome = Math.Max(0, 1m - useful);
            for (var roll = 0; roll < rolls[0]; roll++)
            {
                var next = new Dictionary<int, decimal>();
                foreach (var state in outcomes)
                {
                    Add(next, state.Key, state.Value * noUsefulOutcome);
                    foreach (var drop in group) Add(next, state.Key | drop.Outcome, state.Value * (drop.Probability / probabilityScale));
                }
                outcomes = next;
            }
        }
        return outcomes;
    }

    private static int Weight(int mask, int[] weights)
    {
        var total = 0;
        for (var index = 0; index < weights.Length; index++) if ((mask & (1 << index)) != 0) total += weights[index];
        return total;
    }

    private static void Add(Dictionary<int, decimal> values, int key, decimal value)
    {
        values.TryGetValue(key, out var existing);
        values[key] = existing + value;
    }

    public static decimal SumRequirements(IEnumerable<decimal?> requirementEstimates, decimal? manualOverride = null)
    {
        if (manualOverride is not null) return manualOverride.Value;
        var estimates = requirementEstimates.ToList();
        return estimates.Any(estimate => estimate is null) ? 0 : estimates.Sum(estimate => estimate!.Value);
    }

    private sealed record BossRate(decimal CompletionsPerHour, IReadOnlyList<RollRate> Drops);
    private sealed record RollRate(decimal Probability, int Outcome, int Rolls, string RollGroup);
}

public sealed record EligibleDropRate(
    decimal? EfficientCompletionsPerHour,
    decimal? NumericProbability,
    Guid? DistinctDropId = null,
    Guid BossActivityId = default,
    int CreditedWeight = 1,
    int RollsPerCompletion = 1,
    string RollGroup = "default");
