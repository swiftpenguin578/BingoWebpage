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
        var first = Guid.NewGuid(); var second = Guid.NewGuid(); var third = Guid.NewGuid();
        var result = EhbCalculator.CalculateDropRequirement(3,
        [
            new EligibleDropRate(65, 1m / 912m, first, Guid.NewGuid()),
            new EligibleDropRate(35, 1m / 375m, second, Guid.NewGuid()),
            new EligibleDropRate(35, 1m / 375m, third, Guid.NewGuid())
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
        var fastBoss = Guid.NewGuid();
        var slowBoss = Guid.NewGuid();
        var result = EhbCalculator.CalculateDropRequirement(3,
        [
            new EligibleDropRate(65, 1m / 912m, hilt, fastBoss),
            new EligibleDropRate(30, 1m / 912m, hilt, slowBoss),
            new EligibleDropRate(65, 1m / 375m, blade, fastBoss),
            new EligibleDropRate(30, 1m / 375m, blade, slowBoss),
            new EligibleDropRate(65, 1m / 375m, gem, fastBoss),
            new EligibleDropRate(30, 1m / 375m, gem, slowBoss)
        ], duplicatesAllowed: false);

        var doingEachItemSeparately = (912m / 65m) + (375m / 65m) + (375m / 65m);
        Assert.NotNull(result);
        Assert.True(result < doingEachItemSeparately);
    }

    [Fact]
    public void WeightedDropAdvancesByItsConfiguredContribution()
    {
        var boss = Guid.NewGuid();
        var result = EhbCalculator.CalculateDropRequirement(2,
            [new EligibleDropRate(10, 0.01m, Guid.NewGuid(), boss, CreditedWeight: 2)]);

        Assert.Equal(10m, result);
    }

    [Fact]
    public void MultipleRollsAreCalculatedAsSeparateChances()
    {
        var result = EhbCalculator.CalculateDropRequirement(1,
            [new EligibleDropRate(1, 0.01m, Guid.NewGuid(), Guid.NewGuid(), RollsPerCompletion: 7)]);

        var expected = 1m / (1m - (decimal)Math.Pow(0.99, 7));
        Assert.InRange(result!.Value, expected - 0.000001m, expected + 0.000001m);
    }

    [Fact]
    public void DefaultRollGroupSeparatesMixedSingleAndMultipleRollDrops()
    {
        var boss = Guid.NewGuid();
        var result = EhbCalculator.CalculateDropRequirement(1,
        [
            new EligibleDropRate(10, 1m / 3000m, Guid.NewGuid(), boss),
            new EligibleDropRate(10, 1m / 4000m, Guid.NewGuid(), boss),
            new EligibleDropRate(10, 1m / 1024m, Guid.NewGuid(), boss, RollsPerCompletion: 2)
        ]);

        var singleRollProbability = 1m / 3000m + 1m / 4000m;
        var multipliedRollProbability = 1m - (1m - 1m / 1024m) * (1m - 1m / 1024m);
        var expected = 1m / (10m * (1m - (1m - singleRollProbability) * (1m - multipliedRollProbability)));

        Assert.InRange(result!.Value, expected - 0.000001m, expected + 0.000001m);
    }

    [Fact]
    public void HarmlessStoredProbabilityRoundingDoesNotInvalidateACompleteLootTable()
    {
        var boss = Guid.NewGuid();
        List<decimal> numerators = [8m, 2m, 2m, 2m, 2m, 2m, 1m];
        var rates = numerators
            .Select((numerator, index) => new EligibleDropRate(
                3m,
                decimal.Round(numerator / 19m, 12),
                Guid.NewGuid(),
                boss,
                index == 6 ? 2 : 1));

        var result = EhbCalculator.CalculateDropRequirement(6, rates);

        Assert.NotNull(result);
        Assert.True(result > 0);
    }

    [Fact]
    public void NormalModeRaidPurpleAssumptionsProduceDifferentRealisticEstimates()
    {
        var tobBoss = Guid.NewGuid();
        List<decimal> tobNumerators = [8m, 2m, 2m, 2m, 2m, 2m, 1m];
        var tob = EhbCalculator.CalculateDropRequirement(6, tobNumerators.Select((numerator, index) =>
            new EligibleDropRate(3m, numerator / 19m / 27.3m, Guid.NewGuid(), tobBoss, index == 6 ? 2 : 1)));

        var coxBoss = Guid.NewGuid();
        List<decimal> coxNumerators = [3m, 3m, 3m, 20m, 20m, 3m, 3m, 4m, 2m, 2m, 2m, 4m];
        var coxPurpleChance = 33_000m / 867_600m;
        var cox = EhbCalculator.CalculateDropRequirement(6, coxNumerators.Select((numerator, index) =>
            new EligibleDropRate(3m, numerator / 69m * coxPurpleChance, Guid.NewGuid(), coxBoss, index is 8 or 9 or 10 ? 2 : 1)));

        Assert.InRange(tob!.Value, 50m, 55m);
        Assert.InRange(cox!.Value, 45m, 50m);
        Assert.NotEqual(tob, cox);
    }

    [Fact]
    public void FiveLunarAndFiveBarrowsDropsSumAsTwoRequirements()
    {
        var lunar = EhbCalculator.CalculateDropRequirement(5,
            Enumerable.Repeat(new EligibleDropRate(14, 1m / 224m), 12));
        var barrowsBoss = Guid.NewGuid();
        var barrows = EhbCalculator.CalculateDropRequirement(5,
            Enumerable.Repeat(new EligibleDropRate(18, 1m / 2448m, BossActivityId: barrowsBoss, RollsPerCompletion: 7), 24));

        var total = EhbCalculator.SumRequirements([lunar, barrows]);

        Assert.InRange(total, 10m, 12m);
    }

    [Fact]
    public void UncalculableRequirementMakesEntireTileUncalculable()
    {
        var total = EhbCalculator.SumRequirements([6.67m, null]);

        Assert.Equal(0, total);
    }
}
