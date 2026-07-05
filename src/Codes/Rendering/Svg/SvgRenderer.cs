using System.Globalization;
using System.Text;
using WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Matrix;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Svg;

/// <summary>Provides the framework-agnostic SVG emitter that turns a <see cref="ModuleMatrix"/> and <see cref="StyleSpec"/> into an SVG string — the single styled render path every export and the live preview share.</summary>
/// <remarks>Run <see cref="StyleSpecNormalizer"/> over the style before calling <see cref="Emit"/>. The all-square style takes a byte-parity fast path identical to the legacy output; any other style splits the foreground into a data body (<see cref="ModuleShape"/>) and geometry-driven finder eyes (<see cref="FinderShape"/> and <see cref="FinderDotShape"/>), which keeps a stylised code scannable.</remarks>
public sealed class SvgRenderer
{
    /// <summary>The output-space size of one module, in pixels.</summary>
    private const int PixelsPerModule = 20;

    /// <summary>The side length of a finder pattern in modules (the outer frame).</summary>
    private const int FinderSize = 7;

    /// <summary>The side length of a finder pupil in modules (the solid center block), inset by 2 inside the 7×7 frame.</summary>
    private const int FinderDotSize = 3;

    /// <summary>The smallest QR symbol side (version 1 is 21×21); below this, corner regions are shaped as data rather than finders.</summary>
    private const int QrMinSize = 21;

    /// <summary>The id of the foreground gradient def, referenced by the data + eye fills when a gradient is set.</summary>
    private const string ForegroundGradientId = "sqr-fg";

    /// <summary>Center-emoji size bounds (fraction of the canvas) + the hole/halo radius factor — shared by the matrix knockout and the emoji emit so the blank hole and the glyph align.</summary>
    private const double EmojiMinRatio = 0.1;
    private const double EmojiMaxRatio = 0.27;
    private const double EmojiHoleFactor = 0.62;

    /// <summary>Emits the styled SVG for <paramref name="matrix"/> under <paramref name="style"/>. The style is consumed as-is — run <see cref="StyleSpecNormalizer"/> first.</summary>
    public string Emit(ModuleMatrix matrix, StyleSpec style)
    {
        var quietZone = style.QuietZoneModules;
        var size = matrix.Size + (2 * quietZone); // full canvas side in modules (symbol + quiet zone both sides)
        var pixels = size * PixelsPerModule;

        var sb = new StringBuilder();

        // L0 — document open: module-sized viewBox scaled to pixels; crispEdges keeps module borders sharp.
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ")
            .Append(size).Append(' ').Append(size)
            .Append("\" shape-rendering=\"crispEdges\" width=\"")
            .Append(pixels).Append("\" height=\"").Append(pixels).Append("\">\n");

        // L1 — background: a full-canvas rect, or nothing when transparent.
        if (!style.TransparentBackground)
            sb.Append("<rect x=\"0\" y=\"0\" width=\"").Append(size).Append("\" height=\"").Append(size)
                .Append("\" fill=\"").Append(style.BackgroundColor).Append("\"/>\n");

        // L1b — foreground gradient def (when set, ≥2 stops); referenced by the data + eye fills below.
        var hasGradient = style.Gradient is { Stops.Count: >= 2 };
        if (hasGradient)
            EmitForegroundGradientDefs(sb, style.Gradient!, size);

        var foregroundFill = hasGradient ? $"url(#{ForegroundGradientId})" : style.ForegroundColor;

        // A center emoji knocks its footprint out of the data matrix — a genuine blank center (not an overlay over live
        // modules), so a transparent background reads as a real hole. The auto-bumped ECC=H reconstructs the cleared modules.
        var bodyMatrix = style.Emoji is { } emoji
            ? matrix.WithCenterHole(size * Math.Clamp(emoji.SizeRatio, EmojiMinRatio, EmojiMaxRatio) * EmojiHoleFactor)
            : matrix;

        // L2 — foreground. The legacy look takes the byte-parity fast path; any styling takes the split (data body + eyes) path.
        if (IsLegacySquare(style))
            EmitLegacyForegroundPath(sb, bodyMatrix, foregroundFill, quietZone);
        else
            EmitStyledForeground(sb, bodyMatrix, style, foregroundFill, quietZone);

        // Overlay the optional center logo as an <image> (no module knockout).
        EmitLogo(sb, size, style.Logo);

        // Overlay the optional center emoji as <text> over a knockout halo (ECC=H compensates for the occlusion).
        EmitEmoji(sb, size, style.BackgroundColor, style.TransparentBackground, style.Emoji);

        // L3 — document close.
        sb.Append("</svg>");

        return sb.ToString();
    }

