namespace WoW.Two.Sdk.Backend.Beta.Geo.IpLocation;

/// <summary>Holds options for reading a MaxMind DB file.</summary>
public sealed record MmdbIpLocationOptions
{
    /// <summary>Gets or sets the MMDB file path, such as a DB-IP Lite, GeoLite2 or GeoIP2 database.</summary>
    public string DatabasePath { get; set; } = string.Empty;

    /// <summary>Gets or sets how often a lookup checks the file for a newer write time. Defaults to one minute.</summary>
    public TimeSpan ReloadCheckInterval { get; set; } = TimeSpan.FromMinutes(1);
}
