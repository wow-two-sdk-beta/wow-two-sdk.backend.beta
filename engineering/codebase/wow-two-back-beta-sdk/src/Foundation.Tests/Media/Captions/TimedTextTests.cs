using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Media.Captions;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Media.Captions;

/// <summary>Timed text slices to the part a reader asks for and renders as timestamped lines or paragraphs.</summary>
public sealed class TimedTextTests
{
    private static readonly IReadOnlyList<CaptionSegment> Segments =
    [
        Segment(0, 2.5, "Hello there."),
        Segment(3, 4, "General Kenobi."),
        Segment(70, 72, "A new topic."),
    ];

    [Fact]
    public void Slice_ShouldKeepTheOverlappingSpans_WhenAPartIsGiven()
    {
        var part = Segments.Slice(TimeSpan.FromSeconds(3.5), TimeSpan.FromSeconds(71));

        part.Select(segment => segment.Text).Should().Equal("General Kenobi.", "A new topic.");
    }

    [Fact]
    public void Slice_ShouldKeepNothing_WhenThePartIsEmpty()
    {
        Segments.Slice(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10)).Should().BeEmpty();
    }

    [Fact]
    public void Write_ShouldStartEachLineWithItsClock_WhenTimestampsAreOn()
    {
        TimedTextRenderer.Write(Segments)
            .Should().Be("[0:00] Hello there.\n[0:03] General Kenobi.\n[1:10] A new topic.\n");
    }

    [Fact]
    public void Write_ShouldBreakParagraphsAtPauses_WhenTimestampsAreOff()
    {
        TimedTextRenderer.Write(Segments, new TimedTextOptions { HasTimestamps = false })
            .Should().Be("Hello there. General Kenobi.\n\nA new topic.\n");
    }

    [Fact]
    public void ToParagraphs_ShouldBreakALongParagraph_WhenSpeechRunsPastTheLength()
    {
        var talk = Enumerable.Range(0, 70).Select(second => Segment(second, second + 1, $"w{second}")).ToList();

        talk.ToParagraphs().Should().HaveCount(2);
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(65.9, "1:05")]
    [InlineData(3_723, "1:02:03")]
    [InlineData(-4, "0:00")]
    public void ToClock_ShouldReadLikeYouTube_WhenAnOffsetIsGiven(double seconds, string clock)
    {
        CaptionClockMapper.ToClock(TimeSpan.FromSeconds(seconds)).Should().Be(clock);
    }

    private static CaptionSegment Segment(double start, double end, string text) => new()
    {
        Start = TimeSpan.FromSeconds(start),
        End = TimeSpan.FromSeconds(end),
        Text = text,
    };
}
