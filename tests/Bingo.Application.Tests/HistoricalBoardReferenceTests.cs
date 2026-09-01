using Bingo.Application.Boards;

namespace Bingo.Application.Tests;

public sealed class HistoricalBoardReferenceTests
{
    public static TheoryData<string, int, decimal, decimal, decimal> RemainingHistoricalTiles => new()
    {
        // Name, target contributions, aggregate expected attempts per contribution, efficient completions/hour, napkin board target.
        { "Nex uniques excluding pet", 3, 215m, 16m, 35m },
        { "GWD ten distinct eligible drops from the approved 20-item pool", 10, 65m, 24m, 28m },
        { "Complete Voidwaker", 1, 21m, 1m, 21m },
        { "Araxxor Nid or jar", 1, 1000m, 30m, 21m },
        { "Three Phosani uniques excluding pet", 3, 135m, 8m, 49m },
        { "Four Yama armour or horn drops", 4, 120m, 14m, 35m },
        { "Two Dragon hunter wands", 2, 315m, 16m, 39m },
        { "Vorkath necklace visage or pet", 1, 537m, 27m, 21m },
        { "Three Sarachnis cudgels", 3, 384m, 54m, 21m },
        { "Two Granite hammers", 2, 375m, 27m, 28m },
        { "Five Maggot King Crimson kisten or Elder venator fang drops", 5, 200m, 28m, 35m },
        { "Hydra claw pet or jar", 1, 545m, 23m, 21m },
        { "Five Zulrah uniques excluding pet and mutagens", 5, 128m, 42m, 14m },
        { "Three Chromium ingots", 3, 256m, 27m, 28m },
        { "Two Primordial crystals", 2, 520m, 43m, 21m }
    };

    [Theory]
    [MemberData(nameof(RemainingHistoricalTiles))]
    public void AggregateHistoricalTileRemainsInNapkinSanityRange(
        string name,
        int target,
        decimal attemptsPerContribution,
        decimal completionsPerHour,
        decimal napkinTarget)
    {
        var result = EhbCalculator.CalculateDropRequirement(target,
            [new EligibleDropRate(completionsPerHour, 1m / attemptsPerContribution, Guid.NewGuid(), Guid.NewGuid())]);

        Assert.True(result is > 0, $"{name} did not produce a positive estimate.");
        Assert.InRange(result.Value, napkinTarget * 0.35m, napkinTarget * 1.70m);
    }

    [Fact]
    public void SuperiorSlayerUsesItsHistoricalManualEstimate()
    {
        var result = EhbCalculator.SumRequirements([], manualOverride: 21m);

        Assert.Equal(21m, result);
    }

    [Fact]
    public void FullFortisRunUsesCumulativeSunfireChance()
    {
        var boss = Guid.NewGuid();
        var result = EhbCalculator.CalculateDropRequirement(6,
            Enumerable.Repeat(new EligibleDropRate(2m, 3m / 80m, Guid.NewGuid(), boss), 3));

        Assert.InRange(result!.Value, 26.66m, 26.68m);
    }

    [Fact]
    public void RoyalTitansThreePiecesFromEachLootChoiceIsNearHistoricalTarget()
    {
        var firePieces = EhbCalculator.CalculateDropRequirement(3,
            [new EligibleDropRate(44m, 1m / 150m, Guid.NewGuid(), Guid.NewGuid())]);
        var icePieces = EhbCalculator.CalculateDropRequirement(3,
            [new EligibleDropRate(44m, 1m / 150m, Guid.NewGuid(), Guid.NewGuid())]);

        var result = EhbCalculator.SumRequirements([firePieces, icePieces]);

        Assert.InRange(result, 19m, 23m); // Historical board target: 21 EHB.
    }

    [Fact]
    public void TwoDukeOrWhispererDropsMayComeFromTheSameSelectedBoss()
    {
        var result = EhbCalculator.CalculateDropRequirement(2,
        [
            new EligibleDropRate(30m, 1m / 720m, Guid.NewGuid(), Guid.NewGuid()),
            new EligibleDropRate(17m, 1m / 512m, Guid.NewGuid(), Guid.NewGuid())
        ]);

        Assert.InRange(result!.Value, 45m, 50m); // Historical balancing allocation: 42 EHB.
    }

