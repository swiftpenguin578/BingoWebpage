namespace Bingo.Application.Boards;

public static class RequirementProgressCalculator
{
    public static int DropContribution(bool duplicatesAllowed, bool allowHigherWeightings, IReadOnlyCollection<EligibleDrop> eligibleDrops, IReadOnlyCollection<DropCredit> approvedCredits)
    {
        var eligible = eligibleDrops.ToDictionary(x => x.SourceDropId);
        if (duplicatesAllowed)
            return approvedCredits.Where(x => eligible.ContainsKey(x.SourceDropId)).Sum(x => allowHigherWeightings ? Math.Max(1, x.Weight) : 1);

        return approvedCredits.Where(x => eligible.ContainsKey(x.SourceDropId)).GroupBy(x => x.SourceDropId).Sum(group =>
        {
            var maximum = eligible[group.Key].MaximumContribution ?? 1;
            var contribution = allowHigherWeightings ? group.Sum(x => Math.Max(1, x.Weight)) : group.Count();
            return Math.Min(maximum, contribution);
        });
    }

    public static bool IsComplete(int target, int contribution) => target > 0 && contribution >= target;
    public static bool AreAllRequirementsComplete(IEnumerable<RequirementResult> requirements) => requirements.Any() && requirements.All(x => IsComplete(x.Target, x.Contribution));
}

public sealed record EligibleDrop(Guid SourceDropId, int? MaximumContribution);
public sealed record DropCredit(Guid SourceDropId, int Weight = 1);
public sealed record RequirementResult(int Target, int Contribution);
