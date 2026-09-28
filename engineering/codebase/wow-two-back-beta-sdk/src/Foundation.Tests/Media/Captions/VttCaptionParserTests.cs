using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Media.Captions;
using WoW.Two.Sdk.Backend.Beta.Media.Captions.Parsers;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Media.Captions;

/// <summary>WebVTT reads one segment per spoken line — written captions per cue, rolling ones per line.</summary>
public sealed class VttCaptionParserTests
{
    /// <summary>Shaped on YouTube's auto-caption WebVTT from yt-dlp: a placeholder line, then rolling cues.</summary>
    /// <remarks>Built line by line: a raw string literal would empty the one-space placeholder lines.</remarks>
    private static readonly string RollingCaptions = string.Join(
        '\n',
        "WEBVTT",
        "Kind: captions",
        "Language: en",
        "",
        "00:00:00.160 --> 00:00:02.270 align:start position:0%",
        " ",
        "hi<00:00:00.320><c> everyone</c><00:00:01.280><c> so</c>",
        "",
        "00:00:02.270 --> 00:00:02.280 align:start position:0%",
        "hi everyone so",
        " ",
        "",
        "00:00:02.280 --> 00:00:04.230 align:start position:0%",
        "hi everyone so",
        "a<00:00:02.800><c> talk</c><00:00:03.000><c> on</c><00:00:03.159><c> models</c>",
        "",
        "00:00:04.230 --> 00:00:04.240 align:start position:0%",
        "a talk on models",
        " ",
        "",
        "00:00:04.240 --> 00:00:06.389 align:start position:0%",
        "a talk on models",
        "just<00:00:04.400><c> kind</c><00:00:04.520><c> of</c>");

    private readonly VttCaptionParser parser = new();

    [Fact]
    public void Parse_ShouldKeepEachSpokenLineOnce_WhenYouTubeAutoCaptionsRoll()
    {
        var segments = parser.Parse(RollingCaptions);

        segments.Select(segment => segment.Text).Should().Equal("hi everyone so", "a talk on models", "just kind of");
        segments[0].Start.Should().Be(TimeSpan.FromMilliseconds(160));
        segments[1].Start.Should().Be(TimeSpan.FromMilliseconds(2280));
        segments[1].End.Should().Be(TimeSpan.FromMilliseconds(4230));
    }

    [Fact]
    public void Parse_ShouldKeepOneSegmentPerCue_WhenCaptionsAreWritten()
    {
        const string vtt = """
            WEBVTT

            1
            00:00:01.000 --> 00:00:03.000
            We're no strangers
            to love

            NOTE the chorus follows

            2
            00:00:03.000 --> 00:00:05.500
            You know the rules &amp; so do I
            """;

        var segments = parser.Parse(vtt);

        segments.Select(segment => segment.Text)
            .Should().Equal("We're no strangers to love", "You know the rules & so do I");
        segments[1].End.Should().Be(TimeSpan.FromMilliseconds(5500));
    }

    [Fact]
    public void Parse_ShouldDropAnIdenticalCue_WhenItRepeatsAtThePreviousEnd()
    {
        const string vtt = """
            WEBVTT

            00:00:01.000 --> 00:00:02.000
            Hello

            00:00:02.000 --> 00:00:03.000
            Hello

            00:00:04.000 --> 00:00:05.000
            Hello
            """;

        var segments = parser.Parse(vtt);

        segments.Select(segment => segment.Start.TotalSeconds).Should().Equal(1, 4);
    }

    [Fact]
    public void Parse_ShouldReturnNoSegments_WhenTheTextHoldsNoCues()
    {
        parser.Parse("WEBVTT\n\n").Should().BeEmpty();
        parser.Parse("   ").Should().BeEmpty();
    }
}
