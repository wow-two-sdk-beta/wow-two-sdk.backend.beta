using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace WoW.Two.Sdk.Backend.Beta.Media.Captions.Parsers;

/// <summary>
/// Parses TTML/DFXP XML into caption segments from paragraphs in any namespace.
/// Resolves begin/end or begin/duration, flattens inline spans, and collapses whitespace.
/// </summary>
public sealed partial class TtmlCaptionParser : ITtmlCaptionParser
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRuns();

    /// <inheritdoc />
    public IReadOnlyList<CaptionSegment> Parse(string ttmlContent)
    {
        if (string.IsNullOrWhiteSpace(ttmlContent)) return [];

        XDocument document;
        try
        {
            document = XDocument.Parse(ttmlContent);
        }
        catch (XmlException)
        {
            return [];
        }

        var segments = new List<CaptionSegment>();

        foreach (var paragraph in document.Descendants().Where(static e => e.Name.LocalName == "p"))
        {
            var beginRaw = AttributeByLocalName(paragraph, "begin");
            if (beginRaw is null) continue;

            var start = CaptionTimecodeMapper.ParseTtml(beginRaw);

            var endRaw = AttributeByLocalName(paragraph, "end");
            var end = endRaw is not null
                ? CaptionTimecodeMapper.ParseTtml(endRaw)
                : AttributeByLocalName(paragraph, "dur") is { } durRaw
                    ? start + CaptionTimecodeMapper.ParseTtml(durRaw)
                    : start;

            var builder = new StringBuilder();
            AppendText(paragraph, builder);
            var text = WhitespaceRuns().Replace(builder.ToString(), " ").Trim();

            if (text.Length > 0)
                segments.Add(new CaptionSegment { Start = start, End = end, Text = text });
        }

        return segments;
    }

    private static string? AttributeByLocalName(XElement element, string localName)
        => element.Attributes().FirstOrDefault(a => a.Name.LocalName == localName)?.Value;

    private static void AppendText(XElement element, StringBuilder builder)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    builder.Append(text.Value);
                    break;
                case XElement child when child.Name.LocalName == "br":
                    builder.Append(' ');
                    break;
                case XElement child:
                    AppendText(child, builder);
                    break;
                default:
                    break;
            }
        }
    }
}
