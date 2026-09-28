namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Maps RGB pixels to their average color and their most common distinct colors.</summary>
internal static class ImagePaletteMapper
{
    /// <param name="pixels">Opaque pixels as red, green, blue.</param>
    /// <param name="count">The most colors returned.</param>
    public static (string Average, IReadOnlyList<ImageColorShare> Dominant) Map(ReadOnlySpan<(byte Red, byte Green, byte Blue)> pixels, int count = 5)
    {
        long red = 0, green = 0, blue = 0;
        var buckets = new Dictionary<int, (long Red, long Green, long Blue, int Count)>();
        foreach (var pixel in pixels)
        {
            red += pixel.Red;
            green += pixel.Green;
            blue += pixel.Blue;
            var key = ((pixel.Red >> 4) << 8) | ((pixel.Green >> 4) << 4) | (pixel.Blue >> 4);
            var bucket = buckets.GetValueOrDefault(key);
            buckets[key] = (bucket.Red + pixel.Red, bucket.Green + pixel.Green, bucket.Blue + pixel.Blue, bucket.Count + 1);
        }

        var total = Math.Max(1, pixels.Length);
        var average = ImageColorMapper.ToHex((byte)(red / total), (byte)(green / total), (byte)(blue / total));
        var dominant = new List<(byte Red, byte Green, byte Blue, int Count)>();
        foreach (var bucket in buckets.Values.OrderByDescending(bucket => bucket.Count))
        {
            var color = ((byte)(bucket.Red / bucket.Count), (byte)(bucket.Green / bucket.Count), (byte)(bucket.Blue / bucket.Count));
            var near = dominant.FindIndex(chosen => Distance(chosen, color) < 40);
            if (near >= 0)
            {
                dominant[near] = dominant[near] with { Count = dominant[near].Count + bucket.Count };
                continue;
            }

            if (dominant.Count < count)
                dominant.Add((color.Item1, color.Item2, color.Item3, bucket.Count));
        }

        return (average, [.. dominant
            .OrderByDescending(chosen => chosen.Count)
            .Select(chosen => new ImageColorShare { Color = ImageColorMapper.ToHex(chosen.Red, chosen.Green, chosen.Blue), Share = chosen.Count / (float)total })]);
    }

    private static double Distance((byte Red, byte Green, byte Blue, int Count) chosen, (byte Red, byte Green, byte Blue) color)
    {
        var dr = chosen.Red - color.Red;
        var dg = chosen.Green - color.Green;
        var db = chosen.Blue - color.Blue;
        return Math.Sqrt((dr * dr) + (dg * dg) + (db * db));
    }
}
