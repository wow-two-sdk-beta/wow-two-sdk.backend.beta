using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdownTable = Markdig.Extensions.Tables.Table;
using MarkdownTableCell = Markdig.Extensions.Tables.TableCell;
using MarkdownTableRow = Markdig.Extensions.Tables.TableRow;
using WordTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using WordTableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using WordTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;

namespace WoW.Two.Sdk.Backend.Beta.Media.Word;

/// <summary>Renders a Markdown document into a Word body with real styles, numbering and hyperlinks.</summary>
/// <param name="main">The document part being built; its styles and numbering parts exist.</param>
internal sealed class MarkdownWordRenderer(MainDocumentPart main)
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .UseTaskLists()
        .DisableHtml()
        .Build();

    private int _nextNumberingId = WordStylesMapper.BulletNumberingId + 1;

    /// <summary>Parses <paramref name="markdown"/> and appends its blocks to <paramref name="body"/>.</summary>
    public void Render(string markdown, Body body)
    {
        foreach (var block in Markdig.Markdown.Parse(markdown, Pipeline))
            Block(block, body, new Place());
    }

    private void Block(Block block, OpenXmlElement target, Place place)
    {
        switch (block)
        {
            case HeadingBlock heading:
                target.Append(Paragraph(heading.Inline, $"Heading{Math.Clamp(heading.Level, 1, 6)}", place with { Numbering = null }));
                break;
            case ParagraphBlock paragraph:
                target.Append(Paragraph(paragraph.Inline, place.Quote ? "Quote" : place.Depth >= 0 ? "ListParagraph" : null, place));
                break;
            case ListBlock list:
                List(list, target, place);
                break;
            case QuoteBlock quote:
                foreach (var child in quote)
                    Block(child, target, place with { Quote = true, Numbering = null });
                break;
            case FencedCodeBlock or CodeBlock:
                Code((LeafBlock)block, target);
                break;
            case ThematicBreakBlock:
                target.Append(new Paragraph(new ParagraphProperties(new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 6, Space = 1, Color = "BFBFBF" }))));
                break;
            case MarkdownTable table:
                target.Append(Table(table));
                break;
            case HtmlBlock html:
                Code(html, target);
                break;
            case ContainerBlock container:
                foreach (var child in container)
                    Block(child, target, place);
                break;
        }
    }

    private void List(ListBlock list, OpenXmlElement target, Place place)
    {
        var numbering = WordStylesMapper.BulletNumberingId;
        if (list.IsOrdered)
        {
            numbering = _nextNumberingId++;
            var start = int.TryParse(list.OrderedStart, out var first) ? first : 1;
            main.NumberingDefinitionsPart!.Numbering!.Append(WordStylesMapper.OrderedInstance(numbering, start));
        }

        var depth = Math.Min(place.Depth + 1, 8);
        foreach (var item in list.OfType<ListItemBlock>())
        {
            var marked = false;
            foreach (var child in item)
            {
                if (child is ParagraphBlock && !marked)
                {
                    Block(child, target, place with { Depth = depth, Numbering = numbering });
                    marked = true;
                }
                else
                {
                    Block(child, target, place with { Depth = depth, Numbering = null });
                }
            }
        }
    }

    private Paragraph Paragraph(ContainerInline? inlines, string? style, Place place)
    {
        var properties = new ParagraphProperties();
        if (style is not null)
            properties.Append(new ParagraphStyleId { Val = style });
        if (place.Numbering is { } numbering)
            properties.Append(new NumberingProperties(new NumberingLevelReference { Val = place.Depth }, new NumberingId { Val = numbering }));
        else if (place.Depth >= 0 && style != "Quote")
            properties.Append(new Indentation { Left = (720 * (place.Depth + 1)).ToString(System.Globalization.CultureInfo.InvariantCulture) });

        var paragraph = new Paragraph();
        if (properties.HasChildren)
            paragraph.Append(properties);

        Inlines(inlines, paragraph, new Format());
        return paragraph;
    }

    private static void Code(LeafBlock block, OpenXmlElement target)
    {
        var lines = block.Lines.Lines.Take(block.Lines.Count).Select(line => line.Slice.ToString()).ToList();
        if (lines.Count == 0)
            lines.Add(string.Empty);

        foreach (var line in lines)
            target.Append(new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "CodeBlock" }), new Run(new Text(line) { Space = SpaceProcessingModeValues.Preserve })));
    }

    private WordTable Table(MarkdownTable table)
    {
        var columns = table.ColumnDefinitions.Count;
        var grid = new TableGrid();
        for (var column = 0; column < columns; column++)
            grid.Append(new GridColumn());

        var word = new WordTable(
            new TableProperties(new TableStyle { Val = "TableGrid" }, new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }),
            grid);
        foreach (var row in table.OfType<MarkdownTableRow>())
        {
            var wordRow = new WordTableRow();
            if (row.IsHeader)
                wordRow.Append(new TableRowProperties(new TableHeader()));

            var index = 0;
            foreach (var cell in row.OfType<MarkdownTableCell>())
            {
                var paragraph = new Paragraph();
                var alignment = index < columns ? table.ColumnDefinitions[index].Alignment : null;
                if (alignment is not null)
                {
                    paragraph.Append(new ParagraphProperties(new Justification
                    {
                        Val = alignment switch
                        {
                            Markdig.Extensions.Tables.TableColumnAlign.Center => JustificationValues.Center,
                            Markdig.Extensions.Tables.TableColumnAlign.Right => JustificationValues.Right,
                            _ => JustificationValues.Left,
                        },
                    }));
                }

                foreach (var leaf in cell.OfType<ParagraphBlock>())
                    Inlines(leaf.Inline, paragraph, new Format { Bold = row.IsHeader });

                wordRow.Append(new WordTableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Auto }), paragraph));
                index++;
            }

            for (; index < columns; index++)
                wordRow.Append(new WordTableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Auto }), new Paragraph()));

            word.Append(wordRow);
        }

        return word;
    }

    private void Inlines(ContainerInline? container, OpenXmlElement target, Format format)
    {
        for (var inline = container?.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    target.Append(Run(literal.Content.ToString(), format));
                    break;
                case EmphasisInline emphasis:
                    Inlines(emphasis, target, emphasis.DelimiterChar switch
                    {
                        '~' when emphasis.DelimiterCount == 2 => format with { Strike = true },
                        '*' or '_' when emphasis.DelimiterCount >= 2 => format with { Bold = true },
                        '*' or '_' => format with { Italic = true },
                        _ => format,
                    });
                    break;
                case CodeInline code:
                    target.Append(Run(code.Content, format with { Code = true }));
                    break;
                case LinkInline { IsImage: true } image:
                    target.Append(Run("[" + PlainText(image) + "]", format));
                    break;
                case LinkInline link:
                    Link(link.Url, link, null, target, format);
                    break;
                case AutolinkInline auto:
                    Link(auto.IsEmail ? "mailto:" + auto.Url : auto.Url, null, auto.Url, target, format);
                    break;
                case LineBreakInline lineBreak:
                    target.Append(lineBreak.IsHard ? new Run(new Break()) : Run(" ", format));
                    break;
                case HtmlEntityInline entity:
                    target.Append(Run(entity.Transcoded.ToString(), format));
                    break;
                case HtmlInline html:
                    target.Append(Run(html.Tag, format));
                    break;
                case TaskList task:
                    target.Append(Run(task.Checked ? "☑" : "☐", format));
                    break;
                case ContainerInline nested:
                    Inlines(nested, target, format);
                    break;
            }
        }
    }

    /// <summary>A hyperlink for http, https and mailto targets or an in-document anchor; any other scheme stays plain text.</summary>
    private void Link(string? url, ContainerInline? label, string? text, OpenXmlElement target, Format format)
    {
        OpenXmlElement? hyperlink = null;
        if (!string.IsNullOrWhiteSpace(url))
        {
            if (url.StartsWith('#'))
            {
                hyperlink = new Hyperlink { Anchor = url[1..], History = true };
            }
            else if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto")
            {
                hyperlink = new Hyperlink { Id = main.AddHyperlinkRelationship(uri, true).Id, History = true };
            }
        }

        var holder = hyperlink ?? target;
        var linkFormat = hyperlink is null ? format : format with { Link = true };
        if (label is not null)
            Inlines(label, holder, linkFormat);
        else
            holder.Append(Run(text ?? url ?? string.Empty, linkFormat));

        if (hyperlink is not null)
            target.Append(hyperlink);
    }

    private static Run Run(string text, Format format)
    {
        var properties = new RunProperties();
        if (format.Link)
            properties.Append(new RunStyle { Val = "Hyperlink" });
        else if (format.Code)
            properties.Append(new RunStyle { Val = "CodeChar" });
        if (format.Bold)
            properties.Append(new Bold());
        if (format.Italic)
            properties.Append(new Italic());
        if (format.Strike)
            properties.Append(new Strike());

        var run = new Run();
        if (properties.HasChildren)
            run.Append(properties);

        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static string PlainText(ContainerInline container)
        => string.Concat(container.Descendants<LiteralInline>().Select(literal => literal.Content.ToString()));

    /// <summary>Represents where a block sits: list depth (-1 outside lists), its numbering instance, and quoting.</summary>
    private sealed record Place
    {
        public int Depth { get; init; } = -1;

        public int? Numbering { get; init; }

        public bool Quote { get; init; }
    }

    /// <summary>Represents the character formatting in effect.</summary>
    private sealed record Format
    {
        public bool Bold { get; init; }

        public bool Italic { get; init; }

        public bool Strike { get; init; }

        public bool Code { get; init; }

        public bool Link { get; init; }
    }
}
