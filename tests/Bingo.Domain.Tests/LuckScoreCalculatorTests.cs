using Bingo.Domain.Events;

namespace Bingo.Domain.Tests;

public sealed class LuckScoreCalculatorTests
{
    [Theory]
    [InlineData(0, 6.73978704090637)]
    [InlineData(1, 27.0130664599527)]
    [InlineData(2, 54.107118200989)]
    [InlineData(3, 76.7080504724828)]
    [InlineData(4, 90.2595694624704)]
    [InlineData(5, 96.5679561426864)]
    [InlineData(6, 98.9606824599232)]
    public void MatchesApprovedTwoExpectedDropsTable(int received, double expectedScore)
    {
        AssertScore(expectedScore, LuckScoreCalculator.Calculate([new(502, 1m / 251)], received));
    }

    [Theory]
    [InlineData(0, 49.800796812749)]
    [InlineData(1, 99.800796812749)]
    public void FirstKillUsesSameFormulaWithInterpolatedNeutralBaseline(int received, double expectedScore)
    {
        AssertScore(expectedScore, LuckScoreCalculator.Calculate([new(1, 1m / 251)], received));
    }

    [Theory]
    [InlineData(0, 0.439453125)]
    [InlineData(1, 5.126953125)]
    [InlineData(2, 23.92578125)]
    [InlineData(3, 58.69140625)]
    [InlineData(4, 88.134765625)]
    [InlineData(5, 98.681640625)]
    public void IndependentHeterogeneousComponentsMatchExactRationalConvolution(int received, double expectedScore)
    {
        // Independently expanded polynomial: (.75 + .25z)^2 (.25 + .75z)^3.
        AssertScore(expectedScore, LuckScoreCalculator.Calculate([new(2, 0.25m), new(3, 0.75m)], received));
    }

    [Fact]
    public void FullSmallDistributionMatchesIndependentBernoulliEnumeration()
    {
        LuckBinomialComponent[] components = [new(3, 0.07m), new(5, 0.4m), new(2, 0.91m)];
        var mass = EnumerateTrials(components);
        var ranks = Enumerable.Range(0, mass.Length).Select(k => mass.Take(k).Sum() + mass[k] / 2).ToArray();

        for (var received = 0; received < mass.Length; received++)
        {
            var exactScore = 100 * ranks[received];
            AssertScore((double)exactScore, LuckScoreCalculator.Calculate(components, received));
        }
    }

    [Fact]
    public void EqualProbabilitiesPoolTrialsAndComponentOrderDoesNotChangeScore()
    {
        for (var received = 0; received < 8; received++)
        {
            var merged = LuckScoreCalculator.Calculate([new(502, 1m / 251), new(80, 0.03m)], received);
            var split = LuckScoreCalculator.Calculate([new(200, 1m / 251), new(80, 0.03m), new(302, 1m / 251)], received);
            AssertScore((double)merged!.Value, split);
        }
    }

    [Fact]
    public void SameExpectedCountDoesNotEraseDifferentDistributions()
    {
        var dense = LuckScoreCalculator.Calculate([new(4, 0.5m)], 0);
        var rare = LuckScoreCalculator.Calculate([new(100, 0.02m)], 0);
        Assert.NotEqual(dense, rare);
    }

    [Fact]
    public void IntegerExpectationIsNeutralAndScoresAreMonotonicAndBounded()
    {
        LuckBinomialComponent[] components = [new(10, 0.3m), new(20, 0.1m)];
        Assert.InRange(LuckScoreCalculator.Calculate(components, 5)!.Value, 0, 100);
        decimal previous = -100;
        for (var received = 0; received <= 30; received++)
        {
            var score = LuckScoreCalculator.Calculate(components, received);
            Assert.NotNull(score);
            Assert.InRange(score.Value, previous, 100);
            previous = score.Value;
        }
    }

    [Fact]
    public void ComplementaryDistributionsReverseScores()
    {
        for (var received = 0; received <= 40; received++)
        {
            var forward = LuckScoreCalculator.Calculate([new(40, 0.15m)], received);
            var reverse = LuckScoreCalculator.Calculate([new(40, 0.85m)], 40 - received);
            AssertScore(100 - (double)forward!.Value, reverse);
        }
    }

