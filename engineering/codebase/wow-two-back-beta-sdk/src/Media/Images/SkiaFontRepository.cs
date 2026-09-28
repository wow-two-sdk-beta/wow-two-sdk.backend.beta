using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>
/// Reads typefaces: configured font files first, then the platform's families through Skia's font manager, then a
/// fallback family that has the glyphs the text needs (Cyrillic, Uzbek Latin, …).
/// </summary>
/// <param name="options">The configured fonts and default family.</param>
internal sealed class SkiaFontRepository(IOptionsMonitor<ImageOptions> options)
{
    private readonly ConcurrentDictionary<string, SKTypeface?> _files = new(StringComparer.OrdinalIgnoreCase);

    public SKTypeface Find(string? family, bool bold, bool italic, string text)
    {
        var current = options.CurrentValue;
        family ??= current.DefaultFontFamily;
        var style = new SKFontStyle(
            bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        var typeface = (family is not null ? FromFile(family, current) : null)
            ?? (family is not null ? SKTypeface.FromFamilyName(family, style) : null)
            ?? SKTypeface.FromFamilyName(null, style)
            ?? SKTypeface.Default;
        return Covering(typeface, style, text);
    }

    private SKTypeface? FromFile(string family, ImageOptions current)
        => current.Fonts.TryGetValue(family, out var path)
            ? _files.GetOrAdd(path, static file => File.Exists(file) ? SKTypeface.FromFile(file) : null)
            : null;

    /// <summary>The typeface itself when it draws every character, else a platform family that draws the first missing one.</summary>
    private static SKTypeface Covering(SKTypeface typeface, SKFontStyle style, string text)
    {
        using var font = new SKFont(typeface);
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune) || Rune.IsControl(rune) || font.ContainsGlyph(rune.Value))
                continue;

            return SKFontManager.Default.MatchCharacter(typeface.FamilyName, style, null, rune.Value) ?? typeface;
        }

        return typeface;
    }
}
