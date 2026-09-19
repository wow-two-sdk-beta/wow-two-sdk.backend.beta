using Xunit;
using WoW.Two.Sdk.Backend.Beta.Codes.Models;
using WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Matrix;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Svg;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Codes;

/// <summary>Proves the shape seam — <see cref="ModuleShape"/> primitives, eye shapes, the byte-parity path.</summary>
/// <remarks>Geometry only — the decode proof lives in <see cref="QrDecodeRoundTripTests"/>.</remarks>
public class SvgShapeTests
{
    private readonly SvgRenderer _emitter = new();
    private readonly QrMatrixGenerator _matrixSource = new();
    private const string Payload = "https://foreverpin.com/abc1234";

    /// <summary>Builds a real QR matrix (≥ 21×21) so the three finder regions exist.</summary>
    private ModuleMatrix RealQr() => _matrixSource.Generate(Payload, EccLevel.Q);

    // ── Byte-parity fast path ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void All_square_style_takes_legacy_fast_path_single_path_no_fill_rule()
    {
        // The legacy look emits ONE foreground <path> with horizontal-run tokens and no evenodd rule.
        var svg = _emitter.Emit(RealQr(), StyleSpec.Default);

        Assert.Single(PathOccurrences(svg));
        Assert.DoesNotContain("fill-rule", svg);
        Assert.Contains("v1h-", svg); // the legacy run token
    }

    [Fact]
    public void Square_module_shape_is_identical_to_default()
    {
        var matrix = RealQr();

        var byDefault = _emitter.Emit(matrix, StyleSpec.Default);
        var explicitSquare = _emitter.Emit(matrix, StyleSpec.Default with { ModuleShape = ModuleShape.Square });

        Assert.Equal(byDefault, explicitSquare);
    }

    // ── Per-shape primitives ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Dots_module_shape_emits_circles_for_data()
    {
        var svg = _emitter.Emit(RealQr(), StyleSpec.Default with { ModuleShape = ModuleShape.Dots });

        // Circles are drawn with elliptical arc commands ('a'); squares never use them.
        Assert.Contains("a0.5 0.5 0 1 0", svg);
    }

    [Fact]
    public void Rounded_module_shape_emits_rounded_rect_arcs_for_data()
    {
        var svg = _emitter.Emit(RealQr(), StyleSpec.Default with { ModuleShape = ModuleShape.Rounded });

        // Rounded-rect corners are quarter arcs "a{r} {r} 0 0 1".
        Assert.Contains("0 0 1", svg);
    }

    [Theory]
    [InlineData(ModuleShape.Classy)]
    [InlineData(ModuleShape.ClassyRounded)]
    public void Classy_family_emits_connected_arc_corners(ModuleShape shape)
    {
        var svg = _emitter.Emit(RealQr(), StyleSpec.Default with { ModuleShape = shape });

        // Neighbour-aware tiles round exposed corners with quarter arcs.
        Assert.Contains("0 0 1", svg);
        Assert.StartsWith("<svg", svg);
        Assert.EndsWith("</svg>", svg);
    }

    [Fact]
    public void Vertical_bars_merge_contiguous_column_runs_into_taller_than_wide_rects()
    {
        // A 2-tall single-column dark run (outside any finder) must collapse into ONE bar, not two modules.
        // (The matrix is QR-sized so the geometry-driven eye group is also emitted — assert on the DATA path only.)
        var matrix = SingleColumnRun(size: 25, col: 12, startRow: 11, length: 2);
        var data = DataPathBody(_emitter.Emit(
            matrix,
            StyleSpec.Default with { ModuleShape = ModuleShape.VerticalBars }));

        // One bar ⇒ exactly one sub-path start; height 2 with 0.5 radius ⇒ vertical run of 2 - 2*0.5 = 1.
        Assert.Equal(1, CountOccurrences(data, "M"));
        Assert.Contains("v1", data);
    }

    [Fact]
    public void Horizontal_bars_merge_contiguous_row_runs()
    {
        var matrix = SingleRowRun(size: 25, row: 12, startCol: 11, length: 3);
        var data = DataPathBody(_emitter.Emit(
            matrix,
            StyleSpec.Default with { ModuleShape = ModuleShape.HorizontalBars }));

        // One bar ⇒ one sub-path; width 3 with 0.5 radius ⇒ horizontal run of 3 - 2*0.5 = 2.
        Assert.Equal(1, CountOccurrences(data, "M"));
        Assert.Contains("h2", data);
    }

