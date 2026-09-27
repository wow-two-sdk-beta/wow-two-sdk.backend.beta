using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Media.YouTube;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Media.YouTube;

/// <summary>Pasted text yields its unique video and playlist links in order, and the pieces that named none.</summary>
public sealed class YouTubeLinkExtractorTests
{
    [Fact]
    public void Extract_ShouldKeepPasteOrderAndDropRepeats_WhenLinksAreMixed()
    {
        const string text = """
            https://youtu.be/aaaaaaaaaaa, https://www.youtube.com/watch?v=bbbbbbbbbbb
            <https://www.youtube.com/shorts/aaaaaaaaaaa>
            https://www.youtube.com/playlist?list=PLabcdefghij0123 (https://www.youtube.com/playlist?list=PLabcdefghij0123)
            """;

        var found = YouTubeLinkExtractor.Extract(text);

        found.Links.Select(link => (link.Kind, link.Id)).Should().Equal(
            (YouTubeLinkKind.Video, "aaaaaaaaaaa"),
            (YouTubeLinkKind.Video, "bbbbbbbbbbb"),
            (YouTubeLinkKind.Playlist, "PLabcdefghij0123"));
        found.Unrecognized.Should().BeEmpty();
    }

    [Fact]
    public void Extract_ShouldReportUnrecognizedPieces_WhenTextNamesNoLink()
    {
        var found = YouTubeLinkExtractor.Extract("hello https://vimeo.com/123 https://youtu.be/aaaaaaaaaaa");

        found.Links.Should().ContainSingle().Which.Id.Should().Be("aaaaaaaaaaa");
        found.Unrecognized.Should().Equal("hello", "https://vimeo.com/123");
    }

    [Fact]
    public void Extract_ShouldFindNothing_WhenTextIsBlank()
    {
        var found = YouTubeLinkExtractor.Extract("  \n ");

        found.Links.Should().BeEmpty();
        found.Unrecognized.Should().BeEmpty();
    }
}
