using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Backend.Beta.Meta;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;
using WoW.Two.Sdk.Backend.Beta.Storage.FileSystem;
using WoW.Two.Sdk.Backend.Beta.Storage.Transfer;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Storage;

/// <summary>HMAC-signed transfer URLs over local storage: direct upload and download, and every way a URL is refused.</summary>
public sealed class BlobTransferTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wow2-transfer-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero));
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.AddApiDefaults(options =>
        {
            options.EnableHttpsRedirection = false;
            options.EnableOtlpExporters = false;
            options.ExposeOpenApi = false;
            options.EnableRateLimiting = false;
        });
        builder.Services.AddSingleton<TimeProvider>(_time);
        builder.Services.AddLocalBlobStorage(_root);
        builder.Services.AddBlobTransferUrls(o =>
        {
            o.BaseUrl = new Uri("http://localhost/files");
            o.SigningKey = "0123456789abcdef0123456789abcdef";
            o.MaxUploadBytes = 1024;
        });
        _app = builder.Build();
        _app.UseApiDefaults();
        _app.MapBlobTransferEndpoints();
        await _app.StartAsync();
        _client = _app.GetTestServer().CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task SignedUrls_ShouldUploadAndDownloadDirectly()
    {
        var urls = _app.Services.GetRequiredService<IBlobUrlIssuer>();
        var upload = await urls.IssueWriteUrlAsync("reports/q3 summary.pdf", TimeSpan.FromMinutes(10), "application/pdf");
        upload.Method.Should().Be("PUT");
        upload.Headers.Should().ContainKey("Content-Type").WhoseValue.Should().Be("application/pdf");

        (await SendAsync(HttpMethod.Put, upload.Url, "%PDF-1.7 body"u8.ToArray(), "application/pdf")).StatusCode.Should().Be(HttpStatusCode.OK);

        var download = await urls.IssueReadUrlAsync("reports/q3 summary.pdf", TimeSpan.FromMinutes(10), "Q3 отчёт.pdf");
        using var response = await _client.GetAsync(download.Url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("%PDF-1.7 body");
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("Q3 отчёт.pdf");
    }

    [Fact]
    public async Task AlteredExpiredOrMismatchedRequests_ShouldBeRefused()
    {
        var urls = _app.Services.GetRequiredService<IBlobUrlIssuer>();
        var upload = await urls.IssueWriteUrlAsync("a/b.png", TimeSpan.FromMinutes(1), "image/png");

        (await SendAsync(HttpMethod.Put, upload.Url, [1, 2, 3], "image/jpeg")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "another media type");
        (await SendAsync(HttpMethod.Put, new Uri(upload.Url.ToString().Replace("a/b.png", "a/c.png", StringComparison.Ordinal)), [1], "image/png"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "another path");
        (await SendAsync(HttpMethod.Put, upload.Url, new byte[2048], "image/png")).StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);

        (await SendAsync(HttpMethod.Put, upload.Url, [1, 2, 3], "image/png")).StatusCode.Should().Be(HttpStatusCode.OK);
        var download = await urls.IssueReadUrlAsync("a/b.png", TimeSpan.FromMinutes(1));
        (await _client.GetAsync(new Uri(download.Url + "x"))).StatusCode.Should().Be(HttpStatusCode.Forbidden, "an altered signature");
        (await _client.PutAsync(download.Url, new ByteArrayContent([9]))).StatusCode.Should().Be(HttpStatusCode.Forbidden, "a read URL cannot write");

        _time.Advance(TimeSpan.FromMinutes(2));
        (await _client.GetAsync(download.Url)).StatusCode.Should().Be(HttpStatusCode.Forbidden, "the URL expired");
        await FluentActions.Awaiting(() => urls.IssueReadUrlAsync("a/b.png", TimeSpan.FromDays(8))).Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => urls.IssueReadUrlAsync("../etc/passwd", TimeSpan.FromMinutes(1))).Should().ThrowAsync<ArgumentException>();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, Uri url, byte[] body, string contentType)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var request = new HttpRequestMessage(method, url) { Content = content };
        return await _client.SendAsync(request);
    }
}
