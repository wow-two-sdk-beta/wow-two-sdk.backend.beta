using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace WoW.Two.Sdk.Backend.Beta.Media.Word;

/// <summary>Maps a Word body to plain text or Markdown in reading order; tracked deletions and field codes stay out.</summary>
internal static partial class WordTextMapper
{
    private static readonly HashSet<string> CodeFonts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Consolas", "Courier New", "Courier", "Menlo", "Monaco", "Lucida Console", "Cascadia Code", "Cascadia Mono",
    };

    /// <summary>The body as text in <paramref name="format"/>.</summary>
    public static string ToText(MainDocumentPart main, WordTextFormat format)
    {
        var body = main.Document?.Body;
        if (body is null)
            return string.Empty;

        var reader = new Reader(main, format == WordTextFormat.Markdown);
        var blocks = new List<(BlockKind Kind, string Text, string? List)>();
        foreach (var block in Blocks(body))
        {
            if (block is Table table)
                blocks.Add((BlockKind.Table, reader.Table(table), null));
            else if (block is Paragraph paragraph && reader.Paragraph(paragraph) is { } line)
                blocks.Add(line);
        }

        return Join(blocks, reader.Markdown);
    }

    /// <summary>The words in <paramref name="text"/>.</summary>
    public static int CountWords(string text) => Words().Count(text);

    /// <summary>The paragraphs and tables of a body, content controls and custom XML unwrapped.</summary>
    private static IEnumerable<OpenXmlElement> Blocks(OpenXmlElement container)
    {
        foreach (var child in container.ChildElements)
        {
            switch (child)
            {
                case Paragraph or Table:
                    yield return child;
                    break;
                case SdtBlock or SdtContentBlock or CustomXmlBlock:
                    foreach (var nested in Blocks(child))
                        yield return nested;
                    break;
            }
        }
    }

    /// <summary>Joins blocks: blank lines between blocks in Markdown, except inside one list and one code block.</summary>
    private static string Join(List<(BlockKind Kind, string Text, string? List)> blocks, bool markdown)
    {
        var text = new StringBuilder();
        for (var index = 0; index < blocks.Count; index++)
        {
            if (index > 0)
            {
                var together = !markdown
                    || (blocks[index].Kind == BlockKind.ListItem && blocks[index - 1].Kind == BlockKind.ListItem && (blocks[index].List == blocks[index - 1].List || blocks[index].Text.StartsWith(' ')))
                    || (blocks[index].Kind == BlockKind.Code && blocks[index - 1].Kind == BlockKind.Code);
                text.Append(together ? "\n" : "\n\n");
            }

            var current = blocks[index];
            if (markdown && current.Kind == BlockKind.Code)
            {
                if (index == 0 || blocks[index - 1].Kind != BlockKind.Code)
                    text.Append("```\n");

                text.Append(current.Text);
                if (index == blocks.Count - 1 || blocks[index + 1].Kind != BlockKind.Code)
                    text.Append("\n```");
            }
            else
            {
                text.Append(current.Text);
            }
        }

        return text.ToString();
    }

    [GeneratedRegex(@"\S+")]
    private static partial Regex Words();

    [GeneratedRegex(@"^heading\s*([1-9])$", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingName();

    private enum BlockKind
    {
        Paragraph,
        ListItem,
        Code,
        Table,
    }

    /// <summary>Reads paragraphs and tables with the styles and numbering of one document.</summary>
    private sealed class Reader
    {
        private readonly MainDocumentPart _main;
        private readonly Dictionary<string, string> _styleNames;
        private readonly Dictionary<int, int> _abstractOfNum = [];
        private readonly Dictionary<(int Abstract, int Level), bool> _bulletLevels = [];

        public Reader(MainDocumentPart main, bool markdown)
        {
            _main = main;
            Markdown = markdown;
            _styleNames = main.StyleDefinitionsPart?.Styles?.Elements<Style>()
                .Where(style => style.StyleId?.Value is not null)
                .GroupBy(style => style.StyleId!.Value!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().StyleName?.Val?.Value ?? group.Key, StringComparer.Ordinal) ?? [];
            if (main.NumberingDefinitionsPart?.Numbering is { } numbering)
            {
                foreach (var instance in numbering.Elements<NumberingInstance>())
                {
                    if (instance.NumberID?.Value is { } id && instance.AbstractNumId?.Val?.Value is { } abstractId)
                        _abstractOfNum[id] = abstractId;
                }

                foreach (var abstractNum in numbering.Elements<AbstractNum>())
                {
                    foreach (var level in abstractNum.Elements<Level>())
                    {
                        if (abstractNum.AbstractNumberId?.Value is { } abstractId && level.LevelIndex?.Value is { } levelIndex)
                            _bulletLevels[(abstractId, levelIndex)] = level.NumberingFormat?.Val?.Value == NumberFormatValues.Bullet;
                    }
                }
            }
        }

        public bool Markdown { get; }

        public (BlockKind Kind, string Text, string? List)? Paragraph(Paragraph paragraph)
        {
            var style = StyleName(paragraph);
            var heading = HeadingLevel(paragraph, style);
            var code = style is not null && (style.Equals("Code Block", StringComparison.OrdinalIgnoreCase) || style.Equals("CodeBlock", StringComparison.OrdinalIgnoreCase) || style.Equals("HTML Preformatted", StringComparison.OrdinalIgnoreCase));
            var text = Inline(paragraph, emphasis: Markdown && heading == 0 && !code, rawCode: code);
            if (string.IsNullOrWhiteSpace(text) && !code)
                return null;
            if (!Markdown)
                return (BlockKind.Paragraph, text, null);
            if (code)
                return (BlockKind.Code, text, null);
            if (heading > 0)
                return (BlockKind.Paragraph, new string('#', heading) + " " + text.Trim(), null);
            if (ListMarker(paragraph) is { } marker)
                return (BlockKind.ListItem, marker.Marker + text.Trim(), marker.List);
            if (style is not null && style.Contains("Quote", StringComparison.OrdinalIgnoreCase))
                return (BlockKind.Paragraph, "> " + text.Trim(), null);

            return (BlockKind.Paragraph, text.Trim(), null);
        }

        public string Table(Table table)
        {
            var rows = table.Elements<TableRow>()
                .Select(row => row.Elements<TableCell>().Select(Cell).ToList())
                .Where(row => row.Count > 0)
                .ToList();
            if (rows.Count == 0)
                return string.Empty;
            if (!Markdown)
                return string.Join('\n', rows.Select(row => string.Join('\t', row)));

            var columns = rows.Max(row => row.Count);
            var lines = new List<string> { Row(rows[0], columns), "|" + string.Concat(Enumerable.Repeat(" --- |", columns)) };
            lines.AddRange(rows.Skip(1).Select(row => Row(row, columns)));
            return string.Join('\n', lines);
        }

        private static string Row(List<string> cells, int columns)
            => "| " + string.Join(" | ", cells.Concat(Enumerable.Repeat(string.Empty, columns - cells.Count)).Select(cell => cell.Replace("|", "\\|", StringComparison.Ordinal))) + " |";

        private string Cell(TableCell cell)
            => string.Join(Markdown ? "<br>" : " ", Blocks(cell)
                .Select(block => block is Paragraph paragraph ? Inline(paragraph, emphasis: Markdown, rawCode: false).Trim() : string.Empty)
                .Where(text => text.Length > 0));

        private string? StyleName(Paragraph paragraph)
        {
            var id = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            return id is null ? null : _styleNames.GetValueOrDefault(id, id);
        }

        private static int HeadingLevel(Paragraph paragraph, string? style)
        {
            if (style is not null)
            {
                if (style.Equals("Title", StringComparison.OrdinalIgnoreCase))
                    return 1;

                var match = HeadingName().Match(style.Replace("Heading", "heading ", StringComparison.Ordinal).Trim());
                if (match.Success)
                    return Math.Min(int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 6);
            }

            return paragraph.ParagraphProperties?.OutlineLevel?.Val?.Value is { } outline && outline < 6 ? outline + 1 : 0;
        }

        /// <summary>The indented marker of a numbered paragraph and the list it belongs to; null for other paragraphs.</summary>
        private (string Marker, string List)? ListMarker(Paragraph paragraph)
        {
            var numbering = paragraph.ParagraphProperties?.NumberingProperties;
            if (numbering?.NumberingId?.Val?.Value is not { } numId || numId == 0)
                return null;

            var level = numbering.NumberingLevelReference?.Val?.Value ?? 0;
            var bullet = !_abstractOfNum.TryGetValue(numId, out var abstractId) || _bulletLevels.GetValueOrDefault((abstractId, level), true);
            return (new string(' ', level * 2) + (bullet ? "- " : "1. "), numId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>The text of runs under <paramref name="element"/>, with Markdown emphasis and links when asked.</summary>
        private string Inline(OpenXmlElement element, bool emphasis, bool rawCode)
        {
            var segments = new List<Segment>();
            Collect(element, segments, link: null);
            if (!Markdown || rawCode)
                return string.Concat(segments.Select(segment => segment.Text));

            var text = new StringBuilder();
            foreach (var group in Merge(segments))
                text.Append(Render(group, emphasis));

            return text.ToString();
        }

        private void Collect(OpenXmlElement element, List<Segment> segments, string? link)
        {
            foreach (var child in element.ChildElements)
            {
                switch (child)
                {
                    case Run run:
                        CollectRun(run, segments, link);
                        break;
                    case Hyperlink hyperlink:
                        Collect(hyperlink, segments, LinkOf(hyperlink) ?? link);
                        break;
                    case DeletedRun or MoveFromRun or ParagraphProperties:
                        break;
                    default:
                        if (child.HasChildren)
                            Collect(child, segments, link);
                        break;
                }
            }
        }

        private void CollectRun(Run run, List<Segment> segments, string? link)
        {
            var properties = run.RunProperties;
            var font = properties?.RunFonts?.Ascii?.Value;
            var style = properties?.RunStyle?.Val?.Value;
            var format = new Segment
            {
                Text = string.Empty,
                Bold = IsOn(properties?.Bold),
                Italic = IsOn(properties?.Italic),
                Strike = IsOn(properties?.Strike),
                Code = (font is not null && CodeFonts.Contains(font)) || (style is not null && (style.StartsWith("Code", StringComparison.OrdinalIgnoreCase) || style.Equals("HTMLCode", StringComparison.OrdinalIgnoreCase))),
                Link = link,
            };
            foreach (var child in run.ChildElements)
            {
                var text = child switch
                {
                    Text value => value.Text,
                    TabChar => "\t",
                    Break or CarriageReturn => Markdown ? "  \n" : "\n",
                    NoBreakHyphen => "-",
                    _ => null,
                };
                if (text is not null)
                    segments.Add(format with { Text = text });
            }
        }

        private string? LinkOf(Hyperlink hyperlink)
        {
            if (hyperlink.Id?.Value is { } id)
                return _main.HyperlinkRelationships.FirstOrDefault(relationship => relationship.Id == id)?.Uri.ToString();

            return hyperlink.Anchor?.Value is { } anchor ? "#" + anchor : null;
        }

        private static bool IsOn(OnOffType? toggle) => toggle is not null && (toggle.Val?.Value ?? true);

        private static IEnumerable<Segment> Merge(List<Segment> segments)
        {
            Segment? current = null;
            foreach (var segment in segments)
            {
                if (current is not null && current with { Text = string.Empty } == segment with { Text = string.Empty })
                {
                    current = current with { Text = current.Text + segment.Text };
                    continue;
                }

                if (current is not null)
                    yield return current;

                current = segment;
            }

            if (current is not null)
                yield return current;
        }

        private static string Render(Segment segment, bool emphasis)
        {
            var text = segment.Text;
            if (text.Trim().Length == 0)
                return text;

            var core = segment.Code ? "`" + text.Trim() + "`" : Escape(text.Trim());
            if (emphasis && !segment.Code)
            {
                var marker = (segment.Bold ? "**" : string.Empty) + (segment.Italic ? "*" : string.Empty);
                core = marker + core + new string(marker.Reverse().ToArray());
                if (segment.Strike)
                    core = "~~" + core + "~~";
            }

            if (segment.Link is { } link)
                core = $"[{core}]({link.Replace(" ", "%20", StringComparison.Ordinal)})";

            var leading = text[..(text.Length - text.TrimStart().Length)];
            var trailing = text[text.TrimEnd().Length..];
            return leading + core + trailing;
        }

        private static string Escape(string text)
            => text.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("*", "\\*", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal)
                .Replace("`", "\\`", StringComparison.Ordinal);

        /// <summary>Represents a run of text with one formatting.</summary>
        private sealed record Segment
        {
            public required string Text { get; init; }

            public bool Bold { get; init; }

            public bool Italic { get; init; }

            public bool Strike { get; init; }

            public bool Code { get; init; }

            public string? Link { get; init; }
        }
    }
}