    /// <summary>True when the style requests the legacy all-square look (data body + both finder layers square) — the byte-parity fast path.</summary>
    private static bool IsLegacySquare(StyleSpec style) =>
        style.ModuleShape == ModuleShape.Square
        && style.FinderShape == FinderShape.Square
        && style.FinderDotShape == FinderDotShape.Square;

    #region Legacy fast path (byte-parity)

    /// <summary>Appends the single foreground <c>&lt;path&gt;</c> with dark modules merged into horizontal runs, shifted by the quiet zone.</summary>
    private static void EmitLegacyForegroundPath(StringBuilder sb, ModuleMatrix matrix, string foregroundFill, int quietZone)
    {
        sb.Append("<path fill=\"").Append(foregroundFill).Append("\" d=\"");

        for (var row = 0; row < matrix.Size; row++)
            AppendRowRuns(sb, matrix, row, quietZone);

        sb.Append("\"/>\n");
    }

    /// <summary>Merges consecutive dark modules in <paramref name="row"/> into <c>M{x} {y}h{run}v1h-{run}z</c> sub-paths (square modules), left to right.</summary>
    private static void AppendRowRuns(StringBuilder sb, ModuleMatrix matrix, int row, int quietZone)
    {
        var col = 0;
        var y = row + quietZone;

        while (col < matrix.Size)
        {
            if (!matrix[row, col])
            {
                col++;
                continue;
            }

            var start = col;
            while (col < matrix.Size && matrix[row, col])
                col++;

            var run = col - start;
            var x = start + quietZone;

            sb.Append('M').Append(x).Append(' ').Append(y)
                .Append('h').Append(run)
                .Append("v1h-").Append(run)
                .Append('z');
        }
    }

    #endregion

    #region Styled path (data body + finder eyes)

    /// <summary>Emits the foreground as a styled data body plus, on a real QR symbol, three independently styled finder eyes.</summary>
    private static void EmitStyledForeground(StringBuilder sb, ModuleMatrix matrix, StyleSpec style, string foregroundFill, int quietZone)
    {
        var finders = DetectFinders(matrix.Size);

        // L2a — data body: every dark module that is NOT inside a finder region, drawn via ModuleShape.
        var body = new StringBuilder();
        EmitDataBody(body, matrix, style.ModuleShape, finders, quietZone);
        if (body.Length > 0)
            sb.Append("<path fill=\"").Append(foregroundFill).Append("\" d=\"").Append(body).Append("\"/>\n");

        // L2b — finder eyes: outer frame (FinderShape) + inner pupil (FinderDotShape), one group each, drawn from geometry
        // (not the matrix bits) so the eyes are always complete and crisp regardless of body shape.
        if (finders.Count == 0)
            return;

        var eyes = new StringBuilder();
        foreach (var f in finders)
            AppendFinderEye(eyes, f, style.FinderShape, style.FinderDotShape, quietZone);

        sb.Append("<path fill=\"").Append(foregroundFill).Append("\" fill-rule=\"evenodd\" d=\"").Append(eyes).Append("\"/>\n");
    }

    /// <summary>The top-left corners (in matrix coordinates) of the three 7×7 finder regions, or an empty list when <paramref name="size"/> is below a real QR symbol.</summary>
    private static IReadOnlyList<(int Row, int Col)> DetectFinders(int size)
    {
        if (size < QrMinSize)
            return [];

        var last = size - FinderSize;
        return
        [
            (0, 0),       // top-left
            (0, last),    // top-right
            (last, 0),    // bottom-left
        ];
    }

