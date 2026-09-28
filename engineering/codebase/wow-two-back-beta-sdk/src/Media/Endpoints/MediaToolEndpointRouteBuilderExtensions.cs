using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Media.Images;
using WoW.Two.Sdk.Backend.Beta.Media.Pdf;
using WoW.Two.Sdk.Backend.Beta.Web.Contracts;

namespace WoW.Two.Sdk.Backend.Beta.Media.Endpoints;

/// <summary>
/// Maps the image and PDF tools as multipart HTTP endpoints, so a file-tool product is one line of routing. Specs travel
/// as a JSON form field named <c>spec</c> in the same shape as the C# records; files come back as downloads.
/// </summary>
public static class MediaToolEndpointRouteBuilderExtensions
{
    private static readonly JsonSerializerOptions SpecJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    /// Maps <c>images/probe</c>, <c>images/edit</c>, <c>images/collage</c>, <c>images/metadata</c> and <c>images/analyze</c> under
    /// <paramref name="endpoints"/>; needs <c>AddImageProcessing</c>. Returns the group for authorization or rate limits.
    /// </summary>
    /// <param name="endpoints">The route builder or group.</param>
    public static RouteGroupBuilder MapImageToolEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("images").DisableAntiforgery();
        group.MapPost("probe", async (IFormFile file, IImageService images, CancellationToken ct)
            => Results.Ok(ApiResponse<ImageProbeResult>.Ok(await images.ProbeAsync(file.OpenReadStream(), ct))));
        group.MapPost("edit", async (IFormFile file, [FromForm] string? spec, IFormFileCollection files, IImageService images, CancellationToken ct) =>
        {
            var edit = Spec<ImageEditSpec>(spec) ?? new ImageEditSpec();
            if (files.GetFile("watermark") is { } logo)
                edit = edit with { Watermark = (edit.Watermark ?? new ImageWatermarkSpec { Content = [] }) with { Content = await ReadAsync(logo, ct) } };
            var result = await images.EditAsync(file.OpenReadStream(), edit, ct);
            return Results.File(result.Content, result.ContentType, Rename(file.FileName, result.Extension));
        });
        group.MapPost("collage", async (IFormFileCollection files, [FromForm] string? spec, IImageService images, CancellationToken ct) =>
        {
            var result = await images.CollageAsync([.. files.Select(part => part.OpenReadStream())], Spec<CollageSpec>(spec) ?? new CollageSpec(), ct);
            return Results.File(result.Content, result.ContentType, $"collage.{result.Extension}");
        });
        group.MapPost("metadata", async (IFormFile file, IImageService images, CancellationToken ct)
            => Results.Ok(ApiResponse<ImageMetadataResult>.Ok(await images.ReadMetadataAsync(file.OpenReadStream(), ct))));
        group.MapPost("analyze", async (IFormFile file, IImageService images, CancellationToken ct)
            => Results.Ok(ApiResponse<ImageAnalysisResult>.Ok(await images.AnalyzeAsync(file.OpenReadStream(), ct))));
        return group;
    }

    /// <summary>
    /// Maps <c>pdf/info</c>, <c>merge</c>, <c>split</c> (a ZIP), <c>extract</c>, <c>remove</c>, <c>rotate</c>,
    /// <c>from-images</c>, <c>compress</c>, <c>metadata</c>, <c>encrypt</c>, <c>decrypt</c>, <c>text</c>, <c>form</c>,
    /// <c>form/fill</c>, <c>watermark</c> and
    /// <c>page-numbers</c> under <paramref name="endpoints"/>; needs <c>AddPdfProcessing</c>. Page lists travel as
    /// <c>pages=1-3,5</c>. Returns the group for authorization or rate limits.
    /// </summary>
    /// <param name="endpoints">The route builder or group.</param>
    public static RouteGroupBuilder MapPdfToolEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("pdf").DisableAntiforgery();
        group.MapPost("info", async (IFormFile file, [FromForm] string? password, IPdfService pdf, CancellationToken ct)
            => Results.Ok(ApiResponse<PdfInfoResult>.Ok(await pdf.ReadInfoAsync(file.OpenReadStream(), password, ct))));
        group.MapPost("merge", async (IFormFileCollection files, IPdfService pdf, CancellationToken ct)
            => Download(await pdf.MergeAsync([.. files.Select(part => part.OpenReadStream())], ct), "merged.pdf"));
        group.MapPost("split", async (IFormFile file, [FromForm] int pagesPerPart, IPdfService pdf, CancellationToken ct) =>
        {
            var parts = await pdf.SplitAsync(file.OpenReadStream(), pagesPerPart, ct);
            return Results.File(Zip(parts, Path.GetFileNameWithoutExtension(file.FileName)), "application/zip", Rename(file.FileName, "zip"));
        });
        group.MapPost("extract", async (IFormFile file, [FromForm] string pages, IPdfService pdf, CancellationToken ct)
            => Download(await pdf.ExtractPagesAsync(file.OpenReadStream(), PdfPageRange.Parse(pages), ct), file.FileName));
        group.MapPost("remove", async (IFormFile file, [FromForm] string pages, IPdfService pdf, CancellationToken ct)
            => Download(await pdf.RemovePagesAsync(file.OpenReadStream(), PdfPageRange.Parse(pages), ct), file.FileName));
        group.MapPost("rotate", async (IFormFile file, [FromForm] int degrees, [FromForm] string? pages, IPdfService pdf, CancellationToken ct)
            => Download(await pdf.RotatePagesAsync(file.OpenReadStream(), degrees, pages is null ? null : PdfPageRange.Parse(pages), ct), file.FileName));
        group.MapPost("from-images", async (IFormFileCollection files, [FromForm] string? spec, IPdfService pdf, CancellationToken ct)
            => Download(await pdf.FromImagesAsync([.. files.Select(part => part.OpenReadStream())], Spec<PdfImagesSpec>(spec) ?? new PdfImagesSpec(), ct), "images.pdf"));
        group.MapPost("compress", async (IFormFile file, [FromForm] string? spec, IPdfService pdf, CancellationToken ct)
            => Download(await pdf.CompressAsync(file.OpenReadStream(), Spec<PdfCompressSpec>(spec) ?? new PdfCompressSpec(), ct), file.FileName));
        group.MapPost("metadata", async (IFormFile file, [FromForm] string spec, IPdfService pdf, CancellationToken ct)
            => Download(await pdf.UpdateMetadataAsync(file.OpenReadStream(), Required<PdfMetadataSpec>(spec), ct), file.FileName));
        group.MapPost("encrypt", async (IFormFile file, [FromForm] string spec, IPdfService pdf, CancellationToken ct)
            => Download(await pdf.EncryptAsync(file.OpenReadStream(), Required<PdfEncryptSpec>(spec), ct), file.FileName));
        group.MapPost("decrypt", async (IFormFile file, [FromForm] string password, IPdfService pdf, CancellationToken ct)
            => Download(await pdf.DecryptAsync(file.OpenReadStream(), password, ct), file.FileName));
        group.MapPost("text", async (IFormFile file, [FromForm] string? pages, [FromForm] string? password, IPdfService pdf, CancellationToken ct)
            => Results.Ok(ApiResponse<PdfTextResult>.Ok(await pdf.ExtractTextAsync(file.OpenReadStream(), pages is null ? null : PdfPageRange.Parse(pages), password, ct))));
        group.MapPost("form", async (IFormFile file, IPdfService pdf, CancellationToken ct)
            => Results.Ok(ApiResponse<IReadOnlyList<PdfFormField>>.Ok(await pdf.ReadFormAsync(file.OpenReadStream(), ct))));
        group.MapPost("form/fill", async (IFormFile file, [FromForm] string spec, IPdfService pdf, CancellationToken ct)
            => Download(await pdf.FillFormAsync(file.OpenReadStream(), Required<PdfFormFillSpec>(spec), ct), file.FileName));
        group.MapPost("watermark", async (IFormFile file, [FromForm] string spec, IPdfService pdf, CancellationToken ct)
            => Download(await pdf.AddWatermarkAsync(file.OpenReadStream(), Required<PdfWatermarkSpec>(spec), ct), file.FileName));
        group.MapPost("page-numbers", async (IFormFile file, [FromForm] string? spec, IPdfService pdf, CancellationToken ct)
            => Download(await pdf.AddPageNumbersAsync(file.OpenReadStream(), Spec<PdfPageNumberSpec>(spec) ?? new PdfPageNumberSpec(), ct), file.FileName));
        return group;
    }

    private static IResult Download(PdfResult result, string name) => Results.File(result.Content, result.ContentType, Rename(name, "pdf"));

    /// <summary>The spec a form field carries as JSON; null when the field is absent.</summary>
    private static T? Spec<T>(string? json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json, SpecJson);
        }
        catch (JsonException exception)
        {
            throw AppErrorFactory.Validation($"The spec is not a valid {typeof(T).Name}: {exception.Message}").ToException();
        }
    }

    private static T Required<T>(string? json)
        where T : class
        => Spec<T>(json) ?? throw AppErrorFactory.Validation($"The form needs a spec field holding a {typeof(T).Name}.").ToException();

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static byte[] Zip(IReadOnlyList<PdfResult> parts, string stem)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var index = 0; index < parts.Count; index++)
            {
                using var entry = archive.CreateEntry($"{(stem.Length == 0 ? "part" : stem)}-{index + 1}.pdf", CompressionLevel.Fastest).Open();
                entry.Write(parts[index].Content);
            }
        }

        return buffer.ToArray();
    }

    private static string Rename(string? fileName, string extension)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return $"{(string.IsNullOrWhiteSpace(stem) ? "result" : stem)}.{extension}";
    }
}