    // ── Finder ↔ data separation ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Stylised_style_splits_foreground_into_data_and_eye_paths()
    {
        // Non-square style ⇒ two foreground paths: the data body and the (evenodd) eye group.
        var svg = _emitter.Emit(RealQr(), StyleSpec.Default with
        {
            ModuleShape = ModuleShape.Dots,
            FinderShape = FinderShape.Rounded,
            FinderDotShape = FinderDotShape.Circle,
        });

        Assert.Equal(2, PathOccurrences(svg).Count);
        Assert.Contains("fill-rule=\"evenodd\"", svg); // the eye group
    }

    [Fact]
    public void Finder_shape_circle_renders_ring_arcs_in_eye_group()
    {
        var svg = _emitter.Emit(RealQr(), StyleSpec.Default with
        {
            ModuleShape = ModuleShape.Square, // square body still triggers the split because finder is non-square
            FinderShape = FinderShape.Circle,
            FinderDotShape = FinderDotShape.Square,
        });

        // Square body + circular eyes ⇒ split path; the eye annulus uses radius-3.5 arcs.
        Assert.Equal(2, PathOccurrences(svg).Count);
        Assert.Contains("a3.5 3.5 0 1 0", svg);
    }

    [Fact]
    public void Finder_dot_circle_renders_pupil_arc()
    {
        var svg = _emitter.Emit(RealQr(), StyleSpec.Default with { FinderDotShape = FinderDotShape.Circle });

        // The 3×3 pupil as a circle → radius-1.5 arc.
        Assert.Contains("a1.5 1.5 0 1 0", svg);
    }

    [Fact]
    public void Finder_eyes_render_even_when_independent_of_body_shape()
    {
        // The eyes are drawn from geometry, not matrix bits, so all three appear regardless of body shape.
        // Each circular outer frame is one disc = two 180° arc halves, so 3 eyes ⇒ 6 occurrences of the radius-3.5 arc.
        var svg = _emitter.Emit(RealQr(), StyleSpec.Default with
        {
            ModuleShape = ModuleShape.Dots,
            FinderShape = FinderShape.Circle,
        });

        Assert.Equal(6, CountOccurrences(svg, "a3.5 3.5 0 1 0")); // 3 finder frames × 2 arc halves
    }

    [Fact]
    public void Small_non_qr_matrix_does_not_special_case_corners()
    {
        // Below the QR floor (21), corner regions are NOT treated as finders — the whole grid shapes uniformly,
        // so there is no separate evenodd eye group.
        var tiny = Checker(5);
        var svg = _emitter.Emit(tiny, StyleSpec.Default with { ModuleShape = ModuleShape.Dots });

        Assert.Single(PathOccurrences(svg));
        Assert.DoesNotContain("fill-rule", svg);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────

    private static List<int> PathOccurrences(string svg)
    {
        var positions = new List<int>();
        var idx = 0;
        while ((idx = svg.IndexOf("<path", idx, StringComparison.Ordinal)) >= 0)
        {
            positions.Add(idx);
            idx += 5;
        }
        return positions;
    }

    /// <summary>Gets the data body, the <c>d</c> attribute of the first foreground <c>&lt;path&gt;</c>.</summary>
    private static string DataPathBody(string svg)
    {
        var pathStart = svg.IndexOf("<path", StringComparison.Ordinal);
        var dStart = svg.IndexOf("d=\"", pathStart, StringComparison.Ordinal) + 3;
        var dEnd = svg.IndexOf('"', dStart);
        return svg[dStart..dEnd];
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }

    private static ModuleMatrix Checker(int n)
    {
        var m = new bool[n, n];
        for (var r = 0; r < n; r++)
            for (var c = 0; c < n; c++)
                m[r, c] = (r + c) % 2 == 0;
        return new ModuleMatrix(m);
    }

    /// <summary>Builds an all-light n×n matrix with one vertical dark run, away from the finder corners.</summary>
    private static ModuleMatrix SingleColumnRun(int size, int col, int startRow, int length)
    {
        var m = new bool[size, size];
        for (var i = 0; i < length; i++)
            m[startRow + i, col] = true;
        return new ModuleMatrix(m);
    }

    /// <summary>Builds an all-light n×n matrix with one horizontal dark run, away from the finder corners.</summary>
    private static ModuleMatrix SingleRowRun(int size, int row, int startCol, int length)
    {
        var m = new bool[size, size];
        for (var i = 0; i < length; i++)
            m[row, startCol + i] = true;
        return new ModuleMatrix(m);
    }
}