    /// <summary>Appends the data-body sub-paths: all dark modules outside the finder regions, shaped per <paramref name="shape"/>.</summary>
    private static void EmitDataBody(
        StringBuilder sb,
        ModuleMatrix matrix,
        ModuleShape shape,
        IReadOnlyList<(int Row, int Col)> finders,
        int quietZone)
    {
        switch (shape)
        {
            case ModuleShape.VerticalBars:
                AppendVerticalBars(sb, matrix, finders, quietZone);
                break;
            case ModuleShape.HorizontalBars:
                AppendHorizontalBars(sb, matrix, finders, quietZone);
                break;
            default:
                AppendPerModuleBody(sb, matrix, shape, finders, quietZone);
                break;
        }
    }

    /// <summary>Per-module shapes (square / rounded / dots / classy family): emit one primitive per dark data module.</summary>
    private static void AppendPerModuleBody(
        StringBuilder sb,
        ModuleMatrix matrix,
        ModuleShape shape,
        IReadOnlyList<(int Row, int Col)> finders,
        int quietZone)
    {
        for (var row = 0; row < matrix.Size; row++)
        for (var col = 0; col < matrix.Size; col++)
        {
            if (!matrix[row, col] || IsInFinder(row, col, finders))
                continue;

            double x = col + quietZone;
            double y = row + quietZone;

            switch (shape)
            {
                case ModuleShape.Square:
                    AppendUnitSquare(sb, x, y);
                    break;
                case ModuleShape.Dots:
                    AppendCircle(sb, x + 0.5, y + 0.5, 0.5);
                    break;
                case ModuleShape.Rounded:
                    AppendRoundedRect(sb, x, y, 1, 1, 0.35);
                    break;
                case ModuleShape.Classy:
                    AppendConnectedRounded(sb, matrix, row, col, x, y, 0.35);
                    break;
                case ModuleShape.ClassyRounded:
                    AppendConnectedRounded(sb, matrix, row, col, x, y, 0.5);
                    break;
                default:
                    AppendUnitSquare(sb, x, y);
                    break;
            }
        }
    }

    /// <summary>Merges each column's contiguous dark data runs into a single rounded vertical bar (pill).</summary>
    private static void AppendVerticalBars(
        StringBuilder sb,
        ModuleMatrix matrix,
        IReadOnlyList<(int Row, int Col)> finders,
        int quietZone)
    {
        for (var col = 0; col < matrix.Size; col++)
        {
            var row = 0;
            while (row < matrix.Size)
            {
                if (!matrix[row, col] || IsInFinder(row, col, finders))
                {
                    row++;
                    continue;
                }

                var start = row;
                while (row < matrix.Size && matrix[row, col] && !IsInFinder(row, col, finders))
                    row++;

                var run = row - start;
                AppendRoundedRect(sb, col + quietZone, start + quietZone, 1, run, 0.5);
            }
        }
    }

    /// <summary>Merges each row's contiguous dark data runs into a single rounded horizontal bar (pill).</summary>
    private static void AppendHorizontalBars(
        StringBuilder sb,
        ModuleMatrix matrix,
        IReadOnlyList<(int Row, int Col)> finders,
        int quietZone)
    {
        for (var row = 0; row < matrix.Size; row++)
        {
            var col = 0;
            while (col < matrix.Size)
            {
                if (!matrix[row, col] || IsInFinder(row, col, finders))
                {
                    col++;
                    continue;
                }

                var start = col;
                while (col < matrix.Size && matrix[row, col] && !IsInFinder(row, col, finders))
                    col++;

                var run = col - start;
                AppendRoundedRect(sb, start + quietZone, row + quietZone, run, 1, 0.5);
            }
        }
    }

    // ── Finder eyes ──

    /// <summary>Appends one finder eye — the outer hollow frame (<paramref name="frame"/>) then the inner solid pupil (<paramref name="dot"/>), in module coordinates offset by the quiet zone.</summary>
    private static void AppendFinderEye(
        StringBuilder sb,
        (int Row, int Col) finder,
        FinderShape frame,
        FinderDotShape dot,
        int quietZone)
    {
        double x = finder.Col + quietZone;
        double y = finder.Row + quietZone;

        AppendFinderFrame(sb, x, y, frame);

        // Pupil is the centered 3×3 block (inset 2 inside the 7×7 frame).
        double px = x + 2;
        double py = y + 2;
        AppendFinderDot(sb, px, py, dot);
    }

