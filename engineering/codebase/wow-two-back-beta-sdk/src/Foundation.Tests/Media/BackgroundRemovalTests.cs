using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using WoW.Two.Sdk.Backend.Beta.Media.BackgroundRemoval;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Media;

/// <summary>Worker boundaries use real raster bytes and in-memory HTTP responses.</summary>
public sealed class BackgroundRemovalTests
{
    [Fact]
    public async Task Unconfigured_ShouldReportUnavailableWithoutCallingNetwork()
    {
        var calls = 0;
        var service = Build((_, _) => { calls++; throw new InvalidOperationException(); }, new());
        (await service.GetStatusAsync()).Available.Should().BeFalse();
        var act = () => service.RemoveAsync(Png());
        (await act.Should().ThrowAsync<BackgroundRemovalException>()).Which.Code.Should().Be("worker_unavailable");
        calls.Should().Be(0);
    }

    [Theory]
    [InlineData("http://example.test")]
    [InlineData("https://user:pass@example.test")]
    [InlineData("https://example.test?key=x")]
    [InlineData("file:///tmp/image")]
    public async Task UnsafeConfiguration_ShouldFailClosed(string url)
    {
        var service = Build((_, _) => throw new InvalidOperationException(), new() { BaseUrl = url, ApiKey = "private-unit-test-key" });
        (await service.GetStatusAsync()).Available.Should().BeFalse();
    }

    [Fact]
    public async Task Remove_ShouldSendOnlyBytesToConfiguredWorker_AndPreserveShape()
    {
        var source = Png();
        var service = Build(async (request, token) => {
            request.RequestUri!.AbsoluteUri.Should().Be("http://127.0.0.1:8290/v1/remove-background");
            request.Headers.GetValues("X-Image-Worker-Key").Single().Should().Be("private-unit-test-key");
            (await request.Content!.ReadAsByteArrayAsync(token)).Should().Equal(source);
            return Response(source);
        });
        var result = await service.RemoveAsync(source);
        result.Content.Should().Equal(source);
        (result.Width, result.Height, result.Model).Should().Be((2, 3, "u2net"));
    }

    [Fact]
    public async Task Readiness_ShouldRequireActualModelAndBoundJson()
    {
        foreach (var body in new[] { "{}", "[]", "{\"ready\":false}", new string('x', 4097), "{\"ready\":true,\"model\":\"\"}" })
        {
            var service = Build((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }));
            (await service.GetStatusAsync()).Available.Should().BeFalse();
        }
        var ready = Build((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"ready\":true,\"model\":\"u2net\"}") }));
        (await ready.GetStatusAsync()).Should().Be(new BackgroundRemovalStatus(true, "u2net"));
    }

    [Fact]
    public async Task Output_ShouldRejectWrongShapeOversizeMalformedAndWrongType()
    {
        foreach (var response in new[] { Response(Png(3, 3)), Response(Encoding.UTF8.GetBytes("broken")), Response(Png(), "image/jpeg"), Response(new byte[1025]) })
        {
            var service = Build((_, _) => Task.FromResult(response), new() { BaseUrl = "http://127.0.0.1:8290", ApiKey = "private-unit-test-key", MaximumBytes = 1024 });
            var act = () => service.RemoveAsync(Png());
            (await act.Should().ThrowAsync<BackgroundRemovalException>()).Which.Code.Should().Be("invalid_worker_output");
        }
    }

    [Fact]
    public async Task Cancellation_ShouldPropagate_AndRedirectShouldNotCountAsSuccess()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var service = Build((_, token) => Task.FromCanceled<HttpResponseMessage>(token));
        var act = () => service.RemoveAsync(Png(), cancelled.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
        var redirect = Build((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect)));
        (await redirect.GetStatusAsync()).Available.Should().BeFalse();
        var redirectAct = () => redirect.RemoveAsync(Png());
        (await redirectAct.Should().ThrowAsync<BackgroundRemovalException>()).Which.Code.Should().Be("worker_unavailable");
    }

    [Fact]
    public void RasterGuard_ShouldRejectCorruptionAndPixelBombBeforeAcceptingBytes()
    {
        Action truncated = () => RasterImageGuard.Validate(Png()[..30]);
        truncated.Should().Throw<BackgroundRemovalException>();
        Action pixels = () => RasterImageGuard.Validate(Png(11, 11), maximumPixels: 100);
        pixels.Should().Throw<BackgroundRemovalException>();
        Action size = () => RasterImageGuard.Validate(Png(), maximumBytes: 4);
        size.Should().Throw<BackgroundRemovalException>();
        RasterImageGuard.Validate(Png()).Should().Be(new RasterImageInfo(2, 3, "image/png"));
    }

    private static HttpBackgroundRemovalService Build(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send, BackgroundRemovalOptions? options = null) =>
        new(new HttpClient(new Handler(send)), Microsoft.Extensions.Options.Options.Create(options ?? new() { BaseUrl = "http://127.0.0.1:8290", ApiKey = "private-unit-test-key" }));
    private static HttpResponseMessage Response(byte[] bytes, string type = "image/png")
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(type);
        response.Headers.Add("X-Image-Model", "u2net");
        return response;
    }
    private static byte[] Png(int width = 2, int height = 3)
    {
        using var bitmap = new SKBitmap(width, height); bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }
}
