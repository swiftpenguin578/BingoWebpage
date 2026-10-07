using System.Globalization;
using Bingo.Web.UI;

namespace Bingo.Web.Pages.Admin.Accounts;

public static class AccountsPresentation
{
    /// <summary>
    /// Directory "Last login": Today/Yesterday compare calendar dates in the display timezone
    /// (RC11 note: never "under 48 hours"), so DST days of 23 or 25 hours stay correct.
    /// </summary>
    public static string RelativeLogin(DateTimeOffset? value, DateTimeOffset now, string never, string todayFormat, string yesterdayFormat, IFormatProvider? provider = null)
    {
        if (value is null) return never;
        provider ??= CultureInfo.CurrentCulture;
        var local = DateTimePresentation.ToTimezone(value.Value);
        var today = DateTimePresentation.ToTimezone(now).Date;
        var time = local.ToString("HH:mm", provider);
        if (local.Date == today) return string.Format(provider, todayFormat, time);
        if (local.Date == today.AddDays(-1)) return string.Format(provider, yesterdayFormat, time);
        return local.ToString("d MMM yyyy, HH:mm", provider);
    }

    public static string FullTime(DateTimeOffset value, IFormatProvider? provider = null) =>
        DateTimePresentation.Format(value, "d MMM yyyy, HH:mm", provider: provider ?? CultureInfo.CurrentCulture);

    public static string Day(DateTimeOffset value, string? timezone, IFormatProvider? provider = null) =>
        DateTimePresentation.Format(value, "d MMM yyyy", timezone, provider ?? CultureInfo.CurrentCulture);

    public static string Initials(string username)
    {
        var words = new string(username.Select(character => char.IsAsciiLetterOrDigit(character) ? character : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpperInvariant(word[0]));
        var initials = string.Concat(words);
        return initials.Length > 0 ? initials : "?";
    }
}

public sealed record AccountChangeRow(string Who, string Before, string After);
