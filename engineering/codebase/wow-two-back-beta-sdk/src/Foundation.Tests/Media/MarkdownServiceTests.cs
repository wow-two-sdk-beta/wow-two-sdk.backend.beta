using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Media.Markdown;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Media;

/// <summary>Markdown rendering that is safe for user content, with the summaries a page needs.</summary>
public sealed class MarkdownServiceTests
{
    private const string Post = """
        ---
        title: "Release notes"
        tags: [sdk, pdf, "images"]
        date: 2026-09-28
        ---
        # What's new

        ## PDF tools

        Merge, split and **compress** files. See [docs](https://example.com/docs) or the [guide](/guide).

        | Tool | Status |
        |---|---|
        | Merge | done |

        - [x] Images
        - [ ] Passkeys
        """;

    private static readonly IMarkdownService Markdown = Build();

    [Fact]
    public void Render_ShouldProduceGithubFlavouredHtml_WithHeadingAnchorsAndFrontMatter()
    {
        var result = Markdown.Render(Post);

        result.Html.Should().Contain("<h1 id=\"whats-new\">").And.Contain("<table>").And.Contain("type=\"checkbox\"").And.Contain("<strong>compress</strong>");
        result.Html.Should().NotContain("Release notes", "front matter is data, not content");
        result.Headings.Should().BeEquivalentTo([
            new MarkdownHeading { Level = 1, Text = "What's new", Id = "whats-new" },
            new MarkdownHeading { Level = 2, Text = "PDF tools", Id = "pdf-tools" },
        ]);
        result.FrontMatter.Should().Contain("title", "Release notes").And.Contain("tags", "sdk, pdf, images").And.Contain("date", "2026-09-28");
    }

    [Fact]
    public void Links_ShouldOpenExternalOnesSafely_AndNeutralizeScriptSchemes()
    {
        var html = Markdown.Render("[docs](https://example.com) [local](/guide) [bad](javascript:alert(1)) [sneaky](JaVa Script:alert(1)) ![img](javascript:x)").Html;

        html.Should().Contain("href=\"https://example.com\"").And.Contain("rel=\"nofollow noopener noreferrer\"").And.Contain("target=\"_blank\"");
        html.Should().Contain("href=\"/guide\"");
        html.Should().NotContainEquivalentOf("javascript");
    }

    [Fact]
    public void RawHtml_ShouldBeEscapedByDefault_AndPassWhenTheHostTrustsIt()
    {
        const string source = "Hello <script>alert(1)</script> <b>bold</b>";

        Markdown.Render(source).Html.Should().NotContain("<script>").And.Contain("&lt;script&gt;");
        Build(o => o.AllowRawHtml = true).Render(source).Html.Should().Contain("<b>bold</b>");
    }

    [Fact]
    public void Summaries_ShouldCountWordsAndReadingTime()
    {
        var long_ = Markdown.Render(string.Join(' ', Enumerable.Repeat("word", 450)));

        (long_.WordCount, long_.ReadingMinutes).Should().Be((450, 3));
        Markdown.ToPlainText("**Bold** and [link](https://example.com)").Should().Be("Bold and link");
        Markdown.Render(string.Empty).ReadingMinutes.Should().Be(0);
    }

    private static IMarkdownService Build(Action<MarkdownOptions>? configure = null)
        => new ServiceCollection().AddMarkdown(configure).BuildServiceProvider().GetRequiredService<IMarkdownService>();
}
