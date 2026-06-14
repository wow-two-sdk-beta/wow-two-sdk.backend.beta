using TimeZoneConverter;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Time;

/// <summary>
/// Cross-platform time-zone resolution.
/// </summary>
/// <remarks>
/// Wraps <see cref="TimeZoneConverter"/> so consumers can pass either Windows or IANA time-zone IDs
/// and get a <see cref="TimeZoneInfo"/> regardless of the host OS.
/// </remarks>
public static class TimeZoneHelpers
{
    /// <summary>
    /// Resolve any time-zone identifier (Windows or IANA) to a <see cref="TimeZoneInfo"/>.
    /// </summary>
    /// <exception cref="TimeZoneNotFoundException">If the id is not recognized.</exception>
    public static TimeZoneInfo ResolveTimeZone(string anyZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anyZoneId);
        return TZConvert.GetTimeZoneInfo(anyZoneId);
    }

    /// <summary>Convert an IANA time-zone id to its Windows equivalent.</summary>
    public static string IanaToWindows(string ianaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ianaId);
        return TZConvert.IanaToWindows(ianaId);
    }

    /// <summary>Convert a Windows time-zone id to its IANA equivalent.</summary>
    public static string WindowsToIana(string windowsId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(windowsId);
        return TZConvert.WindowsToIana(windowsId);
    }
}
