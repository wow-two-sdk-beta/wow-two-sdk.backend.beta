using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Integrations.Ghcr;

/// <summary>Wraps the GitHub Container Registry (GHCR) v2 API to confirm a published image tag exists.</summary>
/// <remarks>
/// Auth flow per probe:
///   1. Fetch an anonymous GHCR pull token (works for public images) and HEAD the manifest.
///   2. On 401/403, mint a pull token authenticated with the token from <see cref="IAccessTokenProvider"/>
///      (needs <c>read:packages</c>) via GHCR's Bearer realm, then re-HEAD — covers private images.
///   3. Still refused → report <see cref="ImageCheck.Unauthorized"/> rather than throwing.
/// </remarks>
internal sealed partial class GhcrClient(
    HttpClient http,
    IAccessTokenProvider tokenProvider,
    ILogger<GhcrClient> logger) : IContainerRegistryClient
{
    /// <summary>The manifest media types a HEAD must accept so GHCR returns the tag's manifest.</summary>
    private static readonly string[] ManifestMediaTypes =
    [
        "application/vnd.oci.image.index.v1+json",
        "application/vnd.oci.image.manifest.v1+json",
        "application/vnd.docker.distribution.manifest.list.v2+json",
        "application/vnd.docker.distribution.manifest.v2+json"
    ];

    /// <summary>Basic-auth username for the GHCR token request — GHCR validates the token (password), not this.</summary>
    private const string BasicAuthUser = "wow-two-sdk";

    /// <inheritdoc />
    public async Task<ImageCheck> ImageExistsAsync(string repo, string tag, CancellationToken ct)
    {
        // GHCR image paths are always lowercase regardless of the GitHub repo's casing.
        var imagePath = repo.ToLowerInvariant();

        // First attempt: an anonymous pull token (public images need no credentials).
        var anonymousToken = await GetPullTokenAsync(imagePath, null, ct);
        var anonymous = await ProbeManifestAsync(imagePath, tag, anonymousToken, ct);
        if (anonymous is ImageCheck.Exists or ImageCheck.Missing or ImageCheck.Failed)
            return anonymous;

        // Anonymous was refused → the image may be private. Mint a pull token authenticated with the
        // configured access token (needs read:packages), then re-probe.
        var authToken = await tokenProvider.GetAccessTokenAsync(ct);
        if (authToken is null)
            return ImageCheck.Unauthorized;

        var authenticatedToken = await GetPullTokenAsync(imagePath, authToken, ct);
        if (authenticatedToken is null)
            return ImageCheck.Unauthorized;

        return await ProbeManifestAsync(imagePath, tag, authenticatedToken, ct);
    }

    private async Task<ImageCheck> ProbeManifestAsync(string imagePath, string tag, string? token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, $"https://ghcr.io/v2/{imagePath}/manifests/{tag}");
        foreach (var mediaType in ManifestMediaTypes)
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            LogProbeUnreachable(ex, imagePath, tag);
            return ImageCheck.Failed;
        }

        using (response)
        {
            return response.StatusCode switch
            {
                HttpStatusCode.OK => ImageCheck.Exists,
                HttpStatusCode.NotFound => ImageCheck.Missing,
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ImageCheck.Unauthorized,
                _ => Unexpected(imagePath, tag, response.StatusCode)
            };
        }
    }

    private async Task<string?> GetPullTokenAsync(string imagePath, string? accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"https://ghcr.io/token?service=ghcr.io&scope=repository:{imagePath}:pull");

        // For a private image, authenticate the token request with the access token. GHCR validates
        // the token (the Basic username is ignored), then mints a pull-scoped registry bearer.
        if (accessToken is not null)
        {
            var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{BasicAuthUser}:{accessToken}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            LogTokenRequestFailed(ex, imagePath);
            return null;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                return null;

            try
            {
                var payload = await response.Content.ReadFromJsonAsync<GhcrToken>(ct);
                return string.IsNullOrWhiteSpace(payload?.Token) ? null : payload.Token;
            }
            catch (Exception ex) when (ex is JsonException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                LogTokenParseFailed(ex, imagePath);
                return null;
            }
        }
    }

    private ImageCheck Unexpected(string imagePath, string tag, HttpStatusCode status)
    {
        LogUnexpectedStatus(imagePath, tag, status);
        return ImageCheck.Failed;
    }

    [LoggerMessage(EventId = 5001, Level = LogLevel.Warning, Message = "GHCR manifest probe for {Image}:{Tag} failed to reach the registry.")]
    private partial void LogProbeUnreachable(Exception exception, string image, string tag);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Warning, Message = "GHCR token request for {Image} failed.")]
    private partial void LogTokenRequestFailed(Exception exception, string image);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Warning, Message = "GHCR token for {Image} could not be parsed.")]
    private partial void LogTokenParseFailed(Exception exception, string image);

    [LoggerMessage(EventId = 5004, Level = LogLevel.Warning, Message = "GHCR manifest probe for {Image}:{Tag} returned an unexpected status {Status}.")]
    private partial void LogUnexpectedStatus(string image, string tag, HttpStatusCode status);

    /// <summary>The token slice of a GHCR auth response.</summary>
    private sealed record GhcrToken([property: JsonPropertyName("token")] string? Token);
}
