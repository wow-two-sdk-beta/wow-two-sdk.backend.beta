using WoW.Two.Sdk.Backend.Beta.Web.RequestContext.Models;

namespace WoW.Two.Sdk.Backend.Beta.Web.RequestContext.Mappers;

/// <summary>Maps a supplied User-Agent string to a heuristic device family.</summary>
public static class UserAgentDeviceMapper
{
    /// <summary>Classifies bot markers before device markers; absent or unmatched input is unknown.</summary>
    public static UserAgentDevice Map(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return UserAgentDevice.Unknown;
        if (Contains(userAgent, "bot", "crawler", "spider")) return UserAgentDevice.Bot;
        if (Contains(userAgent, "iphone", "ipad", "ipod")) return UserAgentDevice.Ios;
        if (Contains(userAgent, "android")) return UserAgentDevice.Android;
        if (Contains(userAgent, "windows", "macintosh", "x11", "linux")) return UserAgentDevice.Desktop;
        return UserAgentDevice.Unknown;
    }

    private static bool Contains(string value, params string[] markers) => markers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
