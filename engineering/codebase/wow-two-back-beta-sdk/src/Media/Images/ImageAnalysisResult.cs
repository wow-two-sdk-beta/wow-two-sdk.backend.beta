namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Represents what an image looks like in summary: a placeholder, its colors and a fingerprint.</summary>
public sealed record ImageAnalysisResult
{
    /// <summary>Gets a BlurHash placeholder (4×3 components) a client decodes into a blurred preview.</summary>
    public required string BlurHash { get; init; }

    /// <summary>Gets the average color as <c>#RRGGBB</c>, for a flat placeholder.</summary>
    public required string AverageColor { get; init; }

    /// <summary>Gets the dominant colors, most common first.</summary>
    public required IReadOnlyList<ImageColorShare> DominantColors { get; init; }

    /// <summary>
    /// Gets a 64-bit difference hash; near-duplicates differ in few bits (see <see cref="PerceptualHashExtensions.Distance"/>).
    /// </summary>
    public required ulong PerceptualHash { get; init; }
}
