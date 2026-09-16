namespace Bingo.Domain.Catalogue;

public enum ApiMappingStatus { NotConfigured, Verified, Unsupported, TemporarilyUnavailable }
public enum CataloguePriceSource { Missing, Api, Manual, Untradeable }

public static class CataloguePricing
{
    public static bool IsSuspiciousMove(long? trustedValue, long candidate)
    {
        if (candidate < 0 || trustedValue is < 0) throw new ArgumentOutOfRangeException(nameof(candidate));
        return trustedValue is { } baseline && ((decimal)candidate * 2 < baseline || candidate > (decimal)baseline * 2);
    }

    public static DateTimeOffset LastCompletedHour(DateTimeOffset actualStartedAt)
    {
        var utc = actualStartedAt.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero).AddHours(-1);
    }

    public static long? Midpoint(long? high, long? low)
    {
        if (high is < 0 || low is < 0) throw new ArgumentOutOfRangeException(nameof(high));
        return high is { } h && low is { } l
            ? checked((long)decimal.Round(((decimal)h + l) / 2, 0, MidpointRounding.AwayFromZero))
            : high ?? low;
    }
}
