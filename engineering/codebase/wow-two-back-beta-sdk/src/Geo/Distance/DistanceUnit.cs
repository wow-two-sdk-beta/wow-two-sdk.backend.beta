namespace WoW.Two.Sdk.Backend.Beta.Geo.Distance;

/// <summary>Units of length for expressing geographic distances.</summary>
public enum DistanceUnit
{
    /// <summary>Metres (the SI base unit and the calculator's native output).</summary>
    Meters,

    /// <summary>Kilometres (1000 metres).</summary>
    Kilometers,

    /// <summary>International miles (1609.344 metres).</summary>
    Miles,

    /// <summary>Nautical miles (1852 metres).</summary>
    NauticalMiles,
}

/// <summary>Conversions between metres and the other <see cref="DistanceUnit"/> values.</summary>
public static class DistanceUnitExtensions
{
    private const double MetersPerKilometer = 1000.0;
    private const double MetersPerMile = 1609.344;
    private const double MetersPerNauticalMile = 1852.0;

    /// <summary>Converts a distance in metres to the given unit.</summary>
    /// <param name="meters">The distance in metres.</param>
    /// <param name="unit">The target unit.</param>
    /// <returns>The distance expressed in <paramref name="unit"/>.</returns>
    public static double FromMeters(double meters, DistanceUnit unit) => unit switch
    {
        DistanceUnit.Meters => meters,
        DistanceUnit.Kilometers => meters / MetersPerKilometer,
        DistanceUnit.Miles => meters / MetersPerMile,
        DistanceUnit.NauticalMiles => meters / MetersPerNauticalMile,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown distance unit."),
    };

    /// <summary>Converts a distance expressed in the given unit to metres.</summary>
    /// <param name="value">The distance in <paramref name="unit"/>.</param>
    /// <param name="unit">The unit of <paramref name="value"/>.</param>
    /// <returns>The distance in metres.</returns>
    public static double ToMeters(double value, DistanceUnit unit) => unit switch
    {
        DistanceUnit.Meters => value,
        DistanceUnit.Kilometers => value * MetersPerKilometer,
        DistanceUnit.Miles => value * MetersPerMile,
        DistanceUnit.NauticalMiles => value * MetersPerNauticalMile,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown distance unit."),
    };
}
