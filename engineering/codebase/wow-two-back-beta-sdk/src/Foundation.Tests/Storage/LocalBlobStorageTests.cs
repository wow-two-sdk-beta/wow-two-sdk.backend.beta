using System.Text;
using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;
using WoW.Two.Sdk.Backend.Beta.Storage.FileSystem;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Storage;

/// <summary>Local blob storage and path rules: folder-style prefixes list, unsafe paths and prefixes are refused.</summary>
public sealed class LocalBlobStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"blobs-{Guid.NewGuid():N}");

    [Fact]
    public async Task ListAsync_ShouldAcceptAFolderStylePrefix()
    {
        var blobs = new LocalFileBlobRepository(_root);
        await blobs.SaveAsync("avatars/u1.png", new MemoryStream(Encoding.UTF8.GetBytes("a")));
        await blobs.SaveAsync("avatarsets/x.png", new MemoryStream(Encoding.UTF8.GetBytes("b")));

        var listed = new List<string>();
        await foreach (var blob in blobs.ListAsync("avatars/"))
            listed.Add(blob.Path);

        listed.Should().Equal("avatars/u1.png");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    [InlineData("avatars/", "avatars/")]
    [InlineData("/avatars/u1", "avatars/u1")]
    [InlineData("a\\b/", "a/b/")]
    public void NormalizePrefix_ShouldKeepOneTrailingSlash(string? prefix, string? expected)
        => BlobStoragePathMapper.NormalizePrefix(prefix).Should().Be(expected);

    [Theory]
    [InlineData("../etc/")]
    [InlineData("a//b")]
    [InlineData("avatars//")]
    public void NormalizePrefix_ShouldRefuseUnsafeSegments(string prefix)
    {
        var act = () => BlobStoragePathMapper.NormalizePrefix(prefix);

        act.Should().Throw<ArgumentException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}
