namespace Bingo.Application.Boards;

public static class RequirementProgressCalculator
{
    public static int DropContribution(bool duplicatesAllowed, bool allowHigherWeightings, IReadOnlyCollection<EligibleDrop> eligibleDrops, IReadOnlyCollection<DropCredit> approvedCredits)
    {
        var eligible = eligibleDrops.ToDictionary(x => x.SourceDropId);
        var matchingCredits = approvedCredits.Where(x => eligible.ContainsKey(x.SourceDropId)).ToList();
        if (duplicatesAllowed)
            return matchingCredits.GroupBy(x => x.SourceDropId).Sum(group =>
            {
                var maximum = eligible[group.Key].MaximumContribution ?? int.MaxValue;
                var contribution = allowHigherWeightings ? group.Sum(x => Math.Max(1, x.Weight)) : group.Count();
                return Math.Min(maximum, contribution);
            });

        var capsByItem = eligible.Values.GroupBy(x => x.ItemIdSnapshot).ToDictionary(group => group.Key, group =>
        {
            var caps = group.Select(x => x.MaximumContribution ?? 1).Distinct().ToList();
            if (caps.Count != 1) throw new InvalidOperationException($"Eligible aliases for catalogue item {group.Key} have inconsistent contribution caps.");
            return caps[0];
        });
        return matchingCredits.GroupBy(x => eligible[x.SourceDropId].ItemIdSnapshot).Sum(group =>
        {
            var contribution = allowHigherWeightings ? group.Sum(x => Math.Max(1, x.Weight)) : group.Count();
            return Math.Min(capsByItem[group.Key], contribution);
        });
    }

    public static bool IsComplete(int target, int contribution) => target > 0 && contribution >= target;
    public static bool AreAllRequirementsComplete(IEnumerable<RequirementResult> requirements) => requirements.Any() && requirements.All(x => IsComplete(x.Target, x.Contribution));
}

public sealed record EligibleDrop(Guid SourceDropId, Guid ItemIdSnapshot, int? MaximumContribution)
{
    public EligibleDrop(Guid sourceDropId, int? maximumContribution) : this(sourceDropId, sourceDropId, maximumContribution) { }
}
public sealed record DropCredit(Guid SourceDropId, int Weight = 1);
public sealed record RequirementResult(int Target, int Contribution);
