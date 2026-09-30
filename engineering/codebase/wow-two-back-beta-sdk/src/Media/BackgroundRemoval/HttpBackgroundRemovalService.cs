using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Media.BackgroundRemoval;

/// <summary>Bounded client for an explicitly configured private segmentation worker.</summary>
public sealed class HttpBackgroundRemovalService(HttpClient client, IOptions<BackgroundRemovalOptions> options) : IBackgroundRemovalService
{
    /// <inheritdoc />
    public async Task<BackgroundRemovalStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!Configured(out var origin)) return new(false);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            using var request = Request(HttpMethod.Get, new Uri(origin!, "/health/ready"));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) return new(false);
            using var json = JsonDocument.Parse(await ReadAsync(response.Content, 4096, timeout.Token));
            var ready = json.RootElement.TryGetProperty("ready", out var r) && r.ValueKind == JsonValueKind.True;
            var model = json.RootElement.TryGetProperty("model", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            return ready && ModelValid(model) ? new(true, model) : new(false);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or BackgroundRemovalException or InvalidOperationException || e is OperationCanceledException && !cancellationToken.IsCancellationRequested) { return new(false); }
    }

    /// <inheritdoc />
    public async Task<BackgroundRemovalResult> RemoveAsync(byte[] source, CancellationToken cancellationToken = default)
    {
        if (!Configured(out var origin)) throw Unavailable();
        var limits = options.Value;
        var input = RasterImageGuard.Validate(source, limits.MaximumBytes, limits.MaximumPixels, limits.MaximumDimension);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(limits.Timeout);
        try
        {
            using var request = Request(HttpMethod.Post, new Uri(origin!, "/v1/remove-background"));
            request.Content = new ByteArrayContent(source);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(input.ContentType);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) throw Unavailable();
            var models = response.Headers.TryGetValues("X-Image-Model", out var header) ? header.ToArray() : [];
            var model = models.Length == 1 ? models[0] : null;
            if (response.Content.Headers.ContentType?.MediaType != "image/png" || !ModelValid(model)) throw InvalidOutput();
            var bytes = await ReadAsync(response.Content, limits.MaximumBytes, timeout.Token);
            RasterImageInfo output;
            try { output = RasterImageGuard.Validate(bytes, limits.MaximumBytes, limits.MaximumPixels, limits.MaximumDimension); }
            catch (BackgroundRemovalException) { throw InvalidOutput(); }
            if (output.ContentType != "image/png" || input.Width != output.Width || input.Height != output.Height) throw InvalidOutput();
            return new(bytes, output.Width, output.Height, model!);
        }
        catch (Exception e) when (e is HttpRequestException || e is OperationCanceledException && !cancellationToken.IsCancellationRequested) { throw Unavailable(); }
    }

    private bool Configured(out Uri? uri)
    {
        var o = options.Value;
        return Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out uri) && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.AbsolutePath == "/"
            && (uri.Scheme == "https" || uri.Scheme == "http" && IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address))
            && o.ApiKey is {Length: >= 16 and <= 512} && !o.ApiKey.Any(char.IsControl)
            && o.MaximumBytes is > 0 and <= 64 * 1024 * 1024 && o.MaximumPixels is > 0 and <= 50_000_000
            && o.MaximumDimension is > 0 and <= 16384 && o.Timeout > TimeSpan.Zero && o.Timeout <= TimeSpan.FromMinutes(2);
    }
    private HttpRequestMessage Request(HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-Image-Worker-Key", options.Value.ApiKey);
        return request;
    }
    private static bool ModelValid(string? model) => model is {Length: > 0 and <= 100} && model.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
    private static async Task<byte[]> ReadAsync(HttpContent content, int limit, CancellationToken token)
    {
        if (content.Headers.ContentLength > limit) throw InvalidOutput();
        await using var stream = await content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(chunk, token)) > 0)
        {
            if (buffer.Length + count > limit) throw InvalidOutput();
            buffer.Write(chunk, 0, count);
        }
        return buffer.ToArray();
    }
    private static BackgroundRemovalException Unavailable() => new("worker_unavailable", "Background removal is unavailable. Your original image is unchanged.");
    private static BackgroundRemovalException InvalidOutput() => new("invalid_worker_output", "Background removal returned an invalid image. Your original image is unchanged.");
}
