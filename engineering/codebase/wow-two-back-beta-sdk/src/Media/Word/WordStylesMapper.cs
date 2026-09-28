using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace WoW.Two.Sdk.Backend.Beta.Media.Word;

/// <summary>Maps typography to the style and numbering definitions a built document uses.</summary>
internal static class WordStylesMapper
{
    /// <summary>The bullet list numbering instance every bulleted list shares.</summary>
    public const int BulletNumberingId = 1;

    /// <summary>The abstract definition ordered lists start new instances of.</summary>
    public const int OrderedAbstractId = 1;

    private static readonly string[] BulletGlyphs = ["•", "◦", "▪"];
    private static readonly NumberFormatValues[] OrderedFormats = [NumberFormatValues.Decimal, NumberFormatValues.LowerLetter, NumberFormatValues.LowerRoman];
    private static readonly double[] HeadingPoints = [20, 16, 14, 12, 11, 11];

    /// <summary>Normal, Title, Heading 1–6, Quote, Code Block, List Paragraph, Hyperlink, Code and Table Grid.</summary>
    public static Styles Styles(string font, double points, string codeFont)
    {
        var styles = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(Fonts(font), new FontSize { Val = HalfPoints(points) }, new FontSizeComplexScript { Val = HalfPoints(points) })),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(new SpacingBetweenLines { After = "160", Line = "264", LineRule = LineSpacingRuleValues.Auto }))),
            new Style(new StyleName { Val = "Normal" }, new PrimaryStyle()) { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true },
            Paragraph("Title", "Title", new StyleRunProperties(new Bold(), new FontSize { Val = HalfPoints(26) }), new StyleParagraphProperties(new SpacingBetweenLines { After = "240" })));
        for (var level = 1; level <= 6; level++)
        {
            styles.Append(Paragraph(
                $"Heading{level}",
                $"heading {level}",
                new StyleRunProperties(new Bold(), new FontSize { Val = HalfPoints(HeadingPoints[level - 1]) }),
                new StyleParagraphProperties(new KeepNext(), new SpacingBetweenLines { Before = level == 1 ? "360" : "240", After = "80" }, new OutlineLevel { Val = level - 1 })));
        }

        styles.Append(
            Paragraph(
                "Quote",
                "Quote",
                new StyleRunProperties(new Italic(), new Color { Val = "555555" }),
                new StyleParagraphProperties(
                    new ParagraphBorders(new LeftBorder { Val = BorderValues.Single, Size = 12, Space = 8, Color = "BBBBBB" }),
                    new Indentation { Left = "360" })),
            Paragraph(
                "CodeBlock",
                "Code Block",
                new StyleRunProperties(Fonts(codeFont), new FontSize { Val = HalfPoints(points * 0.9) }),
                new StyleParagraphProperties(
                    new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "F3F4F6" },
                    new SpacingBetweenLines { After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto },
                    new ContextualSpacing())),
            Paragraph("ListParagraph", "List Paragraph", new StyleRunProperties(), new StyleParagraphProperties(new SpacingBetweenLines { After = "60" }, new Indentation { Left = "720" }, new ContextualSpacing())),
            new Style(new StyleName { Val = "Hyperlink" }, new UIPriority { Val = 99 }, new StyleRunProperties(new Color { Val = "0563C1" }, new Underline { Val = UnderlineValues.Single }))
            {
                Type = StyleValues.Character,
                StyleId = "Hyperlink",
            },
            new Style(new StyleName { Val = "Code" }, new StyleRunProperties(Fonts(codeFont), new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "F3F4F6" }))
            {
                Type = StyleValues.Character,
                StyleId = "CodeChar",
            },
            new Style(
                new StyleName { Val = "Table Grid" },
                new StyleParagraphProperties(new SpacingBetweenLines { After = "0" }),
                new StyleTableProperties(
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
                        new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
                        new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
                        new RightBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" }),
                    new TableCellMarginDefault(
                        new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new TableCellLeftMargin { Width = 108, Type = TableWidthValues.Dxa },
                        new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                        new TableCellRightMargin { Width = 108, Type = TableWidthValues.Dxa })))
            {
                Type = StyleValues.Table,
                StyleId = "TableGrid",
            });
        return styles;
    }

    /// <summary>A bullet definition and an ordered definition over nine levels, and the shared bullet instance.</summary>
    public static Numbering Numbering()
    {
        var bullets = new AbstractNum(new MultiLevelType { Val = MultiLevelValues.HybridMultilevel }) { AbstractNumberId = 0 };
        var ordered = new AbstractNum(new MultiLevelType { Val = MultiLevelValues.HybridMultilevel }) { AbstractNumberId = OrderedAbstractId };
        for (var level = 0; level < 9; level++)
        {
            bullets.Append(Level(level, NumberFormatValues.Bullet, BulletGlyphs[level % BulletGlyphs.Length]));
            ordered.Append(Level(level, OrderedFormats[level % OrderedFormats.Length], $"%{level + 1}."));
        }

        return new Numbering(bullets, ordered, new NumberingInstance(new AbstractNumId { Val = 0 }) { NumberID = BulletNumberingId });
    }

    /// <summary>A new ordered instance restarting at <paramref name="start"/>.</summary>
    public static NumberingInstance OrderedInstance(int id, int start)
        => new(new AbstractNumId { Val = OrderedAbstractId }, new LevelOverride(new StartOverrideNumberingValue { Val = start }) { LevelIndex = 0 }) { NumberID = id };

    private static Level Level(int index, NumberFormatValues format, string text)
        => new(
            new StartNumberingValue { Val = 1 },
            new NumberingFormat { Val = format },
            new LevelText { Val = text },
            new LevelJustification { Val = LevelJustificationValues.Left },
            new PreviousParagraphProperties(new Indentation { Left = (720 * (index + 1)).ToString(CultureInfo.InvariantCulture), Hanging = "360" }))
        {
            LevelIndex = index,
        };

    private static Style Paragraph(string id, string name, StyleRunProperties run, StyleParagraphProperties paragraph)
        => new(new StyleName { Val = name }, new BasedOn { Val = "Normal" }, new NextParagraphStyle { Val = "Normal" }, new PrimaryStyle(), paragraph, run)
        {
            Type = StyleValues.Paragraph,
            StyleId = id,
        };

    private static RunFonts Fonts(string font) => new() { Ascii = font, HighAnsi = font, EastAsia = font, ComplexScript = font };

    private static string HalfPoints(double points) => ((int)Math.Round(points * 2)).ToString(CultureInfo.InvariantCulture);
}
