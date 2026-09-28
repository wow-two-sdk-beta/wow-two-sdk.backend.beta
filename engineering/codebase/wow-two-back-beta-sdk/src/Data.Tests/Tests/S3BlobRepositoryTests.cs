using System.Diagnostics.CodeAnalysis;
using System.Text;
using Amazon.S3;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;
using WoW.Two.Sdk.Backend.Beta.Storage.S3;

namespace WoW.Two.Sdk.Backend.Beta.Data.Tests.Tests;

/// <summary>The S3 blob repository against Adobe S3Mock (MinIO no longer ships public images): save, read, info, list, delete, key prefix.</summary>
[SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Teardown runs in IAsyncLifetime.DisposeAsync, which xUnit invokes.")]
public sealed class S3BlobRepositoryTests : IAsyncLifetime
{
    private const string AccessKey = "test-access";
    private const string SecretKey = "test-secret";

    private readonly IContainer _s3 = new ContainerBuilder()
        .WithImage("adobe/s3mock:latest")
        .WithPortBinding(9090, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPath("/").ForPort(9090)))
        .Build();

    private ServiceProvider? _provider;

    public async Task InitializeAsync()
    {
        await _s3.StartAsync();
        var services = new ServiceCollection();
        services.AddS3BlobStorage(o =>
        {
            o.BucketName = "blobs";
            o.KeyPrefix = "app-a";
            o.ServiceUrl = new Uri($"http://{_s3.Hostname}:{_s3.GetMappedPublicPort(9090)}");
            o.AccessKey = AccessKey;
            o.SecretKey = SecretKey;
        });
        _provider = services.BuildServiceProvider();
        await _provider.GetRequiredService<IAmazonS3>().PutBucketAsync("blobs");
    }

    public async Task DisposeAsync()
    {
        if (_provider is not null)
            await _provider.DisposeAsync();
        await _s3.DisposeAsync();
    }

    [Fact]
    public async Task RoundTripsBlobsUnderTheKeyPrefix()
    {
        var blobs = _provider!.GetRequiredService<IBlobRepository>();

        await blobs.SaveAsync("avatars/u1.png", new MemoryStream(Encoding.UTF8.GetBytes("png-bytes")), "image/png");
        await blobs.SaveAsync("avatars/u2.png", new NonSeekableStream(Encoding.UTF8.GetBytes("second")), "image/png");
        await blobs.SaveAsync("docs/readme.txt", new MemoryStream(Encoding.UTF8.GetBytes("hello")));

        await using (var read = await blobs.OpenReadAsync("avatars/u1.png"))
        {
            Assert.NotNull(read);
            Assert.Equal("png-bytes", await new StreamReader(read).ReadToEndAsync());
        }

        var info = await blobs.GetInfoAsync("avatars/u2.png");
        Assert.Equal(new { Path = "avatars/u2.png", SizeBytes = 6L, ContentType = (string?)"image/png" }, new { info!.Path, info.SizeBytes, info.ContentType });

        var avatars = new List<string>();
        await foreach (var blob in blobs.ListAsync("avatars/"))
            avatars.Add(blob.Path);
        Assert.Equal(["avatars/u1.png", "avatars/u2.png"], avatars);

        Assert.True(await blobs.DeleteAsync("avatars/u1.png"));
        Assert.False(await blobs.DeleteAsync("avatars/u1.png"));
        Assert.Null(await blobs.OpenReadAsync("avatars/u1.png"));
        Assert.False(await blobs.ExistsAsync("avatars/u1.png"));

        var keys = await _provider!.GetRequiredService<IAmazonS3>().ListObjectsV2Async(new Amazon.S3.Model.ListObjectsV2Request { BucketName = "blobs" });
        Assert.All(keys.S3Objects, entry => Assert.StartsWith("app-a/", entry.Key, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PresignedUrls_ShouldUploadAndDownloadWithoutTheApi()
    {
        var urls = _provider!.GetRequiredService<IBlobUrlIssuer>();
        var upload = await urls.IssueWriteUrlAsync("direct/upload.txt", TimeSpan.FromMinutes(5), "text/plain");
        Assert.Equal("PUT", upload.Method);
        Assert.True(upload.Url.Query.Contains("X-Amz-Signature=", StringComparison.Ordinal), upload.Url.ToString());
        Assert.Contains("X-Amz-Expires=300", upload.Url.Query, StringComparison.Ordinal);
        Assert.Contains("/blobs/app-a/direct/upload.txt", upload.Url.AbsolutePath, StringComparison.Ordinal);

        using var http = new HttpClient();
        using var put = new HttpRequestMessage(HttpMethod.Put, upload.Url) { Content = new StringContent("straight to the bucket", Encoding.UTF8) };
        put.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        Assert.True((await http.SendAsync(put)).IsSuccessStatusCode);
        await using (var stored = await _provider!.GetRequiredService<IBlobRepository>().OpenReadAsync("direct/upload.txt"))
            Assert.Equal("straight to the bucket", await new StreamReader(stored!).ReadToEndAsync());

        var download = await urls.IssueReadUrlAsync("direct/upload.txt", TimeSpan.FromMinutes(5), "notes.txt");
        Assert.Contains("response-content-disposition=", download.Url.Query, StringComparison.Ordinal);
        Assert.Equal("straight to the bucket", await http.GetStringAsync(download.Url));
    }

    /// <summary>A read-only stream that hides its length, as a request body does.</summary>
    private sealed class NonSeekableStream(byte[] content) : Stream
    {
        private readonly MemoryStream _inner = new(content);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
