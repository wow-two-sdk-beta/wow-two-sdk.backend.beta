using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Web.Quotas;

/// <summary>Maps a moment to its quota window: a stable key and the moment the window ends, in UTC.</summary>
internal static class QuotaPeriodMapper
{
    /// <summary>The window key, such as <c>d2026-09-28</c>, and its end; a total window never ends.</summary>
    public static (string Key, DateTimeOffset? EndsAt) Window(QuotaPeriod period, DateTimeOffset now)
    {
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        return period switch
        {
            QuotaPeriod.Day => ("d" + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), day.AddDays(1)),
            QuotaPeriod.Week => Week(day),
            QuotaPeriod.Month => ("m" + day.ToString("yyyy-MM", CultureInfo.InvariantCulture), new DateTimeOffset(day.Year, day.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1)),
            QuotaPeriod.Year => ("y" + day.Year.ToString(CultureInfo.InvariantCulture), new DateTimeOffset(day.Year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            _ => ("total", null),
        };
    }

    private static (string Key, DateTimeOffset? EndsAt) Week(DateTimeOffset day)
    {
        var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        var key = $"w{ISOWeek.GetYear(day.UtcDateTime).ToString(CultureInfo.InvariantCulture)}-{ISOWeek.GetWeekOfYear(day.UtcDateTime).ToString("00", CultureInfo.InvariantCulture)}";
        return (key, monday.AddDays(7));
    }
}
