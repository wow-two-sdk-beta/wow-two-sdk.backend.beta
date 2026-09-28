using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Maps RGB pixels to a BlurHash string (the Wolt algorithm) that clients decode into a blurred placeholder.</summary>
internal static class BlurHashMapper
{
    private const string Characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz#$%*+,-.:;=?@[]^_{|}~";

    /// <param name="pixels">Opaque pixels, row by row, as red, green, blue.</param>
    /// <param name="width">Pixels per row.</param>
    /// <param name="height">Rows.</param>
    /// <param name="componentsX">Horizontal components, 1–9.</param>
    /// <param name="componentsY">Vertical components, 1–9.</param>
    public static string Map(ReadOnlySpan<(byte Red, byte Green, byte Blue)> pixels, int width, int height, int componentsX = 4, int componentsY = 3)
    {
        var factors = new (double Red, double Green, double Blue)[componentsX * componentsY];
        for (var y = 0; y < componentsY; y++)
        {
            for (var x = 0; x < componentsX; x++)
            {
                double normalisation = x == 0 && y == 0 ? 1 : 2;
                double red = 0, green = 0, blue = 0;
                for (var j = 0; j < height; j++)
                {
                    var vertical = Math.Cos(Math.PI * y * j / height);
                    for (var i = 0; i < width; i++)
                    {
                        var basis = normalisation * Math.Cos(Math.PI * x * i / width) * vertical;
                        var pixel = pixels[(j * width) + i];
                        red += basis * ToLinear(pixel.Red);
                        green += basis * ToLinear(pixel.Green);
                        blue += basis * ToLinear(pixel.Blue);
                    }
                }

                var scale = 1.0 / (width * height);
                factors[(y * componentsX) + x] = (red * scale, green * scale, blue * scale);
            }
        }

        var hash = new StringBuilder();
        Encode83(hash, componentsX - 1 + ((componentsY - 1) * 9), 1);

        var maximum = 1.0;
        if (factors.Length > 1)
        {
            var actual = factors.Skip(1).Max(f => Math.Max(Math.Abs(f.Red), Math.Max(Math.Abs(f.Green), Math.Abs(f.Blue))));
            var quantised = (int)Math.Max(0, Math.Min(82, Math.Floor((actual * 166) - 0.5)));
            maximum = (quantised + 1) / 166.0;
            Encode83(hash, quantised, 1);
        }
        else
        {
            Encode83(hash, 0, 1);
        }

        var dc = factors[0];
        Encode83(hash, (ToSrgb(dc.Red) << 16) + (ToSrgb(dc.Green) << 8) + ToSrgb(dc.Blue), 4);
        foreach (var factor in factors.Skip(1))
            Encode83(hash, (Quantise(factor.Red, maximum) * 19 * 19) + (Quantise(factor.Green, maximum) * 19) + Quantise(factor.Blue, maximum), 2);

        return hash.ToString();
    }

    private static int Quantise(double value, double maximum)
        => (int)Math.Max(0, Math.Min(18, Math.Floor((SignPow(value / maximum, 0.5) * 9) + 9.5)));

    private static double SignPow(double value, double exponent) => Math.CopySign(Math.Pow(Math.Abs(value), exponent), value);

    private static double ToLinear(byte value)
    {
        var v = value / 255.0;
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    private static int ToSrgb(double value)
    {
        var v = Math.Clamp(value, 0, 1);
        return v <= 0.0031308 ? (int)((v * 12.92 * 255) + 0.5) : (int)((((1.055 * Math.Pow(v, 1 / 2.4)) - 0.055) * 255) + 0.5);
    }

    private static void Encode83(StringBuilder hash, int value, int length)
    {
        for (var index = 1; index <= length; index++)
            hash.Append(Characters[(int)(value / Math.Pow(83, length - index) % 83)]);
    }
}
