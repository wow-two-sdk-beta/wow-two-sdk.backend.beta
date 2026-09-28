using System.Text.Json;
using AwesomeAssertions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Media.Word;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Media;

/// <summary>Word documents: template filling across split runs, row repeats, Markdown in and out, and refusals.</summary>
public sealed class WordServiceTests
{
    private static readonly IWordService Word = Build();

    [Fact]
    public async Task FillTemplate_ShouldReplaceSplitPlaceholdersRepeatRowsAndKeepFormatting()
    {
        var data = JsonSerializer.SerializeToElement(new
        {
            company = "Analytical Engines Ltd",
            customer = new { name = "Ada Lovelace" },
            order = new { id = "A-17", total = 42.5 },
            lines = new[] { new { sku = "A1", qty = 2 }, new { sku = "B2", qty = 1 } },
            notes = "Leave at the door.\nRing twice.",
        });

        var filled = await Word.FillTemplateAsync(new MemoryStream(Template()), new WordTemplateSpec { Data = data });
        var text = (await Word.ExtractTextAsync(new MemoryStream(filled.Content))).Text;

        text.Should().Contain("Dear Ada Lovelace, your order A-17 ships soon.");
        text.Should().Contain("Sku\tQty\nA1\t2\nB2\t1\nTotal\t42.5");
        text.Should().Contain("Leave at the door.\nRing twice.");
        text.Should().NotContain("{{");

        using var document = WordprocessingDocument.Open(new MemoryStream(filled.Content), false);
        var main = document.MainDocumentPart!;
        main.HeaderParts.Single().Header!.InnerText.Should().Be("Analytical Engines Ltd");
        var bold = main.Document!.Body!.Descendants<Run>().Single(run => run.RunProperties?.Bold is not null);
        bold.InnerText.Should().Be("Ada Lovelace", "the value takes the formatting of the run the placeholder opened in");
    }

    [Fact]
    public async Task FillTemplate_ShouldRejectKeepOrEmptyMissingValues()
    {
        var data = JsonSerializer.SerializeToElement(new { customer = new { name = "Ada" }, lines = Array.Empty<object>() });

        var rejected = await FluentActions.Awaiting(() => Word.FillTemplateAsync(new MemoryStream(Template()), new WordTemplateSpec { Data = data }))
            .Should().ThrowAsync<WordRejectedException>();
        rejected.Which.Reason.Should().Be("word_template_value_missing");
        rejected.Which.Message.Should().Contain("Company").And.Contain("Order.Id").And.Contain("Notes");

        var kept = await Word.FillTemplateAsync(new MemoryStream(Template()), new WordTemplateSpec { Data = data, MissingValues = WordMissingValue.Keep });
        var keptText = (await Word.ExtractTextAsync(new MemoryStream(kept.Content))).Text;
        keptText.Should().Contain("Dear Ada, your order {{ Order.Id }} ships soon.");
        keptText.Should().NotContain("Lines[]", "an empty collection removes its row");

        var emptied = await Word.FillTemplateAsync(new MemoryStream(Template()), new WordTemplateSpec { Data = data, MissingValues = WordMissingValue.Empty });
        (await Word.ExtractTextAsync(new MemoryStream(emptied.Content))).Text.Should().Contain("Dear Ada, your order  ships soon.");
    }

    [Fact]
    public async Task FromMarkdown_ShouldBuildAValidDocumentThatReadsBackAsMarkdown()
    {
        const string Source = """
            # Quarterly report

            Revenue grew **12%** with *steady* churn; see [the dashboard](https://example.com/q3) and `sdk.pdf`.

            - Wins
              - Passkeys shipped
            - Risks

            3. Hire
            4. Ship

            > Keep the bar high.

            ```
            dotnet test
            ```

            | Area | Status |
            |:---|---:|
            | Identity | done |

            ---

            - [x] Webhooks
            """;

        var built = await Word.FromMarkdownAsync(Source, new WordDocumentSpec { Title = "Q3", Author = "Ops", Paper = WordPaperSize.Letter });

        using (var document = WordprocessingDocument.Open(new MemoryStream(built.Content), false))
        {
            new OpenXmlValidator(FileFormatVersions.Office2019).Validate(document).Select(error => $"{error.Path?.XPath}: {error.Description}").Should().BeEmpty();
            document.MainDocumentPart!.HyperlinkRelationships.Single().Uri.Should().Be(new Uri("https://example.com/q3"));
        }

        var markdown = (await Word.ExtractTextAsync(new MemoryStream(built.Content), WordTextFormat.Markdown)).Text;
        markdown.Should().StartWith("# Quarterly report\n\nRevenue grew **12%** with *steady* churn; see [the dashboard](https://example.com/q3) and `sdk.pdf`.");
        markdown.Should().Contain("- Wins\n  - Passkeys shipped\n- Risks");
        markdown.Should().Contain("- Risks\n\n1. Hire\n1. Ship", "a new list starts after a blank line");
        markdown.Should().Contain("> Keep the bar high.");
        markdown.Should().Contain("```\ndotnet test\n```");
        markdown.Should().Contain("| **Area** | **Status** |\n| --- | --- |\n| Identity | done |");
        markdown.Should().Contain("☑ Webhooks");

        var info = await Word.ReadInfoAsync(new MemoryStream(built.Content));
        info.Title.Should().Be("Q3");
        info.Author.Should().Be("Ops");
        info.Tables.Should().Be(1);
        info.Words.Should().BeGreaterThan(20);
    }

    [Fact]
    public async Task Inputs_ThatAreNotWordOrTooLarge_AreRefused()
    {
        var unreadable = await FluentActions.Awaiting(() => Word.ExtractTextAsync(new MemoryStream("not a zip"u8.ToArray())))
            .Should().ThrowAsync<WordRejectedException>();
        unreadable.Which.Reason.Should().Be("word_unreadable");

        var small = Build(o => o.MaxInputBytes = 512);
        var tooLarge = await FluentActions.Awaiting(() => small.ReadInfoAsync(new MemoryStream(Template())))
            .Should().ThrowAsync<WordRejectedException>();
        tooLarge.Which.Reason.Should().Be("word_too_large");
    }

    private static IWordService Build(Action<WordOptions>? configure = null)
        => new ServiceCollection().AddWordProcessing(configure).BuildServiceProvider().GetRequiredService<IWordService>();

    /// <summary>A template the way Word saves one: placeholders split across runs, a repeat row and a header.</summary>
    private static byte[] Template()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            var header = main.AddNewPart<HeaderPart>();
            header.Header = new Header(new Paragraph(Run("{{"), Run("Company}}")));
            main.Document = new Document(new Body(
                new Paragraph(Run("Dear "), Run("{{Cust", bold: true), Run("omer.Name}}"), Run(", your order "), Run("{{ Order.Id }}"), Run(" ships soon.")),
                new Table(
                    Row("Sku", "Qty"),
                    Row("{{Lines[].Sku}}", "{{Lines[].Qty}}"),
                    Row("Total", "{{Order.Total}}")),
                new Paragraph(Run("{{Notes}}")),
                new SectionProperties(new HeaderReference { Type = HeaderFooterValues.Default, Id = main.GetIdOfPart(header) })));
        }

        return stream.ToArray();
    }

    private static TableRow Row(params string[] cells)
        => new(cells.Select(cell => new TableCell(new Paragraph(Run(cell)))));

    private static Run Run(string text, bool bold = false)
    {
        var run = new Run();
        if (bold)
            run.Append(new RunProperties(new Bold()));

        run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }
}
