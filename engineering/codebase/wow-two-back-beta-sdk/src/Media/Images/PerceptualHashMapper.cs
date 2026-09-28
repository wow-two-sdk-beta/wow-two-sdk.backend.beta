namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Maps a 9×8 grayscale thumbnail to a 64-bit difference hash: bit set where a pixel is brighter than its right neighbour.</summary>
internal static class PerceptualHashMapper
{
    /// <param name="gray">Luminance values of a 9-column, 8-row thumbnail, row by row.</param>
    public static ulong Map(ReadOnlySpan<byte> gray)
    {
        if (gray.Length != 72)
            throw new ArgumentException("The difference hash needs a 9×8 thumbnail.", nameof(gray));

        ulong hash = 0;
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                hash <<= 1;
                if (gray[(y * 9) + x] > gray[(y * 9) + x + 1])
                    hash |= 1;
            }
        }

        return hash;
    }
}