    /// <summary>Appends the outer 7×7 finder frame as a hollow ring — square / rounded-rect / circular — using an even-odd outer+inner subpath.</summary>
    private static void AppendFinderFrame(StringBuilder sb, double x, double y, FinderShape shape)
    {
        const double outer = FinderSize;        // 7
        const double thickness = 1;             // the ring is 1 module thick (QR spec)
        const double innerOffset = thickness;   // hole starts 1 module in
        const double inner = outer - (2 * thickness); // 5

        switch (shape)
        {
            case FinderShape.Circle:
                // Outer disc minus inner disc (annulus). Radii to the cell centers' bounding circle.
                AppendCircle(sb, x + outer / 2, y + outer / 2, outer / 2);
                AppendCircle(sb, x + outer / 2, y + outer / 2, inner / 2);
                break;
            case FinderShape.Rounded:
                AppendRoundedRect(sb, x, y, outer, outer, 1.75);
                AppendRoundedRect(sb, x + innerOffset, y + innerOffset, inner, inner, 1.0);
                break;
            default: // Square — sharp ring
                AppendUnitRect(sb, x, y, outer, outer);
                AppendUnitRect(sb, x + innerOffset, y + innerOffset, inner, inner);
                break;
        }
    }

    /// <summary>Appends the inner 3×3 finder pupil as a solid square / rounded-rect / circle.</summary>
    private static void AppendFinderDot(StringBuilder sb, double x, double y, FinderDotShape shape)
    {
        const double side = FinderDotSize; // 3

        switch (shape)
        {
            case FinderDotShape.Circle:
                AppendCircle(sb, x + side / 2, y + side / 2, side / 2);
                break;
            case FinderDotShape.Rounded:
                AppendRoundedRect(sb, x, y, side, side, 0.9);
                break;
            default: // Square
                AppendUnitRect(sb, x, y, side, side);
                break;
        }
    }

    #endregion

    // ── Geometry primitives (module units, SVG path data) ──

    /// <summary>Appends a 1×1 filled square at (<paramref name="x"/>,<paramref name="y"/>).</summary>
    private static void AppendUnitSquare(StringBuilder sb, double x, double y) =>
        AppendUnitRect(sb, x, y, 1, 1);

    /// <summary>A filled axis-aligned rectangle, clockwise so it fills under non-zero and reads as a solid (outer) ring under even-odd.</summary>
    private static void AppendUnitRect(StringBuilder sb, double x, double y, double w, double h)
    {
        sb.Append('M').Append(Num(x)).Append(' ').Append(Num(y))
            .Append('h').Append(Num(w))
            .Append('v').Append(Num(h))
            .Append('h').Append(Num(-w))
            .Append('z');
    }

    /// <summary>A filled circle of <paramref name="r"/> centered at (<paramref name="cx"/>,<paramref name="cy"/>), drawn with two arc halves.</summary>
    private static void AppendCircle(StringBuilder sb, double cx, double cy, double r)
    {
        // Start at the left edge; two 180° arcs trace the full circle (clockwise).
        sb.Append('M').Append(Num(cx - r)).Append(' ').Append(Num(cy))
            .Append('a').Append(Num(r)).Append(' ').Append(Num(r)).Append(" 0 1 0 ").Append(Num(2 * r)).Append(" 0")
            .Append('a').Append(Num(r)).Append(' ').Append(Num(r)).Append(" 0 1 0 ").Append(Num(-2 * r)).Append(" 0")
            .Append('z');
    }

    /// <summary>A filled rounded rectangle (uniform corner radius <paramref name="r"/>, clamped to half the shorter side), clockwise from the top-left tangent.</summary>
    private static void AppendRoundedRect(StringBuilder sb, double x, double y, double w, double h, double r)
    {
        r = Math.Min(r, Math.Min(w, h) / 2);

        // Top edge → TR corner → right edge → BR → bottom → BL → left → TL, using arc (a) corners.
        sb.Append('M').Append(Num(x + r)).Append(' ').Append(Num(y))
            .Append('h').Append(Num(w - 2 * r))
            .Append('a').Append(Num(r)).Append(' ').Append(Num(r)).Append(" 0 0 1 ").Append(Num(r)).Append(' ').Append(Num(r))
            .Append('v').Append(Num(h - 2 * r))
            .Append('a').Append(Num(r)).Append(' ').Append(Num(r)).Append(" 0 0 1 ").Append(Num(-r)).Append(' ').Append(Num(r))
            .Append('h').Append(Num(-(w - 2 * r)))
            .Append('a').Append(Num(r)).Append(' ').Append(Num(r)).Append(" 0 0 1 ").Append(Num(-r)).Append(' ').Append(Num(-r))
            .Append('v').Append(Num(-(h - 2 * r)))
            .Append('a').Append(Num(r)).Append(' ').Append(Num(r)).Append(" 0 0 1 ").Append(Num(r)).Append(' ').Append(Num(-r))
            .Append('z');
    }

