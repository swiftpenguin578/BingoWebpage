using System.Globalization;
using System.Text.RegularExpressions;

namespace Bingo.Application.Catalogue;

public static partial class DropRateParser
{
    public static decimal? TryParseProbability(string? displayedRate)
    {
        if (string.IsNullOrWhiteSpace(displayedRate)) return null;
        var match = RatePattern().Match(displayedRate.Trim()); if (!match.Success) return null;
        var multiplier = ParseNumber(match.Groups["multiplier"].Value) ?? 1m; var denominator = ParseNumber(match.Groups["denominator"].Value);
        return multiplier > 0 && denominator > 0 ? multiplier / denominator.Value : null;
    }

    private static decimal? ParseNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null; var normalized = value.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (ThousandsWithComma().IsMatch(normalized)) normalized = normalized.Replace(",", string.Empty, StringComparison.Ordinal);
        else if (ThousandsWithPeriod().IsMatch(normalized)) normalized = normalized.Replace(".", string.Empty, StringComparison.Ordinal);
        else normalized = normalized.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    [GeneratedRegex(@"(?:(?<multiplier>\d+(?:[.,]\d+)?)\s*[x×*]\s*)?1\s*/\s*(?<denominator>[\d., ]+)", RegexOptions.IgnoreCase)]
    private static partial Regex RatePattern();
    [GeneratedRegex(@"^\d{1,3}(,\d{3})+$")]
    private static partial Regex ThousandsWithComma();
    [GeneratedRegex(@"^\d{1,3}(\.\d{3})+$")]
    private static partial Regex ThousandsWithPeriod();
}
