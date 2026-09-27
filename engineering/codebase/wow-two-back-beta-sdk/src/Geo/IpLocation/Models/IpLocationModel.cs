using WoW.Two.Sdk.Backend.Beta.Geo.Coordinates;

namespace WoW.Two.Sdk.Backend.Beta.Geo.IpLocation.Models;

/// <summary>Represents where an IP address is registered, at the precision its database provides.</summary>
/// <remarks>Country-level databases fill the country and continent only; city-level databases add the rest.</remarks>
public sealed record IpLocationModel
{
    /// <summary>Gets the upper-case ISO 3166-1 alpha-2 country code.</summary>
    public required string CountryCode { get; init; }

    /// <summary>Gets the English country name, or <see langword="null"/> when the database has none.</summary>
    public string? CountryName { get; init; }

    /// <summary>Gets the two-letter continent code, or <see langword="null"/> when the database has none.</summary>
    public string? ContinentCode { get; init; }

    /// <summary>Gets the English name of the first-level subdivision, such as a state, in city-level databases.</summary>
    public string? Subdivision { get; init; }

    /// <summary>Gets the English city name in city-level databases.</summary>
    public string? City { get; init; }

    /// <summary>Gets the approximate coordinate in city-level databases.</summary>
    public GeoCoordinate? Coordinate { get; init; }
}
