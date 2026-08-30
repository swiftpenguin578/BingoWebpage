using System.Globalization;

namespace Bingo.Web.UI;

public static class DateTimePresentation
{
    public const string DefaultTimezoneId = "Europe/Copenhagen";

    public static DateTimeOffset ToTimezone(DateTimeOffset value, string? timezoneId = null)
        => TimeZoneInfo.ConvertTime(value, ResolveTimezone(timezoneId));

    public static string Format(DateTimeOffset value, string format, string? timezoneId = null, IFormatProvider? provider = null)
        => ToTimezone(value, timezoneId).ToString(format, provider ?? CultureInfo.CurrentCulture);

    private static TimeZoneInfo ResolveTimezone(string? timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId)) return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimezoneId);
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
