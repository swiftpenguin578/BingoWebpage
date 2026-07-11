using System.Globalization;
using System.Text;

namespace Bingo.Web.Events;

public static class EventSlugGenerator
{
    public static string Generate(string eventName)
    {
        var expanded = eventName.Trim().ToLowerInvariant()
            .Replace("æ", "ae", StringComparison.Ordinal)
            .Replace("ø", "o", StringComparison.Ordinal)
            .Replace("å", "a", StringComparison.Ordinal);
        var normalized = expanded.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(normalized.Length);
        var separatorPending = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (separatorPending && result.Length > 0) result.Append('-');
                result.Append(character);
                separatorPending = false;
            }
            else
            {
                separatorPending = true;
            }
        }

        return result.Length == 0 ? "event" : result.ToString();
    }
}
