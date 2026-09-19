namespace WoW.Two.Sdk.Backend.Beta.Geo.Distance;

/// <summary>Refers to units of length for expressing geographic distances.</summary>
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
