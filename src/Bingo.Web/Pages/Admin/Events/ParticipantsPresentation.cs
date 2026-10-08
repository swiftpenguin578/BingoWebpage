using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;

namespace Bingo.Web.Pages.Admin.Events;

/// <summary>U5: small rendering helpers for the Participants page (no page copies of shared behaviour).</summary>
public static partial class ParticipantsPresentation
{
    /// <summary>Fills a translated "{0} … {1}" template with HTML parts, encoding the words.</summary>
    public static IHtmlContent Fill(string template, params IHtmlContent[] values)
    {
        var builder = new HtmlContentBuilder();
        foreach (var part in Placeholder().Split(template))
        {
            if (part.Length > 2 && part[0] == '{' && part[^1] == '}' && int.TryParse(part[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < values.Length) builder.AppendHtml(values[index]);
            else if (part.Length > 0) builder.Append(part);
        }
        return builder;
    }

    public static IHtmlContent Number(int value) => new HtmlString($"<b class=\"tnum\">{value.ToString("N0", CultureInfo.CurrentCulture)}</b>");
    public static readonly IHtmlContent PendingNumber = new HtmlString("<span class=\"tab-count\" data-pending-count><span class=\"sk\" aria-hidden=\"true\"></span></span>");

    /// <summary>EHB display formatting only (one decimal, grouped); stored values are never rounded.</summary>
    public static string Ehb(decimal? value) => value is { } ehb ? ehb.ToString("#,##0.#", CultureInfo.CurrentCulture) : "—";

    [GeneratedRegex(@"(\{\d+\})")]
    private static partial Regex Placeholder();
}
