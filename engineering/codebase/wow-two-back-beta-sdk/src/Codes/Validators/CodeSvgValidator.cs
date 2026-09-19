using System.Buffers;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Validators;

/// <summary>Validates the bounded SVG subset emitted by the code engine.</summary>
internal sealed class CodeSvgValidator
{
    private readonly InlineImageValidator _images = new();
    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdefABCDEF");
    private static readonly HashSet<string> Elements = new(StringComparer.Ordinal)
        { "svg", "g", "path", "rect", "circle", "text", "defs", "linearGradient", "radialGradient", "stop", "image", "desc" };
    private static readonly HashSet<string> Attributes = new(StringComparer.Ordinal)
        { "viewBox", "width", "height", "x", "y", "d", "fill", "fill-rule", "shape-rendering", "cx", "cy", "r",
          "font-size", "font-family", "text-anchor", "dominant-baseline", "id", "gradientUnits", "x1", "y1", "x2", "y2",
          "gradientTransform", "offset", "stop-color", "href", "preserveAspectRatio", "version", "stroke", "stroke-width" };

    internal void Validate(string svg)
    {
        if (string.IsNullOrWhiteSpace(svg) || svg.Length > 4_194_304) Fail();
        try
        {
            using var reader = XmlReader.Create(new StringReader(svg), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4_194_304,
            });
            var count = 0;
            while (reader.Read())
            {
                if (reader.Depth > 32 || ++count > 100_000 || reader.NodeType == XmlNodeType.ProcessingInstruction) Fail();
            }
            var document = XDocument.Parse(svg);
            var root = document.Root;
            if (root is null || root.Name != XName.Get("svg", "http://www.w3.org/2000/svg")) Fail();
            long imagePixels = 0;
            foreach (var element in root!.DescendantsAndSelf())
            {
                if (element.Name.NamespaceName != "http://www.w3.org/2000/svg" || !Elements.Contains(element.Name.LocalName)) Fail();
                foreach (var attribute in element.Attributes())
                {
                    if (attribute.IsNamespaceDeclaration) continue;
                    var name = attribute.Name.LocalName;
                    if (!Attributes.Contains(name) || (attribute.Name.NamespaceName.Length != 0
                        && !(name == "href" && attribute.Name.NamespaceName == "http://www.w3.org/1999/xlink"))) Fail();
                    var value = attribute.Value;
                    if (name == "href")
                    {
                        if (element.Name.LocalName != "image" || !_images.TryGetPixelCount(value, out var pixels)) Fail();
                        else if ((imagePixels += pixels) > 16_000_000) Fail();
                    }
                    else if (name is "width" or "height" or "x" or "y" or "cx" or "cy" or "r"
                        or "x1" or "y1" or "x2" or "y2" or "font-size" or "offset" or "stroke-width")
                    {
                        if (!double.TryParse(value.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                            || !double.IsFinite(number) || Math.Abs(number) > 4096) Fail();
                    }
                    else if (name == "viewBox")
                    {
                        var values = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                        if (values.Length != 4 || values.Any(part => !double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                            || !double.IsFinite(number) || Math.Abs(number) > 4096)) Fail();
                    }
                    else if (name is "fill" or "stroke" or "stop-color")
                    {
                        // No CSS escapes or external paint servers; generated gradients have a fixed local ID.
                        if (value != "none" && value != "black" && value != "white" && value != "url(#sqr-fg)"
                            && !(value.Length is 4 or 7 or 9 && value[0] == '#'
                                && value.AsSpan(1).IndexOfAnyExcept(HexDigits) < 0)) Fail();
                    }
                }
            }
            foreach (var dimension in new[] { "width", "height" })
            {
                var value = (string?)root.Attribute(dimension);
                if (value is null || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                    || !double.IsFinite(number) || number <= 0 || number > 4096) Fail();
            }
        }
        catch (XmlException) { Fail(); }
    }

    private static void Fail() => throw CodeRenderValidationExtensions.Invalid("Svg", "Use a bounded SVG from the code rendering engine.");
}
