namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Geo.IpLocation;

/// <summary>Builds GeoIP2-shaped records, as DB-IP Lite and GeoLite2 store them.</summary>
internal static class IpLocationTestData
{
    public static IDictionary<string, object> Country(string code, string name, string continent) => new Dictionary<string, object>
    {
        ["continent"] = new Dictionary<string, object> { ["code"] = continent },
        ["country"] = new Dictionary<string, object>
        {
            ["iso_code"] = code,
            ["names"] = new Dictionary<string, object> { ["en"] = name },
        },
    };

    public static IDictionary<string, object> City() => new Dictionary<string, object>
    {
        ["city"] = new Dictionary<string, object> { ["names"] = new Dictionary<string, object> { ["en"] = "Mountain View" } },
        ["continent"] = new Dictionary<string, object> { ["code"] = "NA" },
        ["country"] = new Dictionary<string, object> { ["iso_code"] = "US" },
        ["location"] = new Dictionary<string, object> { ["latitude"] = 37.386, ["longitude"] = -122.0838 },
        ["subdivisions"] = new List<object>
        {
            new Dictionary<string, object> { ["names"] = new Dictionary<string, object> { ["en"] = "California" } },
        },
    };
}
