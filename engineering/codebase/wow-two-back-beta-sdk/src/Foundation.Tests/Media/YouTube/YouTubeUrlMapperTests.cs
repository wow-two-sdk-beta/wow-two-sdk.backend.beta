using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Media.YouTube;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Media.YouTube;

/// <summary>Every published video and playlist URL form maps to its identifier, and identifiers map back.</summary>
public sealed class YouTubeUrlMapperTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?feature=share&v=dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ&t=42")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=abc")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ")]
    [InlineData(" https://music.youtube.com/watch?v=dQw4w9WgXcQ ")]
    public void TryGetVideoId_ShouldReadTheId_WhenUrlIsAYouTubeVideo(string url)
    {
        YouTubeUrlMapper.TryGetVideoId(url, out var videoId).Should().BeTrue();
        videoId.Should().Be("dQw4w9WgXcQ");
    }

    [Theory]
    [InlineData("https://vimeo.com/123456789")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    [InlineData("https://www.youtube.com/playlist?list=PLabcdefghij0123")]
    [InlineData("not a url")]
    [InlineData(null)]
    public void TryGetVideoId_ShouldFindNothing_WhenUrlNamesNoVideo(string? url)
    {
        YouTubeUrlMapper.TryGetVideoId(url, out var videoId).Should().BeFalse();
        videoId.Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://www.youtube.com/playlist?list=PLabcdefghij0123")]
    [InlineData("https://youtube.com/playlist?si=x&list=PLabcdefghij0123")]
    public void TryGetPlaylistId_ShouldReadTheId_WhenUrlIsAPlaylistPage(string url)
    {
        YouTubeUrlMapper.TryGetPlaylistId(url, out var playlistId).Should().BeTrue();
        playlistId.Should().Be("PLabcdefghij0123");
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLabcdefghij0123")]
    [InlineData("https://www.youtube.com/playlist?list=RDdQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/playlist?list=short")]
    [InlineData("https://vimeo.com/playlist?list=PLabcdefghij0123")]
    public void TryGetPlaylistId_ShouldFindNothing_WhenUrlIsNoExpandablePlaylist(string url)
    {
        YouTubeUrlMapper.TryGetPlaylistId(url, out _).Should().BeFalse();
    }

    [Fact]
    public void ToWatchUrl_ShouldOpenAtTheSecond_WhenAnOffsetIsGiven()
    {
        YouTubeUrlMapper.ToWatchUrl("dQw4w9WgXcQ", TimeSpan.FromSeconds(65.9))
            .Should().Be("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=65s");
    }

    [Fact]
    public void ToCanonicalUrl_ShouldReturnTheWatchUrl_WhenUrlIsAShortLink()
    {
        YouTubeUrlMapper.ToCanonicalUrl(" https://youtu.be/dQw4w9WgXcQ ")
            .Should().Be("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
    }

    [Fact]
    public void ToPlaylistUrl_ShouldReturnThePlaylistPage_WhenIdIsGiven()
    {
        YouTubeUrlMapper.ToPlaylistUrl("PLabcdefghij0123")
            .Should().Be("https://www.youtube.com/playlist?list=PLabcdefghij0123");
    }
}
