using Bingo.Application.Boards;

namespace Bingo.Application.Tests;

public sealed class RequirementProgressCalculatorTests
{
    private static readonly Guid Fang = Guid.NewGuid();
    private static readonly Guid Visage = Guid.NewGuid();

    [Fact]
    public void DuplicateAllowedDropCanContributeRepeatedly()
    {
        var result = RequirementProgressCalculator.DropContribution(true, false, [new(Fang, null)], [new(Fang), new(Fang), new(Fang)]);
        Assert.Equal(3, result);
    }

    [Fact]
    public void DuplicateDisallowedDropIsCappedPerEligibleDrop()
    {
        var result = RequirementProgressCalculator.DropContribution(false, false, [new(Fang, 1), new(Visage, 1)], [new(Fang), new(Fang), new(Visage)]);
        Assert.Equal(2, result);
    }

    [Fact]
    public void HigherWeightOnlyCountsWhenRequirementAllowsIt()
    {
        var eligible = new[] { new EligibleDrop(Fang, null) }; var credits = new[] { new DropCredit(Fang, 3) };
        Assert.Equal(1, RequirementProgressCalculator.DropContribution(true, false, eligible, credits));
        Assert.Equal(3, RequirementProgressCalculator.DropContribution(true, true, eligible, credits));
    }

    [Fact]
    public void ManualQuantityAndMultipleRequirementsUseTheirTargets()
    {
        Assert.True(RequirementProgressCalculator.IsComplete(3, 3));
        Assert.False(RequirementProgressCalculator.AreAllRequirementsComplete([new(5, 5), new(5, 4)]));
        Assert.True(RequirementProgressCalculator.AreAllRequirementsComplete([new(5, 5), new(5, 6)]));
    }
}