    [Fact]
    public void DeterministicComponentsShiftSupportWithoutChangingScore()
    {
        for (var received = 0; received <= 5; received++)
        {
            var score = LuckScoreCalculator.Calculate([new(502, 1m / 251)], received);
            var shifted = LuckScoreCalculator.Calculate([new(100, 1), new(900, 0), new(0, 0.2m), new(502, 1m / 251)], received + 100);
            AssertScore((double)score!.Value, shifted);
        }
        Assert.Equal(50m, LuckScoreCalculator.Calculate([], 0));
        Assert.Equal(50m, LuckScoreCalculator.Calculate([new(0, 0.5m), new(400, 0)], 0));
        Assert.Equal(50m, LuckScoreCalculator.Calculate([new(20, 1)], 20));
    }

    [Fact]
    public void ObservationsOutsideTheoreticalSupportAreUnavailable()
    {
        Assert.Null(LuckScoreCalculator.Calculate([], 1));
        Assert.Null(LuckScoreCalculator.Calculate([new(100, 0)], 1));
        Assert.Null(LuckScoreCalculator.Calculate([new(10, 1), new(3, 0.2m)], 9));
        Assert.Null(LuckScoreCalculator.Calculate([new(10, 1), new(3, 0.2m)], 14));
        Assert.Null(LuckScoreCalculator.Calculate([new(4, 0.25m)], 5));
    }

    [Fact]
    public void BillionTrialRareDistributionRetainsBinomialCorrection()
    {
        // The bounded recurrence remains usable at a billion rare trials.
        Assert.InRange(LuckScoreCalculator.Calculate([new(1_000_000_000, 0.000000001m)], 0)!.Value, 0, 100);
        Assert.InRange(LuckScoreCalculator.Calculate([new(1_000_000_000, 0.000000001m)], 1)!.Value, 0, 100);
    }

    [Fact]
    public void LargeModeDoesNotUnderflowAndThirtyMillionMixedTrialsRemainSupported()
    {
        Assert.InRange(LuckScoreCalculator.Calculate([new(1_000_000_000, 0.004m)], 4_000_000)!.Value, 0, 100);
        var mixed = LuckScoreCalculator.Calculate([new(10_000_000, 0.004m), new(20_000_000, 0.002m)], 80_001);
        Assert.NotNull(mixed);
        Assert.InRange(mixed.Value, 0, 100);
    }

    [Fact]
    public void HundredsOfDifferentPersonalRateComponentsRemainAvailable()
    {
        // 400 player/source opportunities, each with 2,000 KC and a different rate.
        var components = Enumerable.Range(200, 400).Select(denominator => new LuckBinomialComponent(2_000, 1m / denominator)).ToArray();
        var expected = components.Sum(x => x.Trials * x.Probability);
        var score = LuckScoreCalculator.Calculate(components, (long)decimal.Ceiling(expected));
        Assert.NotNull(score);
        Assert.InRange(score.Value, 0, 100);
    }

    [Fact]
    public void ExcessiveWorkReturnsUnavailableWithoutSubstitutingAnotherDistribution()
    {
        Assert.Null(LuckScoreCalculator.Calculate([new(100_000_000, 0.004m), new(200_000_000, 0.002m)], 800_000));
        Assert.Null(LuckScoreCalculator.Calculate([new(1_000_000_000, 0.5m)], 500_000_000));
        Assert.Null(LuckScoreCalculator.Calculate([new(long.MaxValue, 0.01m)], 0));
    }

    [Fact]
    public void InvalidInputsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => LuckScoreCalculator.Calculate(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => LuckScoreCalculator.Calculate([new(-1, 0.5m)], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => LuckScoreCalculator.Calculate([new(1, -0.01m)], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => LuckScoreCalculator.Calculate([new(1, 1.01m)], 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => LuckScoreCalculator.Calculate([new(1, 0.5m)], -1));
    }

    private static decimal[] EnumerateTrials(IEnumerable<LuckBinomialComponent> components)
    {
        decimal[] mass = [1];
        foreach (var component in components)
            for (long trial = 0; trial < component.Trials; trial++)
            {
                var next = new decimal[mass.Length + 1];
                for (var i = 0; i < mass.Length; i++)
                {
                    next[i] += mass[i] * (1 - component.Probability);
                    next[i + 1] += mass[i] * component.Probability;
                }
                mass = next;
            }
        return mass;
    }

    private static void AssertScore(double expected, decimal? actual, double tolerance = 1e-8)
    {
        Assert.NotNull(actual);
        Assert.InRange(Math.Abs((double)actual.Value - expected), 0, tolerance);
    }
}
