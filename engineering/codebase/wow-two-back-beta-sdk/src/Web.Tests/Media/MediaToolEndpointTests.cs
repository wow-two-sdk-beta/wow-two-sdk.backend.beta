using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using WoW.Two.Sdk.Backend.Beta.Media.Endpoints;
using WoW.Two.Sdk.Backend.Beta.Media.Pdf;
using WoW.Two.Sdk.Backend.Beta.Meta;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Media;

/// <summary>The image and PDF tools over HTTP: multipart in, files or JSON out, refusals as problem details.</summary>
public sealed class MediaToolEndpointTests : IAsyncLifetime
{
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
        builder.Services.AddPdfProcessing();
        _app = builder.Build();
        _app.UseApiDefaults();
        var tools = _app.MapGroup("/tools");
        tools.MapImageToolEndpoints();
        tools.MapPdfToolEndpoints();
        await _app.StartAsync();
        _client = _app.GetTestServer().CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task EditEndpoint_ShouldReturnTheEditedFile()
    {
        using var form = Form(("file", "photo.jpg", Image(400, 200, SKEncodedImageFormat.Jpeg)));
        form.Add(new StringContent("""{"resize":{"width":100},"output":{"format":"webp"},"texts":[{"text":"Hi"}]}"""), "spec");

        using var response = await _client.PostAsync("/tools/images/edit", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("photo.webp");
        using var bitmap = SKBitmap.Decode(await response.Content.ReadAsByteArrayAsync());
        (bitmap.Width, bitmap.Height).Should().Be((100, 50));
    }

    [Fact]
    public async Task CollageAndAnalyze_ShouldTakeSeveralFiles_AndAnswerJson()
    {
        using var collageForm = Form(("files", "a.png", Image(300, 400)), ("files", "b.png", Image(300, 400)));
        collageForm.Add(new StringContent("""{"layout":"row","width":800}"""), "spec");
        using var collage = await _client.PostAsync("/tools/images/collage", collageForm);
        collage.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");

        using var analyzeForm = Form(("file", "a.png", Image(64, 64)));
        using var analysis = await _client.PostAsync("/tools/images/analyze", analyzeForm);
        (await Data(analysis)).GetProperty("blurHash").GetString().Should().HaveLength(28);
    }

    [Fact]
    public async Task PdfEndpoints_ShouldBuildSplitStampAndRead()
    {
        using var imagesForm = Form(("files", "1.png", Image(100, 100)), ("files", "2.png", Image(100, 100)), ("files", "3.png", Image(100, 100)));
        using var built = await _client.PostAsync("/tools/pdf/from-images", imagesForm);
        built.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var pdf = await built.Content.ReadAsByteArrayAsync();

        using var splitForm = Form(("file", "report.pdf", pdf));
        splitForm.Add(new StringContent("2"), "pagesPerPart");
        using var split = await _client.PostAsync("/tools/pdf/split", splitForm);
        using (var archive = new ZipArchive(new MemoryStream(await split.Content.ReadAsByteArrayAsync())))
            archive.Entries.Select(entry => entry.Name).Should().Equal("report-1.pdf", "report-2.pdf");

        using var numberForm = Form(("file", "report.pdf", pdf));
        using var numbered = await _client.PostAsync("/tools/pdf/page-numbers", numberForm);
        using var textForm = Form(("file", "report.pdf", await numbered.Content.ReadAsByteArrayAsync()));
        textForm.Add(new StringContent("2"), "pages");
        using var text = await _client.PostAsync("/tools/pdf/text", textForm);
        (await Data(text)).GetProperty("pages").GetProperty("2").GetString().Should().Contain("2 / 3");
    }

    [Fact]
    public async Task Refusals_ShouldAnswerProblemDetailsWithTheirReason()
    {
        using var garbage = Form(("file", "x.jpg", "not an image"u8.ToArray()));
        using var unreadable = await _client.PostAsync("/tools/images/edit", garbage);
        unreadable.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var imagesForm = Form(("files", "1.png", Image(50, 50)));
        var pdf = await (await _client.PostAsync("/tools/pdf/from-images", imagesForm)).Content.ReadAsByteArrayAsync();
        using var encryptForm = Form(("file", "a.pdf", pdf));
        encryptForm.Add(new StringContent("""{"userPassword":"open","ownerPassword":"owner"}"""), "spec");
        var locked = await (await _client.PostAsync("/tools/pdf/encrypt", encryptForm)).Content.ReadAsByteArrayAsync();

        using var infoForm = Form(("file", "a.pdf", locked));
        using var info = await _client.PostAsync("/tools/pdf/info", infoForm);
        info.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await info.Content.ReadAsStringAsync()).Should().Contain("password");

        using var badSpec = Form(("file", "a.pdf", pdf));
        badSpec.Add(new StringContent("{not json"), "spec");
        (await _client.PostAsync("/tools/pdf/watermark", badSpec)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static MultipartFormDataContent Form(params (string Field, string Name, byte[] Bytes)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (field, name, bytes) in files)
        {
            var part = new ByteArrayContent(bytes);
            part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(part, field, name);
        }

        return form;
    }

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private static byte[] Image(int width, int height, SKEncodedImageFormat format = SKEncodedImageFormat.Png)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
            canvas.Clear(SKColors.Coral);
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(format, 90).ToArray();
    }
}