    /// <summary>Appends a neighbour-aware module that rounds only the corners with no orthogonal neighbour, so the classy family flows into a connected body, with corner radius <paramref name="r"/> (≤ 0.5).</summary>
    private static void AppendConnectedRounded(
        StringBuilder sb,
        ModuleMatrix matrix,
        int row,
        int col,
        double x,
        double y,
        double r)
    {
        r = Math.Min(r, 0.5);

        var up = IsDark(matrix, row - 1, col);
        var down = IsDark(matrix, row + 1, col);
        var left = IsDark(matrix, row, col - 1);
        var right = IsDark(matrix, row, col + 1);

        // A corner is rounded only when BOTH edges meeting at it are exposed (no neighbour on either side).
        var tl = !up && !left;
        var tr = !up && !right;
        var br = !down && !right;
        var bl = !down && !left;

        // Trace clockwise from just right of the top-left corner. Each corner is either a square step or a quarter arc.
        sb.Append('M').Append(Num(x + (tl ? r : 0))).Append(' ').Append(Num(y));

        // Top edge → top-right corner
        sb.Append('h').Append(Num(1 - (tl ? r : 0) - (tr ? r : 0)));
        AppendCorner(sb, tr, r, +1, +1); // arc into the right edge

        // Right edge → bottom-right corner
        sb.Append('v').Append(Num(1 - (tr ? r : 0) - (br ? r : 0)));
        AppendCorner(sb, br, r, -1, +1);

        // Bottom edge → bottom-left corner
        sb.Append('h').Append(Num(-(1 - (br ? r : 0) - (bl ? r : 0))));
        AppendCorner(sb, bl, r, -1, -1);

        // Left edge → top-left corner
        sb.Append('v').Append(Num(-(1 - (bl ? r : 0) - (tl ? r : 0))));
        AppendCorner(sb, tl, r, +1, -1);

        sb.Append('z');
    }

    /// <summary>Emits one corner of <see cref="AppendConnectedRounded"/>: a quarter arc when <paramref name="rounded"/>, else a square step. <paramref name="dx"/>/<paramref name="dy"/> give the turn direction (±1).</summary>
    private static void AppendCorner(StringBuilder sb, bool rounded, double r, int dx, int dy)
    {
        if (rounded)
            sb.Append('a').Append(Num(r)).Append(' ').Append(Num(r)).Append(" 0 0 1 ").Append(Num(r * dx)).Append(' ').Append(Num(r * dy));
        // square corner = no extra segment; the preceding h/v already reached the turn point.
    }

    /// <summary>Reads a module bit with out-of-bounds treated as light (false) — the neighbour test for connected rounding.</summary>
    private static bool IsDark(ModuleMatrix matrix, int row, int col) =>
        row >= 0 && col >= 0 && row < matrix.Size && col < matrix.Size && matrix[row, col];

    /// <summary>True when (<paramref name="row"/>,<paramref name="col"/>) falls inside any of the 7×7 finder regions.</summary>
    private static bool IsInFinder(int row, int col, IReadOnlyList<(int Row, int Col)> finders)
    {
        foreach (var f in finders)
            if (row >= f.Row && row < f.Row + FinderSize && col >= f.Col && col < f.Col + FinderSize)
                return true;
        return false;
    }

