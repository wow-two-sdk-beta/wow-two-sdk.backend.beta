namespace WoW.Two.Sdk.Backend.Beta.Geo.Geohash;

/// <summary>A cardinal direction for locating an adjacent geohash cell.</summary>
public enum GeohashDirection
{
    /// <summary>Northward (increasing latitude).</summary>
    North,

    /// <summary>Southward (decreasing latitude).</summary>
    South,

    /// <summary>Eastward (increasing longitude).</summary>
    East,

    /// <summary>Westward (decreasing longitude).</summary>
    West,
}
