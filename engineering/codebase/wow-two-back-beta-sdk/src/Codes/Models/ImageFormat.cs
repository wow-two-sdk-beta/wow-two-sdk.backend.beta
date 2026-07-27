namespace WoW.Two.Sdk.Backend.Beta.Codes.Models;

/// <summary>Output raster/vector format for a rendered code.</summary>
/// <remarks>Vector-first: SVG is the working/design format (infinite scale, live re-style); PNG is the raster export.</remarks>
public enum ImageFormat
{
    /// <summary>Scalable vector — default. Infinite print scale, tiny, re-styleable.</summary>
    Svg,

    /// <summary>Raster PNG — for surfaces that won't accept SVG.</summary>
    Png,
}
