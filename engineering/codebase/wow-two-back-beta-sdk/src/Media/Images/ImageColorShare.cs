namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Represents a color and the share of the image it covers.</summary>
public sealed record ImageColorShare
{
    /// <summary>Gets the color as <c>#RRGGBB</c>.</summary>
    public required string Color { get; init; }

    /// <summary>Gets the share of pixels, 0–1.</summary>
    public required float Share { get; init; }
}
