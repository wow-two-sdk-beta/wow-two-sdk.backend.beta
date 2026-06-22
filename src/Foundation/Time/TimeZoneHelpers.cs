using TimeZoneConverter;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Time;

/// <summary>Provides cross-platform time-zone resolution — accepts Windows or IANA IDs on any host OS.</summary>
public static class TimeZoneHelpers
{
    /// <summary>Resolves any time-zone identifier (Windows or IANA) to a <see cref="TimeZoneInfo"/>.</summary>
    /// <param name="anyZoneId">The Windows or IANA time-zone identifier to resolve.</param>
    /// <exception cref="TimeZoneNotFoundException">If the id is not recognized.</exception>
    public static TimeZoneInfo ResolveTimeZone(string anyZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anyZoneId);
        return TZConvert.GetTimeZoneInfo(anyZoneId);
    }

    /// <summary>Convert an IANA time-zone id to its Windows equivalent.</summary>
    /// <param name="ianaId">The IANA time-zone identifier to convert.</param>
    public static string IanaToWindows(string ianaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ianaId);
        return TZConvert.IanaToWindows(ianaId);
    }

    /// <summary>Convert a Windows time-zone id to its IANA equivalent.</summary>
    /// <param name="windowsId">The Windows time-zone identifier to convert.</param>
    public static string WindowsToIana(string windowsId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(windowsId);
        return TZConvert.WindowsToIana(windowsId);
    }
}
