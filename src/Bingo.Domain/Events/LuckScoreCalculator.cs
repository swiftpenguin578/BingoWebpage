namespace Bingo.Domain.Events;

/// <summary>Independent repetitions of one Bernoulli opportunity.</summary>
public readonly record struct LuckBinomialComponent(long Trials, decimal Probability);

/// <summary>Scores the mid-rank of observed drops relative to the interpolated expected mid-rank.</summary>
public static class LuckScoreCalculator
{
    // Counts must remain exactly representable in the double-precision PMF recurrence.
    private const long MaximumTrials = 9_007_199_254_740_991;
    private const int MaximumSupport = 131_072;
    private const long MaximumOperations = 20_000_000;
    private const double OmittedProbabilityBound = 1e-13;

    /// <summary>
    /// Combines independent binomial distributions, never their scores. Returns null for
    /// impossible observations or distributions exceeding the numerical work limits.
    /// Empty and deterministic distributions score zero at their sole possible outcome.
    /// </summary>
    public static decimal? Calculate(IReadOnlyCollection<LuckBinomialComponent> components, long received)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentOutOfRangeException.ThrowIfNegative(received);
        var groups = new Dictionary<decimal, long>();
        long totalTrials = 0, minimum = 0, maximum = 0;
        decimal expected = 0;
        foreach (var component in components)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(component.Trials);
            if (component.Probability is < 0 or > 1)
                throw new ArgumentOutOfRangeException(nameof(components), "Probabilities must be between zero and one.");
            if (component.Trials > MaximumTrials - totalTrials) return null;
            totalTrials += component.Trials;
            if (component.Trials == 0 || component.Probability == 0) continue;
            maximum += component.Trials;
            expected += component.Trials * component.Probability;
            if (component.Probability == 1)
            {
                minimum += component.Trials;
                continue;
            }

            groups[component.Probability] = groups.GetValueOrDefault(component.Probability) + component.Trials;
        }

        if (received < minimum || received > maximum) return null;
        if (groups.Count == 0) return 0m;

        // The union bound limits the combined omitted mass. Starting at the mode avoids
        // underflow at P(X=0), even with billions of trials. No distribution is substituted.
        var tailBound = OmittedProbabilityBound / (4 * groups.Count);
        var operationsRemaining = MaximumOperations;
        var distributions = new List<Distribution>(groups.Count);
        foreach (var (probability, trials) in groups)
        {
            var distribution = Binomial(trials, probability, tailBound, ref operationsRemaining);
            if (distribution is null) return null;
            distributions.Add(distribution);
        }

        // Small supports first reduce the work of the sequential convolution.
        distributions.Sort((left, right) => left.Mass.Length.CompareTo(right.Mass.Length));
        var pooled = distributions[0];
        for (var index = 1; index < distributions.Count; index++)
        {
            var combined = Convolve(pooled, distributions[index], tailBound, ref operationsRemaining);
            if (combined is null) return null;
            pooled = combined;
        }

        var lowerExpected = (long)decimal.Floor(expected);
        var fraction = (double)(expected - lowerExpected);
        var baseline = MidRank(pooled, lowerExpected - minimum);
        if (fraction > 0)
            baseline += fraction * (MidRank(pooled, lowerExpected + 1 - minimum) - baseline);
        var rank = MidRank(pooled, received - minimum);
        var difference = rank - baseline;
        var score = difference == 0 ? 0 : 100 * difference / (difference < 0 ? baseline : 1 - baseline);
        return (decimal)Math.Clamp(score, -100, 100);
    }

    private static Distribution? Binomial(long trials, decimal probability, double tailBound, ref long operationsRemaining)
    {
        var p = (double)probability;
        var complement = (double)(1 - probability);
        var mode = Math.Min(trials, (long)Math.Floor((trials + 1) * p));
        var masses = new List<double> { 1 };
        var first = mode;
        var mass = 1.0;
        for (var count = mode; count > 0; count--)
        {
            var ratio = count / (double)(trials - count + 1) * complement / p;
            // Successive ratios decrease away from the mode, so the entire remaining
            // tail is bounded by a geometric series. Relative normalization is >= 1.
            if (ratio < 1 && mass * ratio / (1 - ratio) <= tailBound) break;
            if (masses.Count >= MaximumSupport || --operationsRemaining < 0) return null;
            mass *= ratio;
            masses.Add(mass);
            first = count - 1;
        }

        masses.Reverse();
        mass = 1;
        for (var count = mode; count < trials; count++)
        {
            var ratio = (trials - count) / (double)(count + 1) * p / complement;
            if (ratio < 1 && mass * ratio / (1 - ratio) <= tailBound) break;
            if (masses.Count >= MaximumSupport || --operationsRemaining < 0) return null;
            mass *= ratio;
            masses.Add(mass);
        }

        var values = masses.ToArray();
        Normalize(values);
        return new(first, values);
    }

    private static Distribution? Convolve(Distribution left, Distribution right, double tailBound, ref long operationsRemaining)
    {
        var length = left.Mass.Length + right.Mass.Length - 1;
        var operations = (long)left.Mass.Length * right.Mass.Length;
        if (length > MaximumSupport || operations > operationsRemaining) return null;
        operationsRemaining -= operations;
        var values = new double[length];
        for (var i = 0; i < left.Mass.Length; i++)
            for (var j = 0; j < right.Mass.Length; j++)
                values[i + j] += left.Mass[i] * right.Mass[j];
        Normalize(values);
        // Reclaim only explicitly bounded tail mass after each convolution; otherwise
        // hundreds of individually narrow supports needlessly grow in total width.
        var first = 0;
        var last = values.Length - 1;
        double discarded = 0;
        while (first < last && discarded + values[first] <= tailBound) discarded += values[first++];
        discarded = 0;
        while (last > first && discarded + values[last] <= tailBound) discarded += values[last--];
        var retained = values[first..(last + 1)];
        Normalize(retained);
        return new(left.First + right.First + first, retained);
    }

    private static double MidRank(Distribution distribution, long count)
    {
        var index = count - distribution.First;
        if (index < 0) return 0;
        if (index >= distribution.Mass.Length) return 1;
        double sum = 0, correction = 0;
        for (var i = 0; i < index; i++) Add(distribution.Mass[i], ref sum, ref correction);
        return Math.Clamp(sum + 0.5 * distribution.Mass[index], 0, 1);
    }

    private static void Normalize(double[] values)
    {
        double sum = 0, correction = 0;
        foreach (var value in values) Add(value, ref sum, ref correction);
        for (var index = 0; index < values.Length; index++) values[index] /= sum;
    }

    private static void Add(double value, ref double sum, ref double correction)
    {
        var adjusted = value - correction;
        var next = sum + adjusted;
        correction = (next - sum) - adjusted;
        sum = next;
    }

    private sealed record Distribution(long First, double[] Mass);
}
