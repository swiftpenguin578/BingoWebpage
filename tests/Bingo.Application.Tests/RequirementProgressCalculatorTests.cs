using Bingo.Application.Boards;

namespace Bingo.Application.Tests;

public sealed class RequirementProgressCalculatorTests
{
    private static readonly Guid Fang = Guid.NewGuid();
    private static readonly Guid Visage = Guid.NewGuid();
    private static readonly Guid SharedItem = Guid.NewGuid();

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
    public void DuplicateDisallowedAliasesShareTheImmutableItemCap()
    {
        var eligible = new[] { new EligibleDrop(Fang, SharedItem, 1), new EligibleDrop(Visage, SharedItem, 1) };
        var result = RequirementProgressCalculator.DropContribution(false, false, eligible, [new(Fang), new(Visage)]);
        Assert.Equal(1, result);
    }

    [Fact]
    public void DuplicateDisallowedAliasesCanUseAConsistentHigherCap()
    {
        var eligible = new[] { new EligibleDrop(Fang, SharedItem, 2), new EligibleDrop(Visage, SharedItem, 2) };
        var result = RequirementProgressCalculator.DropContribution(false, true, eligible, [new(Fang, 2), new(Visage)]);
        Assert.Equal(2, result);
    }

    [Fact]
    public void DuplicateDisallowedAliasesWithInconsistentCapsFailClosed()
    {
        var eligible = new[] { new EligibleDrop(Fang, SharedItem, 1), new EligibleDrop(Visage, SharedItem, 2) };
        Assert.Throws<InvalidOperationException>(() => RequirementProgressCalculator.DropContribution(false, false, eligible, [new(Fang)]));
    }

    [Fact]
    public void DuplicateAllowedCopiesRespectEachSourceCap()
    {
        var eligible = new[] { new EligibleDrop(Fang, SharedItem, 2), new EligibleDrop(Visage, SharedItem, 3) };
        var result = RequirementProgressCalculator.DropContribution(true, false, eligible, [new(Fang), new(Fang), new(Fang), new(Visage), new(Visage), new(Visage), new(Visage)]);
        Assert.Equal(5, result);
    }

    [Fact]
    public void TheSameImmutableItemIsIndependentInSiblingRequirements()
    {
        var eligible = new[] { new EligibleDrop(Fang, SharedItem, null) };
        Assert.Equal(1, RequirementProgressCalculator.DropContribution(false, false, eligible, [new(Fang)]));
        Assert.Equal(1, RequirementProgressCalculator.DropContribution(false, false, eligible, [new(Fang)]));
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
