using Bingo.Application.Boards;

namespace Bingo.Application.Tests;

public sealed class EhbCalculatorTests
{
    [Fact]
    public void MoleExampleIsTenEhb()
    {
        var result = EhbCalculator.CalculateDropRequirement(1, [new EligibleDropRate(100, 0.001m)]);
        Assert.Equal(10m, result);
    }

    [Fact]
    public void MultipleEligibleDropsCombineTheirHourlyAcquisitionRates()
    {
        var result = EhbCalculator.CalculateDropRequirement(5,
        [
            new EligibleDropRate(10, 0.01m),
            new EligibleDropRate(10, 0.02m)
        ]);
        Assert.Equal(16.666666666666666666666666667m, result);
    }

    [Fact]
    public void MissingNumericDataRequiresManualEstimate()
    {
        Assert.Null(EhbCalculator.CalculateDropRequirement(1, [new EligibleDropRate(100, null)]));
    }

    [Fact]
    public void DistinctDropsAddTheirIndividualExpectedHours()
    {
        var result = EhbCalculator.CalculateDropRequirement(3,
        [
            new EligibleDropRate(65, 1m / 912m),
            new EligibleDropRate(35, 1m / 375m),
            new EligibleDropRate(35, 1m / 375m)
        ], duplicatesAllowed: false);

        var expected = (912m / 65m) + (375m / 35m) + (375m / 35m);
        Assert.InRange(result!.Value, expected - 0.000001m, expected + 0.000001m);
    }

    [Fact]
    public void DistinctRequirementWithoutEnoughEligibleDropsCannotBeEstimated()
    {
        var result = EhbCalculator.CalculateDropRequirement(2,
            [new EligibleDropRate(10, 0.01m)], duplicatesAllowed: false);

        Assert.Null(result);
    }

    [Fact]
    public void AlternativeBossesForTheSameDistinctItemUseTheBestSourceWithoutCountingTwice()
    {
        var hilt = Guid.NewGuid();
        var blade = Guid.NewGuid();
        var gem = Guid.NewGuid();
        var result = EhbCalculator.CalculateDropRequirement(3,
        [
            new EligibleDropRate(65, 1m / 912m, hilt),
            new EligibleDropRate(65, 1m / 912m, hilt),
            new EligibleDropRate(35, 1m / 375m, blade),
            new EligibleDropRate(35, 1m / 375m, blade),
            new EligibleDropRate(35, 1m / 375m, gem),
            new EligibleDropRate(35, 1m / 375m, gem)
        ], duplicatesAllowed: false);

        var expected = (912m / 65m) + (375m / 35m) + (375m / 35m);
        Assert.InRange(result!.Value, expected - 0.000001m, expected + 0.000001m);
    }

    [Fact]
    public void FiveLunarAndFiveBarrowsDropsSumAsTwoRequirements()
    {
        var lunar = EhbCalculator.CalculateDropRequirement(5,
            Enumerable.Repeat(new EligibleDropRate(18, 1m / 224m), 12));
        var barrows = EhbCalculator.CalculateDropRequirement(5,
            Enumerable.Repeat(new EligibleDropRate(22, 7m / 2448m), 24));

        var total = EhbCalculator.SumRequirements([lunar, barrows]);

        Assert.InRange(total, 8.49m, 8.51m);
    }
}