    /// <summary>Appends a centered <c>&lt;image&gt;</c> logo sized to <see cref="LogoSpec.SizeRatio"/> of the symbol (excluding the quiet zone), in module-unit coordinates.</summary>
    private static void EmitLogo(StringBuilder sb, int sizeModules, LogoSpec? logo)
    {
        if (logo is null)
            return;

        // Size relative to the symbol (canvas minus the quiet zone both sides), clamped to a sane band.
        var symbolModules = sizeModules; // canvas units; ratio applied to the full canvas keeps the logo proportional and centered
        var ratio = Math.Clamp(logo.SizeRatio, 0.05, 0.4);
        var side = symbolModules * ratio;
        var offset = (sizeModules - side) / 2.0;

        sb.Append("<image x=\"").Append(Num(offset)).Append("\" y=\"").Append(Num(offset))
            .Append("\" width=\"").Append(Num(side)).Append("\" height=\"").Append(Num(side))
            .Append("\" href=\"").Append(EscapeAttr(logo.DataUrl))
            .Append("\" preserveAspectRatio=\"xMidYMid meet\"/>\n");
    }

    /// <summary>Emits the optional center emoji — a knockout halo (bg color, or white when transparent) plus a centered SVG <c>&lt;text&gt;</c> glyph.</summary>
    private static void EmitEmoji(StringBuilder sb, int sizeModules, string backgroundColor, bool transparent, EmojiSpec? emoji)
    {
        if (emoji is null)
            return;

        var ratio = Math.Clamp(emoji.SizeRatio, EmojiMinRatio, EmojiMaxRatio);
        var side = sizeModules * ratio;
        var center = sizeModules / 2.0;

        // The data modules under the glyph are already knocked out of the matrix (a genuine blank center). On a solid
        // background, smooth the matrix-quantized hole edge with a bg-colored disc; on a transparent background, leave the real hole.
        if (!transparent)
            sb.Append("<circle cx=\"").Append(Num(center)).Append("\" cy=\"").Append(Num(center))
                .Append("\" r=\"").Append(Num(side * EmojiHoleFactor)).Append("\" fill=\"").Append(backgroundColor).Append("\"/>\n");

        sb.Append("<text x=\"").Append(Num(center)).Append("\" y=\"").Append(Num(center))
            .Append("\" font-size=\"").Append(Num(side))
            .Append("\" text-anchor=\"middle\" dominant-baseline=\"central\">")
            .Append(EscapeAttr(emoji.Char)).Append("</text>\n");
    }

    /// <summary>Emits the foreground gradient <c>&lt;defs&gt;</c> in user space spanning the full canvas — a <see cref="RadialGradientSpec"/> from the center, or a <see cref="LinearGradientSpec"/> rotated by its angle about the center — referenced as <c>url(#sqr-fg)</c> by the data + eye fills.</summary>
    private static void EmitForegroundGradientDefs(StringBuilder sb, GradientSpec gradient, int size)
    {
        var mid = size / 2.0;
        var radial = gradient is RadialGradientSpec;
        sb.Append("<defs>");

        switch (gradient)
        {
            case RadialGradientSpec r:
                sb.Append("<radialGradient id=\"").Append(ForegroundGradientId)
                    .Append("\" gradientUnits=\"userSpaceOnUse\" cx=\"").Append(Num(mid))
                    .Append("\" cy=\"").Append(Num(mid)).Append("\" r=\"").Append(Num(mid * Math.Clamp(r.Radius, 0.0, 1.0))).Append("\">");
                break;
            case LinearGradientSpec l:
                sb.Append("<linearGradient id=\"").Append(ForegroundGradientId)
                    .Append("\" gradientUnits=\"userSpaceOnUse\" x1=\"0\" y1=\"0\" x2=\"").Append(size)
                    .Append("\" y2=\"0\" gradientTransform=\"rotate(").Append(Num(l.Angle))
                    .Append(' ').Append(Num(mid)).Append(' ').Append(Num(mid)).Append(")\">");
                break;
        }

        foreach (var stop in gradient.Stops)
            sb.Append("<stop offset=\"").Append(Num(Math.Clamp(stop.Offset, 0.0, 1.0)))
                .Append("\" stop-color=\"").Append(stop.Color).Append("\"/>");

        sb.Append(radial ? "</radialGradient>" : "</linearGradient>")
            .Append("</defs>\n");
    }

    /// <summary>Invariant decimal formatting (no thousands separators, '.' radix) so SVG numerics are culture-stable.</summary>
    private static string Num(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Minimal XML-attribute escaping for the logo href (data URLs are base64 but may carry '&amp;' / quotes in edge cases).</summary>
    private static string EscapeAttr(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);
}
