namespace Bingo.Application.Boards;

public static class EhbCalculator
{
    public static decimal? CalculateDropRequirement(
        int targetContribution,
        IEnumerable<EligibleDropRate> eligibleDrops,
        bool duplicatesAllowed = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(targetContribution, 1);
        var validDrops = eligibleDrops
            .Where(drop => drop.EfficientCompletionsPerHour is > 0 && drop.NumericProbability is > 0)
            .ToList();

        if (!duplicatesAllowed)
        {
            var distinctDropRates = validDrops
                .Select((drop, index) => new { Key = drop.DistinctDropId?.ToString() ?? $"source-{index}", HourlyRate = drop.EfficientCompletionsPerHour!.Value * drop.NumericProbability!.Value })
                .GroupBy(drop => drop.Key)
                .Select(group => group.Max(drop => drop.HourlyRate))
                .ToList();

            if (distinctDropRates.Count < targetContribution)
            {
                return null;
            }

            return distinctDropRates
                .Select(hourlyRate => 1m / hourlyRate)
                .OrderBy(hours => hours)
                .Take(targetContribution)
                .Sum();
        }

        var contributionsPerHour = validDrops
            .Sum(drop => drop.EfficientCompletionsPerHour!.Value * drop.NumericProbability!.Value);
        return contributionsPerHour <= 0 ? null : targetContribution / contributionsPerHour;
    }

    public static decimal SumRequirements(IEnumerable<decimal?> requirementEstimates, decimal? manualOverride = null) =>
        manualOverride ?? requirementEstimates.Sum(estimate => estimate ?? 0);
}

public sealed record EligibleDropRate(decimal? EfficientCompletionsPerHour, decimal? NumericProbability, Guid? DistinctDropId = null);
