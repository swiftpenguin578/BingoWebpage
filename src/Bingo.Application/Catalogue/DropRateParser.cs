using System.Globalization;
using System.Text.RegularExpressions;

namespace Bingo.Application.Catalogue;

public static partial class DropRateParser
{
    public static decimal? TryParseProbability(string? displayedRate)
    {
        if (string.IsNullOrWhiteSpace(displayedRate)) return null;
        var match = RatePattern().Match(displayedRate.Trim()); if (!match.Success) return null;
        var multiplier = ParseNumber(match.Groups["multiplier"].Value);
        var numerator = ParseNumber(match.Groups["numerator"].Value);
        var denominator = ParseNumber(match.Groups["denominator"].Value);
        if (numerator is not > 0 || denominator is not > 0) return null;

        var probabilityPerRoll = numerator.Value / denominator.Value;
        if (probabilityPerRoll > 1) return null;
        if (multiplier is null) return probabilityPerRoll;
        if (multiplier <= 0 || multiplier != decimal.Truncate(multiplier.Value) || multiplier > 10_000) return null;

        var noDropProbability = 1m;
        for (var roll = 0; roll < (int)multiplier.Value; roll++)
        {
            noDropProbability *= 1 - probabilityPerRoll;
        }

        return 1 - noDropProbability;
    }

    private static decimal? ParseNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null; var normalized = value.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (ThousandsWithComma().IsMatch(normalized)) normalized = normalized.Replace(",", string.Empty, StringComparison.Ordinal);
        else if (ThousandsWithPeriod().IsMatch(normalized)) normalized = normalized.Replace(".", string.Empty, StringComparison.Ordinal);
        else normalized = normalized.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    [GeneratedRegex(@"(?:(?<multiplier>\d+(?:[.,]\d+)?)\s*[x×*]\s*)?(?<numerator>\d+(?:[.,]\d+)?)\s*/\s*(?<denominator>[\d., ]+)", RegexOptions.IgnoreCase)]
    private static partial Regex RatePattern();
    [GeneratedRegex(@"^\d{1,3}(,\d{3})+$")]
    private static partial Regex ThousandsWithComma();
    [GeneratedRegex(@"^\d{1,3}(\.\d{3})+$")]
    private static partial Regex ThousandsWithPeriod();
}