    [Fact]
    public void ThreeCorpSpiritShieldsIsNearHistoricalTarget()
    {
        var result = EhbCalculator.CalculateDropRequirement(3,
            [new EligibleDropRate(10m, 8m / 512m, Guid.NewGuid(), Guid.NewGuid())]);

        Assert.InRange(result!.Value, 18m, 22m); // Historical board target: 21 EHB.
    }

    [Fact]
    public void WeightedTrioTobPurplesRemainNearHistoricalTarget()
    {
        var boss = Guid.NewGuid();
        List<(decimal Probability, int Weight)> rates =
        [
            (1m / 64.839m, 1),
            (1m / 259.35m, 1), (1m / 259.35m, 1), (1m / 259.35m, 1),
            (1m / 259.35m, 1), (1m / 259.35m, 1),
            (1m / 518.7m, 2)
        ];

        var result = EhbCalculator.CalculateDropRequirement(6, rates.Select(rate =>
            new EligibleDropRate(3m, rate.Probability, Guid.NewGuid(), boss, rate.Weight)));

        Assert.InRange(result!.Value, 48m, 58m); // Historical board target: 56 EHB.
    }

    [Fact]
    public void WeightedCoxPurplesRemainNearHistoricalTarget()
    {
        var boss = Guid.NewGuid();
        List<(decimal Probability, int Weight)> rates =
        [
            (1m / 90.69m, 1), (1m / 90.69m, 1),
            (1m / 453.47m, 1), (1m / 453.47m, 1),
            (1m / 604.62m, 1), (1m / 604.62m, 1), (1m / 604.62m, 1),
            (1m / 604.62m, 1), (1m / 604.62m, 1),
            (1m / 906.93m, 2), (1m / 906.93m, 2), (1m / 906.93m, 2)
        ];

        var result = EhbCalculator.CalculateDropRequirement(6, rates.Select(rate =>
            new EligibleDropRate(3m, rate.Probability, Guid.NewGuid(), boss, rate.Weight)));

        Assert.InRange(result!.Value, 43m, 58m); // Historical board target: 56 EHB.
    }

    [Fact]
    public void SoloRaidLevel300ToaRatesProduceAReasonableNonzeroEstimate()
    {
        var boss = Guid.NewGuid();
        List<(decimal Probability, int Weight)> rates =
        [
            (0.001726m, 2),
            (0.003453m, 1), (0.003453m, 1), (0.003453m, 1),
            (0.005179m, 1), (0.012085m, 1), (0.012085m, 1)
        ];

        var result = EhbCalculator.CalculateDropRequirement(6, rates.Select(rate =>
            new EligibleDropRate(3m, rate.Probability, Guid.NewGuid(), boss, rate.Weight)));

        Assert.InRange(result!.Value, 30m, 50m); // DKL reference target was 56 EHB.
    }

    [Fact]
    public void FourPlayerHardModeTobRatesIncludeThePersonalShareExactlyOnce()
    {
        var boss = Guid.NewGuid();
        List<(decimal Probability, int Weight)> rates =
        [
            (1m / 79.2m, 1),
            (1m / 277.2m, 1), (1m / 277.2m, 1), (1m / 277.2m, 1),
            (1m / 277.2m, 1), (1m / 277.2m, 1),
            (1m / 554.4m, 2)
        ];

        var result = EhbCalculator.CalculateDropRequirement(6, rates.Select(rate =>
            new EligibleDropRate(3m, rate.Probability, Guid.NewGuid(), boss, rate.Weight)));

        Assert.InRange(result!.Value, 45m, 65m);
    }

    [Fact]
    public void BarrowsAndMoonsAreSeparateRequiredSubtotals()
    {
        var barrows = EhbCalculator.CalculateDropRequirement(5,
            [new EligibleDropRate(14m, 1m / 19m, Guid.NewGuid(), Guid.NewGuid())]);
        var moons = EhbCalculator.CalculateDropRequirement(5,
            [new EligibleDropRate(14m, 1m / 19m, Guid.NewGuid(), Guid.NewGuid())]);

        var result = EhbCalculator.SumRequirements([barrows, moons]);

        Assert.InRange(result, 12m, 16m); // Historical board target: 14 EHB.
    }
}
