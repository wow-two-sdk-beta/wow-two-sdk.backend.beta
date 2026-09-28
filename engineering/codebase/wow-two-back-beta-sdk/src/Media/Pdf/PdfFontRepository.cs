using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using PdfSharp.Fonts;
using SkiaSharp;

namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>
/// Reads font files for PDFsharp: configured files by family first, then the platform's families through Skia's font
/// manager, so stamps render on Linux and macOS without hard-coded font paths. Collection faces (<c>.ttc</c>) are
/// extracted, since PDFsharp embeds single fonts; bold is never simulated, so a family without a bold face renders
/// regular. Installed once as PDFsharp's fallback resolver, so an application's own resolver still wins.
/// </summary>
internal sealed class PdfFontRepository : IFontResolver
{
    private static readonly Lock Gate = new();
    private static PdfFontRepository? s_installed;
    private readonly ConcurrentDictionary<string, Face?> _faces = new(StringComparer.OrdinalIgnoreCase);
    private IOptionsMonitor<PdfOptions> _options;

    private PdfFontRepository(IOptionsMonitor<PdfOptions> options) => _options = options;

    /// <summary>Installs the one repository as PDFsharp's fallback resolver; later calls only refresh its options.</summary>
    public static void Install(IOptionsMonitor<PdfOptions> options)
    {
        lock (Gate)
        {
            if (s_installed is not null)
            {
                s_installed._options = options;
                return;
            }

            s_installed = new PdfFontRepository(options);
            GlobalFontSettings.FallbackFontResolver = s_installed;
        }
    }

    /// <inheritdoc />
    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        var name = $"{familyName}|{(bold ? "b" : string.Empty)}{(italic ? "i" : string.Empty)}";
        return _faces.GetOrAdd(name, _ => Load(familyName, bold, italic)) is { } face
            ? new FontResolverInfo(name, false, face.SimulateItalic)
            : null;
    }

    /// <inheritdoc />
    public byte[]? GetFont(string faceName) => _faces.TryGetValue(faceName, out var face) ? face?.Bytes : null;

    private Face? Load(string family, bool bold, bool italic)
    {
        if (_options.CurrentValue.Fonts.TryGetValue(family, out var path) && File.Exists(path))
            return new Face { Bytes = Standalone(File.ReadAllBytes(path), 0), SimulateItalic = italic };

        var style = new SKFontStyle(
            bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        using var typeface = SKTypeface.FromFamilyName(family, style) ?? SKTypeface.FromFamilyName(null, style);
        if (typeface is null)
            return null;

        using var stream = typeface.OpenStream(out var collection);
        if (stream is null || stream.Length <= 0)
            return null;

        var bytes = new byte[stream.Length];
        stream.Read(bytes, bytes.Length);
        return new Face { Bytes = Standalone(bytes, collection), SimulateItalic = italic && !typeface.IsItalic };
    }

    /// <summary>A collection's face as a standalone font, since PDFsharp embeds single fonts only.</summary>
    private static byte[] Standalone(byte[] font, int collection)
        => TrueTypeCollectionMapper.IsCollection(font) ? TrueTypeCollectionMapper.Extract(font, collection) : font;

    private sealed record Face
    {
        public required byte[] Bytes { get; init; }

        public required bool SimulateItalic { get; init; }
    }
}
