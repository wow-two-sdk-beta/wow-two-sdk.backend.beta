using System.Numerics;

namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Compares perceptual hashes from <see cref="ImageAnalysisResult.PerceptualHash"/>.</summary>
public static class PerceptualHashExtensions
{
    /// <summary>The number of differing bits: 0 is the same picture, up to about 10 a near-duplicate, 64 unrelated.</summary>
    /// <param name="hash">One hash.</param>
    /// <param name="other">The other hash.</param>
    public static int Distance(this ulong hash, ulong other) => BitOperations.PopCount(hash ^ other);

    /// <summary>Whether two hashes are within <paramref name="maxDistance"/> bits of each other. Default 10.</summary>
    /// <param name="hash">One hash.</param>
    /// <param name="other">The other hash.</param>
    /// <param name="maxDistance">The largest distance still counted as the same picture.</param>
    public static bool IsNearDuplicateOf(this ulong hash, ulong other, int maxDistance = 10) => hash.Distance(other) <= maxDistance;
}
